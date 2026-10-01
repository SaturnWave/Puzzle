using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;

namespace Puzzle.Bot.Simulation
{
    /// <summary>
    /// Statistics over many uncapped bot runs of one level. Because the bot ignores the moves left, a run that
    /// finished on move k is a win for every move limit ≥ k — so one batch yields the whole win-rate curve.
    /// </summary>
    public sealed class LevelReport
    {
        private const int NotCompleted = int.MaxValue;

        private readonly IReadOnlyList<RunResult> _runs;
        private readonly int[] _sortedCompletion;

        private LevelReport(IReadOnlyList<RunResult> runs, int moveCap)
        {
            if (runs.Count == 0)
            {
                throw new ArgumentException("A report needs at least one run.", nameof(runs));
            }

            _runs = runs;
            MoveCap = moveCap;
            _sortedCompletion = runs.Select(r => r.CompletedAtMove ?? NotCompleted).OrderBy(m => m).ToArray();
        }

        public int Runs => _runs.Count;

        public int MoveCap { get; }

        /// <summary>Share of runs that met every goal within the move cap.</summary>
        public double CompletionRate => WinRateAt(MoveCap);

        /// <summary>Share of runs that ended with no legal move even after shuffling (a broken level).</summary>
        public double StuckRate => _runs.Count(r => r.FinalStatus == GameStatus.LostNoMovesPossible) / (double)Runs;

        public double AverageShuffles => _runs.Average(r => r.Totals.Shuffles);

        public double AverageSpecialsCreated => _runs.Average(r => r.Totals.SpecialsCreated);

        /// <summary>Moves of every run that finished within the cap, ascending.</summary>
        public IReadOnlyList<int> CompletionMoves => _sortedCompletion.Where(m => m != NotCompleted).ToArray();

        public double AverageCascadesPerMove => _runs.Sum(r => r.Totals.Cascades) / (double)Math.Max(1, _runs.Sum(r => r.MovesUsed));

        public static LevelReport From(IReadOnlyList<RunResult> runs, int moveCap) => new LevelReport(runs, moveCap);

        public double WinRateAt(int moveLimit)
        {
            int wins = UpperBound(moveLimit);
            return wins / (double)Runs;
        }

        /// <summary>Lost at this limit but had reached <paramref name="threshold"/> of the goals: the "one more move" feeling.</summary>
        public double NearMissRateAt(int moveLimit, double threshold = 0.8) =>
            _runs.Count(r => !WonWithin(r, moveLimit) && r.ProgressAt(moveLimit) >= threshold) / (double)Runs;

        /// <summary>Moves needed by the given share of runs, or absent when that share never finished.</summary>
        public int? MovesNeeded(double share)
        {
            int index = Math.Max(0, (int)Math.Ceiling(share * Runs) - 1);
            int moves = _sortedCompletion[Math.Min(index, Runs - 1)];
            return moves == NotCompleted ? (int?)null : moves;
        }

        /// <summary>The smallest move limit whose win rate reaches <paramref name="targetWinRate"/>.</summary>
        public int? MoveLimitFor(double targetWinRate) => MovesNeeded(targetWinRate);

        private static bool WonWithin(RunResult run, int moveLimit) => run.CompletedAtMove is int moves && moves <= moveLimit;

        /// <summary>Number of completion values ≤ <paramref name="moveLimit"/>.</summary>
        private int UpperBound(int moveLimit)
        {
            int low = 0;
            int high = _sortedCompletion.Length;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (_sortedCompletion[mid] <= moveLimit)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }
    }
}
