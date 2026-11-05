#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>A sprite of the board and the cell it belongs to: the tape over a piece, an exit mark.</summary>
    public readonly struct CellSprite
    {
        public CellSprite(Position cell, Transform transform)
        {
            Cell = cell;
            Transform = transform;
        }

        public Position Cell { get; }

        public Transform Transform { get; }
    }

    /// <summary>
    /// A board as the player sees it: the tray, the jelly slabs on it, the pieces, the tape over chained ones, and
    /// the exit marks under the columns. Everything hangs under one object whose unit is a cell (its origin is the
    /// board's top left corner, a row down is -1), so fitting the board to a screen moves and scales that one object.
    /// The frame around the board is not drawn here: it keeps its width whatever the size of a cell (BoardFrame).
    /// </summary>
    /// <remarks>
    /// The view holds what it shows and nothing of the rules: pieces are added, taken and changed by whoever plays a
    /// turn back, and <see cref="DifferencesFrom"/> says afterwards whether what is seen is the engine's board.
    /// </remarks>
    public sealed class BoardView : IDisposable
    {
        /// <summary>A piece is drawn a tenth larger than its cell, as the drawings were made: their soft edges overlap.</summary>
        public const float PieceScale = 1.1f;

        /// <summary>...and a little high in its cell, so the shadow under it falls inside the cell.</summary>
        public const float PieceLift = 0.015f;

        private readonly ArtCatalogue _art;
        private readonly BoardShape _shape;
        private readonly Transform _root;
        private readonly PaintedShape _floor;
        private readonly PaintedShape _weave;
        private readonly Texture2D _weavePicture;
        private readonly BoardMask _mask;
        private readonly Dictionary<int, PieceSprite> _pieces = new Dictionary<int, PieceSprite>();

        /// <summary>
        /// Pieces, tapes and slabs that were taken from the board in the turn that is being shown. Their sprites go
        /// when the turn is over (<see cref="ClearAwayWhatWasTaken"/>).
        /// </summary>
        private readonly List<PieceSprite> _takenPieces = new List<PieceSprite>();
        private readonly List<SpriteRenderer> _takenLayers = new List<SpriteRenderer>();
        private readonly Dictionary<Position, Layer> _tapes = new Dictionary<Position, Layer>();
        private readonly Dictionary<Position, Layer> _slabs = new Dictionary<Position, Layer>();
        private readonly Dictionary<Position, Transform> _exits = new Dictionary<Position, Transform>();

        private BoardView(Transform parent, ArtCatalogue art, BoardShape shape)
        {
            _art = art;
            _shape = shape;
            _root = new GameObject("Board").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _weavePicture = BoardFloor.BuildWeave();
            _floor = PaintedShape.Create("Floor", _root, art.PaintMaterial, DrawOrder.Floor);
            _floor.Show(BoardFloor.BuildPaint(shape));
            _weave = PaintedShape.Create("Weave", _root, art.PaintMaterial, DrawOrder.FloorWeave, _weavePicture);
            _weave.Show(BoardFloor.BuildWeaveCover(shape));
            _mask = BoardMask.Create(_root, shape);
        }

        /// <summary>The object everything of the board hangs under: its unit is a cell, its origin the board's top left corner.</summary>
        public Transform Root => _root;

        /// <summary>The pieces that belong to the board.</summary>
        public IEnumerable<PieceSprite> Pieces => _pieces.Values;

        /// <summary>The tapes that lie over pieces.</summary>
        public IEnumerable<CellSprite> Tapes
        {
            get
            {
                foreach (KeyValuePair<Position, Layer> tape in _tapes)
                {
                    yield return new CellSprite(tape.Key, tape.Value.Sprite.transform);
                }
            }
        }

        /// <param name="ledger">What lies on the board, with the number of every piece.</param>
        /// <param name="showExits">True on a level with items to drop: the marks show where they leave the board.</param>
        public static BoardView Create(Transform parent, ArtCatalogue art, PieceLedger ledger, bool showExits)
        {
            var view = new BoardView(parent, art, ledger.Shape);
            foreach (Position cell in ledger.Cells())
            {
                if (showExits && ledger.Shape.IsExit(cell))
                {
                    view._exits[cell] = view.AddSprite(PieceArt.Exit, (int)DrawOrder.Exit + cell.Y, cell, isCutAtTheEdge: false).transform;
                }
            }

            view.Lay(ledger);
            return view;
        }

        /// <summary>
        /// Takes everything that lies on the tray off the stage and lays the ledger's board out anew, every piece at
        /// rest. The last resort of a stage whose turn went wrong: what was on its way is given up, and what is seen
        /// afterwards is the board again.
        /// </summary>
        public void ShowAnew(PieceLedger ledger)
        {
            ClearAwayWhatWasTaken();
            foreach (PieceSprite piece in _pieces.Values)
            {
                piece.Remove();
            }

            foreach (Layer layer in _tapes.Values.Concat(_slabs.Values))
            {
                SceneObjects.DestroyObjectOf(layer.Sprite);
            }

            foreach (Transform exit in _exits.Values)
            {
                exit.localScale = new Vector3(ScreenLayout.ExitSizeInCells, ScreenLayout.ExitSizeInCells, 1f);
            }

            _pieces.Clear();
            _tapes.Clear();
            _slabs.Clear();
            Lay(ledger);
        }

        /// <summary>Puts the board where the layout says, at the layout's cell size.</summary>
        public void Place(ScreenLayout layout) => Place(new Vector2(layout.BoardLeft, layout.BoardTop), layout.CellSize);

        /// <summary>Puts the board's top left corner at a place of what it hangs under, with cells of a size (a tiny board on a card: BoosterCard).</summary>
        public void Place(Vector2 topLeft, float cellSize)
        {
            _root.localPosition = new Vector3(topLeft.x, topLeft.y, 0f);
            _root.localScale = new Vector3(cellSize, cellSize, 1f);

            // The weave keeps its size in points, so a larger cell shows more of its repeats.
            float repeatsPerCell = cellSize / BoardFloor.WeavePitch;
            _weave.RepeatTexture(new Vector2(repeatsPerCell, repeatsPerCell));

            // The gap between the board and its exit marks is the frame's, in points; here it is told in cells.
            float markMiddleBelowCell = ScreenLayout.ExitGap / cellSize + ScreenLayout.ExitSizeInCells / 2f;
            foreach (KeyValuePair<Position, Transform> exit in _exits)
            {
                exit.Value.localPosition = new Vector3(exit.Key.X + 0.5f, -(exit.Key.Y + 1f + markMiddleBelowCell), 0f);
                exit.Value.localScale = new Vector3(ScreenLayout.ExitSizeInCells, ScreenLayout.ExitSizeInCells, 1f);
            }
        }

        /// <summary>The piece with a number.</summary>
        /// <exception cref="InvalidOperationException">The board has no such piece: the view and its ledger have come apart.</exception>
        public PieceSprite Piece(int id) => _pieces.TryGetValue(id, out PieceSprite? piece)
            ? piece
            : throw new InvalidOperationException($"The board shows no piece with the number {id}.");

        /// <summary>A new piece, at rest in its cell.</summary>
        public PieceSprite AddPiece(int id, Tile tile, Position cell)
        {
            var piece = new PieceSprite(id, _root, _art, tile, cell);
            _pieces.Add(id, piece);
            return piece;
        }

        /// <summary>
        /// The piece no longer belongs to the board, though it is still seen: whoever takes it lets it go in its own
        /// way and puts it out of sight when that is over (<see cref="PieceSprite.GoForGood"/>). Its sprite is taken
        /// off the stage by <see cref="ClearAwayWhatWasTaken"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The board has no such piece.</exception>
        public PieceSprite TakePiece(int id)
        {
            PieceSprite piece = Piece(id);
            _pieces.Remove(id);
            _takenPieces.Add(piece);
            return piece;
        }

        /// <summary>
        /// Takes the sprites of every piece, tape and slab that was taken from the board off the stage. Call it when
        /// the turn is over and nothing holds them any more; until then what was taken and has gone is only out of
        /// sight, so that no act of the turn is left holding a sprite that is no longer there.
        /// </summary>
        public void ClearAwayWhatWasTaken()
        {
            foreach (PieceSprite piece in _takenPieces)
            {
                piece.Remove();
            }

            foreach (SpriteRenderer layer in _takenLayers)
            {
                SceneObjects.DestroyObjectOf(layer);
            }

            _takenPieces.Clear();
            _takenLayers.Clear();
        }

        /// <summary>The mark under a column that items leave the board through; nothing on a level without items.</summary>
        public Transform? ExitUnder(Position lowestCell) => _exits.TryGetValue(lowestCell, out Transform? exit) ? exit : null;

        /// <summary>Lays tape for so many hits over a cell; none for no hit.</summary>
        public void LayTape(Position cell, int hitsLeft)
        {
            if (hitsLeft > 0)
            {
                SpriteRenderer tape = AddSprite(PieceArtOf.Tape(hitsLeft), (int)DrawOrder.Tape + cell.Y, cell, isCutAtTheEdge: true);
                tape.transform.localPosition += new Vector3(0f, PieceLift, 0f);
                tape.transform.localScale = new Vector3(PieceScale, PieceScale, 1f);
                _tapes[cell] = new Layer(tape, hitsLeft);
            }
        }

        /// <summary>
        /// The tape over a cell stops being part of the board; whoever takes it lets it come off and puts it out of
        /// sight, and <see cref="ClearAwayWhatWasTaken"/> takes its sprite off the stage. Nothing when there is none.
        /// </summary>
        public SpriteRenderer? TakeTape(Position cell) => Take(_tapes, cell);

        /// <summary>Lays a slab of so many layers on a cell's floor; none for no layer.</summary>
        public void LaySlab(Position cell, int layers)
        {
            if (layers > 0)
            {
                _slabs[cell] = new Layer(AddSprite(PieceArtOf.Slab(layers, cell), (int)DrawOrder.Slab + cell.Y, cell, isCutAtTheEdge: false), layers);
            }
        }

        /// <summary>The slab of a cell stops being part of the board, as a tape does (<see cref="TakeTape"/>). Nothing when there is none.</summary>
        public SpriteRenderer? TakeSlab(Position cell) => Take(_slabs, cell);

        /// <summary>
        /// Where what is seen is not the engine's board, one line per fault; empty when the player sees exactly that
        /// board with every piece at rest. This is the check after every turn: an animation may show the way there
        /// as it likes, but it must end on the engine's board.
        /// </summary>
        public IReadOnlyList<string> DifferencesFrom(Board board)
        {
            var differences = new List<string>();
            var pieceIn = new Dictionary<Position, PieceSprite>();
            foreach (PieceSprite piece in _pieces.Values)
            {
                if (pieceIn.ContainsKey(piece.Cell))
                {
                    differences.Add($"{piece.Cell}: two pieces are shown, {pieceIn[piece.Cell].Art} and {piece.Art}");
                    continue;
                }

                pieceIn[piece.Cell] = piece;
                if (piece.WhatIsNotAtRest() is string unrest)
                {
                    differences.Add($"{piece.Cell}: the {piece.Art} is not at rest, it {unrest}");
                }
            }

            foreach (Position cell in BoardCells.Of(_shape))
            {
                PieceArt? shown = pieceIn.TryGetValue(cell, out PieceSprite? piece) ? piece.Art : (PieceArt?)null;
                PieceArt? engines = PieceArtOf.Tile(board.TileAt(cell));
                if (shown != engines)
                {
                    differences.Add($"{cell}: shows {Named(shown)}, the engine has {Named(engines)}");
                }

                int tape = _tapes.TryGetValue(cell, out Layer taped) ? taped.Count : 0;
                if (tape != board.ChainAt(cell))
                {
                    differences.Add($"{cell}: shows tape for {tape} hits, the engine has {board.ChainAt(cell)}");
                }

                int slab = _slabs.TryGetValue(cell, out Layer slabbed) ? slabbed.Count : 0;
                if (slab != board.JellyAt(cell))
                {
                    differences.Add($"{cell}: shows a slab of {slab} layers, the engine has {board.JellyAt(cell)}");
                }
            }

            return differences;
        }

        public void Dispose()
        {
            _floor.Dispose();
            _weave.Dispose();
            _mask.Dispose();
            SceneObjects.Destroy(_weavePicture);
            SceneObjects.DestroyObjectOf(_root);
        }

        private static string Named(PieceArt? art) => art?.ToString() ?? "nothing";

        /// <summary>Lays out what a ledger holds: slabs, tapes, and every piece at rest in its cell.</summary>
        private void Lay(PieceLedger ledger)
        {
            foreach (Position cell in ledger.Cells())
            {
                LaySlab(cell, ledger.SlabAt(cell));
                LayTape(cell, ledger.TapeAt(cell));
            }

            foreach (PlacedPiece piece in ledger.Pieces)
            {
                AddPiece(piece.Id, piece.Tile, piece.Cell);
            }
        }

        private SpriteRenderer? Take(Dictionary<Position, Layer> layers, Position cell)
        {
            if (!layers.TryGetValue(cell, out Layer layer))
            {
                return null;
            }

            layers.Remove(cell);
            _takenLayers.Add(layer.Sprite);
            return layer.Sprite;
        }

        /// <summary>A sprite in the middle of a cell, as large as the cell. Every drawing is one cell wide at scale 1.</summary>
        private SpriteRenderer AddSprite(PieceArt art, int order, Position cell, bool isCutAtTheEdge)
        {
            var sprite = new GameObject(art.ToString()).AddComponent<SpriteRenderer>();
            sprite.transform.SetParent(_root, worldPositionStays: false);
            sprite.transform.localPosition = new Vector3(cell.X + 0.5f, -(cell.Y + 0.5f), 0f);
            sprite.sprite = _art.Of(art);
            sprite.sharedMaterial = _art.SpriteMaterial;
            sprite.sortingOrder = order;
            sprite.maskInteraction = isCutAtTheEdge ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
            return sprite;
        }

        /// <summary>A tape or a slab on a cell: its sprite and how many hits or layers it stands for.</summary>
        private readonly struct Layer
        {
            public Layer(SpriteRenderer sprite, int count)
            {
                Sprite = sprite;
                Count = count;
            }

            public SpriteRenderer Sprite { get; }

            public int Count { get; }
        }
    }
}
