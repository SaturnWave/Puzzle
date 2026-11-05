using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The game's Google Play Games project as a build carries it (2026-11-02, Stage 6): the plugin's settings read as a
/// closed set, the project id written into the manifest as text, and the built package read back for it.
/// <c>PlayGamesProject</c> is the Unity project's Editor source file, compiled into these tests; the step runs only while
/// a package is built (unity/Assets/Editor/AndroidBuild.cs).
/// </summary>
public class PlayGamesProjectTests
{
    private const string AppId = "235305914512";
    private const string WebClientId = "235305914512-a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6.apps.googleusercontent.com";

    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    private const string UnitysManifest =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.unity3d.player\">\n"
        + "  <application android:appCategory=\"game\">\n"
        + "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" />\n"
        + "  </application>\n"
        + "</manifest>\n";

    private const string DumpStart =
        "N: android=http://schemas.android.com/apk/res/android\n"
        + "  E: manifest (line=2)\n"
        + "    E: application (line=63)\n";

    /// <summary>What aapt says of a meta-data whose value Android keeps as text: the text, and the raw "\u003…" it was written as.</summary>
    private const string AsText =
        "      E: meta-data (line=120)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.gms.games.APP_ID\" (Raw: \"com.google.android.gms.games.APP_ID\")\n"
        + "        A: android:value(0x01010024)=\"235305914512\" (Raw: \"\\u003235305914512\")\n";

    /// <summary>What aapt says of the same meta-data written as a bare number: Android keeps it as a number, which getString does not read.</summary>
    private const string AsNumber =
        "      E: meta-data (line=120)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.gms.games.APP_ID\" (Raw: \"com.google.android.gms.games.APP_ID\")\n"
        + "        A: android:value(0x01010024)=(type 0x10)0xc8b9b1d0\n";

    private static PlayGamesProject.SetUp Project => Assert.IsType<PlayGamesProject.SetUp>(PlayGamesProject.Read(AppId, WebClientId));

    [Fact]
    public void Settings_with_both_ids_of_one_project_are_set_up_and_with_neither_not_set_up()
    {
        Assert.Equal((AppId, WebClientId), (Project.AppId, Project.WebClientId));
        Assert.IsType<PlayGamesProject.SetUp>(PlayGamesProject.Read($" {AppId} ", $"{WebClientId}\n"));
        Assert.IsType<PlayGamesProject.NotSetUp>(PlayGamesProject.Read(null, null));
        Assert.IsType<PlayGamesProject.NotSetUp>(PlayGamesProject.Read(string.Empty, "  "));
    }

    [Theory]
    [InlineData(AppId, "")]
    [InlineData("", WebClientId)]
    [InlineData("Museum Puzzle", WebClientId)]
    [InlineData(AppId, "235305914512-a1b2c3.example.com")]
    [InlineData(AppId, "999999999999-a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6.apps.googleusercontent.com")]
    public void Settings_with_one_id_without_the_other_or_ids_not_of_googles_forms_or_of_two_projects_are_refused(string appId, string webClientId)
    {
        Assert.IsType<PlayGamesProject.Refused>(PlayGamesProject.Read(appId, webClientId));
    }

    [Fact]
    public void The_manifest_is_given_the_project_id_as_text_once_and_the_rest_stays_as_it_was()
    {
        string written = PlayGamesProject.Declare(UnitysManifest, Project);

        XElement application = XDocument.Parse(written).Root!.Element("application")!;
        XElement metaData = Assert.Single(application.Elements("meta-data"));
        Assert.Equal(PlayGamesProject.AppIdName, (string?)metaData.Attribute(Android + "name"));
        Assert.Equal(@"\u003" + AppId, (string?)metaData.Attribute(Android + "value"));
        Assert.Single(application.Elements("activity"));
        Assert.Equal(written, PlayGamesProject.Declare(written, Project));
        Assert.Throws<InvalidOperationException>(() => PlayGamesProject.Declare("<application />", Project));
    }

    [Fact]
    public void A_package_that_holds_the_id_as_text_passes_and_one_without_it_or_with_a_number_stops_the_build()
    {
        PlayGamesProject.RequireDeclaredIn(DumpStart + AsText, Project);
        PlayGamesProject.RequireDeclaredIn((DumpStart + AsText).Replace("\n", "\r\n", StringComparison.Ordinal), Project);

        Assert.Throws<InvalidOperationException>(() => PlayGamesProject.RequireDeclaredIn(DumpStart, Project));
        InvalidOperationException asNumber = Assert.Throws<InvalidOperationException>(() => PlayGamesProject.RequireDeclaredIn(DumpStart + AsNumber, Project));
        Assert.Contains(PlayGamesProject.AppIdName, asNumber.Message, StringComparison.Ordinal);
        PlayGamesProject.SetUp another = Assert.IsType<PlayGamesProject.SetUp>(
            PlayGamesProject.Read("111111111111", "111111111111-a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6.apps.googleusercontent.com"));
        Assert.Throws<InvalidOperationException>(() => PlayGamesProject.RequireDeclaredIn(DumpStart + AsText, another));
        Assert.Throws<InvalidOperationException>(() => PlayGamesProject.RequireDeclaredIn("package: name='com.saturnwave.museumpuzzle'", Project));
    }
}
