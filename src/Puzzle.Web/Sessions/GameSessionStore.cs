using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Recording;

namespace Puzzle.Web.Sessions;

/// <summary>Why an attempt stands outside its device's sequence of retries, so its record holds no retry number.</summary>
public enum SequenceBreak
{
    /// <summary>Seeds, or a retry number, asked for by hand: possibly a game the device has played.</summary>
    ChosenByHand,

    /// <summary>The lab's start-board override: a game the level itself would not give.</summary>
    StartBoardOverride,

    /// <summary>The browser keeps no device id beyond this tab or page load (site data blocked): the numbering restarts with it.</summary>
    DeviceNotKept,

    /// <summary>A retry past the level's seed pool, which cycles: it replays a game the device has played.</summary>
    PoolReplay,
}

/// <summary>Where an attempt's seeds came from.</summary>
/// <param name="Attempt">The retry number the seeds were drawn for; absent for seeds given directly.</param>
/// <param name="FromPool">Whether the level's calibrated pool supplied the play seed.</param>
/// <param name="StartBoard">The start-board rule the seeds follow.</param>
/// <param name="InSequence">
/// Whether <paramref name="Attempt"/> is the device's next retry, numbered by the server (<see cref="RetryNumbers"/>). A
/// retry number asked for by hand may replay a game the device has seen, so it is not a real retry count.
/// </param>
/// <param name="DeviceKept">
/// Whether the browser keeps its device id across page loads and tabs. Without that, the server's numbering starts
/// again with every new id, so even its next retry may be one the person has played.
/// </param>
public sealed record AttemptOrigin(int? Attempt, bool FromPool, StartBoard StartBoard, bool InSequence, bool DeviceKept)
{
    /// <summary>
    /// Why this attempt of <paramref name="level"/> stands outside the device's sequence of retries; absent when it is
    /// the device's next retry under the level's own rules, the only kind a bot can be set beside.
    /// </summary>
    public SequenceBreak? SequenceBreakOn(LevelDefinition level)
    {
        if (!InSequence || Attempt is not int attempt)
        {
            return SequenceBreak.ChosenByHand;
        }

        if (StartBoard != level.Attempts.Board)
        {
            return SequenceBreak.StartBoardOverride;
        }

        if (!DeviceKept)
        {
            return SequenceBreak.DeviceNotKept;
        }

        int poolSize = level.Attempts.SeedPool.Count;
        return poolSize > 0 && attempt >= poolSize ? SequenceBreak.PoolReplay : null;
    }
}

/// <summary>How far an attempt's one record has come.</summary>
public enum RecordProgress
{
    /// <summary>The attempt is still being played: there is no record yet.</summary>
    Open,

    /// <summary>The record is made, but the log could not be written; the recorder tries again (<see cref="AttemptRecorder.RetryWaiting"/>).</summary>
    Waiting,

    /// <summary>The record is in the log.</summary>
    Written,

    /// <summary>The record could not be made, or gave way while it waited: it will never be written.</summary>
    Lost,
}

/// <summary>Who chose a move.</summary>
public enum MoveSource
{
    Player,
    Bot,
}

/// <summary>Which face of the web app an attempt is played in; its record names it as the client.</summary>
public enum WebClient
{
    /// <summary>The owner's test lab, with its tools.</summary>
    Lab,

    /// <summary>The public mode's player page: a signed-in player, no tools. Its attempts are kept with the player, not in the session store.</summary>
    Public,
}

/// <summary>
/// One browser attempt. <see cref="Game"/> and the attempt's history are not thread-safe: hold <see cref="Gate"/>
/// while touching them.
/// </summary>
public sealed class GameSession
{
    private readonly List<Move> _moves = new();
    private readonly TimeProvider _clock;
    private readonly long _openedAt;
    private readonly DateTimeOffset _openedUtc;
    private volatile RecordProgress _record;

    /// <param name="sequenceBreak">Why the attempt stands outside its device's retries; absent when it is the next one.</param>
    private GameSession(
        string id,
        LibraryLevel level,
        AttemptOrigin origin,
        SequenceBreak? sequenceBreak,
        int assist,
        Game game,
        AttemptStart start,
        TimeProvider clock)
    {
        Id = id;
        Level = level;
        Origin = origin;
        SequenceBreak = sequenceBreak;
        Assist = assist;
        Game = game;
        Start = start;
        _clock = clock;
        _openedAt = clock.GetTimestamp();

        // An attempt rebuilt after a restart started long before this session; no session is made before its attempt started.
        DateTimeOffset now = clock.GetUtcNow();
        _openedUtc = now > start.StartedUtc ? now : start.StartedUtc;
        LastActivityUtc = start.StartedUtc;
        BotRng = new Rng(Rng.Derive(start.Seeds.Play, 99));
    }

