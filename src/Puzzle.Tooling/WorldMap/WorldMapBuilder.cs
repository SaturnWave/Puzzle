using System.Text.Json;
using Puzzle.Museum.Places;

namespace Puzzle.Tooling.WorldMap;

/// <summary>How the world map's coastlines are thinned and which bits of land and water are left out.</summary>
/// <param name="Tolerance">The Douglas-Peucker tolerance, in map units: a point closer than this to the line its neighbours keep is dropped.</param>
/// <param name="LeastLandArea">An island smaller than this many square map units is dropped, as a hole in the land is.</param>
/// <param name="LeastLakeArea">A lake smaller than this is dropped.</param>
/// <param name="AntarcticLatitude">Land whose outer ring lies south of this latitude on average is dropped: no painting is there, and it would only take the map's foot.</param>
public sealed record WorldMapSettings(double Tolerance, double LeastLandArea, double LeastLakeArea, double AntarcticLatitude)
{
    /// <summary>The design canvas' own (out/design/worldmap-src/build_geo.py).</summary>
    public static WorldMapSettings OfTheDesign { get; } = new(Tolerance: 0.035, LeastLandArea: 0.6, LeastLakeArea: 3.0, AntarcticLatitude: -62.0);
}

/// <summary>What a build of the map made, and what it left out, in figures the build reports.</summary>
public sealed record WorldMapBuild(
    WorldMapShape Map,
    int LandPolygons,
    int LakePolygons,
    int RingsDropped,
    int PolygonsDropped,
    double AreaAsTriangles,
    double AreaAsRings);

/// <summary>
/// Turns Natural Earth's land and lakes (public domain, naturalearthdata.com; the 1:50m GeoJSON files of
/// github.com/nvkelso/natural-earth-vector) into the world map the game draws: projected by Natural Earth I
/// (<see cref="NaturalEarthProjection"/>), thinned by Douglas-Peucker, the smallest islands and lakes and
/// Antarctica left out as the design canvas leaves them out, rounded to the map file's twentieths of a unit, and
/// every polygon cut into triangles (<see cref="Earcut"/>) after rounding, so the triangles fill exactly the rings
/// the file stores.
/// </summary>
public static class WorldMapBuilder
{
    public static WorldMapBuild Build(string landGeoJson, string lakesGeoJson, WorldMapSettings settings)
    {
        var land = new OutlineBuilder();
        var lakes = new OutlineBuilder();
        foreach (List<List<(double Lon, double Lat)>> polygon in PolygonsOf(landGeoJson))
        {
            if (AverageLatitude(polygon[0]) < settings.AntarcticLatitude)
            {
                land.PolygonsDropped++;
                continue;
            }

            land.Add(polygon, settings.Tolerance, settings.LeastLandArea);
        }

        foreach (List<List<(double Lon, double Lat)>> polygon in PolygonsOf(lakesGeoJson))
        {
            lakes.Add(polygon, settings.Tolerance, settings.LeastLakeArea);
        }

        return new WorldMapBuild(
            new WorldMapShape(land.ToOutline(), lakes.ToOutline()),
            land.Polygons,
            lakes.Polygons,
            land.RingsDropped + lakes.RingsDropped,
            land.PolygonsDropped + lakes.PolygonsDropped,
            land.AreaAsTriangles + lakes.AreaAsTriangles,
            land.AreaAsRings + lakes.AreaAsRings);
    }

    /// <summary>Douglas-Peucker on an open run of points: the first and last stay, and every point that strays further than the tolerance from the line between those kept.</summary>
    public static List<(double X, double Y)> Simplify(IReadOnlyList<(double X, double Y)> points, double tolerance)
    {
        if (points.Count < 4)
        {
            return points.ToList();
        }

        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int A, int B)>();
        stack.Push((0, points.Count - 1));
        while (stack.Count > 0)
        {
            (int a, int b) = stack.Pop();
            (double ax, double ay) = points[a];
            (double bx, double by) = points[b];
            double dx = bx - ax, dy = by - ay;
            double length = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1e-12);
            double farthest = -1;
            int at = -1;
            for (int i = a + 1; i < b; i++)
            {
                double distance = Math.Abs(dy * points[i].X - dx * points[i].Y + bx * ay - by * ax) / length;
                if (distance > farthest)
                {
                    farthest = distance;
                    at = i;
                }
            }

