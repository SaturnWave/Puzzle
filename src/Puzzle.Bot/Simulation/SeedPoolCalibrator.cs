using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;

namespace Puzzle.Bot.Simulation
{
    /// <summary>How hard one attempt seed played for the bot.</summary>
    public sealed class SeedDifficulty
    {
        internal SeedDifficulty(uint seed, int medianMoves, double winRateAtLimit)
        {
            Seed = seed;
            MedianMoves = medianMoves;
            WinRateAtLimit = winRateAtLimit;
        }

        public uint Seed { get; }

        /// <summary>Moves the bot needed in half of its games on this seed (move cap + 1 when it rarely finished).</summary>
        public int MedianMoves { get; }

        public double WinRateAtLimit { get; }
    }

    public sealed class SeedPoolSettings
    {
        public int Candidates { get; init; } = 64;

        /// <summary>Bot games per candidate seed; each uses different bot randomness on the same tiles.</summary>
        public int GamesPerSeed { get; init; } = 64;

        public int PoolSize { get; init; } = 16;

        /// <summary>
        /// A seed joins the pool when its win rate at the level's move limit is within this many percentage points
        /// of the typical seed's: the difficulty a player actually feels.
        /// </summary>
        public int TolerancePoints { get; init; } = 5;

        public int MoveCap { get; init; } = 150;

        public ulong BaseSeed { get; init; } = 1;
    }

    public sealed class SeedPoolReport
    {
        internal SeedPoolReport(IReadOnlyList<SeedDifficulty> candidates, IReadOnlyList<SeedDifficulty> pool, double typicalWinRate, int typicalMedianMoves)
        {
            Candidates = candidates;
            Pool = pool;
            TypicalWinRate = typicalWinRate;
            TypicalMedianMoves = typicalMedianMoves;
        }

        /// <summary>Every candidate, easiest first.</summary>
        public IReadOnlyList<SeedDifficulty> Candidates { get; }

        /// <summary>The selected seeds, closest to typical first.</summary>
        public IReadOnlyList<SeedDifficulty> Pool { get; }

        /// <summary>The win rate pool seeds were chosen around: the requested target, or the median candidate.</summary>
        public double TypicalWinRate { get; }

        public int TypicalMedianMoves { get; }

        /// <summary>The candidate closest to typical (by win rate, then by median moves).</summary>
        public SeedDifficulty Typical => Candidates
            .OrderBy(c => Math.Abs(c.WinRateAtLimit - TypicalWinRate))
            .ThenBy(c => Math.Abs(c.MedianMoves - TypicalMedianMoves))
            .ThenBy(c => c.Seed)
            .First();
    }

    /// <summary>
    /// Builds a seed pool for "different patterns, same difficulty": random seeds differ a lot in how hard they
    /// play, so each candidate is measured with many bot games and only seeds near the typical difficulty are
    /// kept. Attempts then cycle through the pool.
    /// </summary>
    public static class SeedPoolCalibrator
    {
        /// <param name="createPolicy">Called once per worker thread (policies are not thread-safe).</param>
        /// <param name="targetWinRate">
        /// The win rate pool seeds should play at — normally the level's calibrated win rate. Absent means the
        /// median candidate, which on hard levels can be far from the average because seed difficulty is skewed.
        /// </param>
        public static SeedPoolReport Calibrate(LevelDefinition level, StartBoard board, Func<IBotPolicy> createPolicy, SeedPoolSettings settings, double? targetWinRate = null)
        {
            uint[] candidates = CandidateSeeds(level, settings);
            var attempts = new AttemptPolicy { Board = board };
            int games = settings.GamesPerSeed;
            var moves = new int[candidates.Length * games];
            Parallel.For(
                0,
                moves.Length,
                createPolicy,
                (i, _, policy) =>
                {
                    uint seed = candidates[i / games];
                    RunResult run = SimulationRunner.Play(level, policy, attempts.SeedsForPlaySeed(seed), Rng.Derive(seed, (ulong)(i % games)), settings.MoveCap);
                    moves[i] = run.CompletedAtMove ?? settings.MoveCap + 1;
                    return policy;
                },
                _ => { });

            SeedDifficulty[] measured = candidates
                .Select((seed, c) => Measure(seed, new ArraySegment<int>(moves, c * games, games), level.MoveLimit))
                .OrderBy(d => d.MedianMoves)
                .ThenBy(d => d.Seed)
                .ToArray();
            int typicalMoves = measured[measured.Length / 2].MedianMoves;
            double typicalWinRate = targetWinRate ?? measured.Select(d => d.WinRateAtLimit).OrderBy(w => w).ElementAt(measured.Length / 2);
            double tolerance = settings.TolerancePoints / 100.0;
            SeedDifficulty[] pool = measured
                .Where(d => Math.Abs(d.WinRateAtLimit - typicalWinRate) <= tolerance + 1e-9)
                .OrderBy(d => Math.Abs(d.WinRateAtLimit - typicalWinRate))
                .ThenBy(d => Math.Abs(d.MedianMoves - typicalMoves))
                .ThenBy(d => d.Seed)
                .Take(settings.PoolSize)
                .ToArray();
            return new SeedPoolReport(measured, pool, typicalWinRate, typicalMoves);
        }

        private static uint[] CandidateSeeds(LevelDefinition level, SeedPoolSettings settings)
        {
            var seeds = new HashSet<uint>();
            ulong levelSeed = Rng.Derive(settings.BaseSeed, (ulong)level.Id.Value);
            for (ulong i = 0; seeds.Count < settings.Candidates; i++)
            {
                seeds.Add((uint)Rng.Derive(levelSeed, i));
            }

            return seeds.ToArray();
        }

        private static SeedDifficulty Measure(uint seed, ArraySegment<int> moves, int moveLimit)
        {
            int[] sorted = moves.OrderBy(m => m).ToArray();
            double winRate = sorted.Count(m => m <= moveLimit) / (double)sorted.Length;
            return new SeedDifficulty(seed, sorted[sorted.Length / 2], winRate);
        }
    }
}
