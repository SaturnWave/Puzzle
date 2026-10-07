#nullable enable
using System;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// What a finger did on the board, before anyone asks what it means: it tapped a cell, or it swiped from a cell
    /// towards a neighbour. A closed set: the private constructor keeps both kinds inside this file. What a gesture
    /// means depends on the board it meets (<see cref="BoardPointer.MoveMeantBy"/>), so a gesture made while the
    /// pieces are still moving can be kept and asked about once they have come to rest.
    /// </summary>
    public abstract class BoardGesture
    {
        private BoardGesture()
        {
        }

        /// <summary>The finger came down on a cell and lifted without going anywhere.</summary>
        public sealed class Tap : BoardGesture
        {
            public Tap(Position cell)
            {
                Cell = cell;
            }

            public Position Cell { get; }
        }

        /// <summary>The finger came down on a cell and went towards a neighbouring cell of the board.</summary>
        public sealed class Swipe : BoardGesture
        {
            public Swipe(Position from, Position toward)
            {
                From = from;
                Toward = toward;
            }

            public Position From { get; }

            public Position Toward { get; }
        }
    }

    /// <summary>
    /// What a finger on the board means. Dragging a piece towards a neighbour trades the two as soon as the finger
    /// has gone far enough, without waiting for it to lift. A touch that stays where it was is a tap: on a special
    /// piece it sets the piece off; on another piece it picks that piece, and a tap on a neighbour then trades the
    /// two. Places are told in cells: (2.5, 0.5) is the middle of the third cell of the top row.
    /// </summary>
    /// <remarks>
    /// Reading a finger is two steps. <see cref="Down"/>, <see cref="MovedTo"/> and <see cref="Up"/> only follow
    /// the finger over the board's cells and say which gesture it made; they need nothing of the game but the
    /// board's shape, so they work while a turn is still being shown. <see cref="MoveMeantBy"/> then says what a
    /// gesture means on the board as it stands, which is asked when the board is at rest. The pointer only proposes
    /// moves; whether a move is legal is the engine's to say when it is played.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </remarks>
    public sealed class BoardPointer
    {
        /// <summary>A finger that has moved less than this part of a cell has not dragged: it tapped.</summary>
        public const float DragThreshold = 0.3f;

        private readonly Game _game;
        private Press? _press;

        public BoardPointer(Game game)
        {
            _game = game;
        }

        /// <summary>The piece that was picked by a tap and waits for its neighbour to be tapped.</summary>
        public Position? Selected { get; private set; }

        /// <summary>A finger came down. A touch outside the board's cells starts nothing.</summary>
        public void Down(float column, float row)
        {
            var cell = new Position((int)Math.Floor(column), (int)Math.Floor(row));
            _press = _game.Board.Shape.IsPlayable(cell) ? new Press(cell, column, row) : null;
        }

        /// <summary>The finger moved. The swipe it made, the first time it has gone far enough towards a neighbouring cell.</summary>
        public BoardGesture? MovedTo(float column, float row)
        {
            if (_press is not Press press || press.HasDragged)
            {
                return null;
            }

            float across = column - press.Column;
            float down = row - press.Row;
            if (Math.Max(Math.Abs(across), Math.Abs(down)) < DragThreshold)
            {
                return null;
            }

            press.HasDragged = true;
            Selected = null;
            Position toward = Math.Abs(across) > Math.Abs(down)
                ? new Position(press.Cell.X + Math.Sign(across), press.Cell.Y)
                : new Position(press.Cell.X, press.Cell.Y + Math.Sign(down));

            // A drag off the board's edge or into a gap is no swipe.
            return _game.Board.Shape.IsPlayable(toward) ? new BoardGesture.Swipe(press.Cell, toward) : null;
        }

        /// <summary>The finger lifted. The tap it made, if it never dragged.</summary>
        public BoardGesture? Up()
        {
            Press? press = _press;
            _press = null;
            return press is null || press.HasDragged ? null : new BoardGesture.Tap(press.Cell);
        }

        /// <summary>The touch was taken away (a call came in, the game lost the screen): it means nothing.</summary>
        public void Cancel() => _press = null;

        /// <summary>Forgets the picked piece: the board has changed under it.</summary>
        public void ClearSelection() => Selected = null;

        /// <summary>
        /// The move a gesture means on the board as it stands now, if it means one. A tap that means no move may
        /// pick a piece or let the picked piece go instead (<see cref="Selected"/>).
        /// </summary>
        public Move? MoveMeantBy(BoardGesture gesture)
        {
            switch (gesture)
            {
                case BoardGesture.Swipe swipe:
                    Selected = null;
                    return Move.Swap(swipe.From, swipe.Toward);
                case BoardGesture.Tap tap:
                    return MoveMeantByATapOn(tap.Cell);
                default:
                    throw new InvalidOperationException($"A {gesture.GetType().Name} is not a gesture the board knows.");
            }
        }

        private Move? MoveMeantByATapOn(Position cell)
        {
            if (Selected is Position picked)
            {
                Selected = null;
                if (picked == cell)
                {
                    return null;
                }

                if (picked.IsAdjacentTo(cell))
                {
                    return Move.Swap(picked, cell);
                }
            }

            if (_game.IsLegal(Move.Tap(cell)))
            {
                return Move.Tap(cell);
            }

            Selected = CanBeTraded(cell) ? cell : (Position?)null;
            return null;
        }

        /// <summary>Gems, special pieces and items can be traded, unless tape holds them.</summary>
        private bool CanBeTraded(Position cell) =>
            _game.Board.TileAt(cell).Kind.IsSwappable() && _game.Board.ChainAt(cell) == 0;

        /// <summary>A finger on the board: where it came down, and whether it has already dragged.</summary>
        private sealed class Press
        {
            public Press(Position cell, float column, float row)
            {
                Cell = cell;
                Column = column;
                Row = row;
            }

            public Position Cell { get; }

            public float Column { get; }

            public float Row { get; }

            public bool HasDragged { get; set; }
        }
    }
}
