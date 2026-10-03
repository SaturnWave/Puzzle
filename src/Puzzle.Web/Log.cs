using Puzzle.Core.Recording;

namespace Puzzle.Web;

internal static partial class Log
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Skipped level {Path}: {Problem}")]
    public static partial void LevelSkipped(ILogger logger, string path, string problem);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Loaded {Count} levels from {Path}")]
    public static partial void LevelsLoaded(ILogger logger, int count, string path);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Recording attempts to {Path}, which holds {Count} records")]
    public static partial void RecordingTo(ILogger logger, string path, int count);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Recorded attempt: {Result} on {Level} after {Moves} moves")]
    public static partial void AttemptRecorded(ILogger logger, AttemptResult result, string level, int moves);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "Could not write the record of an attempt on {Level} to {Path}; it waits for another try and the game goes on")]
    public static partial void AttemptNotWritten(ILogger logger, string level, string path, Exception exception);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Error, Message = "Could not build the record of an attempt on {Level}; the game goes on")]
    public static partial void AttemptNotBuilt(ILogger logger, string level, Exception exception);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "Skipped line {Line} of the attempt records: {Problem}")]
    public static partial void RecordSkipped(ILogger logger, int line, string problem);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Error, Message = "Could not read the attempt records at {Path} for the recordings panel")]
    public static partial void RecordsNotRead(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Information, Message = "Compared {Attempts} attempts with the bots ({Runs} games each) in {ElapsedMs} ms")]
    public static partial void BotsCompared(ILogger logger, int attempts, int runs, long elapsedMs);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Warning, Message = "Still cannot write to {Path}; {Count} attempt records wait")]
    public static partial void RecordsStillWaiting(ILogger logger, int count, string path, Exception exception);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Error, Message = "Gave up the record of an attempt on {Level}: {Max} records already wait for {Path}")]
    public static partial void WaitingRecordDropped(ILogger logger, string level, int max, string path);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Error, Message = "Lost {Count} attempt records that could not be written to {Path} before the lab stopped")]
    public static partial void WaitingRecordsLost(ILogger logger, int count, string path);
}
