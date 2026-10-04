using System.IO;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>Files of the repository that tests read, found by walking up from the test binaries.</summary>
internal static class RepoFiles
{
    private static readonly Lazy<GenerationProfile> LoadedDefaultProfile = new(() => ProfileLoader.Load(DefaultProfilePath));

    public static string DefaultProfilePath => Find("profiles", "default.json");

    /// <summary>The test settings file of the public test, beside the web app's settings.</summary>
    public static string ReadyMadePlayersPath => Find("src", "Puzzle.Web", "public-test-players.json");

    /// <summary>profiles/default.json, loaded once.</summary>
    public static GenerationProfile DefaultProfile => LoadedDefaultProfile.Value;

    /// <exception cref="FileNotFoundException">No directory above the test binaries holds the file.</exception>
    private static string Find(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"{Path.Combine(parts)} not found above {AppContext.BaseDirectory}.");
    }
}
