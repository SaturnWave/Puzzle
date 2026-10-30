using System;

namespace Puzzle.Core.Levels
{
    /// <summary>
    /// Which revision of a level's content this is (docs/GAME_DESIGN.md §20). The levels a release ships are at their
    /// first version; a patch that changes a level gives it a higher one, and of two versions of a level the higher
    /// is the one to play. It orders revisions of one level number; it says nothing across levels.
    /// </summary>
    public readonly struct ContentVersion : IEquatable<ContentVersion>, IComparable<ContentVersion>
    {
        /// <summary>The number of the first version, as level files and attempt records write it.</summary>
        public const int FirstValue = 1;

        // Counted from the first version, so that the default value of the type is a valid version: the first.
        private readonly int _revisions;

        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is below <see cref="FirstValue"/>.</exception>
        public ContentVersion(int value)
        {
            _revisions = value >= FirstValue
                ? value - FirstValue
                : throw new ArgumentOutOfRangeException(nameof(value), value, $"Content versions start at {FirstValue}.");
        }

        public static ContentVersion First => default;

        public int Value => _revisions + FirstValue;

        public bool IsFirst => _revisions == 0;

        /// <summary>The version a change to this content gets.</summary>
        public ContentVersion Next() => new ContentVersion(Value + 1);

        public int CompareTo(ContentVersion other) => _revisions.CompareTo(other._revisions);

        public bool Equals(ContentVersion other) => _revisions == other._revisions;

        public override bool Equals(object? obj) => obj is ContentVersion other && Equals(other);

        public override int GetHashCode() => _revisions;

        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool operator ==(ContentVersion left, ContentVersion right) => left.Equals(right);

        public static bool operator !=(ContentVersion left, ContentVersion right) => !left.Equals(right);

        public static bool operator >(ContentVersion left, ContentVersion right) => left.CompareTo(right) > 0;

        public static bool operator <(ContentVersion left, ContentVersion right) => left.CompareTo(right) < 0;

        public static bool operator >=(ContentVersion left, ContentVersion right) => left.CompareTo(right) >= 0;

        public static bool operator <=(ContentVersion left, ContentVersion right) => left.CompareTo(right) <= 0;
    }
}
