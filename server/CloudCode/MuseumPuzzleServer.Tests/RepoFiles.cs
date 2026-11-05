using System.IO;

namespace MuseumPuzzle.Server.Tests;

/// <summary>The repository's files, found from the test binaries upward (the folder that holds Puzzle.slnx).</summary>
public static class RepoFiles
{
    public static string Root { get; } = FindRoot();

    public static string PathOf(params string[] parts) => Path.Combine(new[] { Root }.Concat(parts).ToArray());

    public static string Read(params string[] parts) => File.ReadAllText(PathOf(parts));

    private static string FindRoot()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Puzzle.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new FileNotFoundException("No folder above the test binaries holds Puzzle.slnx.");
    }
}
