using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// How the museum's paintings (Assets/Resources/Paintings) are imported, whatever their .meta files say and also
    /// when a picture is replaced by a larger copy of the same name. A painting is no sprite: it is a texture that
    /// jigsaw pieces and frames show parts of, loaded by name when a screen needs it.
    /// </summary>
    public sealed class PaintingImport : AssetPostprocessor
    {
        public const string PaintingsFolder = "Assets/Resources/Paintings";

        /// <summary>The museums' open-access pictures are about 1900 pixels on their longer side; nothing larger is kept.</summary>
        private const int LargestSide = 2048;

        /// <summary>Raise after changing the settings below: Unity then imports the pictures again.</summary>
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PaintingsFolder + "/"))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None; // a painting keeps its proportions: the jigsaw is cut to them
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true; // the same picture fills a screen and sits as a thumbnail on the collection wall
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = LargestSide;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            // On a phone the paintings are the largest pictures by far; six-by-six blocks keep brushwork at under half a byte a pixel.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = LargestSide,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100,
            });
        }
    }
}
