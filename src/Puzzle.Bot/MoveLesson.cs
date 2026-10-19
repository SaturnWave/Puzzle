using System;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;

namespace Puzzle.Bot
{
    /// <summary>
    /// What a move the game's tutorial shows by hand teaches (docs/GAME_DESIGN.md §19, the owner's decision of
    /// 2026-10-19: the first three levels). A closed set.
    /// </summary>
    public enum MoveLesson
    {
        /// <summary>Two neighbouring gems traded make a match of three, and nothing more.</summary>
        Match,

        /// <summary>Four gems in a straight line make a brush (a rocket).</summary>
        FourInALine,

        /// <summary>Five gems in a straight line make a palette (a colour bomb).</summary>
        FiveInALine,
    }

    /// <summary>
    /// Whether a move teaches a lesson, told by the engine itself: the move is played on a lookahead copy of the game
    /// (hidden refills, which never match), and what that copy counts for the move (Game.LastMove) is read.
    /// </summary>
    /// <remarks>
    /// Only a trade of two plain gems can teach: a special, an item or a tap teaches something else. A move teaches
    /// only when nothing fell into a second match after its own (no cascade), so that what the copy counts is what
    /// the move's own match made: the engine makes a colour bomb of a group with a straight run of five or more, and a
    /// rocket of a group whose best shape is a straight four (not an L, a T, a cross or a five; MatchFinder's order).
    /// So a move teaches the match when its match made no special, the four when it made one or two rockets and no
    /// other special, and the five when it made a colour bomb. No special is set off by a trade of two plain gems that
    /// no cascade follows, and that is checked too. The copy is the move's own: the game is never touched.
    /// </remarks>
    public static class MoveLessons
    {
        /// <summary>True when <paramref name="move"/> on <paramref name="game"/> as it stands teaches <paramref name="lesson"/>.</summary>
        /// <param name="lookaheadSeed">Seeds the copy's own random choices; none of them decides a lesson, so any seed gives the same answer.</param>
        /// <exception cref="ArgumentException">The game does not accept the move now.</exception>
        public static bool Teaches(Game game, Move move, MoveLesson lesson, ulong lookaheadSeed)
        {
            if (!game.IsLegal(move))
            {
                throw new ArgumentException($"{move} is not a move the game accepts now.", nameof(move));
            }

            if (move.Kind != MoveKind.Swap || !game.Board.TileAt(move.From).IsGem || !game.Board.TileAt(move.To).IsGem)
            {
                return false;
            }

            Game copy = game.CreateLookahead(lookaheadSeed);
            if (!copy.TryPlay(move))
            {
                throw new InvalidOperationException($"Legal move {move} was refused by its lookahead.");
            }

            PlayStats made = copy.LastMove;
            if (made.Cascades > 0 || made.SpecialsActivated > 0)
            {
                return false;
            }

            return lesson switch
            {
                MoveLesson.Match => made.SpecialsCreated == 0,
                MoveLesson.FourInALine => made.RocketsCreated > 0 && made.SpecialsCreated == made.RocketsCreated,
                MoveLesson.FiveInALine => made.ColorBombsCreated > 0,
                _ => throw new ArgumentOutOfRangeException(nameof(lesson), lesson, "A lesson the tutorial does not know."),
            };
        }
    }
}
