#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Client.Words
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the file's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// A language's words for the museum as museum/titles/&lt;code&gt;.json writes them: each room's name, and each
    /// painting's title, painter and year as its plate says them:
    /// <c>{"language":"en","review":"machine","rooms":[{"number":1,"name":"Impressionism"}],"paintings":[{"id":"monet-water-lilies-1906","title":"Water Lilies","artist":"Claude Monet","year":"1906","titleFrom":"original"}]}</c>.
    /// A painter keeps their name in every language; the plate's words around it ("attributed to", "the Elder")
    /// and the year's ("c. 1893") are the language's. <c>titleFrom</c> says where a title comes from: "original"
    /// (the catalogue's original title, in this language already), "established" (the title the work is known by
    /// in this language, with <c>reference</c> naming where it was found, such as "wikidata:Q12418"), or
    /// "translation" (made for the game). The Turkish words are the catalogue's own, which has no such file.
    /// No Unity dependency.
    /// </summary>
    [Serializable]
    public sealed class CaptionFile
    {
        public string language = string.Empty;
        public string review = string.Empty;
        public RoomCaption[] rooms = Array.Empty<RoomCaption>();
        public PaintingCaption[] paintings = Array.Empty<PaintingCaption>();
    }

    [Serializable]
    public sealed class RoomCaption
    {
        public int number;
        public string name = string.Empty;
    }

    [Serializable]
    public sealed class PaintingCaption
    {
        public string id = string.Empty;
        public string title = string.Empty;
        public string artist = string.Empty;
        public string year = string.Empty;

        /// <summary>"original", "established" or "translation" (<see cref="TitleSource"/>).</summary>
        public string titleFrom = string.Empty;

        /// <summary>Where an established title was found; empty for the others.</summary>
        public string reference = string.Empty;
    }

    /// <summary>Where a painting's title in a language comes from. A closed set.</summary>
    public enum TitleSource
    {
        /// <summary>The catalogue's original title, which is in the language already.</summary>
        Original,

        /// <summary>The title the work is known by in the language, found where the entry's reference says.</summary>
        Established,

        /// <summary>Made for the game, as no established title was found.</summary>
        Translation,
    }

    /// <summary>
    /// The museum's words in one language: a room's name, a painting's title, painter and year as its plate says
    /// them. Turkish has the catalogue's own (<see cref="OfTheCatalogue"/>); another language has a table that is
    /// complete once it has every room and painting of the catalogue (<see cref="IsCompleteFor"/>). Where a table
    /// that is not complete lacks something (only such a table can, and the game offers none such), the plate
    /// says the catalogue's words. No Unity dependency.
    /// </summary>
    public sealed class CaptionTable
    {
        private readonly Dictionary<int, string> _rooms;
        private readonly Dictionary<string, Plate> _plates;

        private CaptionTable(Language language, TableReview review, Dictionary<int, string> rooms, Dictionary<string, Plate> plates)
        {
            Language = language;
            Review = review;
            _rooms = rooms;
            _plates = plates;
        }

        public Language Language { get; }

        public TableReview Review { get; }

        /// <summary>The language the catalogue is written in: its rooms' names and its titles are the source of every other table's.</summary>
        public static Language CatalogueLanguage => Language.Turkish;

        /// <summary>The catalogue's own words, which are Turkish.</summary>
        public static CaptionTable OfTheCatalogue { get; } =
            new CaptionTable(CatalogueLanguage, TableReview.Source, new Dictionary<int, string>(), new Dictionary<string, Plate>());

        /// <summary>True for the catalogue's own words: they need no file and are always complete.</summary>
        public bool IsTheCatalogues => ReferenceEquals(this, OfTheCatalogue);

        /// <summary>Every piece of words the table holds, for the tests that hold them against the letters the game can write.</summary>
        public IEnumerable<string> AllWords
        {
            get
            {
                foreach (string name in _rooms.Values)
                {
                    yield return name;
                }

                foreach (Plate plate in _plates.Values)
                {
                    yield return plate.Title;
                    yield return plate.Artist;
                    yield return plate.Year;
                }
            }
        }

        /// <summary>The table a file holds.</summary>
        /// <exception cref="WordTableException">
        /// The file names no language the game knows, or is of the catalogue's own language; a room or painting
        /// comes twice, or lacks its words; a title says no source, or an established one no reference.
        /// </exception>
        public static CaptionTable Of(CaptionFile file)
        {
            Language language = Languages.FromCode(file.language)
                ?? throw new WordTableException($"A table of the museum's words is of \"{file.language}\", which is no language the game speaks.");
            if (language == CatalogueLanguage)
            {
                throw new WordTableException("The catalogue's own words are Turkish; a Turkish table of the museum's words would stand beside them.");
            }

            TableReview review = TableReviews.Of(file.review);
            var rooms = new Dictionary<int, string>();
            foreach (RoomCaption room in file.rooms ?? Array.Empty<RoomCaption>())
            {
                if (string.IsNullOrEmpty(room.name) || rooms.ContainsKey(room.number))
                {
                    throw new WordTableException($"The {file.language} museum table names room {room.number} {(rooms.ContainsKey(room.number) ? "twice" : "with no words")}.");
                }

                rooms.Add(room.number, room.name);
            }

            var plates = new Dictionary<string, Plate>();
            foreach (PaintingCaption painting in file.paintings ?? Array.Empty<PaintingCaption>())
            {
                if (plates.ContainsKey(painting.id))
                {
                    throw new WordTableException($"The {file.language} museum table has the painting {painting.id} twice.");
                }

                plates.Add(painting.id, Plate.Of(painting, file.language));
            }

            return new CaptionTable(language, review, rooms, plates);
        }

        /// <summary>True when the table has a name for every room and a plate for every painting of a catalogue.</summary>
        public bool IsCompleteFor(MuseumCatalogue catalogue) => IsTheCatalogues || MissingFrom(catalogue).Count == 0;

        /// <summary>What the table lacks of a catalogue: rooms as "room 3", paintings by their ids; nothing for the catalogue's own words.</summary>
        public IReadOnlyList<string> MissingFrom(MuseumCatalogue catalogue)
        {
            var missing = new List<string>();
            if (IsTheCatalogues)
            {
                return missing;
            }

            foreach (MuseumRoom room in catalogue.Rooms)
            {
                if (!room.IsNameProvisional && !_rooms.ContainsKey(room.Number.Value))
                {
                    missing.Add($"room {room.Number.Value}");
                }

                foreach (Artwork artwork in room.Artworks)
                {
                    if (!_plates.ContainsKey(artwork.Id.Value))
                    {
                        missing.Add(artwork.Id.Value);
                    }
                }
            }

            return missing;
        }

        /// <summary>Where a painting's title in this language comes from; nothing for the catalogue's own words and for a painting the table lacks.</summary>
        public TitleSource? SourceOf(Artwork artwork) => _plates.TryGetValue(artwork.Id.Value, out Plate plate) ? plate.Source : (TitleSource?)null;

        /// <summary>A room's name: its movement in this language.</summary>
        public string NameOf(MuseumRoom room) => _rooms.TryGetValue(room.Number.Value, out string? name) ? name : room.Movement;

        public string TitleOf(Artwork artwork) => _plates.TryGetValue(artwork.Id.Value, out Plate plate) ? plate.Title : artwork.Caption.Title;

        /// <summary>The painter as the plate names them: their own name, with this language's words around it where the catalogue has some.</summary>
        public string ArtistOf(Artwork artwork) => _plates.TryGetValue(artwork.Id.Value, out Plate plate) ? plate.Artist : artwork.Caption.Artist;

        /// <summary>The year as the plate says it, with this language's words where it is not one year ("c. 1893", "after 1795").</summary>
        public string YearOf(Artwork artwork) => _plates.TryGetValue(artwork.Id.Value, out Plate plate) ? plate.Year : artwork.Caption.Year;

        /// <summary>A painting's plate in a language.</summary>
        private readonly struct Plate
        {
            private Plate(string title, string artist, string year, TitleSource source)
            {
                Title = title;
                Artist = artist;
                Year = year;
                Source = source;
            }

            public string Title { get; }

            public string Artist { get; }

            public string Year { get; }

            public TitleSource Source { get; }

            public static Plate Of(PaintingCaption painting, string code)
            {
                if (string.IsNullOrEmpty(painting.title) || string.IsNullOrEmpty(painting.artist) || string.IsNullOrEmpty(painting.year))
                {
                    throw new WordTableException($"The {code} museum table gives the painting {painting.id} no title, painter or year; a plate has all three.");
                }

                TitleSource source = painting.titleFrom switch
                {
                    "original" => TitleSource.Original,
                    "established" => TitleSource.Established,
                    "translation" => TitleSource.Translation,
                    _ => throw new WordTableException($"The {code} museum table says the title of {painting.id} comes from \"{painting.titleFrom}\"; write original, established or translation."),
                };
                if (source == TitleSource.Established && string.IsNullOrEmpty(painting.reference))
                {
                    throw new WordTableException($"The {code} museum table gives {painting.id} an established title without saying where it was found.");
                }

                return new Plate(painting.title, painting.artist, painting.year, source);
            }
        }
    }
}
