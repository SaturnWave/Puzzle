using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// Lives in the game (docs/GAME_DESIGN.md section 19): five, one back every thirty minutes, one lost with a lost
/// attempt. The meter knows no clock of its own: every test hands it the time, as the game will.
/// </summary>
public class MetaLivesTests
{
    private static readonly LifeRules FiveLives = new(max: 5, perLife: TimeSpan.FromMinutes(30));

    private static LifeMeter MeterAfterLossesAt(params double[] minutes) =>
        minutes.Aggregate(LifeMeter.Full, (meter, minute) => meter.WithOneLost(MinutesLater(minute), FiveLives));

    private static int LivesAt(LifeMeter meter, double minute) => meter.SettledAt(MinutesLater(minute), FiveLives).LivesUnder(FiveLives);

    [Fact]
    public void A_new_player_has_every_life_and_no_clock()
    {
        LifeMeter meter = LifeMeter.Full;

        Assert.Equal(default, meter);
        Assert.True(meter.IsFull);
        Assert.Equal(5, meter.LivesUnder(FiveLives));
        Assert.Null(meter.NextLifeAt);
        Assert.Null(meter.UntilNextLife(Evening));
        Assert.Equal(LifeMeter.Full, meter.SettledAt(Evening.AddYears(3), FiveLives));
    }

    [Fact]
    public void A_lost_attempt_takes_one_life_and_only_the_first_loss_starts_the_clock()
    {
        LifeMeter afterOne = MeterAfterLossesAt(0);
        LifeMeter afterTwo = afterOne.WithOneLost(MinutesLater(10), FiveLives);

        Assert.Equal(4, afterOne.LivesUnder(FiveLives));
        Assert.Equal(MinutesLater(30), afterOne.NextLifeAt);
        Assert.Equal(TimeSpan.FromMinutes(18), afterOne.UntilNextLife(MinutesLater(12)));

        // The second loss does not start the clock again: the first life is still due thirty minutes after the first loss.
        Assert.Equal(3, afterTwo.LivesUnder(FiveLives));
        Assert.Equal(MinutesLater(30), afterTwo.NextLifeAt);
    }

    [Fact]
    public void One_life_comes_back_every_thirty_minutes_until_the_meter_is_full()
    {
        LifeMeter empty = MeterAfterLossesAt(0, 1, 2, 3, 4);

        Assert.Equal(0, empty.LivesUnder(FiveLives));
        Assert.False(empty.HasOneUnder(FiveLives));
        Assert.Equal(
            [0, 0, 1, 1, 2, 3, 4, 4, 5, 5],
            new[] { 5, 29.99, 30, 59.99, 60, 90, 120, 149.99, 150, 1000 }.Select(minute => LivesAt(empty, minute)));

        // After each life the next is due thirty minutes after that one, not after the time the meter was asked.
        LifeMeter askedLate = empty.SettledAt(MinutesLater(47), FiveLives);
        Assert.Equal(new LifeMeter(shortOfFull: 4, MinutesLater(60)), askedLate);
        Assert.Equal(TimeSpan.FromMinutes(13), askedLate.UntilNextLife(MinutesLater(47)));

        // Full again, the meter has no clock; a wait that is over is no wait.
        Assert.Equal(LifeMeter.Full, empty.SettledAt(MinutesLater(150), FiveLives));
        Assert.Equal(TimeSpan.Zero, empty.UntilNextLife(MinutesLater(45)));
    }

    [Fact]
    public void Lives_come_back_over_midnight_and_while_the_game_is_closed_for_days()
    {
        // 23:50, ten minutes before the player's midnight: the evening began at 21:00.
        const double TenToMidnight = 170;
        LifeMeter empty = MeterAfterLossesAt(TenToMidnight, TenToMidnight + 1, TenToMidnight + 2, TenToMidnight + 3, TenToMidnight + 4);

        Assert.Equal(0, LivesAt(empty, TenToMidnight + 29));
        Assert.Equal(1, LivesAt(empty, TenToMidnight + 30));  // 00:20 the next day
        Assert.Equal(3, LivesAt(empty, TenToMidnight + 100)); // 01:30
        Assert.Equal(LifeMeter.Full, empty.SettledAt(Evening.AddDays(1).AddHours(-12.5), FiveLives)); // 08:30 the next morning
        Assert.Equal(LifeMeter.Full, empty.SettledAt(Evening.AddDays(40), FiveLives));

        // The same instant on a clock of another zone is the same instant: a journey changes no wait.
        DateTimeOffset inAnotherZone = MinutesLater(TenToMidnight + 30).ToOffset(TimeSpan.FromHours(-5));
        Assert.Equal(1, empty.SettledAt(inAnotherZone, FiveLives).LivesUnder(FiveLives));
    }

