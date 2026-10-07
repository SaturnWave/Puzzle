#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Core.Model;

namespace Puzzle.Client.Play
{
    /// <summary>A piece of the board, known by a number that it keeps for as long as it lies there, wherever it moves.</summary>
    public readonly struct PlacedPiece
    {
        public PlacedPiece(int id, Position cell, Tile tile)
        {
            Id = id;
            Cell = cell;
            Tile = tile;
        }

        public int Id { get; }

        public Position Cell { get; }

        public Tile Tile { get; }
    }

    /// <summary>
    /// The board as the view knows it: what lies in every cell, and which piece of the view that is. The engine's
    /// board only says what a cell holds; an animation needs to know that the gem now in this cell is the one that
    /// was three rows up, so every piece gets a number here when it appears and keeps it until it goes. The ledger
    /// is brought forward event by event (<see cref="TurnChoreographer"/>), and after every turn it must say what the
    /// engine's board says (<see cref="DifferencesFrom"/>). This file has no Unity dependency, so the repository's
    /// tests compile it too.
    /// </summary>
    public sealed class PieceLedger
    {
        /// <summary>The number that means "no piece": numbers of pieces start above it.</summary>
        public const int NoPiece = 0;

        private readonly Tile[] _tiles;
        private readonly int[] _pieceIds;
        private readonly int[] _tapeHits;
        private readonly int[] _slabLayers;
        private int _lastId = NoPiece;

        private PieceLedger(BoardShape shape)
        {
            Shape = shape;
            int cells = shape.Width * shape.Height;
            _tiles = new Tile[cells];
            _pieceIds = new int[cells];
            _tapeHits = new int[cells];
            _slabLayers = new int[cells];
        }

        public BoardShape Shape { get; }

        /// <summary>Every piece on the board, row by row from the top.</summary>
        public IEnumerable<PlacedPiece> Pieces
        {
            get
            {
                foreach (Position cell in Cells())
                {
                    if (PieceAt(cell) != NoPiece)
                    {
                        yield return new PlacedPiece(PieceAt(cell), cell, TileAt(cell));
                    }
                }
            }
        }

        /// <summary>The ledger of a board as it stands: every tile on it becomes a numbered piece.</summary>
        public static PieceLedger Of(Board board)
        {
            var ledger = new PieceLedger(board.Shape);
            ledger.StartAgainFrom(board);
            return ledger;
        }

        /// <summary>
        /// Forgets what the ledger held and takes the board down as it stands, every tile a piece with a number no
        /// piece has had before. This is how a view that has lost track of a turn finds its way back to the game.
        /// </summary>
        /// <exception cref="ArgumentException">The board is not the shape this ledger was made for.</exception>
        public void StartAgainFrom(Board board)
        {
            if (board.Shape.Width != Shape.Width || board.Shape.Height != Shape.Height)
            {
                throw new ArgumentException($"The ledger is of a board of {Shape.Width}x{Shape.Height} cells, not {board.Shape.Width}x{board.Shape.Height}.", nameof(board));
            }

            foreach (Position cell in Cells())
            {
                Tile tile = board.TileAt(cell);
                Put(cell, tile.IsEmpty ? NoPiece : NewId(), tile);
                SetTape(cell, board.ChainAt(cell));
                SetSlab(cell, board.JellyAt(cell));
            }
        }

        /// <summary>Every playable cell, row by row from the top, each row from the left.</summary>
        public IEnumerable<Position> Cells()
        {
            for (int y = 0; y < Shape.Height; y++)
            {
                for (int x = 0; x < Shape.Width; x++)
                {
                    var cell = new Position(x, y);
                    if (Shape.IsPlayable(cell))
                    {
                        yield return cell;
                    }
                }
            }
        }

        public Tile TileAt(Position cell) => _tiles[IndexOf(cell)];

        /// <summary>The number of the piece in a cell, or <see cref="NoPiece"/>.</summary>
        public int PieceAt(Position cell) => _pieceIds[IndexOf(cell)];

        public int TapeAt(Position cell) => _tapeHits[IndexOf(cell)];

        public int SlabAt(Position cell) => _slabLayers[IndexOf(cell)];

        /// <summary>A number no piece has had yet.</summary>
        public int NewId() => ++_lastId;

        public void Put(Position cell, int pieceId, Tile tile)
        {
            _pieceIds[IndexOf(cell)] = pieceId;
            _tiles[IndexOf(cell)] = tile;
        }

        /// <summary>Empties a cell and says which piece lay in it.</summary>
        /// <exception cref="InvalidOperationException">The cell was empty: an event named a piece the ledger does not have.</exception>
        public PlacedPiece Take(Position cell)
        {
            var piece = new PlacedPiece(PieceAt(cell), cell, TileAt(cell));
            if (piece.Id == NoPiece)
            {
                throw new InvalidOperationException($"The view has no piece in cell {cell}, but the engine took one from it.");
            }

            Put(cell, NoPiece, Tile.Empty);
            return piece;
        }

        /// <summary>Moves the piece of one cell into another, which must be empty.</summary>
        /// <exception cref="InvalidOperationException">There is no piece to move, or the other cell is taken.</exception>
        public PlacedPiece Move(Position from, Position to)
        {
            if (PieceAt(to) != NoPiece)
            {
                throw new InvalidOperationException($"The engine moved a piece from {from} into {to}, where the view still has one.");
            }

            PlacedPiece piece = Take(from);
            Put(to, piece.Id, piece.Tile);
            return new PlacedPiece(piece.Id, to, piece.Tile);
        }

        /// <summary>Trades the contents of two cells; either may be empty.</summary>
        public void Trade(Position one, Position other)
        {
            (int id, Tile tile) = (PieceAt(one), TileAt(one));
            Put(one, PieceAt(other), TileAt(other));
            Put(other, id, tile);
        }

        public void SetTape(Position cell, int hitsLeft) => _tapeHits[IndexOf(cell)] = hitsLeft;

        public void SetSlab(Position cell, int layersLeft) => _slabLayers[IndexOf(cell)] = layersLeft;

        /// <summary>
        /// Where the ledger and the engine's board disagree, one line per cell; empty when they say the same. After a
        /// turn's events have been played into the ledger it must be empty, or an event was misread.
        /// </summary>
        public IReadOnlyList<string> DifferencesFrom(Board board)
        {
            var differences = new List<string>();
            foreach (Position cell in Cells())
            {
                if (TileAt(cell) != board.TileAt(cell))
                {
                    differences.Add($"{cell}: the view has {TileAt(cell)}, the engine {board.TileAt(cell)}");
                }

                if ((PieceAt(cell) == NoPiece) != TileAt(cell).IsEmpty)
                {
                    differences.Add($"{cell}: the view's tile {TileAt(cell)} and its piece number {PieceAt(cell)} do not go together");
                }

                if (TapeAt(cell) != board.ChainAt(cell))
                {
                    differences.Add($"{cell}: the view has tape for {TapeAt(cell)} hits, the engine for {board.ChainAt(cell)}");
                }

                if (SlabAt(cell) != board.JellyAt(cell))
                {
                    differences.Add($"{cell}: the view has {SlabAt(cell)} slab layers, the engine {board.JellyAt(cell)}");
                }
            }

            return differences;
        }

        /// <exception cref="ArgumentOutOfRangeException">The cell is not on the board.</exception>
        private int IndexOf(Position cell) => Shape.Contains(cell)
            ? cell.Y * Shape.Width + cell.X
            : throw new ArgumentOutOfRangeException(nameof(cell), cell, $"The board is {Shape.Width}x{Shape.Height} cells.");
    }
}
