using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;

namespace Puzzle.Bot
{
    /// <summary>
    /// The game's hint: the move a player model of the bot family plays on the board as the player sees it
    /// (docs/GAME_DESIGN.md §0: the hint is the bot). Which model is a setting; a weaker one helps less, so the hint
    /// can help a player without playing the level for them.
    /// </summary>
    /// <remarks>
    /// The move is thought out on a copy of the game whose refills are hidden and whose random choices come from a seed
    /// of the hint's own (<see cref="Game.CreateLookahead"/>): asking for a hint never draws from, advances or reads
    /// ahead in the game's own streams, so a game in which hints were shown plays on exactly as one in which none was.
    /// The seed follows from the attempt's play seed and the number of the player's actions so far, so the same attempt
    /// at the same point gives the same hint every time it is asked. One advisor keeps its bot's scratch buffers: give
    /// every thread its own.
    /// </remarks>
    public sealed class HintAdvisor
    {
        /// <summary>The salt that sets the hints' own stream of seeds apart from the attempt's other streams.</summary>
        private const ulong HintStream = 0x48_49_4E_54; // "HINT"

        /// <summary>The salt that sets the tutorial's moves apart from the hints at the same point of the same attempt.</summary>
        private const ulong LessonStream = 0x4C_45_53_53_4F_4E; // "LESSON"

        private readonly IBotPolicy _bot;
        private readonly List<Move> _legalMoves = new List<Move>();
        private readonly List<Move> _lessonMoves = new List<Move>();

        private HintAdvisor(IBotPolicy bot, string botName)
        {
            _bot = bot;
            BotName = botName;
        }

        /// <summary>The name of the player model whose move the hint shows, as it was given.</summary>
        public string BotName { get; }

        /// <summary>
        /// The advisor whose hints are the moves of the player model <paramref name="botName"/> names
        /// (case-insensitive): the random bot, a named skill profile or a rung of the skill ladder
        /// (<see cref="BotPolicies.IsPlayerModel"/>). A measurement bot is not one: it is too slow for a phone and plays
        /// no player.
        /// </summary>
        /// <returns>False, with no advisor, for any other name and for a missing one.</returns>
        public static bool TryCreate(string? botName, [NotNullWhen(true)] out HintAdvisor? advisor)
        {
            if (!BotPolicies.IsPlayerModel(botName) || !BotPolicies.TryCreate(botName, out IBotPolicy bot))
            {
                advisor = null;
                return false;
            }

            advisor = new HintAdvisor(bot, botName!);
            return true;
        }

        /// <summary>
        /// The move the hint shows on <paramref name="game"/> as it stands: always one the game would accept now.
        /// Nothing for a game that is no longer being played, and for a board without a legal move.
        /// </summary>
        /// <param name="attemptSeed">The attempt's play seed (AttemptSeeds.Play): with the game's actions so far it seeds the hint's own choices.</param>
        public Move? MoveFor(Game game, ulong attemptSeed)
        {
            if (game.Status != GameStatus.Playing)
            {
                return null;
            }

            var rng = new Rng(SeedAt(game, attemptSeed));
            Game copy = game.CreateLookahead(rng.NextULong());
            _legalMoves.Clear();
            copy.CollectLegalMoves(_legalMoves);
            return _legalMoves.Count == 0 ? (Move?)null : _bot.ChooseMove(copy, _legalMoves, rng);
        }

        /// <summary>
        /// The move the tutorial shows by hand to teach a lesson on <paramref name="game"/> as it stands: of the moves
        /// that teach it (<see cref="MoveLessons.Teaches"/>), the one the advisor's bot plays when those are all it is
        /// offered, thought out on a copy as a hint is. Nothing while the board offers no move that teaches it, and for
        /// a game that is no longer being played. The same game, seed and lesson give the same move every time.
        /// </summary>
        /// <param name="attemptSeed">The attempt's play seed (AttemptSeeds.Play), as for <see cref="MoveFor(Game, ulong)"/>.</param>
        public Move? MoveFor(Game game, ulong attemptSeed, MoveLesson lesson)
        {
            if (game.Status != GameStatus.Playing)
            {
                return null;
            }

            var rng = new Rng(Rng.Derive(SeedAt(game, attemptSeed), LessonStream));
            Game copy = game.CreateLookahead(rng.NextULong());
            ulong lessonSeed = rng.NextULong();
            _legalMoves.Clear();
            copy.CollectLegalMoves(_legalMoves);
            _lessonMoves.Clear();
            foreach (Move move in _legalMoves)
            {
                if (MoveLessons.Teaches(copy, move, lesson, lessonSeed))
                {
                    _lessonMoves.Add(move);
                }
            }

            return _lessonMoves.Count == 0 ? (Move?)null : _bot.ChooseMove(copy, _lessonMoves, rng);
        }

        /// <summary>The hints' own seed for the game at this point of the attempt: its play seed and the number of the player's actions so far.</summary>
        private static ulong SeedAt(Game game, ulong attemptSeed) => Rng.Derive(Rng.Derive(attemptSeed, HintStream), (ulong)game.Actions.Count);
    }
}
