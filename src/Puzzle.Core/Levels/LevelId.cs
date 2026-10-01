using System;

namespace Puzzle.Core.Levels
{
    /// <summary>1-based level number.</summary>
    public readonly struct LevelId : IEquatable<LevelId>
    {
        public LevelId(int value)
        {
            Value = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Level ids start at 1.");
        }

        public int Value { get; }

        public bool Equals(LevelId other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is LevelId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool operator ==(LevelId left, LevelId right) => left.Equals(right);

        public static bool operator !=(LevelId left, LevelId right) => !left.Equals(right);
    }
}
