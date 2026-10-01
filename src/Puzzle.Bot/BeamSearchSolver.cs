using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;

namespace Puzzle.Bot
{
    /// <summary>The best line a search found from some position.</summary>
    public sealed class SolveResult
    {
        internal SolveResult(IReadOnlyList<Move>? path, int positionsExplored)
        {
            Path = path ?? System.Array.Empty<Move>();
            Solved = path is not null;
            PositionsExplored = positionsExplored;
        }

        public bool Solved { get; }

        /// <summary>The winning moves in order; empty when no win was found.</summary>
        public IReadOnlyList<Move> Path { get; }

        /// <summary>Moves needed from the searched position; absent when no win was found.</summary>
        public int? MovesNeeded => Solved ? Path.Count : (int?)null;

        public int PositionsExplored { get; }
    }

    /// <summary>
    /// Perfect-information beam search: it plays on exact replicas, so it knows every future refill — the
    /// "always the right move" player. Its result is a near-optimal lower bound on the moves a level needs for
    /// one seed; comparing it with the human-like bot gives the level's margin for error.
    /// </summary>
    /// <remarks>
    /// A full depth-first search is hopeless (the game is NP-hard and branches ~30 ways per move), so each
    /// depth keeps only the <see cref="BeamWidth"/> most promising positions. Width 1 is a greedy player that
    /// can see the future; larger widths approach the true optimum.
    /// </remarks>
    public sealed class BeamSearchSolver
    {
        private readonly MoveEvaluator _evaluator;

        public BeamSearchSolver(int beamWidth, MoveEvaluator? evaluator = null)
        {
            BeamWidth = beamWidth >= 1
                ? beamWidth
                : throw new System.ArgumentOutOfRangeException(nameof(beamWidth), beamWidth, "Beam width must be at least 1.");
            _evaluator = evaluator ?? MoveEvaluator.Default;
        }

        public int BeamWidth { get; }

        /// <summary>Searches from the game's current position without touching the game itself.</summary>
        /// <param name="maxMoves">Search depth; the game's own move limit still applies.</param>
        public SolveResult Solve(Game game, int maxMoves)
        {
            if (game.Status != GameStatus.Playing)
            {
                return new SolveResult(null, 0);
            }

            var frontier = new List<Node> { new Node(game.CreateReplica(), null, default, 0) };
            var seen = new HashSet<ulong>();
            var buffer = new List<Move>();
            int explored = 0;
            for (int depth = 0; depth < maxMoves && frontier.Count > 0; depth++)
            {
                var children = new List<Node>();
                foreach (Node node in frontier)
                {
                    buffer.Clear();
                    node.Game.CollectLegalMoves(buffer);
                    foreach (Move move in buffer)
                    {
                        Game child = node.Game.CreateReplica();
                        child.TryPlay(move);
                        explored++;
                        if (child.Status == GameStatus.Won)
                        {
                            return new SolveResult(new Node(child, node, move, 0).Path(), explored);
                        }

                        if (child.Status == GameStatus.Playing && seen.Add(PositionKey(child)))
                        {
                            children.Add(new Node(child, node, move, _evaluator.ValueOf(child)));
                        }
                    }
                }

                frontier = children.OrderByDescending(c => c.Value).Take(BeamWidth).ToList();
            }

            return new SolveResult(null, explored);
        }

        /// <summary>Board plus goal counters; equal keys are treated as the same position.</summary>
        private static ulong PositionKey(Game game)
        {
            ulong key = game.Board.ComputeHash();
            for (int i = 0; i < game.Goals.Count; i++)
            {
                key = Rng.Derive(key, (ulong)game.Goals[i].Done);
            }

            return key;
        }

        /// <summary>A searched position; the move list is recovered by walking the parent links.</summary>
        private sealed class Node
        {
            public Node(Game game, Node? parent, Move move, double value)
            {
                Game = game;
                Parent = parent;
                Move = move;
                Value = value;
            }

            public Game Game { get; }

            public Node? Parent { get; }

            public Move Move { get; }

            public double Value { get; }

            public List<Move> Path()
            {
                var path = new List<Move>();
                for (Node? node = this; node?.Parent is not null; node = node.Parent)
                {
                    path.Add(node.Move);
                }

                path.Reverse();
                return path;
            }
        }
    }
}
