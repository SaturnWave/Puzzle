#nullable enable
using System;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the tutorial's pill says at each step (docs/GAME_DESIGN.md §19): what to look at or do, and why, short and
    /// plain; a step that a touch ends says so in a small last line. The words are the tables' (words/&lt;code&gt;.json).
    /// No Unity dependency.
    /// </summary>
    public static class TutorialWords
    {
        /// <summary>The pill of a step.</summary>
        /// <param name="asksForATouch">True for a step that is only looked at, which a touch anywhere ends.</param>
        public static PillWords.Lesson Of(TutorialStep step, bool asksForATouch)
        {
            (WordId says, WordId why) = IdsOf(step);
            return new PillWords.Lesson(Speech.Say(says), Speech.Say(why), asksForATouch ? Speech.Say(WordId.TutorialTouchToGoOn) : null);
        }

        private static (WordId Says, WordId Why) IdsOf(TutorialStep step) => step switch
        {
            TutorialStep.Goal => (WordId.TutorialGoal, WordId.TutorialGoalMore),
            TutorialStep.Match => (WordId.TutorialMatch, WordId.TutorialMatchMore),
            TutorialStep.Moves => (WordId.TutorialMoves, WordId.TutorialMovesMore),
            TutorialStep.FourInALine => (WordId.TutorialFour, WordId.TutorialFourMore),
            TutorialStep.FiveInALine => (WordId.TutorialFive, WordId.TutorialFiveMore),
            TutorialStep.Box => (WordId.TutorialBox, WordId.TutorialBoxMore),
            TutorialStep.Jelly => (WordId.TutorialJelly, WordId.TutorialJellyMore),
            TutorialStep.Chain => (WordId.TutorialChain, WordId.TutorialChainMore),
            TutorialStep.Item => (WordId.TutorialItem, WordId.TutorialItemMore),
            TutorialStep.FirstPiece => (WordId.TutorialFirstPiece, WordId.TutorialFirstPieceMore),
            TutorialStep.FirstPainting => (WordId.TutorialFirstPainting, WordId.TutorialFirstPaintingMore),
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "A step the tutorial does not have."),
        };
    }
}
