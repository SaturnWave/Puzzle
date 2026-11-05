using Puzzle.Client.Screens;

namespace Puzzle.Core.Tests;

/// <summary>
/// The settings card's switch in what it shows, before anything is drawn: the knob at the right end and the lit track
/// when on, at the left end and the dull track when off, and a slide from one to the other that never shows both or
/// goes back on itself. <c>ToggleLook</c> is the Unity project's source file, compiled into these tests.
/// </summary>
public class ToggleLookTests
{
    [Fact]
    public void At_rest_a_switch_that_is_on_has_its_knob_right_on_the_lit_track_and_one_that_is_off_left_on_the_dull_one()
    {
        ToggleLook on = ToggleLook.AtRest(isOn: true);
        ToggleLook off = ToggleLook.AtRest(isOn: false);

        Assert.Equal((ToggleLook.KnobReach, 1f), (on.KnobOffset, on.LitTrack));
        Assert.Equal((-ToggleLook.KnobReach, 0f), (off.KnobOffset, off.LitTrack));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_slide_begins_where_the_other_state_rests_and_ends_where_its_own_does(bool isOn)
    {
        Assert.Equal(ToggleLook.AtRest(!isOn), ToggleLook.Sliding(isOn, 0f));
        Assert.Equal(ToggleLook.AtRest(isOn), ToggleLook.Sliding(isOn, 1f));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_knob_and_the_light_move_one_way_only_during_a_slide(bool isOn)
    {
        ToggleLook before = ToggleLook.Sliding(isOn, 0f);
        for (int step = 1; step <= 20; step++)
        {
            ToggleLook now = ToggleLook.Sliding(isOn, step / 20f);
            float knobMoved = now.KnobOffset - before.KnobOffset;
            float lightChanged = now.LitTrack - before.LitTrack;

            Assert.True(isOn ? knobMoved >= 0f && lightChanged >= 0f : knobMoved <= 0f && lightChanged <= 0f, $"step {step} went back");
            before = now;
        }
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    public void A_slide_is_not_drawn_outside_its_run(float progress)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ToggleLook.Sliding(isOn: true, progress));
    }
}
