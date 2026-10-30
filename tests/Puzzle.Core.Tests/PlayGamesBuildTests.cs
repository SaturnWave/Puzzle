using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Android build's steps for Google's Play Games plugin: the Unity library's manifest marks Play Games' start-up
/// provider for removal, so nothing of Play Games starts with the game while the player's switch is off; the check of
/// the built package, whose manifest must not declare it; and the Unity library's build.gradle, which is given the Play
/// Games library once however often the step runs. <c>ManifestPlayGamesStart</c> and <c>PlayGamesDependency</c> are
/// the Unity project's Editor source files, compiled into these tests; the steps run only while a package is built.
/// </summary>
public class PlayGamesBuildTests
{
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
