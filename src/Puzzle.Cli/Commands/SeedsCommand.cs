using System.Diagnostics;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>seeds &lt;level.json&gt; [--board varied|fixed] [--board-seed N] [--candidates 64] [--games 64] [--pool 16]
/// [--tolerance 5] [--bot average] [--write]</c>: measures how differently random seeds play and builds the
/// equal-difficulty seed pool that retries cycle through.
/// </summary>
internal static class SeedsCommand
{
    public static int Run(CliArgs args)
    {
        string path = args.Required(0, "level.json");
        LevelDefinition level = LevelJson.Load(path);
        string botName = args.Text("bot", "average");
        SimCommand.CreatePolicy(botName); // fail fast on a typo
        var settings = new SeedPoolSettings
        {
            Candidates = args.Int("candidates", 64),
            GamesPerSeed = args.Int("games", 64),
            PoolSize = args.Int("pool", 16),
            TolerancePoints = args.Int("tolerance", 5),
        };
        Console.WriteLine(SimCommand.Describe(level));

        var clock = Stopwatch.StartNew();
        StartBoard board = ChooseBoard(args, level, botName, settings);
        double? target = args.Flag("target") ? ParseTarget(args.Text("target", string.Empty)) : null;
        SeedPoolReport report = SeedPoolCalibrator.Calibrate(level, board, () => SimCommand.CreatePolicy(botName), settings, target);
        clock.Stop();

        Print(report, board, settings, level.MoveLimit);
        Console.WriteLine($"time {clock.Elapsed.TotalSeconds:0.0} s");
        if (args.Flag("write"))
        {
            var attempts = new AttemptPolicy { Board = board, SeedPool = report.Pool.Select(d => d.Seed).ToArray() };
            LevelJson.Save(level.WithAttempts(attempts), path);
            Console.WriteLine($"wrote {report.Pool.Count} pool seeds ({Describe(board)}) to {path}");
        }

        return 0;
    }

    /// <summary>A fixed board without an explicit seed uses the varied-board candidate of typical difficulty.</summary>
    private static StartBoard ChooseBoard(CliArgs args, LevelDefinition level, string botName, SeedPoolSettings settings)
    {
        string mode = args.Text("board", "varied");
        if (mode == "varied")
        {
            return new StartBoard.Varied();
        }

        if (mode != "fixed")
        {
            throw new UsageException($"--board must be varied or fixed, got '{mode}'.");
        }

        if (args.Flag("board-seed"))
        {
            return new StartBoard.Fixed((uint)args.ULong("board-seed", 0));
        }

        SeedPoolReport varied = SeedPoolCalibrator.Calibrate(level, new StartBoard.Varied(), () => SimCommand.CreatePolicy(botName), settings);
        Console.WriteLine($"fixed board: picked seed {varied.Typical.Seed} (median {varied.Typical.MedianMoves} moves, typical {varied.TypicalMedianMoves})");
        return new StartBoard.Fixed(varied.Typical.Seed);
    }

    private static void Print(SeedPoolReport report, StartBoard board, SeedPoolSettings settings, int moveLimit)
    {
        IReadOnlyList<SeedDifficulty> all = report.Candidates;
        SeedDifficulty At(double share) => all[Math.Min(all.Count - 1, (int)(share * all.Count))];
        Console.WriteLine($"{all.Count} candidate seeds x {settings.GamesPerSeed} bot games ({Describe(board)})");
        Console.WriteLine("median moves per seed  : "
            + $"easiest {all[0].MedianMoves} | p10 {At(0.1).MedianMoves} | typical {report.TypicalMedianMoves} | p90 {At(0.9).MedianMoves} | hardest {all[^1].MedianMoves}");
        Console.WriteLine($"target win rate        : {SimCommand.Percent(report.TypicalWinRate).Trim()} at limit {moveLimit}");
        Console.WriteLine($"win rate at limit {moveLimit,3}  : {Range(all)}  (spread between random seeds)");
        if (report.Pool.Count == 0)
        {
            Console.WriteLine("pool: no seed within tolerance");
            return;
        }

        Console.WriteLine($"pool {report.Pool.Count} seeds (+/-{settings.TolerancePoints} points): median moves {report.Pool.Min(d => d.MedianMoves)}..{report.Pool.Max(d => d.MedianMoves)}, win rate {Range(report.Pool)}");
    }

    private static double ParseTarget(string text) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double target) && target is > 0 and < 1
            ? target
            : throw new UsageException($"--target expects a win rate between 0 and 1, got '{text}'.");

    private static string Range(IEnumerable<SeedDifficulty> seeds) =>
        $"{SimCommand.Percent(seeds.Min(d => d.WinRateAtLimit)).Trim()} .. {SimCommand.Percent(seeds.Max(d => d.WinRateAtLimit)).Trim()}";

    private static string Describe(StartBoard board) => board switch
    {
        StartBoard.Fixed fixedBoard => $"fixed board {fixedBoard.Seed}",
        StartBoard.Varied _ => "varied boards",
        _ => throw new InvalidOperationException($"Unhandled start board {board.GetType().Name}."),
    };
}
