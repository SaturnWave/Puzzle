using Puzzle.Client.Play;
using Puzzle.Client.Screens;

namespace Puzzle.Core.Tests;

/// <summary>
/// What a finger on the museum's pages means in the Unity game: a tap, a swipe that drags the row of pages, and
/// where the row settles when it is let go. <c>PageSwipe</c> is the Unity project's source file, compiled into
/// these tests: it has no Unity dependency. The screen is the design's phone, 360 points wide; the row is the bar's
/// four pages with the hall (place 2) shown, unless a test says otherwise.
/// </summary>
public class PageSwipeTests
{
    private const float Width = 360f;
    private const float Frame = 1f / 60f;

    private static readonly SwipeRow TheBar = new SwipeRow(count: 4, shown: 2, Width);

    private static PageSwipe FingerDownAt(float x, SwipeRow? row = null, float y = 0f)
    {
        var swipe = new PageSwipe();
        swipe.Down(x, y, seconds: 0f, row ?? TheBar);
        return swipe;
    }

    /// <summary>The finger slides across in even steps, one a frame, from one place to another in so many seconds; what the last step reads.</summary>
    private static SwipeReading Slide(PageSwipe swipe, float fromX, float toX, float startsAt, float seconds, float y = 0f)
    {
        int steps = Math.Max(1, (int)Math.Round(seconds / Frame));
        SwipeReading reading = SwipeReading.None.Instance;
        for (int step = 1; step <= steps; step++)
        {
            reading = swipe.MovedTo(fromX + (toX - fromX) * step / steps, y, startsAt + seconds * step / steps);
        }

        return reading;
    }

    [Fact]
    public void A_finger_that_lifts_where_it_came_down_is_a_tap()
    {
        PageSwipe swipe = FingerDownAt(0f);

        Assert.IsType<SwipeReading.None>(swipe.MovedTo(3f, -2f, 0.05f));
        Assert.IsType<SwipeReading.Tap>(swipe.Up(0.1f));
    }

    [Fact]
    public void A_finger_that_slides_no_further_than_the_slop_is_still_a_tap()
    {
        PageSwipe swipe = FingerDownAt(0f);

        Assert.IsType<SwipeReading.None>(swipe.MovedTo(-PageSwipe.Slop + 0.5f, 4f, 0.05f));
        Assert.False(swipe.IsDragging);
        Assert.IsType<SwipeReading.Tap>(swipe.Up(0.1f));
    }

    [Fact]
    public void A_sideways_drag_past_the_slop_drags_the_page_from_where_it_left_the_slop_so_it_does_not_jump()
    {
        PageSwipe swipe = FingerDownAt(50f);

        SwipeReading.Drag drag = Assert.IsType<SwipeReading.Drag>(swipe.MovedTo(20f, 3f, 0.1f));

        Assert.Equal(-(30f - PageSwipe.Slop), drag.By, precision: 3);
        Assert.True(swipe.IsDragging);
    }

    [Theory]
    [InlineData(-60f, 3)]
    [InlineData(60f, 1)]
    public void A_finger_going_left_brings_in_the_page_after_and_one_going_right_the_page_before(float across, int beside)
    {
        PageSwipe swipe = FingerDownAt(0f);

        SwipeReading.Drag drag = Assert.IsType<SwipeReading.Drag>(swipe.MovedTo(across, 0f, 0.1f));

        Assert.Equal(beside, drag.Beside);
        Assert.Equal(Math.Sign(across), Math.Sign(drag.By));
    }

    [Fact]
    public void A_drag_that_goes_up_or_down_first_never_turns_a_page_even_when_it_goes_sideways_later()
    {
        PageSwipe swipe = FingerDownAt(0f);

        Assert.IsType<SwipeReading.None>(swipe.MovedTo(2f, 15f, 0.05f));
        Assert.IsType<SwipeReading.None>(swipe.MovedTo(-150f, 15f, 0.3f));
        Assert.False(swipe.IsDragging);
        Assert.IsType<SwipeReading.None>(swipe.Up(0.35f));
    }

