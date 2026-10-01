using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Core.Engine;

namespace Puzzle.Bot
{
    /// <summary>Outcome of trying every move sequence up to a depth.</summary>
    public sealed class ExhaustiveResult
    {
        internal ExhaustiveResult(bool winFound, long positionsExamined)
        {
            WinFound = winFound;
            PositionsExamined = positionsExamined;
        }

        /// <summary>Some sequence within the depth wins. False is a proof that none does (for this seed).</summary>
        public bool WinFound { get; }

        public long PositionsExamined { get; }
    }

    /// <summary>
    /// Depth-first search over every legal move sequence, on exact replicas (future refills known). It can prove
    /// that no shorter win exists — the only way to know a line is truly optimal — but its cost is roughly
    /// branching^depth, which is why it is only usable for very short depths.
    /// </summary>
    public static class ExhaustiveSearch
    {
        /// <summary>Whether any sequence of at most <paramref name="depth"/> moves wins from the game's position.</summary>
        public static ExhaustiveResult FindWinWithin(Game game, int depth)
        {
            if (depth <= 0 || game.Status != GameStatus.Playing)
            {
                return new ExhaustiveResult(false, 0);
            }

            Game root = game.CreateReplica();
            IReadOnlyList<Move> firstMoves = root.FindLegalMoves();
            long examined = 0;
            int found = 0;
            Parallel.ForEach(firstMoves, (move, loop) =>
            {
                long local = 0;
                Game child = root.CreateReplica();
                child.TryPlay(move);
                local++;
                bool wins = child.Status == GameStatus.Won
                    || (child.Status == GameStatus.Playing && Search(child, depth - 1, ref local, ref found));
                Interlocked.Add(ref examined, local);
                if (wins)
                {
                    Interlocked.Exchange(ref found, 1);
                    loop.Stop();
                }
            });

            return new ExhaustiveResult(found == 1, examined);
        }

        private static bool Search(Game game, int depthLeft, ref long examined, ref int foundElsewhere)
        {
            if (depthLeft == 0 || Volatile.Read(ref foundElsewhere) == 1)
            {
                return false;
            }

            foreach (Move move in game.FindLegalMoves())
            {
                Game child = game.CreateReplica();
                child.TryPlay(move);
                examined++;
                if (child.Status == GameStatus.Won)
                {
                    return true;
                }

                if (child.Status == GameStatus.Playing && Search(child, depthLeft - 1, ref examined, ref foundElsewhere))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