    [Fact]
    public void A_clock_set_back_brings_no_life_and_takes_none()
    {
        LifeMeter twoLost = MeterAfterLossesAt(0, 1);

        // Asked at a time before the one it was last told: nothing comes back, nothing is lost, the life stays due when it was.
        Assert.Equal(twoLost, twoLost.SettledAt(Evening.AddDays(-1), FiveLives));
        Assert.Equal(twoLost, twoLost.SettledAt(Evening.AddYears(-10), FiveLives));

        // A life lost with the clock set back keeps the clock that runs: it does not start one in the past.
        LifeMeter lostInThePast = twoLost.WithOneLost(Evening.AddDays(-1), FiveLives);
        Assert.Equal(new LifeMeter(shortOfFull: 3, MinutesLater(30)), lostInThePast);

        // With the clock put right the lives come back by the real time, as if it had never been touched.
        Assert.Equal(new LifeMeter(shortOfFull: 2, MinutesLater(60)), lostInThePast.SettledAt(MinutesLater(30), FiveLives));
    }

    [Fact]
    public void A_life_that_fell_due_during_the_attempt_is_back_before_the_loss_counts()
    {
        LifeMeter oneLost = MeterAfterLossesAt(0);

        // The meter was full again at minute 30 and had no clock; the loss at minute 45 starts one of its own.
        LifeMeter lostAgain = oneLost.WithOneLost(MinutesLater(45), FiveLives);

        Assert.Equal(new LifeMeter(shortOfFull: 1, MinutesLater(75)), lostAgain);

        // With two missing, the one that came back at minute 30 leaves the clock running for the other: due at 60.
        LifeMeter twoLost = MeterAfterLossesAt(0, 1).WithOneLost(MinutesLater(45), FiveLives);
        Assert.Equal(new LifeMeter(shortOfFull: 2, MinutesLater(60)), twoLost);
    }

    [Fact]
    public void An_attempt_cannot_be_lost_without_a_life()
    {
        LifeMeter empty = MeterAfterLossesAt(0, 1, 2, 3, 4);

        Assert.Throws<InvalidOperationException>(() => empty.WithOneLost(MinutesLater(5), FiveLives));

        // Half an hour on there is one again, and it can be lost.
        Assert.Equal(0, empty.WithOneLost(MinutesLater(31), FiveLives).LivesUnder(FiveLives));
    }

    [Fact]
    public void A_meter_holds_what_is_missing_so_a_change_of_the_settings_cannot_break_it()
    {
        var sevenLives = new LifeRules(max: 7, perLife: TimeSpan.FromMinutes(30));
        var oneLife = new LifeRules(max: 1, perLife: TimeSpan.FromMinutes(30));
        LifeMeter twoLost = MeterAfterLossesAt(0, 1);

        // More lives in the settings: a full meter is still full, and one with two missing still misses two.
        Assert.Equal(7, LifeMeter.Full.LivesUnder(sevenLives));
        Assert.Equal(5, twoLost.LivesUnder(sevenLives));

        // Fewer lives: a meter that missed more than there are now is empty, never below, and full after one wait.
        LifeMeter empty = MeterAfterLossesAt(0, 1, 2, 3, 4);
        Assert.Equal(0, empty.LivesUnder(oneLife));
        Assert.Equal(new LifeMeter(shortOfFull: 1, MinutesLater(30)), empty.SettledAt(MinutesLater(10), oneLife));
        Assert.Equal(LifeMeter.Full, empty.SettledAt(MinutesLater(30), oneLife));
    }

    [Fact]
    public void A_meter_and_its_rules_are_stated_whole_or_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LifeMeter(shortOfFull: -1, nextLifeAt: null));
        Assert.Throws<ArgumentException>(() => new LifeMeter(shortOfFull: 2, nextLifeAt: null));
        Assert.Throws<ArgumentException>(() => new LifeMeter(shortOfFull: 0, Evening));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LifeRules(max: 0, perLife: TimeSpan.FromMinutes(30)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LifeRules(max: 5, perLife: TimeSpan.Zero));

        // Two meters that say the same are the same value, whatever zone their clock was read in.
        Assert.Equal(new LifeMeter(2, Evening), new LifeMeter(2, Evening.ToOffset(TimeSpan.Zero)));
        Assert.NotEqual(new LifeMeter(2, Evening), new LifeMeter(3, Evening));
        Assert.NotEqual(new LifeMeter(2, Evening), new LifeMeter(2, Evening.AddSeconds(1)));
        Assert.Equal(new LifeRules(5, TimeSpan.FromMinutes(30)), FiveLives);
    }
}
