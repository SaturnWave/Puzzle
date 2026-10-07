using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Says from the command line whether this Editor can build for Android and which SDK, NDK and JDK it would use.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.AndroidToolchainCheck.Run
    /// </summary>
    public static class AndroidToolchainCheck
    {
        public static void Run()
        {
            bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log($"Android toolchain: build target supported = {supported}");
            Debug.Log($"Android toolchain: SDK = {AndroidExternalToolsSettings.sdkRootPath}");
            Debug.Log($"Android toolchain: NDK = {AndroidExternalToolsSettings.ndkRootPath}");
            Debug.Log($"Android toolchain: JDK = {AndroidExternalToolsSettings.jdkRootPath}");
            if (!supported)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
