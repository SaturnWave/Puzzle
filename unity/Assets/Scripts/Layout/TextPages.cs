#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Layout
{
    /// <summary>Which paragraphs of a text one page of a card shows: the first one's index and how many follow it there.</summary>
    public readonly struct TextPage : IEquatable<TextPage>
    {
        public TextPage(int first, int count)
        {
            First = first >= 0 ? first : throw new ArgumentOutOfRangeException(nameof(first), first, "A page begins at a paragraph of the text.");
            Count = count >= 1 ? count : throw new ArgumentOutOfRangeException(nameof(count), count, "A page shows a paragraph at least.");
        }

        public int First { get; }

        public int Count { get; }

        /// <summary>True when the page shows the paragraph of this index.</summary>
        public bool Shows(int paragraph) => paragraph >= First && paragraph < First + Count;

        public bool Equals(TextPage other) => First == other.First && Count == other.Count;

        public override bool Equals(object? obj) => obj is TextPage other && Equals(other);

        public override int GetHashCode() => (First * 397) ^ Count;

        public static bool operator ==(TextPage left, TextPage right) => left.Equals(right);

        public static bool operator !=(TextPage left, TextPage right) => !left.Equals(right);

        public override string ToString() => $"paragraphs {First + 1} to {First + Count}";
    }

    /// <summary>
    /// A text too long for one card, set on pages: its paragraphs in their order, as many on a page as its room holds,
    /// a paragraph never broken over two pages (the privacy card, Screens/PrivacyCard.cs). A paragraph higher than the
    /// room has a page of its own, which is then that much higher; the card sets its type smaller before that happens.
    /// No Unity dependency.
    /// </summary>
    public static class TextPages
    {
        /// <param name="heights">How high each paragraph is set, in its order.</param>
        /// <param name="gap">How far apart two paragraphs of a page stand.</param>
        /// <param name="room">How high a page's text may be.</param>
        /// <exception cref="ArgumentException">There is no paragraph to set.</exception>
        public static IReadOnlyList<TextPage> Of(IReadOnlyList<float> heights, float gap, float room)
        {
            if (heights.Count == 0)
            {
                throw new ArgumentException("A text to set on pages has a paragraph.", nameof(heights));
            }

            var pages = new List<TextPage>();
            int first = 0;
            float filled = heights[0];
            for (int paragraph = 1; paragraph < heights.Count; paragraph++)
            {
                float withIt = filled + gap + heights[paragraph];
                if (withIt <= room)
                {
                    filled = withIt;
                    continue;
                }

                pages.Add(new TextPage(first, paragraph - first));
                first = paragraph;
                filled = heights[paragraph];
            }

            pages.Add(new TextPage(first, heights.Count - first));
            return pages;
        }

        /// <summary>How high a page's text is: its paragraphs and the gaps between them.</summary>
        public static float HeightOf(TextPage page, IReadOnlyList<float> heights, float gap)
        {
            float height = 0f;
            for (int paragraph = page.First; paragraph < page.First + page.Count; paragraph++)
            {
                height += heights[paragraph];
            }

            return height + gap * (page.Count - 1);
        }
    }
}
