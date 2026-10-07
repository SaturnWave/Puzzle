using Puzzle.Client.Painting;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>
    /// The tray under the pieces: a square of light teal per playable cell, every second one a shade darker, lighter
    /// at the top of the board than at its foot, and over it a fine weave like linen. The squares are plain: the
    /// frame is drawn over their outer edge and gives the tray its round corners. Both meshes are in cells: the
    /// board's top left corner is the origin, x grows to the right and a row down is -1.
    /// </summary>
    public static class BoardFloor
    {
        /// <summary>The weave repeats every so many points, whatever the size of a cell.</summary>
        public const float WeavePitch = 5f;

        private const float CheckerStrength = 0.085f;

        /// <summary>The weave's picture is one repeat, this many pixels a side: 8 for each of its 5 points.</summary>
        private const int WeavePixels = 40;
        private const int WeavePixelsPerPoint = 8;

        /// <summary>Each thread is one point wide and lies this far from the edge of the repeat.</summary>
        private const float ThreadFrom = 0.7f;
        private const float ThreadUntil = 1.7f;

        private static readonly Color TrayTop = Tint.Of(0xA3D6CF);
        private static readonly Color TrayFoot = Tint.Of(0x8CC4BD);
        private static readonly Color CheckerShade = Tint.Of(0x0D545A);
        private static readonly Color LightThread = Tint.Of(0xFFFFFF, 0.13f);
        private static readonly Color DarkThread = Tint.Of(0x0B4C52, 0.07f);

        /// <summary>The tray's paint. The caller owns the mesh and destroys it.</summary>
        public static Mesh BuildPaint(BoardShape shape)
        {
            var sketch = new MeshSketch();
            foreach (Position cell in BoardCells.Of(shape))
            {
                bool isShaded = (cell.X + cell.Y) % 2 == 1;
                sketch.AddRectangle(
                    new Rect(cell.X, -(cell.Y + 1), 1f, 1f),
                    PaintAt((cell.Y + 1f) / shape.Height, isShaded),
                    PaintAt((float)cell.Y / shape.Height, isShaded),
                    new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));
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

        private static Color32 PaintAt(float downTheBoard, bool isShaded)
        {
            Color tray = Color.Lerp(TrayTop, TrayFoot, downTheBoard);
            return isShaded ? Color.Lerp(tray, CheckerShade, CheckerStrength) : tray;
        }
    }
}
