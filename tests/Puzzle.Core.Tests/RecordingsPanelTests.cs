using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Recording;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

/// <summary>The lab's recordings panel: the summary of a scope's attempts, and the bounds of its bot comparison.</summary>
public class RecordingsPanelTests
{
    [Fact]
    public void Summary_figures_cover_the_comparable_attempts_and_count_the_others_by_why_they_are_left_out()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord[] records =
        {
            Of(level, AttemptResult.Won, 20, lab: Lab(level)),
            Of(level, AttemptResult.Won, 24, lab: Lab(level)),
            Of(level, AttemptResult.OutOfMoves, 30, lab: Lab(level)),
            Of(level, AttemptResult.Abandoned, 6, lab: Lab(level)),
            Of(level, AttemptResult.Won, 10, lab: Lab(level, hints: 1)),
            Of(level, AttemptResult.Won, 11, lab: Lab(level, botMoves: 2)),
            Of(level, AttemptResult.Won, 12, lab: Lab(level, assist: 20)),
            Of(level, AttemptResult.Won, 12, attempt: null, lab: Lab(level)),
        };

        AttemptSummary summary = AttemptSummary.Of(records);

        LevelAttempts row = Assert.Single(summary.Levels);
        Assert.Equal((level.Id, "generated/level_0001", 30), (row.Level, row.LevelKey, row.MoveLimit));
        Assert.Equal((3, 1, 2), (row.Play.Finished, row.Play.Abandoned, row.Play.Wins));
        Assert.Equal(2 / 3.0, row.Play.HumanWinRate);
        Assert.Equal(22, row.MedianWinMoves);
        Assert.Equal((3, 1), (row.Assisted, row.ChosenSeeds));
        Assert.Empty(row.Play.Bots);
        Assert.Equal((3, 1, 2), (summary.Overall.Finished, summary.Overall.Abandoned, summary.Overall.Wins));
        Assert.Equal((3, 1), (summary.Assisted, summary.ChosenSeeds));
    }

    [Fact]
    public void Summary_keeps_level_versions_and_move_limits_apart_and_lists_the_latest_played_first()
    {
        LevelDefinition level = GameTests.OpenLevel();
        LevelDefinition sameIdOtherContent = level.WithAttempts(new AttemptPolicy { Board = new StartBoard.Fixed(77), SeedPool = new uint[] { 11, 12 } });
        AttemptRecord[] records =
        {
            Of(level, AttemptResult.Won, 20, lab: Lab(level), endedUtc: Noon.AddMinutes(10)),
            Of(sameIdOtherContent, AttemptResult.Won, 20, lab: Lab(sameIdOtherContent, key: "samples/s01_basic"), endedUtc: Noon.AddMinutes(30)),
            Of(level, AttemptResult.Won, 20, lab: Lab(level), moveLimit: 25, endedUtc: Noon.AddMinutes(20)),
        };

        AttemptSummary summary = AttemptSummary.Of(records);

        Assert.Equal(new[] { Noon.AddMinutes(30), Noon.AddMinutes(20), Noon.AddMinutes(10) }, summary.Levels.Select(row => row.LastPlayedUtc));
        Assert.Equal(new[] { "samples/s01_basic", "generated/level_0001", "generated/level_0001" }, summary.Levels.Select(row => row.LevelKey));
        Assert.Equal(new[] { 30, 25, 30 }, summary.Levels.Select(row => row.MoveLimit));
    }

    [Fact]
    public void Summary_counts_people_exactly_as_the_bot_comparison_does()
    {
        LevelDefinition level = GameTests.OpenLevel();
        var tablet = new DeviceId("tablet-0002");
        AttemptRecord[] records =
        {
            Of(level, AttemptResult.Won, 25, lab: Lab(level)),
            Of(level, AttemptResult.Won, 18, lab: Lab(level), device: tablet),
            Of(level, AttemptResult.Won, 33, lab: Lab(level)), // won only with bought moves: a loss at the limit
            Of(level, AttemptResult.Abandoned, 30, lab: Lab(level)), // left after the limit was used up: a loss too
            Of(level, AttemptResult.Abandoned, 4, lab: Lab(level), device: tablet),
            Of(level, AttemptResult.OutOfMoves, 25, lab: Lab(level), moveLimit: 25),
            Of(level, AttemptResult.Won, 9, lab: Lab(level, hints: 2)),
        };

        AttemptSummary summary = AttemptSummary.Of(records);
        HumanBotReport report = HumanBotComparison.Compare(records, new[] { level }, botRuns: 1);

        Assert.Equal(report.Levels.Count, summary.Levels.Count);
        foreach (LevelComparison compared in report.Levels)
        {
            LevelAttempts summed = Assert.Single(summary.Levels, row => row.MoveLimit == compared.MoveLimit);
            AssertSamePeople(compared.Figures, summed.Play);
            Assert.Equal(compared.MedianWinMoves, summed.MedianWinMoves);
        }

        AssertSamePeople(report.Overall, summary.Overall);
        Assert.Equal(report.Records.Assisted, summary.Assisted);
    }

    [Fact]
    public void Comparison_rows_name_the_level_version_they_compare()
    {
        LevelDefinition generated = GameTests.OpenLevel();
        LevelDefinition sample = generated.WithAttempts(new AttemptPolicy { Board = new StartBoard.Fixed(77), SeedPool = new uint[] { 11, 12 } });
        AttemptRecord[] records =
        {
            Of(generated, AttemptResult.Won, 20, lab: Lab(generated)),
            Of(sample, AttemptResult.OutOfMoves, 30, lab: Lab(sample, key: "samples/s01_basic")),
        };

        HumanBotReport report = HumanBotComparison.Compare(records, new[] { generated, sample }, botRuns: 1);

        Assert.Equal(2, report.Levels.Count);
        Assert.All(report.Levels, row => Assert.Equal((generated.Id, 30), (row.Level, row.MoveLimit)));
        Assert.Equal(
            new[] { LevelJson.Fingerprint(generated), LevelJson.Fingerprint(sample) }.Order(),
            report.Levels.Select(row => row.LevelFingerprint).Order());
    }

    [Fact]
    public void A_bounded_comparison_takes_the_latest_comparable_attempts_and_every_record_left_out_anyway()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord[] comparable = Enumerable.Range(1, 5)
            .Select(minute => Of(level, AttemptResult.Won, 20, lab: Lab(level), endedUtc: Noon.AddMinutes(minute)))
            .ToArray();
        AttemptRecord assisted = Of(level, AttemptResult.Won, 20, lab: Lab(level, hints: 1), endedUtc: Noon.AddMinutes(9));
        AttemptRecord chosen = Of(level, AttemptResult.Won, 20, attempt: null, lab: Lab(level), endedUtc: Noon.AddMinutes(9));

        IReadOnlyList<AttemptRecord> taken = LabRecords.ForComparison(comparable.Append(assisted).Append(chosen).ToArray(), maxComparable: 3);

        Assert.Equal(5, taken.Count);
        Assert.Contains(assisted, taken);
        Assert.Contains(chosen, taken);
        Assert.Equal(comparable[2..], taken.Where(record => record != assisted && record != chosen).OrderBy(record => record.Outcome.EndedUtc));
    }

    [Fact]
    public void Device_scope_keeps_the_asking_devices_records_and_all_scope_keeps_every_record()
    {
        LevelDefinition level = GameTests.OpenLevel();
        var tablet = new DeviceId("tablet-0002");
        AttemptRecord mine = Of(level, AttemptResult.Won, 20, lab: Lab(level));
        AttemptRecord theirs = Of(level, AttemptResult.Won, 20, lab: Lab(level), device: tablet);

        Assert.Equal(new[] { mine }, LabRecords.InScope(new[] { mine, theirs }, RecordScope.Device, new DeviceId(Phone.Value)));
        Assert.Equal(new[] { mine, theirs }, LabRecords.InScope(new[] { mine, theirs }, RecordScope.All, Phone));
    }

    [Fact]
    public void Only_one_comparison_runs_at_a_time()
    {
        using var gate = new ComparisonGate();

        Assert.True(gate.TryEnter());
        Assert.False(gate.TryEnter());
        gate.Exit();
        Assert.True(gate.TryEnter());
    }

    private static void AssertSamePeople(ComparisonFigures expected, ComparisonFigures actual)
    {
        Assert.Equal(
            (expected.Finished, expected.Abandoned, expected.Wins, expected.Devices),
            (actual.Finished, actual.Abandoned, actual.Wins, actual.Devices));
        Assert.Equal(expected.HumanWinRate, actual.HumanWinRate);
        Assert.Equal(expected.HumanWinRateLowerBound, actual.HumanWinRateLowerBound);
    }
}
