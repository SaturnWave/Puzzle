using System;
using System.Collections.Generic;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// The font files whose letters stand in where a design typeface has none: the design's three families draw
    /// Latin only, and every other script the game writes is drawn by a fallback, one a script and a kind of type,
    /// so a line in any style keeps the weight and the manner of its typeface as far as another script can. A closed
    /// set: Korean now; Japanese, Chinese (Simplified and Traditional) and Cyrillic join it in the next step of the
    /// languages. tools/unity/fetch-fonts.js fetches a file for each member and the Editor's LetteringBuilder makes
    /// each ready to draw with the letters the tables of words use. No Unity dependency.
    /// </summary>
    public enum FallbackTypeface
    {
        /// <summary>Noto Sans KR at weight 800: Korean behind Gabarito and Paytone One.</summary>
        NotoSansKR,

        /// <summary>Noto Serif KR at weight 700: Korean behind Young Serif, for titles and names.</summary>
        NotoSerifKR,
    }

    /// <summary>
    /// Which fallbacks stand behind each typeface, in the order they are asked for a letter, and which letters each
    /// draws: the one place that says so. A letter of the game's own set (<see cref="LetterSet"/>) is always the design
    /// typeface's; any other is drawn by the first fallback behind it that covers it.
    /// </summary>
    /// <remarks>
    /// The next step adds a fallback a script: the scripts do not share letters, except the Chinese characters that
    /// Japanese and both Chinese write, which each draws in its own way. Where two fallbacks cover a letter, the
    /// language the game speaks decides which is asked first, and that order is set when the language is.
    /// </remarks>
    public static class FallbackTypefaces
    {
        /// <summary>Every fallback, in the order of the enumeration.</summary>
        public static IReadOnlyList<FallbackTypeface> All { get; } = (FallbackTypeface[])Enum.GetValues(typeof(FallbackTypeface));

        /// <summary>The fallbacks behind a typeface, in the order a letter is looked for in them.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A typeface this list does not know.</exception>
        public static IReadOnlyList<FallbackTypeface> Behind(Typeface typeface) => typeface switch
        {
            Typeface.GabaritoBold => new[] { FallbackTypeface.NotoSansKR },
            Typeface.GabaritoExtraBold => new[] { FallbackTypeface.NotoSansKR },
            Typeface.PaytoneOne => new[] { FallbackTypeface.NotoSansKR },
            Typeface.YoungSerif => new[] { FallbackTypeface.NotoSerifKR },
            _ => throw new ArgumentOutOfRangeException(nameof(typeface), typeface, "A typeface whose fallbacks are not listed."),
        };

        /// <summary>True when a fallback draws a letter: Korean's draw Hangul, its syllables and its letters.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A fallback this list does not know.</exception>
        public static bool Covers(FallbackTypeface fallback, char letter) => fallback switch
        {
            FallbackTypeface.NotoSansKR => IsHangul(letter),
            FallbackTypeface.NotoSerifKR => IsHangul(letter),
            _ => throw new ArgumentOutOfRangeException(nameof(fallback), fallback, "A fallback whose letters are not listed."),
        };

        /// <summary>
        /// The letters a fallback must hold to write some words: those of the words outside the game's own set that it
        /// covers, each once, in the order of their code. Its typeface is made with exactly these.
        /// </summary>
        public static string LettersFor(FallbackTypeface fallback, IEnumerable<string> words)
        {
            var letters = new SortedSet<char>();
            foreach (string each in words)
            {
                foreach (char letter in LetterSet.LettersOutside(each))
                {
                    if (Covers(fallback, letter))
                    {
                        letters.Add(letter);
                    }
                }
            }

            return string.Concat(letters);
        }

        /// <summary>The letters of some words that neither the game's own set nor any fallback draws, each once and in the order they come: what would be drawn as a gap.</summary>
        public static string LettersNoneDraws(string words)
        {
            var none = new List<char>();
            foreach (char letter in LetterSet.LettersOutside(words))
            {
                bool isDrawn = false;
                foreach (FallbackTypeface fallback in All)
                {
                    isDrawn |= Covers(fallback, letter);
                }

                if (!isDrawn && !none.Contains(letter))
                {
                    none.Add(letter);
                }
            }

            return string.Concat(none);
        }

        /// <summary>Hangul syllables (U+AC00 to U+D7A3), and the letters they are made of (U+1100 to U+11FF, U+3130 to U+318F).</summary>
        private static bool IsHangul(char letter) =>
            (letter >= '가' && letter <= '힣') || (letter >= 'ᄀ' && letter <= 'ᇿ') || (letter >= '㄰' && letter <= '㆏');
    }
}
