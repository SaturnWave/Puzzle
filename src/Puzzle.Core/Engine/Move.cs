using System;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine
{
    public enum MoveKind : byte
    {
        /// <summary>Drag the tile at <see cref="Move.From"/> onto the adjacent <see cref="Move.To"/>.</summary>
        Swap,

        /// <summary>Activate the special at <see cref="Move.From"/> in place.</summary>
        Tap,
    }

    /// <summary>A player action. Constructing one only checks its shape; legality depends on the board.</summary>
    public readonly struct Move : IEquatable<Move>
    {
        private Move(MoveKind kind, Position from, Position to)
        {
            Kind = kind;
            From = from;
            To = to;
        }

        public MoveKind Kind { get; }

        public Position From { get; }

        /// <summary>The swap destination; equals <see cref="From"/> for a tap.</summary>
        public Position To { get; }

        public static Move Swap(Position from, Position to) => from.IsAdjacentTo(to)
            ? new Move(MoveKind.Swap, from, to)
            : throw new ArgumentException($"Swap cells {from} and {to} are not adjacent.", nameof(to));

        public static Move Tap(Position at) => new Move(MoveKind.Tap, at, at);

        public bool Equals(Move other) => Kind == other.Kind && From == other.From && To == other.To;

        public override bool Equals(object? obj) => obj is Move other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, From, To);

        public override string ToString() => Kind == MoveKind.Tap ? $"Tap{From}" : $"Swap{From}->{To}";

        public static bool operator ==(Move left, Move right) => left.Equals(right);

        public static bool operator !=(Move left, Move right) => !left.Equals(right);
    }
}
