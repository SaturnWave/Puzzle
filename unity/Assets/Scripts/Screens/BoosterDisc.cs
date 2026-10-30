#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The picture on a booster's disc: one of the canvas' own marks, a piece of the board, or two pieces side by
    /// side. A picture is laid out in a box of 100 points around the disc's middle, as the canvas draws its marks,
    /// and drawn as large as the disc needs.
    /// </summary>
    public sealed class BoosterPicture
    {
        /// <summary>A mark of the canvas is drawn in a box of 100 points; a piece of the board fills a box one point across.</summary>
        private const float MarkBox = 100f;
        private const float PieceBox = 1f;

        private readonly Part[] _parts;

        private BoosterPicture(params Part[] parts)
        {
            _parts = parts;
        }

        /// <summary>
        /// The picture of the booster that starts a level with a brush and a splat on the board, as the canvas
        /// composes it (LevelStartTR): the upright brush at the left, 84 of the box's 100 across, and the splat
        /// at its right, 58 across and a point lower.
        /// </summary>
        public static BoosterPicture BrushAndSplat => new BoosterPicture(
            new Part(PieceArt.RocketVertical, fromMiddle: new Vector2(-30f, 0f), across: 84f),
            new Part(PieceArt.Bomb, fromMiddle: new Vector2(19f, -1f), across: 58f));

        /// <summary>The pieces of the picture, from the one that lies lowest up.</summary>
        internal IReadOnlyList<Part> Parts => _parts;

        /// <summary>One of the canvas' marks: the spatula, the shuffled palette, the token of three more moves.</summary>
        public static BoosterPicture Of(GalleryArt mark) => new BoosterPicture(new Part(mark));

        /// <summary>A piece of the board: a brush that lies or stands, the rainbow palette.</summary>
        public static BoosterPicture Of(PieceArt piece) => new BoosterPicture(new Part(piece, fromMiddle: Vector2.zero, across: MarkBox));

        /// <summary>One drawing of a picture: where its middle lies in the picture's box of 100, and how far across it is there.</summary>
        internal readonly struct Part
        {
            private readonly GalleryArt? _mark;
            private readonly PieceArt _piece;

            public Part(GalleryArt mark)
            {
                _mark = mark;
                _piece = default;
                FromMiddle = Vector2.zero;
                Across = MarkBox;
            }

            public Part(PieceArt piece, Vector2 fromMiddle, float across)
            {
                _mark = null;
                _piece = piece;
                FromMiddle = fromMiddle;
                Across = across;
            }

            /// <summary>From the middle of the picture's box, in the box's points, y upwards.</summary>
            public Vector2 FromMiddle { get; }

            public float Across { get; }

            public Sprite SpriteIn(ArtCatalogue art) => _mark is GalleryArt mark ? art.Of(mark) : art.Of(_piece);

            /// <summary>How far across the part's own drawing is at scale 1.</summary>
            public float DrawnAcross => _mark.HasValue ? MarkBox : PieceBox;
        }
    }

    /// <summary>
    /// A booster on its round cream disc, as the design canvas draws it on the board's shelf, before a level and
    /// in the shop (BoardTR, LevelStartTR, ShopTR): the booster's picture on a cream disc in a gilt ring, at its
    /// lower right the teal badge that counts how many the player owns, or the green one with a plus when none is
    /// left; a chosen or armed booster stands in a green ring, a chosen one with a green tick at its upper right;
    /// one that is set aside is dimmed and takes no finger. What the disc shows is told to it anew as the
    /// player's stock and choice change (<see cref="BoosterFace"/>). Sizes are in points around the disc's middle.
    /// </summary>
    /// <remarks>
    /// The disc is as large as its place asks (60 on the board's shelf, 66 before a level, 44 in the shop) and its
    /// picture grows with it, while its badges keep their size, as the canvas has them on a disc of 60 and on one
    /// of 66. Whoever places the disc may draw all of it smaller. A disc of the player's stock carries its badge;
    /// a disc of goods (<see cref="BoosterFace.Goods"/>) is bare, as the canvas draws a set's boosters over the pills
    /// that count them (ShopTR).
    /// </remarks>
    public sealed class BoosterDisc : IPressable
    {
        /// <summary>The disc's drawing is 60 points across.</summary>
        private const float DrawnSize = 60f;

        /// <summary>The picture's box as a share of the disc: the canvas draws 48 on 60, 54 on 66 and 36 on 44.</summary>
        private const float PictureShareOfDisc = 0.81f;

        /// <summary>A badge of 22 points reaches this far past the disc's right edge, and past its foot or its top (BoardTR, LevelStartTR).</summary>
        private const float BadgeBeyondTheSide = 5f;
        private const float CountBeyondTheFoot = 4f;
        private const float TickBeyondTheSide = 6f;
        private const float TickBeyondTheTop = 5f;

        /// <summary>A finger may land a little outside the disc and still press it.</summary>
        private const float FingerBeyondTheDisc = 4f;

        /// <summary>A pressed disc goes down a little, and loses a little of its light (<see cref="Dimming.Held"/>).</summary>
        private const float PressedDown = 2f;

        private readonly Transform _root;

        /// <summary>The disc with all that is on it: what goes down while it is held.</summary>
        private readonly Transform _cap;
        private readonly float _diameter;
        private readonly SpriteRenderer _disc;
        private readonly SpriteRenderer _ring;
        private readonly List<SpriteRenderer> _picture = new List<SpriteRenderer>();
        private readonly CountBadge _badge;
        private readonly SpriteRenderer _tickBadge;
        private readonly SpriteRenderer _tick;
        private bool _isPressed;

        /// <summary>How far from the disc's middle a finger presses it, in the points of what it stands on, where its place says; nothing for its own reach.</summary>
        private float? _touchReach;

        /// <param name="diameter">How far across the disc is at full size: 60 on the board's shelf, 66 before a level, 44 in the shop.</param>
        /// <param name="order">The disc's place in the drawing order; what stands on it takes the places above (<see cref="DiscPart"/>).</param>
        /// <param name="face">What the disc shows first.</param>
        public BoosterDisc(string name, Transform parent, ArtCatalogue art, BoosterPicture picture, float diameter, DrawOrder order, BoosterFace face)
        {
            _diameter = diameter;
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            _cap = new GameObject("Cap").transform;
            _cap.SetParent(_root, worldPositionStays: false);

            float discScale = diameter / DrawnSize;
            _disc = SceneObjects.AddSprite("Disc", _cap, art.Of(GalleryArt.CreamDisc), art.SpriteMaterial, order + (int)DiscPart.Disc);
            _disc.transform.localScale = new Vector3(discScale, discScale, 1f);
            _ring = SceneObjects.AddSprite("Chosen ring", _cap, art.Of(GalleryArt.ChosenRing), art.SpriteMaterial, order + (int)DiscPart.Ring);
            _ring.transform.localScale = new Vector3(discScale, discScale, 1f);
            AddThePicture(picture, art, order + (int)DiscPart.Picture);

            _badge = CountBadge.Add("Badge", _cap, art, BadgeFace.GetMore, order + (int)DiscPart.Badge);
            _tickBadge = SceneObjects.AddSprite("Tick badge", _cap, art.Of(GalleryArt.GreenBadge), art.SpriteMaterial, order + (int)DiscPart.Badge);
            _tickBadge.transform.localPosition = new Vector3(
                diameter / 2f + TickBeyondTheSide - CountBadge.Size / 2f, diameter / 2f + TickBeyondTheTop - CountBadge.Size / 2f, 0f);
            _tick = SceneObjects.AddSprite("Tick", _tickBadge.transform, art.Of(GalleryArt.GlyphTick), art.SpriteMaterial, order + (int)DiscPart.BadgeLettering);
            Show(face);
        }

        /// <summary>The middle of the disc, in the points of what it stands on.</summary>
        public Vector2 Middle => _root.localPosition;

        /// <summary>The disc with all that is on it, under what it stands on.</summary>
        public Transform Transform => _root;

        /// <summary>What the disc shows now.</summary>
        public BoosterFace Face { get; private set; }

        /// <summary>Puts the middle of the disc at a place of what it stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale = 1f)
        {
            _root.localPosition = new Vector3(middle.x, middle.y, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Lets a finger press the disc this far from its middle, in the points of what it stands on (the board's shelf: ScreenLayout.ShelfTouchReach).</summary>
        public void TouchFrom(float reach) => _touchReach = reach;

        /// <summary>Shows the disc, or takes it out of sight; a disc out of sight is not under any finger.</summary>
        public void Show(bool isShown) => _root.gameObject.SetActive(isShown);

        /// <summary>Changes what the disc shows, where it stands: its count or its plus, its ring, its tick, and whether it is dimmed.</summary>
        public void Show(BoosterFace face)
        {
            Face = face;
            _ring.enabled = face.IsRinged;
            _tickBadge.enabled = face.IsTicked;
            _tick.enabled = face.IsTicked;
            _badge.Transform.gameObject.SetActive(face.Badge != BoosterBadge.None);
            if (face.Badge == BoosterBadge.None)
            {
                ShowItsLight();
                return; // goods: the bare disc, its count told by a pill under it
            }

            _badge.Show(face.Badge switch
            {
                BoosterBadge.Count => BadgeFace.Owned(face.Owned),
                BoosterBadge.GetMore => BadgeFace.GetMore,
                BoosterBadge.OpensOn => BadgeFace.OpensOn(face.OpensOn ?? throw new InvalidOperationException("A locked face names the level it opens on.")),
                _ => throw new InvalidOperationException($"A booster's disc has no badge of the kind {face.Badge}."),
            });

            // A badge that widens for a longer number keeps its right end where it was; the level a locked booster
            // opens on stands across the disc's foot, in the middle.
            float badgeX = face.Badge == BoosterBadge.OpensOn ? 0f : _diameter / 2f + BadgeBeyondTheSide - _badge.Width / 2f;
            _badge.Transform.localPosition = new Vector3(badgeX, -(_diameter / 2f + CountBeyondTheFoot - CountBadge.Size / 2f), 0f);
            ShowItsLight();
        }

        /// <param name="point">A point of what the disc stands on.</param>
        public bool IsUnder(Vector2 point)
        {
            float reach = _touchReach ?? (_diameter / 2f + FingerBeyondTheDisc) * _root.localScale.x;
            return !Face.IsFaint && _root.gameObject.activeSelf && (point - Middle).sqrMagnitude <= reach * reach;
        }

        public void ShowPressed(bool isPressed)
        {
            _isPressed = isPressed;
            _cap.localPosition = new Vector3(0f, isPressed ? -PressedDown : 0f, 0f);
            ShowItsLight();
        }

        private void AddThePicture(BoosterPicture picture, ArtCatalogue art, DrawOrder order)
        {
            // The picture's box of 100 points, drawn as large as this disc has it.
            float boxScale = PictureShareOfDisc * _diameter / 100f;
            for (int part = 0; part < picture.Parts.Count; part++)
            {
                BoosterPicture.Part drawing = picture.Parts[part];
                SpriteRenderer drawn = SceneObjects.AddSprite($"Picture {part + 1}", _cap, drawing.SpriteIn(art), art.SpriteMaterial, order + part);
                float scale = boxScale * drawing.Across / drawing.DrawnAcross;
                drawn.transform.localPosition = new Vector3(boxScale * drawing.FromMiddle.x, boxScale * drawing.FromMiddle.y, 0f);
                drawn.transform.localScale = new Vector3(scale, scale, 1f);
                _picture.Add(drawn);
            }
        }

        /// <summary>
        /// The light of the disc and of its picture: dimmed while it is set aside, a little less while it is held,
        /// their own otherwise. Its badge is dimmed with it, and keeps its light while the disc is held.
        /// </summary>
        private void ShowItsLight()
        {
            Color colour = Dimming.Of(Face.IsFaint, _isPressed);
            _disc.color = colour;
            _ring.color = colour;
            foreach (SpriteRenderer drawn in _picture)
            {
                drawn.color = colour;
            }

            // The level a locked booster opens on is what the player is to read on it: its badge keeps its light.
            _badge.DrawIn(Dimming.Of(Face.IsFaint && Face.Badge != BoosterBadge.OpensOn));
        }
    }
}
