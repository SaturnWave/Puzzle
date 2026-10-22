#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Museum.Places;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// Where the world map lies on its sheet at a moment: a point of the map, in its units, is drawn at
    /// <see cref="OffsetX"/> + <see cref="Scale"/> × x and <see cref="OffsetY"/> + <see cref="Scale"/> × y points from
    /// the sheet's top left corner, y downwards, as the design's map is moved and scaled (MapWorldTR). No Unity dependency.
    /// </summary>
    public readonly struct MapFrame : IEquatable<MapFrame>
    {
        /// <exception cref="ArgumentOutOfRangeException">The scale is not more than nothing.</exception>
        public MapFrame(float offsetX, float offsetY, float scale)
        {
            if (!(scale > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "A map is drawn at a scale of more than nothing.");
            }

            OffsetX = offsetX;
            OffsetY = offsetY;
            Scale = scale;
        }

        public float OffsetX { get; }

        public float OffsetY { get; }

        /// <summary>How many of the sheet's points a unit of the map is drawn across.</summary>
        public float Scale { get; }

        /// <summary>The frame that has a point of the map in the middle of the part of the sheet the camera frames.</summary>
        public static MapFrame Centred(float centreX, float centreY, float scale, float mapWidth, float mapHeight) =>
            new MapFrame(mapWidth / 2f - scale * centreX, mapHeight / 2f - scale * centreY, scale);

        /// <summary>
        /// A frame between two, a share of the way: offsets and scale each go evenly, as a style's transition takes a
        /// translation and a scale from one to the other, so a glide that zooms in swings a little as it closes in.
        /// </summary>
        public static MapFrame Between(MapFrame from, MapFrame to, float share) => new MapFrame(
            from.OffsetX + (to.OffsetX - from.OffsetX) * share,
            from.OffsetY + (to.OffsetY - from.OffsetY) * share,
            from.Scale + (to.Scale - from.Scale) * share);

        /// <summary>Where a point of the map lies on the sheet, in the sheet's points from its top left corner, y downwards.</summary>
        public (float X, float Y) SheetOf(MapPoint point) => (OffsetX + Scale * point.X, OffsetY + Scale * point.Y);

        public bool Equals(MapFrame other) => OffsetX.Equals(other.OffsetX) && OffsetY.Equals(other.OffsetY) && Scale.Equals(other.Scale);

        public override bool Equals(object? obj) => obj is MapFrame other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(OffsetX, OffsetY, Scale);

        public override string ToString() => FormattableString.Invariant($"({OffsetX:0.##}, {OffsetY:0.##}) x{Scale:0.###}");

        public static bool operator ==(MapFrame left, MapFrame right) => left.Equals(right);

        public static bool operator !=(MapFrame left, MapFrame right) => !left.Equals(right);
    }

    /// <summary>
    /// What the world map's camera frames (MapWorldTR): the whole world from California to Japan, where the museum's
    /// paintings are, or the five paintings of one era, close enough that its pins stand apart but never closer than
    /// <see cref="MostZoom"/> times the world's scale allows. The rules are the design canvas' own (logic.js), for a
    /// map of any size. No Unity dependency.
    /// </summary>
    public static class MapCamera
    {
        /// <summary>An era is never drawn larger than this many points a map unit.</summary>
        public const float MostZoom = 9f;

        /// <summary>An era's places are framed as though they spread at least this far, in map units, so a single town is not drawn at the largest zoom for nothing.</summary>
        public const float LeastSpanAcross = 22f;
        public const float LeastSpanUpright = 16f;

        /// <summary>What an era's frame keeps clear around its places, in points: room for the pins at the sides, above them and for the card under them.</summary>
        private const float RoomAcross = 120f;
        private const float RoomUpright = 170f;

        /// <summary>An era's places stand this many points over the middle: the pins stand over their places.</summary>
        private const float PlacesLift = 14f;

        /// <summary>The world as the map frames it: from California to Japan across, from 74 degrees north to 44 south upright, drawn a little low.</summary>
        private static readonly MapPoint West = NaturalEarthProjection.Project(-127, 40);
        private static readonly MapPoint East = NaturalEarthProjection.Project(147, 40);
        private static readonly MapPoint North = NaturalEarthProjection.Project(0, 74);
        private static readonly MapPoint South = NaturalEarthProjection.Project(0, -44);
        private const float WorldMarginAcross = 10f;
        private const float WorldMarginUpright = 20f;
        private const float WorldLowered = 6f;

        /// <summary>The least room an era's frame has for its places, in points, on a screen too low for the rest.</summary>
        private const float LeastRoomForPlaces = 40f;

        /// <param name="mapWidth">How wide the map is, in points.</param>
        /// <param name="mapHeight">How high the part of the sheet the camera frames is, in points.</param>
        public static MapFrame OfTheWorld(float mapWidth, float mapHeight)
        {
            float scale = Math.Min((mapWidth - WorldMarginAcross) / (East.X - West.X), (mapHeight - WorldMarginUpright) / (South.Y - North.Y));
            return MapFrame.Centred((West.X + East.X) / 2f, (North.Y + South.Y) / 2f + WorldLowered, scale, mapWidth, mapHeight);
        }

        /// <summary>The frame of an era: its places in the middle, as close as the room around them allows.</summary>
        /// <exception cref="ArgumentException">The era has no place.</exception>
        public static MapFrame OfAnEra(IReadOnlyList<MapPoint> places, float mapWidth, float mapHeight)
        {
            if (places.Count == 0)
            {
                throw new ArgumentException("An era without a place has nothing for the camera to frame.", nameof(places));
            }

            float left = float.MaxValue, right = float.MinValue, top = float.MaxValue, bottom = float.MinValue;
            foreach (MapPoint place in places)
            {
                left = Math.Min(left, place.X);
                right = Math.Max(right, place.X);
                top = Math.Min(top, place.Y);
                bottom = Math.Max(bottom, place.Y);
            }

            float spanAcross = Math.Max(right - left, LeastSpanAcross);
            float spanUpright = Math.Max(bottom - top, LeastSpanUpright);
            float scale = Math.Min(
                Math.Min(Math.Max(mapWidth - RoomAcross, LeastRoomForPlaces) / spanAcross, Math.Max(mapHeight - RoomUpright, LeastRoomForPlaces) / spanUpright),
                MostZoom);
            return MapFrame.Centred((left + right) / 2f, (top + bottom) / 2f - PlacesLift / scale, scale, mapWidth, mapHeight);
        }
    }

    /// <summary>
    /// An easing as a style sheet's cubic-bezier(x1, y1, x2, y2) gives it: the curve from (0, 0) to (1, 1) with those two
    /// control points, read as the share of the way gone (y) at a share of the time (x). No Unity dependency.
    /// </summary>
    public readonly struct CubicBezier
    {
        private const int Bisections = 24;

        public CubicBezier(float x1, float y1, float x2, float y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        /// <summary>The design's glide of the map: cubic-bezier(.22, .8, .24, 1), quick to leave and long to settle.</summary>
        public static CubicBezier MapGlide => new CubicBezier(0.22f, 0.8f, 0.24f, 1f);

        public float X1 { get; }

        public float Y1 { get; }

        public float X2 { get; }

        public float Y2 { get; }

        /// <summary>The share of the way gone at a share of the time, both from 0 to 1.</summary>
        public float At(float time)
        {
            if (time <= 0f)
            {
                return 0f;
            }

            if (time >= 1f)
            {
                return 1f;
            }

            // x(t) rises with t for control points between 0 and 1: halve the interval until x(t) is the time.
            float low = 0f, high = 1f, t = time;
            for (int step = 0; step < Bisections; step++)
            {
                t = (low + high) / 2f;
                if (Coordinate(t, X1, X2) < time)
                {
                    low = t;
                }
                else
                {
                    high = t;
                }
            }

            return Coordinate(t, Y1, Y2);
        }

        private static float Coordinate(float t, float first, float second)
        {
            float rest = 1f - t;
            return 3f * rest * rest * t * first + 3f * rest * t * t * second + t * t * t;
        }
    }

    /// <summary>
    /// The camera on its way from one frame to another over <see cref="Duration"/> seconds of the page's clock, eased as
    /// the design eases it. A glide that begins while another runs begins where that one has got to. No Unity dependency.
    /// </summary>
    public sealed class CameraGlide
    {
        public CameraGlide(MapFrame from, MapFrame to, float beginsAt, float duration)
        {
            From = from;
            To = to;
            BeginsAt = beginsAt;
            Duration = duration;
        }

        public MapFrame From { get; }

        public MapFrame To { get; }

        public float BeginsAt { get; }

        public float Duration { get; }

        public float EndsAt => BeginsAt + Duration;

        /// <summary>A camera at rest: it stays at its frame.</summary>
        public static CameraGlide StillAt(MapFrame frame) => new CameraGlide(frame, frame, 0f, 0f);

        public bool IsOverAt(float now) => now >= EndsAt;

        public MapFrame At(float now) => Duration <= 0f || now >= EndsAt
            ? To
            : MapFrame.Between(From, To, CubicBezier.MapGlide.At(Math.Max(0f, now - BeginsAt) / Duration));
    }
}
