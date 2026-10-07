using System;
using UnityEngine;

namespace Puzzle.Client.Painting
{
    /// <summary>Colours as the design writes them.</summary>
    public static class Tint
    {
        /// <summary>A colour from its web notation: <c>Tint.Of(0x17666F)</c> is #17666F.</summary>
        public static Color Of(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        /// <summary>One paint laid over another, as a painter or a browser does it: neither needs to be opaque.</summary>
        public static Color Over(Color top, Color bottom)
        {
            float alpha = top.a + bottom.a * (1f - top.a);
            if (alpha <= 0f)
            {
                return Color.clear;
            }

            Color mixed = (top * top.a + bottom * (bottom.a * (1f - top.a))) / alpha;
            mixed.a = alpha;
            return mixed;
        }
    }

    /// <summary>
    /// A gradient as a stylesheet writes it: colours at places along a line, blended evenly between them. Before the
    /// first place and after the last the colour stays what it is there.
    /// </summary>
    public sealed class ColourRamp
    {
        private readonly (float At, Color Colour)[] _stops;

        /// <exception cref="ArgumentException">No stop is given, or the stops are not in rising order.</exception>
        public ColourRamp(params (float At, Color Colour)[] stops)
        {
            if (stops.Length == 0)
            {
                throw new ArgumentException("A ramp needs a colour.", nameof(stops));
            }

            for (int stop = 1; stop < stops.Length; stop++)
            {
                if (stops[stop].At < stops[stop - 1].At)
                {
                    throw new ArgumentException("The stops of a ramp are given from its start to its end.", nameof(stops));
                }
            }

            _stops = stops;
        }

        public Color At(float place)
        {
            if (place <= _stops[0].At)
            {
                return _stops[0].Colour;
            }

            for (int stop = 1; stop < _stops.Length; stop++)
            {
                if (place <= _stops[stop].At)
                {
                    float span = _stops[stop].At - _stops[stop - 1].At;
                    float along = span > 0f ? (place - _stops[stop - 1].At) / span : 1f;
                    return Color.Lerp(_stops[stop - 1].Colour, _stops[stop].Colour, along);
                }
            }

            return _stops[_stops.Length - 1].Colour;
        }
    }
}
