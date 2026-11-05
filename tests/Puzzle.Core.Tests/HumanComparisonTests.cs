using System.IO;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

public class HumanComparisonTests
{
    [Fact]
    public void Comparison_counts_unassisted_policy_attempts_of_the_levels_as_they_are()
    {
        LevelDefinition level = GameTests.OpenLevel();
        LevelDefinition sameIdOtherContent = level.WithMoveLimit(25);
        var tablet = new DeviceId("tablet-0002");
        using var folder = new TempFolder();
        var log = new AttemptLog(Path.Combine(folder.Path, "attempts.jsonl"));
        AttemptRecord[] records =
        {
            Of(level, AttemptResult.Won, 25, lab: Lab(level)),
            Of(level, AttemptResult.Won, 18, lab: Lab(level)),
            Of(level, AttemptResult.Won, 21, lab: Lab(level), device: tablet),
            Of(level, AttemptResult.OutOfMoves, 30, lab: Lab(level), device: tablet),
            Of(level, AttemptResult.Abandoned, 6, lab: Lab(level), device: tablet),
            Of(level, AttemptResult.OutOfMoves, 30, lab: null), // the released game's records match by id alone
            Of(level, AttemptResult.Won, 10, lab: Lab(level, hints: 1)),
            Of(level, AttemptResult.Won, 12, lab: Lab(level, botMoves: 3)),
            Of(level, AttemptResult.Won, 12, attempt: null, lab: Lab(level)),
            Of(sameIdOtherContent, AttemptResult.Won, 12, lab: Lab(sameIdOtherContent)),
        };
        foreach (AttemptRecord record in records)
        {
            log.Append(record);
        }

        HumanBotReport report = HumanBotComparison.Compare(log.ReadAll().Records, new[] { level }, botRuns: 2);

        Assert.Equal(new RecordTally(Compared: 6, Assisted: 2, ChosenSeeds: 1, UnknownLevel: 1, TesterMode: 0), report.Records);
        LevelComparison row = Assert.Single(report.Levels);
        Assert.Equal(level.Id, row.Level);
        Assert.Equal(30, row.MoveLimit);
        Assert.Equal(21, row.MedianWinMoves);
        ComparisonFigures humans = row.Figures;
        Assert.Equal((5, 1, 3, 2), (humans.Finished, humans.Abandoned, humans.Wins, humans.Devices));
        Assert.Equal(0.6, humans.HumanWinRate);
        Assert.Equal(0.5, humans.HumanWinRateLowerBound);
        Assert.Equal(SkillProfile.All.Select(profile => profile.Name), humans.Bots.Select(bot => bot.Bot));
    }

    [Fact]
    public void Bots_play_each_attempts_own_game_at_its_own_move_limit()
    {
        // Every attempt of this level starts from one fixed board that random bot seeds would never draw.
        LevelDefinition level = GameTests.OpenLevel().WithAttempts(new AttemptPolicy { Board = new StartBoard.Fixed(77), SeedPool = new uint[] { 11, 12, 13 } });
        AttemptRecord[] atTwelve =
        {
            Of(level, AttemptResult.Won, 10, attempt: 0, lab: Lab(level), seeds: level.Attempts.SeedsFor(0, 0), moveLimit: 12),
            Of(level, AttemptResult.OutOfMoves, 12, attempt: 1, lab: Lab(level), seeds: level.Attempts.SeedsFor(1, 0), moveLimit: 12),
            Of(level, AttemptResult.Abandoned, 5, attempt: 2, lab: Lab(level), seeds: level.Attempts.SeedsFor(2, 0), moveLimit: 12),
        };
        AttemptRecord atTwenty = Of(level, AttemptResult.Won, 15, attempt: 0, lab: null, seeds: level.Attempts.SeedsFor(0, 0), moveLimit: 20);
        const int runs = 8;

        HumanBotReport report = HumanBotComparison.Compare(atTwelve.Append(atTwenty), new[] { level }, runs);

        double[] expectedAtTwelve = IndependentBotRates(level, atTwelve, runs);
        Assert.Contains(expectedAtTwelve, rate => rate is > 0 and < 1); // the limit matters: the level's own 30 moves win nearly always
        Assert.Equal(new[] { 12, 20 }, report.Levels.Select(row => row.MoveLimit));
        AssertRates(expectedAtTwelve, OnAll(report.Levels[0].Figures));
        AssertRates(IndependentBotRates(level, atTwelve[..2], runs), OnFinished(report.Levels[0].Figures));
        AssertRates(IndependentBotRates(level, new[] { atTwenty }, runs), OnAll(report.Levels[1].Figures));
        AssertRates(IndependentBotRates(level, atTwelve.Append(atTwenty).ToArray(), runs), OnAll(report.Overall));
        AssertRates(IndependentBotRates(level, new[] { atTwelve[0], atTwelve[1], atTwenty }, runs), OnFinished(report.Overall));
    }

