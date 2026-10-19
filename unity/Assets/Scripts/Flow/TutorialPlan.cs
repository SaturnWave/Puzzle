#nullable enable
using System.Collections.Generic;
using System.Linq;
using Puzzle.Museum;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// Where the tutorial's steps may come (docs/GAME_DESIGN.md §19). The three steps of the first level, the four
    /// and the five belong to their own level: a step shown by hand waits there for a board that offers its move, and
    /// when none does it waits on the next levels too, at most <see cref="LevelsAfterItsOwn"/> of them; after that it
    /// is let go unseen. An obstacle's step comes on the first board the player meets with that obstacle, whatever
    /// its level. The jigsaw's steps come on the first jigsaw a piece waits on and with the first painting made whole.
    /// No Unity dependency.
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
        public static IReadOnlyList<TutorialStep> MayComeOn(int level, TutorialSeen seen) =>
            TutorialSteps.All.Where(step => step.IsOnTheBoard() && !seen.Has(step) && MayComeOnLevel(step, level)).ToArray();

        /// <summary>
        /// What a player whose save is of a layout before the tutorial has seen: every step of a level they have won
        /// (an obstacle's level is the first that has it), the first piece once a piece is in place, the first painting
        /// once one is whole. A player past a step's level has met what it teaches; one who is not sees it there.
        /// </summary>
        public static TutorialSeen OfAnOlderSave(MuseumProgress museum) =>
            TutorialSeen.Of(TutorialSteps.All.Where(step => step switch
            {
                TutorialStep.FirstPiece => museum.PiecesPlaced > 0,
                TutorialStep.FirstPainting => museum.PaintingsCompleted > 0,
                _ => museum.LevelsWon >= (OwnLevels.TryGetValue(step, out int own) ? own : FirstLevelWith[step]),
            }));

        private static bool MayComeOnLevel(TutorialStep step, int level) =>
            !OwnLevels.TryGetValue(step, out int own) || (level >= own && level <= own + LevelsAfterItsOwn);
    }
}
