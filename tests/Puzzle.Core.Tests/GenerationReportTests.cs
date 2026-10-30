using System.IO;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Generator.Calibration;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

public class GenerationReportTests
{
    /// <summary>
    /// The profile before onboarding, which wrote the old report rows below: the average bot calibrates every level and
    /// no level has a move margin.
    /// </summary>
    private static GenerationProfile Profile => RepoFiles.ProfileBeforeOnboarding;

    private static LevelPlanner Planner => new(Profile);

    /// <summary>The first rows of levels/generated/report.csv as written before the winPlus5 column existed.</summary>
    private const string ReportWithoutPlus5 =
        "level,tier,size,playable,colors,style,goals,moves,moveRange,targetWin,win,p50,p90,nearMiss,casualWin,expertWin,poolSize,poolMinWin,poolMaxWin,candidates,relaxed,seconds\n"
        + "1,Easy,10x10,100,4,Full+Collect1,Red:107,22,16-24,0.950,0.965,10,20,0.020,0.967,0.950,16,0.938,1.000,3,no,4.3\n"
        + "2,Easy,10x10,100,4,Full+Collect2,Yellow:100 Green:100,18,16-24,0.950,1.000,8,14,0.000,0.983,0.967,16,1.000,1.000,5,no,4.5\n";

    /// <summary>A report row with a casual floor as written before the floorBot column existed.</summary>
    private const string ReportWithoutFloorBot =
        "level,tier,size,playable,colors,style,goals,moves,moveRange,targetWin,win,winPlus5,floor,floorWin,floorSetLimit,p50,p90,nearMiss,casualWin,expertWin,poolSize,poolMinWin,poolMaxWin,candidates,relaxed,seconds\n"
        + "1,Easy,10x10,100,4,Full+Collect1,Red:107,22,16-24,0.950,0.965,1.000,0.800,0.815,true,10,20,0.020,0.967,0.950,16,0.938,1.000,3,no,4.3\n";

    /// <summary>
    /// Rows of levels 3, 4 and 52 as written before the calibrationBot, calibratedMoves and moveMargin columns existed.
    /// In the default profile the random bot calibrates level 3, the novice bot level 4 and ladder rung 55 level 52.
    /// </summary>
    private const string ReportWithoutCalibrationBot =
        "level,tier,size,playable,colors,style,goals,moves,moveRange,targetWin,win,winPlus5,floor,floorWin,floorSetLimit,floorBot,p50,p90,nearMiss,casualWin,expertWin,poolSize,poolMinWin,poolMaxWin,candidates,relaxed,seconds\n"
        + "3,Easy,10x10,100,4,Full+Collect1,Red:50,17,14-20,0.900,0.875,0.980,,,false,,13,18,0.100,1.000,1.000,15,0.813,0.938,5,no,0.3\n"
        + "4,Easy,10x10,100,4,Full+Collect1,Blue:80,16,16-24,0.950,0.970,0.995,,,false,,9,14,0.010,1.000,1.000,16,0.969,1.000,5,no,3.5\n"
        + "52,Normal,10x11,86,6,Diamond+Items,Items:4,27,20-33,0.750,0.775,0.870,0.500,0.525,false,casual,20,37,0.000,0.433,0.983,13,0.719,0.844,1,no,1.9\n";

    /// <summary>The profile's floor bot, which --repair hands to <see cref="GenerationReport.Read"/>.</summary>
    private const string CurrentFloorBot = "casual";

    [Fact]
    public void Repair_reads_a_report_written_before_the_plus5_column()
    {
        using var report = TemporaryFile.Holding(ReportWithoutPlus5);

        IReadOnlyDictionary<int, ReportRow> rows = GenerationReport.Read(report.Path, Profile);

        Assert.Equal(Row(new WinRateCheck(0.95, 0.965)), rows[1]);
        Assert.Equal(Row(new WinRateCheck(0.95, 1.0)), rows[2]);
    }

