#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Engine;
using Puzzle.Museum.Places;
using UnityEngine;
using UnityEngine.Rendering;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// The world map's meshes, in the map's units with y turned upwards (a point of the map at (x, y) lies at (x, -y)),
    /// built once from the coastlines the game carries and shared by every map page after that: the land's and the
    /// lakes' triangles in the colours of their corners, the coastlines and the lakes' shores as lines, and the
    /// graticule. A line's mesh holds each point twice, with the mitre that leads out to either side and how far
    /// along its line it lies (MapLine.shader). The cache holds exactly these five meshes, the one map the game has,
    /// and never more; they are made when a map is first shown and kept while the game runs.
    /// </summary>
    public static class WorldMapMeshes
    {
        /// <summary>A mitre longer than this many times the line's half width is cut short: a sharp corner of a jagged coast is drawn a little thinner rather than as a spike.</summary>
        private const float LongestMitre = 1.5f;

        /// <summary>A line's corners go out by up to a few points from the line, which the shader adds: the meshes' bounds are kept this much larger, in map units, so nothing is culled at the sheet's edge.</summary>
        private const float BoundsRoom = 60f;

        /// <summary>The land's cream from north to south (MapWorldTR: #FBF2DA, #F3E5C2 at 55 %, #E7D3A4).</summary>
        private static readonly (float At, Color32 Colour)[] LandColours =
        {
            (0f, new Color32(0xFB, 0xF2, 0xDA, 0xFF)),
            (0.55f, new Color32(0xF3, 0xE5, 0xC2, 0xFF)),
            (1f, new Color32(0xE7, 0xD3, 0xA4, 0xFF)),
        };

        private static readonly Color32 LakeColour = new Color32(0x2A, 0x8A, 0x90, 0xFF);

        /// <summary>The graticule (MapWorldTR): a meridian every 30 degrees from 60 south to 80 north, and the parallels at 60, 30 and 0.</summary>
        private const int MeridianEvery = 30;
        private const int SouthOfTheGraticule = -60;
        private const int NorthOfTheGraticule = 80;
        private static readonly int[] Parallels = { -60, -30, 0, 30, 60 };
        private const int MeridianStep = 2;
        private const int ParallelStep = 3;

        private static Built? _built;

        /// <summary>The land's triangles.</summary>
        public static Mesh Land => Meshes.Land;

        public static Mesh Lakes => Meshes.Lakes;

        public static Mesh Coastlines => Meshes.Coastlines;

        public static Mesh Shores => Meshes.Shores;

        public static Mesh Graticule => Meshes.Graticule;

        /// <summary>How many triangles the five meshes hold together, and how many corners: what the map costs to draw.</summary>
        public static (int Triangles, int Vertices) Size
        {
            get
            {
                Built built = Meshes;
                int triangles = 0, vertices = 0;
                foreach (Mesh mesh in new[] { built.Land, built.Lakes, built.Coastlines, built.Shores, built.Graticule })
                {
                    triangles += (int)mesh.GetIndexCount(0) / 3;
                    vertices += mesh.vertexCount;
                }

                return (triangles, vertices);
            }
        }

        /// <summary>How long the meshes took to build, in milliseconds, the first time they were asked for.</summary>
        public static double BuildMilliseconds => Meshes.Milliseconds;

        private static Built Meshes => _built ??= Build();

        private static Built Build()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            WorldMapShape map = PlacesPack.WorldMap();
            (float top, float bottom) = map.Land.SpanOfY;
            Mesh land = FillOf(map.Land, "World land", point => LandColourAt((point.Y - top) / Math.Max(bottom - top, 1f)));
            Mesh lakes = FillOf(map.Lakes, "World lakes", point => LakeColour);
            Mesh coastlines = LinesOf(RingsOf(map.Land), isClosed: true, "World coastlines");
            Mesh shores = LinesOf(RingsOf(map.Lakes), isClosed: true, "World lake shores");
            Mesh graticule = LinesOf(GraticuleLines(), isClosed: false, "World graticule");
            return new Built(land, lakes, coastlines, shores, graticule, clock.Elapsed.TotalMilliseconds);
        }

        private static Color32 LandColourAt(float downTheLand)
        {
            for (int stop = 1; stop < LandColours.Length; stop++)
            {
                if (downTheLand <= LandColours[stop].At)
                {
                    (float fromAt, Color32 from) = LandColours[stop - 1];
                    (float toAt, Color32 to) = LandColours[stop];
                    return Color32.Lerp(from, to, (downTheLand - fromAt) / (toAt - fromAt));
                }
            }

            return LandColours[LandColours.Length - 1].Colour;
        }

        private static Mesh FillOf(MapOutline outline, string name, Func<MapPoint, Color32> colourAt)
        {
            var vertices = new Vector3[outline.Points.Count];
            var colours = new Color32[outline.Points.Count];
            for (int point = 0; point < outline.Points.Count; point++)
            {
                MapPoint at = outline.Points[point];
                vertices[point] = new Vector3(at.X, -at.Y, 0f);
                colours[point] = colourAt(at);
            }

            var triangles = new int[outline.Triangles.Count];
            for (int index = 0; index < triangles.Length; index++)
            {
                triangles[index] = outline.Triangles[index];
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0, calculateBounds: true);
            mesh.bounds = Grown(mesh.bounds);
            mesh.UploadMeshData(markNoLongerReadable: false);
            return mesh;
        }

        private static IEnumerable<IReadOnlyList<Vector2>> RingsOf(MapOutline outline)
        {
            foreach (RingSpan ring in outline.Rings)
            {
                var points = new Vector2[ring.Count];
                for (int point = 0; point < ring.Count; point++)
                {
                    MapPoint at = outline.Points[ring.Start + point];
                    points[point] = new Vector2(at.X, -at.Y);
                }

                yield return points;
            }
        }

        private static IEnumerable<IReadOnlyList<Vector2>> GraticuleLines()
        {
            for (int longitude = -180; longitude <= 180; longitude += MeridianEvery)
            {
                var line = new List<Vector2>();
                for (int latitude = SouthOfTheGraticule; latitude <= NorthOfTheGraticule; latitude += MeridianStep)
                {
                    line.Add(MeshPointOf(longitude, latitude));
                }

                yield return line;
            }

            foreach (int latitude in Parallels)
            {
                var line = new List<Vector2>();
                for (int longitude = -180; longitude <= 180; longitude += ParallelStep)
                {
                    line.Add(MeshPointOf(longitude, latitude));
                }

                yield return line;
            }
        }

        private static Vector2 MeshPointOf(double longitude, double latitude)
        {
            MapPoint point = NaturalEarthProjection.Project(longitude, latitude);
            return new Vector2(point.X, -point.Y);
        }

        /// <summary>Every line as a strip: each point twice, the mitre out to either side, which side, and how far along its line it lies.</summary>
        private static Mesh LinesOf(IEnumerable<IReadOnlyList<Vector2>> lines, bool isClosed, string name)
        {
            var vertices = new List<Vector3>();
            var mitres = new List<Vector2>();
            var sidesAndAlong = new List<Vector2>();
            var triangles = new List<int>();
            foreach (IReadOnlyList<Vector2> line in lines)
            {
                int count = line.Count;
                if (count < 2)
                {
                    continue;
                }

                int first = vertices.Count;
                float along = 0f;
                for (int point = 0; point < count; point++)
                {
                    if (point > 0)
                    {
                        along += Vector2.Distance(line[point - 1], line[point]);
                    }

                    Vector2 mitre = MitreAt(line, point, isClosed);
                    vertices.Add(line[point]);
                    vertices.Add(line[point]);
                    mitres.Add(mitre);
                    mitres.Add(mitre);
                    sidesAndAlong.Add(new Vector2(-1f, along));
                    sidesAndAlong.Add(new Vector2(1f, along));
                }

                int segments = isClosed ? count : count - 1;
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = first + 2 * segment;
                    int b = first + 2 * ((segment + 1) % count);
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, mitres);
            mesh.SetUVs(1, sidesAndAlong);
            mesh.SetTriangles(triangles, 0, calculateBounds: true);
            mesh.bounds = Grown(mesh.bounds);
            mesh.UploadMeshData(markNoLongerReadable: false);
            return mesh;
        }

        /// <summary>The way out from a line at one of its points, at the length that keeps the line's width along both its segments there.</summary>
        private static Vector2 MitreAt(IReadOnlyList<Vector2> line, int point, bool isClosed)
        {
            int count = line.Count;
            bool hasBefore = isClosed || point > 0;
            bool hasAfter = isClosed || point < count - 1;
            Vector2 before = hasBefore ? NormalOf(line[(point - 1 + count) % count], line[point]) : Vector2.zero;
            Vector2 after = hasAfter ? NormalOf(line[point], line[(point + 1) % count]) : Vector2.zero;
            if (!hasBefore || before == Vector2.zero)
            {
                return after;
            }

            if (!hasAfter || after == Vector2.zero)
            {
                return before;
            }

            Vector2 sum = before + after;
            if (sum.sqrMagnitude < 1e-8f)
            {
                return before; // the line turns straight back: no corner can keep both widths
            }

            Vector2 direction = sum.normalized;
            float cosine = Vector2.Dot(direction, before);
            return direction * Mathf.Min(1f / Mathf.Max(cosine, 1e-4f), LongestMitre);
        }

        private static Vector2 NormalOf(Vector2 from, Vector2 to)
        {
            Vector2 along = to - from;
            return along.sqrMagnitude < 1e-12f ? Vector2.zero : new Vector2(-along.y, along.x).normalized;
        }

        private static Bounds Grown(Bounds bounds)
        {
            bounds.Expand(new Vector3(2f * BoundsRoom, 2f * BoundsRoom, 0f));
            return bounds;
        }

        private sealed class Built
        {
            public Built(Mesh land, Mesh lakes, Mesh coastlines, Mesh shores, Mesh graticule, double milliseconds)
            {
                Land = land;
                Lakes = lakes;
                Coastlines = coastlines;
                Shores = shores;
                Graticule = graticule;
                Milliseconds = milliseconds;
            }

            public Mesh Land { get; }

            public Mesh Lakes { get; }

            public Mesh Coastlines { get; }

            public Mesh Shores { get; }

            public Mesh Graticule { get; }

            public double Milliseconds { get; }
        }
    }
}
