using System.Collections.Generic;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>
    /// The row of beads on the frame's gilt band: small pale pearls, each with a darker one under it as its shade.
    /// All beads are one mesh of little squares that show one picture, which is painted here in code.
    /// </summary>
    public static class FrameBeads
    {
        /// <summary>Beads lie about this far apart, in points; on every stretch of the ring the spacing is evened out to a whole number of beads.</summary>
        private const float Pitch = 4.4f;

        private const float BeadRadius = 0.95f;
        private const float ShadeRadius = 1.03f;

        /// <summary>The picture is this square around the bead's middle, in points: wide enough for the shade beside the bead.</summary>
        private const float PictureHalfSide = 1.9f;
        private const int PictureSize = 64;

        /// <summary>The white spot where the light strikes a bead is this far across from its middle.</summary>
        private const float GlintRadius = 0.29f;

        /// <summary>The shade lies to the lower right of its bead (the page's y grows downwards): the light comes from the upper left.</summary>
        private static readonly Vector2 ShadeOffset = new Vector2(0.4f, 0.72f);
        private static readonly Vector2 GlintOffset = new Vector2(-0.25f, -0.29f);

        private static readonly Color Pearl = Tint.Of(0xFFF6CC);
        private static readonly Color PearlInShade = Tint.Of(0xF3D58A);
        private static readonly Color Shade = Tint.Of(0x9C5C0B, 0.8f);

        /// <summary>The picture of one bead with its shade. The caller owns it.</summary>
        public static Texture2D BuildPicture()
        {
            float pixelsPerPoint = PictureSize / (2f * PictureHalfSide);
            var pixels = new Color[PictureSize * PictureSize];
            for (int row = 0; row < PictureSize; row++)
            {
                for (int column = 0; column < PictureSize; column++)
                {
                    // A texture's first row is its lowest; the page's y grows downwards.
                    var fromBead = new Vector2((column + 0.5f) / pixelsPerPoint - PictureHalfSide, PictureHalfSide - (row + 0.5f) / pixelsPerPoint);
                    pixels[row * PictureSize + column] = BeadAt(fromBead, pixelsPerPoint);
                }
            }

            var picture = new Texture2D(PictureSize, PictureSize, TextureFormat.RGBA32, mipChain: true)
            {
                name = "Frame bead",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            picture.SetPixels(pixels);
            picture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return picture;
        }

        /// <summary>
        /// Adds the beads of one stretch of a ring to a sketch: a straight side, or the turn of a round corner. The
        /// first bead lies on the stretch's beginning and the rest follow evenly; its end is left to the stretch that
        /// begins there. A ring beaded stretch by stretch has a bead on every corner and the same row of beads on
        /// either side of it, where one spacing carried around the whole ring would meet each corner differently.
        /// </summary>
        /// <param name="stretch">A line of page points from one corner of the ring to the next; a line without length adds nothing.</param>
        public static void AddStretch(MeshSketch sketch, IReadOnlyList<Vector2> stretch)
        {
            float length = 0f;
            for (int point = 0; point + 1 < stretch.Count; point++)
            {
                length += Vector2.Distance(stretch[point], stretch[point + 1]);
            }

            if (length <= 0f)
            {
                return;
            }

            int beads = Mathf.Max(1, Mathf.RoundToInt(length / Pitch));
            float evenPitch = length / beads;
            float walked = 0f;
            int placed = 0;
            for (int point = 0; point + 1 < stretch.Count && placed < beads; point++)
            {
                Vector2 from = stretch[point];
                Vector2 to = stretch[point + 1];
                float step = Vector2.Distance(from, to);
                while (placed < beads && placed * evenPitch <= walked + step)
                {
                    float along = step > 0f ? (placed * evenPitch - walked) / step : 0f;
                    AddBead(sketch, Vector2.Lerp(from, to, along));
                    placed++;
                }

                walked += step;
            }
        }

        private static void AddBead(MeshSketch sketch, Vector2 pageMiddle)
        {
            Vector2 middle = BoardPage.ToScene(pageMiddle);
            var square = new Rect(middle.x - PictureHalfSide, middle.y - PictureHalfSide, 2f * PictureHalfSide, 2f * PictureHalfSide);
            sketch.AddRectangle(square, Color.white, Color.white, new Rect(0f, 0f, 1f, 1f));
        }

        /// <summary>The picture at a point, given from the bead's middle in page points; edges are one pixel soft.</summary>
        private static Color BeadAt(Vector2 fromBead, float pixelsPerPoint)
        {
            float Covered(float radius, Vector2 from) => Mathf.Clamp01((radius - from.magnitude) * pixelsPerPoint + 0.5f);

            // The pearl is lightest where the light strikes it and takes a warmer tone towards its far rim.
            float towardsFarRim = Mathf.Clamp01(((fromBead - GlintOffset).magnitude - GlintRadius) / (2f * BeadRadius));
            Color pearl = Color.Lerp(Color.white, Color.Lerp(Pearl, PearlInShade, towardsFarRim), Mathf.Clamp01(towardsFarRim * 4f));
            pearl.a = Covered(BeadRadius, fromBead);
            Color shade = Shade;
            shade.a *= Covered(ShadeRadius, fromBead - ShadeOffset);

            // Where nothing is painted the colour is still the shade's: a filtered edge then fades out, not to black.
            Color painted = Tint.Over(pearl, shade);
            return painted.a > 0f ? painted : new Color(Shade.r, Shade.g, Shade.b, 0f);
        }
    }
}
