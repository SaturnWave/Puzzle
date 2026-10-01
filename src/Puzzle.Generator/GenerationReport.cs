using System.Globalization;
using System.Text;
using Puzzle.Core.Levels;

namespace Puzzle.Generator;

/// <summary>Quality numbers of one level as recorded in the report.</summary>
public sealed record ReportRow(double TargetWinRate, double WinRate, int PoolSize);

/// <summary>The generation report: one CSV row per level, merged with the rows already on disk.</summary>
public static class GenerationReport
{
    public const string FileName = "report.csv";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly string[] LeadingColumns = ["level", "tier", "size", "playable", "colors", "style", "goals", "moves", "moveRange", "targetWin", "win", "p50", "p90", "nearMiss"];
    private static readonly string[] TrailingColumns = ["poolSize", "poolMinWin", "poolMaxWin", "candidates", "relaxed", "seconds"];

    /// <summary>Writes <paramref name="rows"/> into the report, replacing earlier rows of the same levels.</summary>
    public static void Upsert(string path, IReadOnlyList<LevelMetrics> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> bots = rows[0].BotWinRates.Keys.OrderBy(b => b, StringComparer.Ordinal).ToArray();
        string header = string.Join(',', LeadingColumns.Concat(bots.Select(b => b + "Win")).Concat(TrailingColumns));

        var lines = new SortedDictionary<int, string>();
        if (File.Exists(path))
        {
            foreach (string line in File.ReadLines(path).Skip(1))
            {
                if (int.TryParse(line.Split(',')[0], NumberStyles.Integer, Invariant, out int level))
                {
                    lines[level] = line;
                }
            }
        }

        foreach (LevelMetrics row in rows)
        {
            lines[row.Number] = ToCsv(row, bots);
        }

        File.WriteAllLines(path, lines.Values.Prepend(header));
    }

    /// <summary>The quality columns of existing report rows, keyed by level (for <c>generate --repair</c>).</summary>
    public static IReadOnlyDictionary<int, ReportRow> Read(string path)
    {
        var rows = new Dictionary<int, ReportRow>();
        if (!File.Exists(path))
        {
            return rows;
        }

        string[] lines = File.ReadAllLines(path);
        string[] header = lines.Length > 0 ? lines[0].Split(',') : [];
        int level = Array.IndexOf(header, "level");
        int target = Array.IndexOf(header, "targetWin");
        int win = Array.IndexOf(header, "win");
        int pool = Array.IndexOf(header, "poolSize");
        if (Math.Min(Math.Min(level, target), Math.Min(win, pool)) < 0)
        {
            return rows;
        }

        foreach (string[] cells in lines.Skip(1).Select(line => line.Split(',')))
        {
            if (cells.Length == header.Length
                && int.TryParse(cells[level], NumberStyles.Integer, Invariant, out int number)
                && double.TryParse(cells[target], NumberStyles.Float, Invariant, out double targetWin)
                && double.TryParse(cells[win], NumberStyles.Float, Invariant, out double winRate)
                && int.TryParse(cells[pool], NumberStyles.Integer, Invariant, out int poolSize))
            {
                rows[number] = new ReportRow(targetWin, winRate, poolSize);
            }
        }

        return rows;
    }

    /// <summary>Per-tier averages, so a profile's outcome can be compared with another at a glance.</summary>
    public static string Summarize(IReadOnlyList<LevelMetrics> rows)
    {
        var text = new StringBuilder();
        text.AppendLine("tier       levels  avg moves  avg target  avg win  avg |win-target|  relaxed");
        foreach (IGrouping<DifficultyTier, LevelMetrics> tier in rows.GroupBy(r => r.Tier).OrderBy(g => g.Key))
        {
            text.AppendLine(string.Format(
                Invariant,
                "{0,-10} {1,6}  {2,9:0.0}  {3,9:P0}  {4,7:P0}  {5,15:P1}  {6,7}",
                tier.Key,
                tier.Count(),
                tier.Average(r => r.MoveLimit),
                tier.Average(r => r.TargetWinRate),
                tier.Average(r => r.WinRate),
                tier.Average(r => Math.Abs(r.WinRate - r.TargetWinRate)),
                tier.Count(r => r.Relaxed)));
        }

        return text.ToString();
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

    private static string Ratio(double value) => value.ToString("0.000", Invariant);
}
