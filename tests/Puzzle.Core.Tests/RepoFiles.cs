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

    /// <summary>The first difficulty policy: the rules the live levels were made under.</summary>
    public static string DefaultPolicyPath => Find("policies", "default.json");

    /// <summary>The levels the game ships and the test server reads; tests only ever read them.</summary>
    public static string LiveLevelsFolder => Path.GetDirectoryName(Find("levels", "generated", "report.csv"))!;

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

    /// <summary>The game's hints on the sample games on the tools' runtime (see <c>EngineTraceTests</c>).</summary>
    public static string HintTracePath => Find("tests", "fixtures", "hint-trace.txt");

    /// <summary>The museum's rooms and paintings, as the game and the tools read them.</summary>
    public static string MuseumCataloguePath => Find("museum", "catalogue.json");

    /// <summary>A language's table of the words the game says (words/&lt;code&gt;.json); the Turkish one is the source.</summary>
    public static string WordTablePath(string code) => Find("words", $"{code}.json");

    /// <summary>The folder of the tables of words: one file a language.</summary>
    public static string WordTablesFolder => Path.GetDirectoryName(Find("words", "tr.json"))!;

    /// <summary>A language's words for the museum's rooms and plates (museum/titles/&lt;code&gt;.json); Turkish has the catalogue's own and no such file.</summary>
    public static string CaptionTablePath(string code) => Find("museum", "titles", $"{code}.json");

    /// <summary>The folder of the museum's words in the other languages.</summary>
    public static string CaptionTablesFolder => Path.GetDirectoryName(Find("museum", "catalogue.json"))! + Path.DirectorySeparatorChar + "titles";

    /// <summary>The catalogue as it was when only the first room had its paintings: a museum that ends after room 1.</summary>
    public static string OneRoomCataloguePath => Find("tests", "fixtures", "catalogue-one-room.json");

    /// <summary>The fingerprints of a few jigsaw cuts on the tools' runtime (see <c>JigsawCutTests</c>).</summary>
    public static string JigsawCutsPath => Find("tests", "fixtures", "jigsaw-cuts.txt");

    /// <summary>The folder the Unity game loads a painting's picture from; the catalogue names files below it.</summary>
    public static string PaintingPicturesFolder =>
        Path.Combine(Path.GetDirectoryName(Find("unity", "ProjectSettings", "ProjectVersion.txt"))!, "..", "Assets", "Resources", "Paintings");

    /// <summary>A source file of the Unity game, named from its Scripts folder down: tests that read what the game says read it there.</summary>
    public static string ClientScriptPath(params string[] parts) => Find(new[] { "unity", "Assets", "Scripts" }.Concat(parts).ToArray());

    /// <summary>A source file of the Unity game's Editor tools: tests that read the tables the Editor imports art by read them there.</summary>
    public static string ClientEditorScriptPath(string file) => Find("unity", "Assets", "Editor", file);

    /// <summary>The drawings of the gallery as they are written (art/unity): one .svg per sprite, and the wall's damask.</summary>
    public static string GalleryArtSourcesFolder => Path.GetDirectoryName(Find("art", "unity", "plaque.svg"))!;

    /// <summary>The same drawings as the game reads them: the pictures tools/unity/export-gallery-art.js writes from the sources.</summary>
    public static string GalleryArtPicturesFolder => Path.GetDirectoryName(Find("unity", "Assets", "Art", "Gallery", "plaque.png"))!;

    /// <summary>The Unity game's project folder; what the Editor derives and the repository keeps is named from here.</summary>
    public static string ClientProjectFolder => Path.GetDirectoryName(Path.GetDirectoryName(Find("unity", "ProjectSettings", "ProjectVersion.txt"))!)!;

    /// <summary>The art catalogue as the Editor's project setup last wrote it: the lists the game reads its sprites and typefaces from.</summary>
    public static string ArtCataloguePath => Path.Combine(ClientProjectFolder, "Assets", "Art", "ArtCatalogue.asset");

    /// <summary>The record of the game's font files (art/fonts/licences.json): where each came from, its licence, and the bytes it is kept with.</summary>
    public static string FontRecordPath => Find("art", "fonts", "licences.json");

    /// <summary>The record of the game's music (art/sounds/music.json): its source, its licence, and how tools/music/cut-loop.py cuts it.</summary>
    public static string MusicRecordPath => Find("art", "sounds", "music.json");

    /// <summary>The record of the CC0 sounds of play (art/sounds/licences.json), which tools/unity/fetch-sounds.js writes.</summary>
    public static string SoundLicencesPath => Find("art", "sounds", "licences.json");

    /// <summary>The music brief (docs/music/MUSIC_BRIEF.md), which holds the prompt each piece was made with.</summary>
    public static string MusicBriefPath => Find("docs", "music", "MUSIC_BRIEF.md");

    /// <summary>A file of the repository by its path from the repository's root, written with forward slashes as the music's record writes it.</summary>
    public static string RepositoryFile(string pathFromTheRoot) => Find(pathFromTheRoot.Split('/'));

    /// <summary>A font file of the game, as tools/unity/fetch-fonts.js keeps it.</summary>
    public static string FontFilePath(string file) => Path.Combine(ClientProjectFolder, "Assets", "Art", "Fonts", file);

    /// <summary>The typeface the Editor's project setup made from a font file, named as the font file is without its ending.</summary>
    public static string TypefacePath(string fontFileName) => Path.Combine(ClientProjectFolder, "Assets", "Art", "Fonts", $"{fontFileName} Lettering.asset");

    /// <summary>A file of TextMesh Pro's own resources in the Unity project (its settings, its line-breaking lists).</summary>
    public static string TextMeshProResourcePath(string file) => Path.Combine(ClientProjectFolder, "Assets", "TextMesh Pro", "Resources", file);

    /// <summary>The hand-made sample levels (levels/samples), in file-name order.</summary>
    public static string[] SampleLevelPaths => Directory
        .GetFiles(Path.GetDirectoryName(Find("levels", "samples", "s01_basic.json"))!, "*.json")
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    /// <summary>A shipped settings file of the economy simulator (economy/default.json and its variants): placeholders their owner changes.</summary>
    public static string EconomySettingsPath(string name) => Find("economy", name);

    /// <summary>
    /// economy/default.json as it was when the simulator was first built. Tests of the simulator's rules start from
    /// this fixture, so they do not move when a shipped price, amount or habit does.
    /// </summary>
    public static string EconomySettingsFixturePath => Find("tests", "fixtures", "economy-settings.json");

    /// <summary>
    /// The game's part of economy/default.json as tools/unity/sync-engine.js packs it for the Unity client, pinned
    /// by <c>node tools/unity/economy-pack.js --pin</c>: the file the game sees, for the tests to see too.
    /// </summary>
    public static string EconomyPackPath => Find("tests", "fixtures", "economy-pack.json");

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
