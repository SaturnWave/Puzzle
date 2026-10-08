using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;

namespace Puzzle.Tooling.Simulation;

/// <summary>Which games the runs of a batch play. Closed set.</summary>
public enum BatchGames
{
    /// <summary>Run i plays a seed of its own, <c>Rng.Derive(Seed, i)</c>: the level on boards nobody picked.</summary>
    FreshSeeds,

    /// <summary>
    /// Run i plays the level's attempt i as a player meets it (<see cref="AttemptPolicy.SeedsFor"/>): the seeds of its
    /// retry pool in turn, or any seed where the level has no pool. <c>Rng.Derive(Seed, i)</c> then drives only the
    /// bot's own choices, so the level is measured on the pool it ships with, in games no calibration has read.
    /// </summary>
    PlayerAttempts,
}

/// <summary>
/// One batch of bot games: <paramref name="Level"/>, played by the bot <paramref name="CreatePolicy"/> makes. With
/// <see cref="BatchGames.FreshSeeds"/> run i plays the seed <see cref="BatchSimulator.Run"/> would give it,
/// <c>Rng.Derive(Seed, i)</c>. Either way two batches of one level with the same seed play the same games: the same
/// starting boards and the same falling tiles for both bots.
/// </summary>
/// <param name="CreatePolicy">Called once per game, on the thread that plays it: policies are not thread-safe.</param>
public sealed record SimulationBatch(LevelDefinition Level, Func<IBotPolicy> CreatePolicy, ulong Seed, BatchGames Games = BatchGames.FreshSeeds)
{
    /// <summary>Plays run <paramref name="run"/> of the batch with a policy of its own, followed to <paramref name="moveCap"/>.</summary>
    /// <exception cref="LevelInitializationException">The level cannot produce a starting board.</exception>
    public RunResult Play(int run, int moveCap)
    {
        ulong runSeed = Rng.Derive(Seed, (ulong)run);
        return Games switch
        {
            BatchGames.FreshSeeds => SimulationRunner.Play(Level, CreatePolicy(), runSeed, moveCap),
            BatchGames.PlayerAttempts => SimulationRunner.Play(Level, CreatePolicy(), Level.Attempts.SeedsFor(run, unpooledSeed: (uint)runSeed), runSeed, moveCap),
            _ => throw new InvalidOperationException($"Unhandled batch games {Games}."),
        };
    }
}

/// <summary>Plays many batches of bot games as one queue across the CPU cores.</summary>
public static class MultiBatchSimulator
{
    /// <summary>
    /// Plays <paramref name="runs"/> games of every batch, each followed to <paramref name="moveCap"/>, and hands back
    /// one report per batch, in the order of the batches. Every game is a job in a single queue, so cores only idle
    /// during the very last games instead of at the end of every batch, and a batch's report is handed back as soon as
    /// it and every batch before it are done. A game depends on nothing but its batch and its run number, so the
    /// reports are the same whatever the number of workers and whichever game finishes first.
    /// </summary>
    /// <remarks>
    /// A failed game travels through the queue as its outcome, so every batch before the failing one still gets its
    /// report; then the game's own exception is rethrown with its original stack trace.
    /// </remarks>
    /// <param name="workers">Games played at the same time; absent for one per core.</param>
    /// <exception cref="ArgumentOutOfRangeException">Fewer than one run or one worker.</exception>
    /// <exception cref="LevelInitializationException">While the reports are read: a level cannot produce a starting board.</exception>
    /// <exception cref="InvalidOperationException">While the reports are read: a bot or the engine failed in a game.</exception>
    public static IEnumerable<LevelReport> Run(IEnumerable<SimulationBatch> batches, int runs, int moveCap, int? workers = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(runs, 1);
        if (workers is int count)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(count, 1, nameof(workers));
        }

        return ReportsInBatchOrder(batches, runs, moveCap, workers);
    }

    private static IEnumerable<LevelReport> ReportsInBatchOrder(IEnumerable<SimulationBatch> batches, int runs, int moveCap, int? workers)
    {
        ParallelQuery<GameJob> queue = Partitioner.Create(Jobs(batches, runs), EnumerablePartitionerOptions.NoBuffering)
            .AsParallel()
            .AsOrdered()
            .WithMergeOptions(ParallelMergeOptions.NotBuffered); // pass each result on at once instead of in batches
        if (workers is int count)
        {
            queue = queue.WithDegreeOfParallelism(count);
        }

        foreach (GameOutcome[] outcomes in queue.Select(job => job.Play(moveCap)).AsSequential().Chunk(runs))
        {
            if (outcomes.OfType<GameOutcome.Failed>().FirstOrDefault() is { } failed)
            {
                ExceptionDispatchInfo.Capture(failed.Exception).Throw();
            }

            RunResult[] results = outcomes.OfType<GameOutcome.Played>().Select(played => played.Result).ToArray();
            yield return LevelReport.From(results, moveCap);
        }
    }

    private static IEnumerable<GameJob> Jobs(IEnumerable<SimulationBatch> batches, int runs) =>
        from batch in batches
        from run in Enumerable.Range(0, runs)
        select new GameJob(batch, run);

    /// <summary>Run <see cref="Run"/> of a batch.</summary>
    private readonly record struct GameJob(SimulationBatch Batch, int Run)
    {
        /// <summary>
        /// Plays the game with a policy of its own. What a game can fail with (an unplayable level, or a bot or engine
        /// fault reported as <see cref="InvalidOperationException"/>) becomes the outcome, to be rethrown in batch order;
        /// nothing is swallowed.
        /// </summary>
        public GameOutcome Play(int moveCap)
        {
            try
            {
                return new GameOutcome.Played(Batch.Play(Run, moveCap));
            }
            catch (Exception ex) when (ex is LevelInitializationException or InvalidOperationException)
            {
                return new GameOutcome.Failed(ex);
            }
        }
    }

    /// <summary>How one game ended: with its result, or with the exception it failed with.</summary>
    private abstract record GameOutcome
    {
        private GameOutcome()
        {
        }

        public sealed record Played(RunResult Result) : GameOutcome;

        public sealed record Failed(Exception Exception) : GameOutcome;
    }
}
