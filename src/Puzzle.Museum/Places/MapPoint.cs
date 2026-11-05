using System;

namespace Puzzle.Museum.Places
{
    /// <summary>
    /// A point of the world map in its own units (<see cref="NaturalEarthProjection"/>): the map is
    /// <see cref="NaturalEarthProjection.WorldWidth"/> units wide, x grows eastwards from the date line at the left
    /// edge and y grows southwards from the north pole at the top, as a picture is drawn.
    /// </summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public MapPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public bool Equals(MapPoint other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object? obj) => obj is MapPoint other && Equals(other);

        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();

        public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###})");

        public static bool operator ==(MapPoint left, MapPoint right) => left.Equals(right);

        public static bool operator !=(MapPoint left, MapPoint right) => !left.Equals(right);
    }
}
