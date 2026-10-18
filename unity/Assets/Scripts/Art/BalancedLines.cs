#nullable enable
using System;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// How words too long for one line are set on two: broken where the longer of the two lines is the shortest, so
    /// both can be set as large as possible. A line breaks at a space, which the break takes away; words with no
    /// space stay whole on the first line. The width of some words is the caller's measure (a typeface at a size).
    /// No Unity dependency.
    /// </summary>
    /// <remarks>
    /// Japanese and Chinese write no spaces between words; the next step of the languages adds the places such a
    /// line may break between two of its characters, which the same measure then chooses among.
    /// </remarks>
    public static class BalancedLines
    {
        /// <summary>The words broken in two at the space that leaves the longer line the shortest; the words whole and nothing second where they have no space.</summary>
        public static (string First, string Second) SplitInTwo(string words, Func<string, float> widthOf)
        {
            (string First, string Second) best = (words, string.Empty);
            float bestLonger = float.MaxValue;
            for (int space = words.IndexOf(' '); space > 0; space = words.IndexOf(' ', space + 1))
            {
                string first = words.Substring(0, space);
                string second = words.Substring(space + 1);
                float longer = Math.Max(widthOf(first), widthOf(second));
                if (longer < bestLonger)
                {
                    best = (first, second);
                    bestLonger = longer;
                }
            }

            return best;
        }
    }
}
