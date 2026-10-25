using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The iOS build's StoreKit step: what it reads of an exported Xcode project, and what it asks of one for the App
/// Store's purchases. <c>XcodeProjectText</c> is the Unity project's Editor source file, compiled into these tests;
/// the step itself runs only while an Xcode project is exported (unity/Assets/Editor/IosBuild.cs), which these tests
/// do not do. The project text is cut down from Unity 6.6's export with Unity IAP 5.4.4 (2026-10-24), its ids and
/// layout as written.
/// </summary>
public class XcodeProjectTextTests
{
    private const string AppsStoreKit = "\t\t\t\tA1A24C62BAAAB3EA75510B28 /* StoreKit.framework in Frameworks */,\n";
    private const string FrameworksStoreKit = "\t\t\t\tFEF4673A8DD14865BB898CF1 /* StoreKit.framework in Frameworks */,\n";

    /// <summary>The project as Unity and IAP write it: StoreKit in the app's and in UnityFramework's frameworks phase.</summary>
    private const string Exported =
        "// !$*UTF8*$!\n"
        + "{\n"
        + "\tarchiveVersion = 1;\n"
        + "\tobjects = {\n"
        + "\n"
        + "/* Begin PBXBuildFile section */\n"
        + "\t\t00000000008063A1000160D3 /* UnityRuntime.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = 7B079BAA2E828D2B00C8A203 /* UnityRuntime.framework */; };\n"
        + "\t\t5623C57717FDCB0800090B9E /* UIKit.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = 5623C57517FDCB0800090B9E /* UIKit.framework */; };\n"
        + "\t\t9D25ABA1213FB47800354C27 /* UnityFramework.framework in Embed Frameworks */ = {isa = PBXBuildFile; fileRef = 9D25AB9D213FB47800354C27 /* UnityFramework.framework */; settings = {ATTRIBUTES = (CodeSignOnCopy, RemoveHeadersOnCopy, ); }; };\n"
        + "\t\tA1A24C62BAAAB3EA75510B28 /* StoreKit.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = C801E7F3CBAB00070AC52BC6 /* StoreKit.framework */; };\n"
        + "\t\tD7C2D97F0F5BA8BF5205A09C /* UIKit.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = 5623C57517FDCB0800090B9E /* UIKit.framework */; };\n"
        + "\t\tFEF4673A8DD14865BB898CF1 /* StoreKit.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = C801E7F3CBAB00070AC52BC6 /* StoreKit.framework */; };\n"
        + "/* End PBXBuildFile section */\n"
        + "\n"
        + "/* Begin PBXFileReference section */\n"
        + "\t\t5623C57517FDCB0800090B9E /* UIKit.framework */ = {isa = PBXFileReference; lastKnownFileType = wrapper.framework; name = UIKit.framework; path = System/Library/Frameworks/UIKit.framework; sourceTree = SDKROOT; };\n"
        + "\t\t7B079BAA2E828D2B00C8A203 /* UnityRuntime.framework */ = {isa = PBXFileReference; lastKnownFileType = wrapper.framework; path = Frameworks/UnityRuntime.framework; sourceTree = \"<group>\"; };\n"
        + "\t\t9D25AB9D213FB47800354C27 /* UnityFramework.framework */ = {isa = PBXFileReference; explicitFileType = wrapper.framework; includeInIndex = 0; path = UnityFramework.framework; sourceTree = BUILT_PRODUCTS_DIR; };\n"
        + "\t\tC801E7F3CBAB00070AC52BC6 /* StoreKit.framework */ = {isa = PBXFileReference; lastKnownFileType = wrapper.framework; name = StoreKit.framework; path = System/Library/Frameworks/StoreKit.framework; sourceTree = SDKROOT; };\n"
        + "/* End PBXFileReference section */\n"
        + "\n"
        + "/* Begin PBXFrameworksBuildPhase section */\n"
        + "\t\t1D60588F0D05DD3D006BFB54 /* Frameworks */ = {\n"
        + "\t\t\tisa = PBXFrameworksBuildPhase;\n"
        + "\t\t\tbuildActionMask = 2147483647;\n"
        + "\t\t\tfiles = (\n"
        + AppsStoreKit
        + "\t\t\t);\n"
        + "\t\t\trunOnlyForDeploymentPostprocessing = 0;\n"
        + "\t\t};\n"
        + "\t\t5623C57017FDCB0800090B9E /* Frameworks */ = {\n"
        + "\t\t\tisa = PBXFrameworksBuildPhase;\n"
        + "\t\t\tbuildActionMask = 2147483647;\n"
        + "\t\t\tfiles = (\n"
        + "\t\t\t\t5623C57717FDCB0800090B9E /* UIKit.framework in Frameworks */,\n"
        + "\t\t\t);\n"
        + "\t\t\trunOnlyForDeploymentPostprocessing = 0;\n"
        + "\t\t};\n"
        + "\t\t9D25AB99213FB47800354C27 /* Frameworks */ = {\n"
        + "\t\t\tisa = PBXFrameworksBuildPhase;\n"
        + "\t\t\tbuildActionMask = 2147483647;\n"
        + "\t\t\tfiles = (\n"
        + "\t\t\t\t00000000008063A1000160D3 /* UnityRuntime.framework in Frameworks */,\n"
        + "\t\t\t\tD7C2D97F0F5BA8BF5205A09C /* UIKit.framework in Frameworks */,\n"
        + FrameworksStoreKit
        + "\t\t\t);\n"
        + "\t\t\trunOnlyForDeploymentPostprocessing = 0;\n"
        + "\t\t};\n"
        + "/* End PBXFrameworksBuildPhase section */\n"
        + "\n"
        + "/* Begin PBXNativeTarget section */\n"
        + "\t\t1D6058900D05DD3D006BFB54 /* Unity-iPhone */ = {\n"
        + "\t\t\tisa = PBXNativeTarget;\n"
        + "\t\t\tbuildConfigurationList = 1D6058960D05DD3E006BFB54 /* Build configuration list for PBXNativeTarget \"Unity-iPhone\" */;\n"
        + "\t\t\tbuildPhases = (\n"
        + "\t\t\t\t1D60588D0D05DD3D006BFB54 /* Resources */,\n"
        + "\t\t\t\t1D60588F0D05DD3D006BFB54 /* Frameworks */,\n"
        + "\t\t\t\t9D25ABAB213FB47800354C27 /* Embed Frameworks */,\n"
        + "\t\t\t);\n"
        + "\t\t\tbuildRules = (\n"
        + "\t\t\t);\n"
        + "\t\t\tname = \"Unity-iPhone\";\n"
        + "\t\t\tproductName = \"iPhone-target\";\n"
        + "\t\t\tproductType = \"com.apple.product-type.application\";\n"
        + "\t\t};\n"
        + "\t\t5623C57217FDCB0800090B9E /* Unity-iPhone Tests */ = {\n"
        + "\t\t\tisa = PBXNativeTarget;\n"
        + "\t\t\tbuildPhases = (\n"
        + "\t\t\t\t5623C57017FDCB0800090B9E /* Frameworks */,\n"
        + "\t\t\t);\n"
        + "\t\t\tname = \"Unity-iPhone Tests\";\n"
        + "\t\t\tproductType = \"com.apple.product-type.bundle.unit-test\";\n"
        + "\t\t};\n"
        + "\t\t9D25AB9C213FB47800354C27 /* UnityFramework */ = {\n"
        + "\t\t\tisa = PBXNativeTarget;\n"
        + "\t\t\tbuildPhases = (\n"
        + "\t\t\t\t9D25AB99213FB47800354C27 /* Frameworks */,\n"
        + "\t\t\t);\n"
        + "\t\t\tname = UnityFramework;\n"
        + "\t\t\tproductType = \"com.apple.product-type.framework\";\n"
        + "\t\t};\n"
        + "/* End PBXNativeTarget section */\n"
        + "\t};\n"
        + "\trootObject = 29B97313FDCFA39411CA2CEA /* Project object */;\n"
        + "}\n";

