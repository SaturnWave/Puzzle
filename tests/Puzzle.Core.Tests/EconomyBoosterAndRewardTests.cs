using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>Boosters, rewards and coins in the simulator. The levels here have 20 moves; the tests' player gets a booster only where a test's settings give one.</summary>
public class EconomyBoosterAndRewardTests
{
    private const int Limit = 20;

    private const string HitCellEveryDay = """{ "rewards": { "daily": { "boostersEachTime": 1, "boosterRotation": ["hit-cell"] } } }""";

    /// <summary>A daily reward of one booster, always <paramref name="booster"/>, which the player uses after one loss on a level.</summary>
    private static EconomySettings GivenDaily(string booster, params string[] changes) => OnePlayer(
    [
        $$"""{ "rewards": { "daily": { "boostersEachTime": 1, "boosterRotation": ["{{booster}}"] } } }""",
        $$"""{ "player": { "startBoosters": { "useAfterLossesOnLevel": 1, "preference": ["{{booster}}"] } } }""",
        .. changes,
    ]);

    [Fact]
    public void A_start_booster_is_spent_won_or_lost()
    {
        EconomySettings settings = GivenDaily("start-extra-moves");
        HandMadeLevels lostWithIt = new HandMadeLevels().Level(Limit, Always(Unfinished()), Always(Unfinished()), Always(Won(10)));
        HandMadeLevels wonWithIt = new HandMadeLevels().Level(Limit, Always(Unfinished()), Always(Won(Limit + 2)));

        AttemptPlayed[] lost = Journey(settings, lostWithIt).Attempts.ToArray();
        AttemptPlayed[] won = Journey(settings, wonWithIt).Attempts.ToArray();

        Assert.IsType<StartBoosterDecision.None>(lost[0].StartBooster);
        Assert.Equal(1, lost[0].After.Held.Count(BoosterKind.StartExtraMoves));
        Assert.Equal(new StartBoosterDecision.UseOwned(BoosterKind.StartExtraMoves), lost[1].StartBooster);
        Assert.False(lost[1].IsWon);
        Assert.Equal(0, lost[1].After.Held.Count(BoosterKind.StartExtraMoves));
        Assert.IsType<StartBoosterDecision.None>(lost[2].StartBooster);

        Assert.Equal(new StartBoosterDecision.UseOwned(BoosterKind.StartExtraMoves), won[1].StartBooster);
        Assert.True(won[1].IsWon);
        Assert.Equal(0, won[1].After.Held.Count(BoosterKind.StartExtraMoves));
    }

    [Fact]
    public void The_plus_three_booster_reads_the_same_game_three_moves_further()
    {
        HandMadeLevels levels = new HandMadeLevels().Level(Limit, Always(Unfinished()), Always(Won(Limit + 2)), Always(Won(10)));

        AttemptPlayed withIt = Journey(GivenDaily("start-extra-moves"), levels).Attempts.ElementAt(1);
        AttemptPlayed without = Journey(OnePlayer(), levels).Attempts.ElementAt(1);

        Assert.Equal(new AttemptEnding.Won(new MoveCount(1)), withIt.Reading.Ending);
        Assert.Equal(new MoveCount(Limit + 2), withIt.Reading.MovesPlayed);
        Assert.IsType<AttemptEnding.LostOutOfMoves>(without.Reading.Ending);
        Assert.Equal(new MoveCount(Limit), without.Reading.MovesPlayed);
        Assert.Equal(withIt.Met, without.Met);
    }

    [Fact]
    public void A_board_booster_reads_the_game_measured_with_that_start()
    {
        EconomySettings settings = GivenDaily("start-color-bomb");
        HandMadeLevels levels = new HandMadeLevels()
            .Level(Limit, Always(Unfinished()), Always(Unfinished()), Always(Won(10)))
            .Starting(BoardStart.ColorBomb, Always(Unfinished()), Always(Won(12)), Always(Unfinished()));

        AttemptPlayed[] attempts = Journey(settings, levels).Attempts.ToArray();

        Assert.Equal(2, attempts.Length);
        Assert.Equal(new StartBoosterDecision.UseOwned(BoosterKind.StartColorBomb), attempts[1].StartBooster);
        Assert.Equal(new AttemptEnding.Won(new MoveCount(Limit - 12)), attempts[1].Reading.Ending);
    }