    [Fact]
    public void A_new_row_puts_the_plus5_win_rate_next_to_win_and_old_rows_move_under_the_new_header()
    {
        using var report = TemporaryFile.Holding(ReportWithoutPlus5);

        GenerationReport.Upsert(report.Path, [Metrics(number: 2, winRate: 0.9, winRateWithExtraMoves: 0.98)]);

        string[][] table = File.ReadAllLines(report.Path).Select(line => line.Split(',')).ToArray();
        string[] header = table[0];
        string Cell(string level, string column) => table.Single(row => row[0] == level)[Array.IndexOf(header, column)];
        Assert.Equal("winPlus5", header[Array.IndexOf(header, "win") + 1]);
        Assert.All(table, row => Assert.Equal(header.Length, row.Length));
        Assert.Equal(("0.965", string.Empty, "0.020", "0.967", "4.3"), (Cell("1", "win"), Cell("1", "winPlus5"), Cell("1", "nearMiss"), Cell("1", "casualWin"), Cell("1", "seconds")));
        Assert.Equal(("0.900", "0.980"), (Cell("2", "win"), Cell("2", "winPlus5")));
        Assert.Equal(new[] { 1, 2 }, GenerationReport.Read(report.Path, Profile).Keys.Order());
    }

    [Fact]
    public void Floor_columns_follow_winPlus5_and_stay_empty_where_there_is_no_floor()
    {
        using var report = TemporaryFile.Holding(ReportWithoutPlus5);

        GenerationReport.Upsert(
            report.Path,
            [
                Metrics(number: 2, winRate: 0.97, winRateWithExtraMoves: 1.0) with { Floor = new FloorCheck("casual", new WinRateCheck(0.8, 0.815)), FloorSetLimit = true },
                Metrics(number: 3, winRate: 0.6, winRateWithExtraMoves: 0.8) with { Tier = DifficultyTier.Hard, TargetWinRate = 0.6 },
            ]);

        string[][] table = File.ReadAllLines(report.Path).Select(line => line.Split(',')).ToArray();
        string[] header = table[0];
        string Cell(string level, string column) => table.Single(row => row[0] == level)[Array.IndexOf(header, column)];
        Assert.Equal(new[] { "winPlus5", "floor", "floorWin", "floorSetLimit", "floorBot" }, header.Skip(Array.IndexOf(header, "winPlus5")).Take(5));
        Assert.All(table, row => Assert.Equal(header.Length, row.Length));
        Assert.Equal((string.Empty, string.Empty, string.Empty, string.Empty), (Cell("1", "floor"), Cell("1", "floorWin"), Cell("1", "floorSetLimit"), Cell("1", "floorBot")));
        Assert.Equal(("0.800", "0.815", "true", "casual"), (Cell("2", "floor"), Cell("2", "floorWin"), Cell("2", "floorSetLimit"), Cell("2", "floorBot")));
        Assert.Equal((string.Empty, string.Empty, "false", string.Empty), (Cell("3", "floor"), Cell("3", "floorWin"), Cell("3", "floorSetLimit"), Cell("3", "floorBot")));
    }

    [Fact]
    public void Repair_reads_the_floor_bot_back_and_takes_a_floor_recorded_without_one_as_the_current_floor_bot()
    {
        using var report = TemporaryFile.Holding(ReportWithoutFloorBot);
        FloorCheck? FloorOf(int level) => GenerationReport.Read(report.Path, Profile with { FloorBot = "novice" })[level].Floor;

        FloorCheck beforeTheColumn = Assert.IsType<FloorCheck>(FloorOf(1));
        GenerationReport.Upsert(
            report.Path,
            [Metrics(number: 2, winRate: 0.97, winRateWithExtraMoves: 1.0) with { Floor = new FloorCheck("casual", new WinRateCheck(0.8, 0.82)) }]);

        Assert.Equal(new FloorCheck("novice", new WinRateCheck(0.8, 0.815)), beforeTheColumn);
        Assert.Equal("novice", Assert.IsType<FloorCheck>(FloorOf(1)).Bot);   // moved under the new header with an empty floorBot cell
        Assert.Equal("casual", Assert.IsType<FloorCheck>(FloorOf(2)).Bot);   // recorded
    }

