using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Android build's manifest step: what it writes of each permission on its list into the Unity library's manifest;
/// and the check of the built package, which must ask for exactly the build's permissions once every manifest is
/// merged. <c>ManifestPermissions</c> and <c>PackagePermissions</c> are the Unity project's Editor source files,
/// compiled into these tests; the step and the check run only while a package is built
/// (unity/Assets/Editor/AndroidBuild.cs), which these tests do not do.
/// </summary>
public class ManifestPermissionsTests
{
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
    private static readonly XNamespace Tools = "http://schemas.android.com/tools";

    /// <summary>A manifest as Unity writes the library's: its package, the internet asked for, and the application with its backup flag.</summary>
    private const string UnitysManifest =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.unity3d.player\">\n"
        + "  <uses-permission android:name=\"android.permission.INTERNET\" />\n"
        + "  <application android:allowBackup=\"true\" android:extractNativeLibs=\"true\">\n"
        + "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" />\n"
        + "  </application>\n"
        + "</manifest>\n";

    private const string Vibrate = "android.permission.VIBRATE";
    private const string Internet = "android.permission.INTERNET";

    private static XElement[] DeclarationsOf(string manifest, string permission) =>
        XDocument.Parse(manifest).Root!.Elements("uses-permission").Where(element => (string?)element.Attribute(Android + "name") == permission).ToArray();

    [Fact]
    public void Asking_for_vibration_declares_it_once_before_the_application_and_leaves_the_rest_as_it_was()
    {
        string written = ManifestPermissions.Apply(UnitysManifest, new[] { PermissionLine.Ask(Vibrate) });

        XElement vibrate = Assert.Single(DeclarationsOf(written, Vibrate));
        Assert.Null(vibrate.Attribute(Tools + "node"));
        Assert.Equal("application", vibrate.ElementsAfterSelf().First().Name.LocalName);
        Assert.Single(DeclarationsOf(written, Internet));
        Assert.Equal("true", (string?)XDocument.Parse(written).Root!.Element("application")!.Attribute(Android + "allowBackup"));
    }

    [Fact]
    public void Asking_again_changes_nothing_so_the_step_may_run_on_a_manifest_it_wrote_before()
    {
        string once = ManifestPermissions.Apply(UnitysManifest, new[] { PermissionLine.Ask(Vibrate) });

        string twice = ManifestPermissions.Apply(once, new[] { PermissionLine.Ask(Vibrate) });

        Assert.Equal(once, twice);
    }

    [Fact]
    public void A_withdrawn_permission_loses_its_declaration_and_is_marked_for_the_merger_to_take_it_out_of_the_libraries_too()
    {
        // The build asked so from 2026-10-16 until v1 asked for the internet again (AndroidBuild.Permissions).
        string written = ManifestPermissions.Apply(UnitysManifest, new[] { PermissionLine.Ask(Vibrate), PermissionLine.Withdraw(Internet) });

        XElement internet = Assert.Single(DeclarationsOf(written, Internet));
        Assert.Equal("remove", (string?)internet.Attribute(Tools + "node"));
        Assert.Single(DeclarationsOf(written, Vibrate));
    }

    [Fact]
    public void Asking_for_a_permission_marked_for_removal_takes_the_mark_away()
    {
        string withdrawn = ManifestPermissions.Apply(UnitysManifest, new[] { PermissionLine.Withdraw(Vibrate) });

        string asked = ManifestPermissions.Apply(withdrawn, new[] { PermissionLine.Ask(Vibrate) });

        Assert.Null(Assert.Single(DeclarationsOf(asked, Vibrate)).Attribute(Tools + "node"));
    }

    [Fact]
    public void Asking_for_the_internet_takes_away_the_mark_an_earlier_build_left_in_the_kept_manifest()
    {
        // The purchase spike's trap: a manifest written while the internet was withdrawn, kept by Unity for the next build.
        string kept = ManifestPermissions.Apply(UnitysManifest, new[] { PermissionLine.Ask(Vibrate), PermissionLine.Withdraw(Internet) });

        string written = ManifestPermissions.Apply(kept, new[] { PermissionLine.Ask(Vibrate), PermissionLine.Ask(Internet) });

        Assert.Null(Assert.Single(DeclarationsOf(written, Internet)).Attribute(Tools + "node"));
        Assert.Single(DeclarationsOf(written, Vibrate));
    }

