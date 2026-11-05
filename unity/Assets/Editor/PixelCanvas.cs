using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// A picture painted in memory, without a camera: shapes are given as polygons and come out with smooth edges.
    /// It is for the Editor's study pictures of things that have no screen yet (the jigsaw cut); what the game shows
    /// is drawn by the game's camera. Points are counted in pixels from the picture's top left corner, y downwards.
    /// </summary>
    public sealed class PixelCanvas
    {
        /// <summary>How many lines across a pixel row are tested for a shape's edge: enough for a smooth slanted edge.</summary>
        private const int LinesPerRow = 4;

        private readonly Color[] _pixels;

        public PixelCanvas(int width, int height, Color ground)
        {
            Width = width;
            Height = height;
            _pixels = new Color[width * height];
            Array.Fill(_pixels, ground);
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Fills a closed shape. <paramref name="paint"/> gives the colour for a pixel's middle; its alpha lets the ground show through.</summary>
        public void Fill(IReadOnlyList<Vector2> polygon, Func<Vector2, Color> paint)
        {
            (int top, int bottom) = RowsOf(polygon);
            var cover = new float[Width];
            var crossings = new List<float>();
            for (int row = top; row <= bottom; row++)
            {
                Array.Clear(cover, 0, Width);
                for (int line = 0; line < LinesPerRow; line++)
                {
                    FindCrossings(polygon, row + (line + 0.5f) / LinesPerRow, crossings);
                    for (int pair = 0; pair + 1 < crossings.Count; pair += 2)
                    {
                        AddSpan(cover, crossings[pair], crossings[pair + 1], 1f / LinesPerRow);
                    }
                }

                for (int column = 0; column < Width; column++)
                {
                    if (cover[column] > 0f)
                    {
                        Blend(column, row, paint(new Vector2(column + 0.5f, row + 0.5f)), cover[column]);
                    }
                }
            }
        }

        /// <summary>Draws a closed shape's outline as a line of the given width, centred on the outline.</summary>
        public void Stroke(IReadOnlyList<Vector2> polygon, float width, Color colour)
        {
            // The line's cover is gathered for the whole outline first and laid down once: where two of its stretches
            // meet, the joint is not painted twice.
            var cover = new Dictionary<int, float>();
            float reach = width / 2f + 0.5f;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 from = polygon[index];
                Vector2 to = polygon[(index + 1) % polygon.Count];
                int left = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - reach));
                int right = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + reach));
                int top = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - reach));
                int bottom = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + reach));
                for (int row = top; row <= bottom; row++)
                {
                    for (int column = left; column <= right; column++)
                    {
                        float share = Mathf.Clamp01(reach - DistanceToStretch(new Vector2(column + 0.5f, row + 0.5f), from, to));
                        int pixel = row * Width + column;
                        if (share > 0f && (!cover.TryGetValue(pixel, out float sofar) || share > sofar))
                        {
                            cover[pixel] = share;
                        }
                    }
                }
            }

            foreach (KeyValuePair<int, float> covered in cover)
            {
                Blend(covered.Key % Width, covered.Key / Width, colour, covered.Value);
            }
        }

        /// <summary>The canvas as a texture. The caller owns the picture and destroys it.</summary>
        public Texture2D ToPicture()
        {
            var picture = new Texture2D(Width, Height, TextureFormat.RGB24, mipChain: false);
            var rowsFromTheFoot = new Color[_pixels.Length]; // a texture counts its rows from the foot up
            for (int row = 0; row < Height; row++)
            {
                Array.Copy(_pixels, row * Width, rowsFromTheFoot, (Height - 1 - row) * Width, Width);
            }

            picture.SetPixels(rowsFromTheFoot);
            picture.Apply(updateMipmaps: false);
            return picture;
        }

        private (int Top, int Bottom) RowsOf(IReadOnlyList<Vector2> polygon)
        {
            float top = float.MaxValue;
            float bottom = float.MinValue;
            foreach (Vector2 point in polygon)
            {
                top = Mathf.Min(top, point.y);
                bottom = Mathf.Max(bottom, point.y);
            }

            return (Mathf.Max(0, Mathf.FloorToInt(top)), Mathf.Min(Height - 1, Mathf.CeilToInt(bottom)));
        }

        /// <summary>Where a horizontal line crosses the shape's outline, from left to right: the shape lies between the first and second crossing, the third and fourth, and so on.</summary>
        private static void FindCrossings(IReadOnlyList<Vector2> polygon, float y, List<float> crossings)
        {
            crossings.Clear();
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 from = polygon[index];
                Vector2 to = polygon[(index + 1) % polygon.Count];
                if (from.y <= y != to.y <= y)
                {
                    crossings.Add(from.x + (y - from.y) / (to.y - from.y) * (to.x - from.x));
                }
            }

            crossings.Sort();
        }

        /// <summary>Adds a covered stretch of one line to the row: whole pixels get the line's weight, the two end pixels their covered share of it.</summary>
        private void AddSpan(float[] cover, float from, float to, float weight)
        {
            int first = Mathf.Max(0, Mathf.FloorToInt(from));
            int last = Mathf.Min(Width - 1, Mathf.FloorToInt(to));
            for (int column = first; column <= last; column++)
            {
                float covered = Mathf.Min(to, column + 1) - Mathf.Max(from, column);
                cover[column] += weight * Mathf.Clamp01(covered);
            }
        }

        private void Blend(int column, int row, Color colour, float share)
        {
            int pixel = row * Width + column;
            _pixels[pixel] = Color.Lerp(_pixels[pixel], new Color(colour.r, colour.g, colour.b, 1f), share * colour.a);
        }

        private static float DistanceToStretch(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 along = to - from;
            float lengthSquared = along.sqrMagnitude;
            float share = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared) : 0f;
            return Vector2.Distance(point, from + share * along);
        }
    }
}
