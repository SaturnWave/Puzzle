using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Google Play bundle's withdrawal of Play Asset Delivery's foreground service (2026-10-28): the Unity library's
/// manifest marks the dataSync service that unpacks downloaded packs for removal, so the bundle needs none of the
/// foreground-service permissions the test package does not ask for; and the built package is read back for it.
/// <c>ManifestAssetDelivery</c> is the Unity project's Editor source file, compiled into these tests; the step runs only
/// while a package is built (unity/Assets/Editor/AndroidBuild.cs).
/// </summary>
public class ManifestAssetDeliveryTests
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

    /// <summary>What aapt says of the bundle's base APK of 28 October, before the withdrawal, shortened.</summary>
    private const string BundleDump =
        "N: android=http://schemas.android.com/apk/res/android\n"
        + "  E: manifest (line=2)\n"
        + "    E: application (line=63)\n"
        + "      E: service (line=145)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.play.core.assetpacks.AssetPackExtractionService\" (Raw: \"com.google.android.play.core.assetpacks.AssetPackExtractionService\")\n"
        + "      E: service (line=153)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.play.core.assetpacks.ExtractionForegroundService\" (Raw: \"com.google.android.play.core.assetpacks.ExtractionForegroundService\")\n"
        + "        A: android:foregroundServiceType(0x01010599)=(type 0x11)0x1\n"
        + "      E: receiver (line=159)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.play.core.assetpacks.SessionStateBroadcastReceiver\" (Raw: \"com.google.android.play.core.assetpacks.SessionStateBroadcastReceiver\")\n";

    private static XElement[] ServicesIn(string manifest) =>
        XDocument.Parse(manifest).Root!.Element("application")!.Elements("service")
            .Where(service => (string?)service.Attribute(Android + "name") == ManifestAssetDelivery.ForegroundService)
            .ToArray();

    [Fact]
    public void The_foreground_service_is_marked_for_the_merger_to_take_it_out_and_the_rest_stays_as_it_was()
    {
        string written = ManifestAssetDelivery.Withdraw(UnitysManifest);

        XElement service = Assert.Single(ServicesIn(written));
        Assert.Equal("remove", (string?)service.Attribute(Tools + "node"));
        Assert.Equal(new[] { "name", "node" }, service.Attributes().Select(attribute => attribute.Name.LocalName));
        XElement application = XDocument.Parse(written).Root!.Element("application")!;
        Assert.Equal("game", (string?)application.Attribute(Android + "appCategory"));
        Assert.Single(application.Elements("activity"));
        Assert.Equal(written, ManifestAssetDelivery.Withdraw(written));
    }

    [Fact]
    public void It_goes_with_the_other_withdrawals_in_the_steps_order()
    {
        string all = ManifestAssetDelivery.Withdraw(ManifestPlayGamesStart.Withdraw(ManifestLoggingBackend.Withdraw(UnitysManifest)));

        Assert.Single(ServicesIn(all));
        Assert.Single(XDocument.Parse(all).Root!.Element("application")!.Elements("provider"));
        Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.Withdraw("<application />"));
    }

    [Fact]
    public void A_package_that_still_declares_the_service_stops_the_build_and_one_without_it_passes()
    {
        string withdrawn = string.Join("\n", BundleDump.Split('\n')
            .Where(line => !line.Contains("ExtractionForegroundService", StringComparison.Ordinal) && !line.Contains("foregroundServiceType", StringComparison.Ordinal)));

        InvalidOperationException stopped = Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.RequireWithdrawnFrom(BundleDump));
        Assert.Contains(ManifestAssetDelivery.ForegroundService, stopped.Message, StringComparison.Ordinal);
        ManifestAssetDelivery.RequireWithdrawnFrom(withdrawn);
        Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.RequireWithdrawnFrom("package: name='com.saturnwave.museumpuzzle'"));
    }
}