    public string Id { get; }

    public LibraryLevel Level { get; }

    public AttemptSeeds Seeds => Start.Seeds;

    public AttemptOrigin Origin { get; }

    /// <summary>Why the attempt stands outside its device's sequence of retries; absent when it is the next one.</summary>
    public SequenceBreak? SequenceBreak { get; }

    public int Assist { get; }

    public Game Game { get; }

    /// <summary>The attempt as its record will begin: level, seeds, device, start time.</summary>
    public AttemptStart Start { get; }

    /// <summary>Random choices of bots and hints in this session (separate from the game's own streams).</summary>
    public Rng BotRng { get; }

    public object Gate { get; } = new();

    /// <summary>Every accepted move in order, whoever chose it.</summary>
    public IReadOnlyList<Move> MovesPlayed => _moves;

    public int BotMoves { get; private set; }

    /// <summary>Moves suggested to the player: hint and solver requests.</summary>
    public int HintsShown { get; private set; }

    /// <summary>When a move was last played or suggested; the start until then.</summary>
    public DateTimeOffset LastActivityUtc { get; private set; }

    /// <summary>
    /// How far the attempt's one record has come. Only the recorder moves it on: from <see cref="RecordProgress.Open"/>
    /// while holding the gate, and a waiting record under its own lock, so it may be read without the gate.
    /// </summary>
    public RecordProgress Record
    {
        get => _record;
        internal set => _record = value;
    }

    /// <summary>
    /// Whether the attempt is over: decided by a move, or recorded as abandoned. What happens after that (the solver run
    /// on a lost game, say) no longer shapes the attempt.
    /// </summary>
    public bool HasEnded => Game.Status.IsOver() || Record != RecordProgress.Open;

    /// <summary>
    /// How the attempt's record will stand in the bot comparison (<see cref="AttemptCounting.Standing"/>), shown to the
    /// player while they play: help from the lab first (the <see cref="LabDetails.IsAssisted"/> rule), then the seeds' origin.
    /// </summary>
    public AttemptStanding Standing
    {
        get
        {
            if (LabDetails.IsAssistedBy(Assist, HintsShown, BotMoves))
            {
                return AttemptStanding.Assisted;
            }

            return Start.Attempt is null ? AttemptStanding.ChosenSeeds : AttemptStanding.Comparable;
        }
    }

    /// <summary>
    /// Starts an attempt: the level's game from <paramref name="seeds"/>, and the session that keeps its history for
    /// the record.
    /// </summary>
    /// <param name="device">The lab browser's device id, or in the public mode the player's id.</param>
    /// <param name="startedUtc">
    /// When the attempt started, for one that began before this session: a player's attempt rebuilt after a restart
    /// (<see cref="TryReplay"/>). Now when absent.
    /// </param>
    /// <exception cref="LevelInitializationException">The level cannot produce a starting board.</exception>
    public static GameSession Begin(
        LibraryLevel level,
        AttemptSeeds seeds,
        AttemptOrigin origin,
        int assist,
        DeviceId device,
        WebClient client,
        TimeProvider clock,
        DateTimeOffset? startedUtc = null)
    {
        Game game = Game.Start(level.Level, seeds, new GameOptions { RecordEvents = true, AssistPercent = assist });

        // Only the device's next retry under the level's own rules is a real retry count; any other attempt is
        // recorded without a number, as hand-chosen, and left out of the comparison with the bots.
        SequenceBreak? sequenceBreak = origin.SequenceBreakOn(level.Level);
        var start = new AttemptStart(
            level: level.Level.Id,
            moveLimit: game.MoveLimit,
            seeds: seeds,
            attempt: sequenceBreak is null ? origin.Attempt : null,
            device: device,
            client: ClientName(client),
            startedUtc: startedUtc ?? clock.GetUtcNow());
        return new GameSession(Guid.NewGuid().ToString("N"), level, origin, sequenceBreak, assist, game, start, clock);
    }

    /// <summary>
    /// The time on the attempt's own clock: when this session was made, plus the time measured since. The wall clock
    /// may step back (a time sync after sleep); this never does, so an attempt never ends before it starts.
    /// </summary>
    public DateTimeOffset NowUtc() => _openedUtc + _clock.GetElapsedTime(_openedAt);

    public void MovePlayed(Move move, MoveSource source)
    {
        _moves.Add(move);
        if (source == MoveSource.Bot)
        {
            BotMoves++;
        }

        LastActivityUtc = NowUtc();
    }

