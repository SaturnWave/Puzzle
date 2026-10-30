using System;
using System.Collections.Generic;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Museum.Places
{
    /// <summary>The places file is malformed or does not fit the catalogue; the message names the painting or the key and the problem.</summary>
    public sealed class PlacesFormatException : Exception
    {
        public PlacesFormatException(string message)
            : base(message)
        {
        }

        public PlacesFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>Where every painting of the catalogue stands on the world map: one place each, none for a painting the catalogue does not have.</summary>
    public sealed class MuseumPlaces
    {
        private readonly Dictionary<ArtworkId, PaintingPlace> _places;

        internal MuseumPlaces(Dictionary<ArtworkId, PaintingPlace> places)
        {
            _places = places;
        }

        public IEnumerable<PaintingPlace> All => _places.Values;

        /// <summary>The place of a painting of the catalogue the places were read against.</summary>
        /// <exception cref="ArgumentException">The painting is not one of that catalogue: the places were read for another.</exception>
        public PaintingPlace Of(Artwork artwork) => _places.TryGetValue(artwork.Id, out PaintingPlace? place)
            ? place
            : throw new ArgumentException($"The places were read for another catalogue: they have none for {artwork.Id}.", nameof(artwork));

        /// <summary>Every label key the places name, each once: what the source language's table must hold.</summary>
        public IReadOnlyCollection<LabelKey> KeysNamed
        {
            get
            {
                var keys = new HashSet<LabelKey>();
                foreach (PaintingPlace place in _places.Values)
                {
                    keys.Add(place.Place);
                    keys.Add(place.Museum);
                    if (place.Note is LabelKey note)
                    {
                        keys.Add(note);
                    }
                }

                return keys;
            }
        }
    }

    /// <summary>Turns the raw places file and a language's labels into checked places and a checked table.</summary>
    public static class PlacesFileMapper
    {
        /// <summary>The places of a catalogue's paintings.</summary>
        /// <exception cref="PlacesFormatException">
        /// The file is of another format version; a painting of the catalogue has no place (or two); a place names a
        /// painting the catalogue does not have; a place lies off the earth, has no key, or an unknown precision.
        /// </exception>
        public static MuseumPlaces ToPlaces(PlacesFile file, MuseumCatalogue catalogue)
        {
            if (file.formatVersion != PlacesFile.CurrentFormatVersion)
            {
                throw new PlacesFormatException($"The places file is in format version {file.formatVersion}; this code reads version {PlacesFile.CurrentFormatVersion}.");
            }

            var listed = new Dictionary<ArtworkId, PaintingPlace>();
            foreach (PaintingPlaceFile entry in file.paintings ?? Array.Empty<PaintingPlaceFile>())
            {
                PaintingPlace place = ToPlace(entry);
                if (listed.ContainsKey(place.Artwork))
                {
                    throw new PlacesFormatException($"The places file gives the painting \"{entry.id}\" two places.");
                }

                listed.Add(place.Artwork, place);
            }

            var places = new Dictionary<ArtworkId, PaintingPlace>();
            var missing = new List<string>();
            foreach (MuseumRoom room in catalogue.Rooms)
            {
                foreach (Artwork artwork in room.Artworks)
                {
                    if (listed.TryGetValue(artwork.Id, out PaintingPlace? place))
                    {
                        places.Add(artwork.Id, place);
                        listed.Remove(artwork.Id);
                    }
                    else
                    {
                        missing.Add(artwork.Id.Value);
                    }
                }
            }

            if (missing.Count > 0)
            {
                throw new PlacesFormatException($"The map has no place for {missing.Count} painting(s) of the catalogue: {string.Join(", ", missing)}. Every painting's pin needs one in museum/places.json.");
            }

            if (listed.Count > 0)
            {
                throw new PlacesFormatException($"The places file names painting(s) the catalogue does not have: {string.Join(", ", listed.Keys)}.");
            }

            return new MuseumPlaces(places);
        }

        /// <summary>A language's table of labels.</summary>
        /// <exception cref="PlacesFormatException">The file names no language, says no review, or has a key twice or a label without words.</exception>
        public static PlaceLabelTable ToTable(PlaceLabelsFile file)
        {
            if (string.IsNullOrWhiteSpace(file.language))
            {
                throw new PlacesFormatException("A table of the map's labels names no language.");
            }

            if (string.IsNullOrWhiteSpace(file.review))
            {
                throw new PlacesFormatException($"The {file.language} table of the map's labels says nobody's review.");
            }

            var labels = new Dictionary<LabelKey, string>();
            AddAll(labels, LabelKind.Place, file.places, file.language);
            AddAll(labels, LabelKind.Note, file.notes, file.language);
            AddAll(labels, LabelKind.Museum, file.museums, file.language);
            return new PlaceLabelTable(file.language, labels);
        }

        private static PaintingPlace ToPlace(PaintingPlaceFile entry)
        {
            ArtworkId artwork;
            GeoPoint point;
            try
            {
                artwork = new ArtworkId(entry.id);
                point = new GeoPoint(entry.lat, entry.lon);
            }
            catch (ArgumentException failure)
            {
                throw new PlacesFormatException($"The place of \"{entry.id}\": {failure.Message}", failure);
            }

            PlacePrecision precision = entry.precision switch
            {
                "exact" => PlacePrecision.Exact,
                "city" => PlacePrecision.City,
                "region" => PlacePrecision.Region,
                _ => throw new PlacesFormatException($"The place of \"{entry.id}\" has the precision \"{entry.precision}\"; it is exact, city or region."),
            };
            LabelKey? note = string.IsNullOrEmpty(entry.note) ? (LabelKey?)null : KeyOf(LabelKind.Note, entry.note!, entry.id);
            return new PaintingPlace(artwork, point, KeyOf(LabelKind.Place, entry.place, entry.id), precision, entry.verified, KeyOf(LabelKind.Museum, entry.museum, entry.id), note);
        }

        private static LabelKey KeyOf(LabelKind kind, string value, string painting)
        {
            try
            {
                return new LabelKey(kind, value);
            }
            catch (ArgumentException failure)
            {
                throw new PlacesFormatException($"The place of \"{painting}\": {failure.Message}", failure);
            }
        }

        private static void AddAll(Dictionary<LabelKey, string> labels, LabelKind kind, LabelEntryFile[]? entries, string language)
        {
            foreach (LabelEntryFile entry in entries ?? Array.Empty<LabelEntryFile>())
            {
                LabelKey key;
                try
                {
                    key = new LabelKey(kind, entry.key);
                }
                catch (ArgumentException failure)
                {
                    throw new PlacesFormatException($"The {language} table of the map's labels: {failure.Message}", failure);
                }

                if (string.IsNullOrWhiteSpace(entry.text))
                {
                    throw new PlacesFormatException($"The {language} table of the map's labels gives {key} no words.");
                }

                if (labels.ContainsKey(key))
                {
                    throw new PlacesFormatException($"The {language} table of the map's labels has {key} twice.");
                }

                labels.Add(key, entry.text);
            }
        }
    }
}
