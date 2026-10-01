namespace Puzzle.Web;

internal static partial class Log
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Skipped level {Path}: {Problem}")]
    public static partial void LevelSkipped(ILogger logger, string path, string problem);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Loaded {Count} levels from {Path}")]
    public static partial void LevelsLoaded(ILogger logger, int count, string path);
}