    [Fact]
    public void The_targets_are_read_by_their_names_unquoted_in_their_order()
    {
        Assert.Equal(new[] { "Unity-iPhone", "Unity-iPhone Tests", "UnityFramework" }, XcodeProjectText.Read(Exported).TargetNames);
    }

    [Fact]
    public void A_framework_is_linked_by_the_targets_whose_frameworks_phase_holds_a_build_file_of_it()
    {
        XcodeProjectText project = XcodeProjectText.Read(Exported);

        Assert.Equal(new[] { "Unity-iPhone", "UnityFramework" }, project.TargetsLinking("StoreKit.framework"));
        Assert.Equal(new[] { "Unity-iPhone Tests", "UnityFramework" }, project.TargetsLinking("UIKit.framework"));
        Assert.Equal(new[] { "UnityFramework" }, project.TargetsLinking("UnityRuntime.framework"));
        Assert.Empty(project.TargetsLinking("AdSupport.framework"));
    }

    [Fact]
    public void A_framework_only_embedded_is_not_linked()
    {
        // The app embeds UnityFramework in a copy phase of its own; that is no frameworks phase.
        Assert.Empty(XcodeProjectText.Read(Exported).TargetsLinking("UnityFramework.framework"));
    }

    [Fact]
    public void A_framework_is_known_by_its_file_not_by_the_comment_beside_it()
    {
        string misnamed = Exported.Replace(
            "FEF4673A8DD14865BB898CF1 /* StoreKit.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = C801E7F3CBAB00070AC52BC6",
            "FEF4673A8DD14865BB898CF1 /* StoreKit.framework in Frameworks */ = {isa = PBXBuildFile; fileRef = 5623C57517FDCB0800090B9E");

        Assert.Equal(new[] { "Unity-iPhone" }, XcodeProjectText.Read(misnamed).TargetsLinking("StoreKit.framework"));
    }

