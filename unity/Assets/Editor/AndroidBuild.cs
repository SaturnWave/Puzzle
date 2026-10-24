using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Builds the Android package from the command line.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.AndroidBuild.Run
    /// The package lands in unity/Build/Android/puzzle.apk, signed with Unity's debug key: good for installing on a
    /// test phone, not for a store. What the Editor derives (the atlas, the art catalogue, the typefaces, the scene)
    /// is brought up to date first: the game reads the catalogue by the order of its lists of drawings, and a
    /// package with a catalogue older than those lists would stop at the first drawing added since. The manifest is
    /// given the build's permissions on the way (<see cref="AndroidManifestStep"/>).
    /// </summary>
    public static class AndroidBuild
    {
        private const string PackagePath = "Build/Android/puzzle.apk";

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

            Debug.Log($"Android build: succeeded, {summary.totalSize / (1024 * 1024)} MB at {summary.outputPath} in {summary.totalTime.TotalMinutes:F1} min.");
        }

        /// <summary>
        /// What the package's manifest says of Android's permissions beyond what Unity writes, one line each, in the
        /// order they are written (<see cref="AndroidManifestStep"/>). The internet permission Unity asks for is taken
        /// out (the owner's decision, 2026-10-16): the game calls no network, and without the permission the privacy
        /// card's "sends nothing" holds by construction (Flow/PrivacyWords.cs). Ads, purchases or a reporting service
        /// bring it back, together with a new privacy text. The application's backup flag is not this step's; it stays
        /// on by the same decision.
        /// </summary>
        internal static readonly PermissionLine[] Permissions =
        {
            PermissionLine.Ask("android.permission.VIBRATE"), // the short ticks of play (Scripts/Sound/Haptics.cs)
            PermissionLine.Withdraw("android.permission.INTERNET"), // nothing leaves the phone
        };

        /// <summary>
        /// A 64-bit ARM package for Android 16 (API level 36, what Google Play asks for since 31 August 2026), named so
        /// that a later Unity cannot move the target on its own: from API level 37 on Android has no way out of its rules
        /// for large screens but the game category (<see cref="ManifestGameCategory"/>). The package opens upright, so a
        /// phone never shows it sideways; every way of standing is allowed for the game's own rule, which turns a tablet
        /// or an open foldable with the device once the game runs (Scripts/Engine/DeviceScreen.cs). The project's
        /// committed settings say the same, the target too, so a build leaves them as they were; the manifest step stops
        /// a package that would open another way (<see cref="ManifestOrientation"/>).
        /// </summary>
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = GameIdentity.CompanyName;
            PlayerSettings.productName = GameIdentity.ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, GameIdentity.ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;
        }
    }

    /// <summary>
    /// The build's manifest step: once Unity has made the Gradle project of an Android build (this tool's, or any
    /// other), the Unity library's manifest is given the build's permissions (<see cref="AndroidBuild.Permissions"/>),
    /// must say that the application is a game (<see cref="ManifestGameCategory"/>), and must open Unity's activity
    /// upright (<see cref="ManifestOrientation"/>). A manifest that cannot be read or written, one of another category,
    /// or one that opens another way stops the build: a package without them would fail on the phone, turn on a tablet
    /// as Android wants rather than as the game does, or show a phone the game sideways.
    /// </summary>
    public sealed class AndroidManifestStep : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        /// <param name="path">The Unity library's Gradle project, as Unity hands it over.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            string written = ManifestPermissions.Apply(File.ReadAllText(manifest), AndroidBuild.Permissions);
            ManifestGameCategory.Require(written);
            ManifestOrientation.Require(written);
            File.WriteAllText(manifest, written);
            Debug.Log($"Android build: the manifest says {string.Join<PermissionLine>(", ", AndroidBuild.Permissions)}, of a game that opens {ManifestOrientation.Upright}.");
        }
    }
}
