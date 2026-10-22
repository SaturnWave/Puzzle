using System.Xml.Linq;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The iOS build's Info.plist step: what it writes of each line of its list into the app's Info.plist.
/// <c>InfoPlist</c> is the Unity project's Editor source file, compiled into these tests; the step itself runs only
/// while an Xcode project is exported (unity/Assets/Editor/IosBuild.cs), which these tests do not do.
/// </summary>
public class InfoPlistTests
{
    /// <summary>A list as Unity writes the app's: Apple's document type, tabs, a camera purpose string and a nested dictionary.</summary>
    private const string UnitysPlist =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
        + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
        + "<plist version=\"1.0\">\n"
        + "<dict>\n"
        + "\t<key>CFBundleIdentifier</key>\n"
        + "\t<string>com.saturnwave.puzzle</string>\n"
        + "\t<key>NSCameraUsageDescription</key>\n"
        + "\t<string></string>\n"
        + "\t<key>UIRequiresFullScreen</key>\n"
        + "\t<true/>\n"
        + "\t<key>UIApplicationSceneManifest</key>\n"
        + "\t<dict>\n"
        + "\t\t<key>NSCameraUsageDescription</key>\n"
        + "\t\t<string>a nested key of the same name is not the app's</string>\n"
        + "\t</dict>\n"
        + "</dict>\n"
        + "</plist>\n";

    private const string Encryption = "ITSAppUsesNonExemptEncryption";
    private const string Camera = "NSCameraUsageDescription";

    /// <summary>The top dictionary's keys with the name of each one's value element, in their order.</summary>
    private static (string Key, string Value)[] PairsOf(string plist)
    {
        XElement[] items = XDocument.Parse(plist).Root!.Element("dict")!.Elements().ToArray();
        return Enumerable.Range(0, items.Length / 2).Select(pair => (items[2 * pair].Value, items[2 * pair + 1].Name.LocalName)).ToArray();
    }

    [Fact]
    public void A_key_that_was_not_there_is_added_once_at_the_end_on_lines_of_its_own()
    {
        string written = InfoPlist.Apply(UnitysPlist, new[] { PlistLine.Says(Encryption, false) });

        Assert.Equal((Encryption, "false"), PairsOf(written).Last());
        Assert.Single(PairsOf(written), pair => pair.Key == Encryption);
        Assert.Contains("\t<key>ITSAppUsesNonExemptEncryption</key>\n\t<false />\n</dict>", written);
    }

    [Fact]
    public void A_key_that_was_there_keeps_its_place_and_takes_the_new_value()
    {
        string written = InfoPlist.Apply(UnitysPlist, new[] { PlistLine.Says("UIRequiresFullScreen", false) });

        Assert.Equal(PairsOf(UnitysPlist).Select(pair => pair.Key), PairsOf(written).Select(pair => pair.Key));
        Assert.Contains(("UIRequiresFullScreen", "false"), PairsOf(written));
    }

    [Fact]
    public void Writing_again_changes_nothing_so_the_step_may_run_on_a_list_it_wrote_before()
    {
        PlistLine[] lines = { PlistLine.Says(Encryption, false), PlistLine.Withdraw(Camera) };
        string once = InfoPlist.Apply(UnitysPlist, lines);

        string twice = InfoPlist.Apply(once, lines);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void A_withdrawn_key_goes_with_its_value_and_its_line_and_a_nested_key_of_the_same_name_stays()
    {
        string written = InfoPlist.Apply(UnitysPlist, new[] { PlistLine.Withdraw(Camera) });

        Assert.DoesNotContain(PairsOf(written), pair => pair.Key == Camera);
        Assert.Contains("a nested key of the same name is not the app's", written);
        Assert.Contains("<string>com.saturnwave.puzzle</string>\n\t<key>UIRequiresFullScreen</key>", written);
    }

    [Fact]
    public void Withdrawing_a_key_that_is_not_there_leaves_the_list_as_it_was_read_its_document_type_included()
    {
        string written = InfoPlist.Apply(UnitysPlist, new[] { PlistLine.Withdraw("NSUserTrackingUsageDescription") });

        Assert.Equal(UnitysPlist.Replace("<true/>", "<true />"), written);
    }

    [Fact]
    public void A_file_that_is_not_a_property_list_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => InfoPlist.Apply("<manifest><dict /></manifest>", new[] { PlistLine.Says(Encryption, false) }));
    }

    [Fact]
    public void A_key_without_a_value_is_refused_rather_than_written_around()
    {
        const string broken = "<plist version=\"1.0\"><dict><key>NSCameraUsageDescription</key></dict></plist>";

        Assert.Throws<InvalidOperationException>(() => InfoPlist.Apply(broken, new[] { PlistLine.Withdraw(Camera) }));
    }
}
