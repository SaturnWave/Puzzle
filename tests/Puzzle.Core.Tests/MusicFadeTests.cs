using Puzzle.Client.Sound;

namespace Puzzle.Core.Tests;

/// <summary>
/// How the game's music follows the player's switch, before anything is heard: it fades out in
/// <see cref="MusicFade.Seconds"/> when turned off and back in as fast when turned on, from wherever it was, and is
/// played under the effects. <c>MusicFade</c> and <c>SoundLevels</c> are the Unity project's source files, compiled
/// into these tests; the Editor's MusicCheck hears the same in a running game.
/// </summary>
public class MusicFadeTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void The_music_begins_heard_or_silent_as_the_switch_stands_when_the_game_starts()
    {
        Assert.Equal(1f, new MusicFade(isOn: true).Share);
        Assert.False(new MusicFade(isOn: true).IsSilent);
        Assert.True(new MusicFade(isOn: false).IsSilent);
    }

    [Fact]
    public void Turned_off_the_music_fades_out_over_its_seconds_and_is_silent_then_and_not_before()
    {
        var fade = new MusicFade(isOn: true);
        int frames = 0;
        while (!fade.IsSilent)
        {
            fade.Follow(isOn: false, Frame);
            frames++;
        }

        // A frame more at most, where the steps' rounding leaves a trace of sound after the last whole step.
        Assert.InRange(frames * Frame, MusicFade.Seconds - Frame, MusicFade.Seconds + 2 * Frame);
        Assert.InRange(MusicFade.Seconds, 0.2f, 0.4f);
    }

    [Fact]
    public void Turned_on_again_halfway_the_music_comes_back_from_where_it_was_without_a_jump()
    {
        var fade = new MusicFade(isOn: true);
        fade.Follow(isOn: false, MusicFade.Seconds / 2f);
        float halfway = fade.Share;

        fade.Follow(isOn: true, MusicFade.Seconds / 6f);

        Assert.Equal(0.5f, halfway, precision: 4);
        Assert.Equal(0.5f + 1f / 6f, fade.Share, precision: 4);
        fade.Follow(isOn: true, MusicFade.Seconds);
        Assert.Equal(1f, fade.Share);
    }

    [Fact]
    public void Put_away_with_the_game_the_music_is_silent_at_once_and_fades_in_when_it_comes_back_on()
    {
        var fade = new MusicFade(isOn: true);

        fade.Silence();
        Assert.True(fade.IsSilent);
        fade.Follow(isOn: true, MusicFade.Seconds / 3f);

        Assert.Equal(1f / 3f, fade.Share, precision: 4);
    }

    [Fact]
    public void A_fade_at_either_end_is_at_rest_and_one_halfway_is_not()
    {
        var fade = new MusicFade(isOn: true);
        bool heardIsAtRest = fade.IsAtRest;

        fade.Follow(isOn: false, MusicFade.Seconds / 2f);
        bool halfwayIsAtRest = fade.IsAtRest;
        fade.Follow(isOn: false, MusicFade.Seconds);

        Assert.True(heardIsAtRest);
        Assert.False(halfwayIsAtRest);
        Assert.True(fade.IsAtRest);
    }

    [Fact]
    public void Time_that_runs_backwards_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MusicFade(isOn: true).Follow(isOn: false, -Frame));
    }

    [Fact]
    public void The_music_is_played_about_5_dB_under_the_effects()
    {
        double underTheEffects = 20 * Math.Log10(SoundLevels.Music / SoundLevels.Effects);

        Assert.InRange(underTheEffects, -6.0, -4.0);
    }
}
