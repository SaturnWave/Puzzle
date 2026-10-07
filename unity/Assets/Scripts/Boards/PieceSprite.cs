#nullable enable
using Puzzle.Client.Art;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>
    /// A piece on the board as it is seen: one sprite that keeps its number from the moment it appears until it goes
    /// (PieceLedger gives the numbers). At rest it lies in the middle of its cell at its full size; whoever animates
    /// it moves, sizes, turns and fades it, and says in which cell it has arrived. Places are in cells: (2.5, 0.5) is
    /// the middle of the third cell of the top row, and rows grow downwards as the engine counts them.
    /// </summary>
    public sealed class PieceSprite
    {
        /// <summary>Two numbers closer than this are the same place or size: nothing that small is seen.</summary>
        private const float Sameness = 0.001f;

        /// <summary>A piece keeps its place among the rows in the drawing order even far above or below the board.</summary>
        private const int FarthestRow = 24;

        private readonly ArtCatalogue _art;
        private readonly SpriteRenderer _renderer;
        private Vector2 _place;
        private Vector2 _size = Vector2.one;
        private float _alpha = 1f;
        private float _turn;
        private bool _isLifted;
        private bool _hasGone;

        internal PieceSprite(int id, Transform board, ArtCatalogue art, Tile tile, Position cell)
        {
            Id = id;
            _art = art;
            _renderer = new GameObject("Piece").AddComponent<SpriteRenderer>();
            _renderer.transform.SetParent(board, worldPositionStays: false);
            _renderer.sharedMaterial = art.SpriteMaterial;
            _renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            Show(tile);
            Rest(cell);
        }

        /// <summary>The number the piece is known by in the view's ledger.</summary>
        public int Id { get; }

        /// <summary>The cell the piece belongs to: where it lies, or where it last lay while it is on its way.</summary>
        public Position Cell { get; private set; }

        /// <summary>The drawing the piece shows.</summary>
        public PieceArt Art { get; private set; }

        /// <summary>Where the piece's middle is, in cells.</summary>
        public Vector2 Place
        {
            get => _place;
            set
            {
                _place = value;
                _renderer.transform.localPosition = new Vector3(value.x, -value.y + BoardView.PieceLift, 0f);
                Sort();
            }
        }

        /// <summary>From 1, opaque, down to 0, not seen.</summary>
        public float Alpha
        {
            get => _alpha;
            set
            {
                _alpha = value;
                _renderer.color = new Color(1f, 1f, 1f, value);
            }
        }

        /// <summary>How far the piece is turned around its middle, in degrees counter-clockwise.</summary>
        public float Turn
        {
            get => _turn;
            set
            {
                _turn = value;
                _renderer.transform.localRotation = Quaternion.Euler(0f, 0f, value);
            }
        }

        /// <summary>A piece that is not yet due on the board is kept out of sight until its moment. A piece that has gone is never seen again.</summary>
        public bool IsSeen
        {
            get => _renderer.enabled;
            set => _renderer.enabled = value && !_hasGone;
        }

        /// <summary>The middle of a cell, as a place.</summary>
        public static Vector2 MiddleOf(Position cell) => new Vector2(cell.X + 0.5f, cell.Y + 0.5f);

        /// <summary>Changes what the piece is: a crate that took a hit, a gem that was dealt another colour.</summary>
        /// <exception cref="System.ArgumentException">The tile is empty: an empty cell has no piece.</exception>
        public void Show(Tile tile)
        {
            Art = PieceArtOf.Tile(tile) ?? throw new System.ArgumentException("An empty tile is not shown by a piece.", nameof(tile));
            _renderer.sprite = _art.Of(Art);
            _renderer.name = Art.ToString();
        }

        /// <summary>How wide and how high the piece is drawn; 1 is its size at rest.</summary>
        public void Size(float width, float height)
        {
            _size = new Vector2(width, height);
            _renderer.transform.localScale = new Vector3(width * BoardView.PieceScale, height * BoardView.PieceScale, 1f);
        }

        /// <summary>A carried piece is drawn over the pieces of every row; put down, it is one of its row again.</summary>
        public void Lift(bool isLifted)
        {
            _isLifted = isLifted;
            Sort();
        }

        /// <summary>The piece has reached a cell: it belongs there now and lies in its middle.</summary>
        public void ArriveIn(Position cell)
        {
            Cell = cell;
            Place = MiddleOf(cell);
        }

        /// <summary>Puts the piece at rest in a cell: in the middle, at its full size, upright, opaque, seen.</summary>
        public void Rest(Position cell)
        {
            Lift(false);
            ArriveIn(cell);
            Size(1f, 1f);
            Turn = 0f;
            Alpha = 1f;
            IsSeen = true;
        }

        /// <summary>What keeps this piece from being a piece at rest in its cell; nothing when it is one.</summary>
        public string? WhatIsNotAtRest()
        {
            if ((_place - MiddleOf(Cell)).magnitude > Sameness)
            {
                return $"lies at ({_place.x:0.###}, {_place.y:0.###})";
            }

            if ((_size - Vector2.one).magnitude > Sameness)
            {
                return $"has the size {_size.x:0.###} by {_size.y:0.###}";
            }

            if (Mathf.Abs(_turn) > Sameness || Mathf.Abs(_alpha - 1f) > Sameness || !IsSeen || _isLifted)
            {
                return $"is turned {_turn:0.#} degrees, seen at {_alpha:0.##}, shown: {IsSeen}, lifted: {_isLifted}";
            }

            return null;
        }

        /// <summary>
        /// The piece has left the board: from now on it is not seen, whatever is still done with it. Its sprite
        /// stays on the stage until the board clears away what was taken from it, so that an act of the turn which
        /// still holds the piece (the give of a landing that a burst overtook) finds a sprite to pose.
        /// </summary>
        public void GoForGood()
        {
            _hasGone = true;
            _renderer.enabled = false;
        }

        /// <summary>Takes the piece's sprite off the stage. Nothing may hold the piece any more.</summary>
        public void Remove() => SceneObjects.DestroyObjectOf(_renderer);

        private void Sort()
        {
            int row = Mathf.Clamp(Mathf.FloorToInt(_place.y), -FarthestRow, FarthestRow);
            _renderer.sortingOrder = (int)DrawOrder.Piece + row + (_isLifted ? (int)DrawOrder.PieceLift : 0);
        }
    }
}
