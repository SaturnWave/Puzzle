using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
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
        LevelDefinition[] levels = files.Select(LevelJson.Load).ToArray(); // a broken file stops the run before any game
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        var clock = Stopwatch.StartNew();
        using var writer = new StreamWriter(outPath);
        writer.WriteLine("level,tier,size,colors,moveLimit,runs,completed,winAtLimit,minMoves,p25,medianMoves,p75,p90,maxMoves,avgMoves");
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
    /// Plays every run of every level as one job in a single queue, one worker per core, so cores only idle during
    /// the very last runs instead of at the end of every level. Results come back in (level, run) order, so each level's
    /// report sees its runs in the same order as <see cref="BatchSimulator.Run"/> and its row can be written as soon as
    /// it and every level before it are done.
    /// </summary>
    /// <remarks>
    /// A failed job travels through the pipeline as its outcome, so every level before the failing one still gets its
    /// row; then the job's own exception is rethrown with its original stack trace.
    /// </remarks>
    private static IEnumerable<string> RowsInLevelOrder(LevelDefinition[] levels, string bot, int runs, int cap)
    {
        IEnumerable<RunOutcome[]> outcomesByLevel = Partitioner.Create(Jobs(levels, runs), EnumerablePartitionerOptions.NoBuffering)
            .AsParallel()
            .AsOrdered()
            .WithMergeOptions(ParallelMergeOptions.NotBuffered) // pass each result on at once instead of in batches
            .Select(job => job.Play(SimCommand.CreatePolicy(bot), cap)) // a policy of its own: policies are not thread-safe
            .AsSequential()
            .Chunk(runs);
        foreach ((LevelDefinition level, RunOutcome[] outcomes) in levels.Zip(outcomesByLevel))
        {
            if (outcomes.OfType<RunOutcome.Failed>().FirstOrDefault() is { } failed)
            {
                ExceptionDispatchInfo.Capture(failed.Exception).Throw();
            }

            RunResult[] results = outcomes.OfType<RunOutcome.Played>().Select(played => played.Result).ToArray();
            yield return Row(level, LevelReport.From(results, cap), runs);
        }
    }

    private static IEnumerable<RunJob> Jobs(LevelDefinition[] levels, int runs) =>
        from level in levels
        from run in Enumerable.Range(0, runs)
        select new RunJob(level, run);

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
        });
    }

    /// <summary>Empty when that share of runs never finished within the cap.</summary>
    private static string Moves(int? moves) => moves?.ToString(Invariant) ?? string.Empty;

    /// <summary>
    /// Run <see cref="Run"/> of one level. Seeds are per level (<c>Rng.Derive(7, id)</c>), and run i plays the seed
    /// <see cref="BatchSimulator.Run"/> would give it, <c>Rng.Derive(levelSeed, i)</c>, so no ordering of jobs changes a result.
    /// </summary>
    private readonly record struct RunJob(LevelDefinition Level, int Run)
    {
        /// <summary>
        /// Plays the run. What a game can fail with (an unplayable level, or a bot or engine fault reported as
        /// <see cref="InvalidOperationException"/>) becomes the outcome, to be rethrown in level order; nothing is swallowed.
        /// </summary>
        public RunOutcome Play(IBotPolicy policy, int cap)
        {
            try
            {
                return new RunOutcome.Played(
                    SimulationRunner.Play(Level, policy, Rng.Derive(Rng.Derive(7, (ulong)Level.Id.Value), (ulong)Run), cap));
            }
            catch (Exception ex) when (ex is LevelInitializationException or InvalidOperationException)
            {
                return new RunOutcome.Failed(ex);
            }
        }
    }

    /// <summary>How one job ended: with its result, or with the exception it failed with.</summary>
    private abstract record RunOutcome
    {
        private RunOutcome()
        {
        }

        public sealed record Played(RunResult Result) : RunOutcome;

        public sealed record Failed(Exception Exception) : RunOutcome;
    }
}
