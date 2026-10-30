using System.IO;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Says in which format each painting is imported for the active build target, and fails when one is not
    /// compressed or has no mipmaps: five uncompressed paintings once made the Android package 50 MB larger, and
    /// without mipmaps the small pictures on the collection wall come out grainy.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod Puzzle.Client.EditorTools.PaintingFormatCheck.Run
    /// </summary>
    public static class PaintingFormatCheck
    {
        public static void Run()
        {
            bool allCompressed = true;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PaintingImport.PaintingsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D picture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                bool sound = GraphicsFormatIsCompressed(picture) && picture.mipmapCount > 1;
                allCompressed &= sound;
                Debug.Log($"Painting format: {Path.GetFileName(path)} {picture.width}x{picture.height} {picture.format} mips {picture.mipmapCount} for {EditorUserBuildSettings.activeBuildTarget}{(sound ? string.Empty : " (NOT compressed with mipmaps)")}");
            }

            if (!allCompressed)
            {
                EditorApplication.Exit(1);
            }
        }

        private static bool GraphicsFormatIsCompressed(Texture2D picture) =>
            UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsCompressedFormat(picture.format);
    }
}
