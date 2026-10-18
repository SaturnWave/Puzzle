#nullable enable
using System;
using Puzzle.Client.Engine;
using Puzzle.Client.Words;
using UnityEditor;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The language the Editor's tools speak: Turkish, the source, unless the command line says another with
    /// <c>-language en</c> (a code of <see cref="Languages.CodeOf"/>). Every tool builds the game's screens through
    /// the game's own code, which says everything through <see cref="Speech"/>; the language is loaded the first
    /// time a word is asked for, so a tool needs to do nothing for it. A picture's file name ends in the code of a
    /// language other than Turkish (<see cref="FileNameEnding"/>), so the pictures of two languages lie side by side.
    /// </summary>
    public static class ToolLanguage
    {
        private const string Argument = "-language";

        /// <summary>The language the command line names; Turkish when it names none.</summary>
        /// <exception cref="ArgumentException">The command line names a language the game does not speak, or none after the argument.</exception>
        public static Language FromCommandLine
        {
            get
            {
                string[] arguments = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(arguments, Argument);
                if (at < 0)
                {
                    return Language.Turkish;
                }

                string? code = at + 1 < arguments.Length ? arguments[at + 1] : null;
                return Languages.FromCode(code)
                    ?? throw new ArgumentException($"{Argument} names \"{code}\", which is no language the game speaks: write a code such as tr, en or ko.");
            }
        }

        /// <summary>What a picture's file name ends in: nothing in Turkish, "-en" in English, "-ko" in Korean.</summary>
        public static string FileNameEnding => Speech.Language == Language.Turkish ? string.Empty : $"-{Languages.CodeOf(Speech.Language)}";

        /// <summary>The tools speak the command line's language from the first word they say.</summary>
        [InitializeOnLoadMethod]
        private static void SpeakWhenAsked() => Speech.SpeakWhenAsked(() =>
        {
            Language language = FromCommandLine;
            WordsPack.Speak(language);
            return Speech.Current;
        });
    }
}
