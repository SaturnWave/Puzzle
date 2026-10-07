#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// The pictures of what special pieces throw over the board, painted in code when a stage opens: light, sparks,
    /// and the violet ink of the painter's tools (a brush's stroke for a rocket, a blot for a bomb), in the colours
    /// and proportions of the web test page's effects (gallery.css, .fx). They are small and few, so they are painted
    /// into one picture at run time, and no file has to be exported, imported and packed for them. Every sprite is one unit wide,
    /// which on the board is one cell, unless it says otherwise.
    /// </summary>
    public sealed class EffectArt : IDisposable
    {
        private const int GlowPixels = 64;
        private const int DotPixels = 32;
        private const int RingPixels = 128;
        private const int StreakPixelsLong = 64;
        private const int StreakPixelsWide = 16;

        /// <summary>
        /// A stroke is painted in two parts, one cell high: a round end of half a cell and a middle of one cell.
        /// Each is painted a few spare columns longer than the sprite that is cut from it.
        /// </summary>
        private const int StrokePixelsHigh = 64;
        private const int StrokeEndPixels = 32;
        private const int StrokeMiddlePixels = 64;
        private const int StrokeSpare = 2;

        private const int SplatPixels = 256;

        /// <summary>The ring around a picked piece is painted with a quarter of a cell around the cell: the ring reaches a twentieth past the cell, its glow the rest.</summary>
        private const int MarkPixelsPerCell = 128;
        private const float MarkCells = 1.5f;

        private static readonly Color DeepInk = Tint.Of(0x2A1D5E);
        private static readonly Color Ink = Tint.Of(0x4A3792);
        private static readonly Color WetInk = Tint.Of(0xEFEAFF);

        /// <summary>A brush's stroke across its width, as the web page's gradient has it: dark edges, a wet light middle.</summary>
        private static readonly ColourRamp StrokeAcross = new ColourRamp(
            (0.17f, Tint.Of(0x2A1D5E, 0f)),
            (0.27f, DeepInk),
            (0.34f, Ink),
            (0.44f, Ink),
            (0.50f, WetInk),
            (0.56f, Ink),
            (0.66f, Ink),
            (0.73f, DeepInk),
            (0.83f, Tint.Of(0x2A1D5E, 0f)));

        /// <summary>The light in the middle of a blot, from its centre to its rim.</summary>
        private static readonly ColourRamp BlotLight = new ColourRamp(
            (0f, WetInk),
            (0.45f, Tint.Of(0x8E7BD6, 0.9f)),
            (1f, Tint.Of(0x4A3792, 0f)));

        /// <summary>
        /// The discs a blot is made of, as the web page places them: the place of each in hundredths of the picture
        /// (as a stylesheet places a background) and its size in hundredths. First the body, then drops, then lobes.
        /// </summary>
        private static readonly (float X, float Y, float Size)[] BlotDiscs =
        {
            (50f, 50f, 60f),
            (74.8f, 10.4f, 8f), (98.9f, 39.6f, 10f), (90.1f, 75.1f, 7f), (63.6f, 97.5f, 9f),
            (28.3f, 90.8f, 7f), (1.4f, 65.8f, 10f), (7.8f, 27.5f, 8f), (39.7f, 1.6f, 9f),
            (56.3f, 14.1f, 26f), (79.3f, 27.1f, 22f), (87.1f, 55.2f, 28f), (73.4f, 79.9f, 21f),
            (46.9f, 85.9f, 25f), (21.1f, 74.2f, 23f), (13.2f, 46.8f, 27f), (26.9f, 20.4f, 20f),
        };

        private readonly Texture2D _sheet;
        private readonly List<Sprite> _sprites = new List<Sprite>();

        private EffectArt()
        {
            var sheet = new Sheet();
            Rect glow = sheet.Paint(GlowPixels, GlowPixels, GlowAt);
            Rect dot = sheet.Paint(DotPixels, DotPixels, DotAt);
            Rect ring = sheet.Paint(RingPixels, RingPixels, RingAt);
            Rect streak = sheet.Paint(StreakPixelsLong, StreakPixelsWide, StreakAt);
            Rect strokeEnd = sheet.Paint(StrokeEndPixels + StrokeSpare, StrokePixelsHigh, StrokeEndAt);
            Rect strokeMiddle = sheet.Paint(StrokeMiddlePixels + 2 * StrokeSpare, StrokePixelsHigh, at => StrokeAcross.At(at.y));
            Rect splat = sheet.Paint(SplatPixels, SplatPixels, BlotAt);
            Rect mark = sheet.Paint((int)(MarkCells * MarkPixelsPerCell), (int)(MarkCells * MarkPixelsPerCell), MarkAt);
            _sheet = sheet.ToTexture();

            var middle = new Vector2(0.5f, 0.5f);
            Glow = Cut("Glow", glow, middle, GlowPixels);
            Dot = Cut("Dot", dot, middle, DotPixels);
            Ring = Cut("Ring", ring, middle, RingPixels);
            Streak = Cut("Streak", streak, middle, StreakPixelsLong);
            Splat = Cut("Ink blot", splat, middle, SplatPixels);
            Mark = Cut("Picked mark", mark, middle, MarkPixelsPerCell);

            // The spare columns are painted but left out of the sprites: where an end meets the middle, the
            // smoothing of the picture then finds more of the same stroke beside it, and no seam shows.
            var endWithoutSpare = new Rect(strokeEnd.x, strokeEnd.y, StrokeEndPixels, StrokePixelsHigh);
            var middleWithoutSpare = new Rect(strokeMiddle.x + StrokeSpare, strokeMiddle.y, StrokeMiddlePixels, StrokePixelsHigh);
            StrokeEnd = Cut("Ink stroke end", endWithoutSpare, new Vector2(1f, 0.5f), StrokePixelsHigh);
            StrokeMiddle = Cut("Ink stroke middle", middleWithoutSpare, middle, StrokePixelsHigh);
        }

        /// <summary>Soft white light that fades from its middle outwards.</summary>
        public Sprite Glow { get; }

        /// <summary>A white disc with a crisp edge: a spark, a chip, a drop.</summary>
        public Sprite Dot { get; }

        /// <summary>A soft white ring: the wave of a blast.</summary>
        public Sprite Ring { get; }

        /// <summary>A white ray, one unit long and a quarter wide, that thins out to both ends.</summary>
        public Sprite Streak { get; }

        /// <summary>
        /// The round end of a brush's stroke of ink, one cell high and half a cell long. Its pivot is the middle of
        /// its straight edge, where the stroke goes on; turned half round, it is the stroke's other end.
        /// </summary>
        public Sprite StrokeEnd { get; }

        /// <summary>A piece of a stroke between its ends, one cell high and one cell long, to be stretched to any length.</summary>
        public Sprite StrokeMiddle { get; }

        /// <summary>A blot of ink with lobes and drops around it, one unit across.</summary>
        public Sprite Splat { get; }

        /// <summary>The gilt ring full of light under a picked piece; one and a half cells across, the ring itself on the cell's edge.</summary>
        public Sprite Mark { get; }

        public static EffectArt Paint() => new EffectArt();

        public void Dispose()
        {
            foreach (Sprite sprite in _sprites)
            {
                SceneObjects.Destroy(sprite);
            }

            SceneObjects.Destroy(_sheet);
        }

        private Sprite Cut(string name, Rect from, Vector2 pivot, float pixelsPerUnit)
        {
            Sprite sprite = Sprite.Create(_sheet, from, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            _sprites.Add(sprite);
            return sprite;
        }

        /// <summary>
        /// The one picture all effect sprites are cut from, while it is painted: each painting takes the next free
        /// place in a row, with clear pixels between neighbours. One picture for all of them means that effect
        /// sprites which follow each other in the drawing order are drawn in one go.
        /// </summary>
        private sealed class Sheet
        {
            private const int Side = 512;

            /// <summary>Clear pixels around every painting, so the smoothing of one never reaches into the next.</summary>
            private const int Gap = 2;

            private readonly Color[] _pixels = new Color[Side * Side];
            private int _left = Gap;
            private int _top = Gap;
            private int _rowHeight;

            public Sheet()
            {
                // Clear, but white: smoothing at a painting's edge then fades to nothing without darkening.
                Array.Fill(_pixels, new Color(1f, 1f, 1f, 0f));
            }

            /// <summary>
            /// Paints a picture pixel by pixel and says where on the sheet it lies. The painter gets a pixel's middle
            /// with (0, 0) at the picture's top left corner and (1, 1) at its bottom right one.
            /// </summary>
            /// <exception cref="InvalidOperationException">The sheet is full.</exception>
            public Rect Paint(int width, int height, Func<Vector2, Color> paint)
            {
                if (_left + width + Gap > Side)
                {
                    _left = Gap;
                    _top += _rowHeight + Gap;
                    _rowHeight = 0;
                }

                if (_top + height + Gap > Side || width + 2 * Gap > Side)
                {
                    throw new InvalidOperationException($"The effects' picture of {Side} by {Side} pixels has no room for {width} by {height} more.");
                }

                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        // A texture's first row is its lowest.
                        int textureRow = Side - 1 - (_top + row);
                        _pixels[textureRow * Side + _left + column] = paint(new Vector2((column + 0.5f) / width, (row + 0.5f) / height));
                    }
                }

                var place = new Rect(_left, Side - _top - height, width, height);
                _left += width + Gap;
                _rowHeight = Math.Max(_rowHeight, height);
                return place;
            }

            /// <summary>The caller owns the texture and destroys it.</summary>
            public Texture2D ToTexture()
            {
                var texture = new Texture2D(Side, Side, TextureFormat.RGBA32, mipChain: false)
                {
                    name = "Effects",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                texture.SetPixels(_pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                return texture;
            }
        }


        /// <summary>How far a place is from the picture's middle: 0 there, 1 at the middle of an edge.</summary>
        private static float FromMiddle(Vector2 at) => 2f * (at - new Vector2(0.5f, 0.5f)).magnitude;

        /// <summary>1 inside an edge, 0 outside it, and a soft step of the given width across it.</summary>
        private static float Inside(float distanceToEdge, float softness) => Mathf.Clamp01(0.5f + distanceToEdge / softness);

        private static Color GlowAt(Vector2 at)
        {
            float light = Mathf.Clamp01(1f - FromMiddle(at));
            return new Color(1f, 1f, 1f, light * light);
        }

        private static Color DotAt(Vector2 at) => new Color(1f, 1f, 1f, Inside(0.86f - FromMiddle(at), 0.12f));

        private static Color RingAt(Vector2 at)
        {
            const float Radius = 0.78f;
            const float HalfWidth = 0.1f;
            float inBand = Mathf.Clamp01(1f - Mathf.Abs(FromMiddle(at) - Radius) / HalfWidth);
            return new Color(1f, 1f, 1f, inBand * inBand * (3f - 2f * inBand));
        }

        private static Color StreakAt(Vector2 at)
        {
            float across = Mathf.Clamp01(1f - Mathf.Abs(at.y - 0.5f) * 2f);
            float along = Mathf.Sin(Mathf.PI * at.x);
            return new Color(1f, 1f, 1f, across * across * Mathf.Sqrt(along));
        }
        /// <summary>
        /// The left end of a stroke: the gradient across the stroke, squeezed into a half circle. The spare columns
        /// at its right, where the stroke goes on, are the plain gradient.
        /// </summary>
        private static Color StrokeEndAt(Vector2 at)
        {
            float intoTheEnd = 1f - at.x * (StrokeEndPixels + StrokeSpare) / StrokeEndPixels;
            if (intoTheEnd <= 0f)
            {
                return StrokeAcross.At(at.y);
            }

            float widthLeft = Mathf.Sqrt(1f - intoTheEnd * intoTheEnd);
            float across = 0.5f + (at.y - 0.5f) / Mathf.Max(widthLeft, 0.0001f);
            return across < 0f || across > 1f ? Tint.Of(0x2A1D5E, 0f) : StrokeAcross.At(across);
        }

        private static Color BlotAt(Vector2 at)
        {
            float pixel = 1f / SplatPixels;
            float ink = 0f;
            foreach ((float x, float y, float size) in BlotDiscs)
            {
                // A stylesheet places a background so that the same hundredth of the picture and of its box meet.
                float radius = size / 200f;
                var middle = new Vector2(x / 100f * (1f - 2f * radius) + radius, y / 100f * (1f - 2f * radius) + radius);
                ink = Mathf.Max(ink, Inside(radius * 0.95f - (at - middle).magnitude, 2f * pixel));
            }

            Color blot = Ink;
            blot.a = ink;
            const float LightRadius = 0.17f;
            return Tint.Over(BlotLight.At((at - new Vector2(0.5f, 0.5f)).magnitude / LightRadius), blot);
        }

        /// <summary>
        /// The picked piece's mark, from the outside in: a glow, a dark line, a gilt ring, a pale line, and light
        /// that fills the cell. The shape is the web test page's: a square with round corners a little inside the
        /// cell's edges, with the pale line inside that square and the gilt and the dark line around it. So the gilt
        /// lies on the cell's own edge and the dark line just past it, in the gaps between the pieces, where the
        /// largest piece (the square gem, grown as a picked piece is) does not cover them.
        /// </summary>
        private static Color MarkAt(Vector2 at)
        {
            const float HalfSide = 0.44f;
            const float Corner = 0.23f;
            const float PaleLine = 0.065f;
            const float GiltLine = 0.065f;
            const float DarkLine = 0.045f;
            const float GlowReach = 0.2f;
            float pixel = 1f / MarkPixelsPerCell;

            // In cells from the cell's middle; then how far outside the round square (negative inside it).
            Vector2 fromMiddle = (at - new Vector2(0.5f, 0.5f)) * MarkCells;
            var beyondCorner = new Vector2(Mathf.Abs(fromMiddle.x) - (HalfSide - Corner), Mathf.Abs(fromMiddle.y) - (HalfSide - Corner));
            float outside = Vector2.Max(beyondCorner, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(beyondCorner.x, beyondCorner.y), 0f) - Corner;

            float pastTheRing = outside - GiltLine - DarkLine;
            float glow = Mathf.Clamp01(1f - pastTheRing / GlowReach);
            Color paint = Tint.Of(0xFFF4C4, pastTheRing > 0f ? 0.75f * glow * glow : 0f);
            paint = Tint.Over(Tint.Of(0x5E3305, Inside(-pastTheRing, pixel)), paint);
            paint = Tint.Over(Tint.Of(0xF6B93C, Inside(GiltLine - outside, pixel)), paint);
            paint = Tint.Over(Tint.Of(0xFFF6CC, Inside(-outside, pixel)), paint);
            float fill = Inside(-outside - PaleLine, pixel);
            return Color.Lerp(paint, Tint.Of(0xFFFBE6, 0.5f), fill);
        }
    }
}
