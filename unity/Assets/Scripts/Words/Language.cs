#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Words
{
    /// <summary>
    /// The languages the game is made to speak: the owner's first release (2026-10-18). A closed set; a language
    /// is offered to the player only once its tables are complete (<see cref="Wording.IsCompleteFor"/>). Arabic,
    /// Thai and the others join later, once the lettering can shape them and run them from right to left.
    /// </summary>
    public enum Language
    {
        Turkish,
        English,
        Korean,
        Japanese,
        German,
        French,
        Spanish,

        /// <summary>Brazil's Portuguese, which a phone set to any Portuguese gets.</summary>
        PortugueseBrazil,
        Italian,
        Russian,

        /// <summary>Simplified Chinese, which a phone set to any Chinese but Taiwan's, Hong Kong's and Macao's gets.</summary>
        ChineseSimplified,

        /// <summary>Traditional Chinese: Taiwan's, Hong Kong's and Macao's.</summary>
        ChineseTraditional,
        Indonesian,
    }

    /// <summary>
    /// What the game knows of each language without its tables: the code its files are named by, the language a
    /// phone's setting means, how it writes a large number and which form of a counted word a number takes.
    /// No Unity dependency.
    /// </summary>
    public static class Languages
    {
        /// <summary>The language the game speaks where the phone's is none it is made to speak, or none it offers yet.</summary>
        public const Language Fallback = Language.English;

        /// <summary>Every language, in the order of the enumeration.</summary>
        public static IReadOnlyList<Language> All { get; } = (Language[])Enum.GetValues(typeof(Language));

        /// <summary>The code a language's files are named by (words/en.json, museum/titles/en.json) and the device file keeps it by: a BCP 47 tag.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A language the list does not know.</exception>
        public static string CodeOf(Language language) => language switch
        {
            Language.Turkish => "tr",
            Language.English => "en",
            Language.Korean => "ko",
            Language.Japanese => "ja",
            Language.German => "de",
            Language.French => "fr",
            Language.Spanish => "es",
            Language.PortugueseBrazil => "pt-BR",
            Language.Italian => "it",
            Language.Russian => "ru",
            Language.ChineseSimplified => "zh-Hans",
            Language.ChineseTraditional => "zh-Hant",
            Language.Indonesian => "id",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "A language without a code."),
        };

        /// <summary>The language a code names, as <see cref="CodeOf"/> writes it; nothing for any other text, an empty one included.</summary>
        public static Language? FromCode(string? code)
        {
            foreach (Language language in All)
            {
                if (string.Equals(CodeOf(language), code, StringComparison.Ordinal))
                {
                    return language;
                }
            }

            return null;
        }

        /// <summary>
        /// The language a phone set to a language and region means (a BCP 47 tag such as "pt-PT", "zh-Hant-TW",
        /// "zh_HK" or Java's old "in" for Indonesian): Brazil's Portuguese for any Portuguese, Traditional Chinese for
        /// Taiwan, Hong Kong, Macao and a tag that says "Hant", Simplified Chinese for any other Chinese, and
        /// <see cref="Fallback"/> for a language the game is not made to speak.
        /// </summary>
        public static Language FromPhone(string? tag)
        {
            string[] parts = (tag ?? string.Empty).Replace('_', '-').ToLowerInvariant().Split('-');
            switch (parts[0])
            {
                case "tr": return Language.Turkish;
                case "en": return Language.English;
                case "ko": return Language.Korean;
                case "ja": return Language.Japanese;
                case "de": return Language.German;
                case "fr": return Language.French;
                case "es": return Language.Spanish;
                case "pt": return Language.PortugueseBrazil;
                case "it": return Language.Italian;
                case "ru": return Language.Russian;
                case "id":
                case "in": return Language.Indonesian;
                case "zh": return IsTraditionalChinese(parts) ? Language.ChineseTraditional : Language.ChineseSimplified;
                default: return Fallback;
            }
        }

        /// <summary>
        /// The language the game speaks: the one the player chose on the language card, or else the phone's; either
        /// only where the game offers it, and <see cref="Fallback"/> otherwise.
        /// </summary>
        /// <param name="chosen">The player's choice; nothing for the phone's language.</param>
        /// <param name="offered">The languages whose tables are complete in this build.</param>
        public static Language InEffect(Language? chosen, Language phone, IReadOnlyCollection<Language> offered)
        {
            if (chosen is Language choice && Contains(offered, choice))
            {
                return choice;
            }

            return Contains(offered, phone) ? phone : Fallback;
        }

        /// <summary>
        /// The mark between the thousands of an amount: "1.250" in Turkish, "1,250" in English and Korean, "1 250"
        /// with a space that does not break the number in French and Russian.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A language the list does not know.</exception>
        public static char ThousandsMarkOf(Language language) => language switch
        {
            Language.Turkish => '.',
            Language.German => '.',
            Language.Spanish => '.',
            Language.PortugueseBrazil => '.',
            Language.Italian => '.',
            Language.Indonesian => '.',
            Language.English => ',',
            Language.Korean => ',',
            Language.Japanese => ',',
            Language.ChineseSimplified => ',',
            Language.ChineseTraditional => ',',
            Language.French => ' ',
            Language.Russian => ' ',
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "A language whose numbers are not described."),
        };

        /// <summary>
        /// The form of a counted word that a whole number takes in a language, by the Unicode CLDR's rules for whole
        /// numbers: English "1 coin" and "2 coins"; Russian "1 монета", "2 монеты", "5 монет"; Korean, Japanese,
        /// Chinese and Indonesian one form for all.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A count under nothing, or a language the list does not know.</exception>
        public static PluralForm FormOf(Language language, long count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The game counts nothing under none.");
            }

            switch (language)
            {
                case Language.Korean:
                case Language.Japanese:
                case Language.ChineseSimplified:
                case Language.ChineseTraditional:
                case Language.Indonesian:
                    return PluralForm.Other;
                case Language.Turkish:
                case Language.English:
                case Language.German:
                case Language.Spanish:
                case Language.Italian:
                    return count == 1 ? PluralForm.One : PluralForm.Other;
                case Language.French:
                case Language.PortugueseBrazil:
                    return count <= 1 ? PluralForm.One : PluralForm.Other;
                case Language.Russian:
                    return RussianFormOf(count);
                default:
                    throw new ArgumentOutOfRangeException(nameof(language), language, "A language whose counted words are not described.");
            }
        }

        private static PluralForm RussianFormOf(long count)
        {
            long lastDigit = count % 10;
            long lastTwo = count % 100;
            if (lastDigit == 1 && lastTwo != 11)
            {
                return PluralForm.One;
            }

            bool endsInTwoToFour = lastDigit >= 2 && lastDigit <= 4;
            bool endsInTwelveToFourteen = lastTwo >= 12 && lastTwo <= 14;
            return endsInTwoToFour && !endsInTwelveToFourteen ? PluralForm.Few : PluralForm.Many;
        }

        private static bool IsTraditionalChinese(string[] parts)
        {
            foreach (string part in parts)
            {
                if (part == "hant" || part == "tw" || part == "hk" || part == "mo")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyCollection<Language> languages, Language language)
        {
            foreach (Language each in languages)
            {
                if (each == language)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The forms a counted word takes, as the Unicode CLDR names them for whole numbers. A closed set: a language
    /// uses some of them (<see cref="Languages.FormOf"/>), and a table gives at least <see cref="Other"/>.
    /// </summary>
    public enum PluralForm
    {
        /// <summary>English "1 coin"; Russian also "21 монета".</summary>
        One,

        /// <summary>Russian "2 монеты", "24 монеты".</summary>
        Few,

        /// <summary>Russian "5 монет", "11 монет".</summary>
        Many,

        /// <summary>Every count a language gives no form of its own: English "2 coins"; every count in Korean.</summary>
        Other,
    }
}
