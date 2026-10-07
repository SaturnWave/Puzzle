using Puzzle.Client.Painting;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>
    /// The tray under the pieces: a square of dark teal per playable cell, every second one a shade darker, and over
    /// it a fine weave like linen. The dark ground is the owner's choice (2026-10-07): the pieces' rich colours stand
    /// out on it, and the web test page's tray has the same two colours. The squares are plain: the frame is drawn
    /// over their outer edge and gives the tray its round corners. Both meshes are in cells: the board's top left
    /// corner is the origin, x grows to the right and a row down is -1.
    /// </summary>
    public static class BoardFloor
    {
        /// <summary>The weave repeats every so many points, whatever the size of a cell.</summary>
        public const float WeavePitch = 5f;

        /// <summary>The weave's picture is one repeat, this many pixels a side: 8 for each of its 5 points.</summary>
        private const int WeavePixels = 40;
        private const int WeavePixelsPerPoint = 8;

        /// <summary>Each thread is one point wide and lies this far from the edge of the repeat.</summary>
        private const float ThreadFrom = 0.7f;
        private const float ThreadUntil = 1.7f;

        private static readonly Color Ground = Tint.Of(0x124652);
        private static readonly Color ShadedGround = Tint.Of(0x0F3E49);

        /// <summary>The threads are faint, and the dark one is darker than the ground: on the dark tray a teal thread would not show.</summary>
        private static readonly Color LightThread = Tint.Of(0xFFFFFF, 0.04f);
        private static readonly Color DarkThread = Tint.Of(0x03161C, 0.13f);

        /// <summary>The tray's paint. The caller owns the mesh and destroys it.</summary>
        public static Mesh BuildPaint(BoardShape shape)
        {
            var sketch = new MeshSketch();
            foreach (Position cell in BoardCells.Of(shape))
            {
                bool isShaded = (cell.X + cell.Y) % 2 == 1;
                sketch.AddRectangle(new Rect(cell.X, -(cell.Y + 1), 1f, 1f), isShaded ? ShadedGround : Ground);
            }

            return sketch.ToMesh("Board floor");
        }

        /// <summary>
        /// The cells again, to be drawn with the weave's picture over the paint: the picture's place in a cell is the
        /// cell's own place on the board, so the weave runs on from cell to cell. The caller owns the mesh.
        /// </summary>
        public static Mesh BuildWeaveCover(BoardShape shape)
        {
            var sketch = new MeshSketch();
            foreach (Position cell in BoardCells.Of(shape))
            {
                var square = new Rect(cell.X, -(cell.Y + 1), 1f, 1f);
                sketch.AddRectangle(square, Color.white, Color.white, inTexture: square);
            }

            return sketch.ToMesh("Board floor weave");
        }

        /// <summary>
        /// One repeat of the weave: a light thread across and a darker one down, both faint. It is see-through
        /// between the threads and repeats in both directions. The caller owns the texture.
        /// </summary>
        public static Texture2D BuildWeave()
        {
            var pixels = new Color[WeavePixels * WeavePixels];
            for (int row = 0; row < WeavePixels; row++)
            {
                for (int column = 0; column < WeavePixels; column++)
                {
                    Color across = IsOnThread(row) ? LightThread : Tint.Of(0xFFFFFF, 0f);
                    pixels[row * WeavePixels + column] = IsOnThread(column) ? Tint.Over(DarkThread, across) : across;
                }
            }

            var weave = new Texture2D(WeavePixels, WeavePixels, TextureFormat.RGBA32, mipChain: true)
            {
                name = "Board floor weave",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };
            weave.SetPixels(pixels);
            weave.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return weave;
        }

        private static bool IsOnThread(int pixel)
        {
            float point = (pixel + 0.5f) / WeavePixelsPerPoint;
            return point >= ThreadFrom && point < ThreadUntil;
        }
    }
}
