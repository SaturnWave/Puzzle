using System.Globalization;
using Puzzle.Core.Levels;
using Puzzle.Economy.Settings;
using Puzzle.Tooling;
using Puzzle.Tooling.Hints;
using Puzzle.Tooling.Recording;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>humans [attempts.jsonl] [--levels &lt;folder&gt;] [--economy economy/default.json] [--runs 30] [--out reports/humans.csv]</c>:
/// compares the attempts people played, in the web lab or the released game, with the greedy bots on the same games
/// (docs/GAME_DESIGN.md §20). The attempts the game showed hints in are compared apart, beside the same bots following
/// the hints of the settings file (<see cref="GameHints"/>): its hint bot and its cap, shown before a share of the
/// moves worked out from its wait and the recorded time a move. Without a file it reads the log the lab writes by
/// default, <see cref="AttemptLog.DefaultLocation"/> in the repository root; without a level folder it loads every level
/// the lab offers, <c>levels</c> in the repository root, samples included; without a settings file it reads
/// <c>economy/default.json</c> in the repository root.
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
        string economyPath = args.OptionalText("economy") ?? DefaultEconomyPath();
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

        if (!File.Exists(economyPath))
        {
            throw new UsageException($"No economy settings at '{economyPath}'.");
        }

        EconomySettings economy;
        try
        {
            economy = SettingsLoader.Load(economyPath);
        }
        catch (EconomySettingsFormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
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
        GameHints? gameHints = GameHintsOf(economy.Game.Hints, economy.Habit.PerMove);
        HumanBotReport report = gameHints is null
            ? HumanBotComparison.Compare(log.Records, levels, runs)
            : HumanBotComparison.Compare(log.Records, levels, runs, gameHints);
        WriteCsv(report, outPath);
        Console.WriteLine($"read {Path.GetFullPath(recordsPath)}");
        Console.WriteLine($"levels {Path.GetFullPath(levelsPath)}");
        Console.WriteLine($"hints {Path.GetFullPath(economyPath)}: {HintsTold(economy.Game.Hints, economy.Habit.PerMove, gameHints)}");
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

    /// <exception cref="UsageException">The current folder is not inside a repository.</exception>
    private static string DefaultEconomyPath() =>
        LevelLibrary.FindRepositoryRoot(Directory.GetCurrentDirectory()) is { } root
            ? Path.Combine(root, "economy", "default.json")
            : throw new UsageException("Not inside the repository (no 'levels' folder above here); give the settings file with --economy.");

    /// <summary>
    /// The hints the bots beside the hinted attempts follow: the settings' bot and cap, shown when the player is idle
    /// for longer than the wait (<see cref="HintMoment.WhenIdle.ForWait"/>). Absent when the settings show no hint.
    /// </summary>
    private static GameHints? GameHintsOf(HintSettings hints, TimeSpan perMove) => hints.PerAttempt == 0
        ? null
        : new GameHints(hints.Bot, hints.PerAttempt, HintMoment.WhenIdle.ForWait(hints.Idle, perMove));

    private static string HintsTold(HintSettings hints, TimeSpan perMove, GameHints? gameHints) => gameHints is null
        ? "the game shows none (hints.perAttempt 0), so no bot stands beside the hinted attempts"
        : string.Create(Invariant, $"the bots beside the hinted attempts are shown {gameHints} (the game waits {hints.Idle.TotalSeconds:0.#} s; people take {perMove.TotalSeconds:0.0} s a move), and follow each");

    private static void WriteCsv(HumanBotReport report, string outPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? ".");
        using var writer = new StreamWriter(outPath);
        // A bot's plain column is its rate on the finished attempts' games, beside humanWinRate; its "OnAll" column is on
        // every attempt's game, beside humanWinRateLowerBound. In the hinted group's rows the same bots follow the game's hints.
        writer.WriteLine(
            "group,level,moveLimit,finished,abandoned,wins,devices,humanWinRate,humanWinRateLowerBound,medianWinMoves,"
            + $"{string.Join(',', report.Bots)},{string.Join(',', report.Bots.Select(bot => $"{bot}OnAll"))},"
            + "closest,closestAtLowerBound,abandonsDecideClosest");
        WriteGroup(writer, "unhinted", report.Levels, report.Overall, report.Bots);
        if (report.Records.Hinted > 0)
        {
            WriteGroup(writer, "hinted", report.Hinted.Levels, report.Hinted.Overall, report.Bots);
        }
    }

    private static void WriteGroup(StreamWriter writer, string group, IReadOnlyList<LevelComparison> levels, ComparisonFigures overall, IReadOnlyList<string> bots)
    {
        foreach (LevelComparison row in levels)
        {
            writer.WriteLine(CsvRow(group, row.Level.ToString(), row.MoveLimit.ToString(Invariant), row.MedianWinMoves, row.Figures, bots));
        }

        writer.WriteLine(CsvRow(group, "all", string.Empty, medianWinMoves: null, overall, bots));
    }

    /// <summary>Empty cells for what is absent: no finished attempt, no win, or no bot figure.</summary>
    private static string CsvRow(string group, string level, string moveLimit, double? medianWinMoves, ComparisonFigures figures, IReadOnlyList<string> bots)
    {
        string[] humans =
        [
            group,
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
            $"records: {tally.Total} read, {tally.Compared} compared ({tally.Hinted} of them hinted, compared apart); left out: {tally.Assisted} assisted, "
            + $"{tally.TesterMode} in the game's tester mode, {tally.ChosenSeeds} hand-picked seeds, {tally.UnknownLevel} not in the level folder (or changed since), "
            + $"{unreadableLines} unreadable lines");
        Console.WriteLine($"bots: {runs} games per profile on each attempt's own seeds, at its move limit");
        PrintGroup("without hints", report.Levels, report.Overall, report.Bots);
        if (tally.Hinted > 0)
        {
            PrintGroup("hinted (the bots follow the game's hints)", report.Hinted.Levels, report.Hinted.Overall, report.Bots);
        }
        else
        {
            Console.WriteLine("hinted: no compared attempt was shown a hint by the game");
        }

        Console.WriteLine("humans: finished attempts only, bots on their games; low: every abandoned attempt counted as a loss,");
        Console.WriteLine("matched with the bots on every attempt's game (in the CSV). Two closest bots: the abandons decide.");
    }

    private static void PrintGroup(string group, IReadOnlyList<LevelComparison> levels, ComparisonFigures overall, IReadOnlyList<string> bots)
    {
        Console.WriteLine($"{group}:");
        Console.WriteLine($"{"level",6} {"limit",5} {"finished",8} {"abandoned",9} {"humans",7} {"low",7} " + string.Join(' ', bots.Select(bot => $"{bot,7}")) + "  closest");
        if (levels.Count <= MaxPrintedLevels)
        {
            foreach (LevelComparison row in levels)
            {
                Console.WriteLine(Line(row.Level.ToString(), row.MoveLimit.ToString(Invariant), row.Figures, bots));
            }
        }
        else
        {
            Console.WriteLine($"  ({levels.Count} level rows: see the CSV)");
        }

        Console.WriteLine(Line("all", string.Empty, overall, bots));
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
