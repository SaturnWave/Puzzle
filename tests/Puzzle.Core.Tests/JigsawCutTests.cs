using System.IO;
using Puzzle.Core.Randomness;
using Puzzle.Museum;
using Puzzle.Museum.Jigsaw;

namespace Puzzle.Core.Tests;

/// <summary>
/// The jigsaw cut: every knob meets a socket of its own shape, the pieces cover the picture exactly once, the
/// picture's edges stay straight, and the same seed always cuts the same pieces, in all four sizes and both ways up.
/// </summary>
public class JigsawCutTests
{
    /// <summary>Set to 1 to write the pinned fingerprints anew after a deliberate change of the cut, then run the pinning test once.</summary>
    private const string PinAnewVariable = "PUZZLE_PIN_JIGSAW_CUTS";

    /// <summary>Lengths are compared to this share of a picture's longer side: far below a pixel, far above rounding.</summary>
    private const double Tolerance = 1e-9;

    private const int PointsPerCurve = 12;

    private static readonly ulong[] Seeds = { 1, 7, 2026, 0xC0FFEE, ulong.MaxValue };

    /// <summary>Pictures as wide as a panorama, as the first room's paintings, square, and upright.</summary>
    private static readonly (PaintingOrientation Orientation, double Width, double Height)[] Pictures =
    {
        (PaintingOrientation.Landscape, 1920, 1138),
        (PaintingOrientation.Landscape, 1920, 1407),
        (PaintingOrientation.Landscape, 1920, 1848),
        (PaintingOrientation.Landscape, 2.4, 1),
        (PaintingOrientation.Portrait, 1280, 1589),
        (PaintingOrientation.Portrait, 1, 1.02),
        (PaintingOrientation.Portrait, 600, 1300),
    };

    public static TheoryData<string, PaintingOrientation, double, double> EverySizeOnEveryPicture()
    {
        var cases = new TheoryData<string, PaintingOrientation, double, double>();
        foreach (JigsawSize size in JigsawSize.All)
        {
            foreach ((PaintingOrientation orientation, double width, double height) in Pictures)
            {
                cases.Add(size.Name, orientation, width, height);
            }
        }

        return cases;
    }

    private static JigsawSize SizeNamed(string name) => JigsawSize.All.Single(size => size.Name == name);

    private static IEnumerable<JigsawCut> CutsOf(string sizeName, PaintingOrientation orientation, double width, double height) =>
        Seeds.Select(seed => JigsawCut.Of(seed, SizeNamed(sizeName), orientation, new PictureSize(width, height)));

    [Theory]
    [MemberData(nameof(EverySizeOnEveryPicture))]
    public void The_pieces_are_numbered_in_order_and_each_outline_closes_on_its_start(string size, PaintingOrientation orientation, double width, double height)
    {
        foreach (JigsawCut cut in CutsOf(size, orientation, width, height))
        {
            Assert.Equal(Enumerable.Range(0, cut.Size.PieceCount), cut.Pieces.Select(piece => piece.Number));
            Assert.All(cut.Pieces, piece =>
            {
                Assert.Equal(piece.Outline.Start, piece.Outline.Steps[^1].End);
                Assert.Equal(piece.Cell.TopLeft, piece.Outline.Start);
                Point2 at = piece.Outline.Start;
                foreach (PieceEdge edge in piece.Edges)
                {
                    Assert.Equal(at, edge.Path.Start); // every edge begins exactly where the one before it ended
                    at = edge.Path.End;
                }
            });
        }
    }

