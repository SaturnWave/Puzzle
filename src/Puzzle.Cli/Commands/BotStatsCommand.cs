using System.Diagnostics;
using System.Globalization;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>botstats &lt;dir|file&gt; [--bot expert] [--runs 30] [--cap 150] [--out reports/expert_moves.csv]</c>:
/// plays every level with one bot and writes how many moves it needed (distribution over the runs) next to
/// the level's move limit.
/// </summary>
internal static class BotStatsCommand
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Run(CliArgs args)
    {
        string target = args.Required(0, "dir|file");
        string bot = args.Text("bot", "expert");
        int runs = args.Int("runs", 30);
        int cap = args.Int("cap", 150);
        string outPath = args.Text("out", $"reports/{bot}_moves.csv");
        SimCommand.CreatePolicy(bot); // fail fast on a typo

        string[] files = Directory.Exists(target)
            ? Directory.GetFiles(target, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray()
            : [target];
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        var clock = Stopwatch.StartNew();
        using var writer = new StreamWriter(outPath);
        writer.WriteLine("level,tier,size,colors,moveLimit,runs,completed,winAtLimit,minMoves,p25,medianMoves,p75,p90,maxMoves,avgMoves");
        for (int i = 0; i < files.Length; i++)
        {
            LevelDefinition level = LevelJson.Load(files[i]);
            LevelReport report = BatchSimulator.Run(level, () => SimCommand.CreatePolicy(bot), runs, cap, Rng.Derive(7, (ulong)level.Id.Value));
            IReadOnlyList<int> moves = report.CompletionMoves;
            writer.WriteLine(string.Join(',', new[]
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
            }));
            if ((i + 1) % 50 == 0 || i + 1 == files.Length)
            {
                writer.Flush();
                Console.WriteLine($"{i + 1}/{files.Length} levels, {clock.Elapsed.TotalSeconds:0} s");
            }
        }

        Console.WriteLine($"wrote {outPath}");
        return 0;
    }

    /// <summary>Empty when that share of runs never finished within the cap.</summary>
    private static string Moves(int? moves) => moves?.ToString(Invariant) ?? string.Empty;
}
