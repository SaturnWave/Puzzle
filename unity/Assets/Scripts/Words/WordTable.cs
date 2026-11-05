#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Words
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the file's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// A language's table of words as words/&lt;code&gt;.json writes it:
    /// <c>{"language":"en","review":"machine","words":[{"id":"ShopTitle","text":"Shop"},{"id":"ResultNewPieces","one":"{count} new piece:","other":"{count} new pieces:"}]}</c>.
    /// Each entry is keyed by the stable id of a <see cref="WordId"/>; it has its words in <c>text</c>, or, where they
    /// count something, a form for each count its language tells apart (<c>one</c>, <c>few</c>, <c>many</c>,
    /// <c>other</c>). The Turkish table is the source and carries a <c>note</c> on each entry for its translators.
    /// The fields are public and named as the file names them, because that is how Unity's JSON reader maps a
    /// document; it makes an empty text of a field an entry does not have. No Unity dependency.
    /// </summary>
    [Serializable]
    public sealed class WordFile
    {
        public string language = string.Empty;

        /// <summary>Who stands behind the words: "source", "machine" or "reviewed" (<see cref="TableReview"/>).</summary>
        public string review = string.Empty;

        public WordEntry[] words = Array.Empty<WordEntry>();
    }

    /// <summary>One entry of a <see cref="WordFile"/>.</summary>
    [Serializable]
    public sealed class WordEntry
    {
        public string id = string.Empty;
        public string text = string.Empty;
        public string one = string.Empty;
        public string few = string.Empty;
        public string many = string.Empty;
        public string other = string.Empty;

        /// <summary>For the translators: where the words stand and what they say. Only the source table has notes; the game never reads them.</summary>
        public string note = string.Empty;
    }

    /// <summary>Who stands behind a table's words. A closed set.</summary>
    public enum TableReview
    {
        /// <summary>The source the others are translated from: the Turkish table.</summary>
        Source,

        /// <summary>Translated by a machine and not yet read by a native speaker.</summary>
        MachineMade,

        /// <summary>Read and approved by a native speaker.</summary>
        Reviewed,
    }

    /// <summary>How a table's file writes who stands behind its words.</summary>
    public static class TableReviews
    {
        /// <exception cref="WordTableException">The text is none of "source", "machine" and "reviewed".</exception>
        public static TableReview Of(string review) => review switch
        {
            "source" => TableReview.Source,
            "machine" => TableReview.MachineMade,
            "reviewed" => TableReview.Reviewed,
            _ => throw new WordTableException($"A table says its review is \"{review}\"; write source, machine or reviewed."),
        };
    }

    /// <summary>The words a table's file stands for could not be read: the file is not a table, or an entry is not one.</summary>
    public sealed class WordTableException : Exception
    {
        public WordTableException(string message)
            : base(message)
        {
        }

        public WordTableException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// A language's words, by their ids: what the game says in it. A table may lack words while it is being
    /// translated; the game offers a language only once its table is complete (<see cref="IsComplete"/>).
    /// No Unity dependency.
    /// </summary>
    public sealed class WordTable
    {
        private readonly Dictionary<WordId, Entry> _entries;

        private WordTable(Language language, TableReview review, Dictionary<WordId, Entry> entries)
        {
            Language = language;
            Review = review;
            _entries = entries;
        }

        public Language Language { get; }

        public TableReview Review { get; }

        /// <summary>The ids of every word the game says, in the order of their enumeration.</summary>
        public static IReadOnlyList<WordId> Ids { get; } = (WordId[])Enum.GetValues(typeof(WordId));

        /// <summary>True when the table has words for every id.</summary>
        public bool IsComplete => Missing.Count == 0;

        /// <summary>The ids the table has no words for, in the order of their enumeration.</summary>
        public IReadOnlyList<WordId> Missing
        {
            get
            {
                var missing = new List<WordId>();
                foreach (WordId id in Ids)
                {
                    if (!_entries.ContainsKey(id))
                    {
                        missing.Add(id);
                    }
                }

                return missing;
            }
        }

        /// <summary>Every piece of words the table holds: each entry's text, or each of its forms.</summary>
        public IEnumerable<(WordId Id, string Words)> AllWords
        {
            get
            {
                foreach (KeyValuePair<WordId, Entry> entry in _entries)
                {
                    foreach (string words in entry.Value.AllWords)
                    {
                        yield return (entry.Key, words);
                    }
                }
            }
        }

        /// <summary>The table a file holds.</summary>
        /// <exception cref="WordTableException">
        /// The file names no language the game knows, or no review; an entry has an id the game does not say, comes
        /// twice, has no words, or has both words and forms.
        /// </exception>
        public static WordTable Of(WordFile file)
        {
            Language language = Languages.FromCode(file.language)
                ?? throw new WordTableException($"A table of words is of \"{file.language}\", which is no language the game speaks.");
            TableReview review = TableReviews.Of(file.review);
            var entries = new Dictionary<WordId, Entry>();
            foreach (WordEntry entry in file.words ?? Array.Empty<WordEntry>())
            {
                if (!Enum.TryParse(entry.id, ignoreCase: false, out WordId id) || !Enum.IsDefined(typeof(WordId), id) || IsANumber(entry.id))
                {
                    throw new WordTableException($"The {file.language} table has words for \"{entry.id}\", which the game never says.");
                }

                if (entries.ContainsKey(id))
                {
                    throw new WordTableException($"The {file.language} table has words for {id} twice.");
                }

                entries.Add(id, Entry.Of(entry, file.language));
            }

            return new WordTable(language, review, entries);
        }

        /// <summary>True when the table has words for an id.</summary>
        public bool Has(WordId id) => _entries.ContainsKey(id);

        /// <summary>An id's words, in the form a count takes; the words themselves where they count nothing.</summary>
        /// <exception cref="KeyNotFoundException">The table has no words for the id.</exception>
        public string WordsOf(WordId id, PluralForm form) =>
            _entries.TryGetValue(id, out Entry entry)
                ? entry.In(form)
                : throw new KeyNotFoundException($"The {Languages.CodeOf(Language)} table has no words for {id}.");

        /// <summary>True when an id's entry gives forms for counts rather than one text.</summary>
        public bool IsCounted(WordId id) => _entries.TryGetValue(id, out Entry entry) && entry.IsCounted;

        /// <summary>Enum.TryParse takes "12" for the twelfth member; an id is always a name.</summary>
        private static bool IsANumber(string id) => id.Length > 0 && (char.IsDigit(id[0]) || id[0] == '-');

        /// <summary>An id's words: one text, or a form for each count its language tells apart.</summary>
        private readonly struct Entry
        {
            private readonly string? _text;
            private readonly string? _one;
            private readonly string? _few;
            private readonly string? _many;
            private readonly string? _other;

            private Entry(string? text, string? one, string? few, string? many, string? other)
            {
                _text = text;
                _one = one;
                _few = few;
                _many = many;
                _other = other;
            }

            public bool IsCounted => _text == null;

            public IEnumerable<string> AllWords
            {
                get
                {
                    foreach (string? words in new[] { _text, _one, _few, _many, _other })
                    {
                        if (words != null)
                        {
                            yield return words;
                        }
                    }
                }
            }

            /// <summary>The empty texts Unity's reader makes of fields an entry does not have are no words.</summary>
            public static Entry Of(WordEntry entry, string code)
            {
                string? text = Given(entry.text);
                string? other = Given(entry.other);
                bool hasForms = Given(entry.one) != null || Given(entry.few) != null || Given(entry.many) != null || other != null;
                if (text != null && hasForms)
                {
                    throw new WordTableException($"The {code} table gives {entry.id} both words and forms for counts; give one or the other.");
                }

                if (text == null && other == null)
                {
                    throw new WordTableException($"The {code} table gives {entry.id} no words{(hasForms ? ": a counted word needs its \"other\" form" : string.Empty)}.");
                }

                return new Entry(text, Given(entry.one), Given(entry.few), Given(entry.many), other);
            }

            /// <summary>The text, or the form asked for, or the "other" form where the entry gives none of its own for that count.</summary>
            public string In(PluralForm form)
            {
                if (_text != null)
                {
                    return _text;
                }

                string? own = form switch
                {
                    PluralForm.One => _one,
                    PluralForm.Few => _few,
                    PluralForm.Many => _many,
                    PluralForm.Other => _other,
                    _ => throw new ArgumentOutOfRangeException(nameof(form), form, "A form of a counted word the table does not know."),
                };
                return own ?? _other!;
            }

            private static string? Given(string? words) => string.IsNullOrEmpty(words) ? null : words;
        }
    }
}
