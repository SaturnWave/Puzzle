#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// Where the pieces of a trade line up: for each of its two pieces, the runs of three or more gems of its colour
    /// through the cell it comes to, across and down, as the board would lie the moment the two have traded places.
    /// The tutorial lights them with the two pieces, so that a four or a five shown by hand is seen whole. It reads
    /// the board as it lies and never plays the trade; what the engine then makes of the match is the engine's
    /// (Puzzle.Bot's MoveLessons asks it). No Unity dependency: the repository's tests compile it too.
    /// </summary>
    public static class TradeLine
    {
        /// <summary>The runs the trade lines up, each as its first and last cell in reading order; none for a tap or a trade that lines up nothing.</summary>
        public static IReadOnlyList<(Position First, Position Last)> RunsOf(Board board, Move move)
        {
            var runs = new List<(Position First, Position Last)>();
            if (move.Kind != MoveKind.Swap)
            {
                return runs;
            }

            AddTheRuns(runs, board, comesFrom: move.From, comesTo: move.To);
            AddTheRuns(runs, board, comesFrom: move.To, comesTo: move.From);
            return runs;
        }

        /// <summary>The runs through the cell a piece comes to, across and down, of three or more of its colour.</summary>
        private static void AddTheRuns(List<(Position First, Position Last)> runs, Board board, Position comesFrom, Position comesTo)
        {
            Tile moving = board.TileAt(comesFrom);
            if (!moving.IsGem)
            {
                return;
            }

            bool IsOfItsColour(Position cell) => AfterTheTrade(board, cell, comesFrom, comesTo).IsGemOf(moving.Color);
            foreach ((int stepX, int stepY) in new[] { (1, 0), (0, 1) })
            {
                Position first = comesTo;
                Position last = comesTo;
                while (IsOnTheBoard(board, Step(first, -stepX, -stepY)) && IsOfItsColour(Step(first, -stepX, -stepY)))
                {
                    first = Step(first, -stepX, -stepY);
                }

                while (IsOnTheBoard(board, Step(last, stepX, stepY)) && IsOfItsColour(Step(last, stepX, stepY)))
                {
                    last = Step(last, stepX, stepY);
                }

                int length = Math.Abs(last.X - first.X) + Math.Abs(last.Y - first.Y) + 1;
                if (length >= 3)
                {
                    runs.Add((first, last));
                }
            }
        }

        /// <summary>What lies in a cell the moment the two pieces have traded places.</summary>
        private static Tile AfterTheTrade(Board board, Position cell, Position comesFrom, Position comesTo) =>
            cell == comesTo ? board.TileAt(comesFrom)
            : cell == comesFrom ? board.TileAt(comesTo)
            : board.TileAt(cell);

        private static bool IsOnTheBoard(Board board, Position cell) => board.Shape.IsPlayable(cell);

        private static Position Step(Position cell, int byX, int byY) => new Position(cell.X + byX, cell.Y + byY);
    }
}
