using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Every PNG in Assets/Art/Pieces is imported as one sprite, whatever its .meta file says and also when
    /// tools/unity/export-sprites.js adds a new one. The sprites are not drawn from these textures: the piece atlas
    /// (PieceAtlasBuilder) packs them into one, and that one is compressed and given its smaller copies. So each
    /// drawing is kept here exactly as it was exported, for the atlas to pack from.
    /// </summary>
    public sealed class PieceSpriteImport : AssetPostprocessor
    {
        public const string PiecesFolder = "Assets/Art/Pieces";

        /// <summary>The drawing's side in pixels: one drawing is one world unit, which the board scales to a cell.</summary>
        private const int DrawingSize = 512;

        /// <summary>Raise after changing the settings below: Unity then imports the drawings again.</summary>
        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PiecesFolder + "/"))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = DrawingSize;
            importer.alphaIsTransparency = true; // colours the invisible pixels like their neighbours: no dark rim
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = DrawingSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ClearPlatformTextureSettings("Android");
            UseFullRectangle(importer);
        }

        /// <summary>
        /// The sprite is its whole square, not an outline cut around the drawing: every drawing then has the same
        /// simple shape, with its middle in the middle of the cell.
        /// </summary>
        private static void UseFullRectangle(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