    [Fact]
    public void A_slanting_drag_waits_until_it_is_clearly_sideways()
    {
        PageSwipe swipe = FingerDownAt(0f);

        // Further than the slop across, but not twice as far across as down, and not past the slop down: not yet said.
        Assert.IsType<SwipeReading.None>(swipe.MovedTo(-12f, 8f, 0.05f));
        Assert.IsType<SwipeReading.Drag>(swipe.MovedTo(-30f, 9f, 0.1f));
    }

    [Fact]
    public void A_drag_as_steep_as_a_slanting_line_past_the_slop_down_is_no_swipe()
    {
        PageSwipe swipe = FingerDownAt(0f);

        Assert.IsType<SwipeReading.None>(swipe.MovedTo(-20f, 11f, 0.05f));
        Assert.IsType<SwipeReading.None>(swipe.MovedTo(-120f, 11f, 0.3f));
    }

    [Theory]
    [InlineData(-Width / 2f + 5f, 150f)]
    [InlineData(-Width / 2f + PageSwipe.BackGestureEdge - 1f, 150f)]
    [InlineData(Width / 2f - 5f, -150f)]
    public void A_touch_that_comes_down_at_the_screens_side_edge_is_left_to_the_back_gesture(float x, float across)
    {
        PageSwipe swipe = FingerDownAt(x);

        Assert.IsType<SwipeReading.None>(Slide(swipe, x, x + across, startsAt: 0f, seconds: 0.2f));
        Assert.False(swipe.IsDragging);
        Assert.IsType<SwipeReading.None>(swipe.Up(0.25f));
    }

    [Fact]
    public void A_tap_at_the_screens_edge_is_still_a_tap()
    {
        PageSwipe swipe = FingerDownAt(Width / 2f - 5f);

        Assert.IsType<SwipeReading.Tap>(swipe.Up(0.1f));
    }

    [Fact]
    public void A_touch_just_inside_the_edge_zone_swipes()
    {
        float x = Width / 2f - PageSwipe.BackGestureEdge - 1f;
        PageSwipe swipe = FingerDownAt(x);

        Assert.IsType<SwipeReading.Drag>(Slide(swipe, x, x - 100f, startsAt: 0f, seconds: 0.2f));
    }

    [Fact]
    public void A_slow_drag_past_a_third_of_the_screen_turns_to_the_neighbour_when_it_is_let_go()
    {
        PageSwipe swipe = FingerDownAt(140f);
        SwipeReading.Drag drag = Assert.IsType<SwipeReading.Drag>(Slide(swipe, 140f, 0f, startsAt: 0f, seconds: 1f));

        SwipeReading.Settle settle = Assert.IsType<SwipeReading.Settle>(swipe.Up(1f + Frame));

        Assert.True(Math.Abs(drag.By) >= PageSwipe.TurnShare * Width);
        Assert.Equal(3, settle.Place);
        Assert.Equal(drag.By, settle.From);
        Assert.False(swipe.IsDragging);
    }

    [Fact]
    public void A_slow_drag_short_of_a_third_of_the_screen_springs_back()
    {
        PageSwipe swipe = FingerDownAt(140f);
        Slide(swipe, 140f, 30f, startsAt: 0f, seconds: 1f);

        SwipeReading.Settle settle = Assert.IsType<SwipeReading.Settle>(swipe.Up(1f + Frame));

        Assert.Equal(2, settle.Place);
        Assert.Equal(-100f, settle.From, precision: 3);
    }

    [Fact]
    public void A_flick_turns_to_the_neighbour_though_the_drag_was_short()
    {
        PageSwipe swipe = FingerDownAt(60f);
        Slide(swipe, 60f, 0f, startsAt: 0f, seconds: 0.06f);

        SwipeReading.Settle settle = Assert.IsType<SwipeReading.Settle>(swipe.Up(0.07f));

        Assert.Equal(3, settle.Place);
        Assert.Equal(-50f, settle.From, precision: 3);
    }

