using Puzzle.Client.Stage;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Unity game's stage clock: tweens move only when they are told how much time has passed. <c>Tween</c> is the
/// Unity project's source file, compiled into these tests.
/// </summary>
public class StageTweenTests
{
    private static float Unchanged(float progress) => progress;

    [Fact]
    public void A_tween_puts_its_target_at_the_start_when_it_is_made()
    {
        float seen = -1f;

        _ = new Tween(delaySeconds: 0.5f, durationSeconds: 1f, Unchanged, progress => seen = progress);

        Assert.Equal(0f, seen);
    }

    [Fact]
    public void A_tween_waits_out_its_delay_then_runs_to_its_end()
    {
        float seen = -1f;
        var tween = new Tween(delaySeconds: 0.5f, durationSeconds: 1f, Unchanged, progress => seen = progress);

        tween.Advance(0.5f);
        Assert.Equal(0f, seen);
        Assert.False(tween.IsFinished);

        tween.Advance(0.25f);
        Assert.Equal(0.25f, seen, 0.0001f);

        tween.Advance(5f);
        Assert.Equal(1f, seen);
        Assert.True(tween.IsFinished);
    }

    [Fact]
    public void Many_small_steps_end_where_one_large_step_ends()
    {
        float inFrames = -1f;
        float atOnce = -1f;
        var frames = new Tween(0.1f, 0.3f, Easing.Overshoot, progress => inFrames = progress);
        var jump = new Tween(0.1f, 0.3f, Easing.Overshoot, progress => atOnce = progress);

        for (int frame = 0; frame < 15; frame++)
        {
            frames.Advance(1f / 60f);
        }

        jump.Advance(15f / 60f);

        Assert.Equal(atOnce, inFrames, 0.001f);
    }

    [Fact]
    public void A_set_is_idle_once_every_tween_has_finished()
    {
        var tweens = new TweenSet();
        Assert.True(tweens.IsIdle);

        tweens.Start(new Tween(0f, 0.2f, Unchanged, _ => { }));
        tweens.Start(new Tween(0.3f, 0.2f, Unchanged, _ => { }));
        tweens.Advance(0.25f);
        Assert.False(tweens.IsIdle);

        tweens.Advance(0.25f);
        Assert.True(tweens.IsIdle);
    }

    [Fact]
    public void The_clock_does_not_run_backwards()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TweenSet().Advance(-0.1f));
    }

    [Fact]
    public void A_tween_needs_a_duration_and_cannot_start_in_the_past()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tween(0f, 0f, Unchanged, _ => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tween(-1f, 1f, Unchanged, _ => { }));
    }

    [Fact]
    public void The_overshoot_curve_starts_at_nothing_passes_its_end_and_settles_on_it()
    {
        Assert.Equal(0f, Easing.Overshoot(0f), 0.0001f);
        Assert.Equal(1f, Easing.Overshoot(1f), 0.0001f);
        Assert.True(Easing.Overshoot(0.7f) > 1f);
    }
}
