#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Words
{
    /// <summary>
    /// A language the language card offers: the language, and its name as it calls itself ("English", "한국어"),
    /// which the card shows whatever language the game speaks. No Unity dependency.
    /// </summary>
    public sealed class LanguageOffer
    {
        public LanguageOffer(Language language, string ownName)
        {
            Language = language;
            OwnName = string.IsNullOrEmpty(ownName) ? throw new ArgumentException("A language offered has a name of its own.", nameof(ownName)) : ownName;
        }

        public Language Language { get; }

        public string OwnName { get; }

        /// <summary>The offers of some languages' complete words, each named from its own table, in the order of the enumeration.</summary>
        public static IReadOnlyList<LanguageOffer> Of(IEnumerable<Wording> complete)
        {
            var offers = new List<LanguageOffer>();
            foreach (Wording wording in complete)
            {
                offers.Add(new LanguageOffer(wording.Language, wording.Words.WordsOf(WordId.LanguageOwnName, PluralForm.Other)));
            }

            offers.Sort((one, other) => one.Language.CompareTo(other.Language));
            return offers;
        }

        public override string ToString() => $"{Languages.CodeOf(Language)} ({OwnName})";
    }
}