    [Fact]
    public void A_twitch_too_short_to_be_a_flick_springs_back_however_fast_it_was()
    {
        PageSwipe swipe = FingerDownAt(0f);
        swipe.MovedTo(-12f, 0f, 0.01f);
        swipe.MovedTo(-25f, 0f, 0.02f);

        SwipeReading.Settle settle = Assert.IsType<SwipeReading.Settle>(swipe.Up(0.03f));

        Assert.True(Math.Abs(settle.From) < PageSwipe.FlickDistance);
        Assert.Equal(2, settle.Place);
    }

    [Fact]
    public void A_flick_back_after_a_long_drag_springs_back()
    {
        PageSwipe swipe = FingerDownAt(140f);
        Slide(swipe, 140f, -170f, startsAt: 0f, seconds: 1f);
        SwipeReading.Drag drag = Assert.IsType<SwipeReading.Drag>(Slide(swipe, -170f, -70f, startsAt: 1f, seconds: 0.1f));

        SwipeReading.Settle settle = Assert.IsType<SwipeReading.Settle>(swipe.Up(1.1f + Frame));

        Assert.True(Math.Abs(drag.By) >= PageSwipe.TurnShare * Width);
        Assert.Equal(2, settle.Place);
    }

    [Fact]
    public void The_speed_of_a_flick_is_the_fingers_at_the_end_not_the_whole_drags()
    {
        PageSwipe swipe = FingerDownAt(140f);
        Slide(swipe, 140f, 90f, startsAt: 0f, seconds: 1f);
        Slide(swipe, 90f, 30f, startsAt: 1f, seconds: 0.04f);

        SwipeReading.Settle settle = Assert.IsType<SwipeReading.Settle>(swipe.Up(1.04f + Frame));

        Assert.True(Math.Abs(settle.From) < PageSwipe.TurnShare * Width);
        Assert.Equal(3, settle.Place);
    }

    [Fact]
    public void A_finger_that_rested_before_it_lifted_flicks_nothing()
    {
        PageSwipe lifted = FingerDownAt(60f);
        Slide(lifted, 60f, 0f, startsAt: 0f, seconds: 0.06f);
        PageSwipe heldStill = FingerDownAt(60f);
        Slide(heldStill, 60f, 0f, startsAt: 0f, seconds: 0.06f);
        foreach (float seconds in new[] { 0.1f, 0.2f, 0.3f })
        {
            heldStill.MovedTo(0f, 0f, seconds); // a device reports a finger that rests on the glass as one that moved nowhere
        }

        Assert.Equal(2, Assert.IsType<SwipeReading.Settle>(lifted.Up(0.3f)).Place);
        Assert.Equal(2, Assert.IsType<SwipeReading.Settle>(heldStill.Up(0.3f)).Place);
    }

    [Fact]
    public void Before_the_first_page_the_page_only_stretches_and_springs_back()
    {
        var first = new SwipeRow(count: 4, shown: 0, Width);
        PageSwipe swipe = FingerDownAt(0f, first);

        SwipeReading.Drag drag = Assert.IsType<SwipeReading.Drag>(Slide(swipe, 0f, 170f, startsAt: 0f, seconds: 0.2f));
        SwipeReading.Settle settle = Assert.IsType<SwipeReading.Settle>(swipe.Up(0.2f + Frame));

        Assert.Null(drag.Beside);
        Assert.InRange(drag.By, 1f, PageSwipe.StretchShare * Width);
        Assert.True(drag.By < 170f - PageSwipe.Slop, "a page with nothing before it follows the finger only part of its way");
        Assert.Equal(0, settle.Place);
        Assert.Equal(drag.By, settle.From);
    }

