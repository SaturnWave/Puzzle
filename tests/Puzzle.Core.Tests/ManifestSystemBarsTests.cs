using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Android build's manifest step, for the system bars: Unity is to hide them with Android's default behaviour,
/// under which one back gesture from the screen's edge goes back (T235), and a manifest that hides them another way
/// stops the build. <c>ManifestSystemBars</c> is the Unity project's Editor source file, compiled into these tests; the
/// step itself runs only while a package is built (unity/Assets/Editor/AndroidBuild.cs), which these tests do not do.
/// </summary>
public class ManifestSystemBarsTests
{
    /// <summary>The library's manifest as Unity 6.6 writes it, its application cut to the meta-data around the bars.</summary>
    private static string UnitysManifest(string behaviour) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n"
        + "  <application android:appCategory=\"game\">\n"
        + "    <meta-data android:name=\"unity.requested-visible-insets\" android:value=\"\" />\n"
        + $"    <meta-data android:name=\"unity.system-bars-behavior\" android:value=\"{behaviour}\" />\n"
        + "    <meta-data android:name=\"unity.render-outside-safearea\" android:value=\"True\" />\n"
        + "  </application>\n"
        + "</manifest>\n";

    [Fact]
    public void A_manifest_that_hides_the_bars_with_Android_s_default_behaviour_passes()
    {
        ManifestSystemBars.Require(UnitysManifest(ManifestSystemBars.Default));
    }

    [Fact]
    public void Unity_s_passing_bars_which_spend_the_first_back_gesture_stop_the_build()
    {
        InvalidOperationException stopped = Assert.Throws<InvalidOperationException>(() => ManifestSystemBars.Require(UnitysManifest("ShowTransientBarsBySwipe")));

        Assert.Contains("ShowTransientBarsBySwipe", stopped.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_manifest_that_says_nothing_of_the_bars_or_says_it_twice_stops_the_build()
    {
        string none = UnitysManifest(ManifestSystemBars.Default).Replace("    <meta-data android:name=\"unity.system-bars-behavior\" android:value=\"Default\" />\n", string.Empty, StringComparison.Ordinal);
        string twice = UnitysManifest(ManifestSystemBars.Default).Replace(
            "  </application>", "    <meta-data android:name=\"unity.system-bars-behavior\" android:value=\"Default\" />\n  </application>", StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => ManifestSystemBars.Require(none));
        Assert.Throws<InvalidOperationException>(() => ManifestSystemBars.Require(twice));
    }
}
