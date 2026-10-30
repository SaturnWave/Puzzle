using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// Play Asset Delivery in the Google Play bundle (2026-10-29): the Unity library's manifest keeps the dataSync
/// foreground service that unpacks downloaded packs and takes off its type alone, because the library switches the
/// service on when the game starts and Android refuses that for a service the manifest does not declare (versionCode 1,
/// whose manifest had the service taken out on 2026-10-28, closed on every start); and the built packages are read back
/// for it. <c>ManifestAssetDelivery</c> is the Unity project's Editor source file, compiled into these tests; the step
/// runs only while a package is built (unity/Assets/Editor/AndroidBuild.cs).
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

    private const string DumpStart =
        "N: android=http://schemas.android.com/apk/res/android\n"
        + "  E: manifest (line=2)\n"
        + "    E: application (line=63)\n";

    private const string ExtractionServiceLines =
        "      E: service (line=139)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.play.core.assetpacks.AssetPackExtractionService\" (Raw: \"com.google.android.play.core.assetpacks.AssetPackExtractionService\")\n"
        + "        A: android:enabled(0x0101000e)=(type 0x12)0x0\n"
        + "        A: android:exported(0x01010010)=(type 0x12)0xffffffff\n"
        + "        E: meta-data (line=143)\n"
        + "          A: android:name(0x01010003)=\"com.google.android.play.core.assetpacks.versionCode\" (Raw: \"com.google.android.play.core.assetpacks.versionCode\")\n"
        + "          A: android:value(0x01010024)=(type 0x10)0x4f4c\n";

    private const string ForegroundServiceLines =
        "      E: service (line=153)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.play.core.assetpacks.ExtractionForegroundService\" (Raw: \"com.google.android.play.core.assetpacks.ExtractionForegroundService\")\n"
        + "        A: android:enabled(0x0101000e)=(type 0x12)0x0\n"
        + "        A: android:exported(0x01010010)=(type 0x12)0x0\n";

    private const string TypeLine = "        A: android:foregroundServiceType(0x01010599)=(type 0x11)0x1\n";

    private const string ReceiverLines =
        "      E: receiver (line=159)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.play.core.assetpacks.SessionStateBroadcastReceiver\" (Raw: \"com.google.android.play.core.assetpacks.SessionStateBroadcastReceiver\")\n";

    /// <summary>What aapt says of the bundle's base APK of 28 October before anything was changed, shortened: both services, the foreground one with its type.</summary>
    private const string TypedDump = DumpStart + ExtractionServiceLines + ForegroundServiceLines + TypeLine + ReceiverLines;

    /// <summary>What aapt says of the base APK Play delivered to the owner's phone as versionCode 1, shortened: the foreground service taken out.</summary>
    private const string VersionCode1Dump = DumpStart + ExtractionServiceLines + ReceiverLines;

    /// <summary>What the bundle's base APK is to say: both services, no foreground service type.</summary>
    private const string KeptDump = DumpStart + ExtractionServiceLines + ForegroundServiceLines + ReceiverLines;

    /// <summary>What aapt says of the test package, shortened: one APK, nothing of Play Asset Delivery.</summary>
    private const string TestPackageDump = DumpStart
        + "      E: activity (line=70)\n"
        + "        A: android:name(0x01010003)=\"com.unity3d.player.UnityPlayerGameActivity\" (Raw: \"com.unity3d.player.UnityPlayerGameActivity\")\n";

    private static XElement[] ServicesIn(string manifest) =>
        XDocument.Parse(manifest).Root!.Element("application")!.Elements("service")
            .Where(service => (string?)service.Attribute(Android + "name") == ManifestAssetDelivery.ForegroundService)
            .ToArray();

    [Fact]
    public void The_foreground_service_is_kept_and_the_merger_is_told_to_take_off_its_type_alone()
    {
        string written = ManifestAssetDelivery.KeepWithoutItsType(UnitysManifest);

        XElement service = Assert.Single(ServicesIn(written));
        Assert.Equal("android:foregroundServiceType", (string?)service.Attribute(Tools + "remove"));
        Assert.Equal(new[] { "name", "remove" }, service.Attributes().Select(attribute => attribute.Name.LocalName));
        XElement application = XDocument.Parse(written).Root!.Element("application")!;
        Assert.Equal("game", (string?)application.Attribute(Android + "appCategory"));
        Assert.Single(application.Elements("activity"));
        Assert.Equal(written, ManifestAssetDelivery.KeepWithoutItsType(written));
    }

    [Fact]
    public void It_goes_with_the_other_withdrawals_in_the_steps_order()
    {
        string all = ManifestAssetDelivery.KeepWithoutItsType(ManifestPlayGamesStart.Withdraw(ManifestLoggingBackend.Withdraw(UnitysManifest)));

        Assert.Null(Assert.Single(ServicesIn(all)).Attribute(Tools + "node"));
        Assert.Single(XDocument.Parse(all).Root!.Element("application")!.Elements("provider"));
        Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.KeepWithoutItsType("<application />"));
    }

    [Fact]
    public void A_bundle_without_the_foreground_service_stops_the_build_as_versionCode_1_should_have_been_stopped()
    {
        InvalidOperationException stopped = Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.RequireKeptWithoutItsTypeIn(VersionCode1Dump));

        Assert.Contains(ManifestAssetDelivery.ForegroundService, stopped.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ManifestAssetDelivery.ExtractionService, stopped.Message, StringComparison.Ordinal);
        string asAnActivity = KeptDump.Replace("E: service (line=153)", "E: activity (line=153)", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.RequireKeptWithoutItsTypeIn(asAnActivity));
    }

    [Fact]
    public void A_bundle_whose_foreground_service_keeps_its_type_stops_the_build_and_one_without_the_type_passes()
    {
        InvalidOperationException stopped = Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.RequireKeptWithoutItsTypeIn(TypedDump));

        Assert.Contains("android:foregroundServiceType", stopped.Message, StringComparison.Ordinal);
        ManifestAssetDelivery.RequireKeptWithoutItsTypeIn(KeptDump);
        ManifestAssetDelivery.RequireKeptWithoutItsTypeIn(KeptDump.Replace("\n", "\r\n", StringComparison.Ordinal));
    }

    [Fact]
    public void The_test_package_has_nothing_of_Play_Asset_Delivery_and_one_the_mark_reached_stops_the_build()
    {
        ManifestAssetDelivery.RequireAbsentFrom(TestPackageDump);

        InvalidOperationException stopped = Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.RequireAbsentFrom(TestPackageDump + ForegroundServiceLines));
        Assert.Contains(ManifestAssetDelivery.ForegroundService, stopped.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void What_is_not_a_dump_of_a_manifest_is_refused_by_both_checks()
    {
        const string Badging = "package: name='com.saturnwave.museumpuzzle'";

        Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.RequireKeptWithoutItsTypeIn(Badging));
        Assert.Throws<InvalidOperationException>(() => ManifestAssetDelivery.RequireAbsentFrom(Badging));
    }
}
