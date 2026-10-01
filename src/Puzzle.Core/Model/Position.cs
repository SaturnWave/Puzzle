using System;

namespace Puzzle.Core.Model
{
    /// <summary>A cell coordinate. <see cref="X"/> is the column (0 = left), <see cref="Y"/> the row (0 = top, where tiles spawn).</summary>
    public readonly struct Position : IEquatable<Position>
    {
        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool IsAdjacentTo(Position other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y) == 1;

        public bool Equals(Position other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is Position other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X},{Y})";

        public static bool operator ==(Position left, Position right) => left.Equals(right);

        public static bool operator !=(Position left, Position right) => !left.Equals(right);
    }
}
