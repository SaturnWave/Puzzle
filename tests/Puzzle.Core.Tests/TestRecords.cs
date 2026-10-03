using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Recording;
using Puzzle.Tooling;

namespace Puzzle.Core.Tests;

/// <summary>Builders and fakes for the attempt-recording tests.</summary>
internal static class TestRecords
{
    public static readonly DateTimeOffset Noon = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    public static readonly DeviceId Phone = new("0123456789abcdef0123456789abcdef");

    /// <summary>
    /// A record of <paramref name="level"/>, on seed 42 and the level's move limit unless told otherwise, ending three
    /// minutes after <see cref="Noon"/> unless told otherwise; goals are complete for a win and half done otherwise.
    /// </summary>
    public static AttemptRecord Of(
        LevelDefinition level,
        AttemptResult result,
        int movesUsed,
        int? attempt = 0,
        LabDetails? lab = null,
        DeviceId? device = null,
        AttemptSeeds? seeds = null,
        int? moveLimit = null,
        DateTimeOffset? endedUtc = null)
    {
        var start = new AttemptStart(level.Id, moveLimit ?? level.MoveLimit, seeds ?? AttemptSeeds.From(42), attempt, device ?? Phone, "web-lab", Noon);
        GoalOutcome[] goals = level.Goals
            .Select(goal => new GoalOutcome(goal, result == AttemptResult.Won ? goal.Target : goal.Target / 2))
            .ToArray();
        var outcome = new AttemptOutcome(result, movesUsed, goals, extraMovesBought: false, boostersUsed: Array.Empty<string>(), endedUtc ?? Noon.AddMinutes(3));
        return new AttemptRecord(start, outcome, lab);
    }

    /// <summary>Lab details of an attempt on <paramref name="level"/> with two moves played.</summary>
    public static LabDetails Lab(LevelDefinition level, int hints = 0, int botMoves = 0, int assist = 0, string key = "generated/level_0001") => new(
        levelKey: key,
        levelFingerprint: LevelJson.Fingerprint(level),
        assistPercent: assist,
        hintsShown: hints,
        botMoves: botMoves,
        moves: new[] { Move.Swap(new Position(1, 1), new Position(1, 2)), Move.Tap(new Position(3, 4)) });
}

/// <summary>A folder under the system temp path, deleted with everything in it on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "puzzle-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

/// <summary>
/// A clock the test moves by hand, for the sessions and for the session cache alike. The wall clock and the measured
/// elapsed time advance together, unless the wall clock alone is stepped.
/// </summary>
internal sealed class ManualClock : TimeProvider, ISystemClock
{
    private long _elapsedTicks;

    public DateTimeOffset Now { get; private set; } = TestRecords.Noon;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    DateTimeOffset ISystemClock.UtcNow => Now;

    public void Advance(TimeSpan time)
    {
        Now += time;
        _elapsedTicks += time.Ticks;
    }

    /// <summary>The wall clock jumps, as after a time sync; the time measured since is untouched.</summary>
    public void StepWallClock(TimeSpan step) => Now += step;

    public override DateTimeOffset GetUtcNow() => Now;

    public override long GetTimestamp() => _elapsedTicks;
}

internal sealed record LogEntry(LogLevel Level, Exception? Exception);

/// <summary>Keeps every log entry for the test to inspect; eviction callbacks log from thread-pool threads.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Enqueue(new LogEntry(logLevel, exception));
}