    [Theory]
    [MemberData(nameof(EverySizeOnEveryPicture))]
    public void Every_knob_meets_a_socket_of_exactly_its_shape(string size, PaintingOrientation orientation, double width, double height)
    {
        foreach (JigsawCut cut in CutsOf(size, orientation, width, height))
        {
            foreach (JigsawPiece piece in cut.Pieces)
            {
                foreach (PieceEdge edge in piece.Edges.Where(edge => edge.Joint != Joint.Border))
                {
                    JigsawPiece neighbour = cut.Pieces[edge.Neighbour!.Value];
                    PieceEdge back = Assert.Single(neighbour.Edges, other => other.Neighbour == piece.Number && other.Path.Start == edge.Path.End);

                    Assert.Equal(Opposite(edge.Joint), back.Joint);
                    Assert.Equal(PointsOf(edge.Path.Reversed()), PointsOf(back.Path)); // the very same line, walked the other way
                }
            }

            Assert.Equal(
                cut.Pieces.Sum(piece => piece.Edges.Count(edge => edge.Joint == Joint.Tab)),
                cut.Pieces.Sum(piece => piece.Edges.Count(edge => edge.Joint == Joint.Blank)));
        }
    }

    [Theory]
    [MemberData(nameof(EverySizeOnEveryPicture))]
    public void The_pictures_edges_stay_straight_and_only_they_have_no_neighbour(string size, PaintingOrientation orientation, double width, double height)
    {
        foreach (JigsawCut cut in CutsOf(size, orientation, width, height))
        {
            double borderLength = 0;
            foreach (PieceEdge edge in cut.Pieces.SelectMany(piece => piece.Edges))
            {
                Assert.Equal(edge.Joint == Joint.Border, edge.Neighbour == null);
                if (edge.Joint != Joint.Border)
                {
                    continue;
                }

                PathStep only = Assert.Single(edge.Path.Steps);
                Assert.IsType<PathStep.Line>(only);
                Assert.True(LiesOnTheBorder(edge.Path.Start, cut.Picture) && LiesOnTheBorder(edge.Path.End, cut.Picture));
                borderLength += Math.Abs(edge.Path.End.X - edge.Path.Start.X) + Math.Abs(edge.Path.End.Y - edge.Path.Start.Y);
            }

            Assert.Equal(2 * (width + height), borderLength, Tolerance * Math.Max(width, height));
        }
    }

    [Theory]
    [MemberData(nameof(EverySizeOnEveryPicture))]
    public void The_pieces_cover_the_picture_exactly_once(string size, PaintingOrientation orientation, double width, double height)
    {
        foreach (JigsawCut cut in CutsOf(size, orientation, width, height))
        {
            List<IReadOnlyList<Point2>> polygons = cut.Pieces.Select(piece => piece.Outline.Flatten(PointsPerCurve)).ToList();

            // Clockwise as the picture is seen (y runs down) counts positive; together the pieces are the picture.
            Assert.All(polygons, polygon => Assert.True(Polygon.AreaClockwise(polygon) > 0));
            Assert.Equal(width * height, polygons.Sum(Polygon.AreaClockwise), Tolerance * width * height);

            // No outline crosses itself: a knob never reaches into another knob or socket of its own piece.
            Assert.All(polygons, polygon => Assert.False(Polygon.CrossesItself(polygon)));

            // And any point of the picture lies in one piece, not in two and not in none.
            var points = new Rng(cut.Seed);
            for (int sample = 0; sample < 400; sample++)
            {
                var point = new Point2(points.NextDouble() * width, points.NextDouble() * height);
                Assert.Equal(1, polygons.Count(polygon => Polygon.Contains(polygon, point)));
            }
        }
    }

    [Theory]
    [MemberData(nameof(EverySizeOnEveryPicture))]
    public void Every_piece_belongs_in_the_middle_of_its_cell_and_knows_its_neighbours(string size, PaintingOrientation orientation, double width, double height)
    {
        foreach (JigsawCut cut in CutsOf(size, orientation, width, height))
        {
            Assert.All(cut.Pieces, piece =>
            {
                Assert.True(Polygon.Contains(piece.Outline.Flatten(PointsPerCurve), piece.Home), $"The home of piece {piece.Number} lies outside it.");
                Assert.Equal(piece.Edges.Where(edge => edge.Neighbour != null).Select(edge => edge.Neighbour!.Value).Distinct(), piece.Neighbours);
                Assert.All(piece.Neighbours, neighbour => Assert.Contains(piece.Number, cut.Pieces[neighbour].Neighbours));
            });
            Assert.Equal(width * height, cut.Pieces.Sum(piece => piece.Cell.Width * piece.Cell.Height), Tolerance * width * height);
        }
    }

