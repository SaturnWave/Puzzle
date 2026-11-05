using System;
using System.Threading.Tasks;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;

namespace Puzzle.Bot.Simulation
{
    /// <summary>Plays many bot games of one level across all CPU cores.</summary>
    public static class BatchSimulator
    {
        /// <param name="createPolicy">Called once per worker thread: policies keep scratch buffers and are not thread-safe.</param>
        /// <param name="seed">Run <c>i</c> uses <c>Rng.Derive(seed, i)</c>, so a batch is reproducible.</param>
        public static LevelReport Run(LevelDefinition level, Func<IBotPolicy> createPolicy, int runs, int moveCap, ulong seed)
        {
            if (runs < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(runs), runs, "At least one run is needed.");
            }

            var results = new RunResult[runs];
            Parallel.For(
                0,
                runs,
                createPolicy,
                (i, _, policy) =>
                {
                    results[i] = SimulationRunner.Play(level, policy, Rng.Derive(seed, (ulong)i), moveCap);
                    return policy;
                },
                _ => { });
            return LevelReport.From(results, moveCap);
        }
    }
}
