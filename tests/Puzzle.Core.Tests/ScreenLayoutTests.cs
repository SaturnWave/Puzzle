using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// Where the Unity game puts a board on a screen. <c>ScreenLayout</c> is the Unity project's source file, compiled
/// into these tests: it has no Unity dependency, so the screens the game must support are checked here in numbers.
/// The screens are a tall phone (1080 x 2340, and 1080 x 2424: the cover display of the foldable the game is tried
/// on), the 9:16 phone of the design (1080 x 1920, 720 x 1280), that foldable opened (2076 x 2152, nearly square),
/// and the iPads upright (3:4: the 12.9" Pro 2048 x 2732, the 10th generation 1640 x 2360, the mini 1488 x 2266).
/// </summary>
public class ScreenLayoutTests
{
    /// <summary>The shapes the game's 1000 levels have: 9 to 11 columns, 10 to 13 rows, never wider than high.</summary>
    private static readonly (int Columns, int Rows)[] BoardsOfTheGame =
    {
        (9, 10), (9, 11), (9, 12), (10, 10), (10, 11), (10, 12), (10, 13), (11, 11), (11, 12), (11, 13),
    };

    public static TheoryData<int, int, int, int, bool> ScreensAndBoards()
    {
        (int Width, int Height)[] screens = { (1080, 2424), (1080, 2340), (1080, 1920), (1200, 1920), (2076, 2152), (1038, 1076), (720, 1280), (2048, 2732), (1640, 2360), (1488, 2266), (2732, 2048), (2360, 1640), (2152, 2076), (2560, 1600) };
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
    [InlineData(1080, 2424, 0, 0)]
    [InlineData(1080, 2424, 96, 48)]
    [InlineData(1080, 2340, 0, 0)]
    [InlineData(1080, 2340, 110, 60)]
    [InlineData(1080, 1920, 0, 0)]
    [InlineData(1080, 1920, 90, 48)]
    [InlineData(2076, 2152, 0, 0)]
    [InlineData(2076, 2152, 80, 60)]
    [InlineData(1038, 1076, 0, 0)]
    [InlineData(2048, 2732, 48, 40)]
    [InlineData(1640, 2360, 48, 40)]
    [InlineData(1488, 2266, 48, 40)]
    [InlineData(2732, 2048, 0, 40)]
    [InlineData(2360, 1640, 0, 40)]
    [InlineData(2152, 2076, 0, 0)]
    [InlineData(2560, 1600, 0, 0)]
    public void The_plaques_and_the_shelf_stay_on_the_usable_screen(int width, int height, int topInset, int bottomInset)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height, topInset, bottomInset), new BoardFootprint(10, 10, hasExits: false));

        float usableTop = layout.ViewHeight / 2 - topInset / layout.PixelsPerPoint;
        float usableBottom = -layout.ViewHeight / 2 + bottomInset / layout.PixelsPerPoint;
        float hudTop = layout.HudMiddle + ScreenLayout.HudHeight * layout.HudScale / 2;
        float shelfBottom = layout.ShelfMiddle - ScreenLayout.ShelfHeight * layout.ShelfScale / 2;
        Assert.True(hudTop <= usableTop - ScreenLayout.LeastWallAtTheEdges + 0.001f, "the plaques touch the top of the usable screen");
        Assert.True(shelfBottom >= usableBottom + ScreenLayout.LeastWallAtTheEdges - 0.001f, "the shelf touches the foot of the usable screen");
        Assert.True(layout.HudBottom > layout.ShelfTop, "the plaques and the shelf overlap");
        Assert.True(layout.HudWidth <= layout.ViewWidth - 2 * ScreenLayout.HudSideMargin + 0.001f, "the plaques are cut off at the sides");
        Assert.InRange(layout.ShelfWidth, 300f, layout.ViewWidth - 2 * ScreenLayout.ShelfSideMargin);

        // In its own points the row of plaques is between a tall phone's (the screen's 360 less the wall at its sides)
        // and the widest: each plaque is then 113 to 158 points wide and 84 high, room for three goals and no more.
        Assert.InRange(layout.HudWidth / layout.HudScale, 340f - 0.001f, ScreenLayout.WidestHud + 0.001f);
    }

    [Fact]
    public void A_tall_phone_draws_the_plaques_and_the_shelf_at_full_size_a_9_to_16_phone_smaller_and_a_wide_screen_smallest()
    {
        var board = new BoardFootprint(10, 10, hasExits: false);

        ScreenLayout roomy = ScreenLayout.Of(new ScreenShape(1080, 2424), board);
        ScreenLayout tight = ScreenLayout.Of(new ScreenShape(1080, 1920), board);
        ScreenLayout compact = ScreenLayout.Of(new ScreenShape(2076, 2152), board);

        // A tall phone's board is as wide as the screen whatever the plaques take, so they keep their full size.
        Assert.Equal(1f, roomy.HudScale, 0.0001f);
        Assert.Equal(1f, roomy.ShelfScale, 0.0001f);
        Assert.Equal(ScreenLayout.TightChromeScale, tight.HudScale, 0.0001f);
        Assert.Equal(ScreenLayout.TightChromeScale, tight.ShelfScale, 0.0001f);

        // On the open foldable the height decides every board, so every point the plaques and the shelf give up
        // goes to the cells. The shelf holds one line of lettering and gives up more than the plaques do.
        Assert.Equal(ScreenLayout.CompactHudScale, compact.HudScale, 0.0001f);
        Assert.Equal(ScreenLayout.CompactShelfScale, compact.ShelfScale, 0.0001f);
    }

    [Fact]
    public void A_screen_between_two_shapes_draws_the_plaques_between_their_sizes()
    {
        var board = new BoardFootprint(10, 10, hasExits: false);

        // 1080 x 2100 is 700 points high: halfway from the 9:16 phone's 640 to the roomy 760.
        ScreenLayout tallish = ScreenLayout.Of(new ScreenShape(1080, 2100), board);

        // 1260 x 1920 is 420 points wide: halfway from the 9:16 phone's 360 to the wide 480.
        ScreenLayout widish = ScreenLayout.Of(new ScreenShape(1260, 1920), board);

        Assert.Equal((ScreenLayout.TightChromeScale + 1f) / 2, tallish.HudScale, 0.0001f);
        Assert.Equal((ScreenLayout.TightChromeScale + ScreenLayout.CompactHudScale) / 2, widish.HudScale, 0.0001f);
        Assert.Equal((ScreenLayout.TightChromeScale + ScreenLayout.CompactShelfScale) / 2, widish.ShelfScale, 0.0001f);
    }

    [Theory]
    [InlineData(1080, 2424, 0, 0)]
    [InlineData(1080, 2424, 96, 48)]
    [InlineData(1080, 2340, 0, 0)]
    [InlineData(1080, 1920, 0, 0)]
    [InlineData(1080, 1920, 90, 48)]
    [InlineData(720, 1280, 0, 0)]
    [InlineData(1200, 1920, 0, 0)]
    [InlineData(1260, 1920, 0, 0)]
    [InlineData(2076, 2152, 0, 0)]
    [InlineData(2076, 2152, 80, 60)]
    [InlineData(1038, 1076, 0, 0)]
    [InlineData(2048, 2732, 48, 40)]
    [InlineData(1640, 2360, 48, 40)]
    [InlineData(1488, 2266, 48, 40)]
    [InlineData(2732, 2048, 0, 40)]
    [InlineData(2360, 1640, 0, 40)]
    [InlineData(2152, 2076, 0, 0)]
    [InlineData(2560, 1600, 0, 0)]
    public void The_shelfs_five_discs_stand_apart_on_its_plank_from_end_to_end_and_none_is_less_than_a_fingertip_in_the_hand(int width, int height, int topInset, int bottomInset)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height, topInset, bottomInset), new BoardFootprint(10, 12, hasExits: false));
        IReadOnlyList<Box> discs = layout.ShelfDiscs;
        float plankTop = layout.ShelfTop;
        float plankBottom = layout.ShelfMiddle - ScreenLayout.ShelfHeight * layout.ShelfScale / 2;
        float inFromTheEnds = ScreenLayout.DiscsInFromPlankEnd * layout.ShelfScale;

        // The pause disc, then the places of the four boosters' discs, edge to edge as BoardTR spreads them.
        Assert.Equal(1 + ScreenLayout.BoostersOnTheShelf, discs.Count);
        Assert.Equal((layout.PauseButton.Left, layout.PauseButton.Width), (discs[0].Left, discs[0].Width));
        Assert.Equal(-layout.ShelfWidth / 2 + inFromTheEnds, discs[0].Left, 0.001f);
        Assert.Equal(layout.ShelfWidth / 2 - inFromTheEnds, discs[^1].Right, 0.001f);
        foreach (Box disc in discs)
        {
            Assert.True(disc.Width * layout.PointInTheHand >= ScreenLayout.SmallestDiscInTheHand - 0.001f, $"a disc of {disc} is {disc.Width * layout.PointInTheHand} points across in the hand");
            Assert.True(disc.Bottom >= plankBottom && disc.Top <= plankTop, $"a disc of {disc} stands out of the plank {plankBottom}..{plankTop}");
            Assert.Equal(disc.Width, disc.Height, 0.001f);
        }

        float[] gaps = discs.Zip(discs.Skip(1), (left, right) => right.Left - left.Right).ToArray();
        Assert.All(gaps, gap => Assert.True(gap > 0f, $"two discs of the shelf run into each other: {string.Join(", ", gaps)}"));
        Assert.All(gaps, gap => Assert.Equal(gaps[0], gap, 0.001f));
        Assert.All(discs.Skip(1), disc => Assert.Equal(discs[1].Width, disc.Width, 0.001f));
        Assert.True(discs[0].Width <= discs[1].Width, "the pause disc is larger than a booster's");
    }

    [Fact]
    public void The_open_foldable_shows_a_point_a_quarter_larger_in_the_hand_and_a_phone_its_own()
    {
        var board = new BoardFootprint(10, 12, hasExits: false);

        Assert.Equal(1f, ScreenLayout.Of(new ScreenShape(1080, 2340), board).PointInTheHand);
        Assert.Equal(1f, ScreenLayout.Of(new ScreenShape(1080, 1920), board).PointInTheHand);
        Assert.Equal(ScreenLayout.CompactPointInTheHand, ScreenLayout.Of(new ScreenShape(2076, 2152), board).PointInTheHand);
    }

    /// <summary>
    /// What the shelf's discs cost the cells: the plank grew from 58 points to 72 at full size, and on the open
    /// foldable from half its size to 0.62 of it. A tall phone's width decides every board of the game, as before.
    /// On the 9:16 phone the width decided every board before; now the height holds back one shape, 9 x 12, whose
    /// cell goes from 113 to 111 pixels on 1080 x 1920 (1.8 percent) and from 75 to 74 on 720 x 1280. The open
    /// foldable's height decides every board, and its cells lose 2.9 to 3.3 percent (the pixels it had before are
    /// those the plank of 29 points left, by the board's rows).
    /// </summary>
    [Fact]
    public void The_taller_shelf_costs_a_tall_phone_no_cell_and_a_9_to_16_phone_or_the_open_foldable_at_most_about_three_percent_of_one()
    {
        const float AboutThreePercent = 0.035f;
        var foldableCellsBefore = new Dictionary<int, int> { [10] = 167, [11] = 152, [12] = 139, [13] = 128 };
        foreach ((int columns, int rows) in BoardsOfTheGame)
        {
            var board = new BoardFootprint(columns, rows, hasExits: false);
            foreach ((int width, int height) in new[] { (1080, 2424), (1080, 2340) })
            {
                ScreenLayout tall = ScreenLayout.Of(new ScreenShape(width, height), board);
                Assert.Equal(WidthsCell(tall, columns), tall.CellSize, 0.0001f);
            }

            foreach ((int width, int height) in new[] { (1080, 1920), (720, 1280) })
            {
                ScreenLayout phone = ScreenLayout.Of(new ScreenShape(width, height), board);
                float lost = 1f - phone.CellSize / WidthsCell(phone, columns);
                Assert.True(lost <= AboutThreePercent, $"a {columns} x {rows} board on {width} x {height} loses {lost:P1} of its cell to the shelf");
            }

            ScreenLayout foldable = ScreenLayout.Of(new ScreenShape(2076, 2152), board);
            float foldableLost = 1f - foldable.CellSize * foldable.PixelsPerPoint / foldableCellsBefore[rows];
            Assert.True(foldableLost <= AboutThreePercent, $"a {columns} x {rows} board on the open foldable loses {foldableLost:P1} of its cell to the shelf");
        }
    }

    /// <summary>The cell a phone's width gives a board, on whole pixels: what it has where the height does not hold it back.</summary>
    private static float WidthsCell(ScreenLayout layout, int columns) =>
        MathF.Floor(MathF.Min(ScreenLayout.LargestCell, (layout.ViewWidth - 2 * ScreenLayout.BoardMargin) / columns) * layout.PixelsPerPoint) / layout.PixelsPerPoint;

    [Theory]
    [MemberData(nameof(ScreensAndBoards))]
    public void A_cell_is_a_whole_number_of_pixels(int width, int height, int columns, int rows, bool hasExits)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(columns, rows, hasExits));

        float cellPixels = layout.CellSize * layout.PixelsPerPoint;
        Assert.Equal(MathF.Round(cellPixels), cellPixels, 0.001f);
        Assert.InRange(layout.CellSize, 1f, ScreenLayout.LargestCell);
    }

    [Theory]
    [MemberData(nameof(ScreensAndBoards))]
    public void The_moves_medallion_hangs_above_the_boards_first_row_over_the_middle_of_the_screen(int width, int height, int columns, int rows, bool hasExits)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(columns, rows, hasExits));

        (float column, float row, float across) = layout.MovesMedallionOnTheBoard;

        // Counted in cells from the board's own corner, it is where the plaques' row has it in points.
        (float pixelX, float pixelY) = layout.PixelAt(column, row);
        (float middleX, float middleY) = layout.PixelOfPoint(0f, layout.HudMiddle);
        Assert.Equal(middleX, pixelX, 0.01f);
        Assert.Equal(middleY, pixelY, 0.01f);
        Assert.Equal(ScreenLayout.HudHeight * layout.HudScale, across * layout.CellSize, 0.001f);

        // The board is centred on the screen but for a pixel, and the medallion's foot stays clear of its first row.
        Assert.Equal(columns / 2f, column, 1f / (layout.CellSize * layout.PixelsPerPoint) + 0.001f);
        Assert.True(row + across / 2 < 0f, $"the medallion reaches down to row {row + across / 2}");
    }

    /// <summary>
    /// The cells of the foldable the game is tried on, in pixels. Its cover display is a tall phone at about 422
    /// pixels to the inch, where the width decides the cell; opened, it is nearly square at about 373, where the
    /// height does. In millimetres, and beside what the same boards had while the frame was 10.6 points wide with
    /// 4 points of wall beside it and the open screen drew the plaques and the shelf at the 9:16 phone's size, and
    /// before the shelf grew for its discs (58 points high and half of that on the open screen, now 72 and 0.62):
    /// <code>
    /// cover, 1080 x 2424        before           wider frame      with the discs
    ///    9 columns           110 px  6.6 mm   113 px  6.8 mm   113 px  6.8 mm
    ///   10 columns            99 px  6.0 mm   102 px  6.1 mm   102 px  6.1 mm
    ///   11 columns            90 px  5.4 mm    93 px  5.6 mm    93 px  5.6 mm
    /// open, 2076 x 2152
    ///   10 rows              151 px 10.3 mm   167 px 11.4 mm   162 px 11.0 mm
    ///   11 rows              137 px  9.3 mm   152 px 10.4 mm   147 px 10.0 mm
    ///   12 rows              126 px  8.6 mm   139 px  9.5 mm   135 px  9.2 mm
    ///   13 rows              116 px  7.9 mm   128 px  8.7 mm   124 px  8.4 mm
    /// </code>
    /// On the cover a board of 11 columns cannot have cells of more than 98 pixels even with no frame at all: a
    /// larger cell there takes a board of fewer columns, which is the levels' business and not the layout's.
    /// </summary>
    [Theory]
    [InlineData(1080, 2424, 9, 11, 113)]
    [InlineData(1080, 2424, 9, 12, 113)]
    [InlineData(1080, 2424, 10, 10, 102)]
    [InlineData(1080, 2424, 10, 13, 102)]
    [InlineData(1080, 2424, 11, 11, 93)]
    [InlineData(1080, 2424, 11, 13, 93)]
    [InlineData(1080, 2424, 12, 13, 85)]
    [InlineData(1080, 2424, 13, 13, 78)]
    [InlineData(2076, 2152, 9, 10, 162)]
    [InlineData(2076, 2152, 10, 10, 162)]
    [InlineData(2076, 2152, 9, 11, 147)]
    [InlineData(2076, 2152, 11, 11, 147)]
    [InlineData(2076, 2152, 10, 12, 135)]
    [InlineData(2076, 2152, 10, 13, 124)]
    [InlineData(2076, 2152, 11, 13, 124)]
    [InlineData(2076, 2152, 13, 13, 124)]
    public void The_foldable_the_game_is_tried_on_shows_a_board_with_cells_of_this_many_pixels(int width, int height, int columns, int rows, int cellPixels)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(columns, rows, hasExits: false));

        Assert.Equal(cellPixels, layout.CellSize * layout.PixelsPerPoint, 0.001f);
    }

    [Fact]
    public void A_phone_gives_the_board_all_its_width_but_the_frames()
    {
        // The web test page draws level 40's 11 x 13 board with 30 px cells on a 360 x 640 px page: its frame is a
        // third wider and stands 4 points off the page's sides. Here the cells get that room: 31 points.
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(720, 1280), new BoardFootprint(11, 13, hasExits: false));

        Assert.Equal(360f, layout.ViewWidth);
        Assert.Equal(640f, layout.ViewHeight);
        Assert.Equal(31f, layout.CellSize);

        // What is left of the width beside the cells is the frame, the line of wall beside it, and less than the
        // pixel per column that whole-pixel cells cost.
        float besideTheCells = layout.ViewWidth - 11 * layout.CellSize;
        Assert.InRange(besideTheCells, 2 * ScreenLayout.BoardMargin, 2 * ScreenLayout.BoardMargin + 11 / layout.PixelsPerPoint);
    }

    [Fact]
    public void A_tall_phone_sizes_the_board_by_its_width_and_a_wide_screen_by_its_height()
    {
        var board = new BoardFootprint(10, 10, hasExits: false);

        ScreenLayout tall = ScreenLayout.Of(new ScreenShape(1080, 2340), board);
        ScreenLayout square = ScreenLayout.Of(new ScreenShape(2076, 2152), board);

        // Whole-pixel cells cost less than a pixel per column.
        Assert.Equal(tall.ViewWidth - 2 * ScreenLayout.BoardMargin, 10 * tall.CellSize, 10f / tall.PixelsPerPoint);

        // Between the plaques and the shelf the board leaves room for its frame on both sides, the frame's shadow,
        // and the two gaps of a wide screen (5 points under the plaques, 3 over the shelf).
        float roomForTheCells = square.HudBottom - square.ShelfTop - 2 * ScreenLayout.FrameReach - ScreenLayout.FrameShadowDrop - 8f;
        Assert.Equal(roomForTheCells, 10 * square.CellSize, 10f / square.PixelsPerPoint);
        Assert.True(10 * square.CellSize < square.ViewWidth - 2 * ScreenLayout.BoardMargin);
    }

    [Fact]
    public void On_the_open_foldable_every_board_of_the_game_leaves_wall_for_a_lamp_at_each_side()
    {
        // The height decides the cell there, and no board of the game is wider than it is high: wall is left at
        // both sides of the frame, and the lamps hang in it (Sconces), so it is not left bare. Above the board
        // there is no wall for them, as there is on a phone.
        foreach ((int columns, int rows) in BoardsOfTheGame)
        {
            ScreenLayout layout = ScreenLayout.Of(new ScreenShape(2076, 2152), new BoardFootprint(columns, rows, hasExits: false));

            Assert.True(layout.WallBesideBoard >= Sconces.RoomBesideTheFrame, $"a {columns} x {rows} board leaves {layout.WallBesideBoard} points beside its frame");
            Assert.True(layout.WallAboveBoard < Sconces.RoomInTheCutCorners, $"a {columns} x {rows} board has {layout.WallAboveBoard} points of wall above it");
        }
    }

    [Fact]
    public void On_a_tall_phone_the_width_decides_every_board_of_the_game()
    {
        // So nothing the plaques and the shelf could give up would make a tall phone's cells larger, and they keep
        // their size there. Only the frame and the line of wall beside it stand between the cells and the screen's
        // sides (and less than the pixel per column that whole-pixel cells cost, half of it at each side; the
        // board's left edge is put on a whole pixel, which may take most of a pixel from the wall there). On the
        // 9:16 phone the shelf's discs take a little of the tallest boards' cells (see the test of what they cost).
        foreach ((int width, int height) in new[] { (1080, 2424), (1080, 2340) })
        {
            foreach ((int columns, int rows) in BoardsOfTheGame)
            {
                ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(columns, rows, hasExits: false));

                float wallBesideTheFrame = ScreenLayout.BoardMargin - ScreenLayout.FrameReach;
                float onePixel = 1f / layout.PixelsPerPoint;
                Assert.InRange(layout.WallBesideBoard, wallBesideTheFrame - onePixel, wallBesideTheFrame + columns * onePixel / 2);
            }
        }
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
        Assert.True(withCutout.HudScale < plain.HudScale, "less usable height draws the plaques smaller");
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
