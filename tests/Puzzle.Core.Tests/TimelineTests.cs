using Puzzle.Client.Play;

namespace Puzzle.Core.Tests;

/// <summary>
/// The clock that plays a turn in the Unity game, and the law its pieces fall by. <c>Timeline</c> and <c>FallLaw</c>
/// are the Unity project's source files, compiled into these tests: they have no Unity dependency.
/// </summary>
public class TimelineTests
{
    [Fact]
    public void An_act_does_not_touch_its_target_before_it_begins()
    {
        var timeline = new Timeline();
        var seen = new List<float>();
        timeline.Add(startsIn: 0.5f, seconds: 1f, seen.Add);

        timeline.Advance(0.25f);
        Assert.Empty(seen);

        timeline.Advance(0.5f);
        Assert.Equal(0.25f, Assert.Single(seen), 0.0001f);
    }

    [Fact]
    public void An_act_ends_on_exactly_one_and_is_then_told_that_it_is_over()
    {
        var timeline = new Timeline();
        var seen = new List<string>();
        timeline.Add(0f, 0.1f, progress => seen.Add(progress == 1f ? "end" : "on the way"), () => seen.Add("over"));

        timeline.Advance(0.05f);
        timeline.Advance(5f);
        timeline.Advance(1f);

        Assert.Equal(new[] { "on the way", "end", "over" }, seen);
        Assert.True(timeline.IsDone);
    }

    [Fact]
    public void Something_of_one_moment_happens_once_when_its_moment_has_come()
    {
        var timeline = new Timeline();
        int happened = 0;
        timeline.At(0.2f, () => happened++);

        timeline.Advance(0.1f);
        Assert.Equal(0, happened);
        Assert.False(timeline.IsDone);

        timeline.Advance(0.1f);
        timeline.Advance(0.1f);
        Assert.Equal(1, happened);
    }

    [Fact]
    public void Acts_are_shown_in_the_order_they_begin_so_the_later_one_has_the_last_word()
    {
        var timeline = new Timeline();
        var shown = new List<string>();
        timeline.Add(0.2f, 1f, _ => shown.Add("later"));
        timeline.Add(0.1f, 1f, _ => shown.Add("earlier"));
        timeline.Add(0.2f, 1f, _ => shown.Add("later still"));

        timeline.Advance(0.5f);

        Assert.Equal(new[] { "earlier", "later", "later still" }, shown);
    }

    [Fact]
    public void A_step_that_ends_one_act_and_begins_the_next_shows_the_end_first()
    {
        var timeline = new Timeline();
        var shown = new List<string>();
        timeline.Add(0f, 0.1f, _ => { }, () => shown.Add("fall is over"));
        timeline.Add(0.1f, 0.1f, _ => shown.Add("landing"));

        timeline.Advance(0.15f);

        Assert.Equal(new[] { "fall is over", "landing" }, shown);
    }

    [Fact]
    public void Acts_are_timed_from_the_moment_they_are_added()
    {
        var timeline = new Timeline();
        float seen = -1f;
        timeline.Advance(3f);
        timeline.Add(0.5f, 1f, progress => seen = progress);

        timeline.Advance(1f);

        Assert.Equal(0.5f, seen, 0.0001f);
        Assert.Equal(4f, timeline.Now);
    }

    [Fact]
    public void Many_small_steps_end_where_one_large_step_ends()
    {
        float inFrames = -1f;
        float atOnce = -1f;
        var frames = new Timeline();
        var jump = new Timeline();
        frames.Add(0.1f, 0.3f, progress => inFrames = progress);
        jump.Add(0.1f, 0.3f, progress => atOnce = progress);

        for (int frame = 0; frame < 15; frame++)
        {
            frames.Advance(1f / 60f);
        }

        jump.Advance(15f / 60f);

        Assert.Equal(atOnce, inFrames, 0.001f);
    }

    [Fact]
    public void Time_does_not_run_backwards_and_acts_do_not_begin_in_the_past()
    {
        var timeline = new Timeline();

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Advance(-0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Add(-0.1f, 1f, _ => { }));
    }

