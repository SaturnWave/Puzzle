using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Players;
using Puzzle.Web.Api;
using Puzzle.Web.Recording;
using Puzzle.Web.Sessions;

namespace Puzzle.Web.Players;

/// <summary>What a request for the player's next attempt came to. Closed set (private constructor).</summary>
public abstract record StartOutcome
{
    private StartOutcome()
    {
    }

    /// <summary>The player's game after the request: a new attempt, the live one going on, or none once every level is won.</summary>
    public sealed record Started(PlayerGameDto Game) : StartOutcome;

    /// <summary>
    /// The player has opened as many new attempts as anyone may in so short a time (<see cref="AttemptBudget"/>).
    /// Nothing changed: the attempt they were in is still theirs, and after <see cref="RetryAfter"/> they may go on.
    /// </summary>
    public sealed record TooManyAttempts(TimeSpan RetryAfter) : StartOutcome;
}

/// <summary>What a player's move came to. Closed set (private constructor).</summary>
public abstract record MoveOutcome
{
    private MoveOutcome()
    {
    }

    /// <summary>The engine was asked; <see cref="PlayerMoveResultDto.Accepted"/> says whether it took the move.</summary>
    public sealed record Played(PlayerMoveResultDto Result) : MoveOutcome;

    /// <summary>The player has no live attempt.</summary>
    public sealed record NoLiveAttempt : MoveOutcome;

    /// <summary>
    /// The move was made on another attempt than the live one, or on an earlier board of it: the page that sent it is
    /// behind (a second tab, a request sent twice). Nothing was played.
    /// </summary>
    public sealed record AttemptChanged : MoveOutcome;

    /// <summary>The live attempt is already won or lost.</summary>
    public sealed record AttemptOver : MoveOutcome;
}

/// <summary>A move with what its page was looking at: the attempt, and how many moves that attempt had used there.</summary>
public sealed record PlayerMove(Guid Attempt, int MovesSeen, Move Move);

/// <summary>
/// The public mode's game, as in the released one: a player works through the level set in order, a win opens the next
/// level and a loss means the same level again. The server owns everything — which level, which attempt number and so
/// which seeds, the game and its record — and a player has one live attempt at most. The player only sends moves.
/// </summary>
/// <remarks>
/// <para>
/// A live attempt is the player's until they go on to the next one: it has no idle limit, and it is kept in the player
/// store move by move (<see cref="SavedAttempt"/>), so after a restart it is rebuilt by playing its moves again.
/// </para>
/// <para>
/// A store that cannot be written never fails a move: what could not be saved is kept and tried again — when the
/// player next asks for their game, once a minute (<see cref="SaveUnsavedProgress"/>), and when the server stops.
/// </para>
/// <para>
/// One small seat per player who has played since the server started, each with one game at most, so never more than
/// the store has accounts. Each player's requests are handled one at a time, under the seat's gate; the game's own
/// gate is taken inside it.
/// </para>
/// </remarks>
public sealed class PlayerGames
{
    private readonly PublicLevelSet _levels;
    private readonly PlayerStore _store;
    private readonly AttemptRecorder _recorder;
    private readonly TimeProvider _clock;
    private readonly ILogger<PlayerGames> _logger;
    private readonly ConcurrentDictionary<DeviceId, Seat> _seats = new();

