using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Puzzle.Core.Levels;
using Puzzle.Generator.Calibration;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Generator;

/// <summary>Quality numbers of one level as recorded in the report.</summary>
/// <param name="Target">The target win rate and the calibration bot's win rate at the limit.</param>
/// <param name="Floor">The casual floor and the floor bot's win rate at the limit; absent when the tier had no floor (or the report predates floors).</param>
public sealed record ReportRow(WinRateCheck Target, WinRateCheck? Floor, int PoolSize)
{
    /// <summary>
    /// Whether the recorded level still meets the profile, so <c>generate --repair</c> keeps it: its seed pool is large
    /// enough, it was calibrated against the casual floor its plan has now (a floor the profile added, changed or dropped
    /// since the row was written was never checked), and its limit stands on the recorded win rates.
    /// </summary>
    public bool StillHolds(LevelPlan plan, GenerationProfile profile) =>
        PoolSize >= profile.MinPoolSize
        && RecordsFloorOf(plan)
        && LimitVerification.Stands(Target, Floor, profile.Calibration.WinRateTolerance);

    private bool RecordsFloorOf(LevelPlan plan) => (plan.CasualFloor, Floor) switch
    {
        (null, null) => true,
        (double planned, WinRateCheck recorded) => GenerationReport.SameWhenRecorded(planned, recorded.Required),
        _ => false,
    };
}

/// <summary>The generation report: one CSV row per level, merged with the rows already on disk.</summary>
public static class GenerationReport
{
    public const string FileName = "report.csv";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly string[] LeadingColumns = ["level", "tier", "size", "playable", "colors", "style", "goals", "moves", "moveRange", "targetWin", "win", "winPlus5", "floor", "floorWin", "floorSetLimit", "p50", "p90", "nearMiss"];
    private static readonly string[] TrailingColumns = ["poolSize", "poolMinWin", "poolMaxWin", "candidates", "relaxed", "seconds"];

    /// <summary>Writes <paramref name="rows"/> into the report, replacing earlier rows of the same levels.</summary>
    public static void Upsert(string path, IReadOnlyList<LevelMetrics> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> bots = rows[0].BotWinRates.Keys.OrderBy(b => b, StringComparer.Ordinal).ToArray();
        string[] columns = LeadingColumns.Concat(bots.Select(b => b + "Win")).Concat(TrailingColumns).ToArray();

        SortedDictionary<int, string> lines = ExistingRows(path, columns);
        foreach (LevelMetrics row in rows)
        {
            lines[row.Number] = ToCsv(row, bots);
        }

        File.WriteAllLines(path, lines.Values.Prepend(string.Join(',', columns)));
    }

    /// <summary>
    /// The quality columns of existing report rows, keyed by level (for <c>generate --repair</c>). Columns are found
    /// by name, so a report written before a column was added still reads; a report without the floor columns reads
    /// as levels without a floor. A row whose cells do not parse is left out.
    /// </summary>
    public static IReadOnlyDictionary<int, ReportRow> Read(string path)
    {
        var rows = new Dictionary<int, ReportRow>();
        if (!File.Exists(path))
        {
            return rows;
        }

        string[] lines = File.ReadAllLines(path);
        var columns = new QualityColumns(lines.Length > 0 ? lines[0].Split(',') : []);
        if (!columns.HasRequired)
        {
            return rows;
        }

        foreach (string[] cells in lines.Skip(1).Select(line => line.Split(',')))
        {
            if (columns.TryRead(cells, out int number, out ReportRow? row))
            {
                rows[number] = row;
            }
        }

        return rows;
    }

    /// <summary>Per-tier averages, so a profile's outcome can be compared with another at a glance.</summary>
    public static string Summarize(IReadOnlyList<LevelMetrics> rows)
    {
        var text = new StringBuilder();
        text.AppendLine("tier       levels  avg moves  avg target  avg win  avg |win-target|  avg floor  avg floor win  floor set  relaxed");
        foreach (IGrouping<DifficultyTier, LevelMetrics> tier in rows.GroupBy(r => r.Tier).OrderBy(g => g.Key))
        {
            WinRateCheck[] floors = tier.Select(r => r.Floor).OfType<WinRateCheck>().ToArray();
            text.AppendLine(string.Format(
                Invariant,
                "{0,-10} {1,6}  {2,9:0.0}  {3,9:P0}  {4,7:P0}  {5,15:P1}  {6,9}  {7,13}  {8,9}  {9,7}",
                tier.Key,
                tier.Count(),
                tier.Average(r => r.MoveLimit),
                tier.Average(r => r.TargetWinRate),
                tier.Average(r => r.WinRate),
                tier.Average(r => Math.Abs(r.WinRate - r.TargetWinRate)),
                floors.Length == 0 ? "-" : floors.Average(f => f.Required).ToString("P0", Invariant),
                floors.Length == 0 ? "-" : floors.Average(f => f.Measured).ToString("P0", Invariant),
                tier.Count(r => r.FloorSetLimit),
                tier.Count(r => r.Relaxed)));
        }

        return text.ToString();
    }

