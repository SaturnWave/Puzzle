using Puzzle.Client.Screens;

namespace Puzzle.Core.Tests;

/// <summary>
/// The slop within which a touch of the Unity game is a tap, and so presses the control it lifts on: a sideways
/// swipe across a card's row is no tap and presses no row (the phone test of 2026-10-23). <c>TapSlop</c> is the Unity
/// project's source file, compiled into these tests: it has no Unity dependency. Places are in the layout's points.
/// </summary>
public class TapSlopTests
{
    [Fact]
    public void A_finger_that_lifts_where_it_came_down_taps()
    {
        TapSlop touch = TapSlop.Down(120f, -40f);

        Assert.True(touch.IsATap);
        Assert.True(touch.MovedTo(120f, -40f).IsATap);
    }

    [Theory]
    [InlineData(10f, 0f)]
    [InlineData(-10f, 0f)]
    [InlineData(0f, 10f)]
    [InlineData(0f, -10f)]
    [InlineData(10f, -10f)]
    [InlineData(-6f, 7f)]
    public void A_finger_that_goes_no_further_than_the_slop_across_or_up_and_down_still_taps(float across, float up)
    {
        TapSlop touch = TapSlop.Down(0f, 0f);

        Assert.True(touch.MovedTo(across, up).IsATap);
    }

    [Theory]
    [InlineData(10.5f, 0f)]
    [InlineData(-10.5f, 0f)]
    [InlineData(0f, 10.5f)]
    [InlineData(0f, -10.5f)]
    [InlineData(80f, 2f)]
    public void A_finger_that_goes_further_than_the_slop_either_way_drags(float across, float up)
    {
        TapSlop touch = TapSlop.Down(0f, 0f);

        Assert.False(touch.MovedTo(across, up).IsATap);
    }

    [Fact]
    public void A_sideways_swipe_that_ends_where_it_began_on_a_row_has_dragged()
    {
        // The phone test's swipe: it starts and ends on the same row, and went far beyond the slop in between.
        TapSlop touch = TapSlop.Down(-60f, 5f);
        foreach (float x in new[] { -40f, -10f, 20f, 50f, 20f, -20f, -58f })
        {
            touch = touch.MovedTo(x, 5f);
        }

        Assert.False(touch.IsATap);
    }

    [Fact]
    public void A_finger_that_wavers_within_the_slop_many_times_still_taps()
    {
        TapSlop touch = TapSlop.Down(0f, 0f);
        foreach ((float x, float y) in new[] { (3f, -2f), (-4f, 6f), (9f, 9f), (-9.5f, 0f), (1f, 1f) })
        {
            touch = touch.MovedTo(x, y);
        }

        Assert.True(touch.IsATap);
    }

    [Fact]
    public void The_slop_is_told_in_the_points_of_what_the_finger_is_on()
    {
        // A card drawn at half the layout's size: the layout's 10 points are 20 of the card's own.
        float onAHalfSizeCard = TapSlop.Points / 0.5f;

        TapSlop touch = TapSlop.Down(0f, 0f, onAHalfSizeCard);

        Assert.True(touch.MovedTo(18f, 0f).IsATap);
        Assert.False(touch.MovedTo(21f, 0f).IsATap);
    }

    [Fact]
    public void A_slop_less_than_nothing_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TapSlop.Down(0f, 0f, -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => TapSlop.Down(0f, 0f, float.NaN));
    }
}
