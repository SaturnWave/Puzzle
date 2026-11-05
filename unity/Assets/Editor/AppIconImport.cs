using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// How the app icon's three pictures in Assets/Art/Icon (copies of art/icon's) are imported, whatever their .meta
    /// files say and also when they are copied again. Unity draws every launcher and store size from them when it
    /// builds a package, so each is kept exactly as drawn: full size, uncompressed (no block artefacts on the gilt
    /// edge), and without the smaller copies an icon never shows. The adaptive icon's foreground keeps its
    /// transparency, and its invisible pixels take their neighbours' colour, so scaling it down leaves no dark rim
    /// around the palette. The whole icon and the background are opaque and are kept without an alpha channel, as the
    /// App Store asks of its 1024 icon. <see cref="AppIcons"/> sets them in the player settings.
    /// <para>
    /// Beside them lies the entry page's logo, the same art at the whole icon's size on transparency. It is no icon:
    /// the game draws it, so it keeps its transparency as the foreground does and is compressed in high quality (a
    /// quarter of the memory of its pixels) instead of kept exactly.
    /// </para>
    /// </summary>
    public sealed class AppIconImport : AssetPostprocessor
    {
        public const string IconFolder = "Assets/Art/Icon";

        /// <summary>The whole icon: the default icon, iOS, and Android's one-layer icons.</summary>
        public const string WholeIconPath = IconFolder + "/app-icon-1024.png";

        /// <summary>The Android adaptive icon's bottom layer.</summary>
        public const string BackgroundPath = IconFolder + "/adaptive-background-1024.png";

        /// <summary>The Android adaptive icon's top layer: the palette and the brush, inside the middle 66% the launchers keep.</summary>
        public const string ForegroundPath = IconFolder + "/adaptive-foreground-1024.png";

        /// <summary>The entry page's logo: the palette and the brush at the whole icon's size, see-through around them.</summary>
        public const string EntryLogoPath = IconFolder + "/entry-logo-1024.png";

        /// <summary>Each picture's side in pixels: the largest size asked of it, the App Store's.</summary>
        public const int PictureSize = 1024;

        /// <summary>Raise after changing the settings below: Unity then imports the pictures again.</summary>
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconFolder + "/"))
            {
                return;
            }

            bool isTheLogo = assetPath == EntryLogoPath;
            bool isSeeThrough = assetPath == ForegroundPath || isTheLogo;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = isSeeThrough ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = isSeeThrough;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = PictureSize;
            importer.textureCompression = isTheLogo ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            importer.ClearPlatformTextureSettings("Android");
            importer.ClearPlatformTextureSettings("iPhone");
        }
    }
}
