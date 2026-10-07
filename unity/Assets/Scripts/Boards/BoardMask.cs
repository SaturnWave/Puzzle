#nullable enable
using System;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>
    /// What cuts pieces off at the edge of the tray: a mask in the shape of the board's cells, a little wider than
    /// they are. A new piece falls in from above its column and an item drops out below it; without the mask both
    /// would be drawn over the frame, the wall and the plaques. Sprites that ask for it (pieces, strokes, splashes)
    /// are only drawn inside the mask; everything else on the stage is drawn as before.
    /// </summary>
    /// <remarks>
    /// Into a gap that pieces fall through (a hole in the board with cells above and below it in its column) the
    /// mask does not reach at all. A piece on its way through such a gap is as wide as the gap, and any margin
    /// there would show its sides beside the gap's frame and its foot on top of it; with none, the piece goes out
    /// of sight at the gap's upper edge and comes back at its lower edge. The price is small: the gems lie inside
    /// their cells, and only a bomb, a rocket or a seeker at rest beside such a gap is cut a thirtieth of a cell
    /// short on that side, where the frame's dark line begins.
    /// </remarks>
    public sealed class BoardMask : IDisposable
    {
        /// <summary>The mask's picture has this many pixels per cell; its edges lie on pixel edges, so they stay straight.</summary>
        private const int PixelsPerCell = 24;

        /// <summary>
        /// The mask reaches this far past the cells, a twelfth of a cell: a piece at rest is drawn a tenth larger
        /// than its cell and must not lose its rim in the board's outermost rows.
        /// </summary>
        private const int MarginPixels = 2;

        private readonly Texture2D _picture;
        private readonly Sprite _shape;
        private readonly SpriteMask _mask;

        private BoardMask(Transform board, BoardShape shape)
        {
            _picture = Paint(shape);
            float margin = (float)MarginPixels / PixelsPerCell;
            var whole = new Rect(0f, 0f, _picture.width, _picture.height);
            _shape = Sprite.Create(_picture, whole, pivot: new Vector2(0f, 1f), PixelsPerCell, extrude: 0, SpriteMeshType.FullRect);
            _shape.name = "Board mask";

            _mask = new GameObject("Mask").AddComponent<SpriteMask>();
            _mask.transform.SetParent(board, worldPositionStays: false);
            _mask.transform.localPosition = new Vector3(-margin, margin, 0f);
            _mask.sprite = _shape;
            _mask.alphaCutoff = 0.5f;
        }

        /// <param name="board">The object whose unit is a cell and whose origin is the board's top left corner.</param>
        public static BoardMask Create(Transform board, BoardShape shape) => new BoardMask(board, shape);

        public void Dispose()
        {
            SceneObjects.DestroyObjectOf(_mask);
            SceneObjects.Destroy(_shape);
            SceneObjects.Destroy(_picture);
        }

        /// <summary>Opaque in the cells and in the margin around them, clear elsewhere and in every gap that pieces fall through.</summary>
        private static Texture2D Paint(BoardShape shape)
        {
            int width = shape.Width * PixelsPerCell + 2 * MarginPixels;
            int height = shape.Height * PixelsPerCell + 2 * MarginPixels;
            var pixels = new Color32[width * height];
            for (int fromTop = 0; fromTop < height; fromTop++)
            {
                for (int fromLeft = 0; fromLeft < width; fromLeft++)
                {
                    bool isInside = IsInsideTheMask(fromLeft - MarginPixels, fromTop - MarginPixels, shape);

                    // A texture's rows are counted from its foot.
                    pixels[(height - 1 - fromTop) * width + fromLeft] = new Color32(255, 255, 255, isInside ? (byte)255 : (byte)0);
                }
            }

            var picture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Board mask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            picture.SetPixels32(pixels);
            picture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return picture;
        }

        /// <param name="pixelX">A pixel's place counted from the left edge of the first column; negative in the margin.</param>
        /// <param name="pixelY">Its place counted from the top edge of the first row.</param>
        private static bool IsInsideTheMask(int pixelX, int pixelY, BoardShape shape)
        {
            var own = new Position(CellOf(pixelX), CellOf(pixelY));
            return shape.IsPlayable(own) || (!IsFallenThrough(own, shape) && IsNearACell(pixelX, pixelY, shape));
        }

        /// <summary>True for a place on the board that is no cell but has a cell above it and a cell below it in its column.</summary>
        private static bool IsFallenThrough(Position gap, BoardShape shape) =>
            shape.Contains(gap) && HasACell(shape, gap.X, 0, gap.Y - 1) && HasACell(shape, gap.X, gap.Y + 1, shape.Height - 1);

        private static bool HasACell(BoardShape shape, int column, int fromRow, int untilRow)
        {
            for (int row = fromRow; row <= untilRow; row++)
            {
                if (shape.IsPlayable(new Position(column, row)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNearACell(int pixelX, int pixelY, BoardShape shape)
        {
            for (int row = CellOf(pixelY - MarginPixels); row <= CellOf(pixelY + MarginPixels); row++)
            {
                for (int column = CellOf(pixelX - MarginPixels); column <= CellOf(pixelX + MarginPixels); column++)
                {
                    if (shape.IsPlayable(new Position(column, row)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CellOf(int pixel) => (int)Math.Floor((double)pixel / PixelsPerCell);
    }
}
