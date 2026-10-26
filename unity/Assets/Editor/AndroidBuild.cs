#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Builds the Android package from the command line.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.AndroidBuild.Run
    /// The package lands in unity/Build/Android/puzzle.apk, signed with Unity's debug key: good for installing on a
    /// test phone, not for a store. What the Editor derives (the atlas, the art catalogue, the typefaces, the scene)
    /// is brought up to date first: the game reads the catalogue by the order of its lists of drawings, and a
    /// package with a catalogue older than those lists would stop at the first drawing added since. The manifest is
    /// given the build's permissions on the way (<see cref="AndroidManifestStep"/>), from a manifest Unity has written
    /// anew, and the package that comes out must ask for exactly the permissions of <see cref="MergedPermissions"/>,
    /// as Android's own tool reads them from it (<see cref="PackagePermissions"/>), must register no backend for
    /// Google's logging transport (<see cref="ManifestLoggingBackend"/>), and must not start Play Games with the game's
    /// process (<see cref="ManifestPlayGamesStart"/>). The Unity library's build.gradle is given the Play Games library the
    /// game's Play Games plugin needs (<see cref="PlayGamesDependency"/>).
    /// </summary>
    public static class AndroidBuild
    {
        private const string PackagePath = "Build/Android/puzzle.apk";

        /// <summary>
        /// The Unity library's manifest in the Gradle project Unity keeps between builds. Unity leaves an earlier build's
        /// file in place, and with it what the manifest step wrote there, a permission withdrawn among it: a change of
        /// <see cref="Permissions"/> would not reach the package (the purchase spike saw INTERNET stay withdrawn,
        /// docs/research/iap-and-analytics.md §3.4). It is taken away before every build, and Unity writes it anew.
        /// </summary>
        private const string KeptLibraryManifest = "Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main/AndroidManifest.xml";

        /// <summary>The name under the icon is a placeholder: the game's name is decided last.</summary>
        private const string CompanyName = "SaturnWave";
        private const string ProductName = "Puzzle";

        /// <summary>
        /// The game's permanent package name, the same as its iOS bundle id (decided 2026-10-25): the stores know the game and
        /// its products by it (com.saturnwave.museumpuzzle.coins.pouch), so it stays when the name is decided.
        /// </summary>
        private const string PackageId = "com.saturnwave.museumpuzzle";

        public static void Run()
        {
            try
            {
                ProjectSetup.BringUpToDate();
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException)
            {
                Debug.LogError($"Android build: not started, because the project's setup failed: {failure.Message}");
                EditorApplication.Exit(1);
                return;
            }

            ApplyPlayerSettings();
            TakeAwayTheKeptManifest();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainSceneBuilder.ScenePath },
                locationPathName = PackagePath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Android build: {summary.result} with {summary.totalErrors} errors.");
                EditorApplication.Exit(1);
                return;
            }

            try
            {
                IReadOnlyList<string> asked = PackagePermissions.AskedIn(WhatAaptSays(summary.outputPath, "permissions"));
                PackagePermissions.Require(asked.ToArray(), MergedPermissions);
                Debug.Log($"Android build: the package asks for exactly {string.Join(", ", asked)}.");
                string manifest = WhatAaptSays(summary.outputPath, "xmltree", "AndroidManifest.xml");
                ManifestLoggingBackend.RequireWithdrawnFrom(manifest);
                Debug.Log($"Android build: the package registers no backend for Google's logging transport ({ManifestLoggingBackend.CctBackend} withdrawn).");
                ManifestPlayGamesStart.RequireWithdrawnFrom(manifest);
                Debug.Log($"Android build: the package does not start Play Games with the game ({ManifestPlayGamesStart.InitProvider} withdrawn).");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException || failure is System.ComponentModel.Win32Exception)
            {
                Debug.LogError($"Android build: the package at {summary.outputPath} is not to be installed: {failure.Message}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"Android build: succeeded, {summary.totalSize / (1024 * 1024)} MB at {summary.outputPath} in {summary.totalTime.TotalMinutes:F1} min.");
        }

        /// <summary>
        /// What the package's manifest says of Android's permissions beyond what Unity writes, one line each, in the
        /// order they are written (<see cref="AndroidManifestStep"/>). The internet permission was taken out on
        /// 2026-10-16, while the game sent nothing; v1 asks for it again (the owner's decision, 2026-10-24): the play
        /// statistics, once the player allows them, go to Aptabase over it, and Unity's purchase library sends its
        /// purchase events over it (Flow/PrivacyWords.cs says both). The application's backup flag is not this step's;
        /// it stays on by the earlier decision.
        /// </summary>
        internal static readonly PermissionLine[] Permissions =
        {
            PermissionLine.Ask("android.permission.VIBRATE"), // the short ticks of play (Scripts/Sound/Haptics.cs)
            PermissionLine.Ask("android.permission.INTERNET"), // the play statistics (Scripts/Statistics) and the purchase library's events
        };

        /// <summary>
        /// Every permission the built package asks for, once all the manifests are merged, and no other: the build's
        /// own two (<see cref="Permissions"/>); Google Play's billing and the look at whether a network is there, which
        /// Unity In-App Purchasing 5.4.4 brings; and the permission Unity's library declares for what it registers to
        /// hear. No advertising id (AD_ID), no location, no account: the store declarations (docs/GAME_DESIGN.md §20, Google Play's Data safety) rest on it.
        /// </summary>
        internal static readonly IReadOnlyCollection<string> MergedPermissions = new[]
        {
            "android.permission.VIBRATE",
            "android.permission.INTERNET",
            "com.android.vending.BILLING",
            "android.permission.ACCESS_NETWORK_STATE",
            PackageId + PackagePermissions.UnitysReceiverSuffix,
        };

        /// <summary>The Unity library's manifest of the build before, taken away so that Unity writes it anew; nothing where there is none.</summary>
        private static void TakeAwayTheKeptManifest()
        {
            if (File.Exists(KeptLibraryManifest))
            {
                File.Delete(KeptLibraryManifest);
                Debug.Log($"Android build: the kept {KeptLibraryManifest} was taken away, so Unity writes it anew with the build's permissions.");
            }
        }

        /// <summary>
        /// What Android's own tool (aapt, from the newest build tools of Unity's Android SDK) says of the package:
        /// <c>aapt dump permissions</c> for what it asks for, <c>aapt dump xmltree … AndroidManifest.xml</c> for its merged manifest.
        /// </summary>
        /// <exception cref="InvalidOperationException">No aapt was found, or it did not read the package.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception">aapt could not be started.</exception>
        private static string WhatAaptSays(string package, string what, string? file = null)
        {
            string buildTools = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "build-tools");
            string? aapt = Directory.Exists(buildTools)
                ? Directory.GetDirectories(buildTools).OrderByDescending(folder => Version.TryParse(Path.GetFileName(folder), out Version? number) ? number : new Version(0, 0))
                    .Select(folder => Path.Combine(folder, Application.platform == RuntimePlatform.WindowsEditor ? "aapt.exe" : "aapt"))
                    .FirstOrDefault(File.Exists)
                : null;
            if (aapt == null)
            {
                throw new InvalidOperationException($"no aapt was found under {buildTools} to read the package");
            }

            var reading = new ProcessStartInfo(aapt, $"dump {what} \"{Path.GetFullPath(package)}\"{(file == null ? string.Empty : " " + file)}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using Process tool = Process.Start(reading) ?? throw new InvalidOperationException($"{aapt} did not start");
            string dump = tool.StandardOutput.ReadToEnd();
            string complaint = tool.StandardError.ReadToEnd();
            tool.WaitForExit();
            if (tool.ExitCode != 0)
            {
                throw new InvalidOperationException($"{aapt} could not read the package (exit {tool.ExitCode}): {complaint.Trim()}");
            }

            return dump;
        }

        /// <summary>
        /// A 64-bit ARM package for Android 16 (API level 36, what Google Play asks for since 31 August 2026), named so
        /// that a later Unity cannot move the target on its own: from API level 37 on Android has no way out of its rules
        /// for large screens but the game category (<see cref="ManifestGameCategory"/>). The package opens upright, so a
        /// phone never shows it sideways; every way of standing is allowed for the game's own rule, which turns a tablet
        /// or an open foldable with the device once the game runs (Scripts/Engine/DeviceScreen.cs). The system bars are
        /// hidden with Android's default behaviour, under which one back gesture from the screen's edge goes back
        /// (<see cref="ManifestSystemBars"/>, Scripts/Engine/SystemBars.cs). The project's
        /// committed settings say the same, the target too, so a build leaves them as they were; the manifest step stops
        /// a package that would open another way (<see cref="ManifestOrientation"/>).
        /// </summary>
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.Android.systemBarsBehavior = AndroidSystemBarsBehavior.Default;
            EditorUserBuildSettings.buildAppBundle = false;
        }
    }

    /// <summary>
    /// The build's manifest step: once Unity has made the Gradle project of an Android build (this tool's, or any
    /// other), the Unity library's manifest is given the build's permissions (<see cref="AndroidBuild.Permissions"/>)
    /// and withdraws the backend of Google's logging transport (<see cref="ManifestLoggingBackend"/>) and Play Games'
    /// start-up provider (<see cref="ManifestPlayGamesStart"/>), and it must say
    /// that the application is a game (<see cref="ManifestGameCategory"/>), open Unity's activity upright
    /// (<see cref="ManifestOrientation"/>) and hide the system bars with Android's default behaviour
    /// (<see cref="ManifestSystemBars"/>). A manifest that cannot be read or written, one of another category, one
    /// that opens another way or one that hides the bars another way stops the build: a package without them would
    /// fail on the phone, turn on a tablet as Android wants rather than as the game does, show a phone the game
    /// sideways, or spend the first back gesture on the bars. The Unity library's build.gradle is then given the Play
    /// Games library (<see cref="PlayGamesDependency"/>).
    /// </summary>
    public sealed class AndroidManifestStep : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        /// <param name="path">The Unity library's Gradle project, as Unity hands it over.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            string written = ManifestPlayGamesStart.Withdraw(ManifestLoggingBackend.Withdraw(ManifestPermissions.Apply(File.ReadAllText(manifest), AndroidBuild.Permissions)));
            ManifestGameCategory.Require(written);
            ManifestOrientation.Require(written);
            ManifestSystemBars.Require(written);
            File.WriteAllText(manifest, written);
            Debug.Log($"Android build: the manifest says {string.Join<PermissionLine>(", ", AndroidBuild.Permissions)}, of a game that opens {ManifestOrientation.Upright}, "
                + $"hides the system bars as \"{ManifestSystemBars.Default}\" and withdraws {ManifestLoggingBackend.CctBackend} and {ManifestPlayGamesStart.InitProvider}.");
            string gradle = Path.Combine(path, "build.gradle");
            File.WriteAllText(gradle, PlayGamesDependency.Into(File.ReadAllText(gradle)));
            Debug.Log($"Android build: the Unity library depends on {PlayGamesDependency.Library} for the Play Games plugin.");
        }
    }
}
