using System.Text;
using System.Xml.Linq;
using Puzzle.Client.Cloud;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Android build's steps for Google's Play Games plugin: the Unity library's manifest marks the SDK's start-up
/// provider for removal, which would start Play Games whatever the player's switch says, and a build set up for Play
/// Games declares the game's own, which starts it with the process while the switch is on (docs/LESSONS.md H7); the
/// checks of the built package; and the Unity library's build.gradle, which is given the Play Games library once
/// however often the step runs. <c>ManifestPlayGamesStart</c> and <c>PlayGamesDependency</c> are the Unity project's
/// Editor source files, compiled into these tests; the steps run only while a package is built. The game's provider is
/// Java (unity/Assets/Plugins/Android/PlayGamesStart.java): what it reads of cloud.json is held to the file's layout here.
/// </summary>
public class PlayGamesBuildTests
{
    private const string PackageId = "com.saturnwave.museumpuzzle";
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
    private static readonly XNamespace Tools = "http://schemas.android.com/tools";

    private const string UnitysManifest =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.unity3d.player\">\n"
        + "  <application android:appCategory=\"game\">\n"
        + "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" />\n"
        + "  </application>\n"
        + "</manifest>\n";

    /// <summary>What aapt says of a package whose manifest kept the SDK's provider (its line as play-services-games-v2 22.1.0 declares it).</summary>
    private const string DumpWithTheProvider =
        "N: android=http://schemas.android.com/apk/res/android\n"
        + "  E: manifest (line=2)\n"
        + "      E: provider (line=140)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.gms.games.provider.PlayGamesInitProvider\" (Raw: \"com.google.android.gms.games.provider.PlayGamesInitProvider\")\n"
        + "        A: android:exported(0x01010010)=(type 0x12)0x0\n"
        + "        A: android:authorities(0x01010018)=\"com.saturnwave.museumpuzzle.playgamesinitprovider\" (Raw: \"com.saturnwave.museumpuzzle.playgamesinitprovider\")\n"
        + "      E: activity (line=146)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.gms.games.internal.v2.resolution.GamesResolutionActivity\" (Raw: \"com.google.android.gms.games.internal.v2.resolution.GamesResolutionActivity\")\n";

    private static XElement[] ProvidersIn(string manifest) =>
        XDocument.Parse(manifest).Root!.Element("application")!.Elements("provider")
            .Where(provider => (string?)provider.Attribute(Android + "name") == ManifestPlayGamesStart.InitProvider)
            .ToArray();

    [Fact]
    public void The_start_up_provider_is_marked_for_the_merger_to_take_it_out_and_the_rest_stays_as_it_was()
    {
        string written = ManifestPlayGamesStart.Withdraw(UnitysManifest);

        XElement provider = Assert.Single(ProvidersIn(written));
        Assert.Equal("remove", (string?)provider.Attribute(Tools + "node"));
        Assert.Equal(new[] { "name", "node" }, provider.Attributes().Select(attribute => attribute.Name.LocalName));
        XElement application = XDocument.Parse(written).Root!.Element("application")!;
        Assert.Equal("game", (string?)application.Attribute(Android + "appCategory"));
        Assert.Single(application.Elements("activity"));
        Assert.Equal(written, ManifestPlayGamesStart.Withdraw(written));
    }

    [Fact]
    public void Both_withdrawals_go_together_in_either_order()
    {
        string both = ManifestPlayGamesStart.Withdraw(ManifestLoggingBackend.Withdraw(UnitysManifest));

        Assert.Single(ProvidersIn(both));
        Assert.Equal(both, ManifestLoggingBackend.Withdraw(ManifestPlayGamesStart.Withdraw(both)));
    }

    [Fact]
    public void A_package_that_still_declares_the_provider_stops_the_build_and_one_without_it_passes()
    {
        string withdrawn = string.Join("\n", DumpWithTheProvider.Split('\n').Where(line => !line.Contains("provider", StringComparison.Ordinal)));

        InvalidOperationException stopped = Assert.Throws<InvalidOperationException>(() => ManifestPlayGamesStart.RequireWithdrawnFrom(DumpWithTheProvider));
        Assert.Contains(ManifestPlayGamesStart.InitProvider, stopped.Message, StringComparison.Ordinal);
        ManifestPlayGamesStart.RequireWithdrawnFrom(withdrawn);
        Assert.Throws<InvalidOperationException>(() => ManifestPlayGamesStart.RequireWithdrawnFrom("package: name='com.saturnwave.museumpuzzle'"));
        Assert.Throws<InvalidOperationException>(() => ManifestPlayGamesStart.Withdraw("<application />"));
    }

