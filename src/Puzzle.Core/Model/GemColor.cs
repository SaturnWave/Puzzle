using System;
using System.Collections.Generic;

namespace Puzzle.Core.Model
{
    /// <summary>
    /// Every gem color the game knows. A level picks its own palette from these (default: the first N), so
    /// new colors can be introduced as the player progresses.
    /// </summary>
    public enum GemColor : byte
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Yellow = 3,
        Purple = 4,
        Orange = 5,
        Cyan = 6,
        Pink = 7,
    }

    public static class GemColors
    {
        public const int Count = 8;
        public const int MinPerLevel = 3;

        public static GemColor FromIndex(int index) => index >= 0 && index < Count
            ? (GemColor)index
            : throw new ArgumentOutOfRangeException(nameof(index), index, $"Gem color index must be 0..{Count - 1}.");

        /// <summary>The default palette of a level with <paramref name="count"/> colors.</summary>
        public static IReadOnlyList<GemColor> First(int count)
        {
            if (count < MinPerLevel || count > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, $"A palette has {MinPerLevel}..{Count} colors.");
            }

            var palette = new GemColor[count];
            for (int i = 0; i < count; i++)
            {
                palette[i] = (GemColor)i;
            }

            return palette;
        }
    }
}