    /// <summary>
    /// The rows already in the report, keyed by level and laid out in <paramref name="columns"/>. A report written with
    /// other columns (before a column was added, or with other report bots) is realigned by column name: a column it
    /// lacks stays empty, a column the new layout drops is left out. A row that does not fit its own header is kept as is.
    /// </summary>
    private static SortedDictionary<int, string> ExistingRows(string path, IReadOnlyList<string> columns)
    {
        var rows = new SortedDictionary<int, string>();
        if (!File.Exists(path))
        {
            return rows;
        }

        string[] lines = File.ReadAllLines(path);
        string[] header = lines.Length > 0 ? lines[0].Split(',') : [];
        int[] sourceOf = columns.Select(column => Array.IndexOf(header, column)).ToArray();
        foreach (string line in lines.Skip(1))
        {
            string[] cells = line.Split(',');
            if (int.TryParse(cells[0], NumberStyles.Integer, Invariant, out int level))
            {
                rows[level] = cells.Length == header.Length
                    ? string.Join(',', sourceOf.Select(source => source >= 0 ? cells[source] : string.Empty))
                    : line;
            }
        }

        return rows;
    }

    private static string ToCsv(LevelMetrics row, IReadOnlyList<string> bots)
    {
        IEnumerable<string> fields = new[]
        {
            row.Number.ToString(Invariant),
            row.Tier.ToString(),
            row.Size,
            row.Playable.ToString(Invariant),
            row.Colors.ToString(Invariant),
            row.Style,
            row.Goals,
            row.MoveLimit.ToString(Invariant),
            row.MoveRange.ToString(),
            Ratio(row.TargetWinRate),
            Ratio(row.WinRate),
            row.WinRateWithExtraMoves is double winWithExtraMoves ? Ratio(winWithExtraMoves) : string.Empty,
            row.Floor is WinRateCheck floor ? Ratio(floor.Required) : string.Empty,
            row.Floor is WinRateCheck floorWin ? Ratio(floorWin.Measured) : string.Empty,
            row.FloorSetLimit ? "true" : "false",
            row.MovesP50?.ToString(Invariant) ?? string.Empty,
            row.MovesP90?.ToString(Invariant) ?? string.Empty,
            Ratio(row.NearMiss),
        }
            .Concat(bots.Select(b => row.BotWinRates.TryGetValue(b, out double win) ? Ratio(win) : string.Empty))
            .Concat(new[]
            {
                row.PoolSize.ToString(Invariant),
                Ratio(row.PoolMinWinRate),
                Ratio(row.PoolMaxWinRate),
                row.CandidatesTried.ToString(Invariant),
                row.Relaxed ? "yes" : "no",
                row.Seconds.ToString("0.0", Invariant),
            });
        return string.Join(',', fields);
    }

    /// <summary>Whether two ratios read back alike from the report, which keeps three decimals.</summary>
    internal static bool SameWhenRecorded(double ratio, double otherRatio) => Ratio(ratio) == Ratio(otherRatio);

    private static string Ratio(double value) => value.ToString("0.000", Invariant);

    /// <summary>Where the columns <see cref="Read"/> needs sit in one report's header; the floor columns may be missing.</summary>
    private sealed class QualityColumns(string[] header)
    {
        private readonly int _level = Array.IndexOf(header, "level");
        private readonly int _target = Array.IndexOf(header, "targetWin");
        private readonly int _win = Array.IndexOf(header, "win");
        private readonly int _pool = Array.IndexOf(header, "poolSize");
        private readonly int _floor = Array.IndexOf(header, "floor");
        private readonly int _floorWin = Array.IndexOf(header, "floorWin");

        public bool HasRequired => Math.Min(Math.Min(_level, _target), Math.Min(_win, _pool)) >= 0;

        public bool TryRead(string[] cells, out int number, [NotNullWhen(true)] out ReportRow? row)
        {
            row = null;
            if (cells.Length != header.Length
                || !int.TryParse(cells[_level], NumberStyles.Integer, Invariant, out number)
                || !TryRatio(cells[_target], out double targetWin)
                || !TryRatio(cells[_win], out double winRate)
                || !int.TryParse(cells[_pool], NumberStyles.Integer, Invariant, out int poolSize)
                || !TryReadFloor(cells, out WinRateCheck? floor))
            {
                number = 0;
                return false;
            }

            row = new ReportRow(new WinRateCheck(targetWin, winRate), floor, poolSize);
            return true;
        }

        /// <summary>An empty or missing floor cell is a level without a floor; a floor without a readable floor win is a broken row.</summary>
        private bool TryReadFloor(string[] cells, out WinRateCheck? floor)
        {
            floor = null;
            if (_floor < 0 || cells[_floor].Length == 0)
            {
                return true;
            }

            if (_floorWin >= 0 && TryRatio(cells[_floor], out double required) && TryRatio(cells[_floorWin], out double measured))
            {
                floor = new WinRateCheck(required, measured);
                return true;
            }

            return false;
        }

        private static bool TryRatio(string cell, out double value) => double.TryParse(cell, NumberStyles.Float, Invariant, out value);
    }
}
