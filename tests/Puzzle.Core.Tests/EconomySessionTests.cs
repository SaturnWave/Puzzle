using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// Sessions of a simulated player: three a day, each until the lives are gone or the session has lasted its ceiling.
/// The tests' player takes ten seconds a move and no pause between attempts.
/// </summary>
public class EconomySessionTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(20);

    [Fact]
    public void A_session_ends_when_the_lives_are_gone()
    {
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished()));

        SessionEnded first = Journey(OnePlayer(), levels).Sessions.First();

        Assert.Equal(SessionEnd.LivesGone, first.End);
        Assert.Equal(5, first.Attempts);
        Assert.Equal(TimeSpan.FromMinutes(5), first.Length);
    }

    [Fact]
    public void A_session_ends_at_the_ceiling_though_every_attempt_was_won()
    {
        // Fifteen moves to a win: 150 seconds an attempt, so the eighth ends exactly as the ceiling is reached.
        HandMadeLevels levels = new HandMadeLevels().EasyLevels(100, moveLimit: 20, movesLeft: 5);

        JourneyList journey = Journey(OnePlayer(), levels);

        SessionEnded first = journey.Sessions.First();
        Assert.Equal(SessionEnd.CeilingReached, first.End);
        Assert.Equal(8, first.Attempts);
        Assert.Equal(Ceiling, first.Length);
        Assert.Equal(TimeSpan.Zero, first.PlayDenied);
        Assert.All(journey.Attempts.Take(8), attempt => Assert.Equal(5, attempt.LivesLeft.Value));
    }

    [Fact]
    public void An_attempt_under_way_at_the_ceiling_is_finished()
    {
        // 170 seconds an attempt: the eighth starts ten seconds before the ceiling and is played to its end.
        HandMadeLevels levels = new HandMadeLevels().EasyLevels(100, moveLimit: 20, movesLeft: 3);

        JourneyList journey = Journey(OnePlayer(), levels);

        SessionEnded first = journey.Sessions.First();
        AttemptPlayed last = journey.Attempts.ElementAt(7);
        Assert.Equal(8, first.Attempts);
        Assert.Equal(TimeSpan.FromSeconds(8 * 170), first.Length);
        Assert.True(last.StartedAt - first.StartedAt < Ceiling);
        Assert.True(last.DecidedAt - first.StartedAt > Ceiling);
        Assert.True(last.IsWon);
    }

    [Fact]
    public void A_session_cut_short_by_lives_records_the_play_it_was_denied()
    {
        HandMadeLevels lost = new HandMadeLevels().Level(6, Always(Unfinished()));
        HandMadeLevels won = new HandMadeLevels().EasyLevels(100);

        SessionEnded cutShort = Journey(OnePlayer(), lost).Sessions.First();
        SessionEnded full = Journey(OnePlayer(), won).Sessions.First();

        Assert.Equal(Ceiling - TimeSpan.FromMinutes(5), cutShort.PlayDenied);
        Assert.Equal(TimeSpan.Zero, full.PlayDenied);
    }

    [Fact]
    public void A_session_that_starts_without_a_life_is_denied_all_of_its_play()
    {
        EconomySettings settings = OnePlayer("""{ "lives": { "minutesPerLife": 600 } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished()));

        SessionEnded second = Journey(settings, levels).Sessions.ElementAt(1);

        Assert.Equal(SessionEnd.NoLifeAtStart, second.End);
        Assert.Equal(Ceiling, second.PlayDenied);
    }

    [Fact]
    public void An_attempt_takes_its_moves_an_ad_if_one_is_watched_and_the_pause_before_the_next()
    {
        // Ten seconds a move, twenty between attempts, thirty for an ad; one free ad a day and no coins for a second.
        EconomySettings settings = OnePlayer(
            """{ "habit": { "secondsBetweenAttempts": 20, "secondsPerAd": 30 } }""",
            """{ "extraMovesOffer": { "freeAdsPerDay": 1 }, "player": { "extraMoves": { "watchesAds": true } } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished(nearlyDoneOn: 3)));

        JourneyList journey = Journey(settings, levels);

        // The first attempt: six moves, the ad, the offer's five moves, then the pause. The others: six moves and the pause.
        AttemptPlayed[] attempts = journey.Attempts.Take(5).ToArray();
        Assert.IsType<ExtraMovesDecision.TakenForAd>(attempts[0].Reading.ExtraMoves);
        Assert.Equal(TimeSpan.FromSeconds(110), attempts[0].PlayTime);
        Assert.Equal(TimeSpan.FromSeconds(140), attempts[0].DecidedAt - attempts[0].StartedAt);
        Assert.Equal(TimeSpan.FromSeconds(160), attempts[0].Duration);
        Assert.All(attempts.Skip(1), attempt => Assert.Equal(TimeSpan.FromSeconds(60), attempt.PlayTime));
        Assert.All(attempts.Skip(1), attempt => Assert.Equal(TimeSpan.FromSeconds(80), attempt.Duration));
        Assert.Equal(
            [0, 160, 240, 320, 400],
            attempts.Select(attempt => (int)(attempt.StartedAt - attempts[0].StartedAt).TotalSeconds));

        // The session is as long as its attempts, pauses and ad together; the lives it lost were lost when each attempt was decided.
        SessionEnded first = journey.Sessions.First();
        Assert.Equal(5, first.Attempts);
        Assert.Equal(TimeSpan.FromSeconds(480), first.Length);
        Assert.Equal(TimeSpan.FromSeconds(20 * 60) - TimeSpan.FromSeconds(480), first.PlayDenied);
    }

    [Fact]
    public void A_session_whose_last_life_goes_as_the_ceiling_is_reached_was_ended_by_the_ceiling()
    {
        // One life, a one-minute ceiling and a one-minute attempt that is lost: both reasons hold, and the session had its full length.
        EconomySettings settings = OnePlayer("""{ "lives": { "max": 1 }, "habit": { "sessionCeilingMinutes": 1 } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished()));

        SessionEnded first = Journey(settings, levels).Sessions.First();

        Assert.Equal(1, first.Attempts);
        Assert.Equal(SessionEnd.CeilingReached, first.End);
        Assert.Equal(TimeSpan.Zero, first.PlayDenied);
        Assert.Null(first.Lives.UntilNextLife);
    }

    [Fact]
    public void A_daily_reward_given_every_third_day_comes_on_days_one_four_and_seven()
    {
        EconomySettings settings = OnePlayer("""{ "rewards": { "daily": { "everyDays": 3, "coins": 25, "boostersEachTime": 1 } } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished()));

        JourneyList journey = Journey(settings, levels);

        RewardGiven[] daily = journey.Rewards.Where(reward => reward.Source == CoinSource.Daily).Take(3).ToArray();
        Assert.Equal([1, 4, 7], daily.Select(reward => reward.At.Day.Value));
        Assert.Equal([BoosterKind.StartRocketAndBomb, BoosterKind.StartColorBomb, BoosterKind.StartExtraMoves], daily.SelectMany(reward => reward.Boosters));
        Assert.Equal(new Coins(75), daily[2].After.Balance);
        Assert.Equal(new Coins(25), journey.Days.First(day => day.Day.Value == 3).Standing.Balance);
    }

    [Fact]
    public void The_daily_reward_and_the_free_ads_come_with_the_first_session_of_a_day()
    {
        EconomySettings settings = OnePlayer(
            """{ "rewards": { "daily": { "boostersEachTime": 1 } } }""",
            """{ "extraMovesOffer": { "freeAdsPerDay": 1 }, "player": { "extraMoves": { "watchesAds": true } } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished(nearlyDoneOn: 3)));

        JourneyList journey = Journey(settings, levels);

        RewardGiven[] daily = journey.Rewards.Where(reward => reward.Source == CoinSource.Daily).Take(2).ToArray();
        Assert.Equal([PlayerTime.At(new DayNumber(1), new TimeOnly(8, 30)), PlayerTime.At(new DayNumber(2), new TimeOnly(8, 30))], daily.Select(reward => reward.At));
        Assert.Equal([BoosterKind.StartRocketAndBomb], daily[0].Boosters);
        Assert.Equal([BoosterKind.StartColorBomb], daily[1].Boosters);

        foreach (int day in new[] { 1, 2 })
        {
            AttemptPlayed[] ofDay = journey.Attempts.Where(attempt => attempt.StartedAt.Day.Value == day).ToArray();
            Assert.IsType<ExtraMovesDecision.TakenForAd>(ofDay[0].Reading.ExtraMoves);
            Assert.All(ofDay.Skip(1), attempt => Assert.IsType<ExtraMovesDecision.WantedUnpaid>(attempt.Reading.ExtraMoves));
        }
    }
}