    [Fact]
    public void Lines_that_end_with_a_carriage_return_read_the_same()
    {
        Assert.Equal(new[] { "Unity-iPhone", "UnityFramework" }, XcodeProjectText.Read(Exported.Replace("\n", "\r\n")).TargetsLinking("StoreKit.framework"));
    }

    [Fact]
    public void A_text_without_a_native_target_is_no_project()
    {
        Assert.Throws<InvalidOperationException>(() => XcodeProjectText.Read("<?xml version=\"1.0\"?><plist version=\"1.0\"><dict/></plist>"));
    }

    [Fact]
    public void The_exported_project_has_everything_the_purchases_need()
    {
        Assert.Empty(StoreKitInTheProject.Missing(XcodeProjectText.Read(Exported)));
    }

    /// <summary>What Unity writes without IAP's step, as from an Editor whose active target is not iOS: StoreKit nowhere, each target named.</summary>
    [Fact]
    public void A_project_written_without_the_purchase_librarys_step_is_told_what_it_lacks()
    {
        string withoutTheStep = Exported.Replace(AppsStoreKit, string.Empty).Replace(FrameworksStoreKit, string.Empty);

        Assert.Equal(
            new[] { "Unity-iPhone does not link StoreKit.framework", "UnityFramework does not link StoreKit.framework" },
            StoreKitInTheProject.Missing(XcodeProjectText.Read(withoutTheStep)));
    }

    [Fact]
    public void StoreKit_linked_into_the_app_alone_leaves_UnityFramework_named()
    {
        string appAlone = Exported.Replace(FrameworksStoreKit, string.Empty);

        Assert.Equal(new[] { "UnityFramework does not link StoreKit.framework" }, StoreKitInTheProject.Missing(XcodeProjectText.Read(appAlone)));
    }

    [Fact]
    public void A_project_without_Unitys_targets_names_them()
    {
        string renamed = Exported.Replace("name = UnityFramework;", "name = SomethingElse;");

        Assert.Equal(new[] { "the project has no target UnityFramework" }, StoreKitInTheProject.Missing(XcodeProjectText.Read(renamed)));
    }
}
