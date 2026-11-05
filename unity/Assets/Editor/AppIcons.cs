#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Gives the game its icon, the owner's choice of 2026-10-23 (art/icon/README.md), in the player settings and
    /// reads it back: the default icon, and every size of every icon kind Unity asks for on Android (the adaptive
    /// icon's background and foreground layers, and the legacy and round icons when it asks for them) and on iOS (the
    /// App Store's 1024 icon among them). The project's setup does it too (<see cref="ProjectSetup"/>), so a package is
    /// never built without it. Run it again after the pictures in Assets/Art/Icon change (<see cref="AppIconImport"/>).
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.AppIcons.Run
    /// AppIcons.Check in place of Run only reads the saved player settings back and sets nothing. Either ends the
    /// Editor with exit code 1 when an icon is missing.
    /// </summary>
    /// <remarks>
    /// The kinds are asked of Unity rather than named here, so that iOS needs no type of the iOS module: on an Editor
    /// without that module Unity asks for no iOS icon, and the tool sets none, which keeps this Windows Editor green.
    /// </remarks>
    public static class AppIcons
    {
        /// <summary>The App Store's icon, the one iOS size that must be there.</summary>
        private const int AppStoreIconSize = 1024;

        private static readonly NamedBuildTarget[] Platforms = { NamedBuildTarget.Android, NamedBuildTarget.iOS };

        public static void Run()
        {
            try
            {
                Apply();
                ReadBack();
            }
            catch (InvalidOperationException failure)
            {
                Fail(failure);
            }
        }

        public static void Check()
        {
            try
            {
                ReadBack();
            }
            catch (InvalidOperationException failure)
            {
                Fail(failure);
            }
        }

        /// <summary>Sets the icon at every size of every kind Unity asks for, and saves the player settings.</summary>
        /// <exception cref="InvalidOperationException">A picture is missing, or an icon kind has a number of layers this tool has no pictures for.</exception>
        public static void Apply()
        {
            IconPictures pictures = IconPictures.Load();
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { pictures.Whole }, IconKind.Any);
            foreach (NamedBuildTarget platform in Platforms)
            {
                foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(platform))
                {
                    SetEverySize(platform, kind, pictures.LayersOf(kind));
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <exception cref="InvalidOperationException">The kind's icons take another number of layers than <paramref name="layers"/>.</exception>
        private static void SetEverySize(NamedBuildTarget platform, PlatformIconKind kind, Texture2D[] layers)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(platform, kind);
            foreach (PlatformIcon icon in icons)
            {
                if (icon.maxLayerCount != layers.Length)
                {
                    throw new InvalidOperationException($"{NameOf(platform)} {kind} icons take {icon.maxLayerCount} layers and this tool has {layers.Length}; decide which picture goes on each.");
                }

                icon.SetTextures(layers);
            }

            PlayerSettings.SetPlatformIcons(platform, kind, icons);
        }

        /// <summary>Reads the icon back from the player settings and says, kind by kind, what every size carries.</summary>
        /// <exception cref="InvalidOperationException">A picture is missing or not imported as an icon needs, or a size lacks its pictures; the message names every one.</exception>
        private static void ReadBack()
        {
            IconPictures pictures = IconPictures.Load();
            foreach (Texture2D picture in pictures.All)
            {
                Debug.Log($"App icons: picture {picture.name} {picture.width}x{picture.height} {picture.format}, {picture.mipmapCount} mip level(s).");
            }

            var problems = new List<string>(pictures.ImportProblems());
            Texture2D[] defaultIcons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (defaultIcons.Length == 0 || defaultIcons[0] != pictures.Whole)
            {
                problems.Add($"The default icon is not {pictures.Whole.name}.");
            }

            List<PlatformIcon> android = ReadBack(NamedBuildTarget.Android, pictures, problems);
            List<PlatformIcon> ios = ReadBack(NamedBuildTarget.iOS, pictures, problems);
            if (android.Count == 0)
            {
                problems.Add("Unity asks for no Android icon at all.");
            }

            if (ios.Count > 0 && !ios.Any(icon => icon.width == AppStoreIconSize))
            {
                problems.Add($"iOS has no {AppStoreIconSize} App Store icon.");
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", problems));
            }

            Debug.Log($"App icons: check passed: the default icon, {android.Count} Android icons and {ios.Count} iOS icons carry the owner's icon.");
        }

        /// <summary>Says what every size of every kind of the platform carries, and adds to <paramref name="problems"/> each size that lacks its pictures.</summary>
        /// <returns>The platform's icons, all kinds and sizes.</returns>
        private static List<PlatformIcon> ReadBack(NamedBuildTarget platform, IconPictures pictures, List<string> problems)
        {
            PlatformIconKind[] kinds = PlayerSettings.GetSupportedIconKinds(platform);
            if (kinds.Length == 0)
            {
                Debug.Log($"App icons: {NameOf(platform)}: Unity asks for no icon kind; this Editor has no {NameOf(platform)} module.");
            }

            var all = new List<PlatformIcon>();
            foreach (PlatformIconKind kind in kinds)
            {
                Texture2D[] expected = pictures.LayersOf(kind);
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(platform, kind);
                foreach (PlatformIcon icon in icons.Where(icon => !icon.GetTextures().SequenceEqual(expected)))
                {
                    problems.Add($"{NameOf(platform)} {kind} {icon.width}x{icon.height} does not carry {NamesOf(expected)}.");
                }

                string sizes = icons.Length == 0 ? "no size asked for" : string.Join(", ", icons.Select(icon => icon.width));
                Debug.Log($"App icons: {NameOf(platform)} {kind}: {sizes}: {NamesOf(expected)}.");
                all.AddRange(icons);
            }

            return all;
        }

        /// <summary>The platform's name in the log: Unity names iOS's target "iPhone".</summary>
        private static string NameOf(NamedBuildTarget platform) => platform.Equals(NamedBuildTarget.iOS) ? "iOS" : platform.TargetName;

        private static string NamesOf(IEnumerable<Texture2D> layers) => string.Join(" + ", layers.Select(layer => layer.name));

        private static void Fail(InvalidOperationException failure)
        {
            Debug.LogError($"App icons: FAILED: {failure.Message}");
            EditorApplication.Exit(1);
        }

        /// <summary>The icon's three pictures, as imported from Assets/Art/Icon.</summary>
        private sealed class IconPictures
        {
            private IconPictures(Texture2D whole, Texture2D background, Texture2D foreground)
            {
                Whole = whole;
                Background = background;
                Foreground = foreground;
            }

            public Texture2D Whole { get; }

            public Texture2D Background { get; }

            public Texture2D Foreground { get; }

            /// <exception cref="InvalidOperationException">A picture is missing from Assets/Art/Icon.</exception>
            public static IconPictures Load() =>
                new(LoadPicture(AppIconImport.WholeIconPath), LoadPicture(AppIconImport.BackgroundPath), LoadPicture(AppIconImport.ForegroundPath));

            /// <summary>
            /// What every size of an icon kind carries, in Unity's layer order: the adaptive icon's background, then
            /// its foreground; the whole icon for every one-layer kind.
            /// </summary>
            public Texture2D[] LayersOf(PlatformIconKind kind) =>
                kind.Equals(AndroidPlatformIconKind.Adaptive) ? new[] { Background, Foreground } : new[] { Whole };

            public IReadOnlyList<Texture2D> All => new[] { Whole, Background, Foreground };

            /// <summary>Names each picture that is compressed, has smaller copies, is not full size, or has an alpha channel it should not have or lacks one it needs.</summary>
            public IEnumerable<string> ImportProblems()
            {
                foreach (Texture2D picture in All)
                {
                    bool hasAlpha = GraphicsFormatUtility.HasAlphaChannel(picture.graphicsFormat);
                    bool needsAlpha = picture == Foreground;
                    if (GraphicsFormatUtility.IsCompressedFormat(picture.format) || picture.mipmapCount != 1)
                    {
                        yield return $"{picture.name} is compressed or has smaller copies.";
                    }

                    if (picture.width != AppIconImport.PictureSize || picture.height != AppIconImport.PictureSize)
                    {
                        yield return $"{picture.name} is not {AppIconImport.PictureSize}x{AppIconImport.PictureSize}.";
                    }

                    if (hasAlpha != needsAlpha)
                    {
                        yield return $"{picture.name} {(hasAlpha ? "has an alpha channel" : "has lost its transparency")}.";
                    }
                }
            }

            /// <exception cref="InvalidOperationException">The picture is missing.</exception>
            private static Texture2D LoadPicture(string path) =>
                AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                ?? throw new InvalidOperationException($"{path} is missing; copy it from art/icon (art/icon/README.md).");
        }
    }
}