    [Fact]
    public void An_act_cannot_add_acts_while_it_is_shown()
    {
        var timeline = new Timeline();
        timeline.At(0f, () => timeline.At(0f, () => { }));

        Assert.Throws<InvalidOperationException>(() => timeline.Advance(0.1f));
    }

    [Fact]
    public void An_act_that_throws_is_dropped_and_the_others_go_on_as_if_it_had_never_been()
    {
        var timeline = new Timeline();
        var shown = new List<string>();
        timeline.Add(0f, 0.1f, _ => { }, () => shown.Add("the burst is over"));
        timeline.Add(0.05f, 1f, _ => throw new InvalidOperationException("the piece is gone"));
        timeline.Add(0.1f, 0.2f, progress => shown.Add($"fall at {progress:0.0}"), () => shown.Add("the fall is over"));

        // The step in which the act throws: the exception is not hidden, what was over stays over.
        Assert.Throws<InvalidOperationException>(() => timeline.Advance(0.2f));
        Assert.Equal(new[] { "the burst is over" }, shown);
        Assert.False(timeline.IsDone);

        // The steps after it: the act that threw is not shown again, the act behind it is, and the timeline ends.
        timeline.Advance(0.05f);
        timeline.Advance(0.05f);

        Assert.Equal(new[] { "the burst is over", "fall at 0.8", "fall at 1.0", "the fall is over" }, shown);
        Assert.True(timeline.IsDone);
    }

    [Fact]
    public void An_end_that_throws_is_dropped_with_its_act()
    {
        var timeline = new Timeline();
        timeline.At(0.1f, () => throw new InvalidOperationException("the plaque is gone"));

        Assert.Throws<InvalidOperationException>(() => timeline.Advance(0.2f));

        timeline.Advance(0.1f);
        Assert.True(timeline.IsDone);
    }

    [Fact]
    public void A_cleared_timeline_is_done_and_tells_no_act_that_it_is_over()
    {
        var timeline = new Timeline();
        var seen = new List<string>();
        timeline.Add(0f, 1f, _ => seen.Add("shown"), () => seen.Add("over"));
        timeline.At(0.5f, () => seen.Add("later"));
        timeline.Advance(0.1f);

        timeline.Clear();
        timeline.Advance(5f);

        Assert.Equal(new[] { "shown" }, seen);
        Assert.True(timeline.IsDone);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    [InlineData(12f)]
    public void A_fall_takes_as_long_as_the_law_needs_to_cover_its_distance(float cells)
    {
        Assert.Equal(cells, FallLaw.CellsAfter(FallLaw.SecondsFor(cells)), 0.001f);
    }

    [Fact]
    public void Falls_are_quick_a_row_in_about_a_tenth_of_a_second_a_whole_column_in_under_half()
    {
        Assert.InRange(FallLaw.SecondsFor(1f), 0.08f, 0.13f);
        Assert.InRange(FallLaw.SecondsFor(12f), 0.3f, 0.5f);
    }

    [Fact]
    public void A_piece_that_lets_go_later_never_catches_up_with_the_one_under_it()
    {
        // Both start a row apart; the upper one lets go a moment later and has further to fall.
        float lag = Tempo.FollowLag;
        for (float seconds = lag; seconds < 0.5f; seconds += 1f / 240f)
        {
            float lower = 1f + FallLaw.CellsAfter(seconds);
            float upper = FallLaw.CellsAfter(seconds - lag);
            Assert.True(lower - upper >= 1f, $"the pieces overlap after {seconds} s");
        }
    }

    [Fact]
    public void Each_round_of_a_cascade_is_a_little_quicker_down_to_a_floor()
    {
        Assert.Equal(1f, Tempo.PaceOfRound(0));
        Assert.True(Tempo.PaceOfRound(2) < Tempo.PaceOfRound(1));
        Assert.Equal(Tempo.PaceOfRound(30), Tempo.PaceOfRound(40));
        Assert.True(Tempo.PaceOfRound(40) > 0.5f);
    }
}
