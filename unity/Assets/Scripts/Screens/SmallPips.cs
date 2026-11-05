#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// A painting's progress as a row of small pips under a label, as the design canvas draws it on the hall's blue
    /// "Tablo" button (HomeTR): one pip for each piece in place, gilt, and dark for the rest, every pip in a line of
    /// the ground's dark. A painting of more than ten pieces shows ten pips, each a tenth of it, gilt once that
    /// tenth is in place: the button has room for ten. Painted in code, as a mesh. Sizes are the canvas' points
    /// around the middle of the row.
    /// </summary>
    public sealed class SmallPips : IDisposable
    {
        /// <summary>A pip is 7.5 by 6 points with rounded corners, 1.6 from the next, in a line of 1 point (HomeTR).</summary>
        private const float PipWidth = 7.5f;
        private const float PipHeight = 6f;
        private const float Gap = 1.6f;
        private const float Line = 1f;
        private const float Corner = 2f;

        /// <summary>A corner is drawn as a quarter circle of this many straight steps.</summary>
        private const int StepsInACorner = 3;

        /// <summary>The most pips the row shows: a larger painting is shown in tenths.</summary>
        private const int MostPips = 10;

        private static readonly Color GiltTop = Tint.Of(0xFFF4C4);
        private static readonly Color GiltFoot = Tint.Of(0xF6B93C);

        private readonly PaintedShape _shape;
        private readonly Color _ground;
        private readonly Color _hollow;

        /// <param name="ground">The dark of what the row stands on: its pips' line (HomeTR: #0F2670 on the blue button).</param>
        /// <param name="hollow">The face of a pip whose piece is not in place (HomeTR: the blue's dark at 55 %).</param>
        public SmallPips(string name, Transform parent, ArtCatalogue art, Color ground, Color hollow, DrawOrder order)
        {
            _ground = ground;
            _hollow = hollow;
            _shape = PaintedShape.Create(name, parent, art.PaintMaterial, order);
        }

        /// <summary>The row's place is its middle.</summary>
        public Transform Transform => _shape.Transform;

        /// <summary>How wide a row of so many pieces is drawn, from its first pip's line to its last one's.</summary>
        public static float WidthOf(int pieceCount) => PipsFor(pieceCount) * (PipWidth + Gap) - Gap + 2f * Line;

        /// <summary>Paints the row for a painting of so many pieces, so many of them in place.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The painting has no piece, or more in place than it has.</exception>
        public void Show(int inPlace, int pieceCount)
        {
            if (pieceCount < 1 || inPlace < 0 || inPlace > pieceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(inPlace), inPlace, $"Of {pieceCount} pieces, none to all can be in place.");
            }

            int pips = PipsFor(pieceCount);
            int gilt = pieceCount <= MostPips ? inPlace : inPlace * MostPips / pieceCount;
            var sketch = new MeshSketch();
            float left = -(pips * (PipWidth + Gap) - Gap) / 2f;
            for (int pip = 0; pip < pips; pip++)
            {
                var face = new Rect(left + pip * (PipWidth + Gap), -PipHeight / 2f, PipWidth, PipHeight);
                AddRoundedBox(sketch, new Rect(face.x - Line, face.y - Line, face.width + 2f * Line, face.height + 2f * Line), Corner + Line, _ground, _ground);
                if (pip < gilt)
                {
                    AddRoundedBox(sketch, face, Corner, GiltFoot, GiltTop);
                }
                else
                {
                    AddRoundedBox(sketch, face, Corner, _hollow, _hollow);
                }
            }

            _shape.Show(sketch.ToMesh("Small pips"));
        }

        public void Dispose() => _shape.Dispose();

        private static int PipsFor(int pieceCount) => Math.Min(pieceCount, MostPips);

        /// <summary>A box with rounded corners, each a quarter circle of a few steps, coloured from its lower edge to its upper one.</summary>
        private static void AddRoundedBox(MeshSketch sketch, Rect box, float corner, Color lower, Color upper)
        {
            Color At(float y) => Color.Lerp(lower, upper, (y - box.yMin) / box.height);
            var middles = new[]
            {
                new Vector2(box.xMax - corner, box.yMin + corner), new Vector2(box.xMax - corner, box.yMax - corner),
                new Vector2(box.xMin + corner, box.yMax - corner), new Vector2(box.xMin + corner, box.yMin + corner),
            };
            var outline = new Vector2[middles.Length * (StepsInACorner + 1)];
            for (int side = 0; side < middles.Length; side++)
            {
                // Counterclockwise from the lower right corner, each corner's quarter from where the side before it ends.
                for (int step = 0; step <= StepsInACorner; step++)
                {
                    float angle = (side - 1 + step / (float)StepsInACorner) * Mathf.PI / 2f;
                    outline[side * (StepsInACorner + 1) + step] = middles[side] + corner * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }

            // A fan from the middle, each face wound as the sketch's own rectangles are (clockwise, seen from the camera).
            int middle = sketch.AddCorner(box.center, At(box.center.y));
            int first = sketch.AddCorner(outline[0], At(outline[0].y));
            int previous = first;
            for (int point = 1; point < outline.Length; point++)
            {
                int next = sketch.AddCorner(outline[point], At(outline[point].y));
                sketch.AddTriangle(middle, next, previous);
                previous = next;
            }

            sketch.AddTriangle(middle, first, previous);
        }
    }
}