    [Fact]
    public void A_booster_is_bought_only_after_the_set_losses_and_only_when_the_coins_cover_it()
    {
        EconomySettings settings = OnePlayer(
            """{ "player": { "startBoosters": { "useAfterLossesOnLevel": 2, "buyAfterLossesOnLevel": 2, "preference": ["start-extra-moves"] } } }""",
            """{ "coins": { "perLevelWon": 600 } }""");
        HandMadeLevels levels = new HandMadeLevels().EasyLevels(1, Limit, movesLeft: 5).Level(Limit, Always(Unfinished()));

        AttemptPlayed[] onTheHardLevel = Journey(settings, levels).Attempts.Where(attempt => attempt.Level.Value == 2).Take(4).ToArray();

        Assert.IsType<StartBoosterDecision.None>(onTheHardLevel[0].StartBooster);
        Assert.IsType<StartBoosterDecision.None>(onTheHardLevel[1].StartBooster);
        Assert.Equal(new StartBoosterDecision.Buy(BoosterKind.StartExtraMoves, new Coins(500)), onTheHardLevel[2].StartBooster);
        Assert.Equal(new Coins(650 - 500), onTheHardLevel[2].After.Balance);
        Assert.Equal(new MoveCount(Limit + 3), onTheHardLevel[2].Reading.MovesPlayed);
        Assert.Equal(new StartBoosterDecision.WantedUnpaid(new Shortfall(Price: new Coins(500), Missing: new Coins(350))), onTheHardLevel[3].StartBooster);
        Assert.Equal(new Coins(150), onTheHardLevel[3].After.Balance);
    }

    [Fact]
    public void An_owned_booster_is_used_before_one_is_bought_and_the_dearer_is_bought_only_when_covered()
    {
        var prices = new Dictionary<BoosterKind, Coins>
        {
            [BoosterKind.StartColorBomb] = new(1000),
            [BoosterKind.StartRocketAndBomb] = new(600),
            [BoosterKind.StartExtraMoves] = new(500),
        };
        var habit = new StartBoosterHabit
        {
            UseAfterLossesOnLevel = 2,
            BuyAfterLossesOnLevel = 4,
            Preference = [BoosterKind.StartColorBomb, BoosterKind.StartRocketAndBomb, BoosterKind.StartExtraMoves],
        };
        var rule = new StartBoosterRule(habit, prices);
        BoosterShelf holdsThePlusThree = BoosterShelf.Empty.WithOneMore(BoosterKind.StartExtraMoves);

        Assert.IsType<StartBoosterDecision.None>(rule.Decide(new BeforeAttemptView(1, holdsThePlusThree, new Coins(5000))));
        Assert.Equal(new StartBoosterDecision.UseOwned(BoosterKind.StartExtraMoves), rule.Decide(new BeforeAttemptView(4, holdsThePlusThree, new Coins(5000))));
        Assert.IsType<StartBoosterDecision.None>(rule.Decide(new BeforeAttemptView(3, BoosterShelf.Empty, new Coins(5000))));
        Assert.Equal(new StartBoosterDecision.Buy(BoosterKind.StartColorBomb, new Coins(1000)), rule.Decide(new BeforeAttemptView(4, BoosterShelf.Empty, new Coins(5000))));
        Assert.Equal(new StartBoosterDecision.Buy(BoosterKind.StartRocketAndBomb, new Coins(600)), rule.Decide(new BeforeAttemptView(4, BoosterShelf.Empty, new Coins(700))));
        Assert.Equal(
            new StartBoosterDecision.WantedUnpaid(new Shortfall(Price: new Coins(500), Missing: new Coins(100))),
            rule.Decide(new BeforeAttemptView(4, BoosterShelf.Empty, new Coins(400))));
    }

