using Puzzle.Client.Play;
using Puzzle.Meta;

namespace Puzzle.Core.Tests;

public class HintWatchTests
{
    private static readonly HintRules FiveSecondsThreeHints = new(TimeSpan.FromSeconds(5), perAttempt: 3, bot: "casual");

    [Fact]
    public void A_hint_is_due_only_once_the_board_has_lain_quiet_and_untouched_for_the_wait()
    {
        var watch = new HintWatch(FiveSecondsThreeHints, clockSeconds: 10f);

        Assert.False(watch.IsDue(14.9f, isQuiet: true));
        Assert.True(watch.IsDue(15f, isQuiet: true));
    }

    [Fact]
    public void A_hint_shows_until_a_touch_takes_it_away_and_the_wait_starts_again_from_the_touch()
    {
        var watch = new HintWatch(FiveSecondsThreeHints, clockSeconds: 0f);
        Assert.True(watch.IsDue(5f, isQuiet: true));
        watch.Show(5f);

        Assert.True(watch.IsShowing);
        Assert.False(watch.IsDue(30f, isQuiet: true)); // one shows already: it stays until it is touched away
        watch.Touched(31f);
        Assert.False(watch.IsShowing);
        Assert.False(watch.IsDue(35.9f, isQuiet: true));
        Assert.True(watch.IsDue(36f, isQuiet: true));
    }

    [Fact]
    public void A_board_that_is_not_quiet_holds_the_wait_back_and_takes_a_showing_hint_away()
    {
        var watch = new HintWatch(FiveSecondsThreeHints, clockSeconds: 0f);

        Assert.False(watch.IsDue(4f, isQuiet: false)); // a cascade, a card or an armed booster until second 4
        Assert.False(watch.IsDue(8.9f, isQuiet: true));
        Assert.True(watch.IsDue(9f, isQuiet: true));
        watch.Show(9f);
        Assert.False(watch.IsDue(9.5f, isQuiet: false));
        Assert.False(watch.IsShowing);
        Assert.Equal(1, watch.Shown);
    }

    [Fact]
    public void An_attempt_shows_no_more_hints_than_its_cap_and_a_cap_of_none_shows_none()
    {
        var watch = new HintWatch(FiveSecondsThreeHints, clockSeconds: 0f);
        for (int hint = 0; hint < 3; hint++)
        {
            float due = (hint + 1) * 10f;
            Assert.True(watch.IsDue(due, isQuiet: true));
            watch.Show(due);
            watch.Touched(due + 5f);
        }

        Assert.False(watch.IsDue(1000f, isQuiet: true));
        Assert.Equal(3, watch.Shown);
        Assert.Throws<InvalidOperationException>(() => watch.Show(1000f));
        Assert.False(new HintWatch(new HintRules(TimeSpan.FromSeconds(5), perAttempt: 0, bot: "casual"), 0f).IsDue(1000f, isQuiet: true));
    }

    [Fact]
    public void A_hint_that_finds_no_move_is_not_counted_and_waits_again()
    {
        var watch = new HintWatch(FiveSecondsThreeHints, clockSeconds: 0f);
        Assert.True(watch.IsDue(5f, isQuiet: true));

        watch.FoundNoMove(5f);

        Assert.Equal(0, watch.Shown);
        Assert.False(watch.IsDue(9f, isQuiet: true));
        Assert.True(watch.IsDue(10f, isQuiet: true));
    }
}
