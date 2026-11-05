using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Economy.Settings;
using Puzzle.Tooling.Hints;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

/// <summary>
/// The attempts the released game showed hints in, compared apart from the others (the owner's decision of
/// 2026-10-19): beside the same bot profiles, each shown the game's hints on the same games and following every one,
/// as the measurement's hinted player does. Help of any other kind still leaves an attempt out.
/// </summary>
public class HintedComparisonTests
{
    private static readonly GameHints EveryMoveACasualHint = new("casual", perAttempt: 3, new HintMoment.WhenIdle(100));

    [Fact]
    public void Hinted_attempts_stand_beside_bots_that_follow_the_games_hints_and_the_others_beside_bots_shown_none()
    {
        // Every attempt of this level starts from one fixed board; the hinted attempts play the same games as the others.
        LevelDefinition level = GameTests.OpenLevel().WithAttempts(new AttemptPolicy { Board = new StartBoard.Fixed(77), SeedPool = new uint[] { 11, 12, 13 } });
        AttemptRecord[] plain =
        {
            Of(level, AttemptResult.Won, 7, attempt: 0, seeds: AttemptSeeds.WithFixedBoard(77, 1), moveLimit: 8),
            Of(level, AttemptResult.OutOfMoves, 8, attempt: 1, seeds: AttemptSeeds.WithFixedBoard(77, 3), moveLimit: 8),
        };
        AttemptRecord[] hinted =
        {
            ShownHints(Of(level, AttemptResult.Won, 8, attempt: 2, seeds: AttemptSeeds.WithFixedBoard(77, 1), moveLimit: 8), 2),
            ShownHints(Of(level, AttemptResult.Abandoned, 3, attempt: 3, seeds: AttemptSeeds.WithFixedBoard(77, 3), moveLimit: 8), 1),
        };
        const int runs = 8;

        HumanBotReport report = HumanBotComparison.Compare(plain.Concat(hinted), new[] { level }, runs, EveryMoveACasualHint);

        double[] shownNone = IndependentRates(level, plain, runs, profile => new GreedyPolicy(profile));
        double[] following = IndependentRates(level, hinted, runs, profile => EveryMoveACasualHint.Follow(new GreedyPolicy(profile)));
        AssertRates(shownNone, OnAll(Assert.Single(report.Levels).Figures));
        AssertRates(following, OnAll(Assert.Single(report.Hinted.Levels).Figures));
        AssertRates(following, OnAll(report.Hinted.Overall));
        Assert.NotEqual(shownNone, following); // the same games: only the hints differ
        Assert.Equal(EveryMoveACasualHint, report.Hinted.Hints);
        Assert.Equal(SkillProfile.All.Select(profile => profile.Name), report.Hinted.Overall.Bots.Select(bot => bot.Bot));
        Assert.Equal((2, 0, 1), (report.Overall.Finished, report.Overall.Abandoned, report.Overall.Wins));
        Assert.Equal((1, 1, 1), (report.Hinted.Overall.Finished, report.Hinted.Overall.Abandoned, report.Hinted.Overall.Wins));
        Assert.Equal(new RecordTally(Compared: 4, Assisted: 0, ChosenSeeds: 0, UnknownLevel: 0, TesterMode: 0, Hinted: 2), report.Records);
    }

    [Fact]
    public void A_hinted_attempt_with_a_move_shown_by_hand_a_booster_or_the_labs_help_is_still_help_and_left_out()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord played = Of(level, AttemptResult.Won, 20);
        var withABooster = new AttemptOutcome(
            played.Outcome.Result, played.Outcome.MovesUsed, played.Outcome.Goals, extraMovesBought: false, boostersUsed: new[] { "hit-cell" }, played.Outcome.EndedUtc);
        AttemptRecord[] helped =
        {
            new(played.Start, played.Outcome, lab: null, actions: null, hintsShown: 2, guidedMoves: 1),
            new(played.Start, withABooster, lab: null, actions: null, hintsShown: 1),
            new(played.Start, played.Outcome, Lab(level, botMoves: 3), actions: null, hintsShown: 1),
        };
        AttemptRecord onChosenSeeds = ShownHints(Of(level, AttemptResult.Won, 20, attempt: null), 1);

        HumanBotReport report = HumanBotComparison.Compare(helped.Append(onChosenSeeds).Append(played), new[] { level }, botRuns: 1, EveryMoveACasualHint);

