using System.IO;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

public class GenerationReportTests
{
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

        Assert.Equal(new ReportRow(0.95, 0.965, 16), rows[1]);
        Assert.Equal(new ReportRow(0.95, 1.0, 16), rows[2]);
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
