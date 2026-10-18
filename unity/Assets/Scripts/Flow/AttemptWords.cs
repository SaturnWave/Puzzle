using System.Globalization;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>What the game says about an attempt while it is played, in the language the game speaks. No Unity dependency.</summary>
    public static class AttemptWords
    {
        /// <summary>How the game is played: the line of a first try.</summary>
        public static string HowToPlay => Speech.Say(WordId.ShelfHowToPlay);

        /// <summary>
        /// The line on the shelf under the board. A first try is told how to play. A later try is told which try it
        /// is, and that its board is another one: a level deals a new board of the same difficulty every time.
        /// </summary>
        public static string OnTheShelf(AttemptBrief brief) => brief.AttemptNumber == 1
            ? HowToPlay
            : Speech.Say(WordId.ShelfLaterAttempt, ("attempt", brief.AttemptNumber.ToString(CultureInfo.InvariantCulture)));
    }
}