    [Fact]
    public void After_the_last_page_the_page_stretches_further_the_further_the_finger_goes_and_never_past_its_limit()
    {
        var last = new SwipeRow(count: 4, shown: 3, Width);
        PageSwipe swipe = FingerDownAt(140f, last);
        float limit = PageSwipe.StretchShare * Width;

        float[] stretches = new[] { 100f, 0f, -170f, -3000f }
            .Select(x => Assert.IsType<SwipeReading.Drag>(swipe.MovedTo(x, 0f, 0.1f)))
            .Select(drag => -drag.By)
            .ToArray();

        Assert.Equal(stretches.OrderBy(stretch => stretch), stretches);
        Assert.All(stretches, stretch => Assert.InRange(stretch, 0f, limit - 0.01f));
        Assert.Equal(3, Assert.IsType<SwipeReading.Settle>(swipe.Up(0.1f + Frame)).Place);
    }

    [Fact]
    public void A_row_of_one_page_only_stretches_either_way()
    {
        var alone = new SwipeRow(count: 1, shown: 0, Width);
        PageSwipe swipe = FingerDownAt(0f, alone);

        Assert.Null(Assert.IsType<SwipeReading.Drag>(swipe.MovedTo(-80f, 0f, 0.1f)).Beside);
        Assert.Null(Assert.IsType<SwipeReading.Drag>(swipe.MovedTo(80f, 0f, 0.2f)).Beside);
        Assert.Equal(0, Assert.IsType<SwipeReading.Settle>(swipe.Up(0.21f)).Place);
    }

    [Fact]
    public void A_drag_never_moves_the_page_further_than_one_page()
    {
        PageSwipe swipe = FingerDownAt(140f);

        SwipeReading.Drag drag = Assert.IsType<SwipeReading.Drag>(swipe.MovedTo(-600f, 0f, 0.5f));

        Assert.Equal(-Width, drag.By);
    }

    [Fact]
    public void A_swipe_taken_away_springs_back_and_any_other_touch_taken_away_means_nothing()
    {
        PageSwipe swiping = FingerDownAt(140f);
        Slide(swiping, 140f, -100f, startsAt: 0f, seconds: 0.3f);
        PageSwipe tapping = FingerDownAt(0f);

        SwipeReading.Settle settle = Assert.IsType<SwipeReading.Settle>(swiping.Cancel());

        Assert.Equal(2, settle.Place);
        Assert.False(swiping.IsDragging);
        Assert.IsType<SwipeReading.None>(tapping.Cancel());
        Assert.IsType<SwipeReading.None>(tapping.Up(0.1f));
    }

    [Fact]
    public void A_finger_that_comes_down_forgets_the_one_before_it()
    {
        PageSwipe swipe = FingerDownAt(140f);
        swipe.MovedTo(60f, 0f, 0.1f);

        swipe.Down(0f, 0f, 0.2f, TheBar);

        Assert.False(swipe.IsDragging);
        Assert.IsType<SwipeReading.Tap>(swipe.Up(0.25f));
    }

    [Fact]
    public void Without_a_finger_down_nothing_means_anything()
    {
        var swipe = new PageSwipe();

        Assert.IsType<SwipeReading.None>(swipe.MovedTo(-100f, 0f, 0.1f));
        Assert.IsType<SwipeReading.None>(swipe.Up(0.2f));
        Assert.IsType<SwipeReading.None>(swipe.Cancel());
    }

    [Theory]
    [InlineData(0, 0, Width)]
    [InlineData(4, 4, Width)]
    [InlineData(4, -1, Width)]
    [InlineData(4, 2, 0f)]
    public void A_row_without_pages_a_shown_page_outside_it_or_a_screen_without_width_is_refused(int count, int shown, float width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwipeRow(count, shown, width));
    }

    [Theory]
    [InlineData(1f, Tempo.PageSettles)]
    [InlineData(-2f, Tempo.PageSettles)]
    [InlineData(0.5f, Tempo.PageSettles / 2f)]
    [InlineData(0.01f, Tempo.PageSettlesAtLeast)]
    [InlineData(0f, Tempo.PageSettlesAtLeast)]
    public void A_page_let_go_takes_the_part_of_a_whole_pages_time_that_its_way_left_is_of_a_page(float shareLeft, float seconds)
    {
        Assert.Equal(seconds, Tempo.PageSettlesIn(shareLeft), precision: 5);
    }
}
