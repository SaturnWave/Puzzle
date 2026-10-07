using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// Where the Unity game puts a board on a screen. <c>ScreenLayout</c> is the Unity project's source file, compiled
/// into these tests: it has no Unity dependency, so the screens the game must support are checked here in numbers.
/// </summary>
public class ScreenLayoutTests
{
    public static TheoryData<int, int, int, int, bool> ScreensAndBoards()
    {
        (int Width, int Height)[] screens = { (1080, 2340), (1080, 1920), (2076, 2152), (1038, 1076), (720, 1280) };
        (int Columns, int Rows, bool HasExits)[] boards = { (10, 10, false), (11, 13, false), (10, 11, true), (16, 16, true), (5, 5, false), (16, 4, false) };
        var cases = new TheoryData<int, int, int, int, bool>();
        foreach ((int width, int height) in screens)
        {
            foreach ((int columns, int rows, bool hasExits) in boards)
            {
                cases.Add(width, height, columns, rows, hasExits);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(ScreensAndBoards))]
    public void A_board_stays_between_the_plaques_and_the_shelf_and_inside_the_screen(int width, int height, int columns, int rows, bool hasExits)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(columns, rows, hasExits));

        // Positions are put on whole pixels, which may move an edge by less than one pixel.
        float onePixel = 1f / layout.PixelsPerPoint;
        float boardRight = layout.BoardLeft + columns * layout.CellSize;
        float exits = hasExits ? ScreenLayout.ExitGap + ScreenLayout.ExitSizeInCells * layout.CellSize : 0f;
        float blockBottom = layout.BoardTop - rows * layout.CellSize - exits;
        Assert.True(layout.BoardLeft >= -layout.ViewWidth / 2 + ScreenLayout.BoardMargin - onePixel, "cut off at the left");
        Assert.True(boardRight <= layout.ViewWidth / 2 - ScreenLayout.BoardMargin + onePixel, "cut off at the right");
        Assert.True(layout.BoardTop <= layout.HudBottom - ScreenLayout.FrameReach + onePixel, "the frame runs under the plaques");
        Assert.True(blockBottom >= layout.ShelfTop + ScreenLayout.FrameReach + ScreenLayout.FrameShadowDrop - onePixel, "the frame's shadow falls on the shelf");
    }

    [Theory]
    [InlineData(1080, 2340, 0, 0)]
    [InlineData(1080, 2340, 110, 60)]
    [InlineData(1080, 1920, 0, 0)]
    [InlineData(1080, 1920, 90, 48)]
    [InlineData(2076, 2152, 0, 0)]
    [InlineData(1038, 1076, 0, 0)]
    public void The_plaques_and_the_shelf_stay_on_the_usable_screen(int width, int height, int topInset, int bottomInset)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height, topInset, bottomInset), new BoardFootprint(10, 10, hasExits: false));

        float usableTop = layout.ViewHeight / 2 - topInset / layout.PixelsPerPoint;
        float usableBottom = -layout.ViewHeight / 2 + bottomInset / layout.PixelsPerPoint;
        float hudTop = layout.HudMiddle + ScreenLayout.HudHeight * layout.ChromeScale / 2;
        float shelfBottom = layout.ShelfMiddle - ScreenLayout.ShelfHeight * layout.ChromeScale / 2;
        Assert.True(hudTop <= usableTop - 12f + 0.001f, "the plaques touch the top of the usable screen");
        Assert.True(shelfBottom >= usableBottom + 10f - 0.001f, "the shelf touches the foot of the usable screen");
        Assert.True(layout.HudBottom > layout.ShelfTop, "the plaques and the shelf overlap");
        Assert.InRange(layout.HudWidth, 300f, layout.ViewWidth - 2 * ScreenLayout.HudSideMargin);
        Assert.InRange(layout.ShelfWidth, 300f, layout.ViewWidth - 2 * ScreenLayout.ShelfSideMargin);
    }

    [Fact]
    public void A_short_screen_draws_the_plaques_and_the_shelf_smaller_and_a_tall_one_at_full_size()
    {
        var board = new BoardFootprint(10, 10, hasExits: false);

        ScreenLayout shortest = ScreenLayout.Of(new ScreenShape(1080, 1920), board);
        ScreenLayout square = ScreenLayout.Of(new ScreenShape(2076, 2152), board);
        ScreenLayout tall = ScreenLayout.Of(new ScreenShape(1080, 2340), board);

        Assert.Equal(ScreenLayout.SmallestChromeScale, shortest.ChromeScale, 0.0001f);
        Assert.Equal(ScreenLayout.SmallestChromeScale, square.ChromeScale, 0.0001f);
        Assert.Equal(1f, tall.ChromeScale, 0.0001f);
    }

    [Theory]
    [MemberData(nameof(ScreensAndBoards))]
    public void A_cell_is_a_whole_number_of_pixels(int width, int height, int columns, int rows, bool hasExits)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(columns, rows, hasExits));

        float cellPixels = layout.CellSize * layout.PixelsPerPoint;
        Assert.Equal(MathF.Round(cellPixels), cellPixels, 0.001f);
        Assert.InRange(layout.CellSize, 1f, ScreenLayout.LargestCell);
    }

    [Fact]
    public void A_9_to_16_phone_shows_level_40_with_the_web_test_pages_cells()
    {
        // The web test page draws this 11 x 13 board with 30 px cells on a 360 x 640 px page (out/web-theme/screens).
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(720, 1280), new BoardFootprint(11, 13, hasExits: false));

        Assert.Equal(360f, layout.ViewWidth);
        Assert.Equal(640f, layout.ViewHeight);
        Assert.Equal(30f, layout.CellSize);
    }

    [Fact]
    public void A_tall_phone_sizes_the_board_by_its_width_and_a_square_screen_by_its_height()
    {
        var board = new BoardFootprint(10, 10, hasExits: false);

        ScreenLayout tall = ScreenLayout.Of(new ScreenShape(1080, 2340), board);
        ScreenLayout square = ScreenLayout.Of(new ScreenShape(2076, 2152), board);

        Assert.Equal(tall.ViewWidth - 2 * ScreenLayout.BoardMargin, 10 * tall.CellSize, 0.5f);
        // Between the plaques and the shelf the board leaves room for its frame on both sides, the frame's shadow,
        // and the two gaps of the shortest screen (6 points under the plaques, 4 over the shelf).
        float roomForTheCells = square.HudBottom - square.ShelfTop - 2 * ScreenLayout.FrameReach - ScreenLayout.FrameShadowDrop - 10f;
        Assert.Equal(roomForTheCells, 10 * square.CellSize, 10f / square.PixelsPerPoint);
        Assert.True(10 * square.CellSize < square.ViewWidth - 2 * ScreenLayout.BoardMargin);
    }

    [Fact]
    public void A_small_board_does_not_grow_past_the_largest_cell()
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(1080, 1920), new BoardFootprint(4, 4, hasExits: false));

        Assert.Equal(ScreenLayout.LargestCell, layout.CellSize);
    }

    [Fact]
    public void A_cutout_at_the_top_pushes_the_plaques_and_the_board_down_and_leaves_the_cells_their_size()
    {
        var board = new BoardFootprint(10, 10, hasExits: false);

        ScreenLayout plain = ScreenLayout.Of(new ScreenShape(1080, 2340), board);
        ScreenLayout withCutout = ScreenLayout.Of(new ScreenShape(1080, 2340, topInsetPixels: 120), board);

        // 120 px are 40 points on this screen. The width still decides the cell; the plaques hang under the cutout.
        Assert.Equal(plain.CellSize, withCutout.CellSize);
        Assert.True(withCutout.HudMiddle < plain.HudMiddle);
        Assert.True(withCutout.BoardTop < plain.BoardTop);
        Assert.True(withCutout.ChromeScale < plain.ChromeScale, "less usable height draws the plaques smaller");
    }

    [Fact]
    public void Exit_marks_take_their_room_from_the_board()
    {
        var screen = new ScreenShape(2076, 2152);

        ScreenLayout without = ScreenLayout.Of(screen, new BoardFootprint(10, 10, hasExits: false));
        ScreenLayout with = ScreenLayout.Of(screen, new BoardFootprint(10, 10, hasExits: true));

        Assert.True(with.CellSize < without.CellSize);
    }

    [Theory]
    [InlineData(0, 100, 0, 0)]
    [InlineData(100, 100, 60, 40)]
    [InlineData(100, 100, -1, 0)]
    public void A_screen_without_room_is_refused(int width, int height, int topInset, int bottomInset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenShape(width, height, topInset, bottomInset));
    }
}
