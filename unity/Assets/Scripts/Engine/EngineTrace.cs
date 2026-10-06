using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// Plays a level to its end with the random bot and writes down what happened in one line. The engine must give
    /// the same line on every runtime (the tools' .NET, the Editor's Mono, IL2CPP on a phone): the levels were
    /// calibrated on one and are played on another. Only whole-number arithmetic is on this path; the random bot draws
    /// integers. The file has no Unity dependency, so the repository's tests compile it too and pin the lines.
    /// </summary>
    public static class EngineTrace
    {
        private static readonly ulong[] PinnedSeeds = { 20261007, 7, 123456789 };

        private const ulong BotStream = 7;
        private const ulong HashPrime = 1099511628211UL;

        /// <summary>
        /// The pinned trace: every hand-made sample level (levels/samples, in file order) under three seeds. The samples
        /// cover a plain board, boxes, jelly on a shaped board, and items with chains, and they do not change when the
        /// game's levels are generated again.
        /// </summary>
        public static string[] OfSamples(IEnumerable<LevelDefinition> sampleLevels) =>
            sampleLevels.SelectMany(level => PinnedSeeds.Select(seed => Of(level, seed))).ToArray();

        public static string Of(LevelDefinition level, ulong seed)
        {
            Game game = Game.Start(level, seed);
            var bot = new RandomPolicy();
            var rng = new Rng(seed, BotStream);
            var legalMoves = new List<Move>();
            ulong boards = 0;
            while (game.Status == GameStatus.Playing)
            {
                legalMoves.Clear();
                game.CollectLegalMoves(legalMoves);
                game.TryPlay(bot.ChooseMove(game, legalMoves, rng));
                boards = unchecked((boards * HashPrime) ^ game.Board.ComputeHash());
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "level {0} seed {1}: {2} after {3} of {4} moves, boards {5:x16}",
                level.Id, seed, game.Status, game.MovesUsed, game.MoveLimit, boards);
        }
    }
}
