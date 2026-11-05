using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Android build's backup rules (T566, 2026-10-27): Android's backup keeps Unity's PlayerPrefs, where Unity
/// Authentication keeps the anonymous account's session, and the game's four files, and nothing else, for Google's cloud
/// backup and for a transfer from phone to phone alike; the manifest names both rules' files with the backup on; and
/// the built package is read back for them. <c>ManifestBackupRules</c> is the Unity project's Editor source file, compiled
/// into these tests; the step runs only while a package is built (unity/Assets/Editor/AndroidBuild.cs).
/// </summary>
public class ManifestBackupRulesTests
{
    private const string Package = "com.saturnwave.museumpuzzle";

    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    private const string UnitysManifest =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.unity3d.player\">\n"
        + "  <application android:appCategory=\"game\" android:extractNativeLibs=\"true\">\n"
        + "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" />\n"
        + "  </application>\n"
        + "</manifest>\n";

    private static (string Domain, string Path)[] IncludesOf(XElement rules) =>
        rules.Elements("include").Select(include => ((string)include.Attribute("domain")!, (string)include.Attribute("path")!)).ToArray();

    private static readonly (string Domain, string Path)[] Kept =
    {
        ("sharedpref", "com.saturnwave.museumpuzzle.v2.playerprefs.xml"),
        ("external", "progress.json"),
        ("external", "cloud.json"),
        ("external", "device.json"),
        ("external", "attempts.jsonl"),
    };

    [Fact]
    public void Both_rules_keep_unitys_player_prefs_and_the_games_four_files_and_nothing_else()
    {
        XElement old = XDocument.Parse(ManifestBackupRules.FullBackupContent(Package)).Root!;
        XElement current = XDocument.Parse(ManifestBackupRules.DataExtractionRules(Package)).Root!;

        Assert.Equal("full-backup-content", old.Name.LocalName);
        Assert.Equal(Kept, IncludesOf(old));
        Assert.Equal("data-extraction-rules", current.Name.LocalName);
        Assert.Equal(new[] { "cloud-backup", "device-transfer" }, current.Elements().Select(section => section.Name.LocalName));
        Assert.All(current.Elements(), section => Assert.Equal(Kept, IncludesOf(section)));
        Assert.Empty(current.Descendants("exclude"));
    }

    [Fact]
    public void The_application_keeps_the_backup_on_and_names_both_rules_and_the_rest_stays_as_it_was()
    {
        string written = ManifestBackupRules.Apply(UnitysManifest);

        XElement application = XDocument.Parse(written).Root!.Element("application")!;
        Assert.Equal("true", (string?)application.Attribute(Android + "allowBackup"));
        Assert.Equal("@xml/game_backup_rules", (string?)application.Attribute(Android + "fullBackupContent"));
        Assert.Equal("@xml/game_data_extraction_rules", (string?)application.Attribute(Android + "dataExtractionRules"));
        Assert.Equal("game", (string?)application.Attribute(Android + "appCategory"));
        Assert.Single(application.Elements("activity"));
        Assert.Equal(written, ManifestBackupRules.Apply(written));
    }

    [Fact]
    public void A_manifest_that_turns_the_backup_off_stops_the_build()
    {
        string off = UnitysManifest.Replace("<application ", "<application android:allowBackup=\"false\" ", StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => ManifestBackupRules.Apply(off));
        Assert.Throws<InvalidOperationException>(() => ManifestBackupRules.Apply("<manifest />"));
    }

    [Fact]
    public void The_package_is_read_back_for_the_backup_and_both_rules()
    {
        const string Ruled =
            "  E: manifest (line=2)\n"
            + "    E: application (line=57)\n"
            + "      A: android:label(0x01010001)=@0x7f0e0021\n"
            + "      A: android:allowBackup(0x01010280)=(type 0x12)0xffffffff\n"
            + "      A: android:fullBackupContent(0x01010473)=@0x7f110000\n"
            + "      A: android:dataExtractionRules(0x0101063e)=@0x7f110001\n"
            + "      E: meta-data (line=64)\n"
            + "        A: android:name(0x01010003)=\"unity.splash-mode\" (Raw: \"unity.splash-mode\")\n";
        string unruled = Ruled.Replace("      A: android:dataExtractionRules(0x0101063e)=@0x7f110001\n", string.Empty, StringComparison.Ordinal);

        ManifestBackupRules.RequireIn(Ruled);
        Assert.Contains("does not name those for Android 12", Assert.Throws<InvalidOperationException>(() => ManifestBackupRules.RequireIn(unruled)).Message);
        Assert.Throws<InvalidOperationException>(() => ManifestBackupRules.RequireIn("no dump"));
    }
}
