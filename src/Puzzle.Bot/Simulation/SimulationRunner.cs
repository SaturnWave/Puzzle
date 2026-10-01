using System;
using System.Collections.Generic;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;

namespace Puzzle.Bot.Simulation
{
    /// <summary>The record of one bot game.</summary>
    public sealed class RunResult
    {
        internal RunResult(ulong seed, GameStatus finalStatus, int movesUsed, int? completedAtMove, float[] progressAfterMove, PlayStats totals)
        {
            Seed = seed;
            FinalStatus = finalStatus;
            MovesUsed = movesUsed;
            CompletedAtMove = completedAtMove;
            ProgressAfterMove = progressAfterMove;
            Totals = totals;
        }

        public ulong Seed { get; }

        public GameStatus FinalStatus { get; }

        public int MovesUsed { get; }

        /// <summary>The move on which every goal was met; absent when the run never finished.</summary>
        public int? CompletedAtMove { get; }

        /// <summary>Overall goal progress (0..1); index 0 is the starting board, index k is after move k.</summary>
        public IReadOnlyList<float> ProgressAfterMove { get; }

        public PlayStats Totals { get; }

        /// <summary>Goal progress when <paramref name="moves"/> moves had been played (the last value if the run ended earlier).</summary>
        public float ProgressAt(int moves) => ProgressAfterMove[Math.Min(moves, ProgressAfterMove.Count - 1)];
    }

    public static class SimulationRunner
    {
        /// <summary>
        /// Plays one game with the level's real rules but a move budget of <paramref name="moveCap"/>, so the
        /// result tells how many moves the bot needed rather than whether it fit the level's limit.
        /// </summary>
        /// <exception cref="LevelInitializationException">The level cannot produce a starting board.</exception>
        public static RunResult Play(LevelDefinition level, IBotPolicy policy, ulong seed, int moveCap)
        {
            Game game = Game.Start(level, Rng.Derive(seed, 1), new GameOptions { MoveLimitOverride = moveCap });
            var rng = new Rng(Rng.Derive(seed, 2));
            var legalMoves = new List<Move>();
            var progress = new List<float>(moveCap + 1) { (float)game.Goals.OverallProgress };

            while (game.Status == GameStatus.Playing)
            {
                legalMoves.Clear();
                game.CollectLegalMoves(legalMoves);
                Move move = policy.ChooseMove(game, legalMoves, rng);
                if (!game.TryPlay(move))
                {
                    throw new InvalidOperationException($"{policy.Name} chose illegal move {move}.");
                }

                progress.Add((float)game.Goals.OverallProgress);
            }

            int? completedAt = game.Status == GameStatus.Won ? game.MovesUsed : (int?)null;
            return new RunResult(seed, game.Status, game.MovesUsed, completedAt, progress.ToArray(), game.TotalStats);
        }
    }
}
