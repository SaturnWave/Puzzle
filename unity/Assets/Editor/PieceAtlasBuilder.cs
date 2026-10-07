using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Packs every sprite of Assets/Art/Pieces and Assets/Art/Gallery into one texture (Assets/Art/Pieces.spriteatlasv2).
    /// The game then draws its pieces, plaques and shelf from one texture in one go instead of changing texture from
    /// sprite to sprite, and a phone loads one compressed texture instead of nearly fifty. It also rules out a fault
    /// seen while every drawing had a texture of its own: the first picture a freshly started Editor rendered showed
    /// all sprites with one texture.
    /// </summary>
    public static class PieceAtlasBuilder
    {
        private const string AtlasPath = "Assets/Art/Pieces.spriteatlasv2";

        /// <summary>
        /// Forty-one pieces of 512 px and the gallery's eleven sprites, with their margins, need about five sixths of a
        /// 4096 px square and are packed into one. A further large drawing may not fit: the atlas then gets a second
        /// texture, which works but costs the one-texture draw.
        /// </summary>
        private const int AtlasSize = 4096;

        /// <summary>
        /// Empty pixels around each drawing. The smaller copies of the atlas (mipmaps) mix neighbouring pixels; with
        /// 8 px the three copies a phone uses for a piece stay free of the drawing next door.
        /// </summary>
        private const int MarginPixels = 8;

        public static void Ensure()
        {
            // The atlas is used wherever sprites are drawn, also in the Editor's screenshot command, not only in builds.
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            // Written anew each time, so the two folders named here are what the atlas packs; its settings below are kept beside it.
            var atlas = new SpriteAtlasAsset();
            atlas.Add(new Object[]
            {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(PieceSpriteImport.PiecesFolder),
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(GalleryArtImport.GalleryFolder),
            });
            bool isNew = AssetImporter.GetAtPath(AtlasPath) == null;
            SpriteAtlasAsset.Save(atlas, AtlasPath);
            if (isNew)
            {
                // Only a first import gives the file its importer; an atlas that has one is packed once, by the reimport below.
                AssetDatabase.ImportAsset(AtlasPath);
            }

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                padding = MarginPixels,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                generateMipMaps = true,
                filterMode = FilterMode.Trilinear,
                sRGB = true,
                readable = false,
            };
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform",
                maxTextureSize = AtlasSize,
                textureCompression = TextureImporterCompression.Uncompressed,
            });
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = AtlasSize,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 100,
            });
            importer.SaveAndReimport();
        }
    }
}
