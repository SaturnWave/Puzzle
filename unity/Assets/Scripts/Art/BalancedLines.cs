#nullable enable
using System;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// How words too long for one line are set on two: broken where the longer of the two lines is the shortest, so
    /// both can be set as large as possible. A line breaks at a space, which the break takes away, or, in Japanese and
    /// Chinese, between two letters where TextMesh Pro would break it (<see cref="LineBreaks"/>); words with no such
    /// place stay whole on the first line. The width of some words is the caller's measure (a typeface at a size).
    /// No Unity dependency.
    /// </summary>
    /// <remarks>
    /// Japanese and Chinese may break between any two letters of a word, but a reader pauses after a mark that ends a
    /// clause ("、", "，", "：", "・"): a break there is taken where its longer line is at most a quarter longer than
    /// that of the best break of all ("エリザベス・ファーレン、" over "後のダービー伯爵夫人", not "…ファーレ" over
    /// "ン、…"). A space is always such a pause.
    /// </remarks>
    public static class BalancedLines
    {
        /// <summary>How much longer the longer line of a break at a pause may be than that of the best break of all, and still be taken.</summary>
        private const float LongerForAPause = 1.25f;

        /// <summary>The marks after which a reader of Japanese or Chinese pauses: the ends of clauses and of quotes.</summary>
        private const string Pauses = "、，。：；！？・）」』】》";

        /// <summary>The words broken in two at the place that leaves the longer line the shortest, a pause preferred; the words whole and nothing second where they have no such place.</summary>
        public static (string First, string Second) SplitInTwo(string words, Func<string, float> widthOf)
        {
            (string First, string Second) best = (words, string.Empty);
            (string First, string Second) bestAtAPause = best;
            float bestLonger = float.MaxValue;
            float bestLongerAtAPause = float.MaxValue;
            for (int at = 1; at < words.Length; at++)
            {
                if (SplitAt(words, at) is (string first, string second))
                {
                    float longer = Math.Max(widthOf(first), widthOf(second));
                    if (longer < bestLonger)
                    {
                        best = (first, second);
                        bestLonger = longer;
                    }

                    bool isAtAPause = words[at] == ' ' || Pauses.IndexOf(words[at - 1]) >= 0;
                    if (isAtAPause && longer < bestLongerAtAPause)
                    {
                        bestAtAPause = (first, second);
                        bestLongerAtAPause = longer;
                    }
                }
            }

            return bestLongerAtAPause <= bestLonger * LongerForAPause ? bestAtAPause : best;
        }

        /// <summary>The two lines of words broken before the letter at a place: a space there is taken away; nothing where no line may break there.</summary>
        private static (string First, string Second)? SplitAt(string words, int at)
        {
            if (words[at] == ' ')
            {
                return at + 1 < words.Length ? (words.Substring(0, at), words.Substring(at + 1)) : ((string, string)?)null;
            }

            return LineBreaks.MayBreakBetween(words[at - 1], words[at]) ? (words.Substring(0, at), words.Substring(at)) : ((string, string)?)null;
        }
    }
}
