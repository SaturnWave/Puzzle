using Puzzle.Client.Boards;
using Puzzle.Client.Layout;
using Puzzle.Core.Model;

namespace Puzzle.Core.Tests;

/// <summary>
/// The outline the Unity game draws its gilt frame along, and the rule that hangs the wall lamps beside a board.
/// <c>BoardOutline</c> and <c>Sconces</c> are the Unity project's source files, compiled into these tests: they have
/// no Unity dependency.
/// </summary>
public class BoardOutlineTests
{
    /// <summary>A board from rows of text: '#' is a playable cell, anything else is not.</summary>
    private static BoardShape Shape(params string[] rows)
    {
        var playable = new List<bool>();
        foreach (string row in rows)
        {
            playable.AddRange(row.Select(cell => cell == '#'));
        }

        return new BoardShape(rows[0].Length, rows.Length, playable);
    }

    private static GridCorner[] Corners(params (int X, int Y)[] corners) => corners.Select(corner => new GridCorner(corner.X, corner.Y)).ToArray();

    /// <summary>Twice the area a loop encloses: positive when it runs clockwise on the screen, where y grows downwards.</summary>
    private static int TwiceTheAreaOf(IReadOnlyList<GridCorner> loop)
    {
        int twiceTheArea = 0;
        for (int corner = 0; corner < loop.Count; corner++)
        {
            GridCorner here = loop[corner];
            GridCorner next = loop[(corner + 1) % loop.Count];
            twiceTheArea += here.X * next.Y - next.X * here.Y;
        }

        return twiceTheArea;
    }

    [Fact]
    public void A_rectangle_has_one_loop_of_four_corners_that_runs_clockwise_from_its_top_left()
    {
        IReadOnlyList<IReadOnlyList<GridCorner>> loops = BoardOutline.LoopsOf(BoardShape.Rectangle(10, 12));

        IReadOnlyList<GridCorner> loop = Assert.Single(loops);
        Assert.Equal(Corners((0, 0), (10, 0), (10, 12), (0, 12)), loop);
    }

    [Fact]
    public void A_step_in_the_board_adds_its_corners_and_a_straight_run_of_cells_adds_none()
    {
        BoardShape stepped = Shape(
            ".##",
            "###",
            "###");

        IReadOnlyList<GridCorner> loop = Assert.Single(BoardOutline.LoopsOf(stepped));

        Assert.Equal(Corners((1, 0), (3, 0), (3, 3), (0, 3), (0, 1), (1, 1)), loop);
    }

    [Fact]
    public void A_hole_is_a_loop_of_its_own_that_runs_the_other_way()
    {
        BoardShape holed = Shape(
            "####",
            "#..#",
            "#..#",
            "####");

        IReadOnlyList<IReadOnlyList<GridCorner>> loops = BoardOutline.LoopsOf(holed);

        Assert.Equal(2, loops.Count);
        Assert.Equal(2 * 16, TwiceTheAreaOf(loops[0]));
        Assert.Equal(-2 * 4, TwiceTheAreaOf(loops[1]));
        Assert.Equal(Corners((1, 3), (1, 1), (3, 1), (3, 3)).ToHashSet(), loops[1].ToHashSet());
    }

    [Fact]
    public void Cells_that_touch_only_at_a_corner_keep_a_loop_each()
    {
        BoardShape diagonal = Shape(
            "#.",
            ".#");

        IReadOnlyList<IReadOnlyList<GridCorner>> loops = BoardOutline.LoopsOf(diagonal);

        Assert.Equal(2, loops.Count);
        Assert.All(loops, loop => Assert.Equal(2, TwiceTheAreaOf(loop)));
    }

    [Fact]
    public void The_outline_encloses_exactly_the_playable_cells()
    {
        BoardShape diamond = Shape(
            "...####...",
            "..######..",
            ".########.",
            "##########",
            "####..####",
            "##########",
            ".########.",
            "..######..",
            "...####...");

        int twiceTheArea = BoardOutline.LoopsOf(diamond).Sum(TwiceTheAreaOf);

        Assert.Equal(2 * diamond.PlayableCount, twiceTheArea);
    }

    [Fact]
    public void Lamps_hang_above_a_plain_board_only_when_the_wall_above_it_is_high_enough()
    {
        BoardShape plain = BoardShape.Rectangle(10, 10);

        Assert.Equal(SconcePlace.AboveTheFrame, Sconces.PlaceBeside(plain, Sconces.RoomAboveTheFrame));
        Assert.Equal(SconcePlace.None, Sconces.PlaceBeside(plain, Sconces.RoomAboveTheFrame - 1f));
    }

    [Fact]
    public void Lamps_hang_in_the_top_corners_of_a_board_that_has_both_cut_away_two_cells_wide()
    {
        BoardShape cut = Shape(
            "..####..",
            "########",
            "########");
        BoardShape cutOnOneSide = Shape(
            "..######",
            "########",
            "########");

        Assert.Equal(SconcePlace.InTheCutCorners, Sconces.PlaceBeside(cut, Sconces.RoomInTheCutCorners));
        Assert.Equal(SconcePlace.None, Sconces.PlaceBeside(cut, Sconces.RoomInTheCutCorners - 1f));
        Assert.Equal(SconcePlace.AboveTheFrame, Sconces.PlaceBeside(cutOnOneSide, Sconces.RoomAboveTheFrame));
    }
}
