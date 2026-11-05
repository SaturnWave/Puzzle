using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Puzzle.Museum.Places
{
    /// <summary>A closed ring of an outline: where its points begin in the outline's list, and how many it has.</summary>
    public readonly struct RingSpan
    {
        /// <exception cref="ArgumentOutOfRangeException">The ring begins before the list, or has fewer than three points.</exception>
        public RingSpan(int start, int count)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "A ring begins at a place of the list.");
            }

            if (count < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "A ring has at least three points.");
            }

            Start = start;
            Count = count;
        }

        public int Start { get; }

        public int Count { get; }
    }

    /// <summary>
    /// Land or water as the map draws it: closed rings of points (the shores, outer rings and holes alike), and the
    /// triangles that fill the area they enclose, three indices into the points each. The rings are drawn as lines,
    /// the triangles as the fill under them.
    /// </summary>
    public sealed class MapOutline
    {
        /// <exception cref="ArgumentException">A ring runs past the points, or a triangle names a point there is not.</exception>
        public MapOutline(IReadOnlyList<MapPoint> points, IReadOnlyList<RingSpan> rings, IReadOnlyList<int> triangles)
        {
            foreach (RingSpan ring in rings)
            {
                if (ring.Start + ring.Count > points.Count)
                {
                    throw new ArgumentException($"A ring of {ring.Count} points at {ring.Start} runs past the {points.Count} points of the outline.", nameof(rings));
                }
            }

            if (triangles.Count % 3 != 0)
            {
                throw new ArgumentException($"{triangles.Count} indices are no whole number of triangles.", nameof(triangles));
            }

            foreach (int index in triangles)
            {
                if (index < 0 || index >= points.Count)
                {
                    throw new ArgumentException($"A triangle names the point {index}; the outline has {points.Count}.", nameof(triangles));
                }
            }

            Points = points;
            Rings = rings;
            Triangles = triangles;
        }

        public static MapOutline Empty { get; } = new MapOutline(Array.Empty<MapPoint>(), Array.Empty<RingSpan>(), Array.Empty<int>());

        /// <summary>Every ring's points, one ring after another.</summary>
        public IReadOnlyList<MapPoint> Points { get; }

        public IReadOnlyList<RingSpan> Rings { get; }

        /// <summary>Three indices into <see cref="Points"/> per triangle.</summary>
        public IReadOnlyList<int> Triangles { get; }

        /// <summary>The highest and lowest y of the points: the land's colour runs from top to foot over this span.</summary>
        public (float Top, float Bottom) SpanOfY
        {
            get
            {
                float top = float.MaxValue;
                float bottom = float.MinValue;
                foreach (MapPoint point in Points)
                {
                    top = Math.Min(top, point.Y);
                    bottom = Math.Max(bottom, point.Y);
                }

                return Points.Count == 0 ? (0f, 0f) : (top, bottom);
            }
        }
    }

    /// <summary>The world map the game draws: the land with its coastlines and the lakes with their shores, in map units (<see cref="NaturalEarthProjection"/>).</summary>
    public sealed class WorldMapShape
    {
        public WorldMapShape(MapOutline land, MapOutline lakes)
        {
            Land = land;
            Lakes = lakes;
        }

        public MapOutline Land { get; }

        public MapOutline Lakes { get; }
    }

    /// <summary>The map data file is not one this code reads.</summary>
    public sealed class WorldMapFormatException : Exception
    {
        public WorldMapFormatException(string message)
            : base(message)
        {
        }

        public WorldMapFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// The world map's data file, as the CLI's <c>worldmap build</c> writes it and the game reads it
    /// (unity/Assets/Resources/WorldMap.bytes): little-endian, "PZWM", the format version and the scale, then the
    /// land and the lakes, each as its counts, its points as 16-bit whole numbers of 1/<see cref="Scale"/> of a map
    /// unit, its rings as start and count, and its triangles as 16-bit indices (32-bit when an outline has more
    /// points than that holds). Whole numbers keep the file small and make every reading of it the same.
    /// </summary>
    public static class WorldMapFormat
    {
        public const ushort CurrentVersion = 1;

        /// <summary>A point is stored in twentieths of a map unit: 0.05 units, about a fifth of a point at the map's largest zoom.</summary>
        public const int Scale = 20;

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PZWM");

        /// <summary>A point's coordinate as the file stores it.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The coordinate lies too far off the map for the file.</exception>
        public static short Quantized(float coordinate)
        {
            double scaled = Math.Round(coordinate * (double)Scale, MidpointRounding.AwayFromZero);
            if (scaled < short.MinValue || scaled > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate, "A point lies too far off the map for its data file.");
            }

            return (short)scaled;
        }

        public static byte[] Write(WorldMapShape map)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(CurrentVersion);
                writer.Write((ushort)Scale);
                WriteOutline(writer, map.Land);
                WriteOutline(writer, map.Lakes);
            }

            return stream.ToArray();
        }

        /// <exception cref="WorldMapFormatException">The bytes are not a map data file of this version, or are cut short.</exception>
        public static WorldMapShape Read(byte[] bytes)
        {
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes, writable: false), Encoding.ASCII);
                byte[] magic = reader.ReadBytes(Magic.Length);
                if (Encoding.ASCII.GetString(magic) != Encoding.ASCII.GetString(Magic))
                {
                    throw new WorldMapFormatException("The map data does not begin as a map data file does.");
                }

                ushort version = reader.ReadUInt16();
                ushort scale = reader.ReadUInt16();
                if (version != CurrentVersion || scale != Scale)
                {
                    throw new WorldMapFormatException($"The map data is of version {version} at scale {scale}; this code reads version {CurrentVersion} at scale {Scale}.");
                }

                MapOutline land = ReadOutline(reader, "land");
                MapOutline lakes = ReadOutline(reader, "lakes");
                if (reader.BaseStream.Position != bytes.Length)
                {
                    throw new WorldMapFormatException($"The map data goes on for {bytes.Length - reader.BaseStream.Position} bytes past its lakes.");
                }

                return new WorldMapShape(land, lakes);
            }
            catch (EndOfStreamException cut)
            {
                throw new WorldMapFormatException("The map data is cut short.", cut);
            }
            catch (ArgumentException wrong)
            {
                throw new WorldMapFormatException($"The map data contradicts itself: {wrong.Message}", wrong);
            }
        }

        private static void WriteOutline(BinaryWriter writer, MapOutline outline)
        {
            writer.Write(outline.Points.Count);
            writer.Write(outline.Rings.Count);
            writer.Write(outline.Triangles.Count);
            foreach (MapPoint point in outline.Points)
            {
                writer.Write(Quantized(point.X));
                writer.Write(Quantized(point.Y));
            }

            foreach (RingSpan ring in outline.Rings)
            {
                writer.Write(ring.Start);
                writer.Write(ring.Count);
            }

            bool isNarrow = HasNarrowIndices(outline.Points.Count);
            foreach (int index in outline.Triangles)
            {
                if (isNarrow)
                {
                    writer.Write((ushort)index);
                }
                else
                {
                    writer.Write(index);
                }
            }
        }

        private static MapOutline ReadOutline(BinaryReader reader, string what)
        {
            int pointCount = reader.ReadInt32();
            int ringCount = reader.ReadInt32();
            int indexCount = reader.ReadInt32();
            if (pointCount < 0 || ringCount < 0 || indexCount < 0)
            {
                throw new WorldMapFormatException($"The map data counts less than nothing in its {what}.");
            }

            var points = new MapPoint[pointCount];
            for (int point = 0; point < pointCount; point++)
            {
                float x = reader.ReadInt16() / (float)Scale;
                float y = reader.ReadInt16() / (float)Scale;
                points[point] = new MapPoint(x, y);
            }

            var rings = new RingSpan[ringCount];
            for (int ring = 0; ring < ringCount; ring++)
            {
                rings[ring] = new RingSpan(reader.ReadInt32(), reader.ReadInt32());
            }

            bool isNarrow = HasNarrowIndices(pointCount);
            var triangles = new int[indexCount];
            for (int index = 0; index < indexCount; index++)
            {
                triangles[index] = isNarrow ? reader.ReadUInt16() : reader.ReadInt32();
            }

            return new MapOutline(points, rings, triangles);
        }

        private static bool HasNarrowIndices(int pointCount) => pointCount <= ushort.MaxValue + 1;
    }
}
