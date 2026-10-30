#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using Puzzle.Museum.Catalogue;
using UnityEngine;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// The tables of words a built game carries: words/&lt;code&gt;.json and museum/titles/&lt;code&gt;.json as
    /// tools/unity/sync-engine.js packs them into Resources/Words and Resources/Titles. A language is offered when
    /// the game carries its words and the museum's, and both are complete for the catalogue the game carries; the
    /// Turkish museum's words are the catalogue's own. Each language is read once, when it is first asked for.
    /// </summary>
    public static class WordsPack
    {
        /// <summary>The folders below a Resources folder the tables lie in, named by their language's code.</summary>
        private const string WordsFolder = "Words";
        private const string TitlesFolder = "Titles";

        private static readonly Dictionary<Language, Wording?> Read = new Dictionary<Language, Wording?>();
        private static readonly Lazy<MuseumCatalogue> Catalogue = new Lazy<MuseumCatalogue>(MuseumPack.LoadPacked);
        private static IReadOnlyList<Wording>? _complete;

        /// <summary>The languages the game offers: those whose words and museum's words it carries complete, in the order of the enumeration.</summary>
        /// <exception cref="WordTableException">A table the game carries is not a table.</exception>
        /// <exception cref="CatalogueFormatException">The game's catalogue is missing or not a catalogue.</exception>
        public static IReadOnlyList<Wording> Complete => _complete ??= FindTheComplete();

        /// <summary>The offers of the language card: every complete language, by its own name.</summary>
        public static IReadOnlyList<LanguageOffer> Offered => LanguageOffer.Of(Complete);

        /// <summary>
        /// The language the game speaks for these settings on a phone set to a language: the player's choice, or
        /// else the phone's, where the game offers it; English otherwise.
        /// </summary>
        public static Language InEffect(DeviceSettings settings, Language phone) =>
            Languages.InEffect(settings.Language, phone, LanguagesOf(Complete));

        /// <summary>From now on the game speaks a language it offers.</summary>
        /// <exception cref="ArgumentException">The game does not offer the language: it lacks its tables, or they are not complete.</exception>
        public static void Speak(Language language)
        {
            foreach (Wording wording in Complete)
            {
                if (wording.Language == language)
                {
                    Speech.Speak(wording);
                    return;
                }
            }

            throw new ArgumentException($"The game does not offer {Languages.CodeOf(language)}: its tables are missing or not complete.", nameof(language));
        }

        /// <summary>A language's words as the game carries them, complete or not; nothing where it carries no table of words for the language.</summary>
        /// <exception cref="WordTableException">A table the game carries is not a table.</exception>
        public static Wording? Load(Language language)
        {
            if (Read.TryGetValue(language, out Wording? known))
            {
                return known;
            }

            Wording? loaded = WordsOf(language) is WordTable words && CaptionsOf(language) is CaptionTable captions ? new Wording(words, captions) : null;
            Read[language] = loaded;
            return loaded;
        }

        private static IReadOnlyList<Wording> FindTheComplete()
        {
            var complete = new List<Wording>();
            foreach (Language language in Languages.All)
            {
                if (Load(language) is Wording wording && wording.IsCompleteFor(Catalogue.Value))
                {
                    complete.Add(wording);
                }
            }

            if (complete.Count == 0)
            {
                throw new WordTableException("The game carries no complete table of words; run node tools/unity/sync-engine.js from the repository root.");
            }

            return complete;
        }

        private static WordTable? WordsOf(Language language)
        {
            TextAsset? packed = Resources.Load<TextAsset>($"{WordsFolder}/{Languages.CodeOf(language)}");
            return packed == null ? null : WordTable.Of(ReadAs<WordFile>(packed.text, language, "words"));
        }

        /// <summary>The museum's words: the catalogue's own in its language, the packed table in another; nothing where the game carries none.</summary>
        private static CaptionTable? CaptionsOf(Language language)
        {
            if (language == CaptionTable.CatalogueLanguage)
            {
                return CaptionTable.OfTheCatalogue;
            }

            TextAsset? packed = Resources.Load<TextAsset>($"{TitlesFolder}/{Languages.CodeOf(language)}");
            return packed == null ? null : CaptionTable.Of(ReadAs<CaptionFile>(packed.text, language, "museum's words"));
        }

        private static T ReadAs<T>(string json, Language language, string what)
            where T : class
        {
            T? file;
            try
            {
                file = JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException notJson)
            {
                throw new WordTableException($"The {Languages.CodeOf(language)} table of the {what} is not JSON: {notJson.Message}", notJson);
            }

            return file ?? throw new WordTableException($"The {Languages.CodeOf(language)} table of the {what} is empty.");
        }

        private static IReadOnlyCollection<Language> LanguagesOf(IReadOnlyList<Wording> wordings)
        {
            var languages = new List<Language>();
            foreach (Wording wording in wordings)
            {
                languages.Add(wording.Language);
            }

            return languages;
        }
    }
}
