using System;

namespace Puzzle.Museum.Places
{
    /// <summary>
    /// The world map's projection: Natural Earth I (Šavrič, Jenny, Patterson and Jenny, 2011), the one the design
    /// canvas draws its map in (out/design/worldmap-src/build_geo.py). The map is <see cref="WorldWidth"/> units wide,
    /// the date line at both edges, the north pole on its top edge, y growing southwards. The coastlines the game
    /// carries were projected by the same code (the CLI's <c>worldmap build</c>), so a painting's pin and the shore
    /// it stands on can never drift apart.
    /// </summary>
    public static class NaturalEarthProjection
    {
        public const float WorldWidth = 1000f;

        /// <summary>The projection's x at the equator for a longitude of pi, which <see cref="WorldWidth"/> stretches to half the map.</summary>
        private const double EquatorFactor = 0.8707;

        private static readonly double UnitsPerRadian = WorldWidth / (2.0 * Math.PI * EquatorFactor);

        /// <summary>How far the north pole lies above the equator, in map units: the equator's y.</summary>
        public static readonly float Top = (float)(Raw(0.0, 90.0).Y * UnitsPerRadian);

        /// <summary>Where a place of the earth lies on the map.</summary>
        public static MapPoint Project(GeoPoint place) => Project(place.Longitude, place.Latitude);

        /// <summary>Where a longitude and latitude, in degrees, lie on the map: the same as <see cref="Project(GeoPoint)"/>, for the many points of a coastline.</summary>
        public static MapPoint Project(double longitude, double latitude)
        {
            (double x, double y) = Raw(longitude, latitude);
            return new MapPoint((float)(WorldWidth / 2.0 + x * UnitsPerRadian), (float)(Top - y * UnitsPerRadian));
        }

        /// <summary>The published polynomials of Natural Earth I, in radians of the unit sphere, y growing northwards.</summary>
        private static (double X, double Y) Raw(double longitude, double latitude)
        {
            double lambda = longitude * Math.PI / 180.0;
            double phi = latitude * Math.PI / 180.0;
            double phi2 = phi * phi;
            double phi4 = phi2 * phi2;
            double x = lambda * (EquatorFactor - 0.131979 * phi2 + phi4 * (-0.013791 + phi4 * (0.003971 * phi2 - 0.001529 * phi4)));
            double y = phi * (1.007226 + phi2 * (0.015085 + phi4 * (-0.044475 + 0.028874 * phi2 - 0.005916 * phi4)));
            return (x, y);
        }
    }
}
