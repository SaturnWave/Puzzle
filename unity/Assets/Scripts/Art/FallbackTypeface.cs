using System;
using System.Collections.Generic;
using Puzzle.Client.Words;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// The font files whose letters stand in where a design typeface has none: the design's three families draw
    /// Latin only, and every other script the game writes is drawn by a fallback, a sans one behind the sans faces
    /// and a serif one behind the serif, so a line in any style keeps the weight and the manner of its typeface as
    /// far as another script can. Each is made for one language: Korean, Russian, Japanese, and Chinese in its
    /// Simplified and its Traditional writing. Japanese and both Chinese write the same Chinese characters in forms of
    /// their own, which is why each has its own pair. A closed set; tools/unity/fetch-fonts.js fetches a file for each
    /// member and the Editor's LetteringBuilder makes each ready to draw with the letters its language's tables use.
    /// No Unity dependency.
    /// </summary>
    public enum FallbackTypeface
    {
        /// <summary>Noto Sans KR at weight 800: Korean behind Gabarito and Paytone One.</summary>
        NotoSansKR,

        /// <summary>Noto Serif KR at weight 700: Korean behind Young Serif, for titles and names.</summary>
        NotoSerifKR,

        /// <summary>Noto Sans at weight 800: Russian's Cyrillic behind the sans faces.</summary>
        NotoSans,

        /// <summary>Noto Serif at weight 700: Russian's Cyrillic behind Young Serif.</summary>
        NotoSerif,

        /// <summary>Noto Sans JP at weight 700: kana, and Chinese characters in their Japanese forms, behind the sans faces.</summary>
        NotoSansJP,

        /// <summary>Noto Serif JP at weight 700: the same behind Young Serif.</summary>
        NotoSerifJP,

        /// <summary>Noto Sans SC at weight 700: Chinese characters in their Simplified, mainland forms, behind the sans faces.</summary>
        NotoSansSC,

        /// <summary>Noto Serif SC at weight 700: the same behind Young Serif.</summary>
        NotoSerifSC,

        /// <summary>Noto Sans TC at weight 700: Chinese characters in their Traditional forms, as Taiwan writes them, behind the sans faces.</summary>
        NotoSansTC,

        /// <summary>Noto Serif TC at weight 700: the same behind Young Serif.</summary>
        NotoSerifTC,
    }

    /// <summary>
    /// Which fallbacks stand behind each typeface, in the order they are asked for a letter, which letters each draws
    /// and which it holds: the one place that says so. A letter of the game's own set (<see cref="LetterSet"/>) is
    /// always the design typeface's; any other is drawn by the first fallback behind it that holds it.
    /// </summary>
    /// <remarks>
    /// A fallback holds the letters of its own language's tables only, so a Chinese character that Japanese and both
    /// Chinese write is held by each of them that writes it, each in its own form. Which form a line shows follows the
    /// language the game speaks (<see cref="Behind(Typeface, Language)"/>): its own fallback is asked first. Another
    /// language's name on the language card ("简体中文" in Japanese) is drawn by the first fallback that holds its
    /// letters, which is that language's own where the spoken one's lacks them.
    /// </remarks>
    public static class FallbackTypefaces
    {
        /// <summary>Every fallback, in the order of the enumeration: the order in which they are asked where no language puts its own first.</summary>
        public static IReadOnlyList<FallbackTypeface> All { get; } = (FallbackTypeface[])Enum.GetValues(typeof(FallbackTypeface));

        /// <summary>The fallbacks of a typeface's kind (sans or serif), in the order of the enumeration: the order a typeface is kept with.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A typeface this list does not know.</exception>
        public static IReadOnlyList<FallbackTypeface> Behind(Typeface typeface)
        {
            bool isSerif = IsSerif(typeface);
            var behind = new List<FallbackTypeface>();
            foreach (FallbackTypeface fallback in All)
            {
                if (IsSerif(fallback) == isSerif)
                {
                    behind.Add(fallback);
                }
            }

            return behind;
        }

        /// <summary>
        /// The fallbacks behind a typeface while the game speaks a language, in the order a letter is looked for in
        /// them: the language's own first where it has one, the others as <see cref="Behind(Typeface)"/> has them.
        /// Japanese asks Noto Sans JP first, Simplified Chinese Noto Sans SC, Traditional Chinese Noto Sans TC, and
        /// Korean Noto Sans KR.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A typeface this list does not know.</exception>
        public static IReadOnlyList<FallbackTypeface> Behind(Typeface typeface, Language language)
        {
            var behind = new List<FallbackTypeface>(Behind(typeface));
            int own = behind.FindIndex(fallback => LanguageOf(fallback) == language);
            if (own > 0)
            {
                FallbackTypeface first = behind[own];
                behind.RemoveAt(own);
                behind.Insert(0, first);
            }

            return behind;
        }

        /// <summary>The language whose tables a fallback is made with the letters of.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A fallback this list does not know.</exception>
        public static Language LanguageOf(FallbackTypeface fallback) => fallback switch
        {
            FallbackTypeface.NotoSansKR => Language.Korean,
            FallbackTypeface.NotoSerifKR => Language.Korean,
            FallbackTypeface.NotoSans => Language.Russian,
            FallbackTypeface.NotoSerif => Language.Russian,
            FallbackTypeface.NotoSansJP => Language.Japanese,
            FallbackTypeface.NotoSerifJP => Language.Japanese,
            FallbackTypeface.NotoSansSC => Language.ChineseSimplified,
            FallbackTypeface.NotoSerifSC => Language.ChineseSimplified,
            FallbackTypeface.NotoSansTC => Language.ChineseTraditional,
            FallbackTypeface.NotoSerifTC => Language.ChineseTraditional,
            _ => throw new ArgumentOutOfRangeException(nameof(fallback), fallback, "A fallback whose language is not listed."),
        };

        /// <summary>
        /// True when a fallback can draw a letter: Korean's draw Hangul, Russian's Cyrillic, and the Japanese and
        /// Chinese ones Chinese characters, kana and the punctuation the three write in full width.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A fallback this list does not know.</exception>
        public static bool Covers(FallbackTypeface fallback, char letter) => LanguageOf(fallback) switch
        {
            Language.Korean => IsHangul(letter),
            Language.Russian => IsCyrillic(letter),
            Language.Japanese => IsChineseOrJapanese(letter),
            Language.ChineseSimplified => IsChineseOrJapanese(letter),
            Language.ChineseTraditional => IsChineseOrJapanese(letter),
            _ => throw new ArgumentOutOfRangeException(nameof(fallback), fallback, "A fallback whose letters are not listed."),
        };

        /// <summary>
        /// The letters a fallback must hold to write some words of its language: those outside the game's own set
        /// that it covers, each once, in the order of their code. Its typeface is made with exactly these.
        /// </summary>
        public static string LettersFor(FallbackTypeface fallback, IEnumerable<string> wordsOfItsLanguage)
        {
            var letters = new SortedSet<char>();
            foreach (string each in wordsOfItsLanguage)
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

        /// <summary>
        /// The letters of some words of a language that neither the game's own set nor a fallback made for the
        /// language draws, each once and in the order they come: what would be drawn as a gap.
        /// </summary>
        public static string LettersNoneDraws(Language language, string words)
        {
            var none = new List<char>();
            foreach (char letter in LetterSet.LettersOutside(words))
            {
                bool isDrawn = false;
                foreach (FallbackTypeface fallback in All)
                {
                    isDrawn |= LanguageOf(fallback) == language && Covers(fallback, letter);
                }

                if (!isDrawn && !none.Contains(letter))
                {
                    none.Add(letter);
                }
            }

            return string.Concat(none);
        }

        /// <summary>True for the serif typeface and the fallbacks of its kind; the others are sans.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A typeface this list does not know.</exception>
        private static bool IsSerif(Typeface typeface) => typeface switch
        {
            Typeface.GabaritoBold => false,
            Typeface.GabaritoExtraBold => false,
            Typeface.PaytoneOne => false,
            Typeface.YoungSerif => true,
            _ => throw new ArgumentOutOfRangeException(nameof(typeface), typeface, "A typeface whose fallbacks are not listed."),
        };

        private static bool IsSerif(FallbackTypeface fallback) => fallback switch
        {
            FallbackTypeface.NotoSerifKR => true,
            FallbackTypeface.NotoSerif => true,
            FallbackTypeface.NotoSerifJP => true,
            FallbackTypeface.NotoSerifSC => true,
            FallbackTypeface.NotoSerifTC => true,
            _ => false,
        };

        /// <summary>Hangul syllables (U+AC00 to U+D7A3), and the letters they are made of (U+1100 to U+11FF, U+3130 to U+318F).</summary>
        private static bool IsHangul(char letter) =>
            (letter >= '가' && letter <= '힣') || (letter >= 'ᄀ' && letter <= 'ᇿ') || (letter >= '㄰' && letter <= '㆏');

        /// <summary>The Cyrillic block, U+0400 to U+04FF.</summary>
        private static bool IsCyrillic(char letter) => letter >= 'Ѐ' && letter <= 'ӿ';

        /// <summary>
        /// Chinese characters (U+4E00 to U+9FFF, and the first extension, U+3400 to U+4DBF), Japanese kana (U+3040 to
        /// U+30FF), the punctuation and symbols of the three (U+3000 to U+303F), and the letters and marks written in
        /// full width (U+FF00 to U+FFEF: "！", "：", "（").
        /// </summary>
        private static bool IsChineseOrJapanese(char letter) =>
            (letter >= '一' && letter <= '鿿') || (letter >= '㐀' && letter <= '䶿') || (letter >= '぀' && letter <= 'ヿ')
            || (letter >= '　' && letter <= '〿') || (letter >= '＀' && letter <= '￯');
    }
}