        Assert.All(helped, record => Assert.Equal(AttemptStanding.Assisted, AttemptCounting.Standing(record)));
        Assert.Equal(AttemptStanding.ChosenSeeds, AttemptCounting.Standing(onChosenSeeds));
        Assert.Equal(new RecordTally(Compared: 1, Assisted: 3, ChosenSeeds: 1, UnknownLevel: 0, TesterMode: 0, Hinted: 0), report.Records);
        Assert.Empty(report.Hinted.Levels);
    }

    [Fact]
    public void Asked_without_the_games_hints_the_comparison_sums_the_hinted_attempts_up_apart_beside_no_bot()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord played = Of(level, AttemptResult.Won, 20);
        AttemptRecord[] hinted = { ShownHints(played, 1), ShownHints(Of(level, AttemptResult.OutOfMoves, 30), 3) };

        HumanBotReport report = HumanBotComparison.Compare(hinted.Append(played), new[] { level }, botRuns: 1);

        Assert.Null(report.Hinted.Hints);
        Assert.Empty(report.Hinted.Overall.Bots);
        ComparisonFigures humans = Assert.Single(report.Hinted.Levels).Figures;
        Assert.Equal((2, 1), (humans.Finished, humans.Wins));
        Assert.Empty(humans.Bots);
        Assert.Equal(SkillProfile.All.Count, Assert.Single(report.Levels).Figures.Bots.Count);
        Assert.Equal((3, 2), (report.Records.Compared, report.Records.Hinted));
    }

    [Fact]
    public void The_summary_of_the_records_counts_the_hinted_attempts_apart_as_the_comparison_does()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord played = Of(level, AttemptResult.Won, 20);
        AttemptRecord lost = Of(level, AttemptResult.OutOfMoves, 30);
        AttemptRecord guided = new(played.Start, played.Outcome, lab: null, actions: null, hintsShown: 1, guidedMoves: 1);

        AttemptSummary summary = AttemptSummary.Of(new[] { played, ShownHints(played, 1), ShownHints(lost, 2), ShownHints(lost, 3), guided });

        Assert.Equal((1, 1), (summary.Overall.Finished, summary.Overall.Wins));
        Assert.Equal((3, 1), (summary.Hinted.Finished, summary.Hinted.Wins));
        Assert.Equal(1, summary.Assisted);
        LevelAttempts onTheLevel = Assert.Single(summary.Levels);
        Assert.Equal((1, 3, 1), (onTheLevel.Play.Finished, onTheLevel.Hinted.Finished, onTheLevel.Assisted));
        Assert.Equal(20, onTheLevel.MedianWinMoves); // of the attempts shown no hint
    }

    [Theory]
    [InlineData(8.0, 5.3, 22)] // the owner's wait against the recordings' time a move
    [InlineData(5.0, 5.3, 39)] // the first wait
    [InlineData(0.0, 5.3, 100)]
    [InlineData(60.0, 5.3, 1)] // never less than one move in a hundred
    public void A_hinted_bot_is_shown_a_hint_before_the_share_of_moves_thought_over_for_longer_than_the_wait(double waitSeconds, double perMoveSeconds, int chancePercent)
    {
        HintMoment.WhenIdle idle = HintMoment.WhenIdle.ForWait(TimeSpan.FromSeconds(waitSeconds), TimeSpan.FromSeconds(perMoveSeconds));

        Assert.Equal(chancePercent, idle.ChancePercent);
    }

    [Fact]
    public void The_owners_hints_are_decided_and_meet_a_hinted_bot_before_about_a_fifth_of_its_moves()
    {
        EconomySettings shipped = SettingsLoader.Load(RepoFiles.EconomySettingsPath("default.json"));

        Assert.Equal(new HintSettings { Idle = TimeSpan.FromSeconds(8), PerAttempt = 3, Bot = "casual" }, shipped.Game.Hints);
        Assert.All(
            new[] { "hints.idleSeconds", "hints.perAttempt", "hints.bot" },
            path => Assert.Equal(Provenance.Decided, SettingsCatalogue.Describe(path).Provenance));
        Assert.Equal(22, HintMoment.WhenIdle.ForWait(shipped.Game.Hints.Idle, shipped.Habit.PerMove).ChancePercent);
    }

    [Fact]
    public void The_games_hints_need_a_player_model_to_show_and_at_least_one_hint_an_attempt()
    {
        Assert.Throws<ArgumentException>(() => new GameHints("usta", perAttempt: 3, new HintMoment.WhenIdle(22)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameHints("casual", perAttempt: 0, new HintMoment.WhenIdle(22)));
    }

    private static AttemptRecord ShownHints(AttemptRecord record, int hints) =>
        new(record.Start, record.Outcome, record.Lab, record.Actions, record.TesterMode, hintsShown: hints, record.GuidedMoves);

    /// <summary>
    /// Each profile's share of wins over the records, worked out apart from the comparison: uncapped games on each
    /// record's seeds, won when every goal was met within the record's limit.
    /// </summary>
    private static double[] IndependentRates(LevelDefinition level, IReadOnlyList<AttemptRecord> records, int runs, Func<SkillProfile, IBotPolicy> playerOf)
    {
        return SkillProfile.All.Select(profile => records.Average(record => WinShare(profile, record))).ToArray();

        double WinShare(SkillProfile profile, AttemptRecord record) => Enumerable.Range(0, runs)
            .Count(run => SimulationRunner.Play(level, playerOf(profile), record.Start.Seeds, Rng.Derive(HumanBotComparison.BotSeed, (ulong)run), 150)
                .CompletedAtMove <= record.Start.MoveLimit) / (double)runs;
    }

    private static double[] OnAll(ComparisonFigures figures) => figures.Bots.Select(bot => bot.WinRateOnAll).ToArray();

    private static void AssertRates(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        Assert.All(expected.Zip(actual), pair => Assert.Equal(pair.First, pair.Second, precision: 12));
    }
}
