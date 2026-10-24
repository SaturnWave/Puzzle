using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Exports the game's Xcode project from the command line.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.IosBuild.Run
    /// The project lands in unity/Build/iOS/, with the scene and the content of the Android package: what the Editor
    /// derives is brought up to date first, for the reason AndroidBuild gives. Unity exports it on Windows too; it is
    /// archived, signed and sent to TestFlight on a Mac with Xcode (docs/IOS_BUILD.md). The export needs Unity's iOS
    /// Build Support module, which only the owner installs: without it the tool stops before it changes anything, and
    /// says so. Nothing here needs that module's Xcode library (UnityEditor.iOS.Xcode): the Info.plist step edits the
    /// list as XML (<see cref="InfoPlist"/>), so this file compiles on an Editor without the module.
    /// </summary>
    public static class IosBuild
    {
        private const string ProjectFolder = "Build/iOS";

        /// <summary>
        /// The oldest iOS the game runs on: the lowest Unity 6.6 supports and Xcode still builds for. It reaches the
        /// iPhone 6s and the first SE (2015-2016) and every iPhone since; the game asks nothing of a later iOS (its taps
        /// need iOS 10, Plugins/iOS/TapticEngine.mm).
        /// </summary>
        private const string OldestIos = "15.0";

        /// <summary>PlayerSettings' number for 64-bit ARM: the only architecture iPhones and the App Store take.</summary>
        private const int Arm64 = 1;

        public static void Run()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Debug.LogError(
                    "iOS build: not started, because this Unity has no iOS Build Support. The owner adds it in Unity Hub "
                    + "(Installs, 6000.6.4f1, Add modules, iOS Build Support); then this tool is run again. Nothing was changed.");
                EditorApplication.Exit(1);
                return;
            }

            try
            {
                ProjectSetup.BringUpToDate();
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException)
            {
                Debug.LogError($"iOS build: not started, because the project's setup failed: {failure.Message}");
                EditorApplication.Exit(1);
                return;
            }

            ApplyPlayerSettings();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainSceneBuilder.ScenePath },
                locationPathName = ProjectFolder,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };
            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"iOS build: {summary.result} with {summary.totalErrors} errors.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"iOS build: the Xcode project is at {summary.outputPath}, made in {summary.totalTime.TotalMinutes:F1} min; archive it on the Mac (docs/IOS_BUILD.md).");
        }

        /// <summary>
        /// What the app's Info.plist says beyond what Unity writes, one line each, in the order they are written
        /// (<see cref="IosInfoPlistStep"/>). The game uses no encryption beyond Apple's own (it makes no network call
        /// at all; the Android build withdraws the internet), so App Store Connect asks no export question of a build.
        /// It tracks no one and uses no camera, microphone or location: no purpose string for them is left, whatever a
        /// setting or a library would write. A purchase, ad or reporting library changes this list, and the privacy text.
        /// The screen's turning, as the owner decided on 2026-10-24 (Scripts/Layout/ScreenClass.cs): an iPhone stands
        /// upright only; an iPad turns all four ways and asks for no full screen, so it runs in a window beside other
        /// apps and in Stage Manager. Apple requires the four ways of every iPad app that does not take the whole screen
        /// (ITMS-90474), and from the iOS 27 SDK on it ignores the full-screen key anyway (TN3192).
        /// </summary>
        internal static readonly PlistLine[] InfoPlistLines =
        {
            PlistLine.Says("ITSAppUsesNonExemptEncryption", false),
            PlistLine.Withdraw("NSUserTrackingUsageDescription"),
            PlistLine.Withdraw("NSCameraUsageDescription"),
            PlistLine.Withdraw("NSMicrophoneUsageDescription"),
            PlistLine.Withdraw("NSLocationWhenInUseUsageDescription"),
            PlistLine.Lists("UISupportedInterfaceOrientations", "UIInterfaceOrientationPortrait"),
            PlistLine.Lists(
                "UISupportedInterfaceOrientations~ipad",
                "UIInterfaceOrientationPortrait",
                "UIInterfaceOrientationPortraitUpsideDown",
                "UIInterfaceOrientationLandscapeLeft",
                "UIInterfaceOrientationLandscapeRight"),
            PlistLine.Withdraw("UIRequiresFullScreen"),
            PlistLine.Withdraw("UIRequiresFullScreen~ipad"),
        };

        /// <summary>
        /// The Android package's identity and code (IL2CPP, stripped as on Android, which a phone has proven), for
        /// iPhones and iPads, turning with the device: the Info.plist lets an iPhone stand upright only and an iPad every
        /// way (<see cref="InfoPlistLines"/>), and the game's root asks for the same once it runs. Unity applies the rest
        /// of the project's settings to both (the name, the splash, the status bar hidden, other apps' sound mixed under
        /// the game's and the silent switch obeyed).
        /// </summary>
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = GameIdentity.CompanyName;
            PlayerSettings.productName = GameIdentity.ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, GameIdentity.ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, Arm64);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android));
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = OldestIos;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;

            // The game turns with an iPad, all four ways, from its first frame; an iPhone's Info.plist keeps it upright.
            // The Android build sets its own package upright again (AndroidBuild).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // An iPad runs in a window beside other apps and in Stage Manager, and is laid out for any shape it is given.
            PlayerSettings.iOS.requiresFullScreen = false;

            // Xcode signs on the Mac with the owner's team, which xcodebuild is given there; the project carries none.
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;

            // The build's number as Android's; Xcode raises it on the way to App Store Connect when that has it already.
            PlayerSettings.iOS.buildNumber = PlayerSettings.Android.bundleVersionCode.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The build's Info.plist step: once Unity has written the Xcode project of an iOS build (this tool's, or any
    /// other), the app's Info.plist is given the build's lines (<see cref="IosBuild.InfoPlistLines"/>). A list that
    /// cannot be read or written stops the build: a project without them would be asked about, or refused, on upload.
    /// </summary>
    public sealed class IosInfoPlistStep : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            string plist = Path.Combine(report.summary.outputPath, "Info.plist");
            File.WriteAllText(plist, InfoPlist.Apply(File.ReadAllText(plist), IosBuild.InfoPlistLines));
            Debug.Log($"iOS build: the Info.plist says {string.Join<PlistLine>(", ", IosBuild.InfoPlistLines)}.");
        }
    }
}
