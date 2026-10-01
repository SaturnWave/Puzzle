using Puzzle.Core.Engine.Resolution;
using Puzzle.Core.Model;

namespace Puzzle.Core.Tests;

public class MatchFinderTests
{
    [Fact]
    public void Line_of_three_clears_without_special()
    {
        var finder = new MatchFinder(TestBoards.BoardOf(
            "ppp.",
            "...."));

        MatchGroup group = Assert.Single(finder.FindGroups(-1, -1));

        Assert.Equal(GemColor.Purple, group.Color);
        Assert.Equal(3, group.Cells.Count);
        Assert.Null(group.Special);
    }

    [Fact]
    public void Horizontal_four_makes_vertical_rocket_at_preferred_cell()
    {
        var board = TestBoards.BoardOf(
            "pppp",
            "....");
        var finder = new MatchFinder(board);

        MatchGroup group = Assert.Single(finder.FindGroups(preferredA: 2, preferredB: -1));

        Assert.Equal(new SpecialSpawn(TileKind.RocketV, 2), group.Special);
    }

    [Fact]
    public void Vertical_four_makes_horizontal_rocket_on_second_cell_by_default()
    {
        var finder = new MatchFinder(TestBoards.BoardOf(
            "p.",
            "p.",
            "p.",
            "p."));

        MatchGroup group = Assert.Single(finder.FindGroups(-1, -1));

        Assert.Equal(TileKind.RocketH, group.Special?.Kind);
        Assert.Equal(2, group.Special?.Anchor); // (0,1)
    }

    [Fact]
    public void Five_in_a_line_beats_everything_and_makes_color_bomb_in_the_middle()
    {
        var finder = new MatchFinder(TestBoards.BoardOf("ppppp"));

        MatchGroup group = Assert.Single(finder.FindGroups(-1, -1));

        Assert.Equal(new SpecialSpawn(TileKind.ColorBomb, 2), group.Special);
    }

    [Theory]
    [InlineData("ppp|p..|p..")] // L
    [InlineData("ppp|.p.|.p.")] // T
    [InlineData(".p.|ppp|.p.")] // plus
    public void Crossing_lines_make_a_bomb_at_the_intersection(string shape)
    {
        var board = TestBoards.BoardOf(shape.Split('|'));
        var finder = new MatchFinder(board);

        MatchGroup group = Assert.Single(finder.FindGroups(-1, -1));

        Assert.Equal(5, group.Cells.Count);
        Assert.Equal(TileKind.Bomb, group.Special?.Kind);
        int anchor = group.Special!.Value.Anchor;
        Assert.True(CountInRow(board, anchor) >= 3 && CountInColumn(board, anchor) >= 3);
    }

    [Fact]
    public void Square_makes_a_seeker()
    {
        var finder = new MatchFinder(TestBoards.BoardOf(
            ".pp",
            ".pp"));

        MatchGroup group = Assert.Single(finder.FindGroups(-1, -1));

        Assert.Equal(new SpecialSpawn(TileKind.Seeker, 1), group.Special);
    }

    [Fact]
    public void Square_with_tail_still_makes_a_seeker()
    {
        var finder = new MatchFinder(TestBoards.BoardOf(
            "ppp",
            "pp."));

        MatchGroup group = Assert.Single(finder.FindGroups(-1, -1));

        Assert.Equal(5, group.Cells.Count);
        Assert.Equal(TileKind.Seeker, group.Special?.Kind);
    }

    [Fact]
    public void Touching_lines_without_shared_cell_stay_separate_groups()
    {
        var finder = new MatchFinder(TestBoards.BoardOf(
            "ppp...",
            "...ppp"));

        Assert.Equal(2, finder.FindGroups(-1, -1).Count);
    }

    [Fact]
    public void Gaps_and_specials_break_lines()
    {
        var finder = new MatchFinder(TestBoards.BoardOf("ppXpp"));

        Assert.Empty(finder.FindGroups(-1, -1));
        Assert.False(finder.AnyMatch());
    }

    [Fact]
    public void Has_match_at_detects_lines_and_squares_locally()
    {
        var finder = new MatchFinder(TestBoards.BoardOf(
            "pp.o",
            "pp.o",
            "g..o"));

        Assert.True(finder.HasMatchAt(0));  // square
        Assert.True(finder.HasMatchAt(11)); // vertical line of o
        Assert.False(finder.HasMatchAt(8)); // lone g
    }

    private static int CountInRow(Board board, int index)
    {
        int y = index / board.Width;
        return Enumerable.Range(0, board.Width).Count(x => board.TileAt(new Position(x, y)).IsGemOf(GemColor.Purple));
    }

    private static int CountInColumn(Board board, int index)
    {
        int x = index % board.Width;
        return Enumerable.Range(0, board.Height).Count(y => board.TileAt(new Position(x, y)).IsGemOf(GemColor.Purple));
    }
}
