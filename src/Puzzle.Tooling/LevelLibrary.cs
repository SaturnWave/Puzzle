using Puzzle.Core.Levels;

namespace Puzzle.Tooling;

/// <summary>A level file that loaded, keyed by its path relative to the library root (e.g. "samples/s01_basic").</summary>
public sealed record LibraryLevel(string Key, string Path, LevelDefinition Level);

/// <summary>A level file that did not load, with the reason.</summary>
public sealed record LibraryProblem(string Path, string Message);

/// <summary>Every level file under a directory, loaded once.</summary>
public sealed class LevelLibrary
{
    private readonly Dictionary<string, LibraryLevel> _byKey;

    private LevelLibrary(string root, IReadOnlyList<LibraryLevel> levels, IReadOnlyList<LibraryProblem> problems)
    {
        Root = root;
        Levels = levels;
        Problems = problems;
        _byKey = levels.ToDictionary(l => l.Key, StringComparer.OrdinalIgnoreCase);
    }

    public string Root { get; }

    public IReadOnlyList<LibraryLevel> Levels { get; }

    public IReadOnlyList<LibraryProblem> Problems { get; }

    /// <summary>Loads every *.json under <paramref name="root"/>; broken files are reported, not thrown.</summary>
    public static LevelLibrary Load(string root)
    {
        var levels = new List<LibraryLevel>();
        var problems = new List<LibraryProblem>();
        foreach (string path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            try
            {
                levels.Add(new LibraryLevel(KeyOf(root, path), path, LevelJson.Load(path)));
            }
            catch (LevelFormatException ex)
            {
                problems.Add(new LibraryProblem(path, ex.Message));
            }
        }

        return new LevelLibrary(root, levels, problems);
    }

    /// <summary>Walks up from <paramref name="start"/> to the first folder containing a "levels" directory.</summary>
    public static string? FindLevelsDirectory(string start)
    {
        for (DirectoryInfo? dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            string candidate = System.IO.Path.Combine(dir.FullName, "levels");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public bool TryGet(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LibraryLevel? level) =>
        _byKey.TryGetValue(key, out level);

    private static string KeyOf(string root, string path) =>
        System.IO.Path.ChangeExtension(System.IO.Path.GetRelativePath(root, path), null).Replace('\\', '/');
}
