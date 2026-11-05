using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// What a finger on the board means in the Unity game. <c>BoardPointer</c> is the Unity project's source file,
/// compiled into these tests: it has no Unity dependency.
/// </summary>
public class BoardPointerTests
{
    private static BoardPointer PointerOn(params string[] tiles) => new BoardPointer(Start(Level(tiles)));

    private static string[] Board() => Filler(6, 6).With(3, 3, 'H').With(5, 5, '1');

    /// <summary>A touch that comes down in the middle of a cell and lifts where it came down, and the move it means on the board as it stands.</summary>
    private static Move? Tap(BoardPointer pointer, int x, int y)
    {
        pointer.Down(x + 0.5f, y + 0.5f);
        Assert.Null(pointer.MovedTo(x + 0.55f, y + 0.45f));
        BoardGesture tap = Assert.IsType<BoardGesture.Tap>(pointer.Up());
        return pointer.MoveMeantBy(tap);
    }

    [Theory]
    [InlineData(0.4f, 0.1f, 2, 1)]
    [InlineData(-0.4f, 0.1f, 0, 1)]
    [InlineData(0.1f, 0.4f, 1, 2)]
    [InlineData(-0.1f, -0.4f, 1, 0)]
    public void A_drag_trades_the_piece_with_the_neighbour_it_is_dragged_towards(float across, float down, int toX, int toY)
    {
        BoardPointer pointer = PointerOn(Board());

        pointer.Down(1.5f, 1.5f);
        BoardGesture? swipe = pointer.MovedTo(1.5f + across, 1.5f + down);

        Assert.Equal(Move.Swap(P(1, 1), P(toX, toY)), pointer.MoveMeantBy(Assert.IsType<BoardGesture.Swipe>(swipe)));
    }

    [Fact]
    public void A_finger_is_followed_without_asking_the_board_so_its_gesture_can_wait_for_the_pieces_to_rest()
    {
        // The stage keeps a gesture made during a turn and asks what it means when the board is at rest:
        // by then the tapped cell holds whatever the turn left there.
        Game game = Start(Level(Board()));
        var pointer = new BoardPointer(game);

        pointer.Down(3.5f, 3.5f);
        BoardGesture tapOnTheRocket = Assert.IsType<BoardGesture.Tap>(pointer.Up());
        pointer.Down(1.5f, 1.5f);
        BoardGesture swipe = Assert.IsType<BoardGesture.Swipe>(pointer.MovedTo(1.9f, 1.5f));

        Assert.Null(pointer.Selected);
        Assert.Equal(0, game.MovesUsed);
        Assert.Equal(Move.Tap(P(3, 3)), pointer.MoveMeantBy(tapOnTheRocket));
        Assert.Equal(Move.Swap(P(1, 1), P(2, 1)), pointer.MoveMeantBy(swipe));
    }

    [Fact]
    public void A_swipe_lets_go_of_the_picked_piece()
    {
        BoardPointer pointer = PointerOn(Board());
        Tap(pointer, 4, 4);

        pointer.Down(1.5f, 1.5f);
        pointer.MovedTo(1.9f, 1.5f);

        Assert.Null(pointer.Selected);
    }

    [Fact]
    public void A_drag_means_its_move_once_however_far_the_finger_goes_on()
    {
        BoardPointer pointer = PointerOn(Board());

        pointer.Down(1.5f, 1.5f);
        Assert.Null(pointer.MovedTo(1.7f, 1.5f));
        Assert.NotNull(pointer.MovedTo(1.9f, 1.5f));
        Assert.Null(pointer.MovedTo(2.6f, 1.5f));
        Assert.Null(pointer.Up());
    }

    [Fact]
    public void A_drag_off_the_edge_of_the_board_is_no_move()
    {
        BoardPointer pointer = PointerOn(Board());

        pointer.Down(0.5f, 0.5f);

        Assert.Null(pointer.MovedTo(0.1f, 0.5f));
        Assert.Null(pointer.Up());
    }

