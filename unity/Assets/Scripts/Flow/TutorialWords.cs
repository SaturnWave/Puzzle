#nullable enable
using System;
using System.Globalization;
using Puzzle.Client.Words;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the tutorial's pill says at each step (docs/GAME_DESIGN.md §19): what to look at or do, and why, short and
    /// plain; a step that a touch ends says so in a small last line, a booster used by hand that it is free (beside the
    /// spatula's target, that every level can be won without boosters). The words are the tables' (words/&lt;code&gt;.json).
    /// No Unity dependency.
    /// </summary>
    public static class TutorialWords
    {
        /// <summary>The pill of a step that is looked at or of a move shown by hand.</summary>
        /// <param name="asksForATouch">True for a step that is only looked at, which a touch anywhere ends.</param>
        /// <exception cref="ArgumentOutOfRangeException">The step is a booster's, said by <see cref="OfTheBooster"/> and <see cref="OfTheStartBooster"/>.</exception>
        public static PillWords.Lesson Of(TutorialStep step, bool asksForATouch)
        {
            (WordId says, WordId? why) = IdsOf(step);
            return new PillWords.Lesson(
                Speech.Say(says), why is WordId more ? Speech.Say(more) : null, asksForATouch ? Speech.Say(WordId.TutorialTouchToGoOn) : null);
        }

        /// <summary>
        /// The pill of a booster used during play, used once by hand: before its disc is pressed, that it is new and
        /// that the disc is to be pressed; once it is armed, which lit cell, row or column to touch. Under it what the
        /// booster does, and that this use is free; beside the armed spatula the honest line that every level can be
        /// won without boosters, in its place.
        /// </summary>
        /// <param name="isArmed">True once the disc was pressed and the booster waits for its target.</param>
        /// <exception cref="ArgumentException">The booster is taken before a level.</exception>
        public static PillWords.Lesson OfTheBooster(BoosterKind booster, bool isArmed)
        {
            string says = Speech.Say(isArmed ? PickTheTargetOf(booster) : NewIdOf(booster));
            string aside = Speech.Say(isArmed && booster == BoosterKind.HitCell ? WordId.BoosterEveryLevelWithout : WordId.TutorialBoosterFree);
            return new PillWords.Lesson(says, BoosterWords.OnTheCard(booster), aside);
        }

        /// <summary>The pill of a start booster lit on the card before a level: its name, one sentence, and that a touch goes on.</summary>
        /// <param name="extraMoves">The moves the start booster of extra moves adds.</param>
        /// <exception cref="ArgumentException">The booster is used during play.</exception>
        public static PillWords.Lesson OfTheStartBooster(BoosterKind booster, int extraMoves)
        {
            string sentence = booster switch
            {
                BoosterKind.StartRocketAndBomb => Speech.Say(WordId.TutorialStartRocketAndBomb),
                BoosterKind.StartColorBomb => Speech.Say(WordId.TutorialStartColorBomb),
                BoosterKind.StartExtraMoves => Speech.SayCounted(WordId.TutorialStartExtraMoves, extraMoves, ("moves", extraMoves.ToString(CultureInfo.InvariantCulture))),
                _ => throw new ArgumentException($"{booster.Name()} is used during play; the card before a level does not show it.", nameof(booster)),
            };
            return new PillWords.Lesson(LevelStartWords.NameOf(booster, extraMoves), sentence, Speech.Say(WordId.TutorialTouchToGoOn));
        }

        private static WordId NewIdOf(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell => WordId.TutorialBoosterHitCell,
            BoosterKind.HitRow => WordId.TutorialBoosterHitRow,
            BoosterKind.HitColumn => WordId.TutorialBoosterHitColumn,
            BoosterKind.Shuffle => WordId.TutorialBoosterShuffle,
            _ => throw new ArgumentException($"{booster.Name()} is taken before a level; it is not used by hand on a board.", nameof(booster)),
        };

        private static WordId PickTheTargetOf(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell => WordId.TutorialBoosterPickTheCell,
            BoosterKind.HitRow => WordId.TutorialBoosterPickTheRow,
            BoosterKind.HitColumn => WordId.TutorialBoosterPickTheColumn,
            _ => throw new ArgumentException($"{booster.Name()} is not aimed: it is used the moment its disc is pressed.", nameof(booster)),
        };

        /// <summary>What a step says and why; nothing for the why of a step whose one sentence says it all (the long press's).</summary>
        private static (WordId Says, WordId? Why) IdsOf(TutorialStep step) => step switch
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
            TutorialStep.LongPress => (WordId.TutorialLongPress, null),
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "A step of the booster intro: OfTheBooster or OfTheStartBooster says it."),
        };
    }
}
