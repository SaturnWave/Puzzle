using System;
using System.IO;
using UnityEditor;
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
    /// package with a catalogue older than those lists would stop at the first drawing added since.
    /// </summary>
    public static class AndroidBuild
    {
        private const string PackagePath = "Build/Android/puzzle.apk";

        /// <summary>Placeholders: the game's name is decided last, and the package id follows it.</summary>
        private const string CompanyName = "SaturnWave";
        private const string ProductName = "Puzzle";
        private const string PackageId = "com.saturnwave.puzzle";

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

        /// <summary>A 64-bit ARM package in portrait: what current phones run and what the stores ask for.</summary>
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUserBuildSettings.buildAppBundle = false;
        }
    }
}
