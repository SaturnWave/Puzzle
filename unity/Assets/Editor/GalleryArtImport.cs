using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// How the pictures of Assets/Art/Gallery are imported, whatever their .meta files say and also when
    /// tools/unity/export-gallery-art.js writes them again. The sprites are sized so that one unit is one point of
    /// the design, as in their sources (art/unity/*.svg), and are packed into the atlas with the pieces. The damask
    /// is no sprite: it is a texture that repeats over the wall.
    /// </summary>
    public sealed class GalleryArtImport : AssetPostprocessor
    {
        public const string GalleryFolder = "Assets/Art/Gallery";
        public const string DamaskPath = GalleryFolder + "/damask.png";

        /// <summary>
        /// The sprites by file name. The numbers are in the source's own points: how wide its picture is, where its
        /// pivot lies from the picture's top left corner, and how far in from each side (and, for a sprite that is
        /// also drawn taller or lower, from its top and from its foot) the part begins that stretches or repeats when
        /// the sprite is drawn at another size (none for a sprite that never is).
        /// </summary>
        private static readonly IReadOnlyDictionary<string, Picture> Sprites = new Dictionary<string, Picture>
        {
            ["plaque"] = new Picture(pointsWide: 128f, pivot: new Vector2(64f, 46f), stretchesFrom: 36f, stretchesFromTop: 26f, stretchesFromFoot: 52f),
            ["medallion"] = new Picture(pointsWide: 132f, pivot: new Vector2(66f, 54f)),
            ["shelf"] = new Picture(pointsWide: 128f, pivot: new Vector2(64f, 35f), stretchesFrom: 38f),
            ["sconce"] = new Picture(pointsWide: 148f, pivot: new Vector2(74f, 134f)),
            ["goal-done"] = new Picture(pointsWide: 20f, pivot: new Vector2(10f, 10f), largestSide: 128),
            ["card"] = new Picture(pointsWide: 160f, pivot: new Vector2(80f, 80f), stretchesFrom: 54f, stretchesFromTop: 54f, stretchesFromFoot: 54f),
            ["nameplate"] = new Picture(pointsWide: 304f, pivot: new Vector2(152f, 32f)),
            ["button"] = new Picture(pointsWide: 176f, pivot: new Vector2(88f, 46f), stretchesFrom: 56f),
            ["alcove"] = new Picture(pointsWide: 316f, pivot: new Vector2(158f, 106f)),
            ["sparkle"] = new Picture(pointsWide: 24f, pivot: new Vector2(12f, 12f), largestSide: 128),
            ["count-badge"] = new Picture(pointsWide: 48f, pivot: new Vector2(24f, 19f), stretchesFrom: 21f, largestSide: 256),
            ["picture-frame"] = new Picture(pointsWide: 177.5f, pivot: new Vector2(88.75f, 88.75f), stretchesFrom: 65f, stretchesFromTop: 65f, stretchesFromFoot: 65f),
            ["picture-light"] = new Picture(pointsWide: 240f, pivot: new Vector2(120f, 23f)),
            ["caption-plate"] = new Picture(pointsWide: 128f, pivot: new Vector2(64f, 23.5f), stretchesFrom: 24f),
            ["pill"] = new Picture(pointsWide: 128f, pivot: new Vector2(64f, 17f), stretchesFrom: 26f),
            ["room-badge"] = new Picture(pointsWide: 48f, pivot: new Vector2(24f, 24f), largestSide: 256),
            ["tab"] = new Picture(pointsWide: 128f, pivot: new Vector2(64f, 66f)),
            ["icon-museum"] = new Picture(pointsWide: 100f, pivot: new Vector2(50f, 50f), largestSide: 256),
            ["icon-map"] = new Picture(pointsWide: 100f, pivot: new Vector2(50f, 50f), largestSide: 256),
            ["icon-lock"] = new Picture(pointsWide: 100f, pivot: new Vector2(50f, 50f), largestSide: 256),
            ["icon-pin"] = new Picture(pointsWide: 32f, pivot: new Vector2(16f, 35f), largestSide: 512),
            ["workbench"] = new Picture(pointsWide: 234f, pivot: new Vector2(117f, 188f)),
            ["button-teal"] = new Picture(pointsWide: 176f, pivot: new Vector2(88f, 46f), stretchesFrom: 56f),
            ["button-blue"] = new Picture(pointsWide: 176f, pivot: new Vector2(88f, 46f), stretchesFrom: 56f),
            ["button-back"] = new Picture(pointsWide: 64f, pivot: new Vector2(32f, 30f), largestSide: 512),
            ["icon-collection"] = new Picture(pointsWide: 100f, pivot: new Vector2(50f, 50f), largestSide: 256),
            ["icon-piece"] = new Picture(pointsWide: 100f, pivot: new Vector2(50f, 50f), largestSide: 256),
            ["tray"] = new Picture(pointsWide: 160f, pivot: new Vector2(80f, 80f), stretchesFrom: 48f, stretchesFromTop: 56f, stretchesFromFoot: 56f),
            ["pip-bar"] = new Picture(pointsWide: 64f, pivot: new Vector2(32f, 15f), stretchesFrom: 20f, largestSide: 512),
            ["pip-on"] = new Picture(pointsWide: 20f, pivot: new Vector2(10f, 7.5f), largestSide: 128),
            ["pip-off"] = new Picture(pointsWide: 20f, pivot: new Vector2(10f, 7.5f), largestSide: 128),
            ["pip-next"] = new Picture(pointsWide: 20f, pivot: new Vector2(10f, 7.5f), largestSide: 128),
            ["badge-red"] = new Picture(pointsWide: 32f, pivot: new Vector2(16f, 15f), stretchesFrom: 15f, largestSide: 256),
        };

        /// <summary>Raise after changing the settings below: Unity then imports the pictures again.</summary>
        public override uint GetVersion() => 4;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(GalleryFolder + "/"))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            if (assetPath == DamaskPath)
            {
                ImportAsRepeatingPattern(importer);
            }
            else if (Sprites.TryGetValue(Path.GetFileNameWithoutExtension(assetPath), out Picture picture))
            {
                ImportAsSprite(importer, picture);
            }
            else
            {
                Debug.LogError($"{assetPath} is not a picture the game lists: add it to GalleryArtImport and to the art catalogue's builder.");
            }
        }

        private static void ImportAsSprite(TextureImporter importer, Picture picture)
        {
            importer.GetSourceTextureWidthAndHeight(out int pixelsWide, out int pixelsHigh);
            float pixelsPerPoint = pixelsWide / picture.PointsWide;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; // colours the invisible pixels like their neighbours: no dark rim
            importer.mipmapEnabled = false;      // the atlas makes the smaller copies
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = picture.LargestSide;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ClearPlatformTextureSettings("Android");

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spritePixelsPerUnit = pixelsPerPoint;
            settings.spriteMeshType = SpriteMeshType.FullRect; // a stretched sprite needs its whole rectangle
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(picture.Pivot.x / picture.PointsWide, 1f - picture.Pivot.y * pixelsPerPoint / pixelsHigh);
            float sideBorder = picture.StretchesFrom * pixelsPerPoint;
            settings.spriteBorder = new Vector4(sideBorder, picture.StretchesFromFoot * pixelsPerPoint, sideBorder, picture.StretchesFromTop * pixelsPerPoint);
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }

        private static void ImportAsRepeatingPattern(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // The pattern is faint (at most a twelfth of white): a phone gets it in the finest of its compressed formats.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 1024,
                format = TextureImporterFormat.ASTC_4x4,
                compressionQuality = 100,
            });
        }

        /// <summary>A sprite's measures, in the points of its source.</summary>
        private readonly struct Picture
        {
            public Picture(float pointsWide, Vector2 pivot, float stretchesFrom = 0f, float stretchesFromTop = 0f, float stretchesFromFoot = 0f, int largestSide = 2048)
            {
                PointsWide = pointsWide;
                Pivot = pivot;
                StretchesFrom = stretchesFrom;
                StretchesFromTop = stretchesFromTop;
                StretchesFromFoot = stretchesFromFoot;
                LargestSide = largestSide;
            }

            public float PointsWide { get; }

            /// <summary>From the picture's top left corner, y downwards, as the source is written.</summary>
            public Vector2 Pivot { get; }

            /// <summary>How far in from the left and from the right side the stretching part begins; 0 for a sprite never drawn wider.</summary>
            public float StretchesFrom { get; }

            /// <summary>How far in from the top the stretching part begins; 0 for a sprite never drawn at another height.</summary>
            public float StretchesFromTop { get; }

            /// <summary>How far in from the foot it ends: a picture's shadow lies under it, so this is not always what the top has.</summary>
            public float StretchesFromFoot { get; }

            /// <summary>The picture is stored no larger than this, in pixels: a small mark needs no large picture.</summary>
            public int LargestSide { get; }
        }
    }
}