    public PlayerGames(PublicLevelSet levels, PlayerStore store, AttemptRecorder recorder, TimeProvider clock, ILogger<PlayerGames> logger)
    {
        _levels = levels;
        _store = store;
        _recorder = recorder;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>The player's progress and their live attempt, if they have one: what a page shows when it loads.</summary>
    public PlayerGameDto Current(DeviceId playerId)
    {
        Seat seat = SeatOf(playerId);
        lock (seat.Gate)
        {
            RebuildSavedAttempt(seat, playerId);
            SaveOrKeepUnsaved(seat, playerId);
            return GameOf(seat, playerId);
        }
    }

    /// <summary>
    /// Opens the player's next attempt on the level they stand on, in place of the live one. A live attempt goes on
    /// instead, as it is, in two cases: nobody has moved in it yet (asking twice deals the same game), or it is being
    /// played and the request does not name it. An attempt being played is left, and recorded as abandoned, only when
    /// <paramref name="leaving"/> names it. With every level won there is nothing to open. A player who has opened
    /// new attempts faster than anyone plays them is refused for a while, and keeps the attempt they were in.
    /// </summary>
    /// <param name="leaving">The attempt the page that asks was showing; absent when it showed none.</param>
    /// <exception cref="LevelInitializationException">The level cannot produce a starting board.</exception>
    /// <exception cref="IOException">The store could not be written; the player's game is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write the store; the player's game is as it was.</exception>
    public StartOutcome Start(DeviceId playerId, Guid? leaving = null)
    {
        Seat seat = SeatOf(playerId);
        lock (seat.Gate)
        {
            RebuildSavedAttempt(seat, playerId);
            if (seat.Live is { } live && GoesOn(live, leaving))
            {
                return new StartOutcome.Started(GameOf(seat, playerId));
            }

            if (seat.NewAttempts.UsedUpFor() is { } retryAfter)
            {
                return new StartOutcome.TooManyAttempts(retryAfter);
            }

            SaveUnsaved(seat, playerId); // going on needs what came before it saved first
            GoOn(seat, playerId);
            return new StartOutcome.Started(GameOf(seat, playerId));
        }
    }

    /// <summary>
    /// Plays the move in the player's live attempt, when that is still the attempt, and the board, its page was looking
    /// at. A move that wins the level opens the next one.
    /// </summary>
    public MoveOutcome Play(DeviceId playerId, PlayerMove move)
    {
        Seat seat = SeatOf(playerId);
        lock (seat.Gate)
        {
            RebuildSavedAttempt(seat, playerId);
            if (seat.Live is not { } live)
            {
                return new MoveOutcome.NoLiveAttempt();
            }

            if (live.Id != move.Attempt)
            {
                return new MoveOutcome.AttemptChanged();
            }

            lock (live.Session.Gate)
            {
                return PlayIn(live, seat, playerId, move);
            }
        }
    }

    /// <summary>
    /// Tries again to write the progress the store refused before, for every player who has some; returns how many
    /// players' progress is still unsaved after it.
    /// </summary>
    public int SaveUnsavedProgress()
    {
        int stillUnsaved = 0;
        foreach ((DeviceId playerId, Seat seat) in _seats)
        {
            lock (seat.Gate)
            {
                SaveOrKeepUnsaved(seat, playerId);
                stillUnsaved += seat.Unsaved is null ? 0 : 1;
            }
        }

        return stillUnsaved;
    }

    /// <summary>The session of the player's live attempt, for the tests to choose legal moves from; hold its gate while reading the game.</summary>
    internal bool TryGetLiveSession(DeviceId playerId, [NotNullWhen(true)] out GameSession? session)
    {
        Seat seat = SeatOf(playerId);
        lock (seat.Gate)
        {
            RebuildSavedAttempt(seat, playerId);
            session = seat.Live?.Session;
            return session is not null;
        }
    }

    /// <summary>
    /// Whether a start leaves the live attempt as it is. A decided one never stays. One nobody has moved in is simply
    /// dealt again. One being played stays unless the start names it: a page that was looking at another attempt (a
    /// second tab, left open) must not end, unseen, the one the player is in.
    /// </summary>
    private static bool GoesOn(LiveAttempt live, Guid? leaving)
    {
        GameSession session = live.Session;
        lock (session.Gate)
        {
            if (session.Game.Status.IsOver())
            {
                return false;
            }

            bool untouched = session.MovesPlayed.Count == 0;
            return untouched || leaving != live.Id;
        }
    }

    /// <summary>The progress after a move was accepted in <paramref name="live"/>. Hold the session's gate.</summary>
    private static PlayerProgress AfterMove(PlayerProgress before, LiveAttempt live)
    {
        // The first move is what uses up the attempt's retry number: one only opened comes again as it was.
        bool firstMove = live.Session.MovesPlayed.Count == 1;
        PlayerProgress counted = firstMove ? before.WithAttemptStartedOn(live.Level.Fingerprint) : before;
        PlayerProgress played = counted.WithLive(live.ToSaved());
        return live.Session.Game.Status == GameStatus.Won ? played.WithLevelWon(live.Level.Id) : played;
    }

    private Seat SeatOf(DeviceId playerId) => _seats.GetOrAdd(playerId, static (_, clock) => new Seat(clock), _clock);

    /// <summary>
    /// Once for each seat: the attempt the store saved for the player becomes their live attempt again. A server that
    /// just started finds its players' attempts this way. Hold the seat's gate.
    /// </summary>
    private void RebuildSavedAttempt(Seat seat, DeviceId playerId)
    {
        if (seat.SavedAttemptRebuilt)
        {
            return;
        }

        seat.SavedAttemptRebuilt = true;
        if (AccountOf(playerId).Progress.Live is { } saved)
        {
            seat.Live = Rebuild(saved, playerId);
        }
    }

    /// <summary>
    /// The saved attempt as a game again: the level started with the saved seeds, then every saved move in order.
    /// Absent, with the reason in the log, when the set no longer has the level as the attempt knew it, or the game
    /// does not take the moves; the player then starts the level afresh.
    /// </summary>
    private LiveAttempt? Rebuild(SavedAttempt saved, DeviceId playerId)
    {
        if (!_levels.TryFind(saved.Level, out PublicLevel? level) || level.Fingerprint != saved.LevelFingerprint)
        {
            Log.SavedAttemptLevelChanged(_logger, saved.Level.Value);
            return null;
        }

        try
        {
            GameSession session = Begin(level, saved.Seeds, saved.Attempt, playerId, saved.StartedUtc);
            if (session.TryReplay(saved.Moves))
            {
                return new LiveAttempt(saved.Id, level, saved.Attempt, session);
            }

            Log.SavedAttemptNotReplayed(_logger, saved.Level.Value, saved.Moves.Count);
            return null;
        }
        catch (LevelInitializationException ex)
        {
            Log.SavedAttemptNotStarted(_logger, saved.Level.Value, ex);
            return null;
        }
    }

    /// <summary>
    /// Puts the attempt on the level the player now stands on in the place of the live one, or nothing once every level
    /// is won. The store is told first: only when it has the new attempt has the player really left the old one, and
    /// only then does the new one count against their budget. Hold the seat's gate.
    /// </summary>
    private void GoOn(Seat seat, DeviceId playerId)
    {
        PlayerProgress progress = ProgressOf(seat, playerId);
        LiveAttempt? next = _levels.TryGetNext(progress, out PublicLevel? level) ? Open(level, progress, playerId) : null;
        Save(seat, playerId, progress.WithLive(next?.ToSaved()));
        RecordLeft(seat.Live);
        seat.Live = next;
        if (next is not null)
        {
            seat.NewAttempts.Spend();
        }
    }

    /// <summary>
    /// The player's next attempt on <paramref name="level"/>: its retry number is the count of attempts they have
    /// played on it, so an attempt that was opened and never moved in is dealt again, number, seeds and board.
    /// </summary>
    /// <exception cref="LevelInitializationException">The level cannot produce a starting board.</exception>
    private LiveAttempt Open(PublicLevel level, PlayerProgress progress, DeviceId playerId)
    {
        int attempt = progress.AttemptsOn(level.Fingerprint);
        AttemptSeeds seeds = level.Entry.Level.Attempts.SeedsFor(attempt, (uint)Random.Shared.Next());
        return new LiveAttempt(Guid.NewGuid(), level, attempt, Begin(level, seeds, attempt, playerId, startedUtc: null));
    }

    /// <summary>
    /// The game of the player's retry <paramref name="attempt"/> under the level's own rules, numbered here: the only
    /// kind a bot can be set beside. Past the level's seed pool the session finds it a replay and records it without a
    /// number.
    /// </summary>
    /// <exception cref="LevelInitializationException">The level cannot produce a starting board.</exception>
    private GameSession Begin(PublicLevel level, AttemptSeeds seeds, int attempt, DeviceId playerId, DateTimeOffset? startedUtc)
    {
        AttemptPolicy policy = level.Entry.Level.Attempts;
        var origin = new AttemptOrigin(attempt, policy.SeedPool.Count > 0, policy.Board, InSequence: true, DeviceKept: true);
        return GameSession.Begin(level.Entry, seeds, origin, assist: 0, playerId, WebClient.Public, _clock, startedUtc);
    }

    /// <summary>
    /// The player left <paramref name="left"/> for their next attempt: undecided, with moves played, it is recorded as
    /// abandoned now. A decided attempt made its record when it was decided.
    /// </summary>
    private void RecordLeft(LiveAttempt? left)
    {
        if (left is null)
        {
            return;
        }

        GameSession session = left.Session;
        lock (session.Gate)
        {
            if (!session.Game.Status.IsOver())
            {
                _recorder.RecordAbandoned(session, session.NowUtc());
            }
        }
    }

    /// <summary>
    /// Plays the move, records the attempt if the move decided it, and saves the attempt as it now stands, with a win
    /// counted. Hold the seat's gate and the session's.
    /// </summary>
    private MoveOutcome PlayIn(LiveAttempt live, Seat seat, DeviceId playerId, PlayerMove move)
    {
        GameSession session = live.Session;
        Game game = session.Game;
        if (game.Status.IsOver())
        {
            return new MoveOutcome.AttemptOver();
        }

        if (game.MovesUsed != move.MovesSeen)
        {
            return new MoveOutcome.AttemptChanged();
        }

        bool accepted = game.TryPlay(move.Move);
        if (accepted)
        {
            session.MovePlayed(move.Move, MoveSource.Player);
            _recorder.RecordIfDecided(session);
            SaveOrKeep(seat, playerId, AfterMove(ProgressOf(seat, playerId), live));
        }

        return new MoveOutcome.Played(new PlayerMoveResultDto(
            accepted,
            accepted ? game.LastEvents.Select(StateMapper.ToEvent).ToArray() : Array.Empty<EventDto>(),
            StateMapper.ToState(game),
            ToDto(ProgressOf(seat, playerId))));
    }

    /// <summary>The player's progress as it truly is: what the store could not be told yet, or else what the store holds.</summary>
    private PlayerProgress ProgressOf(Seat seat, DeviceId playerId) => seat.Unsaved ?? AccountOf(playerId).Progress;

    /// <exception cref="IOException">The store could not be written; it and the seat are as they were.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write the store; it and the seat are as they were.</exception>
    private void Save(Seat seat, DeviceId playerId, PlayerProgress progress)
    {
        _store.UpdateProgress(playerId, _ => progress);
        seat.Unsaved = null;
    }

    /// <exception cref="IOException">The store still cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write the store.</exception>
    private void SaveUnsaved(Seat seat, DeviceId playerId)
    {
        if (seat.Unsaved is { } unsaved)
        {
            Save(seat, playerId, unsaved);
        }
    }

    /// <summary>
    /// Saves <paramref name="progress"/>, or keeps it for the next try when the store cannot be written: a full disk or
    /// a file another program holds must fail no move, and must lose neither a move nor a win.
    /// </summary>
    private void SaveOrKeep(Seat seat, DeviceId playerId, PlayerProgress progress)
    {
        try
        {
            Save(seat, playerId, progress);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            seat.Unsaved = progress;
            Log.ProgressNotSaved(_logger, _store.Path, ex);
        }
    }

    private void SaveOrKeepUnsaved(Seat seat, DeviceId playerId)
    {
        if (seat.Unsaved is { } unsaved)
        {
            SaveOrKeep(seat, playerId, unsaved);
        }
    }

    private PlayerGameDto GameOf(Seat seat, DeviceId playerId) => new(ToDto(ProgressOf(seat, playerId)), AttemptOf(seat));

    private PlayerProgressDto ToDto(PlayerProgress progress) => _levels.TryGetNext(progress, out PublicLevel? level)
        ? new PlayerProgressDto(level.Number, _levels.Count, progress.AttemptsOn(level.Fingerprint), Finished: false)
        : new PlayerProgressDto(_levels.Count, _levels.Count, AttemptsOnLevel: 0, Finished: true);

    private static PlayerAttemptDto? AttemptOf(Seat seat)
    {
        if (seat.Live is not { } live)
        {
            return null;
        }

        lock (live.Session.Gate)
        {
            return new PlayerAttemptDto(live.Id.ToString("N"), live.Level.Number, live.Attempt + 1, StateMapper.ToState(live.Session.Game));
        }
    }

    /// <exception cref="InvalidOperationException">No account has the id: the caller let through a player the store does not know.</exception>
    private PlayerAccount AccountOf(DeviceId playerId) => _store.TryFind(playerId, out PlayerAccount? account)
        ? account
        : throw new InvalidOperationException("A game was asked for a player without an account.");

    /// <summary>
    /// One player's place at the table: their gate, their live attempt, the new attempts they may still open, and what
    /// the store has yet to be told.
    /// </summary>
    private sealed class Seat
    {
        public Seat(TimeProvider clock)
        {
            NewAttempts = new AttemptBudget(clock);
        }

        public object Gate { get; } = new();

        public AttemptBudget NewAttempts { get; }

        public LiveAttempt? Live { get; set; }

        /// <summary>Whether the attempt the store saved for the player was looked for; it is, once, before anything else is done at the seat.</summary>
        public bool SavedAttemptRebuilt { get; set; }

        /// <summary>The player's progress as it truly is, while the store could not be written; absent when the store has it.</summary>
        public PlayerProgress? Unsaved { get; set; }
    }

    /// <summary>The attempt a player is in.</summary>
    /// <param name="Id">Names the attempt to the player's page; it stays the same when the attempt is rebuilt.</param>
    /// <param name="Attempt">The player's 0-based retry number on the level.</param>
    private sealed record LiveAttempt(Guid Id, PublicLevel Level, int Attempt, GameSession Session)
    {
        /// <summary>The attempt as the store keeps it. Hold the session's gate once others may play in it.</summary>
        public SavedAttempt ToSaved() =>
            new(Id, Level.Id, Level.Fingerprint, Attempt, Session.Seeds, Session.Start.StartedUtc, Session.MovesPlayed);
    }
}
