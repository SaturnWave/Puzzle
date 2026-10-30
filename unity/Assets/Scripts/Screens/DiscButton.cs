#nullable enable
using System;
using Puzzle.Client.Art;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>What a round button says by the mark on it. A closed set.</summary>
    public enum DiscGlyph
    {
        /// <summary>An arrow to the left: back, or to what comes before (JigsawTR).</summary>
        Back,

        /// <summary>The same arrow to the right: on to what comes after.</summary>
        Forward,

        /// <summary>Two bars: the level stops (BoardTR).</summary>
        Pause,

        /// <summary>A cross: the card closes (LevelStartTR, SettingsTR).</summary>
        Close,

        /// <summary>A gear: the settings (HomeTR).</summary>
        Gear,

        /// <summary>A plus on the small green disc of "get more" (HomeTR); every other mark stands on the teal disc.</summary>
        Plus,
    }

    /// <summary>
    /// A round button with a mark, as the design canvas draws the ways back, out and on of its screens: a teal disc
    /// in a gilt ring that goes down a little while it is held, with an arrow, the pause bars, a cross or a gear
    /// on it; and the small green disc with a plus. Sizes are in points; the teal disc is 44 across at full size,
    /// the green one 26.
    /// </summary>
    public sealed class DiscButton : IPressable
    {
        /// <summary>The teal disc at full size.</summary>
        public const float Size = 44f;

        /// <summary>A finger may land a little outside a disc and still press it.</summary>
        private const float FingerBeyondTheDisc = 7f;

        /// <summary>A pressed button goes down towards its lip, and loses a little of its light (<see cref="Dimming.Held"/>); one that leads nowhere is faint.</summary>
        private const float PressedDown = 2f;
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.32f);

        private readonly Transform _root;

        /// <summary>The disc and the mark on it: what goes down while the button is held.</summary>
        private readonly Transform _cap;
        private readonly SpriteRenderer _face;
        private readonly SpriteRenderer _mark;
        private readonly float _size;
        private bool _leadsSomewhere = true;

        /// <param name="order">The disc's place in the drawing order; its mark takes the place above.</param>
        public DiscButton(string name, Transform parent, ArtCatalogue art, DiscGlyph glyph, DrawOrder order)
        {
            Look look = LookOf(glyph);
            _size = look.DiscSize;
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            _cap = new GameObject("Cap").transform;
            _cap.SetParent(_root, worldPositionStays: false);
            _face = SceneObjects.AddSprite("Face", _cap, art.Of(look.Disc), art.SpriteMaterial, order);
            _mark = SceneObjects.AddSprite("Mark", _cap, art.Of(look.Mark), art.SpriteMaterial, order + 1);
            _mark.transform.localScale = new Vector3(look.MarkScale, look.MarkScale, 1f);
            _mark.flipX = look.IsTurnedAround; // the same drawing, turned around; its light still comes from above
        }

        /// <summary>The middle of the disc, in the points of what the button stands on.</summary>
        public Vector2 Middle => _root.localPosition;

        /// <summary>Puts the middle of the disc at a place of what the button stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale = 1f)
        {
            _root.localPosition = new Vector3(middle.x, middle.y, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Moves the button to another place in the drawing order; its mark takes the place above.</summary>
        public void DrawAt(DrawOrder order)
        {
            _face.sortingOrder = (int)order;
            _mark.sortingOrder = (int)order + 1;
        }

        /// <summary>Shows the button, or takes it out of sight; a button out of sight is not under any finger.</summary>
        public void Show(bool isShown) => _root.gameObject.SetActive(isShown);

        /// <summary>A button that leads nowhere just now is drawn faint and is not under any finger.</summary>
        public void LeadSomewhere(bool leadsSomewhere)
        {
            _leadsSomewhere = leadsSomewhere;
            DrawIn(leadsSomewhere ? Color.white : Faint);
        }

        /// <param name="point">A point of what the button stands on.</param>
        public bool IsUnder(Vector2 point)
        {
            float reach = _size / 2f * _root.localScale.x + FingerBeyondTheDisc;
            return _leadsSomewhere && _root.gameObject.activeSelf && (point - Middle).sqrMagnitude <= reach * reach;
        }

        public void ShowPressed(bool isPressed)
        {
            _cap.localPosition = new Vector3(0f, isPressed ? -PressedDown : 0f, 0f);
            if (_leadsSomewhere)
            {
                DrawIn(isPressed ? Dimming.Held : Color.white);
            }
        }

        private void DrawIn(Color colour)
        {
            _face.color = colour;
            _mark.color = colour;
        }

        /// <summary>The disc a mark stands on and how the mark is drawn there, as the canvas has each.</summary>
        private static Look LookOf(DiscGlyph glyph) => glyph switch
        {
            // JigsawTR: the arrow's box of 100 is 27 points on the disc of 44.
            DiscGlyph.Back => Look.OnTeal(GalleryArt.IconBack, markAcross: 27f, markDrawnAcross: 100f),
            DiscGlyph.Forward => Look.OnTeal(GalleryArt.IconBack, markAcross: 27f, markDrawnAcross: 100f, isTurnedAround: true),

            // BoardTR: the bars' box is 26 points on a disc of 46, which is this disc drawn a little larger.
            DiscGlyph.Pause => Look.OnTeal(GalleryArt.IconPause, markAcross: 26f * Size / 46f, markDrawnAcross: 100f),

            // LevelStartTR: the cross is drawn as large as it stands on the disc.
            DiscGlyph.Close => Look.OnTeal(GalleryArt.GlyphClose, markAcross: 22f, markDrawnAcross: 22f),

            // HomeTR: the gear's box is 27 points on the disc; the plus is drawn as large as it stands on its green disc of 26.
            DiscGlyph.Gear => Look.OnTeal(GalleryArt.IconGear, markAcross: 27f, markDrawnAcross: 100f),
            DiscGlyph.Plus => new Look(GalleryArt.GreenDisc, discSize: 26f, GalleryArt.GlyphPlus, markScale: 1f, isTurnedAround: false),
            _ => throw new ArgumentOutOfRangeException(nameof(glyph), glyph, "A mark no round button is drawn with."),
        };

        /// <summary>What a round button is drawn with.</summary>
        private readonly struct Look
        {
            public Look(GalleryArt disc, float discSize, GalleryArt mark, float markScale, bool isTurnedAround)
            {
                Disc = disc;
                DiscSize = discSize;
                Mark = mark;
                MarkScale = markScale;
                IsTurnedAround = isTurnedAround;
            }

            public GalleryArt Disc { get; }

            /// <summary>How far across the disc is at full size: what a finger may hit.</summary>
            public float DiscSize { get; }

            public GalleryArt Mark { get; }

            /// <summary>The share of its own drawing's size the mark is drawn at.</summary>
            public float MarkScale { get; }

            public bool IsTurnedAround { get; }

            /// <param name="markAcross">How far across the mark's whole picture is on the disc, in points.</param>
            /// <param name="markDrawnAcross">How far across its drawing draws that picture.</param>
            public static Look OnTeal(GalleryArt mark, float markAcross, float markDrawnAcross, bool isTurnedAround = false) =>
                new Look(GalleryArt.TealDisc, Size, mark, markAcross / markDrawnAcross, isTurnedAround);
        }
    }
}
