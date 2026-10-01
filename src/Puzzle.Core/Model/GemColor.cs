namespace Puzzle.Core.Model
{
    /// <summary>
    /// Gem colors in spawn order: a level with N colors uses the first N values.
    /// </summary>
    public enum GemColor : byte
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Yellow = 3,
        Purple = 4,
        Orange = 5,
    }

    public static class GemColors
    {
        public const int Count = 6;
        public const int MinPerLevel = 3;

        public static GemColor FromIndex(int index) => index >= 0 && index < Count
            ? (GemColor)index
            : throw new System.ArgumentOutOfRangeException(nameof(index), index, "Gem color index must be 0..5.");
    }
}
