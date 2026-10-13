#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Puzzle.Client.Art;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Makes the game's typefaces ready to draw: from each font file of the design (tools/unity/fetch-fonts.js
    /// fetches them) a TextMesh Pro font asset with every letter the game can write (<see cref="LetterSet"/>), and
    /// one material per lettering style. The letters are stored as distances to their outline, not as pixels, so
    /// one picture of them is sharp at every size, and a style can draw a line around the strokes and a shade
    /// under them. An asset holds a fixed set of letters and no font file: a build carries the pictures alone.
    /// </summary>
    /// <remarks>
    /// A typeface is made anew by itself when its picture is not the one the table below asks for, when it lacks
    /// a letter of the set, and when its font file names another drawing for a letter than the typeface holds
    /// (Young Serif's figures became its lining ones that way). A font file that redraws its letters and keeps
    /// their numbers is not noticed: delete the typeface's asset then and run ProjectSetup.Run.
    /// </remarks>
    public static class LetteringBuilder
    {
        private const string FontsFolder = "Assets/Art/Fonts";
        private const string FontFetcher = "tools/unity/fetch-fonts.js";
        private const string PlainShaderName = "TextMeshPro/Mobile/Distance Field";

        /// <summary>Draws a line's outlines under all of its letters (Assets/Art/Shaders/LetteringOutlined.shader).</summary>
        private const string OutlinedShaderName = "Puzzle/Lettering Outlined";

        /// <summary>
        /// Of what a font file says about pairs of letters, a pair's own spacing is kept where it moves a letter by
        /// this part of the type size or more: over a point at the size of a button's label ("Ta", "Yı", "To").
        /// The files name nearly every pair of letters, most of them by less than shows on a phone, and a typeface
        /// that kept them all would be several times the size of its picture.
        /// </summary>
        private const float SmallestPairSpacing = 0.04f;

        /// <summary>
        /// The picture of each typeface. The room around a letter is how far from its edge the picture knows the
        /// distance to it, and so how far from the letter a style may draw: an eighth of the type size everywhere.
        /// Paytone One carries the outlined styles, whose line reaches up to 0.105 of the type size (see
        /// <see cref="Looks"/>); the others are drawn as their files have them and throw at most a thin shade.
        /// Gabarito's lighter weight is only set small, so its picture is smaller too.
        /// </summary>
        private static readonly IReadOnlyDictionary<Typeface, FontPicture> Pictures = new Dictionary<Typeface, FontPicture>
        {
            [Typeface.GabaritoBold] = new FontPicture("Gabarito-Bold", samplingSize: 48, roomAroundALetter: 6, width: 1024, height: 512),
            [Typeface.GabaritoExtraBold] = new FontPicture("Gabarito-ExtraBold", samplingSize: 64, roomAroundALetter: 8, width: 1024, height: 1024),
            [Typeface.PaytoneOne] = new FontPicture("PaytoneOne-Regular", samplingSize: 64, roomAroundALetter: 8, width: 1024, height: 1024),
            [Typeface.YoungSerif] = new FontPicture("YoungSerif-Regular", samplingSize: 64, roomAroundALetter: 8, width: 1024, height: 1024),
        };

        /// <summary>The dark edge under lettering on wood: the web page's "0 1px 0" text shadow at a 13 px type size.</summary>
        private static readonly Shade EdgeOnWood = new Shade(new Color32(40, 18, 6, 166), drop: 0.075f, softness: 0.08f);

        /// <summary>The dark of each ground that white lettering stands on: the colour of its outline.</summary>
        private static readonly Color DarkGreen = new Color32(0x14, 0x5A, 0x1B, 0xFF);
        private static readonly Color DarkTeal = new Color32(0x0A, 0x36, 0x40, 0xFF);
        private static readonly Color DarkBlue = new Color32(0x0F, 0x26, 0x70, 0xFF);
        private static readonly Color DarkRed = new Color32(0x5E, 0x0A, 0x0C, 0xFF);

        /// <summary>
        /// What each style draws around the strokes of its typeface and under them; the strokes themselves are
        /// drawn as the font file has them. A style with an outline (<see cref="LetteringOutlines"/> has its
        /// width) names the outline's colour here.
        /// </summary>
        /// <remarks>
        /// A knowing difference from the design: it gives outlined lettering a hard shade of the outline's colour
        /// too, half a stroke under the letters (HomeTR, "Bölüm 47": stroke 5 px, shadow 0 2.5 px 0), so the dark
        /// edge under each letter is about twice as heavy as at its sides. The game draws the line as wide under
        /// the letters as beside them. Outline and shade together reach about 0.2 of the type size from a letter,
        /// and Paytone One's picture knows the distance to a letter for 0.125 (<see cref="Pictures"/>);
        /// <see cref="MeasuresOf"/> refuses a look that asks for more. Giving the shade back takes more room around
        /// a letter in that picture (14 px at its sampling size, in a larger picture) and a <see cref="Shade"/> of
        /// the outline's colour on these looks, whose drop the outlined shader's first pass draws; it has to be
        /// looked at beside HomeTR before it is kept.
        /// </remarks>
        private static readonly IReadOnlyDictionary<LetteringStyle, Look> Looks = new Dictionary<LetteringStyle, Look>
        {
            [LetteringStyle.Figure] = new Look("Lettering Figure"),
            [LetteringStyle.Label] = new Look("Lettering Label"),
            [LetteringStyle.OnWood] = new Look("Lettering On Wood", shade: EdgeOnWood),
            [LetteringStyle.OnButton] = new Look("Lettering On Button", outlineColour: DarkGreen),
            [LetteringStyle.OnTeal] = new Look("Lettering On Teal", outlineColour: DarkTeal),
            [LetteringStyle.OnBlue] = new Look("Lettering On Blue", outlineColour: DarkBlue),
            [LetteringStyle.Title] = new Look("Lettering Title"),
            [LetteringStyle.Soft] = new Look("Lettering Soft"),
            [LetteringStyle.OnHeart] = new Look("Lettering On Heart", outlineColour: DarkRed),
            [LetteringStyle.OnPricePill] = new Look("Lettering On Price Pill"),
            [LetteringStyle.OnDarkTeal] = new Look("Lettering On Dark Teal"),
        };

        /// <summary>Makes what is missing and returns each style's typeface and material, for the art catalogue.</summary>
        /// <exception cref="InvalidOperationException">
        /// A font file is missing or lacks a letter the game writes, a typeface's picture is too small for the
        /// letters, a style asks for more than its typeface's picture can draw, or a style's outline has a width
        /// and no colour or a colour and no width.
        /// </exception>
        public static (IReadOnlyDictionary<LetteringStyle, TMP_FontAsset> Typefaces, IReadOnlyDictionary<LetteringStyle, Material> Materials) Ensure()
        {
            var made = new Dictionary<Typeface, TMP_FontAsset>();
            foreach (Typeface typeface in (Typeface[])Enum.GetValues(typeof(Typeface)))
            {
                made[typeface] = EnsureTypeface(PictureOf(typeface));
            }

            var typefaces = new Dictionary<LetteringStyle, TMP_FontAsset>();
            var materials = new Dictionary<LetteringStyle, Material>();
            foreach (LetteringStyle style in (LetteringStyle[])Enum.GetValues(typeof(LetteringStyle)))
            {
                if (!Looks.TryGetValue(style, out Look look))
                {
                    throw new InvalidOperationException($"The lettering builder has no look for the style {style}.");
                }

                float outlineWidth = LetteringOutlines.WidthOf(style);
                if (look.OutlineColour.HasValue != outlineWidth > 0f)
                {
                    throw new InvalidOperationException(
                        $"The style {style} has an outline {Share(outlineWidth)} of the type size wide and {(look.OutlineColour.HasValue ? "a" : "no")} colour for one: "
                        + "the list of outlines and the builder's looks must agree.");
                }

                Typeface typeface = Typefaces.Of(style);
                typefaces[style] = made[typeface];
                materials[style] = EnsureMaterial(made[typeface], PictureOf(typeface), look, outlineWidth);
            }

            return (typefaces, materials);
        }

        private static FontPicture PictureOf(Typeface typeface) =>
            Pictures.TryGetValue(typeface, out FontPicture picture)
                ? picture
                : throw new InvalidOperationException($"The lettering builder names no font file for {typeface}.");

        /// <summary>
        /// The typeface of a font file as the table asks for it: the one that is there, or a new one where that
        /// is another picture, lacks a letter, or holds other drawings than the font file names now.
        /// </summary>
        private static TMP_FontAsset EnsureTypeface(FontPicture picture)
        {
            Font fontFile = AssetDatabase.LoadAssetAtPath<Font>(picture.FontFilePath);
            if (fontFile == null)
            {
                throw new InvalidOperationException($"{picture.FontFilePath} is missing; run node {FontFetcher} from the repository root.");
            }

            TMP_FontAsset typeface = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(picture.TypefacePath);
            if (typeface != null && IsMadeAs(picture, typeface) && HoldsTheDrawingsOf(fontFile, picture, typeface))
            {
                return typeface;
            }

            if (typeface != null)
            {
                Debug.Log($"Lettering: {picture.TypefacePath} is not the picture the builder asks for now, or its font file has changed; it is made anew.");
                AssetDatabase.DeleteAsset(picture.TypefacePath);
            }

            return CreateTypeface(fontFile, picture);
        }

        private static bool IsMadeAs(FontPicture picture, TMP_FontAsset typeface) =>
            typeface.faceInfo.pointSize == picture.SamplingSize
            && typeface.atlasPadding == picture.RoomAroundALetter
            && typeface.atlasWidth == picture.Width
            && typeface.atlasHeight == picture.Height
            && typeface.HasCharacters(LetterSet.All)
            && typeface.fontFeatureTable.glyphPairAdjustmentRecords.All(pair => Shows(pair, picture))
            && typeface.fontFeatureTable.MarkToBaseAdjustmentRecords.Count == 0;

        /// <summary>
        /// True where every letter of the set is, in the typeface, the drawing the font file names for it: a font
        /// file numbers its drawings, and a typeface keeps for each letter the number it was made from. A file
        /// that was changed since (other figures, a newer version of the font) names other numbers.
        /// </summary>
        private static bool HoldsTheDrawingsOf(Font fontFile, FontPicture picture, TMP_FontAsset typeface)
        {
            LoadTheFace(fontFile, picture);
            return LetterSet.All.All(letter =>
                FontEngine.TryGetGlyphIndex(letter, out uint drawing)
                && typeface.characterLookupTable.TryGetValue(letter, out TMP_Character kept)
                && kept.glyphIndex == drawing);
        }

        /// <summary>Makes a font file the one the font engine answers questions about.</summary>
        private static void LoadTheFace(Font fontFile, FontPicture picture)
        {
            if (FontEngine.LoadFontFace(fontFile, picture.SamplingSize) != FontEngineError.Success)
            {
                throw new InvalidOperationException($"{picture.FontFilePath} could not be read as a font; its import settings must include the font's data.");
            }
        }

        private static TMP_FontAsset CreateTypeface(Font fontFile, FontPicture picture)
        {
            RefuseAFontThatLacksLetters(fontFile, picture);
            TMP_FontAsset typeface = TMP_FontAsset.CreateFontAsset(
                fontFile, picture.SamplingSize, picture.RoomAroundALetter, GlyphRenderMode.SDFAA, picture.Width, picture.Height,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (typeface == null)
            {
                throw new InvalidOperationException($"{picture.FontFilePath} could not be read as a font; its import settings must include the font's data.");
            }

            // With the letters comes what the font says about pairs of them: how much closer "T" and "a" stand than their widths say.
            if (!typeface.TryAddCharacters(LetterSet.All, out string unplaced, includeFontFeatures: true))
            {
                throw new InvalidOperationException(
                    $"A picture of {picture.Width} by {picture.Height} is too small for the letters of {picture.FontFilePath}: no place was left for \"{unplaced}\".");
            }

            KeepTheSpacingsThatShow(typeface, picture);

            // From here on the set of letters is fixed and the font file is no longer part of the asset.
            typeface.atlasPopulationMode = AtlasPopulationMode.Static;
            typeface.material.shader = ShaderNamed(PlainShaderName);
            AssetDatabase.CreateAsset(typeface, picture.TypefacePath);
            typeface.material.name = $"{picture.FontFileName} Plain";
            AssetDatabase.AddObjectToAsset(typeface.material, typeface);
            typeface.atlasTexture.name = $"{picture.FontFileName} Picture";
            AssetDatabase.AddObjectToAsset(typeface.atlasTexture, typeface);
            EditorUtility.SetDirty(typeface);
            Debug.Log(
                $"Lettering: {picture.TypefacePath} holds {typeface.characterTable.Count} letters and {typeface.fontFeatureTable.glyphPairAdjustmentRecords.Count} pairs of letters with a spacing of their own, "
                + $"in a picture of {picture.Width} by {picture.Height}.");
            return typeface;
        }

        /// <summary>
        /// Of what the font file says about its letters beyond their drawings, keeps the spacings of pairs that
        /// show (<see cref="SmallestPairSpacing"/>). What it says about setting an accent on a letter is dropped
        /// whole: every letter of the set comes with its accent, as one drawing.
        /// </summary>
        private static void KeepTheSpacingsThatShow(TMP_FontAsset typeface, FontPicture picture)
        {
            TMP_FontFeatureTable features = typeface.fontFeatureTable;
            features.glyphPairAdjustmentRecords.RemoveAll(pair => !Shows(pair, picture));
            features.MarkToBaseAdjustmentRecords.Clear();
            features.MarkToMarkAdjustmentRecords.Clear();

            // The typeface looks pairs up in lists of its own, made from the ones just shortened.
            typeface.ReadFontAssetDefinition();
        }

        /// <summary>True for a pair of letters whose own spacing moves either of them far enough to show. A typeface's measures are in the pixels of its picture.</summary>
        private static bool Shows(GlyphPairAdjustmentRecord pair, FontPicture picture)
        {
            GlyphValueRecord first = pair.firstAdjustmentRecord.glyphValueRecord;
            GlyphValueRecord second = pair.secondAdjustmentRecord.glyphValueRecord;
            float moved = Mathf.Max(Mathf.Abs(first.xAdvance), Mathf.Abs(first.xPlacement), Mathf.Abs(second.xAdvance), Mathf.Abs(second.xPlacement));
            return moved >= SmallestPairSpacing * picture.SamplingSize;
        }

        /// <summary>Stops with the names of the letters of the game's set that a font file has no drawing for.</summary>
        private static void RefuseAFontThatLacksLetters(Font fontFile, FontPicture picture)
        {
            LoadTheFace(fontFile, picture);
            char[] lacking = LetterSet.All.Where(letter => !FontEngine.TryGetGlyphIndex(letter, out uint _)).ToArray();
            if (lacking.Length > 0)
            {
                throw new InvalidOperationException($"{picture.FontFilePath} has no letter for {string.Join(" ", lacking)}: the game could not write them in this typeface.");
            }
        }

        private static Shader ShaderNamed(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                throw new InvalidOperationException($"The shader {name} is missing or does not compile; TextMesh Pro's own come with node tools/unity/import-text-resources.js.");
            }

            return shader;
        }

        /// <summary>A style's material: the typeface's own, with the style's look. It is written anew each time, so the table above is the truth.</summary>
        /// <param name="outlineWidth">The width of the style's outline outside the letters, as a part of the type size; 0 for none.</param>
        private static Material EnsureMaterial(TMP_FontAsset typeface, FontPicture picture, Look look, float outlineWidth)
        {
            string path = $"{FontsFolder}/{look.Name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(typeface.material) { name = look.Name };
                AssetDatabase.CreateAsset(material, path);
            }

            ShaderMeasures measures = MeasuresOf(look, outlineWidth, picture);
            material.shader = ShaderNamed(look.OutlineColour.HasValue ? OutlinedShaderName : PlainShaderName);
            material.CopyPropertiesFromMaterial(typeface.material);
            material.shaderKeywords = Array.Empty<string>();
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            if (look.OutlineColour is Color outlineColour)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
                material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColour);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, measures.OutlineWidth);
            }

            if (look.Shade is Shade shade)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, shade.Colour);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -measures.ShadeDrop);
                material.SetFloat(ShaderUtilities.ID_UnderlayDilate, measures.OutlineWidth);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shade.Softness);
            }

            // The shaders scale a look by ratios that depend on it; TextMesh Pro works them out from the material.
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A look in the measures TextMesh Pro's shaders take. They count in parts of the reach of a typeface's
        /// picture: the room around a letter. An outline's width is counted outwards from the letter's edge (the
        /// outlined shader draws the letter's own strokes over it, whole), a shade is as wide as the outline and
        /// lies under it by its drop, and all of it must lie within the reach: the shaders would silently draw a
        /// look smaller that asks for more.
        /// </summary>
        /// <exception cref="InvalidOperationException">The look reaches further from a letter than the picture knows the distance to it.</exception>
        private static ShaderMeasures MeasuresOf(Look look, float outlineWidth, FontPicture picture)
        {
            float outline = outlineWidth / picture.Reach;
            float drop = (look.Shade?.Drop ?? 0f) / picture.Reach;
            float softness = look.Shade?.Softness ?? 0f;
            if (outline + drop + softness > 1f)
            {
                float asked = (outline + drop + softness) * picture.Reach;
                throw new InvalidOperationException(
                    $"{look.Name} draws {Share(asked)} of the type size from a letter, and the picture of {picture.FontFileName} reaches {Share(picture.Reach)}: "
                    + "give the typeface more room around a letter, or the style a thinner outline or a shorter drop.");
            }

            return new ShaderMeasures(outline, drop);
        }

        private static string Share(float part) => part.ToString("0.000", CultureInfo.InvariantCulture);

        /// <summary>A font file and the picture its letters are made into.</summary>
        private readonly struct FontPicture
        {
            public FontPicture(string fontFileName, int samplingSize, int roomAroundALetter, int width, int height)
            {
                FontFileName = fontFileName;
                SamplingSize = samplingSize;
                RoomAroundALetter = roomAroundALetter;
                Width = width;
                Height = height;
            }

            /// <summary>The font file's name without its ending.</summary>
            public string FontFileName { get; }

            /// <summary>How many pixels high a letter's line is in the picture.</summary>
            public int SamplingSize { get; }

            /// <summary>The pixels around each letter, over which the picture knows the distance to the letter's edge.</summary>
            public int RoomAroundALetter { get; }

            public int Width { get; }

            public int Height { get; }

            public string FontFilePath => $"{FontsFolder}/{FontFileName}.ttf";

            public string TypefacePath => $"{FontsFolder}/{FontFileName} Lettering.asset";

            /// <summary>How far from a letter's edge a style may draw, as a part of the type size: the room around a letter.</summary>
            public float Reach => (float)RoomAroundALetter / SamplingSize;
        }

        /// <summary>A second, coloured drawing of the letters and their outline under the first: a dark edge along their foot.</summary>
        private readonly struct Shade
        {
            public Shade(Color colour, float drop, float softness)
            {
                Colour = colour;
                Drop = drop;
                Softness = softness;
            }

            public Color Colour { get; }

            /// <summary>How far under the letters it lies, as a part of the type size.</summary>
            public float Drop { get; }

            /// <summary>How soft its edge is: 0 is a hard edge, 1 an edge as wide as the picture's reach.</summary>
            public float Softness { get; }
        }

        /// <summary>The look of a lettering style, and the name of its material.</summary>
        private readonly struct Look
        {
            public Look(string name, Shade? shade = null, Color? outlineColour = null)
            {
                Name = name;
                Shade = shade;
                OutlineColour = outlineColour;
            }

            public string Name { get; }

            public Shade? Shade { get; }

            /// <summary>The colour of the line around the strokes, outside them, where the strokes keep the weight of the font file; nothing for a style that draws no such line.</summary>
            public Color? OutlineColour { get; }
        }

        /// <summary>What a look is to TextMesh Pro's shaders, in parts of a picture's reach.</summary>
        private readonly struct ShaderMeasures
        {
            public ShaderMeasures(float outlineWidth, float shadeDrop)
            {
                OutlineWidth = outlineWidth;
                ShadeDrop = shadeDrop;
            }

            /// <summary>The outline's width outside the letters; a shade is made as wide.</summary>
            public float OutlineWidth { get; }

            public float ShadeDrop { get; }
        }
    }
}
