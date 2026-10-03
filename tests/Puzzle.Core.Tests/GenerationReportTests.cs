using System.IO;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Generator.Calibration;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

public class GenerationReportTests
{
    private static GenerationProfile Profile => RepoFiles.DefaultProfile;

    private static LevelPlanner Planner => new(Profile);

    /// <summary>The first rows of levels/generated/report.csv as written before the winPlus5 column existed.</summary>
    private const string ReportWithoutPlus5 =
        "level,tier,size,playable,colors,style,goals,moves,moveRange,targetWin,win,p50,p90,nearMiss,casualWin,expertWin,poolSize,poolMinWin,poolMaxWin,candidates,relaxed,seconds\n"
        + "1,Easy,10x10,100,4,Full+Collect1,Red:107,22,16-24,0.950,0.965,10,20,0.020,0.967,0.950,16,0.938,1.000,3,no,4.3\n"
        + "2,Easy,10x10,100,4,Full+Collect2,Yellow:100 Green:100,18,16-24,0.950,1.000,8,14,0.000,0.983,0.967,16,1.000,1.000,5,no,4.5\n";

    [Fact]
    public void Repair_reads_a_report_written_before_the_plus5_column()
    {
        using var report = TemporaryFile.Holding(ReportWithoutPlus5);

        IReadOnlyDictionary<int, ReportRow> rows = GenerationReport.Read(report.Path);

        Assert.Equal(new ReportRow(new WinRateCheck(0.95, 0.965), Floor: null, 16), rows[1]);
        Assert.Equal(new ReportRow(new WinRateCheck(0.95, 1.0), Floor: null, 16), rows[2]);
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
        Assert.Equal(new[] { 1, 2 }, GenerationReport.Read(report.Path).Keys.Order());
    }

    [Fact]
    public void Floor_columns_follow_winPlus5_and_stay_empty_where_there_is_no_floor()
    {
        using var report = TemporaryFile.Holding(ReportWithoutPlus5);

        GenerationReport.Upsert(
            report.Path,
            [
                Metrics(number: 2, winRate: 0.97, winRateWithExtraMoves: 1.0) with { Floor = new WinRateCheck(0.8, 0.815), FloorSetLimit = true },
                Metrics(number: 3, winRate: 0.6, winRateWithExtraMoves: 0.8) with { Tier = DifficultyTier.Hard, TargetWinRate = 0.6 },
            ]);

        string[][] table = File.ReadAllLines(report.Path).Select(line => line.Split(',')).ToArray();
        string[] header = table[0];
        string Cell(string level, string column) => table.Single(row => row[0] == level)[Array.IndexOf(header, column)];
        Assert.Equal(new[] { "winPlus5", "floor", "floorWin", "floorSetLimit" }, header.Skip(Array.IndexOf(header, "winPlus5")).Take(4));
        Assert.All(table, row => Assert.Equal(header.Length, row.Length));
        Assert.Equal((string.Empty, string.Empty, string.Empty), (Cell("1", "floor"), Cell("1", "floorWin"), Cell("1", "floorSetLimit")));
        Assert.Equal(("0.800", "0.815", "true"), (Cell("2", "floor"), Cell("2", "floorWin"), Cell("2", "floorSetLimit")));
        Assert.Equal((string.Empty, string.Empty, "false"), (Cell("3", "floor"), Cell("3", "floorWin"), Cell("3", "floorSetLimit")));
    }

    [Fact]
    public void Repair_reads_the_floor_back_and_drops_a_row_whose_floor_win_is_missing()
    {
        using var report = TemporaryFile.Holding(ReportWithoutPlus5);
        GenerationReport.Upsert(
            report.Path,
            [Metrics(number: 2, winRate: 0.97, winRateWithExtraMoves: 1.0) with { Floor = new WinRateCheck(0.8, 0.815), FloorSetLimit = true }]);
        string[] lines = File.ReadAllLines(report.Path);
        string[] header = lines[0].Split(',');
        string[] broken = lines[2].Split(',');
        broken[0] = "3";
        broken[Array.IndexOf(header, "floorWin")] = string.Empty;
        File.WriteAllLines(report.Path, lines.Append(string.Join(',', broken)));

        IReadOnlyDictionary<int, ReportRow> rows = GenerationReport.Read(report.Path);

        Assert.Null(rows[1].Floor);
        Assert.Equal(new ReportRow(new WinRateCheck(0.95, 0.97), new WinRateCheck(0.8, 0.815), 16), rows[2]);
        Assert.False(rows.ContainsKey(3)); // left out, so --repair regenerates it
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_plan_gained_a_casual_floor_since_its_row_was_written()
    {
        var rowWithoutFloor = new ReportRow(new WinRateCheck(0.75, 0.78), Floor: null, PoolSize: 16);

        Assert.False(rowWithoutFloor.StillHolds(Planner.Plan(52), Profile)); // Normal from level 51: floor 0.50
        Assert.True(rowWithoutFloor.StillHolds(Planner.Plan(55), Profile));  // Hard: no floor
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_plan_lost_its_casual_floor()
    {
        var rowWithFloor = new ReportRow(new WinRateCheck(0.75, 0.78), new WinRateCheck(0.50, 0.52), PoolSize: 16);

        Assert.False(rowWithFloor.StillHolds(Planner.Plan(55), Profile));
        Assert.True(rowWithFloor.StillHolds(Planner.Plan(52), Profile));
    }

    [Fact]
    public void A_recorded_floor_that_matches_the_plan_to_three_decimals_is_judged_on_its_floor_win()
    {
        LevelPlan plan = Planner.Plan(52) with { CasualFloor = 0.5004 };   // the report records 0.500
        var heldFloor = new ReportRow(new WinRateCheck(0.75, 0.90), new WinRateCheck(0.500, 0.48), PoolSize: 16);
        var missedFloor = heldFloor with { Floor = new WinRateCheck(0.500, 0.40) };

        Assert.True(heldFloor.StillHolds(plan, Profile));
        Assert.False(missedFloor.StillHolds(plan, Profile));                            // 10 points under the floor
        Assert.False(heldFloor.StillHolds(plan with { CasualFloor = 0.51 }, Profile));  // the profile moved the floor
    }

    [Fact]
    public void Repair_regenerates_a_level_whose_seed_pool_is_under_the_minimum()
    {
        var row = new ReportRow(new WinRateCheck(0.60, 0.62), Floor: null, PoolSize: Profile.MinPoolSize);

        Assert.True(row.StillHolds(Planner.Plan(55), Profile));
        Assert.False((row with { PoolSize = Profile.MinPoolSize - 1 }).StillHolds(Planner.Plan(55), Profile));
    }

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
        MoveRange = new MoveRange(16, 24),
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
