using System;
using System.IO;
using System.Linq;
using Puzzle.Client.Canvases;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Writes the list of the paintings' true sizes (Assets/Resources/Paintings/sizes.json) from the picture files.
    /// The imported textures are stretched to powers of two, so the game reads a painting's proportions from this
    /// list instead (see <see cref="PaintingSizes"/>). Part of ProjectSetup.Run; never edit the list by hand.
    /// </summary>
    public static class PaintingSizesBuilder
    {
        private const string ListPath = PaintingImport.PaintingsFolder + "/sizes.json";

        public static void Build()
        {
            PaintingSizeFile[] pictures = AssetDatabase.FindAssets("t:Texture2D", new[] { PaintingImport.PaintingsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(SizeOf)
                .ToArray();
            string list = JsonUtility.ToJson(new PaintingSizesFile { pictures = pictures }, prettyPrint: true) + "\n";
            if (File.Exists(ListPath) && File.ReadAllText(ListPath) == list)
            {
                return;
            }

            File.WriteAllText(ListPath, list);
            AssetDatabase.ImportAsset(ListPath);
        }

        private static PaintingSizeFile SizeOf(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            return new PaintingSizeFile
            {
                image = assetPath.Substring(PaintingImport.PaintingsFolder.Length + 1),
                width = width,
                height = height,
            };
        }
    }
}
