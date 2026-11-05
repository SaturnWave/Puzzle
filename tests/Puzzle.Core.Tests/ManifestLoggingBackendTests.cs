using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Android build's manifest step, for Google's logging transport: the Unity library's manifest marks the
/// transport's backend registration for removal, so the merged manifest registers no backend and the billing
/// library's logs never leave the phone (T186); and the check of the built package, whose manifest must not register
/// it. <c>ManifestLoggingBackend</c> is the Unity project's Editor source file, compiled into these tests; the step and
/// the check run only while a package is built (unity/Assets/Editor/AndroidBuild.cs), which these tests do not do.
/// </summary>
public class ManifestLoggingBackendTests
{
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
    private static readonly XNamespace Tools = "http://schemas.android.com/tools";

    /// <summary>A manifest as Unity writes the library's, its application cut to what matters here.</summary>
    private const string UnitysManifest =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.unity3d.player\">\n"
        + "  <uses-permission android:name=\"android.permission.INTERNET\" />\n"
        + "  <application android:appCategory=\"game\" android:extractNativeLibs=\"true\">\n"
        + "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" />\n"
        + "  </application>\n"
        + "</manifest>\n";

    /// <summary>What aapt says of the 25 October package's manifest around the transport's services (aapt dump xmltree … AndroidManifest.xml).</summary>
    private const string DumpWithTheBackend =
        "N: android=http://schemas.android.com/apk/res/android\n"
        + "  E: manifest (line=2)\n"
        + "    A: package=\"com.saturnwave.museumpuzzle\" (Raw: \"com.saturnwave.museumpuzzle\")\n"
        + "      E: service (line=168)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.datatransport.runtime.backends.TransportBackendDiscovery\" (Raw: \"com.google.android.datatransport.runtime.backends.TransportBackendDiscovery\")\n"
        + "        A: android:exported(0x01010010)=(type 0x12)0x0\n"
        + "        E: meta-data (line=171)\n"
        + "          A: android:name(0x01010003)=\"backend:com.google.android.datatransport.cct.CctBackendFactory\" (Raw: \"backend:com.google.android.datatransport.cct.CctBackendFactory\")\n"
        + "          A: android:value(0x01010024)=\"cct\" (Raw: \"cct\")\n"
        + "      E: service (line=175)\n"
        + "        A: android:name(0x01010003)=\"com.google.android.datatransport.runtime.scheduling.jobscheduling.JobInfoSchedulerService\" (Raw: \"com.google.android.datatransport.runtime.scheduling.jobscheduling.JobInfoSchedulerService\")\n";

    private static XElement[] RegistrationsIn(string manifest) =>
        XDocument.Parse(manifest).Root!.Element("application")!.Elements("service")
            .Where(service => (string?)service.Attribute(Android + "name") == ManifestLoggingBackend.DiscoveryService)
            .SelectMany(service => service.Elements("meta-data"))
            .Where(data => (string?)data.Attribute(Android + "name") == ManifestLoggingBackend.CctBackend)
            .ToArray();

    [Fact]
    public void The_backend_is_marked_for_the_merger_to_take_it_out_of_the_libraries_and_the_rest_stays_as_it_was()
    {
        string written = ManifestLoggingBackend.Withdraw(UnitysManifest);

        XElement registration = Assert.Single(RegistrationsIn(written));
        Assert.Equal("remove", (string?)registration.Attribute(Tools + "node"));
        Assert.Null(registration.Attribute(Android + "value"));
        XElement service = registration.Parent!;
        Assert.Equal(new[] { "name" }, service.Attributes().Select(attribute => attribute.Name.LocalName));
        XElement application = XDocument.Parse(written).Root!.Element("application")!;
        Assert.Equal("game", (string?)application.Attribute(Android + "appCategory"));
        Assert.Single(application.Elements("activity"));
        Assert.Single(XDocument.Parse(written).Root!.Elements("uses-permission"));
    }

    [Fact]
    public void Withdrawing_again_changes_nothing_so_the_step_may_run_on_a_manifest_it_wrote_before()
    {
        string once = ManifestLoggingBackend.Withdraw(UnitysManifest);

        Assert.Equal(once, ManifestLoggingBackend.Withdraw(once));
    }

    [Fact]
    public void A_registration_the_manifest_has_already_is_marked_where_it_stands()
    {
        string declared = UnitysManifest.Replace(
            "  </application>",
            $"    <service android:name=\"{ManifestLoggingBackend.DiscoveryService}\" android:exported=\"false\">\n"
            + $"      <meta-data android:name=\"{ManifestLoggingBackend.CctBackend}\" android:value=\"cct\" />\n"
            + "    </service>\n  </application>",
            StringComparison.Ordinal);

        string written = ManifestLoggingBackend.Withdraw(declared);

        XElement registration = Assert.Single(RegistrationsIn(written));
        Assert.Equal("remove", (string?)registration.Attribute(Tools + "node"));
        Assert.Equal("false", (string?)registration.Parent!.Attribute(Android + "exported"));
    }

    [Fact]
    public void A_manifest_without_an_application_or_not_a_manifest_stops_the_step()
    {
        Assert.Throws<InvalidOperationException>(() => ManifestLoggingBackend.Withdraw("<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" />"));
        Assert.Throws<InvalidOperationException>(() => ManifestLoggingBackend.Withdraw("<application />"));
    }

    [Fact]
    public void A_package_that_still_registers_the_backend_stops_the_build_and_one_without_it_passes()
    {
        string withdrawn = DumpWithTheBackend
            .Replace("        E: meta-data (line=171)\n", string.Empty, StringComparison.Ordinal)
            .Replace("          A: android:name(0x01010003)=\"backend:com.google.android.datatransport.cct.CctBackendFactory\" (Raw: \"backend:com.google.android.datatransport.cct.CctBackendFactory\")\n", string.Empty, StringComparison.Ordinal)
            .Replace("          A: android:value(0x01010024)=\"cct\" (Raw: \"cct\")\n", string.Empty, StringComparison.Ordinal);

        InvalidOperationException stopped = Assert.Throws<InvalidOperationException>(() => ManifestLoggingBackend.RequireWithdrawnFrom(DumpWithTheBackend));
        Assert.Contains(ManifestLoggingBackend.CctBackend, stopped.Message, StringComparison.Ordinal);
        ManifestLoggingBackend.RequireWithdrawnFrom(withdrawn);
        Assert.Throws<InvalidOperationException>(() => ManifestLoggingBackend.RequireWithdrawnFrom("package: name='com.saturnwave.museumpuzzle'"));
    }
}
