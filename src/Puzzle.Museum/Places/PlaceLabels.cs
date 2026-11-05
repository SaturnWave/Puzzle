using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Museum.Places
{
    /// <summary>One language's labels for the map: the places, the notes and the museums by their keys.</summary>
    public sealed class PlaceLabelTable
    {
        private readonly IReadOnlyDictionary<LabelKey, string> _labels;

        internal PlaceLabelTable(string language, IReadOnlyDictionary<LabelKey, string> labels)
        {
            Language = language;
            _labels = labels;
        }

        /// <summary>The language's code, as its file names it ("tr", "en", "pt-BR").</summary>
        public string Language { get; }

        public int Count => _labels.Count;

        /// <summary>Every label the table holds, for the tests that hold them against the letters the game can write.</summary>
        public IEnumerable<string> AllWords => _labels.Values;

        /// <summary>The label of a key; nothing where the table lacks it.</summary>
        public string? Find(LabelKey key) => _labels.TryGetValue(key, out string? text) ? text : null;
    }

    /// <summary>A label as the map says it, with the language it was found in: the one asked for, or a fallback.</summary>
    public readonly struct PlaceLabel
    {
        public PlaceLabel(string text, string language)
        {
            Text = text;
            Language = language;
        }

        public string Text { get; }

        /// <summary>The code of the table the label came from.</summary>
        public string Language { get; }

        public override string ToString() => Text;
    }

    /// <summary>What a painting's card says of its place: where its pin stands, the museum that holds it, and a note where it has one.</summary>
    public readonly struct PlaceWords
    {
        public PlaceWords(PlaceLabel place, PlaceLabel museum, PlaceLabel? note)
        {
            Place = place;
            Museum = museum;
            Note = note;
        }

        public PlaceLabel Place { get; }

        public PlaceLabel Museum { get; }

        /// <summary>The note on the place; nothing for a painting without one.</summary>
        public PlaceLabel? Note { get; }
    }

    /// <summary>
    /// The map's labels in the language the player reads, with a clean fallback: a label is looked up in the tables
    /// asked first, in their order (the player's language, then English, as the game falls back for a language it
    /// does not speak), and last in the source table, Turkish, which the places are written against. A language
    /// whose file is missing is simply not asked; a key a table lacks is looked up in the next one. The source must
    /// hold every key the places name, or nothing is read at all.
    /// </summary>
    public sealed class PlaceLabels
    {
        private readonly IReadOnlyList<PlaceLabelTable> _inOrder;

        private PlaceLabels(IReadOnlyList<PlaceLabelTable> inOrder)
        {
            _inOrder = inOrder;
        }

        /// <param name="places">The places whose labels are asked for.</param>
        /// <param name="source">The source table, asked last: it holds every label.</param>
        /// <param name="askedFirst">The tables to ask before the source, in their order; a missing language is left out of the list.</param>
        /// <exception cref="PlacesFormatException">The source lacks a key the places name.</exception>
        public static PlaceLabels Of(MuseumPlaces places, PlaceLabelTable source, IEnumerable<PlaceLabelTable> askedFirst)
        {
            List<LabelKey> missing = places.KeysNamed.Where(key => source.Find(key) == null).OrderBy(key => key.ToString(), StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
            {
                throw new PlacesFormatException($"The {source.Language} table of the map's labels, the source, lacks {missing.Count} key(s): {string.Join(", ", missing)}.");
            }

            var inOrder = askedFirst.Where(table => table.Language != source.Language).ToList();
            inOrder.Add(source);
            return new PlaceLabels(inOrder);
        }

        /// <summary>
        /// The languages whose tables are asked before the source, in their order: the language spoken, then the
        /// fallback (English), each once, and neither when it is the source itself: a player who reads the source
        /// reads its own labels, never the fallback's.
        /// </summary>
        public static IReadOnlyList<string> AskedBeforeTheSource(string spoken, string fallback, string source)
        {
            if (spoken == source)
            {
                return Array.Empty<string>();
            }

            return spoken == fallback || fallback == source ? new[] { spoken } : new[] { spoken, fallback };
        }

        /// <summary>The codes of the tables asked, in their order, the source last.</summary>
        public IReadOnlyList<string> Languages => _inOrder.Select(table => table.Language).ToList();

        public PlaceWords WordsOf(PaintingPlace place) =>
            new PlaceWords(LabelOf(place.Place), LabelOf(place.Museum), place.Note is LabelKey note ? LabelOf(note) : (PlaceLabel?)null);

        /// <exception cref="ArgumentException">No table holds the key: it is not one the places name.</exception>
        public PlaceLabel LabelOf(LabelKey key)
        {
            foreach (PlaceLabelTable table in _inOrder)
            {
                if (table.Find(key) is string text)
                {
                    return new PlaceLabel(text, table.Language);
                }
            }

            throw new ArgumentException($"No table of the map's labels holds {key}: it is not one the places name.", nameof(key));
        }
    }
}