            if (farthest > tolerance)
            {
                keep[at] = true;
                stack.Push((a, at));
                stack.Push((at, b));
            }
        }

        return points.Where((_, i) => keep[i]).ToList();
    }

    /// <summary>The area a ring encloses, whichever way it runs.</summary>
    public static double AreaOf(IReadOnlyList<(double X, double Y)> ring)
    {
        double sum = 0;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            sum += ring[j].X * ring[i].Y - ring[i].X * ring[j].Y;
        }

        return Math.Abs(sum) / 2;
    }

    /// <summary>Every polygon of a GeoJSON feature collection, outer ring first, each ring as longitude and latitude.</summary>
    private static IEnumerable<List<List<(double Lon, double Lat)>>> PolygonsOf(string geoJson)
    {
        using JsonDocument document = JsonDocument.Parse(geoJson);
        foreach (JsonElement feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            JsonElement geometry = feature.GetProperty("geometry");
            string type = geometry.GetProperty("type").GetString() ?? string.Empty;
            JsonElement coordinates = geometry.GetProperty("coordinates");
            switch (type)
            {
                case "Polygon":
                    yield return RingsOf(coordinates);
                    break;
                case "MultiPolygon":
                    foreach (JsonElement polygon in coordinates.EnumerateArray())
                    {
                        yield return RingsOf(polygon);
                    }

                    break;
                default:
                    throw new InvalidDataException($"A feature of the map's sources is a {type}; the map reads polygons only.");
            }
        }
    }

    private static List<List<(double Lon, double Lat)>> RingsOf(JsonElement polygon) =>
        polygon.EnumerateArray()
            .Select(ring => ring.EnumerateArray().Select(point => (point[0].GetDouble(), point[1].GetDouble())).ToList())
            .ToList();

    private static double AverageLatitude(IReadOnlyList<(double Lon, double Lat)> ring) => ring.Average(point => point.Lat);

    /// <summary>The points, rings and triangles of one outline, polygon by polygon.</summary>
    private sealed class OutlineBuilder
    {
        private readonly List<MapPoint> _points = new();
        private readonly List<RingSpan> _rings = new();
        private readonly List<int> _triangles = new();

        public int Polygons { get; private set; }

        public int RingsDropped { get; set; }

        public int PolygonsDropped { get; set; }

        public double AreaAsTriangles { get; private set; }

        public double AreaAsRings { get; private set; }

        public void Add(List<List<(double Lon, double Lat)>> polygon, double tolerance, double leastArea)
        {
            var rings = new List<List<(double X, double Y)>>();
            for (int index = 0; index < polygon.Count; index++)
            {
                List<(double X, double Y)>? ring = Prepared(polygon[index], tolerance, leastArea);
                if (ring == null)
                {
                    if (index == 0)
                    {
                        PolygonsDropped++;
                        return;
                    }

                    RingsDropped++;
                    continue;
                }

                rings.Add(ring);
            }

            var flat = new List<(double X, double Y)>();
            var holeStarts = new List<int>();
            foreach (List<(double X, double Y)> ring in rings)
            {
                if (flat.Count > 0)
                {
                    holeStarts.Add(flat.Count);
                }

                _rings.Add(new RingSpan(_points.Count + flat.Count, ring.Count));
                flat.AddRange(ring);
            }

            List<int> triangles = Earcut.Triangulate(flat, holeStarts);
            int offset = _points.Count;
            _points.AddRange(flat.Select(point => new MapPoint((float)point.X, (float)point.Y)));
            _triangles.AddRange(triangles.Select(index => offset + index));
            AreaAsRings += AreaOf(rings[0]) - rings.Skip(1).Sum(AreaOf);
            for (int t = 0; t < triangles.Count; t += 3)
            {
                AreaAsTriangles += AreaOf(new[] { flat[triangles[t]], flat[triangles[t + 1]], flat[triangles[t + 2]] });
            }

            Polygons++;
        }

        public MapOutline ToOutline() => new(_points, _rings, _triangles);

        /// <summary>A ring projected, thinned and rounded as the file stores it; nothing for one too small or too thin to keep.</summary>
        private static List<(double X, double Y)>? Prepared(IReadOnlyList<(double Lon, double Lat)> ring, double tolerance, double leastArea)
        {
            List<(double X, double Y)> projected = ring.Select(point => NaturalEarthProjection.Project(point.Lon, point.Lat)).Select(point => ((double)point.X, (double)point.Y)).ToList();
            if (projected.Count > 1 && projected[0] == projected[^1])
            {
                projected.RemoveAt(projected.Count - 1);
            }

            if (AreaOf(projected) < leastArea)
            {
                return null;
            }

            var rounded = new List<(double X, double Y)>();
            foreach ((double x, double y) in Simplify(projected, tolerance))
            {
                (double X, double Y) point = (WorldMapFormat.Quantized((float)x) / (double)WorldMapFormat.Scale, WorldMapFormat.Quantized((float)y) / (double)WorldMapFormat.Scale);
                if (rounded.Count == 0 || rounded[^1] != point)
                {
                    rounded.Add(point);
                }
            }

            while (rounded.Count > 1 && rounded[0] == rounded[^1])
            {
                rounded.RemoveAt(rounded.Count - 1);
            }

            return rounded.Count >= 3 ? rounded : null;
        }
    }
}
