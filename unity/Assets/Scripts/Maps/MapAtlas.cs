#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using Puzzle.Museum.Places;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>What the world map shows: the whole world, or one era framed by its five paintings.</summary>
    public readonly struct MapView : IEquatable<MapView>
    {
        private MapView(RoomNumber? era)
        {
            Era = era;
        }

        /// <summary>The view of the whole world, from California to Japan.</summary>
        public static MapView World => new MapView(null);

        /// <summary>The era shown; nothing in the view of the whole world.</summary>
        public RoomNumber? Era { get; }

        public bool IsWorld => Era == null;

        public static MapView Of(RoomNumber era) => new MapView(era);

        public bool Equals(MapView other) => Era == other.Era;

        public override bool Equals(object? obj) => obj is MapView other && Equals(other);

        public override int GetHashCode() => Era?.GetHashCode() ?? 0;

        public override string ToString() => Era is RoomNumber era ? $"era {era}" : "world";

        public static bool operator ==(MapView left, MapView right) => left.Equals(right);

        public static bool operator !=(MapView left, MapView right) => !left.Equals(right);
    }

    /// <summary>
    /// The world map's knowledge of the museum for one visit: which work hangs at each place, where its pin stands on
    /// the map and what its card says of the place in the language spoken, how the player stands, and the middle of
    /// each era's places, which the route of the world view runs through.
    /// </summary>
    public sealed class MapAtlas
    {
        private readonly PaintingGallery _paintings;
        private readonly MuseumPlaces _places;
        private readonly PlaceLabels _labels;

        private MapAtlas(PaintingGallery paintings, MuseumPlaces places, PlaceLabels labels, MapStanding standing)
        {
            _paintings = paintings;
            _places = places;
            _labels = labels;
            Standing = standing;
        }

        public MapStanding Standing { get; }

        public MuseumCatalogue Catalogue => _paintings.Catalogue;

        /// <exception cref="PlacesFormatException">The game carries no places, or a painting has none, or the labels lack one.</exception>
        public static MapAtlas Of(PaintingGallery paintings, MuseumVisit visit)
        {
            MuseumPlaces places = PlacesPack.PlacesOf(paintings.Catalogue);
            return new MapAtlas(paintings, places, PlacesPack.LabelsIn(Speech.Language, places), MapStanding.Of(visit));
        }

        public Artwork? ArtworkAt(PaintingNumber painting) => Catalogue.ArtworkAt(painting);

        /// <summary>Where a painting's pin stands on the map; nothing for a place no work is chosen for yet.</summary>
        public MapPoint? PlaceOf(PaintingNumber painting) => ArtworkAt(painting) is Artwork artwork ? _places.Of(artwork).OnTheMap : (MapPoint?)null;

        /// <summary>What a painting's card says of its place.</summary>
        public PlaceWords WordsOf(Artwork artwork) => _labels.WordsOf(_places.Of(artwork));

        /// <summary>The places of an era's paintings that have one, in the order they are restored.</summary>
        public IReadOnlyList<(PaintingNumber Painting, MapPoint Place)> PlacesIn(RoomNumber era)
        {
            var places = new List<(PaintingNumber, MapPoint)>();
            foreach (PaintingNumber painting in era.Paintings)
            {
                if (PlaceOf(painting) is MapPoint place)
                {
                    places.Add((painting, place));
                }
            }

            return places;
        }

        /// <summary>The middle of an era's places; nothing for an era without a painting yet.</summary>
        public MapPoint? CentreOf(RoomNumber era)
        {
            IReadOnlyList<(PaintingNumber Painting, MapPoint Place)> places = PlacesIn(era);
            if (places.Count == 0)
            {
                return null;
            }

            float x = 0f, y = 0f;
            foreach ((_, MapPoint place) in places)
            {
                x += place.X;
                y += place.Y;
            }

            return new MapPoint(x / places.Count, y / places.Count);
        }

        /// <summary>The picture of a painting and its true proportions; nothing for a place no work is chosen for.</summary>
        public (Texture2D Picture, float Aspect)? PictureOf(PaintingNumber painting)
        {
            PaintingLook look = _paintings.LookOf(painting);
            return look.Picture is Texture2D picture ? (picture, look.Aspect) : ((Texture2D, float)?)null;
        }
    }
}
