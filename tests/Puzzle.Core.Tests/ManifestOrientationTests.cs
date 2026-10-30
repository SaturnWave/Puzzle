using Puzzle.Client.EditorTools;
using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Android build's manifest step, for the screen's turning: the package opens Unity's activity upright, the one
/// way the rule lets a phone stand, and a manifest that opens it another way stops the build. <c>ManifestOrientation</c>
/// is the Unity project's Editor source file, compiled into these tests; the step itself runs only while a package is
/// built (unity/Assets/Editor/AndroidBuild.cs), which these tests do not do.
/// </summary>
public class ManifestOrientationTests
{
    /// <summary>The library's manifest as Unity 6.6 writes it for a Portrait default, its application and activity cut to what matters here.</summary>
    private const string UnitysManifest =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" xmlns:tools=\"http://schemas.android.com/tools\">\n"
        + "  <application android:appCategory=\"game\" android:extractNativeLibs=\"true\">\n"
        + "    <meta-data android:name=\"unity.splash-mode\" android:value=\"0\" />\n"
        + "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" android:resizeableActivity=\"true\" android:screenOrientation=\"portrait\">\n"
        + "      <meta-data android:name=\"unityplayer.UnityActivity\" android:value=\"true\" />\n"
        + "      <meta-data android:name=\"android.app.lib_name\" android:value=\"game\" />\n"
        + "    </activity>\n"
        + "  </application>\n"
        + "</manifest>\n";

    [Fact]
    public void The_package_opens_as_the_rule_lets_a_phone_stand()
    {
        Assert.Equal(new[] { ScreenTurn.Portrait }, ScreenTurns.AllowedFor(ScreenClass.Phone));
        Assert.Equal("portrait", ManifestOrientation.Upright);
    }

    [Fact]
    public void A_manifest_whose_Unity_activity_opens_upright_passes_whatever_other_activities_say()
    {
        ManifestOrientation.Require(UnitysManifest);

        // A library's own activity (a store's purchase screen, say) is not the one the game is played in.
        string withAnother = UnitysManifest.Replace(
            "  </application>",
            "    <activity android:name=\"com.example.PurchaseActivity\" android:screenOrientation=\"fullUser\" />\n  </application>");
        ManifestOrientation.Require(withAnother);
    }

    [Theory]
    [InlineData("fullUser")] // what Unity writes for AutoRotation with the user's lock, the iOS build's default
    [InlineData("userPortrait")] // upright, but upside down too
    [InlineData("landscape")]
    public void A_Unity_activity_that_opens_another_way_stops_the_build(string orientation)
    {
        string manifest = UnitysManifest.Replace("android:screenOrientation=\"portrait\"", $"android:screenOrientation=\"{orientation}\"");

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => ManifestOrientation.Require(manifest));

        Assert.Contains(orientation, refused.Message);
    }

    [Fact]
    public void A_Unity_activity_with_no_orientation_of_its_own_stops_the_build()
    {
        string manifest = UnitysManifest.Replace(" android:screenOrientation=\"portrait\"", "");

        Assert.Throws<InvalidOperationException>(() => ManifestOrientation.Require(manifest));
    }

    [Fact]
    public void A_manifest_without_exactly_one_Unity_activity_stops_the_build()
    {
        string none = UnitysManifest.Replace("android:value=\"true\" />\n      <meta-data android:name=\"android.app.lib_name\"", "android:value=\"false\" />\n      <meta-data android:name=\"android.app.lib_name\"");
        string two = UnitysManifest.Replace(
            "  </application>",
            "    <activity android:name=\"com.unity3d.player.UnityPlayerActivity\" android:screenOrientation=\"portrait\">\n"
            + "      <meta-data android:name=\"unityplayer.UnityActivity\" android:value=\"true\" />\n    </activity>\n  </application>");

        Assert.Throws<InvalidOperationException>(() => ManifestOrientation.Require(none));
        Assert.Throws<InvalidOperationException>(() => ManifestOrientation.Require(two));
        Assert.Throws<InvalidOperationException>(() => ManifestOrientation.Require("<manifest />"));
    }
}