    [Fact]
    public void Each_human_rate_is_set_beside_the_bots_on_the_games_it_covers()
    {
        // People walked away from the hard boards (seeds 3, 6, 18) and finished the easy ones (1, 5, 13).
        LevelDefinition level = GameTests.OpenLevel().WithAttempts(new AttemptPolicy { Board = new StartBoard.Fixed(77), SeedPool = new uint[] { 11, 12, 13 } });
        AttemptRecord[] finished =
        {
            Of(level, AttemptResult.Won, 7, attempt: 0, lab: Lab(level), seeds: AttemptSeeds.WithFixedBoard(77, 1), moveLimit: 8),
            Of(level, AttemptResult.Won, 8, attempt: 1, lab: Lab(level), seeds: AttemptSeeds.WithFixedBoard(77, 5), moveLimit: 8),
            Of(level, AttemptResult.OutOfMoves, 8, attempt: 2, lab: Lab(level), seeds: AttemptSeeds.WithFixedBoard(77, 13), moveLimit: 8),
        };
        AttemptRecord[] abandoned = new uint[] { 3, 6, 18 }
            .Select((seed, i) => Of(level, AttemptResult.Abandoned, 3, attempt: 3 + i, lab: Lab(level), seeds: AttemptSeeds.WithFixedBoard(77, seed), moveLimit: 8))
            .ToArray();
        AttemptRecord[] all = finished.Concat(abandoned).ToArray();
        const int runs = 8;

        ComparisonFigures humans = Assert.Single(HumanBotComparison.Compare(all, new[] { level }, runs).Levels).Figures;

        double[] onFinished = IndependentBotRates(level, finished, runs);
        double[] onAll = IndependentBotRates(level, all, runs);
        AssertRates(onFinished, OnFinished(humans));
        AssertRates(onAll, OnAll(humans));
        Assert.Equal(NearestBot(humans.HumanWinRate, onFinished), humans.ClosestBot);
        Assert.Equal(NearestBot(humans.HumanWinRateLowerBound, onAll), humans.ClosestBotAtLowerBound);
        Assert.NotEqual(NearestBot(humans.HumanWinRate, onAll), humans.ClosestBot); // all games would name another bot
    }

    [Fact]
    public void Closest_bots_are_matched_on_the_rates_of_the_games_each_human_rate_covers()
    {
        BotWinRate[] bots =
        {
            new("novice", WinRateOnFinished: 0.5, WinRateOnAll: 0.2),
            new("casual", WinRateOnFinished: 0.7, WinRateOnAll: 0.5),
            new("expert", WinRateOnFinished: 0.9, WinRateOnAll: 0.7),
        };

        var humans = new ComparisonFigures(Finished: 4, Abandoned: 4, Wins: 3, Devices: 1, bots); // 0.75 and 0.375

        Assert.Equal(("casual", "casual", false), (humans.ClosestBot, humans.ClosestBotAtLowerBound, humans.AbandonsDecideClosest));
    }

    [Fact]
    public void An_attempt_abandoned_before_its_limit_is_undecided_and_only_lowers_the_lower_bound()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord[] records =
        {
            Of(level, AttemptResult.Won, 20),
            Of(level, AttemptResult.OutOfMoves, 30),
            Of(level, AttemptResult.Won, 33), // won only with bought moves: a loss at the limit
            Of(level, AttemptResult.Abandoned, 30), // left after the limit was used up: a loss too
            Of(level, AttemptResult.Abandoned, 5),
            Of(level, AttemptResult.Abandoned, 29),
        };

