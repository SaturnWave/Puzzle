#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Client.Words
{
    /// <summary>A language's words: what the game says in it, and the museum's rooms and plates as it names them. No Unity dependency.</summary>
    public sealed class Wording
    {
        /// <exception cref="ArgumentException">The two tables are of different languages.</exception>
        public Wording(WordTable words, CaptionTable captions)
        {
            Words = words ?? throw new ArgumentNullException(nameof(words));
            Captions = captions ?? throw new ArgumentNullException(nameof(captions));
            if (words.Language != captions.Language)
            {
                throw new ArgumentException(
                    $"The words are {Languages.CodeOf(words.Language)} and the museum's words {Languages.CodeOf(captions.Language)}: a language speaks with one tongue.", nameof(captions));
            }
        }

        public Language Language => Words.Language;

        public WordTable Words { get; }

        public CaptionTable Captions { get; }

        /// <summary>
        /// True when the game may offer the language: every word it says, and every room and plate of the museum,
        /// in this language.
        /// </summary>
        public bool IsCompleteFor(MuseumCatalogue catalogue) => Words.IsComplete && Captions.IsCompleteFor(catalogue);
    }

    /// <summary>
    /// The language the game speaks, and the one way its screens say anything: each word the player sees is read
    /// here, by its id, from the table of that language. The game speaks one language at a time; it changes when
    /// the player chooses another on the language card, and every screen built after that speaks it.
    /// </summary>
    /// <remarks>
    /// Whoever starts the game says which language it speaks (<see cref="Speak"/>): the game's root, from the
    /// player's choice or the phone's language; the Editor's tools, from their command line. Where nothing was
    /// spoken yet, the loader a host left with <see cref="SpeakWhenAsked"/> is asked once. A test that holds the
    /// game's words in another language speaks it for its own flow of work only (<see cref="SpeakWithin"/>), so
    /// tests that run beside it go on hearing the language they expect. No Unity dependency.
    /// </remarks>
    public static class Speech
    {
        private static readonly AsyncLocal<Wording?> SpokenWithin = new AsyncLocal<Wording?>();
        private static Wording? _spoken;
        private static Func<Wording>? _whenAsked;

        /// <summary>The words the game speaks now.</summary>
        /// <exception cref="InvalidOperationException">Nothing was spoken, and no loader was left to ask.</exception>
        public static Wording Current => SpokenWithin.Value ?? _spoken ?? FirstWords();

        public static Language Language => Current.Language;

        /// <summary>The museum's rooms and plates in the language the game speaks.</summary>
        public static CaptionTable Captions => Current.Captions;

        /// <summary>From now on the game speaks these words.</summary>
        public static void Speak(Wording wording) => _spoken = wording ?? throw new ArgumentNullException(nameof(wording));

        /// <summary>Leaves the words to speak until something is spoken: they are loaded the first time a word is asked for.</summary>
        public static void SpeakWhenAsked(Func<Wording> load) => _whenAsked = load ?? throw new ArgumentNullException(nameof(load));

        /// <summary>
        /// Speaks these words within the flow of work that asks, until the returned scope is disposed: the game's
        /// words as they are in another language, for a test, while the tests beside it hear theirs.
        /// </summary>
        public static IDisposable SpeakWithin(Wording wording)
        {
            Wording? before = SpokenWithin.Value;
            SpokenWithin.Value = wording ?? throw new ArgumentNullException(nameof(wording));
            return new Scope(before);
        }

        /// <summary>What an id says in the language the game speaks.</summary>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The language's table has no words for the id: it is not complete.</exception>
        /// <exception cref="FormatException">The words have a place in them: say them with what fills it.</exception>
        public static string Say(WordId id) => Template.Fill(Current.Words.WordsOf(id, PluralForm.Other), Array.Empty<(string, string)>());

        /// <summary>What an id says, with each place in its words filled: <c>Say(WordId.PlayLevel, ("level", "47"))</c>.</summary>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The language's table has no words for the id.</exception>
        /// <exception cref="FormatException">A place in the words has no value among those given.</exception>
        public static string Say(WordId id, params (string Name, string Value)[] filling) =>
            Template.Fill(Current.Words.WordsOf(id, PluralForm.Other), filling);

        /// <summary>
        /// What an id says of a count, in the form the language gives that count ("1 coin", "4 coins"), with each place
        /// filled; the count itself fills a place only where it is given among them, written as the caller wants it.
        /// </summary>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The language's table has no words for the id.</exception>
        /// <exception cref="FormatException">A place in the words has no value among those given.</exception>
        public static string SayCounted(WordId id, long count, params (string Name, string Value)[] filling) =>
            Template.Fill(Current.Words.WordsOf(id, Languages.FormOf(Current.Language, count)), filling);

        private static Wording FirstWords()
        {
            Func<Wording> load = _whenAsked
                ?? throw new InvalidOperationException("The game speaks no language yet: speak one before anything is said, or leave a loader with Speech.SpeakWhenAsked.");
            Wording loaded = load();
            _spoken = loaded;
            return loaded;
        }

        /// <summary>Ends a <see cref="SpeakWithin"/>: the flow of work speaks what it spoke before.</summary>
        private sealed class Scope : IDisposable
        {
            private readonly Wording? _before;
            private bool _isDisposed;

            public Scope(Wording? before)
            {
                _before = before;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    SpokenWithin.Value = _before;
                    _isDisposed = true;
                }
            }
        }
    }
}
