#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Jigsaws
{
    /// <summary>
    /// The wooden tray under a painting's jigsaw, as the design canvas draws it (JigsawTR): a block of wood with a
    /// velvet well in which the pieces the player has won lie side by side (in rows and columns beside the painting on
    /// a screen held sideways, <see cref="JigsawArrangement.SideBySide"/>), each a little askew, a cream pill on
    /// its top edge that says what to do, and, when more pieces wait than the well shows, a second pill that says
    /// how many wait behind. The tray owns every loose piece of the screen: whoever carries a piece or lets it fly
    /// moves the piece's own object and tells the tray when it is gone. Places are in the layout's points.
    /// </summary>
    public sealed class PieceTray : IDisposable
    {
        /// <summary>The tray's picture is the block and the room around it for its edge, lip and shadow: 16 points wider, 32 higher.</summary>
        private const float PictureMarginAcross = 16f;
        private const float PictureMarginUpright = 32f;

        /// <summary>A pill's picture is the pill and the room around it: 16 points wider than the pill, 40 high for a pill of 24.</summary>
        private const float PillPictureMargin = 16f;
        private const float PillPictureHeight = 40f;
        private const float PillSideRoom = 14f;

        /// <summary>The hint, as the design sets it (JigsawTR, "Parçayı yerine sürükle": 13 points).</summary>
        private const float HintSize = 13f;

        /// <summary>The pill of the pieces that wait behind rides on the tray's top edge, its right end this far in from the tray's right side.</summary>
        private const float BehindInFromSide = 16f;
        private const float BehindSideRoom = 14f;

        /// <summary>A piece in the tray lies a little askew, by at most this many degrees, and is held just over the velvet.</summary>
        private const float LargestTurn = 6f;
        private const float RestingLift = 0.3f;

        /// <summary>A finger picks up the piece in a place when it comes down within the place, or this far over or under the well.</summary>
        private const float FingerBeyondTheWell = 10f;

        private readonly Transform _root;
        private readonly ArtCatalogue _art;
        private readonly PaintingLook _look;
        private readonly SpriteRenderer _block;
        private readonly SpriteRenderer _hintPill;
        private readonly TextMeshPro _hint;
        private readonly SpriteRenderer _behindPill;
        private readonly TextMeshPro _behind;
        private readonly Dictionary<int, LoosePiece> _pieces = new Dictionary<int, LoosePiece>();

        private JigsawLayout? _layout;
        private float _softEdge = 1f;

        /// <summary>The row the tray last laid out; the pieces of its places lie at rest unless someone carries them.</summary>
        private TrayRow? _row;

        private PieceTray(Transform parent, ArtCatalogue art, PaintingLook look)
        {
            _art = art;
            _look = look;
            _root = new GameObject("Tray").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _block = SceneObjects.AddSprite("Block", _root, art.Of(GalleryArt.Tray), art.SpriteMaterial, DrawOrder.Tray);
            _block.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches both ways; its corners and the well's rim keep their shape
            (_hintPill, _hint) = AddPill("Hint");
            (_behindPill, _behind) = AddPill("Waiting behind");
            ShowBehind(0);
        }

        public static PieceTray Create(Transform parent, ArtCatalogue art, PaintingLook look) => new PieceTray(parent, art, look);

        /// <summary>How large a piece lies in the tray, as a share of its size in the painting.</summary>
        public float RestScale { get; private set; } = 1f;

        /// <summary>Puts the tray where the layout has it. The pieces are laid again by <see cref="Lay"/>.</summary>
        /// <param name="softEdge">The width of a pixel, in points.</param>
        public void Place(JigsawLayout layout, float softEdge)
        {
            _layout = layout;
            _softEdge = softEdge;
            _block.transform.localPosition = new Vector3(layout.Tray.MiddleX, layout.Tray.MiddleY, 0f);
            _block.size = new Vector2(layout.Tray.Width + PictureMarginAcross, layout.Tray.Height + PictureMarginUpright);
            _hintPill.transform.localPosition = new Vector3(layout.Hint.MiddleX, layout.Hint.MiddleY, 0f);
            PlaceThePillOfThoseBehind();
            RestScale = layout.TrayPieceScale;
        }

        /// <summary>Lays a row of pieces in the tray's places, at rest, and forgets every piece that lay or flew before.</summary>
        /// <exception cref="InvalidOperationException">The tray was not placed on a screen yet.</exception>
        public void Lay(TrayRow row)
        {
            RemoveEveryPiece();
            _row = row;
            for (int place = 0; place < row.Places.Count; place++)
            {
                if (row.Places[place] is int piece)
                {
                    Rest(Add(piece));
                }
            }

            ShowBehind(row.Behind);
        }

        /// <summary>
        /// The pieces of the row have all left their places, though their objects are still the tray's: no place
        /// holds a piece any more, and none waits behind.
        /// </summary>
        public void ForgetTheRow()
        {
            _row = null;
            ShowBehind(0);
        }

        /// <summary>The object of a loose piece: one that lies in a place, or one that was added to fly.</summary>
        /// <exception cref="KeyNotFoundException">The tray holds no such piece.</exception>
        public LoosePiece PieceOf(int piece) => _pieces[piece];

        /// <summary>
        /// Builds a loose piece of the painting at its size on this screen; the caller puts it where it belongs.
        /// A piece that takes a place of the tray's row is at rest once <see cref="Rest"/> is called for it.
        /// </summary>
        /// <exception cref="InvalidOperationException">The tray was not placed on a screen yet.</exception>
        public LoosePiece Add(int piece)
        {
            JigsawLayout layout = PlacedLayout();
            LoosePiece loose = LoosePiece.Create(_root, _art, _look, piece, layout.Hanging.Picture.Width, _softEdge, DrawOrder.TrayPiece);
            _pieces.Add(piece, loose);
            return loose;
        }

        /// <summary>Puts a piece down in its place in the tray: at its place's middle, small, a little askew, among the tray's pieces in the drawing order.</summary>
        public void Rest(LoosePiece piece)
        {
            Vector2 rest = RestOf(piece.Number);
            piece.Transform.localPosition = new Vector3(rest.x, rest.y, 0f);
            piece.Transform.localScale = new Vector3(RestScale, RestScale, 1f);
            piece.Transform.localRotation = Quaternion.Euler(0f, 0f, RestTurnOf(piece.Number));
            piece.DrawFrom(DrawOrder.TrayPiece);
            piece.Lift(RestingLift);
        }

        /// <summary>A piece has gone to its place in the painting: its loose object is taken away.</summary>
        public void Remove(int piece)
        {
            if (_pieces.Remove(piece, out LoosePiece? loose))
            {
                loose.Dispose();
            }
        }

        /// <summary>The middle of the place a piece of the row lies in; the middle of the well for a piece the row does not show.</summary>
        public Vector2 RestOf(int piece)
        {
            JigsawLayout layout = PlacedLayout();
            if (_row?.PlaceOf(piece) is int place)
            {
                (float x, float y) = layout.TrayPlaceMiddle(place, _row.Places.Count);
                return new Vector2(x, y);
            }

            return new Vector2(layout.Well.MiddleX, layout.Well.MiddleY);
        }

        /// <summary>How far a piece is turned while it lies in the tray, in degrees: always the same for the same piece.</summary>
        public static float RestTurnOf(int piece) => ((piece * 7 + 3) % 5 - 2) / 2f * LargestTurn;

        /// <summary>The piece of the row a finger at a point picks up; nothing where no piece lies.</summary>
        public int? PieceAt(Vector2 point)
        {
            if (_layout is not JigsawLayout layout || _row == null)
            {
                return null;
            }

            Box well = layout.Well;
            if (point.y < well.Bottom - FingerBeyondTheWell || point.y > well.Top + FingerBeyondTheWell)
            {
                return null;
            }

            // A tray of one row takes a finger anywhere over or under its places within the well's reach; a tray of
            // rows takes it within the place's own row.
            (float placeWidth, float placeHeight) = layout.TrayPlaceSize;
            foreach (int piece in _row.Shown)
            {
                Vector2 rest = RestOf(piece);
                bool isInItsColumn = Mathf.Abs(point.x - rest.x) <= placeWidth / 2f;
                bool isInItsRow = layout.TrayRows == 1 || Mathf.Abs(point.y - rest.y) <= placeHeight / 2f;
                if (isInItsColumn && isInItsRow)
                {
                    return piece;
                }
            }

            return null;
        }

        /// <summary>Changes what the pill on the tray's top edge says.</summary>
        public void Say(string hint) => Write(_hintPill, _hint, hint, PillSideRoom);

        /// <summary>Says how many pieces wait behind the ones the tray shows; nothing is shown when none do.</summary>
        public void ShowBehind(int pieces)
        {
            _behindPill.gameObject.SetActive(pieces > 0);
            if (pieces > 0)
            {
                Write(_behindPill, _behind, MuseumWords.MoreBehind(pieces), BehindSideRoom);
                PlaceThePillOfThoseBehind();
            }
        }

        public void Dispose()
        {
            RemoveEveryPiece();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>The pill ends at the same distance from the tray's right side whatever it says.</summary>
        private void PlaceThePillOfThoseBehind()
        {
            if (_layout is JigsawLayout layout)
            {
                float pillWidth = _behindPill.size.x - PillPictureMargin;
                _behindPill.transform.localPosition = new Vector3(layout.Tray.Right - BehindInFromSide - pillWidth / 2f, layout.Hint.MiddleY, 0f);
            }
        }

        private void RemoveEveryPiece()
        {
            foreach (LoosePiece piece in _pieces.Values)
            {
                piece.Dispose();
            }

            _pieces.Clear();
        }

        private JigsawLayout PlacedLayout() =>
            _layout ?? throw new InvalidOperationException("The tray has no screen yet; call Place first.");

        private (SpriteRenderer Pill, TextMeshPro Line) AddPill(string name)
        {
            SpriteRenderer pill = SceneObjects.AddSprite(name, _root, _art.Of(GalleryArt.Pill), _art.SpriteMaterial, DrawOrder.TrayLabel);
            // The picture's middle repeats and its round ends keep their shape. Repeated, not stretched: a pill for two
            // figures is far narrower than its picture, and a middle squeezed that hard is drawn from the atlas' small
            // copies, into which the neighbouring pictures have run.
            pill.drawMode = SpriteDrawMode.Tiled;
            TextMeshPro line = Lettering.Write("Words", pill.transform, _art, LetteringStyle.Label, HintSize, Lettering.Ink, DrawOrder.TrayLabelLettering);
            return (pill, line);
        }

        /// <summary>Writes a pill's words and makes the pill as wide as they need.</summary>
        private static void Write(SpriteRenderer pill, TextMeshPro line, string words, float sideRoom)
        {
            Lettering.Say(line, words);
            pill.size = new Vector2(Lettering.WidthOf(line, words) + 2f * sideRoom + PillPictureMargin, PillPictureHeight);
        }
    }
}
