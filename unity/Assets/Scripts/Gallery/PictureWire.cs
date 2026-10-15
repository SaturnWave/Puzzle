using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The thin gilt wire that whatever hangs in the gallery hangs on: the plaques, the moves medallion, a result's
    /// card. Every wire is drawn the same, as the web test page draws them (gallery.css, .hangs::before): the wire
    /// and, a little to its right, its shade on the wall.
    /// </summary>
    public static class PictureWire
    {
        public const float Width = 1.3f;

        private const float ShadeFrom = 0.8f;
        private const float ShadeWidth = 1.6f;
        private const float Strength = 0.85f;
        private static readonly Color Gilt = Tint.Of(0xF3D58A, Strength);
        private static readonly Color Shade = Tint.Of(0x04242B, 0.35f * Strength);

        /// <summary>Adds an upright wire to a shape: from a height up to another, its left edge at a place; the wire is drawn over its shade.</summary>
        public static void Add(MeshSketch sketch, float left, float from, float until)
        {
            sketch.AddRectangle(new Rect(left + ShadeFrom, from, ShadeWidth, until - from), Shade);
            sketch.AddRectangle(new Rect(left, from, Width, until - from), Gilt);
        }

        /// <summary>
        /// Adds a slanting wire from one point to another, as a small frame hangs from a pin (HomeTR's gift): the wire
        /// as wide as an upright one, its shade a little to its right and under it.
        /// </summary>
        public static void AddSlanting(MeshSketch sketch, Vector2 from, Vector2 to)
        {
            AddBand(sketch, from + new Vector2(ShadeFrom, -ShadeFrom), to + new Vector2(ShadeFrom, -ShadeFrom), ShadeWidth, Shade);
            AddBand(sketch, from, to, Width, Gilt);
        }

        /// <summary>A band of one colour along a line, as wide as asked, its ends square.</summary>
        private static void AddBand(MeshSketch sketch, Vector2 from, Vector2 to, float width, Color colour)
        {
            Vector2 along = to - from;
            Vector2 across = new Vector2(-along.y, along.x).normalized * (width / 2f);
            int first = sketch.AddCorner(from - across, colour);
            int second = sketch.AddCorner(to - across, colour);
            int third = sketch.AddCorner(to + across, colour);
            int fourth = sketch.AddCorner(from + across, colour);
            sketch.AddQuad(first, second, third, fourth);
        }
    }
}