    [Theory]
    [MemberData(nameof(EverySizeOnEveryPicture))]
    public void Every_piece_is_filled_by_triangles_that_cover_it_exactly(string size, PaintingOrientation orientation, double width, double height)
    {
        foreach (JigsawCut cut in CutsOf(size, orientation, width, height))
        {
            foreach (JigsawPiece piece in cut.Pieces)
            {
                IReadOnlyList<Point2> polygon = piece.Outline.Flatten(pointsPerCurve: 8);
                IReadOnlyList<int> corners = PolygonTriangles.Of(polygon);

                var triangles = corners.Chunk(3).Select(triangle => triangle.Select(corner => polygon[corner]).ToArray()).ToList();
                Assert.All(triangles, triangle =>
                {
                    Assert.True(Polygon.AreaClockwise(triangle) > 0, "A triangle is turned over or flat.");
                    var middle = new Point2(triangle.Average(point => point.X), triangle.Average(point => point.Y));
                    Assert.True(Polygon.Contains(polygon, middle), "A triangle lies outside its piece.");
                });
                Assert.Equal(Polygon.AreaClockwise(polygon), triangles.Sum(Polygon.AreaClockwise), Tolerance * width * height);
            }
        }
    }

    [Fact]
    public void No_triangle_is_flat_and_a_shape_that_runs_the_wrong_way_is_refused()
    {
        // A square with a corner in the middle of its top side, and a dent in its right side.
        Point2[] dented = { new(0, 0), new(5, 0), new(10, 0), new(10, 4), new(7, 5), new(10, 6), new(10, 10), new(0, 10) };

        double[] areas = PolygonTriangles.Of(dented).Chunk(3)
            .Select(triangle => Polygon.AreaClockwise(triangle.Select(corner => dented[corner]).ToArray()))
            .ToArray();

        Assert.All(areas, area => Assert.True(area > 0));
        Assert.Equal(97, areas.Sum(), 9);
        Assert.Throws<ArgumentException>(() => PolygonTriangles.Of(dented.Reverse().ToArray()));
    }

    [Fact]
    public void Ten_pieces_lie_in_rows_of_three_four_and_three_near_square()
    {
        JigsawCut cut = JigsawCut.Of(seed: 7, JigsawSize.Ten, PaintingOrientation.Landscape, new PictureSize(1200, 900));

        Assert.Equal(new[] { 400.0, 400, 400, 300, 300, 300, 300, 400, 400, 400 }, cut.Pieces.Select(piece => Math.Round(piece.Cell.Width, 6)));
        Assert.All(cut.Pieces, piece => Assert.Equal(300, piece.Cell.Height, 6));
        Assert.Equal(new[] { 0.0, 0, 0, 300, 300, 300, 300, 600, 600, 600 }, cut.Pieces.Select(piece => Math.Round(piece.Cell.TopLeft.Y, 6)));
        Assert.Equal(new[] { 1, 4, 3 }, cut.Pieces[0].Neighbours);          // clockwise from the top left corner: beside it, then two of the four below
        Assert.Equal(new[] { 2, 5, 4, 0 }, cut.Pieces[1].Neighbours);
        Assert.Equal(new[] { 0, 1, 5, 8, 7, 3 }, cut.Pieces[4].Neighbours);
    }

