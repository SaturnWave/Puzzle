#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Client.Maps
{
    /// <summary>Where one pin of an era stands on the sheet: its place on the map, and the tip of its frame's pointer.</summary>
    public readonly struct PinPlace
    {
        public PinPlace(int painting, float placeX, float placeY, float tipX, float tipY)
        {
            Painting = painting;
            PlaceX = placeX;
            PlaceY = placeY;
            TipX = tipX;
            TipY = tipY;
        }

        /// <summary>Which of the pins it is, as the caller numbered them.</summary>
        public int Painting { get; }

        /// <summary>The painting's place on the sheet, in points from its top left corner, y downwards.</summary>
        public float PlaceX { get; }

        public float PlaceY { get; }

        /// <summary>Where the pin's pointer ends: the pin's frame stands over it.</summary>
        public float TipX { get; }

        public float TipY { get; }

        /// <summary>True when the pin was moved off its place so far that a thin line leads from its pointer to it.</summary>
        public bool HasALeader => Math.Sqrt((PlaceX - TipX) * (PlaceX - TipX) + (PlaceY - TipY) * (PlaceY - TipY)) > PinRows.LeaderFrom;
    }

    /// <summary>
    /// How an era's pins stand over their places (MapWorldTR, logic.js): each over its own place, a little higher; but
    /// pins whose places lie closer than a pin's width form one row above them, in the order of their places from
    /// west to east, each with a thin line to its exact place. No pin leaves the part of the sheet the camera frames,
    /// none stands under the round buttons at the top right, and pins that would still overlap push each other apart.
    /// No Unity dependency.
    /// </summary>
    public static class PinRows
    {
        /// <summary>A pin's box (MapWorldTR: 46 by 52 points with its pointer), and how far apart pins of a row stand.</summary>
        public const float PinWidth = 46f;
        public const float PinHeight = 52f;
        public const float Gap = 46f;

        /// <summary>A pin moved further than this from its place gets a line to it.</summary>
        public const float LeaderFrom = 5f;

        /// <summary>A pin stands this far over its place; a row of pins this far over the highest of their places.</summary>
        private const float OverItsPlace = 6f;
        private const float RowOverItsPlaces = 16f;

        /// <summary>The highest a pin's tip may stand: its frame and the badge over it stay on the sheet.</summary>
        private const float HighestTip = PinHeight + 30f;
        private const float LowestTipOverTheFoot = 4f;
        private const float SideRoom = 2f;

        /// <summary>The round buttons at the top right: a pin whose top is higher than this keeps clear of their column.</summary>
        public const float ButtonsFoot = 126f;
        public const float ButtonsColumn = 50f;

        private const int PushRounds = 40;

        /// <param name="places">Each pin's place on the sheet, by its number.</param>
        /// <param name="mapWidth">How wide the map is, in points.</param>
        /// <param name="mapHeight">How high the part of the sheet over the strip is.</param>
        public static IReadOnlyList<PinPlace> Of(IReadOnlyList<(int Painting, float X, float Y)> places, float mapWidth, float mapHeight)
        {
            var pins = places.Select(place => new Pin(place.Painting, place.X, place.Y)).ToList();
            foreach (List<Pin> group in GroupsOf(pins))
            {
                if (group.Count == 1)
                {
                    continue;
                }

                float middle = group.Average(pin => pin.PlaceX);
                float row = group.Min(pin => pin.PlaceY) - RowOverItsPlaces;
                for (int k = 0; k < group.Count; k++)
                {
                    group[k].X = middle + (k - (group.Count - 1) / 2f) * Gap;
                    group[k].Y = row;
                }
            }

            pins.ForEach(pin => Settle(pin, mapWidth, mapHeight));
            for (int round = 0; round < PushRounds; round++)
            {
                bool moved = false;
                for (int i = 0; i < pins.Count; i++)
                {
                    for (int j = i + 1; j < pins.Count; j++)
                    {
                        Pin a = pins[i], b = pins[j];
                        if (Math.Abs(a.Y - b.Y) >= PinHeight - 6f || Math.Abs(a.X - b.X) >= Gap)
                        {
                            continue;
                        }

                        float push = (Gap - Math.Abs(a.X - b.X)) / 2f + 0.5f;
                        float sign = a.X <= b.X ? 1f : -1f;
                        a.X -= sign * push;
                        b.X += sign * push;
                        moved = true;
                    }
                }

                pins.ForEach(pin => Settle(pin, mapWidth, mapHeight));
                if (!moved)
                {
                    break;
                }
            }

            return pins.Select(pin => new PinPlace(pin.Painting, pin.PlaceX, pin.PlaceY, pin.X, pin.Y)).ToList();
        }

        /// <summary>The pins whose places lie closer than a pin's width, gathered west to east: a pin joins the first group that has one so near.</summary>
        private static List<List<Pin>> GroupsOf(List<Pin> pins)
        {
            var groups = new List<List<Pin>>();
            foreach (Pin pin in pins.OrderBy(pin => pin.PlaceX))
            {
                List<Pin>? near = groups.FirstOrDefault(group => group.Any(other => Distance(other, pin) < Gap));
                if (near != null)
                {
                    near.Add(pin);
                }
                else
                {
                    groups.Add(new List<Pin> { pin });
                }
            }

            return groups;
        }

        private static float Distance(Pin a, Pin b) =>
            (float)Math.Sqrt((a.PlaceX - b.PlaceX) * (a.PlaceX - b.PlaceX) + (a.PlaceY - b.PlaceY) * (a.PlaceY - b.PlaceY));

        /// <summary>Keeps a pin on the sheet, and out of the buttons' column near the top.</summary>
        private static void Settle(Pin pin, float mapWidth, float mapHeight)
        {
            pin.Y = Math.Min(Math.Max(pin.Y, HighestTip), mapHeight - LowestTipOverTheFoot);
            float right = pin.Y - PinHeight < ButtonsFoot ? mapWidth - PinWidth / 2f - ButtonsColumn : mapWidth - PinWidth / 2f - SideRoom;
            pin.X = Math.Min(Math.Max(pin.X, PinWidth / 2f + SideRoom), right);
        }

        private sealed class Pin
        {
            public Pin(int painting, float placeX, float placeY)
            {
                Painting = painting;
                PlaceX = placeX;
                PlaceY = placeY;
                X = placeX;
                Y = placeY - OverItsPlace;
            }

            public int Painting { get; }

            public float PlaceX { get; }

            public float PlaceY { get; }

            public float X { get; set; }

            public float Y { get; set; }
        }
    }
}
