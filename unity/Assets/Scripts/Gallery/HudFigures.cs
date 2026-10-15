using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Core.Engine;

namespace Puzzle.Client.Gallery
{
    /// <summary>One goal as the plaque shows it: its icon and how much of it is left.</summary>
    public readonly struct GoalFigure
    {
        public GoalFigure(PieceArt icon, int remaining)
        {
            Icon = icon;
            Remaining = remaining;
        }

        public PieceArt Icon { get; }

        /// <summary>Zero once the goal is met: the plaque then shows a seal in place of a number.</summary>
        public int Remaining { get; }
    }

    /// <summary>
    /// The numbers the plaques show at one moment of a game. They are a copy, not the game itself: while a move is
    /// played back step by step, the plaques are shown the numbers of each step (<see cref="AfterAMove"/>,
    /// <see cref="WithOneCounted"/>), and at the end of the move they must be the game's own again. In the finale
    /// of a won game the moves are counted down as they turn into rockets (<see cref="WithMovesTurnedIntoRockets"/>),
    /// which the game itself does not do: it keeps saying what was left. What the plaques were counted to and what
    /// the game says are compared when nothing moves any more (<see cref="DifferencesFrom"/>).
    /// </summary>
    public sealed class HudFigures
    {
        /// <summary>With this many moves left or fewer, the moves are shown in red.</summary>
        private const int FewMoves = 3;

        public HudFigures(int movesLeft, bool movesAreFew, IReadOnlyList<GoalFigure> goals)
            : this(movesLeft, movesAreFew, goals, movesTurnedIntoRockets: 0)
        {
        }

        private HudFigures(int movesLeft, bool movesAreFew, IReadOnlyList<GoalFigure> goals, int movesTurnedIntoRockets)
        {
            MovesLeft = movesLeft;
            MovesAreFew = movesAreFew;
            Goals = goals;
            MovesTurnedIntoRockets = movesTurnedIntoRockets;
        }

        public int MovesLeft { get; }

        /// <summary>
        /// How many moves left over were counted down into rockets on the way to these numbers; none while a game
        /// is played. Kept beside <see cref="MovesLeft"/> because that number stops at none: a move counted
        /// down once too often would not show in it.
        /// </summary>
        public int MovesTurnedIntoRockets { get; }

        /// <summary>True while the game goes on with few moves left: a warning, which a decided game no longer needs.</summary>
        public bool MovesAreFew { get; }

        public IReadOnlyList<GoalFigure> Goals { get; }

        /// <summary>The numbers of a game as it stands now.</summary>
        public static HudFigures Of(Game game)
        {
            var goals = new GoalFigure[game.Goals.Count];
            for (int goal = 0; goal < goals.Length; goal++)
            {
                GoalProgress progress = game.Goals[goal];
                goals[goal] = new GoalFigure(PieceArtOf.Goal(progress.Goal), progress.Remaining);
            }

            bool movesAreFew = game.Status == GameStatus.Playing && game.MovesLeft <= FewMoves;
            return new HudFigures(game.MovesLeft, movesAreFew, goals);
        }

        /// <summary>
        /// The numbers once the move that is being played back is counted. Whether that move decided the game is
        /// only known at its end, so until then few moves are a warning.
        /// </summary>
        public HudFigures AfterAMove()
        {
            int left = Math.Max(0, MovesLeft - 1);
            return new HudFigures(left, left <= FewMoves, Goals, MovesTurnedIntoRockets);
        }

        /// <summary>
        /// The numbers of a won game in its finale, once so many of the moves left over have turned into rockets:
        /// the moves less those, never below none, and the goals as they are. The game is decided, so few moves
        /// are no warning here; that is why <see cref="AfterAMove"/> does not serve.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The count of rockets is negative.</exception>
        public HudFigures WithMovesTurnedIntoRockets(int rockets)
        {
            if (rockets < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rockets), rockets, "Moves turn into rockets, never rockets into moves.");
            }

            return new HudFigures(Math.Max(0, MovesLeft - rockets), movesAreFew: false, Goals, MovesTurnedIntoRockets + rockets);
        }

        /// <summary>
        /// These numbers while the medallion still counts the moves up to them, as it does with the extra moves a game
        /// that ran out of moves was given: it shows so many of them so far. Whether moves are few is these numbers'
        /// own, not the count's: a game given five is not warned at one, two and three.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The medallion would show fewer than none, or more moves than are left.</exception>
        public HudFigures WithMovesShown(int movesShown)
        {
            if (movesShown < 0 || movesShown > MovesLeft)
            {
                throw new ArgumentOutOfRangeException(nameof(movesShown), movesShown, $"The medallion counts up to the {MovesLeft} moves left, from none.");
            }

            return new HudFigures(movesShown, MovesAreFew, Goals, MovesTurnedIntoRockets);
        }

        /// <summary>The numbers with one more of a goal done; a goal that is met stays met.</summary>
        public HudFigures WithOneCounted(int goalIndex)
        {
            var goals = new GoalFigure[Goals.Count];
            for (int goal = 0; goal < goals.Length; goal++)
            {
                int counted = goal == goalIndex ? 1 : 0;
                goals[goal] = new GoalFigure(Goals[goal].Icon, Math.Max(0, Goals[goal].Remaining - counted));
            }

            return new HudFigures(MovesLeft, MovesAreFew, goals, MovesTurnedIntoRockets);
        }

        /// <summary>
        /// Where these numbers, as the plaques were counted to them step by step, are not the game's own, one
        /// line for each: the moves left, how many of them were counted down into rockets, and what is left of
        /// every goal. Whether few moves are a warning is not compared: that is only known when a move is over.
        /// </summary>
        /// <param name="ofTheGame">The numbers of the game as it stands (<see cref="Of"/>, and after a finale with its rockets counted down).</param>
        /// <exception cref="ArgumentException">The two have not the same goals.</exception>
        public IEnumerable<string> DifferencesFrom(HudFigures ofTheGame)
        {
            if (Goals.Count != ofTheGame.Goals.Count)
            {
                throw new ArgumentException($"These numbers have {Goals.Count} goals and the game's {ofTheGame.Goals.Count}: they are not of one game.", nameof(ofTheGame));
            }

            return DifferencesOfTheSameGoalsFrom(ofTheGame);
        }

        private IEnumerable<string> DifferencesOfTheSameGoalsFrom(HudFigures ofTheGame)
        {
            if (MovesLeft != ofTheGame.MovesLeft)
            {
                yield return $"plaques: {MovesLeft} moves left, the engine has {ofTheGame.MovesLeft}";
            }

            if (MovesTurnedIntoRockets != ofTheGame.MovesTurnedIntoRockets)
            {
                yield return $"plaques: {MovesTurnedIntoRockets} moves were counted down into rockets, the engine made {ofTheGame.MovesTurnedIntoRockets}";
            }

            for (int goal = 0; goal < ofTheGame.Goals.Count; goal++)
            {
                if (Goals[goal].Remaining != ofTheGame.Goals[goal].Remaining)
                {
                    yield return $"plaques: {Goals[goal].Remaining} left of goal {goal + 1}, the engine has {ofTheGame.Goals[goal].Remaining}";
                }
            }
        }
    }
}