    [Fact]
    public void A_portrait_picture_is_cut_with_the_layout_turned_by_a_quarter()
    {
        JigsawCut cut = JigsawCut.Of(seed: 7, JigsawSize.Ten, PaintingOrientation.Portrait, new PictureSize(900, 1200));

        // Columns of three, four and three, numbered column by column from the top.
        Assert.Equal(new[] { 0.0, 0, 0, 300, 300, 300, 300, 600, 600, 600 }, cut.Pieces.Select(piece => Math.Round(piece.Cell.TopLeft.X, 6)));
        Assert.Equal(new[] { 400.0, 400, 400, 300, 300, 300, 300, 400, 400, 400 }, cut.Pieces.Select(piece => Math.Round(piece.Cell.Height, 6)));
        Assert.Equal(new[] { 3, 4, 1 }, cut.Pieces[0].Neighbours); // still clockwise as the picture is seen: right, right, below

        JigsawCut grid = JigsawCut.Of(seed: 7, JigsawSize.Forty, PaintingOrientation.Portrait, new PictureSize(1000, 1600));
        Assert.All(grid.Pieces, piece => Assert.Equal((200.0, 200.0), (Math.Round(piece.Cell.Width, 6), Math.Round(piece.Cell.Height, 6))));
        Assert.Equal(5, grid.Pieces.Select(piece => Math.Round(piece.Cell.TopLeft.X, 6)).Distinct().Count());
    }

    [Fact]
    public void Where_rows_of_three_and_four_meet_the_shortest_stretches_stay_straight()
    {
        foreach (ulong seed in Seeds)
        {
            JigsawCut ten = JigsawCut.Of(seed, JigsawSize.Ten, PaintingOrientation.Landscape, new PictureSize(1920, 1138));

            // Each of the two lines between the rows breaks into six stretches; the two of a twelfth of the width carry no knob.
            Assert.Equal(2 * 2 * 2, ten.Pieces.Sum(piece => piece.Edges.Count(edge => edge.Joint == Joint.Flat)));
            Assert.All(ten.Pieces.SelectMany(piece => piece.Edges).Where(edge => edge.Joint == Joint.Flat), flat =>
                Assert.Equal(1920.0 / 12, Math.Abs(flat.Path.End.X - flat.Path.Start.X), 6));

            JigsawCut twenty = JigsawCut.Of(seed, JigsawSize.Twenty, PaintingOrientation.Landscape, new PictureSize(1920, 1138));
            Assert.DoesNotContain(twenty.Pieces.SelectMany(piece => piece.Edges), edge => edge.Joint == Joint.Flat);
        }
    }

    [Fact]
    public void The_same_seed_cuts_the_same_pieces_and_another_seed_other_pieces()
    {
        var picture = new PictureSize(1920, 1407);
        JigsawCut once = JigsawCut.Of(seed: 2026, JigsawSize.Thirty, PaintingOrientation.Landscape, picture);
        JigsawCut again = JigsawCut.Of(seed: 2026, JigsawSize.Thirty, PaintingOrientation.Landscape, picture);
        JigsawCut other = JigsawCut.Of(seed: 2027, JigsawSize.Thirty, PaintingOrientation.Landscape, picture);

        Assert.Equal(ShapingPointsOf(once), ShapingPointsOf(again));
        Assert.Equal(once.Fingerprint(), again.Fingerprint());
        Assert.NotEqual(once.Fingerprint(), other.Fingerprint());
    }

    [Fact]
    public void Only_the_pictures_proportions_shape_the_cut()
    {
        JigsawCut small = JigsawCut.Of(seed: 7, JigsawSize.Twenty, PaintingOrientation.Landscape, new PictureSize(4, 3));
        JigsawCut large = JigsawCut.Of(seed: 7, JigsawSize.Twenty, PaintingOrientation.Landscape, new PictureSize(2000, 1500));

        Assert.Equal(small.Pieces.SelectMany(piece => piece.Edges).Select(edge => edge.Joint), large.Pieces.SelectMany(piece => piece.Edges).Select(edge => edge.Joint));
        Assert.All(ShapingPointsOf(small).Zip(ShapingPointsOf(large)), pair =>
        {
            Assert.Equal(pair.First.X * 500, pair.Second.X, 6);
            Assert.Equal(pair.First.Y * 500, pair.Second.Y, 6);
        });
    }

