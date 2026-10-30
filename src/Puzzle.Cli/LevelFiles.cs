namespace Puzzle.Cli;

/// <summary>The level files a command's <c>&lt;dir|file&gt;</c> argument stands for.</summary>
internal static class LevelFiles
{
    /// <summary>Every *.json under the folder <paramref name="target"/>, in the order of their paths; or <paramref name="target"/> itself, taken as a file.</summary>
    public static string[] Under(string target) => Directory.Exists(target)
        ? Directory.GetFiles(target, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray()
        : [target];
}