    [Fact]
    public void Repair_reads_the_floor_back_and_drops_a_row_whose_floor_win_is_missing()
    {
        using var report = TemporaryFile.Holding(ReportWithoutPlus5);
        GenerationReport.Upsert(
            report.Path,
            [Metrics(number: 2, winRate: 0.97, winRateWithExtraMoves: 1.0) with { Floor = new FloorCheck("casual", new WinRateCheck(0.8, 0.815)), FloorSetLimit = true }]);
        string[] lines = File.ReadAllLines(report.Path);
        string[] header = lines[0].Split(',');
        string[] broken = lines[2].Split(',');
        broken[0] = "3";
        broken[Array.IndexOf(header, "floorWin")] = string.Empty;
        File.WriteAllLines(report.Path, lines.Append(string.Join(',', broken)));

        IReadOnlyDictionary<int, ReportRow> rows = GenerationReport.Read(report.Path, Profile);

        Assert.Null(rows[1].Floor);
        Assert.Equal(Row(new WinRateCheck(0.95, 0.97), new FloorCheck("casual", new WinRateCheck(0.8, 0.815))), rows[2]);
        Assert.False(rows.ContainsKey(3)); // left out, so --repair regenerates it
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_plan_gained_a_casual_floor_since_its_row_was_written()
    {
        LevelPlan plan = Planner.Plan(52);   // Normal from level 51: target 0.75, floor 0.50
        ReportRow rowWithoutFloor = Row(new WinRateCheck(0.75, 0.78));

        Assert.False(rowWithoutFloor.StillHolds(plan, Profile));
        Assert.True(rowWithoutFloor.StillHolds(plan with { CasualFloor = null }, Profile));
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_plan_lost_its_casual_floor()
    {
        LevelPlan plan = Planner.Plan(52);
        ReportRow rowWithFloor = Row(new WinRateCheck(0.75, 0.78), new FloorCheck(CurrentFloorBot, new WinRateCheck(0.50, 0.52)));

        Assert.False(rowWithFloor.StillHolds(plan with { CasualFloor = null }, Profile));
        Assert.True(rowWithFloor.StillHolds(plan, Profile));
    }

    [Fact]
    public void A_recorded_floor_that_matches_the_plan_to_three_decimals_is_judged_on_its_floor_win()
    {
        LevelPlan plan = Planner.Plan(52) with { CasualFloor = 0.5004 };   // the report records 0.500
        ReportRow heldFloor = Row(new WinRateCheck(0.75, 0.90), new FloorCheck(CurrentFloorBot, new WinRateCheck(0.500, 0.48)));
        var missedFloor = heldFloor with { Floor = new FloorCheck(CurrentFloorBot, new WinRateCheck(0.500, 0.40)) };

        Assert.True(heldFloor.StillHolds(plan, Profile));
        Assert.False(missedFloor.StillHolds(plan, Profile));                            // 10 points under the floor
        Assert.False(heldFloor.StillHolds(plan with { CasualFloor = 0.51 }, Profile));  // the profile moved the floor
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_floor_another_floor_bot_measured()
    {
        LevelPlan plan = Planner.Plan(52);
        ReportRow byCasual = Row(new WinRateCheck(0.75, 0.78), new FloorCheck("casual", new WinRateCheck(0.50, 0.52)));
        var byNovice = byCasual with { Floor = new FloorCheck("novice", new WinRateCheck(0.50, 0.52)) };
        var byCasualCapitalized = byCasual with { Floor = new FloorCheck("Casual", new WinRateCheck(0.50, 0.52)) };

        Assert.True(byCasual.StillHolds(plan, Profile));
        Assert.True(byCasualCapitalized.StillHolds(plan, Profile));   // bot names ignore case, as in the profile
        Assert.False(byNovice.StillHolds(plan, Profile));
        Assert.True(byNovice.StillHolds(plan, Profile with { FloorBot = "novice" }));
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_target_win_rate_changed_since_its_row_was_written()
    {
        LevelPlan plan = Planner.Plan(55);   // Hard from level 51: target 0.50, no floor
        ReportRow row = Row(new WinRateCheck(0.50, 0.52));

        Assert.True(row.StillHolds(plan, Profile));
        Assert.True(row.StillHolds(plan with { TargetWinRate = 0.5004 }, Profile));   // the report records 0.500
        Assert.False(row.StillHolds(plan with { TargetWinRate = 0.55 }, Profile));    // 0.52 would stand, but against the old target
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_seed_pool_is_under_the_minimum()
    {
        ReportRow row = Row(new WinRateCheck(0.50, 0.52)) with { PoolSize = Profile.MinPoolSize };

        Assert.True(row.StillHolds(Planner.Plan(55), Profile));
        Assert.False((row with { PoolSize = Profile.MinPoolSize - 1 }).StillHolds(Planner.Plan(55), Profile));
    }

    [Fact]
    public void A_new_row_records_its_calibration_bot_its_calibrated_limit_and_its_move_margin()
    {
        using var report = TemporaryFile.Holding(ReportWithoutCalibrationBot);

        GenerationReport.Upsert(
            report.Path,
            [Metrics(number: 5, winRate: 0.99, winRateWithExtraMoves: 1.0) with { CalibrationBot = "novice", MoveLimit = 25, CalibratedMoves = 20, MoveMargin = TestProfiles.Margin(0.25m) }]);

        string[][] table = File.ReadAllLines(report.Path).Select(line => line.Split(',')).ToArray();
        string[] header = table[0];
        string Cell(string level, string column) => table.Single(row => row[0] == level)[Array.IndexOf(header, column)];
        Assert.Equal(new[] { "moves", "calibratedMoves", "moveMargin", "moveRange", "calibrationBot", "targetWin" }, header.Skip(Array.IndexOf(header, "moves")).Take(6));
        Assert.All(table, row => Assert.Equal(header.Length, row.Length));
        Assert.Equal(("25", "20", "0.25", "16-24", "novice"), (Cell("5", "moves"), Cell("5", "calibratedMoves"), Cell("5", "moveMargin"), Cell("5", "moveRange"), Cell("5", "calibrationBot")));
        Assert.Equal(("16", string.Empty, string.Empty, "16-24", string.Empty), (Cell("4", "moves"), Cell("4", "calibratedMoves"), Cell("4", "moveMargin"), Cell("4", "moveRange"), Cell("4", "calibrationBot")));
    }

    [Fact]
    public void A_level_without_a_move_margin_records_margin_0_and_its_limit_as_the_calibrated_one()
    {
        using var report = TemporaryFile.Holding(ReportWithoutCalibrationBot);

        GenerationReport.Upsert(report.Path, [Metrics(number: 5, winRate: 0.96, winRateWithExtraMoves: 1.0)]);

        string[][] table = File.ReadAllLines(report.Path).Select(line => line.Split(',')).ToArray();
        string Cell(string column) => table.Single(row => row[0] == "5")[Array.IndexOf(table[0], column)];
        Assert.Equal(("20", "20", "0"), (Cell("moves"), Cell("calibratedMoves"), Cell("moveMargin")));
    }

    [Fact]
    public void Repair_reads_back_the_calibration_bot_and_the_move_margin_a_row_records()
    {
        using var report = TemporaryFile.Holding(ReportWithoutCalibrationBot);
        GenerationReport.Upsert(
            report.Path,
            [Metrics(number: 5, winRate: 0.99, winRateWithExtraMoves: 1.0) with { CalibrationBot = "novice", MoveLimit = 25, CalibratedMoves = 20, MoveMargin = TestProfiles.Margin(0.25m) }]);

        IReadOnlyDictionary<int, ReportRow> rows = GenerationReport.Read(report.Path, Profile);

        Assert.Equal(new ReportRow("novice", new WinRateCheck(0.95, 0.99), Floor: null, TestProfiles.Margin(0.25m), PoolSize: 16), rows[5]);
    }

    [Fact]
    public void A_row_written_before_the_calibrationBot_column_is_taken_as_calibrated_by_the_bands_bot()
    {
        using var report = TemporaryFile.Holding(ReportWithoutCalibrationBot);

        IReadOnlyDictionary<int, ReportRow> againstTheDefault = GenerationReport.Read(report.Path, RepoFiles.DefaultProfile);
        IReadOnlyDictionary<int, ReportRow> againstOneBot = GenerationReport.Read(report.Path, Profile);

        Assert.Equal(("random", "novice", "skill-55"), (againstTheDefault[3].CalibrationBot, againstTheDefault[4].CalibrationBot, againstTheDefault[52].CalibrationBot));
        Assert.All(againstOneBot.Values, row => Assert.Equal("average", row.CalibrationBot));
    }

    [Fact]
    public void A_row_written_before_the_moveMargin_column_counts_as_a_level_without_a_margin()
    {
        using var report = TemporaryFile.Holding(ReportWithoutCalibrationBot);

        ReportRow row = GenerationReport.Read(report.Path, Profile)[52];

        Assert.Equal(MoveMargin.None, row.MoveMargin);
        Assert.True(row.StillHolds(Planner.Plan(52), Profile));
        Assert.False(StillHoldsUnder(Profile.WithMoveMargin(0.25m), row, level: 52));
    }

    [Theory]
    [InlineData("a quarter")]
    [InlineData("25%")]
    [InlineData("1.5")]
    [InlineData("-0.25")]
    public void A_row_whose_move_margin_is_not_a_share_is_left_out(string written)
    {
        using var report = TemporaryFile.Holding(ReportWithoutCalibrationBot);
        GenerationReport.Upsert(report.Path, [Metrics(number: 5, winRate: 0.96, winRateWithExtraMoves: 1.0)]);
        string[] lines = File.ReadAllLines(report.Path);
        string[] broken = lines.Single(line => line.StartsWith("5,", StringComparison.Ordinal)).Split(',');
        broken[Array.IndexOf(lines[0].Split(','), "moveMargin")] = written;
        File.WriteAllLines(report.Path, lines.Select(line => line.StartsWith("5,", StringComparison.Ordinal) ? string.Join(',', broken) : line));

        IReadOnlyDictionary<int, ReportRow> rows = GenerationReport.Read(report.Path, Profile);

        Assert.Equal(new[] { 3, 4, 52 }, rows.Keys.Order());   // level 5 is left out, so --repair regenerates it
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public void A_row_whose_level_is_below_1_is_left_out(string level)
    {
        // No band covers such a level, so a row written before the calibrationBot column could not even be given a bot.
        string strayRow = level + ",Easy,10x10,100,4,Full+Collect1,Red:50,17,14-20,0.900,0.875,0.980,,,false,,13,18,0.100,1.000,1.000,15,0.813,0.938,5,no,0.3\n";
        using var report = TemporaryFile.Holding(ReportWithoutCalibrationBot + strayRow);

        IReadOnlyDictionary<int, ReportRow> rows = GenerationReport.Read(report.Path, RepoFiles.DefaultProfile);

        Assert.Equal(new[] { 3, 4, 52 }, rows.Keys.Order());
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_calibration_bot_changed()
    {
        LevelPlan plan = Planner.Plan(55);   // Hard from level 51: the average bot, target 0.50, no floor
        ReportRow byAverage = Row(new WinRateCheck(0.50, 0.52));
        ReportRow byNovice = byAverage with { CalibrationBot = "novice" };

        Assert.True(byAverage.StillHolds(plan, Profile));
        Assert.True((byAverage with { CalibrationBot = "Average" }).StillHolds(plan, Profile));   // bot names ignore case, as in the profile
        Assert.False(byNovice.StillHolds(plan, Profile));
        Assert.True(byNovice.StillHolds(plan with { CalibrationBot = "novice" }, Profile));
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_move_margin_is_not_its_plans()
    {
        GenerationProfile softer = Profile.WithMoveMargin(0.25m);
        ReportRow calibrated = Row(new WinRateCheck(0.50, 0.52));
        ReportRow softened = calibrated with { MoveMargin = TestProfiles.Margin(0.25m) };

        Assert.True(StillHoldsUnder(Profile, calibrated, level: 55));
        Assert.False(StillHoldsUnder(softer, calibrated, level: 55));
        Assert.True(StillHoldsUnder(softer, softened, level: 55));
        Assert.True(StillHoldsUnder(Profile.WithMoveMargin(0.250m), softened, level: 55));   // the same margin, written with another zero
        Assert.False(StillHoldsUnder(Profile.WithMoveMargin(0.1m), softened, level: 55));
        Assert.False(StillHoldsUnder(Profile, softened, level: 55));
    }

    [Fact]
    public void A_softened_level_is_kept_however_far_its_win_rates_are_above_their_shares_but_not_when_one_falls_short()
    {
        GenerationProfile softer = Profile.WithMoveMargin(0.25m);
        LevelPlan plan = new LevelPlanner(softer).Plan(52);   // Normal from level 51: target 0.75, floor 0.50, a quarter more moves
        ReportRow softened = Row(new WinRateCheck(0.75, 0.97), new FloorCheck(CurrentFloorBot, new WinRateCheck(0.50, 0.80))) with { MoveMargin = TestProfiles.Margin(0.25m) };
        ReportRow sameWinRatesWithoutMargin = softened with { MoveMargin = MoveMargin.None };

        Assert.True(softened.StillHolds(plan, softer));                       // the overshoot is the margin at work
        Assert.False(StillHoldsUnder(Profile, sameWinRatesWithoutMargin, level: 52));    // at a calibrated limit it is a limit set too high
        Assert.False((softened with { Target = new WinRateCheck(0.75, 0.60) }).StillHolds(plan, softer));
        Assert.False((softened with { Floor = new FloorCheck(CurrentFloorBot, new WinRateCheck(0.50, 0.40)) }).StillHolds(plan, softer));
        Assert.True((softened with { Target = new WinRateCheck(0.75, 0.70) }).StillHolds(plan, softer));   // short, but within the tolerance
    }

    /// <summary>Whether repair keeps <paramref name="row"/> when <paramref name="profile"/> plans its level: the margin a row must carry is its plan's.</summary>
    private static bool StillHoldsUnder(GenerationProfile profile, ReportRow row, int level) =>
        row.StillHolds(new LevelPlanner(profile).Plan(level), profile);

    /// <summary>A row the profile before onboarding writes: calibrated by the average bot, no move margin.</summary>
    private static ReportRow Row(WinRateCheck target, FloorCheck? floor = null) => new("average", target, floor, MoveMargin.None, PoolSize: 16);

    private static LevelMetrics Metrics(int number, double winRate, double winRateWithExtraMoves) => new()
    {
        Number = number,
        Tier = DifficultyTier.Easy,
        Size = "10x10",
        Playable = 100,
        Colors = 4,
        Style = "Full+Collect1",
        Goals = "Red:100",
        MoveLimit = 20,
        CalibratedMoves = 20,
        MoveMargin = MoveMargin.None,
        MoveRange = new MoveRange(16, 24),
        CalibrationBot = "average",
        TargetWinRate = 0.95,
        WinRate = winRate,
        WinRateWithExtraMoves = winRateWithExtraMoves,
        Floor = null,
        FloorSetLimit = false,
        MovesP50 = 10,
        MovesP90 = 18,
        NearMiss = 0.01,
        BotWinRates = new Dictionary<string, double> { ["casual"] = 0.9, ["expert"] = 0.95 },
        PoolSize = 16,
        PoolMinWinRate = 0.9,
        PoolMaxWinRate = 1.0,
        CandidatesTried = 1,
        Relaxed = false,
        Seconds = 1.0,
    };

    /// <summary>A file under the temp directory that is removed when disposed.</summary>
    private sealed class TemporaryFile : IDisposable
    {
        private TemporaryFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryFile Holding(string contents)
        {
            var file = new TemporaryFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"puzzle-report-{Guid.NewGuid():N}.csv"));
            File.WriteAllText(file.Path, contents);
            return file;
        }

        public void Dispose() => File.Delete(Path);
    }
}
