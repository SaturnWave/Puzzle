#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Engine;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// A whole game of a level as a bot once played it to its win, kept so that the same game can be started again
    /// with any count of moves and brought to the move that decides it. A game is decided by its seeds and its
    /// moves and by nothing else, so the same moves play the same game whatever the count of moves is: only where
    /// it ends differs. With more moves than the win took, the game is won with the rest left over, which is how a
    /// finale with a chosen count of moves is made; with fewer, it is lost on its last move.
    /// </summary>
    internal sealed class RehearsedGame
    {
        /// <summary>A bot that has not won by then is the wrong bot for the level.</summary>
        private const int MostMoves = 300;

        private const ulong BotSeed = 20261012;
        private const ulong BotStream = 17;

        private readonly IReadOnlyList<Move> _moves;

        private RehearsedGame(LevelDefinition level, IReadOnlyList<Move> moves)
        {
            Level = level;
            _moves = moves;
        }

        public LevelDefinition Level { get; }

        /// <summary>How many moves the bot's win took.</summary>
        public int MovesToWin => _moves.Count;

        /// <summary>Plays the level's first attempt once, with moves enough for any bot that can win it.</summary>
        /// <exception cref="InvalidOperationException">The bot did not win.</exception>
        public static RehearsedGame Of(LevelDefinition level, IBotPolicy bot)
        {
            Game game = LevelStart.AttemptWithMoves(level, attempt: 0, MostMoves);
            var rng = new Rng(BotSeed, BotStream);
            var moves = new List<Move>();
            while (game.Status == GameStatus.Playing)
            {
                Move move = bot.ChooseMove(game, game.FindLegalMoves(), rng);
                if (!game.TryPlay(move))
                {
                    throw new InvalidOperationException($"The engine refused the bot's move {move} at move {moves.Count + 1} of level {level.Id.Value}.");
                }

                moves.Add(move);
            }

            return game.Status == GameStatus.Won
                ? new RehearsedGame(level, moves)
                : throw new InvalidOperationException($"The bot did not win level {level.Id.Value} within {MostMoves} moves ({game.Status}); pick another bot or level.");
        }

        /// <summary>The move that decides the game when it has so many moves: the winning one if they reach, its last one if they do not.</summary>
        public Move DecidingMoveWith(int moves) => _moves[DecidingMoveNumberWith(moves) - 1];

        /// <summary>
        /// The same game started again with so many moves and played to just before the move that decides it. The
        /// game records its events, so a stage can be opened on it and shown that move.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The game has no move.</exception>
        /// <exception cref="InvalidOperationException">The game did not play again as it was played.</exception>
        public Game BeforeItsDecidingMoveWith(int moves)
        {
            if (moves < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(moves), moves, "A game has a move at least.");
            }

            Game game = LevelStart.AttemptWithMoves(Level, attempt: 0, moves);
            foreach (Move move in _moves.Take(DecidingMoveNumberWith(moves) - 1))
            {
                if (game.Status != GameStatus.Playing || !game.TryPlay(move))
                {
                    throw new InvalidOperationException($"The rehearsed game of level {Level.Id.Value} did not play again as it was played: {move} at move {game.MovesUsed + 1} ({game.Status}).");
                }
            }

            return game.Status == GameStatus.Playing
                ? game
                : throw new InvalidOperationException($"The rehearsed game of level {Level.Id.Value} was {game.Status} a move earlier than when it was first played.");
        }

        private int DecidingMoveNumberWith(int moves) => Math.Min(MovesToWin, moves);
    }

    /// <summary>
    /// A level with its goals cut down, for the Editor's studies of a won board that still holds its obstacles. No
    /// level of the game is won that way: each asks for every crate, tape and slab it has. But a rocket of the
    /// finale has to meet them right all the same, and a later level may ask for fewer than it has.
    /// </summary>
    internal static class EarlyWin
    {
        /// <summary>The same level with every goal's count cut to a part of it, one at least.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The part is not above nothing and up to the whole.</exception>
        public static LevelDefinition Of(LevelDefinition level, float part)
        {
            if (!(part > 0f && part <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(part), part, "Goals are cut to a part of what they were: above 0, up to 1.");
            }

            GoalSpec[] goals = level.Goals.Select(goal => Cut(goal, Math.Max(1, (int)(goal.Target * part)))).ToArray();
            return new LevelDefinition(level.Id, level.Difficulty, level.Layout, goals, level.MoveLimit, level.Spawn, level.Attempts, level.ContentVersion);
        }

        private static GoalSpec Cut(GoalSpec goal, int target) => goal switch
        {
            GoalSpec.CollectColor collect => new GoalSpec.CollectColor(collect.Color, target),
            GoalSpec.BreakBoxes _ => new GoalSpec.BreakBoxes(target),
            GoalSpec.ClearJelly _ => new GoalSpec.ClearJelly(target),
            GoalSpec.BreakChains _ => new GoalSpec.BreakChains(target),
            GoalSpec.DropItems _ => new GoalSpec.DropItems(target),
            _ => throw new InvalidOperationException($"A goal of the kind {goal.GetType().Name} is not known to the studies."),
        };
    }
}