    [Fact]
    public void The_games_own_provider_is_declared_once_not_exported_under_the_packages_authority()
    {
        string written = ManifestPlayGamesStart.StartWithTheSwitch(ManifestPlayGamesStart.Withdraw(UnitysManifest), PackageId);

        XElement[] providers = XDocument.Parse(written).Root!.Element("application")!.Elements("provider").ToArray();
        XElement start = Assert.Single(providers, provider => (string?)provider.Attribute(Android + "name") == ManifestPlayGamesStart.GamesStart);
        Assert.Equal("com.saturnwave.museumpuzzle.playgamesstart", (string?)start.Attribute(Android + "authorities"));
        Assert.Equal("false", (string?)start.Attribute(Android + "exported"));
        Assert.Null(start.Attribute(Tools + "node"));
        Assert.Single(ProvidersIn(written));
        Assert.Equal(written, ManifestPlayGamesStart.StartWithTheSwitch(written, PackageId));
        Assert.Throws<InvalidOperationException>(() => ManifestPlayGamesStart.StartWithTheSwitch("<application />", PackageId));
    }

    [Fact]
    public void A_set_up_package_without_the_games_provider_or_its_class_stops_the_build()
    {
        string declared = DumpWithTheProvider.Replace(ManifestPlayGamesStart.InitProvider, ManifestPlayGamesStart.GamesStart, StringComparison.Ordinal);
        byte[] withTheClass = Encoding.ASCII.GetBytes("dex\n035\0...Landroid/content/ContentProvider;...Lcom/saturnwave/museumpuzzle/PlayGamesStart;...");
        byte[] withoutTheClass = Encoding.ASCII.GetBytes("dex\n035\0...Landroid/content/ContentProvider;...Lcom/unity3d/player/UnityPlayerGameActivity;...");

        ManifestPlayGamesStart.RequireTheGamesStartIn(declared, new[] { withoutTheClass, withTheClass });
        InvalidOperationException undeclared = Assert.Throws<InvalidOperationException>(() => ManifestPlayGamesStart.RequireTheGamesStartIn(DumpWithTheProvider, new[] { withTheClass }));
        Assert.Contains(ManifestPlayGamesStart.GamesStart, undeclared.Message, StringComparison.Ordinal);
        InvalidOperationException missing = Assert.Throws<InvalidOperationException>(() => ManifestPlayGamesStart.RequireTheGamesStartIn(declared, new[] { withoutTheClass }));
        Assert.Contains("dex", missing.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ManifestPlayGamesStart.RequireTheGamesStartIn("package: name='com.saturnwave.museumpuzzle'", new[] { withTheClass }));
    }

    [Fact]
    public void The_games_provider_reads_the_switch_as_cloud_json_writes_it()
    {
        string source = File.ReadAllText(RepoFiles.RepositoryFile("unity/Assets/Plugins/Android/PlayGamesStart.java"));
        string className = ManifestPlayGamesStart.GamesStart.Substring(ManifestPlayGamesStart.GamesStart.LastIndexOf('.') + 1);
        string packageName = ManifestPlayGamesStart.GamesStart.Substring(0, ManifestPlayGamesStart.GamesStart.LastIndexOf('.'));

        Assert.Contains($"package {packageName};", source, StringComparison.Ordinal);
        Assert.Contains($"public final class {className} extends ContentProvider", source, StringComparison.Ordinal);
        Assert.Contains($"\"{CloudStateFile.FileName}\"", source, StringComparison.Ordinal);
        Assert.Contains($"optBoolean(\"{nameof(CloudStateDocument.switchedOn)}\", false)", source, StringComparison.Ordinal);
        Assert.Contains($"optString(\"{nameof(CloudStateDocument.writer)}\", \"\")", source, StringComparison.Ordinal);
        Assert.Contains($"optInt(\"{nameof(CloudStateDocument.version)}\", 0) >= 1", source, StringComparison.Ordinal);
        Assert.True(CloudStateDocument.CurrentVersion >= 1);
        Assert.True(CloudState.New("writer").IsSwitchedOn, "A new installation, which has no cloud.json, starts with the switch on: the provider starts Play Games for it.");
        Assert.All(source, letter => Assert.True(letter < 128, "Gradle compiles the Java source in an encoding of its own: it is kept to ASCII."));
    }

    [Fact]
    public void The_library_is_declared_once_at_the_end_of_the_build_file_however_often_the_step_runs()
    {
        const string BuildGradle = "apply plugin: 'com.android.library'\n\ndependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n}\n";

        string once = PlayGamesDependency.Into(BuildGradle);
        string twice = PlayGamesDependency.Into(once);

        Assert.StartsWith(BuildGradle, once, StringComparison.Ordinal);
        Assert.Contains($"implementation '{PlayGamesDependency.Library}'", once, StringComparison.Ordinal);
        Assert.Equal(once, twice);
        Assert.Equal("com.google.android.gms:play-services-games-v2:22.1.0", PlayGamesDependency.Library);
    }

    [Fact]
    public void A_build_file_whose_section_was_cut_is_not_touched()
    {
        string cut = PlayGamesDependency.Into("android {}\n");
        cut = cut.Substring(0, cut.IndexOf("// End of dependencies", StringComparison.Ordinal));

        Assert.Throws<InvalidOperationException>(() => PlayGamesDependency.Into(cut));
    }
}
