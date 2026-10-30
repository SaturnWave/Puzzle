using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>Lives in the economy simulator (docs/GAME_DESIGN.md section 19): five, one back every thirty minutes, one lost with a lost attempt and with nothing else.</summary>
public class EconomyLivesTests
{
    private static readonly LivesSettings FiveLives = new() { Max = new LifeCount(5), PerLife = TimeSpan.FromMinutes(30) };

    private static PlayerTime Minute(int minutes) => new(TimeSpan.FromMinutes(minutes));

    private static LifeMeter MeterAfterLosses(params int[] atMinutes)
    {
        var meter = new LifeMeter(FiveLives);
        foreach (int minute in atMinutes)
        {
            meter.LoseOne(Minute(minute));
        }

        return meter;
    }

    private static int LivesAt(LifeMeter meter, int minute)
    {
        meter.BringUpTo(Minute(minute));
        return meter.Lives.Value;
    }

    [Fact]
    public void A_life_is_lost_only_on_a_lost_attempt()
    {
        HandMadeLevels levels = new HandMadeLevels().EasyLevels(3, moveLimit: 6, movesLeft: 1).Level(6, Always(Unfinished()));

        JourneyList journey = Journey(OnePlayer(), levels);

        AttemptPlayed[] firstSession = journey.Attempts.Take(8).ToArray();
        Assert.Equal([true, true, true, false, false, false, false, false], firstSession.Select(attempt => attempt.IsWon));
        Assert.Equal([5, 5, 5, 4, 3, 2, 1, 0], firstSession.Select(attempt => attempt.LivesLeft.Value));
    }

    [Fact]
    public void A_life_comes_back_every_thirty_minutes_up_to_five()
    {
        LifeMeter meter = MeterAfterLosses(0, 0, 0, 0, 0);

        Assert.Equal(0, LivesAt(meter, 29));
        Assert.Equal(1, LivesAt(meter, 30));
        Assert.Equal(3, LivesAt(meter, 90));
        Assert.Equal(5, LivesAt(meter, 150));
        Assert.Equal(5, LivesAt(meter, 600));
    }

    [Fact]
    public void The_clock_of_the_next_life_does_not_restart_on_a_second_loss()
    {
        LifeMeter meter = MeterAfterLosses(0, 20);

        Assert.Equal(3, LivesAt(meter, 29));
        Assert.Equal(4, LivesAt(meter, 30));
        Assert.Equal(4, LivesAt(meter, 59));
        Assert.Equal(5, LivesAt(meter, 60));

        // A full meter has no clock: the next loss starts one of its own.
        meter.LoseOne(Minute(100));
        Assert.Equal(4, LivesAt(meter, 129));
        Assert.Equal(5, LivesAt(meter, 130));
    }

    [Fact]
    public void A_life_that_falls_due_during_a_lost_attempt_is_back_before_the_loss_starts_a_new_clock()
    {
        // One life down since minute 0: the next is due at minute 30. An attempt runs across that minute and is lost
        // at minute 31. In the game the meter was full at 30 and its clock stopped; the loss at 31 starts a new one.
        LifeMeter meter = MeterAfterLosses(0);

        meter.LoseOne(Minute(31));

        Assert.Equal(4, meter.Lives.Value);
        Assert.Equal(TimeSpan.FromMinutes(30), meter.UntilNextLife(Minute(31)));
        Assert.Equal(4, LivesAt(meter, 60));
        Assert.Equal(5, LivesAt(meter, 61));
        Assert.Null(meter.UntilNextLife(Minute(61)));
        Assert.Equal(2, meter.LivesBack);

        // Two down, and only one falls due during the attempt: the meter was never full, so the old clock runs on.
        LifeMeter twoDown = MeterAfterLosses(0, 10);
        twoDown.LoseOne(Minute(31));
        Assert.Equal(3, twoDown.Lives.Value);
        Assert.Equal(4, LivesAt(twoDown, 60));
        Assert.Equal(5, LivesAt(twoDown, 90));
    }

    [Fact]
    public void A_session_tells_what_the_timer_did_for_it()
    {
        // Two minutes a life, a lost attempt a minute: lives come back while the session runs, and it still ends by lives.
        EconomySettings quickLives = OnePlayer("""{ "lives": { "minutesPerLife": 2 } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished()));

        SessionEnded first = Journey(quickLives, levels).Sessions.First();

        // Lost at minutes 1 to 8; a life back at 3, 5 and 7; the next is due at 9.
        Assert.Equal(SessionEnd.LivesGone, first.End);
        Assert.Equal(8, first.Attempts);
        Assert.Equal(new SessionLives(FullAtStart: true, BackDuringIt: 3, UntilNextLife: TimeSpan.FromMinutes(1)), first.Lives);

        // Ten hours a life: nothing comes back in a session, the second starts empty and the third with one life.
        EconomySettings slowLives = OnePlayer("""{ "lives": { "minutesPerLife": 600 } }""");
        SessionEnded[] firstDay = Journey(slowLives, levels).Sessions.Take(3).ToArray();

        // The first loss is at 08:31, so a life is back at 18:31; the sessions end at 08:35, 13:00 and 21:31.
        Assert.Equal(new SessionLives(FullAtStart: true, BackDuringIt: 0, UntilNextLife: new TimeSpan(9, 56, 0)), firstDay[0].Lives);
        Assert.Equal(new SessionLives(FullAtStart: false, BackDuringIt: 0, UntilNextLife: new TimeSpan(5, 31, 0)), firstDay[1].Lives);
        Assert.Equal(new SessionLives(FullAtStart: false, BackDuringIt: 0, UntilNextLife: new TimeSpan(7, 0, 0)), firstDay[2].Lives);

        // A session that the ceiling ends waits for no life.
        SessionEnded wonThroughout = Journey(OnePlayer(), new HandMadeLevels().EasyLevels(100)).Sessions.First();
        Assert.Equal(new SessionLives(FullAtStart: true, BackDuringIt: 0, UntilNextLife: null), wonThroughout.Lives);
    }

    [Fact]
    public void No_attempt_starts_without_a_life()
    {
        // Ten hours for a life: the five lost from 08:31 on are not back at 13:00, and one is at 21:30.
        EconomySettings settings = OnePlayer("""{ "lives": { "minutesPerLife": 600 } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished()));

        JourneyList journey = Journey(settings, levels);

        SessionEnded[] firstDay = journey.Sessions.Take(3).ToArray();
        Assert.Equal([5, 0, 1], firstDay.Select(session => session.Attempts));
        Assert.Equal([SessionEnd.LivesGone, SessionEnd.NoLifeAtStart, SessionEnd.LivesGone], firstDay.Select(session => session.End));
        Assert.All(journey.Attempts, attempt => Assert.True(attempt.LivesLeft.Value >= 0));
        Assert.Throws<InvalidOperationException>(() => MeterAfterLosses(0, 0, 0, 0, 0).LoseOne(Minute(1)));
    }
}
