#nullable enable
using System.Collections.Generic;
using System.Linq;
using Puzzle.Meta;
using Puzzle.Museum;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// Where the tutorial's steps may come (docs/GAME_DESIGN.md §19). The three steps of the first level, the four and
    /// the five belong to their own level: a step shown by hand waits there for a board that offers its move, and
    /// when none does it waits on the next levels too, at most <see cref="LevelsAfterItsOwn"/> of them; after that it
    /// is let go unseen. An obstacle's step comes on the first board the player meets with that obstacle, whatever
    /// its level. A booster used during play is used once by hand on the level it opens on (the economy's
    /// <see cref="BoosterUnlocks"/>) and on no other: a later level never asks for it again. The jigsaw's steps come on
    /// the first jigsaw a piece waits on and with the first painting made whole; the start boosters' step on the card
    /// before a level the player tried and did not win (<see cref="IntroducesTheStartBoosters"/>). No Unity dependency.
    /// </summary>
    public static class TutorialPlan
    {
        /// <summary>A step shown by hand waits for its move on its own level and on this many levels after it.</summary>
        public const int LevelsAfterItsOwn = 3;

        /// <summary>The level each step of the first levels belongs to.</summary>
        private static readonly IReadOnlyDictionary<TutorialStep, int> OwnLevels = new Dictionary<TutorialStep, int>
        {
            [TutorialStep.Goal] = 1,
            [TutorialStep.Match] = 1,
            [TutorialStep.Moves] = 1,
            [TutorialStep.FourInALine] = 2,
            [TutorialStep.FiveInALine] = 3,
        };

        /// <summary>
        /// The first level of the game's levels with each obstacle (docs/GAME_DESIGN.md §9, the mechanics' schedule):
        /// only a save of a layout before the tutorial is read by it (<see cref="OfAnOlderSave"/>). The tests hold it
        /// to the level files.
        /// </summary>
        public static IReadOnlyDictionary<TutorialStep, int> FirstLevelWith { get; } = new Dictionary<TutorialStep, int>
        {
            [TutorialStep.Box] = 11,
            [TutorialStep.Jelly] = 21,
            [TutorialStep.Chain] = 41,
            [TutorialStep.Item] = 51,
        };

        /// <summary>The steps a board of a level may show to a player who has seen these, in the order they are shown.</summary>
        /// <param name="level">The level's number, from 1.</param>
        /// <param name="unlocks">Where the boosters used during play open: each one's step comes on that level.</param>
        public static IReadOnlyList<TutorialStep> MayComeOn(int level, TutorialSeen seen, BoosterUnlocks unlocks) =>
            TutorialSteps.All.Where(step => step.IsOnTheBoard() && !seen.Has(step) && MayComeOnLevel(step, level, unlocks)).ToArray();

        /// <summary>
        /// True when the card before a level shows the start boosters one after another: the player has not seen
        /// them yet, and has tried this level before without winning it (lost it, or left it after a move), so the
        /// card comes down for another try.
        /// </summary>
        /// <param name="attemptsOnLevel">How many attempts at the level the player stands on have begun.</param>
        public static bool IntroducesTheStartBoosters(TutorialSeen seen, int attemptsOnLevel) =>
            !seen.Has(TutorialStep.StartBoosters) && attemptsOnLevel > 0;

        /// <summary>
        /// What a player whose save is of a layout before the tutorial has seen: every step of a level they have won
        /// (an obstacle's level is the first that has it), the first piece once a piece is in place, the first painting
        /// once one is whole. A player past a step's level has met what it teaches; one who is not sees it there. The
        /// steps of the booster intro, which are younger than every such save, are not seen: a booster's step comes on
        /// the level it opens on only, so a player past that level never meets it, and the start boosters' step comes
        /// once, after the next loss.
        /// </summary>
        public static TutorialSeen OfAnOlderSave(MuseumProgress museum) =>
            TutorialSeen.Of(TutorialSteps.All.Where(step => step switch
            {
                TutorialStep.FirstPiece => museum.PiecesPlaced > 0,
                TutorialStep.FirstPainting => museum.PaintingsCompleted > 0,
                TutorialStep.StartBoosters => false,
                _ when step.BoosterOf() != null => false,
                _ => museum.LevelsWon >= (OwnLevels.TryGetValue(step, out int own) ? own : FirstLevelWith[step]),
            }));

        private static bool MayComeOnLevel(TutorialStep step, int level, BoosterUnlocks unlocks)
        {
            if (step.BoosterOf() is BoosterKind booster)
            {
                return level == unlocks.LevelOf(booster);
            }

            return !OwnLevels.TryGetValue(step, out int own) || (level >= own && level <= own + LevelsAfterItsOwn);
        }
    }
}