    /// <summary>
    /// Plays again, as the player's own, the moves an earlier session of this attempt kept, in their order: the engine
    /// is deterministic, so the same level, seeds and moves give the same board. False when the game refuses one of
    /// them: they are then not the moves of this level and these seeds. Only for a session nobody has played in.
    /// </summary>
    public bool TryReplay(IEnumerable<Move> moves)
    {
        foreach (Move move in moves)
        {
            if (!Game.TryPlay(move))
            {
                return false;
            }

            _moves.Add(move);
        }

        return true;
    }

    /// <summary>A move was suggested to the player; once the attempt has ended, help no longer counts against it.</summary>
    public void HintShown()
    {
        if (HasEnded)
        {
            return;
        }

        HintsShown++;
        LastActivityUtc = NowUtc();
    }

    private static string ClientName(WebClient client) => client switch
    {
        WebClient.Lab => AttemptRecorder.Client,
        WebClient.Public => AttemptRecorder.PublicClient,
        _ => throw new ArgumentOutOfRangeException(nameof(client), client, "Unknown web client."),
    };
}

/// <summary>
/// The lab's sessions, in memory, with a hard size limit and idle expiry, so an open browser tab can never grow the
/// server without bound. A session that leaves the store unfinished is recorded as an abandoned attempt.
/// </summary>
public sealed class GameSessionStore
{
    private readonly MemoryCache _cache;
    private readonly TimeSpan _idleTimeout;
    private readonly AttemptRecorder _recorder;
    private readonly TimeProvider _clock;

    /// <summary>The sessions in the cache, which cannot list its entries; as bounded as the cache, since eviction removes them.</summary>
    private readonly ConcurrentDictionary<string, GameSession> _live = new();

    public GameSessionStore(MemoryCache cache, TimeSpan idleTimeout, AttemptRecorder recorder, TimeProvider clock)
    {
        _cache = cache;
        _idleTimeout = idleTimeout;
        _recorder = recorder;
        _clock = clock;
    }

    /// <param name="device">The lab browser's device id.</param>
    /// <exception cref="LevelInitializationException">The level cannot produce a starting board.</exception>
    public GameSession Create(LibraryLevel level, AttemptSeeds seeds, AttemptOrigin origin, int assist, DeviceId device)
    {
        GameSession session = GameSession.Begin(level, seeds, origin, assist, device, WebClient.Lab, _clock);
        var entry = new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = _idleTimeout };
        entry.RegisterPostEvictionCallback(OnEvicted);

        // Listed before it is cached: a session refused for lack of room is evicted, and unlisted, at once.
        _live[session.Id] = session;
        _cache.Set(session.Id, session, entry);
        return session;
    }

    public bool TryGet(string id, [NotNullWhen(true)] out GameSession? session) => _cache.TryGetValue(id, out session);

    /// <summary>
    /// Drops the session <paramref name="id"/> because its player moved on to another attempt; an unfinished attempt is
    /// recorded as abandoned now. An unknown id is ignored: the session may have expired (and been recorded) already.
    /// </summary>
    public void Leave(string id)
    {
        if (!TryGet(id, out GameSession? session))
        {
            return;
        }

        lock (session.Gate)
        {
            _recorder.RecordAbandoned(session, session.NowUtc());
        }

        _cache.Remove(id);
    }

    /// <summary>
    /// Drops the sessions idle longer than the timeout now; the cache by itself notices them only when it is next used.
    /// Each is recorded as abandoned at its last activity.
    /// </summary>
    public void RemoveIdleSessions() => _cache.Compact(0); // compacting removes every expired entry, and here nothing else

    /// <summary>
    /// Records every attempt still being played as abandoned at its last activity, for when the lab stops: the cache is
    /// then disposed without eviction callbacks.
    /// </summary>
    public void RecordUnfinishedAttempts()
    {
        foreach (GameSession session in _live.Values)
        {
            lock (session.Gate)
            {
                _recorder.RecordAbandoned(session, session.LastActivityUtc);
            }
        }
    }

    /// <summary>
    /// Runs on a thread-pool thread once the cache has dropped a session: idle expiry, lack of room, or
    /// <see cref="Leave"/>. An attempt still unrecorded was abandoned at its last activity.
    /// </summary>
    private void OnEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (value is GameSession session)
        {
            _live.TryRemove(session.Id, out _);
            lock (session.Gate)
            {
                _recorder.RecordAbandoned(session, session.LastActivityUtc);
            }
        }
    }
}
