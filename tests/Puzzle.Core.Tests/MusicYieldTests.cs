using Puzzle.Client.Sound;

namespace Puzzle.Core.Tests;

/// <summary>
/// How the game's music gives way to the player's own music (the owner's decision of 2026-10-23): silent while another
/// app's music plays, back once it has stopped, asked at start, when the game comes to the front and every few
/// seconds, never while the music's own fade moves, and without coming in and out between two songs.
/// <c>MusicYield</c> is the Unity project's source file, compiled into these tests; the Editor's MusicCheck hears the
/// same in a running game with a stand-in for the other app.
/// </summary>
public class MusicYieldTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void As_the_game_starts_the_music_gives_way_only_if_another_app_plays_then()
    {
        Assert.True(new MusicYield(new CountedOtherAudio { IsPlaying = true }).IsGivingWay);
        Assert.False(new MusicYield(new CountedOtherAudio()).IsGivingWay);
    }

    [Fact]
    public void While_the_game_runs_the_device_is_asked_every_few_seconds_and_not_in_between()
    {
        var other = new CountedOtherAudio();
        var yield = new MusicYield(other);
        int askedAtStart = other.Asked;

        // A few frames over three intervals: each interval may end a frame late, as the frames add up.
        Run(yield, seconds: 3 * MusicYield.AskEverySeconds + 5 * Frame);

        Assert.Equal(1, askedAtStart);
        Assert.Equal(1 + 3, other.Asked);
        Assert.InRange(MusicYield.AskEverySeconds, 1f, 5f);
    }

    [Fact]
    public void Another_app_that_begins_to_play_is_given_way_to_at_the_next_question()
    {
        var other = new CountedOtherAudio();
        var yield = new MusicYield(other);
        Run(yield, seconds: MusicYield.AskEverySeconds / 2f);

        other.IsPlaying = true;
        float waited = RunUntil(yield, () => yield.IsGivingWay);

        Assert.InRange(waited, 0f, MusicYield.AskEverySeconds + 2 * Frame);
    }

    [Fact]
    public void Once_the_other_app_stops_the_music_comes_back_after_two_quiet_answers_and_not_at_the_first()
    {
        var other = new CountedOtherAudio { IsPlaying = true };
        var yield = new MusicYield(other);
        Run(yield, seconds: 2 * MusicYield.AskEverySeconds + Frame);

        other.IsPlaying = false;
        int askedWhenItStopped = other.Asked;
        float waited = RunUntil(yield, () => !yield.IsGivingWay);

        Assert.Equal(2, other.Asked - askedWhenItStopped);
        // Each asking interval may end a frame late, as the frames add up.
        Assert.InRange(waited, MusicYield.QuietBeforeReturnSeconds - MusicYield.AskEverySeconds, 2 * MusicYield.AskEverySeconds + 3 * Frame);
        Assert.InRange(MusicYield.QuietBeforeReturnSeconds, MusicYield.AskEverySeconds + Frame, 2 * MusicYield.AskEverySeconds);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.7f)]
    [InlineData(1.3f)]
    [InlineData(1.9f)]
    public void A_gap_between_two_songs_shorter_than_an_asking_interval_does_not_bring_the_music_in_and_out(float gapBeginsAfter)
    {
        var other = new CountedOtherAudio { IsPlaying = true };
        var yield = new MusicYield(other);
        Run(yield, seconds: gapBeginsAfter);

        other.IsPlaying = false;
        bool cameBack = RunWatching(yield, seconds: MusicYield.AskEverySeconds - 2 * Frame);
        other.IsPlaying = true;
        cameBack |= RunWatching(yield, seconds: 3 * MusicYield.AskEverySeconds);

        Assert.False(cameBack);
        Assert.True(yield.IsGivingWay);
    }

    [Fact]
    public void Nothing_is_asked_while_the_music_fades_and_a_question_put_off_is_asked_as_soon_as_the_fade_ends()
    {
        var other = new CountedOtherAudio();
        var yield = new MusicYield(other);
        Run(yield, seconds: MusicYield.AskEverySeconds - 3 * Frame);
        int askedBeforeTheFade = other.Asked;

        Run(yield, seconds: MusicFade.Seconds, isFading: true);
        int askedDuringTheFade = other.Asked - askedBeforeTheFade;
        yield.Advance(Frame, isFading: false);

        Assert.Equal(0, askedDuringTheFade);
        Assert.Equal(askedBeforeTheFade + 1, other.Asked);
    }

    [Fact]
    public void Coming_to_the_front_asks_at_once_and_follows_the_answer_at_once_both_ways()
    {
        var other = new CountedOtherAudio { IsPlaying = true };
        var yield = new MusicYield(other);

        other.IsPlaying = false;
        yield.AskNow();
        bool gaveWayAfterAQuietReturn = yield.IsGivingWay;
        other.IsPlaying = true;
        yield.AskNow();

        Assert.False(gaveWayAfterAQuietReturn);
        Assert.True(yield.IsGivingWay);
        Assert.Equal(3, other.Asked);
    }

    [Fact]
    public void Coming_to_the_front_starts_the_asking_interval_again()
    {
        var other = new CountedOtherAudio();
        var yield = new MusicYield(other);
        Run(yield, seconds: MusicYield.AskEverySeconds - 3 * Frame);

        yield.AskNow();
        int askedOnComingBack = other.Asked;
        Run(yield, seconds: MusicYield.AskEverySeconds - 3 * Frame);

        Assert.Equal(askedOnComingBack, other.Asked);
    }

    [Fact]
    public void Time_that_runs_backwards_and_a_missing_device_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MusicYield(new CountedOtherAudio()).Advance(-Frame, isFading: false));
        Assert.Throws<ArgumentNullException>(() => new MusicYield(null!));
    }

    [Fact]
    public void The_stand_in_of_the_Editor_and_of_other_devices_plays_nothing_until_it_is_told_to()
    {
        var standIn = new OtherAudioStandIn();
        bool atFirst = standIn.IsPlaying;

        standIn.IsPlaying = true;

        Assert.False(atFirst);
        Assert.True(new MusicYield(standIn).IsGivingWay);
    }

    /// <summary>Runs the watch frame by frame for the time given.</summary>
    private static void Run(MusicYield yield, float seconds, bool isFading = false)
    {
        for (float ran = 0f; ran < seconds; ran += Frame)
        {
            yield.Advance(Frame, isFading);
        }
    }

    /// <summary>Runs the watch frame by frame until the condition holds; returns the time it took. Gives up after a minute.</summary>
    private static float RunUntil(MusicYield yield, Func<bool> holds)
    {
        float ran = 0f;
        while (!holds())
        {
            Assert.True(ran < 60f, "it did not come about in a minute");
            yield.Advance(Frame, isFading: false);
            ran += Frame;
        }

        return ran;
    }

    /// <summary>Runs the watch frame by frame for the time given; returns whether the music came back at any frame.</summary>
    private static bool RunWatching(MusicYield yield, float seconds)
    {
        bool cameBack = false;
        for (float ran = 0f; ran < seconds; ran += Frame)
        {
            yield.Advance(Frame, isFading: false);
            cameBack |= !yield.IsGivingWay;
        }

        return cameBack;
    }

    /// <summary>Another app's audio as the test sets it, counting how often the device was asked.</summary>
    private sealed class CountedOtherAudio : IOtherAudio
    {
        private bool _isPlaying;

        public int Asked { get; private set; }

        public bool IsPlaying
        {
            get
            {
                Asked++;
                return _isPlaying;
            }

            set => _isPlaying = value;
        }
    }
}
