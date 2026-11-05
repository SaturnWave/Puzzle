using System;
using System.Collections.Generic;

namespace Puzzle.Museum.Catalogue
{
    /// <summary>The catalogue file is malformed or contradicts itself; the message names the room or painting and the problem.</summary>
    public sealed class CatalogueFormatException : Exception
    {
        public CatalogueFormatException(string message)
            : base(message)
        {
        }

        public CatalogueFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>Turns the raw <see cref="CatalogueFile"/> into a checked <see cref="MuseumCatalogue"/>.</summary>
    public static class CatalogueFileMapper
    {
        private const string LandscapeWord = "landscape";
        private const string PortraitWord = "portrait";

        /// <exception cref="CatalogueFormatException">The file is not a catalogue this code can show.</exception>
        public static MuseumCatalogue ToCatalogue(CatalogueFile file)
        {
            if (file.formatVersion != CatalogueFile.CurrentFormatVersion)
            {
                throw new CatalogueFormatException($"The catalogue is in format version {file.formatVersion}; this code reads version {CatalogueFile.CurrentFormatVersion}.");
            }

            RoomFile[] listed = ListOrNone(file.rooms);
            if (listed.Length != MuseumPlan.RoomCount)
            {
                throw new CatalogueFormatException($"The catalogue lists {listed.Length} rooms; the museum has {MuseumPlan.RoomCount}.");
            }

            var known = new KnownArtworks();
            var rooms = new List<MuseumRoom>(listed.Length);
            for (int index = 0; index < listed.Length; index++)
            {
                rooms.Add(ToRoom(listed[index], expectedNumber: index + 1, known));
            }

            return new MuseumCatalogue(rooms);
        }

        private static MuseumRoom ToRoom(RoomFile file, int expectedNumber, KnownArtworks known)
        {
            if (file.number != expectedNumber)
            {
                throw new CatalogueFormatException($"Room {file.number} stands where room {expectedNumber} belongs: the rooms are listed in order, 1 to {MuseumPlan.RoomCount}.");
            }

            if (string.IsNullOrWhiteSpace(file.movement))
            {
                throw new CatalogueFormatException($"Room {file.number} has no art movement.");
            }

            if (string.IsNullOrWhiteSpace(file.shortName))
            {
                throw new CatalogueFormatException($"Room {file.number} has no short name for the world map's strip of eras.");
            }

            PaintingFile[] listed = ListOrNone(file.paintings);
            if (listed.Length > MuseumPlan.PaintingsPerRoom)
            {
                throw new CatalogueFormatException($"Room {file.number} lists {listed.Length} paintings; a room holds {MuseumPlan.PaintingsPerRoom}.");
            }

            var paintings = new List<Artwork>(listed.Length);
            foreach (PaintingFile painting in listed)
            {
                paintings.Add(ToArtwork(painting, file.number, known));
            }

            return new MuseumRoom(new RoomNumber(file.number), file.movement, file.shortName, file.provisionalName, paintings);
        }

        private static Artwork ToArtwork(PaintingFile file, int room, KnownArtworks known)
        {
            string where = $"Room {room}, painting \"{file.id}\"";
            ArtworkId id = ParseId(file.id, room);
            RequireText(file.title, where, "title");
            RequireText(file.originalTitle, where, "original title");
            RequireText(file.artist, where, "artist");
            RequireText(file.year, where, "year");
            RequireText(file.source, where, "source");
            RequireText(file.licence, where, "licence note");
            RequireImagePath(file.image, where);
            known.Add(id, file.image, where);

            return new Artwork(
                id,
                new ArtworkCaption(file.title, file.originalTitle, file.artist, file.year),
                ParseOrientation(file.orientation, where),
                file.image,
                new ArtworkProvenance(file.source, file.licence));
        }

        /// <summary>A JSON reader hands over nothing where the document writes null for a list; that reads as an empty list.</summary>
        private static T[] ListOrNone<T>(T[]? list) => list ?? Array.Empty<T>();

        private static ArtworkId ParseId(string text, int room)
        {
            try
            {
                return new ArtworkId(text);
            }
            catch (ArgumentException failure)
            {
                throw new CatalogueFormatException($"Room {room}: {failure.Message}", failure);
            }
        }

        private static PaintingOrientation ParseOrientation(string text, string where)
        {
            switch (text)
            {
                case LandscapeWord:
                    return PaintingOrientation.Landscape;
                case PortraitWord:
                    return PaintingOrientation.Portrait;
                default:
                    throw new CatalogueFormatException($"{where}: the orientation is \"{text}\"; it is \"{LandscapeWord}\" or \"{PortraitWord}\".");
            }
        }

        private static void RequireText(string text, string where, string what)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new CatalogueFormatException($"{where} has no {what}.");
            }
        }

        /// <summary>A picture is named by a path that stays below the paintings folder on every system.</summary>
        private static void RequireImagePath(string path, string where)
        {
            RequireText(path, where, "image file");
            bool leavesTheFolder = path.StartsWith('/') || path.Contains("..") || path.Contains('\\') ||path.Contains(':');
            if (leavesTheFolder)
            {
                throw new CatalogueFormatException($"{where}: the image file \"{path}\" must be a path below the paintings folder, written with forward slashes.");
            }
        }

        /// <summary>The ids and picture files seen so far: no two paintings of the museum share either.</summary>
        private sealed class KnownArtworks
        {
            private readonly HashSet<ArtworkId> _ids = new HashSet<ArtworkId>();
            private readonly HashSet<string> _images = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public void Add(ArtworkId id, string image, string where)
            {
                if (!_ids.Add(id))
                {
                    throw new CatalogueFormatException($"{where}: the id is used by another painting too.");
                }

                if (!_images.Add(image))
                {
                    throw new CatalogueFormatException($"{where}: the image file \"{image}\" is used by another painting too.");
                }
            }
        }
    }
}
