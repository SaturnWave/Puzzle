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
    /// <see cref="WithOneCounted"/>), and at the end of the move they must be the game's own again.
    /// </summary>
    public sealed class HudFigures
    {
        /// <summary>With this many moves left or fewer, the moves are shown in red.</summary>
        private const int FewMoves = 3;

        public HudFigures(int movesLeft, bool movesAreFew, IReadOnlyList<GoalFigure> goals)
        {
            MovesLeft = movesLeft;
            MovesAreFew = movesAreFew;
            Goals = goals;
        }

        public int MovesLeft { get; }

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
            return new HudFigures(left, left <= FewMoves, Goals);
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

            return new HudFigures(MovesLeft, MovesAreFew, goals);
        }
    }
}
