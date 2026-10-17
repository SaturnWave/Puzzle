using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The game's lettering: lines of text that stand in the scene like the sprites do and are drawn by the game's
    /// camera, sorted with everything else. A line is set in the typeface of its style, with the spacing between
    /// pairs of letters that the typeface asks for. It is centred on its object's place: sideways on its middle,
    /// upright on the middle of its capital letters and figures, so a number sits where the design puts it whatever
    /// its digits. Sizes are in points, as in the design.
    /// </summary>
    public static class Lettering
    {
        /// <summary>The colours of the design's type.</summary>
        public static readonly Color Ink = Tint.Of(0x46260F);
        public static readonly Color SoftInk = Tint.Of(0x7A4A12);
        public static readonly Color Danger = Tint.Of(0xB3162B);
        public static readonly Color OnDark = Tint.Of(0xFFF8E6);

        /// <summary>The ink of the small capitals that label what stands under them, on cream.</summary>
        public static readonly Color LabelInk = Tint.Of(0x8A5A2B);

        /// <summary>The ink of a title on brass.</summary>
        public static readonly Color BrassInk = Tint.Of(0x4B2A0F);

        /// <summary>The cream of lettering that stands on the gallery's dark teal with no plate under it.</summary>
        public static readonly Color CreamOnTeal = Tint.Of(0xFFF4C4);

        /// <summary>How much larger than the figures a sign the typefaces draw small is set where it stands for a number (<see cref="ReadyForAFigure"/>).</summary>
        public const float SignLarger = 1.8f;

        /// <summary>TextMesh Pro counts the room between letters and between words in hundredths of the type size.</summary>
        private const float HundredthsOfTheTypeSize = 100f;

        public static TextMeshPro Write(string name, Transform parent, ArtCatalogue art, LetteringStyle style, float size, Color colour, DrawOrder order)
        {
            var line = new GameObject(name).AddComponent<TextMeshPro>();
            line.transform.SetParent(parent, worldPositionStays: false);
            line.rectTransform.sizeDelta = Vector2.zero; // no box: the line is laid out around its own place
            line.font = art.TypefaceOf(style);
            line.fontSharedMaterial = art.MaterialOf(style);
            line.fontFeatures = new List<OTL_FeatureTag> { OTL_FeatureTag.kern };

            // An outline takes its width from the gap between two words at each side; the design gives most gaps one width back.
            SetWordsApart(line, LetteringOutlines.WidthOf(style));
            line.isOrthographic = true; // a font size is then a size in scene units, which are points
            line.fontSize = size;
            line.color = colour;
            line.alignment = TextAlignmentOptions.Capline;
            line.textWrappingMode = TextWrappingModes.NoWrap;
            line.overflowMode = TextOverflowModes.Overflow;
            line.sortingOrder = (int)order;
            return line;
        }

        /// <summary>
        /// Sets the words of a line farther apart than its typeface does, by a part of the type size: a
        /// stylesheet's word-spacing. A line is written with its style's outline width for this
        /// (<see cref="Write"/>); a label whose drawing gives its words another gap states that one, before
        /// anything is said or measured.
        /// </summary>
        public static void SetWordsApart(TextMeshPro line, float byPartOfTheTypeSize) => line.wordSpacing = HundredthsOfTheTypeSize * byPartOfTheTypeSize;

        /// <summary>
        /// How far under the middle of its line a style's capitals and figures stand where a stylesheet centres the
        /// line on a height, as a part of the type size; less than nothing where they stand over it. The design
        /// places its numbers by the line, the game by the middle of the digits: a number that is to stand as the
        /// design has it is moved down by this.
        /// </summary>
        public static float FigureDrop(LetteringStyle style) => FigureDropOf(Typefaces.Of(style));

        /// <summary>
        /// Each family's drop, from the measures of its font file: half of what its ascent has over its descent and
        /// the height of its capitals, all as parts of the type size (Gabarito 0.94, 0.26 and 0.681; Paytone One
        /// 1.113, 0.283 and 0.688; Young Serif 1.046, 0.366 and 0.75).
        /// </summary>
        /// <summary>How high a style's capitals stand over its baseline, as a part of the type size: the measures <see cref="FigureDropOf"/> names.</summary>
        public static float CapitalsHeight(LetteringStyle style) => Typefaces.Of(style) switch
        {
            Typeface.GabaritoBold => 0.681f,
            Typeface.GabaritoExtraBold => 0.681f,
            Typeface.PaytoneOne => 0.688f,
            Typeface.YoungSerif => 0.75f,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "A typeface whose measures are not listed."),
        };

        private static float FigureDropOf(Typeface typeface) => typeface switch
        {
            Typeface.GabaritoBold => 0f,
            Typeface.GabaritoExtraBold => 0f,
            Typeface.PaytoneOne => 0.071f,
            Typeface.YoungSerif => -0.035f,
            _ => throw new ArgumentOutOfRangeException(nameof(typeface), typeface, "A typeface whose measures are not listed."),
        };

        /// <summary>
        /// Changes what a line says and lays it out at once: the screenshot command pictures the stage with no game
        /// running, where no frame would do it later.
        /// </summary>
        public static void Say(TextMeshPro line, string words)
        {
            line.text = words;
            line.ForceMeshUpdate();
        }

        /// <summary>
        /// Changes the colour a line is drawn in and draws it so at once, for the same reason: white for a style's
        /// own colours, a shade for a line on something that is dimmed.
        /// </summary>
        public static void DrawIn(TextMeshPro line, Color colour)
        {
            line.color = colour;
            line.ForceMeshUpdate();
        }

        /// <summary>
        /// Readies a line laid out for figures of a size to say a number, centred on its capitals as every line is, or
        /// a sign that the game's typefaces draw as small and as low as a small letter ("∞", the tester mode's coins and
        /// lives): set <see cref="SignLarger"/> times larger and centred on its own drawing, it stands about as large
        /// as the figures and on their middle. The line keeps the figures' place; say the words after this.
        /// </summary>
        public static void ReadyForAFigure(TextMeshPro line, float figureSize, bool isASign)
        {
            line.fontSize = isASign ? figureSize * SignLarger : figureSize;
            line.verticalAlignment = isASign ? VerticalAlignmentOptions.Geometry : VerticalAlignmentOptions.Capline;
        }

        /// <summary>
        /// Says something at a type size, or at a smaller one where the words would not fit the room they have: a
        /// line is never cut off and never runs over the edge of what it is written on.
        /// </summary>
        public static void SayWithin(TextMeshPro line, string words, float size, float room)
        {
            line.fontSize = size;
            line.fontSize = size * Mathf.Min(1f, room / WidthOf(line, words));
            Say(line, words);
        }

        /// <summary>
        /// Says something so that it begins at a place instead of being centred on it: the line is moved to where
        /// its middle then lies. <paramref name="left"/> and <paramref name="height"/> are in the points of what the line stands on.
        /// </summary>
        public static void SayFromLeft(TextMeshPro line, string words, float left, float height)
        {
            Say(line, words);
            line.transform.localPosition = new Vector3(left + WidthOf(line, words) / 2f, height, 0f);
        }

        /// <summary>Says something so that it ends at a place instead of being centred on it.</summary>
        public static void SayUpToRight(TextMeshPro line, string words, float right, float height)
        {
            Say(line, words);
            line.transform.localPosition = new Vector3(right - WidthOf(line, words) / 2f, height, 0f);
        }

        /// <summary>
        /// Says words that may take more than one line: broken between words to fit a width, every line from the left
        /// edge of that width, the block's top edge at a height. <paramref name="left"/> and <paramref name="top"/>
        /// are in the points of what the words stand on.
        /// </summary>
        /// <returns>How high the block of lines is, at the line's type size.</returns>
        public static float SayInLines(TextMeshPro line, string words, float width, float left, float top)
        {
            line.textWrappingMode = TextWrappingModes.Normal;
            line.alignment = TextAlignmentOptions.TopLeft;
            float height = line.GetPreferredValues(words, width, 0f).y;
            line.rectTransform.sizeDelta = new Vector2(width, height);
            Say(line, words);
            line.transform.localPosition = new Vector3(left + width / 2f, top - height / 2f, 0f);
            return height;
        }

        /// <summary>How wide the line would be saying this, at its current size.</summary>
        public static float WidthOf(TextMeshPro line, string words) => line.GetPreferredValues(words).x;
    }
}
