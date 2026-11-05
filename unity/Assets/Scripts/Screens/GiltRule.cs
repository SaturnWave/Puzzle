using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The design's gilt rule, as the canvas draws it across its cards and beside the names of their parts (WinTR,
    /// LevelStartTR): a thin gilt line and a cut diamond that is lit from its upper left. Whoever needs a rule
    /// adds its lines and diamonds to a shape of their own; lengths are in points.
    /// </summary>
    public static class GiltRule
    {
        public const float Thickness = 1.4f;

        /// <summary>The canvas lays a faint brown line under the gilt one, this far lower and this thick.</summary>
        private const float ShadeLower = 0.8f;
        private const float ShadeThickness = 1f;

        /// <summary>A hairline between two rows: 1.5 high, at three quarters of the gilt's strength, fading over a seventh of its length at each end.</summary>
        private const float HairlineThickness = 1.5f;
        private const float HairlineStrength = 0.75f;
        private const float HairlineFadesWithin = 0.14f;

        private static readonly Color Gilt = Tint.Of(0xE0A63A);
        private static readonly Color Shade = Tint.Of(0x8A5512, 0.45f);
        private static readonly Color DiamondEdge = Tint.Of(0x5E3305);
        private static readonly Color DiamondLight = Tint.Of(0xFFF0B8);
        private static readonly Color DiamondGilt = Tint.Of(0xF6B93C);
        private static readonly Color DiamondDark = Tint.Of(0xC8841A);

        /// <summary>A level line from a place to another, its middle at a height.</summary>
        /// <param name="isShaded">True for the canvas' line with the faint shade under it; a result card's rule has none.</param>
        public static void AddLine(MeshSketch sketch, float left, float right, float height, bool isShaded)
        {
            if (isShaded)
            {
                sketch.AddRectangle(new Rect(left, height - ShadeLower - ShadeThickness / 2f, right - left, ShadeThickness), Shade);
            }

            sketch.AddRectangle(new Rect(left, height - Thickness / 2f, right - left, Thickness), Gilt);
        }

        /// <summary>
        /// The hairline between two rows of a card (SettingsTR): a gilt line 1.5 high, three parts of four strong,
        /// that fades in over the first seventh of its length and out over the last.
        /// </summary>
        public static void AddHairline(MeshSketch sketch, float left, float right, float height)
        {
            float faded = (right - left) * HairlineFadesWithin;
            float top = height + HairlineThickness / 2f;
            float bottom = height - HairlineThickness / 2f;
            Color clear = new Color(Gilt.r, Gilt.g, Gilt.b, 0f);
            Color strong = new Color(Gilt.r, Gilt.g, Gilt.b, HairlineStrength);
            float[] across = { left, left + faded, right - faded, right };
            Color[] colours = { clear, strong, strong, clear };
            for (int part = 0; part < across.Length - 1; part++)
            {
                int lowerLeft = sketch.AddCorner(new Vector2(across[part], bottom), colours[part]);
                int upperLeft = sketch.AddCorner(new Vector2(across[part], top), colours[part]);
                int upperRight = sketch.AddCorner(new Vector2(across[part + 1], top), colours[part + 1]);
                int lowerRight = sketch.AddCorner(new Vector2(across[part + 1], bottom), colours[part + 1]);
                sketch.AddQuad(lowerLeft, upperLeft, upperRight, lowerRight);
            }
        }

        /// <summary>A diamond around a middle: its lit face so far across, in a dark edge of a given width.</summary>
        public static void AddDiamond(MeshSketch sketch, Vector2 middle, float across, float edge)
        {
            AddSquareOnItsCorner(sketch, middle, across + 2f * edge, DiamondEdge, DiamondEdge, DiamondEdge);
            AddSquareOnItsCorner(sketch, middle, across, DiamondLight, DiamondGilt, DiamondDark);
        }

        /// <summary>A square on its corner, of a given width, whose colour runs from its left corner over its upper and lower ones to its right corner.</summary>
        private static void AddSquareOnItsCorner(MeshSketch sketch, Vector2 middle, float size, Color left, Color upperAndLower, Color right)
        {
            float reach = size / 2f;
            int leftCorner = sketch.AddCorner(middle + new Vector2(-reach, 0f), left);
            int upperCorner = sketch.AddCorner(middle + new Vector2(0f, reach), upperAndLower);
            int rightCorner = sketch.AddCorner(middle + new Vector2(reach, 0f), right);
            int lowerCorner = sketch.AddCorner(middle + new Vector2(0f, -reach), upperAndLower);
            sketch.AddQuad(leftCorner, upperCorner, rightCorner, lowerCorner);
        }
    }
}
