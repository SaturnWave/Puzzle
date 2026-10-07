#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Puzzle.Client.Art;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Makes the game's typeface ready to draw: from the font file Roboto Bold (it ships with the Unity Editor; see
    /// tools/unity/import-text-resources.js) a TextMesh Pro font asset with every letter the game can write, and one
    /// material per lettering style. The letters are stored as distances to their outline, not as pixels, so one
    /// picture of them is sharp at every size, and a style can draw the strokes heavier than the font file has them.
    /// The asset holds a fixed set of letters and no font file: a build carries the picture alone.
    /// </summary>
    public static class LetteringBuilder
    {
        private const string FontFilePath = "Assets/Art/Fonts/Roboto-Bold.ttf";
        private const string TypefacePath = "Assets/Art/Fonts/Roboto-Bold Lettering.asset";
        private const string TextShaderName = "TextMeshPro/Mobile/Distance Field";

        /// <summary>
        /// Letters are sampled 72 px high with 9 px of room around each: the room is how far a style may thicken a
        /// stroke or throw a shadow, and an eighth of the letter's size is enough for every style.
        /// </summary>
        private const int SamplingSize = 72;
        private const int RoomAroundALetter = 9;
        private const int PictureSide = 1024;

        /// <summary>What Turkish needs beyond the plain Latin letters, and the marks the game's sentences use.</summary>
        private const string LettersBeyondAscii = "ÇĞİÖŞÜçğıöşüÂÎÛâîû·…’“”–×";

        /// <summary>The dark edge under lettering on wood: the web page's "0 1px 0" text shadow at a 13 px type size.</summary>
        private static readonly Shade EdgeOnWood = new Shade(new Color32(40, 18, 6, 166), drop: 0.6f, wider: 0f, softness: 0.08f);

        /// <summary>
        /// A button's label, as the web page draws it: white in a dark green outline, with the outline's own shade
        /// under it, which reads as the letters standing a little above the button.
        /// </summary>
        private static readonly Color ButtonGreen = new Color32(0x14, 0x5A, 0x1B, 0xFF);
        private static readonly Outline OutlineOnButton = new Outline(ButtonGreen, width: 0.5f);
        private static readonly Shade ShadeOnButton = new Shade(ButtonGreen, drop: 0.55f, wider: 0.62f, softness: 0f);

        /// <summary>The same on the gallery's teal: white in a dark teal outline.</summary>
        private static readonly Color DarkTeal = new Color32(0x0A, 0x36, 0x40, 0xFF);
        private static readonly Outline OutlineOnTeal = new Outline(DarkTeal, width: 0.5f);
        private static readonly Shade ShadeOnTeal = new Shade(DarkTeal, drop: 0.55f, wider: 0.62f, softness: 0f);

        /// <summary>The same on the blue of the buttons that lead to a jigsaw: white in a dark blue outline.</summary>
        private static readonly Color DarkBlue = new Color32(0x0F, 0x26, 0x70, 0xFF);
        private static readonly Outline OutlineOnBlue = new Outline(DarkBlue, width: 0.5f);
        private static readonly Shade ShadeOnBlue = new Shade(DarkBlue, drop: 0.55f, wider: 0.62f, softness: 0f);

        /// <summary>How each style changes the drawing of the typeface: how much heavier the strokes are, what is drawn around them and under them.</summary>
        private static readonly IReadOnlyDictionary<LetteringStyle, Look> Looks = new Dictionary<LetteringStyle, Look>
        {
            [LetteringStyle.Figure] = new Look("Lettering Figure", heavier: 0.2f),
            [LetteringStyle.Label] = new Look("Lettering Label", heavier: 0.14f),
            [LetteringStyle.OnWood] = new Look("Lettering On Wood", heavier: 0.06f, shade: EdgeOnWood),
            [LetteringStyle.OnButton] = new Look("Lettering On Button", heavier: 0.4f, shade: ShadeOnButton, outline: OutlineOnButton),
            [LetteringStyle.OnTeal] = new Look("Lettering On Teal", heavier: 0.4f, shade: ShadeOnTeal, outline: OutlineOnTeal),
            [LetteringStyle.OnBlue] = new Look("Lettering On Blue", heavier: 0.4f, shade: ShadeOnBlue, outline: OutlineOnBlue),
        };

        /// <summary>Makes what is missing and returns the typeface with its materials, for the art catalogue.</summary>
        /// <exception cref="InvalidOperationException">The font file is missing, or lacks a letter the game writes.</exception>
        public static (TMP_FontAsset Typeface, IReadOnlyDictionary<LetteringStyle, Material> Materials) Ensure()
        {
            TMP_FontAsset typeface = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TypefacePath);
            if (typeface == null)
            {
                typeface = CreateTypeface();
            }

            RefuseMissingLetters(typeface);
            var materials = new Dictionary<LetteringStyle, Material>();
            foreach (KeyValuePair<LetteringStyle, Look> look in Looks)
            {
                materials[look.Key] = EnsureMaterial(typeface, look.Value);
            }

            return (typeface, materials);
        }

        private static string Letters()
        {
            var letters = new StringBuilder();
            for (char ascii = ' '; ascii <= '~'; ascii++)
            {
                letters.Append(ascii);
            }

            return letters.Append(LettersBeyondAscii).ToString();
        }

        private static TMP_FontAsset CreateTypeface()
        {
            Font fontFile = AssetDatabase.LoadAssetAtPath<Font>(FontFilePath);
            if (fontFile == null)
            {
                throw new InvalidOperationException($"{FontFilePath} is missing; run node tools/unity/import-text-resources.js from the repository root.");
            }

            TMP_FontAsset typeface = TMP_FontAsset.CreateFontAsset(
                fontFile, SamplingSize, RoomAroundALetter, GlyphRenderMode.SDFAA, PictureSide, PictureSide, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (typeface == null)
            {
                throw new InvalidOperationException($"{FontFilePath} could not be read as a font; its import settings must include the font's data.");
            }

            if (!typeface.TryAddCharacters(Letters(), out string missing))
            {
                throw new InvalidOperationException($"{FontFilePath} has no letter for \"{missing}\", or the typeface's picture is too small to hold them all.");
            }

            // From here on the set of letters is fixed and the font file is no longer part of the asset.
            typeface.atlasPopulationMode = AtlasPopulationMode.Static;
            typeface.material.shader = TextShader();
            AssetDatabase.CreateAsset(typeface, TypefacePath);
            typeface.material.name = "Lettering Plain";
            AssetDatabase.AddObjectToAsset(typeface.material, typeface);
            typeface.atlasTexture.name = "Lettering Picture";
            AssetDatabase.AddObjectToAsset(typeface.atlasTexture, typeface);
            EditorUtility.SetDirty(typeface);
            return typeface;
        }

        private static void RefuseMissingLetters(TMP_FontAsset typeface)
        {
            if (!typeface.HasCharacters(Letters(), out List<char> missing))
            {
                throw new InvalidOperationException($"{TypefacePath} lacks {new string(missing.ToArray())}; delete it and run ProjectSetup.Run again to make it anew.");
            }
        }

        private static Shader TextShader()
        {
            Shader shader = Shader.Find(TextShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"The shader {TextShaderName} is missing; run node tools/unity/import-text-resources.js from the repository root.");
            }

            return shader;
        }

        /// <summary>A style's material: the typeface's own, with the style's look. It is written anew each time, so the table above is the truth.</summary>
        private static Material EnsureMaterial(TMP_FontAsset typeface, Look look)
        {
            string path = $"Assets/Art/Fonts/{look.Name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(typeface.material) { name = look.Name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(typeface.material);
            material.shaderKeywords = Array.Empty<string>();
            material.SetFloat(ShaderUtilities.ID_FaceDilate, look.Heavier);
            if (look.Outline is Outline outline)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
                material.SetColor(ShaderUtilities.ID_OutlineColor, outline.Colour);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, outline.Width);
            }

            if (look.Shade is Shade shade)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, shade.Colour);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -shade.Drop);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, shade.Wider);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shade.Softness);
            }

            // The shader scales a look by ratios that depend on it; TextMesh Pro works them out from the material.
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A line of another colour around the strokes. It is drawn half over the strokes' edge and half outside it,
        /// so a style with an outline widens its strokes by as much as the outline covers of them.
        /// </summary>
        private readonly struct Outline
        {
            public Outline(Color colour, float width)
            {
                Colour = colour;
                Width = width;
            }

            public Color Colour { get; }

            /// <summary>As a part of the room around a letter.</summary>
            public float Width { get; }
        }

        /// <summary>A second, coloured drawing of the letters under the first: a shadow, or a dark edge along their foot.</summary>
        private readonly struct Shade
        {
            public Shade(Color colour, float drop, float wider, float softness)
            {
                Colour = colour;
                Drop = drop;
                Wider = wider;
                Softness = softness;
            }

            public Color Colour { get; }

            /// <summary>How far under the letters it lies, as a part of the room around a letter.</summary>
            public float Drop { get; }

            /// <summary>How much wider than the font file's strokes its strokes are: as wide as an outline's outer edge, to show under an outline.</summary>
            public float Wider { get; }

            public float Softness { get; }
        }

        /// <summary>The look of a lettering style, and the name of its material.</summary>
        private readonly struct Look
        {
            public Look(string name, float heavier, Shade? shade = null, Outline? outline = null)
            {
                Name = name;
                Heavier = heavier;
                Shade = shade;
                Outline = outline;
            }

            public string Name { get; }

            /// <summary>How far the strokes are widened, as a part of the room around a letter: 0 draws the font file's own weight.</summary>
            public float Heavier { get; }

            public Shade? Shade { get; }

            public Outline? Outline { get; }
        }
    }
}
