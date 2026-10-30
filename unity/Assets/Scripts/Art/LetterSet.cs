using System.Collections.Generic;
using System.Text;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// The letters the game can write. Every typeface is made ready with exactly these, and a build carries no font
    /// file to draw another from: words with a letter outside the set would be drawn with a gap in them. The set is
    /// the same for every typeface, so any words may be set in any style. No Unity dependency: the Editor makes the
    /// typefaces from it and the tests hold the game's words against it.
    /// </summary>
    public static class LetterSet
    {
        private const char FirstPlainLetter = ' ';
        private const char LastPlainLetter = '~';

        /// <summary>
        /// The letters of the languages of Western Europe that plain Latin lacks: a painter keeps the letters of
        /// their name (Cézanne, Dürer, Velázquez), and Turkish takes its ç, ö, ü and its circumflexed vowels from
        /// here. The two signs among them, × and ÷, are left out; × is one of the marks below.
        /// </summary>
        private const char FirstWesternLetter = 'À';
        private const char LastWesternLetter = 'ÿ';
        private const string SignsAmongTheWesternLetters = "×÷";

        /// <summary>What Turkish needs beyond those.</summary>
        private const string TurkishLetters = "ĞİŞğış";

        /// <summary>
        /// What the other Latin languages of the tables need beyond those: Spanish opens a question and an
        /// exclamation upside down ("¡Nivel superado!"), French ties two letters in "Sœurs" (its capital for a line
        /// set in capitals), and Russian quotes a name between guillemets («Мулен Руж»), which its fallback leaves to
        /// the design's typefaces like every mark of plain Latin.
        /// </summary>
        private const string OtherLatinLetters = "¡¿œŒ«»";

        /// <summary>
        /// A space at which no line breaks: French sets it before "!", "?", ":" and ";", and French and Russian between
        /// the thousands of an amount ("1 250").
        /// </summary>
        public const char NoBreakSpace = ' ';

        /// <summary>The marks the game's sentences use, and "∞", which the tester mode shows for its coins and lives.</summary>
        private const string Marks = "·…’“”–×∞";

        /// <summary>Every letter of the set, each once.</summary>
        public static string All { get; } = Build();

        /// <summary>The letters of some words that the set lacks, each once and in the order they come; no letters at all when the words can be written.</summary>
        public static string LettersOutside(string words)
        {
            var outside = new StringBuilder();
            foreach (char letter in words)
            {
                bool isALineBreak = letter == '\n'; // a button's label on two lines is written as two lines
                if (!isALineBreak && All.IndexOf(letter) < 0 && outside.ToString().IndexOf(letter) < 0)
                {
                    outside.Append(letter);
                }
            }

            return outside.ToString();
        }

        private static string Build()
        {
            var letters = new List<char>();
            for (char letter = FirstPlainLetter; letter <= LastPlainLetter; letter++)
            {
                letters.Add(letter);
            }

            for (char letter = FirstWesternLetter; letter <= LastWesternLetter; letter++)
            {
                if (SignsAmongTheWesternLetters.IndexOf(letter) < 0)
                {
                    letters.Add(letter);
                }
            }

            letters.AddRange(TurkishLetters);
            letters.AddRange(OtherLatinLetters);
            letters.Add(NoBreakSpace);
            letters.AddRange(Marks);
            return new string(letters.ToArray());
        }
    }
}
