using System.IO;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>Files of the repository that tests read, found by walking up from the test binaries.</summary>
internal static class RepoFiles
{
    private static readonly Lazy<GenerationProfile> LoadedDefaultProfile = new(() => ProfileLoader.Load(DefaultProfilePath));
    private static readonly Lazy<GenerationProfile> LoadedProfileBeforeOnboarding = new(() => ProfileLoader.Load(ProfileBeforeOnboardingPath));

    public static string DefaultProfilePath => Find("profiles", "default.json");

    public static string ProfileBeforeOnboardingPath => Find("tests", "fixtures", "profile-before-onboarding.json");

    /// <summary>The test settings file of the public test, beside the web app's settings.</summary>
    public static string ReadyMadePlayersPath => Find("src", "Puzzle.Web", "public-test-players.json");

    /// <summary>profiles/default.json, loaded once.</summary>
    public static GenerationProfile DefaultProfile => LoadedDefaultProfile.Value;

    /// <summary>
    /// profiles/default.json as it was before the first museum room was set for weak players, loaded once: the average
    /// bot calibrates every level, every band has casual floors, and no level has a move margin. Tests that pin a
    /// mechanic of the generator on particular levels and numbers read this fixture, so they do not move when the
    /// shipped curve does.
    /// </summary>
    public static GenerationProfile ProfileBeforeOnboarding => LoadedProfileBeforeOnboarding.Value;

    /// <summary>What the engine prints for the sample levels on the tools' runtime (see <c>EngineTraceTests</c>).</summary>
    public static string EngineTracePath => Find("tests", "fixtures", "engine-trace.txt");

    /// <summary>The museum's rooms and paintings, as the game and the tools read them.</summary>
    public static string MuseumCataloguePath => Find("museum", "catalogue.json");

    /// <summary>The fingerprints of a few jigsaw cuts on the tools' runtime (see <c>JigsawCutTests</c>).</summary>
    public static string JigsawCutsPath => Find("tests", "fixtures", "jigsaw-cuts.txt");

    /// <summary>The folder the Unity game loads a painting's picture from; the catalogue names files below it.</summary>
    public static string PaintingPicturesFolder =>
        Path.Combine(Path.GetDirectoryName(Find("unity", "ProjectSettings", "ProjectVersion.txt"))!, "..", "Assets", "Resources", "Paintings");

    /// <summary>The hand-made sample levels (levels/samples), in file-name order.</summary>
    public static string[] SampleLevelPaths => Directory
        .GetFiles(Path.GetDirectoryName(Find("levels", "samples", "s01_basic.json"))!, "*.json")
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

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