        ComparisonFigures humans = Assert.Single(HumanBotComparison.Compare(records, new[] { level }, botRuns: 1).Levels).Figures;

        Assert.Equal((4, 2, 1), (humans.Finished, humans.Abandoned, humans.Wins));
        Assert.Equal(0.25, humans.HumanWinRate);
        Assert.Equal(1 / 6.0, humans.HumanWinRateLowerBound);
    }

    [Fact]
    public void Closest_bot_is_named_for_both_human_rates_and_flagged_when_the_abandons_decide()
    {
        BotWinRate[] bots = { SameOnEveryGame("novice", 0.2), SameOnEveryGame("casual", 0.5), SameOnEveryGame("expert", 0.7) };

        var split = new ComparisonFigures(Finished: 3, Abandoned: 2, Wins: 2, Devices: 1, bots); // 0.667 and 0.4
        var agreed = new ComparisonFigures(Finished: 4, Abandoned: 1, Wins: 2, Devices: 1, bots); // 0.5 and 0.4

        Assert.Equal(("expert", "casual", true), (split.ClosestBot, split.ClosestBotAtLowerBound, split.AbandonsDecideClosest));
        Assert.Equal(("casual", "casual", false), (agreed.ClosestBot, agreed.ClosestBotAtLowerBound, agreed.AbandonsDecideClosest));
    }

    [Fact]
    public void Without_finished_attempts_only_the_lower_bound_names_a_bot()
    {
        LevelDefinition level = GameTests.OpenLevel();

        HumanBotReport report = HumanBotComparison.Compare(new[] { Of(level, AttemptResult.Abandoned, 4) }, new[] { level }, botRuns: 2);

        ComparisonFigures humans = Assert.Single(report.Levels).Figures;
        Assert.Null(humans.HumanWinRate);
        Assert.Null(humans.ClosestBot);
        Assert.Equal(0, humans.HumanWinRateLowerBound);
        Assert.NotNull(humans.ClosestBotAtLowerBound);
        Assert.True(humans.AbandonsDecideClosest);
        Assert.Equal(SkillProfile.All.Count, report.Overall.Bots.Count);
    }

    /// <summary>
    /// Each profile's share of wins over the records, worked out apart from the comparison: uncapped games on each
    /// record's seeds, won when every goal was met within the record's limit.
    /// </summary>
    private static double[] IndependentBotRates(LevelDefinition level, IReadOnlyList<AttemptRecord> records, int runs)
    {
        return SkillProfile.All.Select(profile => records.Average(record => WinShare(profile, record))).ToArray();

        double WinShare(SkillProfile profile, AttemptRecord record) => Enumerable.Range(0, runs)
            .Count(run => SimulationRunner.Play(level, new GreedyPolicy(profile), record.Start.Seeds, Rng.Derive(HumanBotComparison.BotSeed, (ulong)run), 150)
                .CompletedAtMove <= record.Start.MoveLimit) / (double)runs;
    }

    private static BotWinRate SameOnEveryGame(string bot, double rate) => new(bot, WinRateOnFinished: rate, WinRateOnAll: rate);

    /// <summary>The profile whose rate is nearest <paramref name="humans"/>, the weaker on a tie.</summary>
    private static string NearestBot(double? humans, double[] rates)
    {
        double target = humans ?? throw new ArgumentNullException(nameof(humans));
        int nearest = Enumerable.Range(0, rates.Length).MinBy(profile => Math.Abs(rates[profile] - target));
        return SkillProfile.All[nearest].Name;
    }

    private static double[] OnAll(ComparisonFigures figures) => figures.Bots.Select(bot => bot.WinRateOnAll).ToArray();

    private static double[] OnFinished(ComparisonFigures figures) =>
        figures.Bots.Select(bot => bot.WinRateOnFinished ?? throw new InvalidOperationException($"{bot.Bot} has no rate on finished games.")).ToArray();

    private static void AssertRates(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        Assert.All(expected.Zip(actual), pair => Assert.Equal(pair.First, pair.Second, precision: 12));
    }
}
