using System;

namespace Puzzle.Museum.Catalogue
{
    /// <summary>
    /// A painting's name in the catalogue: lower-case letters, digits and hyphens ("monet-water-lilies"). It never
    /// changes once players have the painting, because the painting's jigsaw is cut from it.
    /// </summary>
    public readonly struct ArtworkId : IEquatable<ArtworkId>
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private readonly string? _value;

        /// <exception cref="ArgumentException">The text is empty or holds something other than a to z, 0 to 9 and hyphens.</exception>
        public ArtworkId(string value)
        {
            if (value.Length == 0)
            {
                throw new ArgumentException("A painting's id cannot be empty.", nameof(value));
            }

            foreach (char letter in value)
            {
                bool isAllowed = (letter >= 'a' && letter <= 'z') || (letter >= '0' && letter <= '9') || letter == '-';
                if (!isAllowed)
                {
                    throw new ArgumentException($"The painting id \"{value}\" may only hold a to z, 0 to 9 and hyphens.", nameof(value));
                }
            }

            _value = value;
        }

        /// <summary>Empty only for the default value, which names no painting.</summary>
        public string Value => _value ?? string.Empty;

        /// <summary>
        /// The seed the painting's jigsaw is cut with: the same id gives the same pieces on every device and in every
        /// version of the game. It is a 64-bit FNV-1a sum of the id's letters, not the runtime's own string hash,
        /// which differs from one run to the next.
        /// </summary>
        public ulong CutSeed
        {
            get
            {
                ulong sum = FnvOffset;
                foreach (char letter in Value)
                {
                    sum = unchecked((sum ^ letter) * FnvPrime);
                }

                return sum;
            }
        }

        public bool Equals(ArtworkId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is ArtworkId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(ArtworkId left, ArtworkId right) => left.Equals(right);

        public static bool operator !=(ArtworkId left, ArtworkId right) => !left.Equals(right);
    }
}
