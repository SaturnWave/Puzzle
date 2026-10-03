using System.Globalization;
using Puzzle.Core.Levels;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>humans [attempts.jsonl] [--levels &lt;folder&gt;] [--runs 30] [--out reports/humans.csv]</c>: compares the
/// attempts people played in the web lab with the greedy bots on the same games (docs/GAME_DESIGN.md §20). Without a
/// file it reads the log the lab writes by default, <see cref="AttemptLog.DefaultLocation"/> in the repository root, and
/// without a level folder it loads every level the lab offers, <c>levels</c> in the repository root, samples included.
/// </summary>
internal static class HumansCommand
{
    /// <summary>More levels than this are only summed up on the console; the CSV always has every row.</summary>
    private const int MaxPrintedLevels = 20;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Run(CliArgs args)
    {
        string recordsPath = args.Positionals.Count > 0 ? args.Positionals[0] : DefaultRecordsPath();
        string levelsPath = args.OptionalText("levels") ?? DefaultLevelsPath();
        int runs = args.Int("runs", 30);
        string outPath = args.Text("out", "reports/humans.csv");
        if (runs < 1)
        {
            throw new UsageException("--runs must be at least 1.");
        }

        if (!File.Exists(recordsPath))
        {
            throw new UsageException($"No attempt records at '{recordsPath}'.");
        }

        if (!Directory.Exists(levelsPath))
        {
            throw new UsageException($"No level folder at '{levelsPath}'.");
        }

        AttemptLogContents log;
        try
        {
            log = new AttemptLog(recordsPath).ReadAll();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not read the attempt records at '{recordsPath}': {ex.Message}");
            return 1;
        }

        foreach (AttemptLogProblem problem in log.Problems)
        {
            Console.Error.WriteLine($"skipped line {problem.Line}: {problem.Message}");
        }

        LevelLibrary library = LevelLibrary.Load(levelsPath);
        foreach (LibraryProblem problem in library.Problems)
        {
            Console.Error.WriteLine($"skipped level {problem.Path}: {problem.Message}");
        }

        LevelDefinition[] levels = library.Levels.Select(entry => entry.Level).ToArray();
        HumanBotReport report = HumanBotComparison.Compare(log.Records, levels, runs);
        WriteCsv(report, outPath);
        Console.WriteLine($"read {Path.GetFullPath(recordsPath)}");
        Console.WriteLine($"levels {Path.GetFullPath(levelsPath)}");
        PrintSummary(report, log.Problems.Count, runs);
        Console.WriteLine($"wrote {outPath}");
        return 0;
    }

    /// <exception cref="UsageException">The current folder is not inside a repository.</exception>
    private static string DefaultRecordsPath() =>
        AttemptLog.FindDefaultPath(Directory.GetCurrentDirectory())
            ?? throw new UsageException("Not inside the repository (no 'levels' folder above here); give the path of the attempts file.");

    /// <summary>The lab's level folder, found the way the lab finds it, so both compare the same levels.</summary>
    /// <exception cref="UsageException">The current folder is not inside a repository.</exception>
    private static string DefaultLevelsPath() =>
        LevelLibrary.FindLevelsDirectory(Directory.GetCurrentDirectory())
            ?? throw new UsageException("Not inside the repository (no 'levels' folder above here); give the level folder with --levels.");

    private static void WriteCsv(HumanBotReport report, string outPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? ".");
        using var writer = new StreamWriter(outPath);
        // A bot's plain column is its rate on the finished attempts' games, beside humanWinRate; its "OnAll" column is on
        // every attempt's game, beside humanWinRateLowerBound.
        writer.WriteLine(
            "level,moveLimit,finished,abandoned,wins,devices,humanWinRate,humanWinRateLowerBound,medianWinMoves,"
            + $"{string.Join(',', report.Bots)},{string.Join(',', report.Bots.Select(bot => $"{bot}OnAll"))},"
            + "closest,closestAtLowerBound,abandonsDecideClosest");
        foreach (LevelComparison row in report.Levels)
        {
            writer.WriteLine(CsvRow(row.Level.ToString(), row.MoveLimit.ToString(Invariant), row.MedianWinMoves, row.Figures, report.Bots));
        }

