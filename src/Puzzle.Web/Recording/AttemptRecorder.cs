using Puzzle.Core.Engine;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Sessions;

namespace Puzzle.Web.Recording;

/// <summary>
/// Writes one record per lab attempt (docs/GAME_DESIGN.md §20) when it ends: decided by a move, or abandoned. Recording
/// never breaks the game. A record that cannot be made is logged and lost; one the log refuses (a full disk, a file
/// another program keeps locked) waits, a bounded number of them, for <see cref="RetryWaiting"/>. The session says how
/// far its record has come (<see cref="GameSession.Record"/>), so the lab never calls an attempt recorded before it is.
/// </summary>
/// <remarks>
/// Call <see cref="RecordIfDecided"/> and <see cref="RecordAbandoned"/> while holding the session's gate. Waiting
/// records are handled under the recorder's own lock, which never waits for a session's gate.
/// </remarks>
public sealed class AttemptRecorder
{
    /// <summary>The client name in the lab's records.</summary>
    public const string Client = "web-lab";

    /// <summary>The client name in the records of the public mode's players.</summary>
    public const string PublicClient = "web-public";

    /// <summary>
    /// Records that may wait for the log at once: as many as the sessions the lab holds by default, each a few kilobytes
    /// at most. Past it, the record that has waited longest gives way.
    /// </summary>
    public const int DefaultMaxWaiting = 200;

    private readonly AttemptLog _log;
    private readonly ILogger<AttemptRecorder> _logger;
    private readonly int _maxWaiting;
    private readonly object _waitingGate = new();
    private readonly Queue<WaitingRecord> _waiting = new();

    /// <param name="maxWaiting">Records that may wait for the log at once; at least 1.</param>
    public AttemptRecorder(AttemptLog log, ILogger<AttemptRecorder> logger, int maxWaiting = DefaultMaxWaiting)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWaiting, 1);
        _log = log;
        _logger = logger;
        _maxWaiting = maxWaiting;
    }

    /// <summary>After a move: records the attempt when that move decided it.</summary>
    public void RecordIfDecided(GameSession session)
    {
        if (session.Game.Status.IsOver())
        {
            Record(session, session.NowUtc());
        }
    }

    /// <summary>The player left the attempt at <paramref name="leftAt"/>; an undecided one is recorded as abandoned.</summary>
    public void RecordAbandoned(GameSession session, DateTimeOffset leftAt)
    {
        // Picking a level in the lab starts an attempt at once; one left without a single move was never really tried.
        if (session.MovesPlayed.Count > 0)
        {
            Record(session, leftAt);
        }
    }

    /// <summary>
    /// Writes the waiting records, the one that has waited longest first, until the log refuses one again. Run now and
    /// then (the idle sweep does) and when the lab stops.
    /// </summary>
    public void RetryWaiting()
    {
        lock (_waitingGate)
        {
            while (_waiting.TryPeek(out WaitingRecord? next))
            {
                try
                {
                    _log.Append(next.Record);
                }
                catch (Exception ex) when (IsWriteFailure(ex))
                {
                    Log.RecordsStillWaiting(_logger, _waiting.Count, _log.Path, ex);
                    return;
                }

                _waiting.Dequeue();
                next.Session.Record = RecordProgress.Written;
                Log.AttemptRecorded(_logger, next.Record.Outcome.Result, next.Session.Level.Key, next.Record.Outcome.MovesUsed);
            }
        }
    }

    /// <summary>For when the lab stops: a last try at the waiting records; whatever still waits after it is lost.</summary>
    public void RetryWaitingBeforeStop()
    {
        RetryWaiting();
        lock (_waitingGate)
        {
            if (_waiting.Count == 0)
            {
                return;
            }

            Log.WaitingRecordsLost(_logger, _waiting.Count, _log.Path);
            foreach (WaitingRecord lost in _waiting)
            {
                lost.Session.Record = RecordProgress.Lost;
            }

            _waiting.Clear();
        }
    }

    private static bool IsWriteFailure(Exception ex) => ex is IOException or UnauthorizedAccessException;

    private static LabDetails LabDetailsOf(GameSession session) => new(
        levelKey: session.Level.Key,
        levelFingerprint: LevelJson.Fingerprint(session.Level.Level),
        assistPercent: session.Assist,
        hintsShown: session.HintsShown,
        botMoves: session.BotMoves,
        moves: session.MovesPlayed);

    private void Record(GameSession session, DateTimeOffset endedUtc)
    {
        if (session.Record != RecordProgress.Open)
        {
            return; // an attempt is recorded once
        }

        AttemptRecord record;
        try
        {
            record = new AttemptRecord(session.Start, AttemptOutcome.Of(session.Game, endedUtc), LabDetailsOf(session));
        }
        catch (ArgumentException ex)
        {
            // The record format refused what the lab handed it: a lab bug, never a reason to fail the player's move.
            Log.AttemptNotBuilt(_logger, session.Level.Key, ex);
            session.Record = RecordProgress.Lost;
            return;
        }

        try
        {
            _log.Append(record);
            session.Record = RecordProgress.Written;
            Log.AttemptRecorded(_logger, record.Outcome.Result, session.Level.Key, record.Outcome.MovesUsed);
        }
        catch (Exception ex) when (IsWriteFailure(ex))
        {
            Log.AttemptNotWritten(_logger, session.Level.Key, _log.Path, ex);
            Wait(new WaitingRecord(session, record));
        }
    }

    /// <summary>Keeps a record the log refused for <see cref="RetryWaiting"/>; past the bound, the one that has waited longest gives way.</summary>
    private void Wait(WaitingRecord waiting)
    {
        lock (_waitingGate)
        {
            waiting.Session.Record = RecordProgress.Waiting;
            _waiting.Enqueue(waiting);
            if (_waiting.Count <= _maxWaiting)
            {
                return;
            }

            WaitingRecord dropped = _waiting.Dequeue();
            dropped.Session.Record = RecordProgress.Lost;
            Log.WaitingRecordDropped(_logger, dropped.Session.Level.Key, _maxWaiting, _log.Path);
        }
    }

    /// <summary>A record the log refused, with the session that shows how far it has come.</summary>
    private sealed record WaitingRecord(GameSession Session, AttemptRecord Record);
}