    /// <summary>What aapt 36.0.0 says of the v1 package's permissions, as the purchase spike read them with the internet asked.</summary>
    private const string V1Dump =
        "package: com.saturnwave.puzzle\n"
        + "uses-permission: name='android.permission.INTERNET'\n"
        + "uses-permission: name='android.permission.VIBRATE'\n"
        + "uses-permission: name='com.android.vending.BILLING'\n"
        + "permission: com.saturnwave.puzzle.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION\n"
        + "uses-permission: name='com.saturnwave.puzzle.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION'\n"
        + "uses-permission: name='android.permission.ACCESS_NETWORK_STATE'\n";

    private static readonly string[] V1Permissions =
    {
        Vibrate, Internet, "com.android.vending.BILLING", "android.permission.ACCESS_NETWORK_STATE",
        "com.saturnwave.puzzle" + PackagePermissions.UnitysReceiverSuffix,
    };

    [Fact]
    public void The_dump_of_a_package_lists_what_it_asks_for_and_not_what_it_only_declares()
    {
        Assert.Equal(
            new[]
            {
                Internet, Vibrate, "com.android.vending.BILLING", "com.saturnwave.puzzle.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION",
                "android.permission.ACCESS_NETWORK_STATE",
            },
            PackagePermissions.AskedIn(V1Dump));
        Assert.Equal(new[] { "android.permission.POST_NOTIFICATIONS" }, PackagePermissions.AskedIn("package: a.b\nuses-permission-sdk-23: name='android.permission.POST_NOTIFICATIONS'\n"));
        Assert.Throws<InvalidOperationException>(() => PackagePermissions.AskedIn("ERROR: dump failed because no AndroidManifest.xml found"));
    }

    [Fact]
    public void The_v1_package_asks_for_exactly_its_five_permissions()
    {
        PackagePermissions.Require(PackagePermissions.AskedIn(V1Dump).ToArray(), V1Permissions);
    }

    [Theory]
    [InlineData("uses-permission: name='com.google.android.gms.permission.AD_ID'\n", "also asks for com.google.android.gms.permission.AD_ID")]
    [InlineData("uses-permission: name='android.permission.ACCESS_COARSE_LOCATION'\n", "also asks for android.permission.ACCESS_COARSE_LOCATION")]
    public void A_package_that_asks_for_more_stops_the_build_and_says_what(string extra, string said)
    {
        string[] asked = PackagePermissions.AskedIn(V1Dump + extra).ToArray();

        var stopped = Assert.Throws<InvalidOperationException>(() => PackagePermissions.Require(asked, V1Permissions));
        Assert.Contains(said, stopped.Message);
    }

    [Fact]
    public void A_package_whose_internet_an_old_manifest_still_withdraws_stops_the_build()
    {
        string withdrawn = V1Dump.Replace("uses-permission: name='android.permission.INTERNET'\n", string.Empty);

        var stopped = Assert.Throws<InvalidOperationException>(() => PackagePermissions.Require(PackagePermissions.AskedIn(withdrawn).ToArray(), V1Permissions));
        Assert.Contains("does not ask for android.permission.INTERNET", stopped.Message);
    }

    [Fact]
    public void What_is_not_an_android_manifest_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => ManifestPermissions.Apply("<resources />", new[] { PermissionLine.Ask(Vibrate) }));
    }

    [Fact]
    public void The_manifest_of_a_game_passes_and_keeps_Android_honouring_the_games_own_turning()
    {
        string game = UnitysManifest.Replace("<application ", "<application android:appCategory=\"game\" ");

        ManifestGameCategory.Require(ManifestPermissions.Apply(game, new[] { PermissionLine.Ask(Vibrate) }));
    }

    [Theory]
    [InlineData("<application android:allowBackup=\"true\">")]
    [InlineData("<application android:appCategory=\"productivity\">")]
    public void A_manifest_that_is_not_a_games_stops_the_build(string application)
    {
        string manifest = UnitysManifest.Replace("<application android:allowBackup=\"true\" android:extractNativeLibs=\"true\">", application);

        Assert.Throws<InvalidOperationException>(() => ManifestGameCategory.Require(manifest));
        Assert.Throws<InvalidOperationException>(() => ManifestGameCategory.Require("<manifest />"));
    }
}