    [Fact]
    public void A_picture_needs_sides_of_a_length()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PictureSize(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PictureSize(10, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PictureSize(double.NaN, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PictureSize(10, double.PositiveInfinity));
    }

    /// <summary>
    /// The cutter's fingerprint on the tools' runtime. The Unity project cuts the same pictures inside the Editor
    /// and compares itself with the same file (unity/Assets/Editor/MuseumReport.cs), so a difference between the
    /// two runtimes shows up there.
    /// </summary>
    [Fact]
    public void The_pinned_pictures_are_cut_as_the_pinned_fingerprints_say()
    {
        string[] cut = JigsawTrace.Lines();
        if (Environment.GetEnvironmentVariable(PinAnewVariable) == "1")
        {
            File.WriteAllLines(RepoFiles.JigsawCutsPath, cut);
        }

        Assert.Equal(File.ReadAllLines(RepoFiles.JigsawCutsPath), cut);
    }

    private static Joint Opposite(Joint joint) => joint switch
    {
        Joint.Tab => Joint.Blank,
        Joint.Blank => Joint.Tab,
        _ => joint,
    };

    private static bool LiesOnTheBorder(Point2 point, PictureSize picture) =>
        point.X == 0 || point.Y == 0 || point.X == picture.Width || point.Y == picture.Height;

    private static IEnumerable<Point2> ShapingPointsOf(JigsawCut cut) =>
        cut.Pieces.SelectMany(piece => new[] { piece.Outline.Start }.Concat(PointsOf(piece.Outline.Steps)));

    private static IEnumerable<Point2> PointsOf(OpenPath path) => new[] { path.Start }.Concat(PointsOf(path.Steps));

    /// <summary>Every point that shapes the steps: a line's end, a curve's two control points and its end.</summary>
    private static IEnumerable<Point2> PointsOf(IEnumerable<PathStep> steps) => steps.SelectMany(step =>
        step is PathStep.Curve curve ? new[] { curve.Control1, curve.Control2, curve.End } : new[] { step.End });

    /// <summary>Plain polygon measures, written out here so the tests do not judge the cut with the cut's own code.</summary>
    private static class Polygon
    {
        /// <summary>The shoelace area, positive when the corners run clockwise with y pointing down.</summary>
        public static double AreaClockwise(IReadOnlyList<Point2> polygon)
        {
            double twice = 0;
            for (int index = 0; index < polygon.Count; index++)
            {
                Point2 from = polygon[index];
                Point2 to = polygon[(index + 1) % polygon.Count];
                twice += from.X * to.Y - to.X * from.Y;
            }

            return twice / 2;
        }

        /// <summary>Even-odd rule: a ray to the right crosses the outline an odd number of times.</summary>
        public static bool Contains(IReadOnlyList<Point2> polygon, Point2 point)
        {
            bool inside = false;
            for (int index = 0; index < polygon.Count; index++)
            {
                Point2 from = polygon[index];
                Point2 to = polygon[(index + 1) % polygon.Count];
                bool straddles = from.Y > point.Y != to.Y > point.Y;
                if (straddles && point.X < from.X + (point.Y - from.Y) / (to.Y - from.Y) * (to.X - from.X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static bool CrossesItself(IReadOnlyList<Point2> polygon)
        {
            for (int first = 0; first < polygon.Count; first++)
            {
                for (int second = first + 2; second < polygon.Count; second++)
                {
                    bool shareACorner = first == 0 && second == polygon.Count - 1;
                    if (!shareACorner && Cross(polygon[first], polygon[first + 1], polygon[second], polygon[(second + 1) % polygon.Count]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Cross(Point2 a, Point2 b, Point2 c, Point2 d) =>
            Side(a, b, c) * Side(a, b, d) < 0 && Side(c, d, a) * Side(c, d, b) < 0;

        private static double Side(Point2 from, Point2 to, Point2 point) =>
            (to.X - from.X) * (point.Y - from.Y) - (to.Y - from.Y) * (point.X - from.X);
    }
}
