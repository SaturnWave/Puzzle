using System.Diagnostics;
using System.Globalization;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;
using Puzzle.Tooling.Simulation;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>botstats &lt;dir|file&gt; [--bot expert] [--runs 30] [--cap 150] [--out reports/expert_moves.csv]</c>:
/// plays every level with one bot and writes how many moves it needed (distribution over the runs) next to
/// the level's move limit. The last column is the win rate with the "+5 moves" offer (<see cref="ExtraMovesOffer"/>),
/// read from the same runs; it stays last so the columns before it keep their positions.
/// </summary>
internal static class BotStatsCommand
{
    /// <summary>Run i of a level plays <c>Rng.Derive(Rng.Derive(BaseSeed, id), i)</c>: seeds are per level, the same for every bot.</summary>
    private const ulong BaseSeed = 7;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Run(CliArgs args)
    {
        string target = args.Required(0, "dir|file");
        string bot = args.Text("bot", "expert");
        int runs = args.Int("runs", 30);
        int cap = args.Int("cap", 150);
        string outPath = args.Text("out", $"reports/{bot}_moves.csv");
        SimCommand.CreatePolicy(bot); // fail fast on a typo

        LevelDefinition[] levels = LevelFiles.Under(target).Select(LevelJson.Load).ToArray(); // a broken file stops the run before any game
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        var clock = Stopwatch.StartNew();
        using var writer = new StreamWriter(outPath);
        writer.WriteLine("level,tier,size,colors,moveLimit,runs,completed,winAtLimit,minMoves,p25,medianMoves,p75,p90,maxMoves,avgMoves,winAtLimitPlus5");
        int written = 0;
        foreach (string row in RowsInLevelOrder(levels, bot, runs, cap))
        {
            writer.WriteLine(row);
            written++;
            if (written % 50 == 0 || written == levels.Length)
            {
                writer.Flush();
                Console.WriteLine($"{written}/{levels.Length} levels, {clock.Elapsed.TotalSeconds:0} s");
            }
        }

        Console.WriteLine($"wrote {outPath}");
        return 0;
    }

    /// <summary>
    /// Every level's row, in level order and each as soon as it and every level before it are done: all runs of all
    /// levels are one queue across the cores (<see cref="MultiBatchSimulator"/>). A game that fails stops the rows at
    /// its level, after every level before it got its row.
    /// </summary>
    private static IEnumerable<string> RowsInLevelOrder(LevelDefinition[] levels, string bot, int runs, int cap)
    {
        IEnumerable<SimulationBatch> batches = levels.Select(level =>
            new SimulationBatch(level, () => SimCommand.CreatePolicy(bot), Rng.Derive(BaseSeed, (ulong)level.Id.Value)));
        return levels.Zip(MultiBatchSimulator.Run(batches, runs, cap), (level, report) => Row(level, report, runs));
    }

    private static string Row(LevelDefinition level, LevelReport report, int runs)
    {
        IReadOnlyList<int> moves = report.CompletionMoves;
        return string.Join(',', new[]
        {
            level.Id.ToString(),
            level.Difficulty.ToString(),
            $"{level.Layout.Shape.Width}x{level.Layout.Shape.Height}",
            level.Spawn.Colors.ToString(Invariant),
            level.MoveLimit.ToString(Invariant),
            runs.ToString(Invariant),
            moves.Count.ToString(Invariant),
            report.WinRateAt(level.MoveLimit).ToString("0.000", Invariant),
            moves.Count > 0 ? moves[0].ToString(Invariant) : string.Empty,
            Moves(report.MovesNeeded(0.25)),
            Moves(report.MovesNeeded(0.50)),
            Moves(report.MovesNeeded(0.75)),
            Moves(report.MovesNeeded(0.90)),
            moves.Count > 0 ? moves[^1].ToString(Invariant) : string.Empty,
            moves.Count > 0 ? moves.Average().ToString("0.0", Invariant) : string.Empty,
            report.WinRateWithExtraMoves(level.MoveLimit)?.ToString("0.000", Invariant) ?? string.Empty,
        });
    }

    /// <summary>Empty when that share of runs never finished within the cap.</summary>
    private static string Moves(int? moves) => moves?.ToString(Invariant) ?? string.Empty;
}
