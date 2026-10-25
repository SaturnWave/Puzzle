using System.IO;
using System.Linq;
using Puzzle.Client.Avatar;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// How the painter's pictures (Assets/Art/Avatar/Resources/Painter, copies of art/avatar) are imported, whatever
    /// their .meta files say. Each is a sprite, loaded by name when a screen shows him and let go of when it is taken
    /// down (Scripts/Avatar/PainterGallery.cs). A standing picture is measured in metres (1100 of its pixels to a metre,
    /// <see cref="PainterPictures.PixelsPerMetre"/>) and turns about his feet, so a screen places him by where he stands
    /// and draws him at so many points a metre; a bust is 100 units wide and turns about its middle, as the gallery's
    /// marks do.
    /// </summary>
    /// <remarks>
    /// The renders are far larger than any screen shows them: a standing figure is about 450 pixels high on the
    /// 1080x2424 phone and a bust about 130 across. A standing picture is kept 1024 pixels on its longer side (its
    /// figure some 960 high, twice what the phone shows), a bust 256. It has no mipmaps: the figure is a UI picture
    /// shown at one size per screen, and mipmaps would cost a picture whose sides are no power of two its compression
    /// (PaintingImport). On a phone a standing picture is compressed in blocks of six by six (as the paintings are), a
    /// bust, whose face is seen at a hundred-odd pixels, in the finer four by four.
    /// </remarks>
    public sealed class AvatarArtImport : AssetPostprocessor
    {
        public const string AvatarFolder = "Assets/Art/Avatar/Resources/" + PainterPictures.ResourceFolder;

        private const int StandingLargestSide = 1024;
        private const int BustLargestSide = 256;

        /// <summary>Raise after changing the settings below: Unity then imports the pictures again.</summary>
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AvatarFolder + "/"))
            {
                return;
            }

            string name = Path.GetFileNameWithoutExtension(assetPath);
            PainterPicture[] named = PainterPictures.All.Where(picture => PainterPictures.FileNameOf(picture) == name).ToArray();
            if (named.Length != 1)
            {
                Debug.LogError($"{assetPath} is not a picture of the painter the game lists: add it to PainterPicture and art/avatar/licences.json.");
                return;
            }

            ImportAsSprite((TextureImporter)assetImporter, named[0]);
        }

        private static void ImportAsSprite(TextureImporter importer, PainterPicture picture)
        {
            bool isStanding = PainterPictures.IsStanding(picture);
            importer.GetSourceTextureWidthAndHeight(out int pixelsWide, out _);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; // colours the invisible pixels like their neighbours: no dark rim round the hair
            importer.mipmapEnabled = false;      // a UI picture, shown at one size on a screen
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = isStanding ? StandingLargestSide : BustLargestSide;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = isStanding ? StandingLargestSide : BustLargestSide,
                format = isStanding ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4,
                compressionQuality = 100,
            });

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // his outline is in the picture's alpha; a tight mesh of a render this size costs more than it saves
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            if (isStanding)
            {
                (float fromLeft, float fromFoot) = PainterPictures.FeetOf(picture);
                settings.spritePixelsPerUnit = PainterPictures.PixelsPerMetre;
                settings.spritePivot = new Vector2(fromLeft, fromFoot);
            }
            else
            {
                settings.spritePixelsPerUnit = pixelsWide / PainterPictures.BustBox;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
            }

            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
