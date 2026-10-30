using System;

namespace Puzzle.Museum.Places
{
    /// <summary>A place on the earth: latitude and longitude in degrees (WGS84), north and east counted positive.</summary>
    public readonly struct GeoPoint : IEquatable<GeoPoint>
    {
        /// <exception cref="ArgumentOutOfRangeException">The latitude lies outside -90..90 or the longitude outside -180..180, or either is no number.</exception>
        public GeoPoint(double latitude, double longitude)
        {
            if (!(latitude >= -90.0 && latitude <= 90.0))
            {
                throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "A latitude lies between -90 and 90 degrees.");
            }

            if (!(longitude >= -180.0 && longitude <= 180.0))
            {
                throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "A longitude lies between -180 and 180 degrees.");
            }

            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; }

        public double Longitude { get; }

        public bool Equals(GeoPoint other) => Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);

        public override bool Equals(object? obj) => obj is GeoPoint other && Equals(other);

        public override int GetHashCode() => (Latitude.GetHashCode() * 397) ^ Longitude.GetHashCode();

        public override string ToString() => FormattableString.Invariant($"{Latitude:0.###}, {Longitude:0.###}");

        public static bool operator ==(GeoPoint left, GeoPoint right) => left.Equals(right);

        public static bool operator !=(GeoPoint left, GeoPoint right) => !left.Equals(right);
    }
}