        writer.WriteLine(CsvRow("all", string.Empty, medianWinMoves: null, report.Overall, report.Bots));
    }

    /// <summary>Empty cells for what is absent: no finished attempt, no win, or no bot figure.</summary>
    private static string CsvRow(string level, string moveLimit, double? medianWinMoves, ComparisonFigures figures, IReadOnlyList<string> bots)
    {
        string[] humans =
        [
            level,
            moveLimit,
            figures.Finished.ToString(Invariant),
            figures.Abandoned.ToString(Invariant),
            figures.Wins.ToString(Invariant),
            figures.Devices.ToString(Invariant),
            CsvRate(figures.HumanWinRate),
            CsvRate(figures.HumanWinRateLowerBound),
            medianWinMoves?.ToString("0.#", Invariant) ?? string.Empty,
        ];
        string[] closest =
        [
            figures.ClosestBot ?? string.Empty,
            figures.ClosestBotAtLowerBound ?? string.Empty,
            figures.AbandonsDecideClosest ? "true" : "false",
        ];
        IEnumerable<string> onFinished = bots.Select(bot => CsvRate(RatesOf(figures, bot)?.WinRateOnFinished));
        IEnumerable<string> onAll = bots.Select(bot => CsvRate(RatesOf(figures, bot)?.WinRateOnAll));
        return string.Join(',', humans.Concat(onFinished).Concat(onAll).Concat(closest));
    }

    private static string CsvRate(double? rate) => rate?.ToString("0.000", Invariant) ?? string.Empty;

    private static BotWinRate? RatesOf(ComparisonFigures figures, string bot) => figures.Bots.FirstOrDefault(rate => rate.Bot == bot);

    private static void PrintSummary(HumanBotReport report, int unreadableLines, int runs)
    {
        RecordTally tally = report.Records;
        Console.WriteLine(
            $"records: {tally.Total} read, {tally.Compared} compared; left out: {tally.Assisted} assisted, "
            + $"{tally.ChosenSeeds} hand-picked seeds, {tally.UnknownLevel} not in the level folder (or changed since), {unreadableLines} unreadable lines");
        Console.WriteLine($"bots: {runs} games per profile on each attempt's own seeds, at its move limit");
        string header = $"{"level",6} {"limit",5} {"finished",8} {"abandoned",9} {"humans",7} {"low",7} "
            + string.Join(' ', report.Bots.Select(bot => $"{bot,7}")) + "  closest";
        Console.WriteLine(header);
        if (report.Levels.Count <= MaxPrintedLevels)
        {
            foreach (LevelComparison row in report.Levels)
            {
                Console.WriteLine(Line(row.Level.ToString(), row.MoveLimit.ToString(Invariant), row.Figures, report.Bots));
            }
        }
        else
        {
            Console.WriteLine($"  ({report.Levels.Count} level rows: see the CSV)");
        }

        Console.WriteLine(Line("all", string.Empty, report.Overall, report.Bots));
        Console.WriteLine("humans: finished attempts only, bots on their games; low: every abandoned attempt counted as a loss,");
        Console.WriteLine("matched with the bots on every attempt's game (in the CSV). Two closest bots: the abandons decide.");
    }

    private static string Line(string level, string moveLimit, ComparisonFigures figures, IReadOnlyList<string> bots) =>
        $"{level,6} {moveLimit,5} {figures.Finished,8} {figures.Abandoned,9} {Percent(figures.HumanWinRate),7} {Percent(figures.HumanWinRateLowerBound),7} "
        + string.Join(' ', bots.Select(bot => $"{Percent(RatesOf(figures, bot)?.WinRateOnFinished),7}"))
        + $"  {Closest(figures)}";

    private static string Closest(ComparisonFigures figures) => figures.AbandonsDecideClosest
        ? $"{figures.ClosestBot ?? "-"} / low: {figures.ClosestBotAtLowerBound ?? "-"}"
        : figures.ClosestBot ?? "-";

    private static string Percent(double? rate) => rate is double value ? SimCommand.Percent(value) : "-";
}
