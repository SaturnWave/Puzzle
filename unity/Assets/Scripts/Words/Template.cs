#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Puzzle.Client.Words
{
    /// <summary>
    /// A table's words with places in them for what changes: "Bölüm {level}", "Reklam izle, {coins} altın kazan".
    /// A place is a name of letters between braces; the words have no brace of their own. No Unity dependency.
    /// </summary>
    public static class Template
    {
        /// <summary>The words with each place filled by the value of its name.</summary>
        /// <exception cref="FormatException">A place has no value among those given, or a brace is not closed.</exception>
        public static string Fill(string words, IReadOnlyList<(string Name, string Value)> filling)
        {
            if (words.IndexOf('{') < 0)
            {
                return words;
            }

            var filled = new StringBuilder(words.Length + 16);
            int at = 0;
            while (at < words.Length)
            {
                int opens = words.IndexOf('{', at);
                if (opens < 0)
                {
                    filled.Append(words, at, words.Length - at);
                    break;
                }

                int closes = words.IndexOf('}', opens + 1);
                if (closes < 0)
                {
                    throw new FormatException($"\"{words}\" opens a place it does not close.");
                }

                filled.Append(words, at, opens - at);
                filled.Append(ValueOf(words.Substring(opens + 1, closes - opens - 1), filling, words));
                at = closes + 1;
            }

            return filled.ToString();
        }

        /// <summary>The names of the places in some words, each once, in the order they first come.</summary>
        /// <exception cref="FormatException">A brace is not closed, or a place has no name of letters.</exception>
        public static IReadOnlyList<string> PlacesIn(string words)
        {
            var places = new List<string>();
            int at = 0;
            while ((at = words.IndexOf('{', at)) >= 0)
            {
                int closes = words.IndexOf('}', at + 1);
                if (closes < 0)
                {
                    throw new FormatException($"\"{words}\" opens a place it does not close.");
                }

                string name = words.Substring(at + 1, closes - at - 1);
                if (!IsAName(name))
                {
                    throw new FormatException($"\"{words}\" has a place \"{{{name}}}\" that is not named with letters.");
                }

                if (!places.Contains(name))
                {
                    places.Add(name);
                }

                at = closes + 1;
            }

            return places;
        }

        private static string ValueOf(string name, IReadOnlyList<(string Name, string Value)> filling, string words)
        {
            foreach ((string Name, string Value) each in filling)
            {
                if (each.Name == name)
                {
                    return each.Value;
                }
            }

            throw new FormatException($"\"{words}\" has a place \"{{{name}}}\" that nothing fills.");
        }

        private static bool IsAName(string name)
        {
            if (name.Length == 0)
            {
                return false;
            }

            foreach (char letter in name)
            {
                if (!(letter >= 'a' && letter <= 'z') && !(letter >= 'A' && letter <= 'Z'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
