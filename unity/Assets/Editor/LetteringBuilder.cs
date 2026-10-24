#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Engine;
using Puzzle.Client.Words;
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
    /// <para>
    /// Behind each typeface stand its fallbacks (<see cref="FallbackTypefaces"/>): typefaces of the other scripts,
    /// each made with exactly the letters of its script that its own language's tables use; all of them stand
    /// behind every typeface, so the language card can name each language in its own letters whatever the game
    /// speaks, and the art catalogue asks the spoken language's own first. A letter a fallback draws
    /// is drawn in the material of the style that asks for it, as TextMesh Pro makes one for the fallback's picture:
    /// each fallback's picture reaches as far from a letter as its typeface's does, so an outline is as wide.
    /// </para>
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

        /// <summary>
        /// The picture of each fallback: an eighth of the type size around a letter, as every typeface has it. A
        /// Hangul syllable fills its square, so the 440 the Korean tables use need a picture of 2048 by 1024 at a
        /// sampling of 48; a new word's syllables find room in it until it is full, and the builder says so then.
        /// The 60-odd Cyrillic letters of Russian fit a picture of 512 by 512. A Chinese character fills its square
        /// too, and Japanese and each Chinese write 550 to 720 of them with their kana and marks: at a sampling of 40
        /// they fit the Korean picture's size, half the memory and the package of a sampling of 48, which would take
        /// a picture of 2048 by 2048 each; a stroke of a dense character stays sharp at the sizes the game sets it.
        /// Both Chinese in the serif write the most of them once the world map's places (#4) and the purchase cards'
        /// words (v1) are counted: they have a picture of 2048 by 2048.
        /// </summary>
        private static readonly IReadOnlyDictionary<FallbackTypeface, FontPicture> FallbackPictures = new Dictionary<FallbackTypeface, FontPicture>
        {
            [FallbackTypeface.NotoSansKR] = new FontPicture("NotoSansKR-ExtraBold", samplingSize: 48, roomAroundALetter: 6, width: 2048, height: 1024),
            [FallbackTypeface.NotoSerifKR] = new FontPicture("NotoSerifKR-Bold", samplingSize: 48, roomAroundALetter: 6, width: 2048, height: 1024),
            [FallbackTypeface.NotoSans] = new FontPicture("NotoSans-ExtraBold", samplingSize: 48, roomAroundALetter: 6, width: 512, height: 512),
            [FallbackTypeface.NotoSerif] = new FontPicture("NotoSerif-Bold", samplingSize: 48, roomAroundALetter: 6, width: 512, height: 512),
            [FallbackTypeface.NotoSansJP] = new FontPicture("NotoSansJP-Bold", samplingSize: 40, roomAroundALetter: 5, width: 2048, height: 1024, ending: OpenTypeWithPostScriptOutlines),
            [FallbackTypeface.NotoSerifJP] = new FontPicture("NotoSerifJP-Bold", samplingSize: 40, roomAroundALetter: 5, width: 2048, height: 1024, ending: OpenTypeWithPostScriptOutlines),
            [FallbackTypeface.NotoSansSC] = new FontPicture("NotoSansSC-Bold", samplingSize: 40, roomAroundALetter: 5, width: 2048, height: 1024, ending: OpenTypeWithPostScriptOutlines),
            [FallbackTypeface.NotoSerifSC] = new FontPicture("NotoSerifSC-Bold", samplingSize: 40, roomAroundALetter: 5, width: 2048, height: 2048, ending: OpenTypeWithPostScriptOutlines),
            [FallbackTypeface.NotoSansTC] = new FontPicture("NotoSansTC-Bold", samplingSize: 40, roomAroundALetter: 5, width: 2048, height: 1024, ending: OpenTypeWithPostScriptOutlines),
            [FallbackTypeface.NotoSerifTC] = new FontPicture("NotoSerifTC-Bold", samplingSize: 40, roomAroundALetter: 5, width: 2048, height: 2048, ending: OpenTypeWithPostScriptOutlines),
        };

        /// <summary>The ending of a font file of the Noto CJK releases, whose letters are drawn as PostScript outlines; the others end in ".ttf".</summary>
        private const string OpenTypeWithPostScriptOutlines = ".otf";

        /// <summary>The dark edge under lettering on wood: the web page's "0 1px 0" text shadow at a 13 px type size.</summary>
        private static readonly Shade EdgeOnWood = new Shade(new Color32(40, 18, 6, 166), drop: 0.075f, softness: 0.08f);

        /// <summary>The dark of each ground that white lettering stands on: the colour of its outline.</summary>
        private static readonly Color DarkGreen = new Color32(0x14, 0x5A, 0x1B, 0xFF);
        private static readonly Color DarkTeal = new Color32(0x0A, 0x36, 0x40, 0xFF);
        private static readonly Color DarkBlue = new Color32(0x0F, 0x26, 0x70, 0xFF);
        private static readonly Color DarkRed = new Color32(0x5E, 0x0A, 0x0C, 0xFF);

        /// <summary>The dark brown of the gilt's own line (the wallet's pills, the entry page's bar), which lettering on the entry page's cream stands in.</summary>
        private static readonly Color DarkBrown = new Color32(0x4B, 0x26, 0x08, 0xFF);

        /// <summary>
        /// The hard shade under the entry page's gilt title (EntryTR: "drop-shadow(0 3px 0 #3A1E06)" at 46 px), under
        /// its outline: the outline, the drop and the edge's softness together stay inside what Young Serif's picture
        /// reaches (0.125 of the type size).
        /// </summary>
        private static readonly Shade EdgeUnderTheGiltTitle = new Shade(new Color32(0x3A, 0x1E, 0x06, 0xFF), drop: 0.055f, softness: 0.05f);

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
            [LetteringStyle.GiltTitle] = new Look("Lettering Gilt Title", shade: EdgeUnderTheGiltTitle, outlineColour: DarkBrown),
            [LetteringStyle.OnCream] = new Look("Lettering On Cream", outlineColour: DarkBrown),
        };

        /// <summary>Makes what is missing and returns each style's typeface and material, and each fallback's typeface, for the art catalogue.</summary>
        /// <exception cref="InvalidOperationException">
        /// A font file is missing or lacks a letter the game writes, a typeface's picture is too small for the
        /// letters, a word of a table has a letter no typeface draws, a style asks for more than its typeface's
        /// picture can draw, or a style's outline has a width and no colour or a colour and no width.
        /// </exception>
        /// <exception cref="WordTableException">A table of words the game carries is not a table.</exception>
        public static (IReadOnlyDictionary<LetteringStyle, TMP_FontAsset> Typefaces, IReadOnlyDictionary<LetteringStyle, Material> Materials,
            IReadOnlyDictionary<FallbackTypeface, TMP_FontAsset> Fallbacks) Ensure()
        {
            var made = new Dictionary<Typeface, TMP_FontAsset>();
            foreach (Typeface typeface in (Typeface[])Enum.GetValues(typeof(Typeface)))
            {
                made[typeface] = EnsureTypeface(PictureOf(typeface), LetterSet.All, holdsExactlyThese: false);
            }

            IReadOnlyDictionary<Language, IReadOnlyList<string>> words = EveryTablesWords();
            RefuseWordsNoTypefaceDraws(words);
            var fallbacks = new Dictionary<FallbackTypeface, TMP_FontAsset>();
            foreach (FallbackTypeface fallback in FallbackTypefaces.All)
            {
                IReadOnlyList<string> wordsOfItsLanguage = words.TryGetValue(FallbackTypefaces.LanguageOf(fallback), out IReadOnlyList<string> its) ? its : Array.Empty<string>();
                fallbacks[fallback] = EnsureTypeface(FallbackPictureOf(fallback), FallbackTypefaces.LettersFor(fallback, wordsOfItsLanguage), holdsExactlyThese: true);
            }

            foreach (KeyValuePair<Typeface, TMP_FontAsset> typeface in made)
            {
                StandBehind(typeface.Value, FallbackTypefaces.Behind(typeface.Key).Select(fallback => fallbacks[fallback]).ToList());
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

            return (typefaces, materials, fallbacks);
        }

        private static FontPicture PictureOf(Typeface typeface) =>
            Pictures.TryGetValue(typeface, out FontPicture picture)
                ? picture
                : throw new InvalidOperationException($"The lettering builder names no font file for {typeface}.");

        private static FontPicture FallbackPictureOf(FallbackTypeface fallback) =>
            FallbackPictures.TryGetValue(fallback, out FontPicture picture)
                ? picture
                : throw new InvalidOperationException($"The lettering builder names no font file for the fallback {fallback}.");

        /// <summary>Every piece of words of every table the game carries, complete or not, by language: what the typefaces and their fallbacks must be able to write.</summary>
        private static IReadOnlyDictionary<Language, IReadOnlyList<string>> EveryTablesWords()
        {
            var words = new Dictionary<Language, IReadOnlyList<string>>();
            foreach (Language language in Languages.All)
            {
                if (WordsPack.Load(language) is Wording wording)
                {
                    words[language] = wording.Words.AllWords.Select(entry => entry.Words).Concat(wording.Captions.AllWords).Concat(PlacesPack.EveryLabelIn(language)).ToList();
                }
            }

            return words;
        }

        /// <summary>Stops with the letters of a language's tables that neither the game's own set nor a fallback made for the language draws: they would be drawn as gaps.</summary>
        private static void RefuseWordsNoTypefaceDraws(IReadOnlyDictionary<Language, IReadOnlyList<string>> words)
        {
            foreach (KeyValuePair<Language, IReadOnlyList<string>> language in words)
            {
                string none = FallbackTypefaces.LettersNoneDraws(language.Key, string.Concat(language.Value));
                if (none.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"The {Languages.CodeOf(language.Key)} tables of words write {string.Join(" ", none.Select(letter => $"U+{(int)letter:X4} {letter}"))}, which no typeface of the game draws: "
                        + "add a fallback for the language that covers them, or the letters to the game's set.");
                }
            }
        }

        /// <summary>Sets the fallbacks a typeface asks for a letter it lacks, in their order; a typeface that has them already is left as it is.</summary>
        private static void StandBehind(TMP_FontAsset typeface, List<TMP_FontAsset> fallbacks)
        {
            List<TMP_FontAsset> before = typeface.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
            if (before.SequenceEqual(fallbacks))
            {
                return;
            }

            typeface.fallbackFontAssetTable = fallbacks;
            EditorUtility.SetDirty(typeface);
            Debug.Log($"Lettering: {typeface.name} asks {string.Join(", ", fallbacks.Select(fallback => fallback.name))} for the letters it lacks.");
        }

        /// <summary>
        /// The typeface of a font file as the table asks for it: the one that is there, or a new one where that
        /// is another picture, lacks a letter, or holds other drawings than the font file names now. A fallback's
        /// letters follow the tables, so they change with a word: a fallback that is the picture asked for and
        /// differs only in its letters is filled anew where it is, which keeps the asset the design typefaces name.
        /// </summary>
        private static TMP_FontAsset EnsureTypeface(FontPicture picture, string letters, bool holdsExactlyThese)
        {
            Font fontFile = AssetDatabase.LoadAssetAtPath<Font>(picture.FontFilePath);
            if (fontFile == null)
            {
                throw new InvalidOperationException($"{picture.FontFilePath} is missing; run node {FontFetcher} from the repository root.");
            }

            TMP_FontAsset typeface = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(picture.TypefacePath);
            if (typeface != null && IsMadeAs(picture, typeface, letters, holdsExactlyThese) && HoldsTheDrawingsOf(fontFile, picture, typeface, letters))
            {
                return typeface;
            }

            if (typeface != null && holdsExactlyThese && IsThePictureAskedFor(picture, typeface))
            {
                return FillAnew(typeface, fontFile, picture, letters);
            }

            if (typeface != null)
            {
                Debug.Log($"Lettering: {picture.TypefacePath} is not the picture the builder asks for now, lacks or has letters it should not, or its font file has changed; it is made anew.");
                AssetDatabase.DeleteAsset(picture.TypefacePath);
            }

            return CreateTypeface(fontFile, picture, letters);
        }

        private static bool IsMadeAs(FontPicture picture, TMP_FontAsset typeface, string letters, bool holdsExactlyThese) =>
            IsThePictureAskedFor(picture, typeface)
            && typeface.HasCharacters(letters)
            && (!holdsExactlyThese || typeface.characterTable.Count == letters.Length)
            && typeface.fontFeatureTable.glyphPairAdjustmentRecords.All(pair => Shows(pair, picture))
            && typeface.fontFeatureTable.MarkToBaseAdjustmentRecords.Count == 0;

        /// <summary>
        /// True where every letter of the set is, in the typeface, the drawing the font file names for it: a font
        /// file numbers its drawings, and a typeface keeps for each letter the number it was made from. A file
        /// that was changed since (other figures, a newer version of the font) names other numbers.
        /// </summary>
        private static bool HoldsTheDrawingsOf(Font fontFile, FontPicture picture, TMP_FontAsset typeface, string letters)
        {
            LoadTheFace(fontFile, picture);
            return letters.All(letter =>
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

        private static bool IsThePictureAskedFor(FontPicture picture, TMP_FontAsset typeface) =>
            typeface.faceInfo.pointSize == picture.SamplingSize
            && typeface.atlasPadding == picture.RoomAroundALetter
            && typeface.atlasWidth == picture.Width
            && typeface.atlasHeight == picture.Height;

        /// <summary>
        /// Empties a typeface and makes it anew with these letters, in the asset it is: for that while, its font file
        /// is its source again, and the letters are drawn into its picture as a new typeface's are.
        /// </summary>
        private static TMP_FontAsset FillAnew(TMP_FontAsset typeface, Font fontFile, FontPicture picture, string letters)
        {
            RefuseAFontThatLacksLetters(fontFile, picture, letters);
            typeface.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            typeface.ClearFontAssetData(setAtlasSizeToZero: false);
            if (!typeface.TryAddCharacters(letters, out string unplaced, includeFontFeatures: true))
            {
                throw new InvalidOperationException(
                    $"A picture of {picture.Width} by {picture.Height} is too small for the letters of {picture.FontFilePath}: no place was left for \"{unplaced}\".");
            }

            KeepTheSpacingsThatShow(typeface, picture);
            typeface.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(typeface);
            Debug.Log($"Lettering: {picture.TypefacePath} was filled anew with the {typeface.characterTable.Count} letters the tables use now.");
            return typeface;
        }

        private static TMP_FontAsset CreateTypeface(Font fontFile, FontPicture picture, string letters)
        {
            RefuseAFontThatLacksLetters(fontFile, picture, letters);
            TMP_FontAsset typeface = TMP_FontAsset.CreateFontAsset(
                fontFile, picture.SamplingSize, picture.RoomAroundALetter, GlyphRenderMode.SDFAA, picture.Width, picture.Height,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (typeface == null)
            {
                throw new InvalidOperationException($"{picture.FontFilePath} could not be read as a font; its import settings must include the font's data.");
            }

            // With the letters comes what the font says about pairs of them: how much closer "T" and "a" stand than their widths say.
            if (!typeface.TryAddCharacters(letters, out string unplaced, includeFontFeatures: true))
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

        /// <summary>Stops with the names of the letters a typeface is made with that its font file has no drawing for.</summary>
        private static void RefuseAFontThatLacksLetters(Font fontFile, FontPicture picture, string letters)
        {
            LoadTheFace(fontFile, picture);
            char[] lacking = letters.Where(letter => !FontEngine.TryGetGlyphIndex(letter, out uint _)).ToArray();
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
            public FontPicture(string fontFileName, int samplingSize, int roomAroundALetter, int width, int height, string ending = ".ttf")
            {
                FontFileName = fontFileName;
                SamplingSize = samplingSize;
                RoomAroundALetter = roomAroundALetter;
                Width = width;
                Height = height;
                Ending = ending;
            }

            /// <summary>The font file's name without its ending.</summary>
            public string FontFileName { get; }

            /// <summary>The font file's ending: ".ttf", or ".otf" for a file of PostScript outlines.</summary>
            public string Ending { get; }

            /// <summary>How many pixels high a letter's line is in the picture.</summary>
            public int SamplingSize { get; }

            /// <summary>The pixels around each letter, over which the picture knows the distance to the letter's edge.</summary>
            public int RoomAroundALetter { get; }

            public int Width { get; }

            public int Height { get; }

            public string FontFilePath => $"{FontsFolder}/{FontFileName}{Ending}";

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
