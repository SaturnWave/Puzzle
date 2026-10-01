using System.Collections.Generic;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;

namespace Puzzle.Bot
{
    /// <summary>
    /// Picks the next move. Implementations keep scratch buffers, so give every simulation thread its own
    /// instance. A policy must not depend on the moves left: the calibration plays with an unlimited budget
    /// and reads the win rate for every move limit from one batch of games.
    /// </summary>
    public interface IBotPolicy
    {
        string Name { get; }

        /// <param name="legalMoves">Non-empty; the game is still playing.</param>
        Move ChooseMove(Game game, IReadOnlyList<Move> legalMoves, Rng rng);
    }

    /// <summary>Uniformly random legal move: the floor every real policy must beat.</summary>
    public sealed class RandomPolicy : IBotPolicy
    {
        public string Name => "random";

        public Move ChooseMove(Game game, IReadOnlyList<Move> legalMoves, Rng rng) => legalMoves[rng.NextInt(legalMoves.Count)];
    }
}
