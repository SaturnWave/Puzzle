using Puzzle.Client.Art;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The game's lettering: lines of text that stand in the scene like the sprites do and are drawn by the game's
    /// camera, sorted with everything else. A line is centred on its object's place: sideways on its middle, upright
    /// on the middle of its capital letters and figures, so a number sits where the design puts it whatever its
    /// digits. Sizes are in points, as in the design.
    /// </summary>
    public static class Lettering
    {
        /// <summary>The colours of the design's type.</summary>
        public static readonly Color Ink = Tint.Of(0x46260F);
        public static readonly Color SoftInk = Tint.Of(0x7A4A12);
        public static readonly Color Danger = Tint.Of(0xB3162B);
        public static readonly Color OnDark = Tint.Of(0xFFF8E6);

        public static TextMeshPro Write(string name, Transform parent, ArtCatalogue art, LetteringStyle style, float size, Color colour, DrawOrder order)
        {
            var line = new GameObject(name).AddComponent<TextMeshPro>();
            line.transform.SetParent(parent, worldPositionStays: false);
            line.rectTransform.sizeDelta = Vector2.zero; // no box: the line is laid out around its own place
            line.font = art.Typeface;
            line.fontSharedMaterial = art.MaterialOf(style);
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
        /// Changes what a line says and lays it out at once: the screenshot command pictures the stage with no game
        /// running, where no frame would do it later.
        /// </summary>
        public static void Say(TextMeshPro line, string words)
        {
            line.text = words;
            line.ForceMeshUpdate();
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

        /// <summary>How wide the line would be saying this, at its current size.</summary>
        public static float WidthOf(TextMeshPro line, string words) => line.GetPreferredValues(words).x;
    }
}