    [Fact]
    public void Every_move_left_pays_the_finale()
    {
        EconomySettings settings = OnePlayer("""{ "coins": { "perMoveLeftInFinale": 12, "perLevelWon": 25 } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(Limit, Always(Won(13)));

        AttemptPlayed win = Journey(settings, levels).Attempts.Single();

        Assert.Equal(new Coins(7 * 12), win.FinaleCoins);
        Assert.Equal(new Coins(25), win.LevelCoins);
        Assert.Equal(new Coins((7 * 12) + 25), win.After.Balance);
    }

    [Fact]
    public void A_completed_painting_pays_once_and_in_the_settings_rotation()
    {
        EconomySettings settings = OnePlayer(
            """{ "rewards": { "paintingCompleted": { "coins": 40, "boostersEachTime": 2, "boosterRotation": ["shuffle", "hit-cell", "hit-row"] } } }""");
        HandMadeLevels levels = new HandMadeLevels().EasyLevels(20);

        JourneyList journey = Journey(settings, levels);

        RewardGiven[] paintings = journey.Rewards.Where(reward => reward.Source == CoinSource.PaintingCompleted).ToArray();
        Assert.Equal(2, paintings.Length);
        Assert.Equal([BoosterKind.Shuffle, BoosterKind.HitCell], paintings[0].Boosters);
        Assert.Equal([BoosterKind.HitRow, BoosterKind.Shuffle], paintings[1].Boosters);
        Assert.All(paintings, reward => Assert.Equal(new Coins(40), reward.Coins));
        Assert.Equal([10, 20], paintings.Select(reward => reward.After.LevelsWon));
        Assert.Equal(2, paintings[1].After.Held.Count(BoosterKind.Shuffle));
        Assert.Equal(new Coins((20 * 50) + 80), journey.Days.Last().Standing.Balance);
    }

    [Fact]
    public void A_milestone_on_a_paintings_last_level_counts_that_paintings_reward()
    {
        // Level 10 completes the first painting and is a milestone: the reward comes with the win, so it belongs to the milestone.
        EconomySettings settings = OnePlayer(
            """{ "rewards": { "paintingCompleted": { "coins": 40, "boostersEachTime": 1, "boosterRotation": ["shuffle"] } } }""",
            """{ "report": { "milestones": [5, 10], "paceTarget": { "level": 10 } } }""");
        HandMadeLevels levels = new HandMadeLevels().EasyLevels(12);

        PlayerOutcome player = PopulationRun.PlayPass(settings, levels.For(settings), MeasuredHalf.All, dayEndsKept: []).Players.Single();

        Checkpoint atLevelFive = player.AtMilestones[0]!;
        Checkpoint atLevelTen = player.AtMilestones[1]!;
        Assert.Equal(new Coins(5 * 50), atLevelFive.Standing.Balance);
        Assert.Equal(0, atLevelFive.Totals.Received(BoosterKind.Shuffle));
        Assert.Equal(new Coins((10 * 50) + 40), atLevelTen.Standing.Balance);
        Assert.Equal(new Coins(40), atLevelTen.Totals.Earned(CoinSource.PaintingCompleted));
        Assert.Equal(1, atLevelTen.Totals.Received(BoosterKind.Shuffle));
        Assert.Equal(1, atLevelTen.Standing.Held.Count(BoosterKind.Shuffle));
        Assert.Equal(10, atLevelTen.Totals.Wins);
    }

    [Fact]
    public void In_level_boosters_do_nothing_at_worth_zero()
    {
        EconomySettings settings = OnePlayer(HitCellEveryDay);
        HandMadeLevels levels = new HandMadeLevels().Level(Limit, Always(Won(Limit + 2, nearlyDoneOn: 10)));

        AttemptPlayed[] firstSession = Journey(settings, levels).Attempts.Take(5).ToArray();

        Assert.False(settings.Player.InLevelBoosters.AssumesAnEffect);
        Assert.All(firstSession, attempt =>
        {
            Assert.Null(attempt.Reading.InLevelBoosterUsed);
            Assert.False(attempt.IsWon);
            Assert.Equal(1, attempt.After.Held.Count(BoosterKind.HitCell));
        });
    }

    [Fact]
    public void An_in_level_booster_with_an_assumed_worth_reads_the_game_that_many_moves_further()
    {
        EconomySettings settings = OnePlayer(HitCellEveryDay, """{ "player": { "inLevelBoosters": { "assumedMovesWorth": { "hit-cell": 3 } } } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(Limit, Always(Won(Limit + 2, nearlyDoneOn: 10)));

        AttemptPlayed attempt = Journey(settings, levels).Attempts.Single();

        Assert.True(settings.Player.InLevelBoosters.AssumesAnEffect);
        Assert.Equal(BoosterKind.HitCell, attempt.Reading.InLevelBoosterUsed);
        Assert.True(attempt.IsWon);
        Assert.Equal(0, attempt.After.Held.Count(BoosterKind.HitCell));
    }
}