    [Fact]
    public void A_touch_beside_the_board_starts_nothing()
    {
        BoardPointer pointer = PointerOn(Board());

        pointer.Down(-0.5f, 2.5f);

        Assert.Null(pointer.MovedTo(0.5f, 2.5f));
        Assert.Null(pointer.Up());
        Assert.Null(pointer.Selected);
    }

    [Fact]
    public void A_tap_picks_a_piece_and_a_tap_on_its_neighbour_trades_the_two()
    {
        BoardPointer pointer = PointerOn(Board());

        Assert.Null(Tap(pointer, 1, 1));
        Assert.Equal(P(1, 1), pointer.Selected);

        Assert.Equal(Move.Swap(P(1, 1), P(1, 2)), Tap(pointer, 1, 2));
        Assert.Null(pointer.Selected);
    }

    [Fact]
    public void A_second_tap_on_the_picked_piece_lets_it_go()
    {
        BoardPointer pointer = PointerOn(Board());

        Tap(pointer, 1, 1);

        Assert.Null(Tap(pointer, 1, 1));
        Assert.Null(pointer.Selected);
    }

    [Fact]
    public void A_tap_on_a_piece_that_is_no_neighbour_picks_that_one_instead()
    {
        BoardPointer pointer = PointerOn(Board());

        Tap(pointer, 1, 1);

        Assert.Null(Tap(pointer, 4, 4));
        Assert.Equal(P(4, 4), pointer.Selected);
    }

    [Fact]
    public void A_tap_on_a_special_piece_sets_it_off()
    {
        BoardPointer pointer = PointerOn(Board());

        Assert.Equal(Move.Tap(P(3, 3)), Tap(pointer, 3, 3));
        Assert.Null(pointer.Selected);
    }

    [Fact]
    public void A_tap_on_a_special_piece_next_to_the_picked_piece_trades_the_two()
    {
        BoardPointer pointer = PointerOn(Board());

        Tap(pointer, 2, 3);

        Assert.Equal(Move.Swap(P(2, 3), P(3, 3)), Tap(pointer, 3, 3));
    }

    [Fact]
    public void A_crate_cannot_be_picked()
    {
        BoardPointer pointer = PointerOn(Board());

        Assert.Null(Tap(pointer, 5, 5));
        Assert.Null(pointer.Selected);
    }

    [Fact]
    public void A_touch_that_was_taken_away_means_nothing()
    {
        BoardPointer pointer = PointerOn(Board());

        pointer.Down(1.5f, 1.5f);
        pointer.Cancel();

        Assert.Null(pointer.Up());
        Assert.Null(pointer.Selected);
    }

    [Theory]
    [InlineData(1080, 2340)]
    [InlineData(2076, 2152)]
    public void A_touch_in_the_middle_of_a_cell_is_found_in_that_cell(int width, int height)
    {
        var screen = new ScreenShape(width, height);
        ScreenLayout layout = ScreenLayout.Of(screen, new BoardFootprint(9, 11, hasExits: false));

        // The middle of the cell in column 3, row 7, in pixels from the lower left corner of the screen.
        float pointX = layout.BoardLeft + 3.5f * layout.CellSize;
        float pointY = layout.BoardTop - 7.5f * layout.CellSize;
        float pixelX = (pointX + layout.ViewWidth / 2f) * layout.PixelsPerPoint;
        float pixelY = (pointY + layout.ViewHeight / 2f) * layout.PixelsPerPoint;

        (float column, float row) = layout.BoardPointAt(pixelX, pixelY);

        Assert.Equal(3.5f, column, 0.001f);
        Assert.Equal(7.5f, row, 0.001f);
        Assert.Equal((pixelX, pixelY), layout.PixelAt(3.5f, 7.5f));
        Assert.InRange(pixelX, 0f, width);
        Assert.InRange(pixelY, 0f, height);
    }
}
