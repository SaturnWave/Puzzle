using System;
using Puzzle.Core.Engine;

namespace Puzzle.Core.Recording
{
    /// <summary>How an attempt ended. Closed set: <see cref="AttemptResults.AtEnd"/> is the only mapping from the engine.</summary>
    public enum AttemptResult
    {
        Won,

        /// <summary>The move limit was reached with goals left.</summary>
        OutOfMoves,

        /// <summary>No legal move existed even after shuffling; a validated level never reaches this.</summary>
        NoMovesPossible,

        /// <summary>The player left before the game was decided: started another attempt or level, or went away.</summary>
        Abandoned,
    }

    public static class AttemptResults
    {
        /// <summary>The result of an attempt that ends while its game has <paramref name="status"/>: a game still being played was abandoned.</summary>
        public static AttemptResult AtEnd(GameStatus status) => status switch
        {
            GameStatus.Won => AttemptResult.Won,
            GameStatus.LostOutOfMoves => AttemptResult.OutOfMoves,
            GameStatus.LostNoMovesPossible => AttemptResult.NoMovesPossible,
            GameStatus.Playing => AttemptResult.Abandoned,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown game status."),
        };
    }
}
