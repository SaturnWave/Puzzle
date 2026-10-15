using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Android build's manifest step: what it writes of each permission on its list into the Unity library's manifest.
/// <c>ManifestPermissions</c> is the Unity project's Editor source file, compiled into these tests; the step itself
/// runs only while a package is built (unity/Assets/Editor/AndroidBuild.cs), which these tests do not do.
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
        // Not on the build's list: the internet waits for the owner's decision. This is what a second line would do.
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
    public void What_is_not_an_android_manifest_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => ManifestPermissions.Apply("<resources />", new[] { PermissionLine.Ask(Vibrate) }));
    }
}
