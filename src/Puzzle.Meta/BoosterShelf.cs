using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Meta
{
    /// <summary>
    /// Boosters by kind: the ones a player holds, or the ones a set of the shop gives. A shelf never changes: taking
    /// or adding one gives another shelf, so a wallet that was read stays what it was.
    /// </summary>
    public sealed class BoosterShelf : IEquatable<BoosterShelf>
    {
        /// <summary>One count per kind, in the order of <see cref="BoosterKind"/>; none is negative.</summary>
        private readonly int[] _counts;

        private BoosterShelf(int[] counts)
        {
            _counts = counts;
        }

        public static BoosterShelf Empty { get; } = new BoosterShelf(new int[BoosterKinds.All.Count]);

        public int Total => _counts.Sum();

        public bool IsEmpty => Total == 0;

        public int Count(BoosterKind kind) => _counts[(int)kind];

        public bool Has(BoosterKind kind) => Count(kind) > 0;

        public BoosterShelf WithOneMore(BoosterKind kind) => With(kind, more: 1);

        /// <exception cref="ArgumentOutOfRangeException"><paramref name="more"/> is negative: a booster leaves a shelf one at a time (<see cref="WithOneFewer"/>).</exception>
        public BoosterShelf With(BoosterKind kind, int more)
        {
            if (more < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(more), more, "Boosters are added in numbers of none or more.");
            }

            int[] counts = (int[])_counts.Clone();
            counts[(int)kind] = checked(counts[(int)kind] + more);
            return new BoosterShelf(counts);
        }

        /// <summary>This shelf with every booster of <paramref name="other"/> added: what a bought set leaves behind.</summary>
        public BoosterShelf Plus(BoosterShelf other)
        {
            BoosterShelf sum = this;
            foreach (BoosterKind kind in BoosterKinds.All)
            {
                sum = sum.With(kind, other.Count(kind));
            }

            return sum;
        }

        /// <exception cref="InvalidOperationException">The shelf holds none of the kind.</exception>
        public BoosterShelf WithOneFewer(BoosterKind kind)
        {
            if (!Has(kind))
            {
                throw new InvalidOperationException($"No {kind.Name()} is held.");
            }

            int[] counts = (int[])_counts.Clone();
            counts[(int)kind]--;
            return new BoosterShelf(counts);
        }

        public bool Equals(BoosterShelf? other) => other is not null && _counts.SequenceEqual(other._counts);

        public override bool Equals(object? obj) => Equals(obj as BoosterShelf);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (int count in _counts)
            {
                hash.Add(count);
            }

            return hash.ToHashCode();
        }

        public override string ToString()
        {
            IEnumerable<string> held = BoosterKinds.All.Where(Has).Select(kind => $"{Count(kind)} {kind.Name()}");
            return IsEmpty ? "no booster" : string.Join(", ", held);
        }
    }
}
