#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>What a button looks like: the drawing of its pill, and how its label is lettered on it.</summary>
    public readonly struct ButtonLook
    {
        public ButtonLook(GalleryArt face, LetteringStyle lettering)
        {
            Face = face;
            Lettering = lettering;
        }

        /// <summary>The green button: the way forward.</summary>
        public static ButtonLook Green => new ButtonLook(GalleryArt.Button, LetteringStyle.OnButton);

        /// <summary>The teal button: the way to another place of the game.</summary>
        public static ButtonLook Teal => new ButtonLook(GalleryArt.TealButton, LetteringStyle.OnTeal);

        /// <summary>The blue button: the way to a painting's jigsaw, and what places its pieces.</summary>
        public static ButtonLook Blue => new ButtonLook(GalleryArt.BlueButton, LetteringStyle.OnBlue);

        public GalleryArt Face { get; }

        public LetteringStyle Lettering { get; }
    }

    /// <summary>
    /// A glossy pill with a label that a finger presses, as the web test page and the design canvas draw their
    /// buttons: the pill stands on a dark lip and goes down onto it while it is held. A label may have two lines
    /// (a line break in its words) and a mark at its left; a red badge with a count may sit on the pill's upper
    /// right corner. Sizes are in points around the middle of the pill at its full size; whoever places the button
    /// may draw it smaller.
    /// </summary>
    public sealed class PushButton : IPressable
    {
        /// <summary>The pill's height, and the lip under it that is part of what a finger may hit.</summary>
        public const float Height = 72f;
        public const float Lip = 7f;

        /// <summary>The button's picture is its pill and the room around it for its rim, lip and shadow: 40 points wider, 112 high.</summary>
        private const float PictureMargin = 40f;
        private const float PictureHeight = 112f;

        /// <summary>The label keeps this far from each end of the pill, where the pill is round.</summary>
        private const float LabelInFromEnds = 22f;

        /// <summary>The label is drawn in an outline, which brings its letters closer: they stand apart by this many hundredths of their size.</summary>
        private const float LabelSpacing = 3f;

        /// <summary>The label stands a little above the pill's middle: the pill's foot is its dark edge.</summary>
        private const float LabelAboveMiddle = 2.5f;

        /// <summary>The two lines of a label stand this far apart, as a share of their type size.</summary>
        private const float LineApart = 1.02f;

        /// <summary>A mark is drawn in a box of 100 points; on a button it is this large, begins this far from the pill's end, and its label begins this far after it.</summary>
        private const float MarkBox = 100f;
        private const float MarkSize = 46f;
        private const float MarkInFromEnd = 10f;
        private const float GapMarkToLabel = 2f;

        /// <summary>The badge: its middle lies this far in from the pill's right end and this far over its top edge.</summary>
        private const float BadgeInFromEnd = 10f;
        private const float BadgeOverTop = 1f;

        /// <summary>A pressed button goes down towards its lip and loses a little of its light.</summary>
        private const float PressedDown = 3f;
        private static readonly Color PressedShade = new Color(0.88f, 0.88f, 0.88f, 1f);

        private readonly Transform _root;
        private readonly Transform _pill;
        private readonly SpriteRenderer _face;
        private readonly ArtCatalogue _art;
        private readonly DrawOrder _letteringOrder;
        private readonly float _width;

        /// <param name="label">What the button says; a line break in it makes two lines.</param>
        /// <param name="width">The pill's width at full size.</param>
        /// <param name="labelSize">The label's type size; a line too long for the pill is set smaller.</param>
        /// <param name="mark">A drawing at the left of the label; none for a button of words alone.</param>
        public PushButton(
            string name, Transform parent, ArtCatalogue art, ButtonLook look, string label, float width, float labelSize,
            DrawOrder faceOrder, DrawOrder letteringOrder, GalleryArt? mark = null)
        {
            _art = art;
            _letteringOrder = letteringOrder;
            _width = width;
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            _pill = new GameObject("Pill").transform;
            _pill.SetParent(_root, worldPositionStays: false);

            _face = SceneObjects.AddSprite("Face", _pill, art.Of(look.Face), art.SpriteMaterial, faceOrder);
            _face.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
            _face.size = new Vector2(width + PictureMargin, PictureHeight);

            float labelLeft = -width / 2f + LabelInFromEnds;
            if (mark is GalleryArt drawing)
            {
                Transform picture = SceneObjects.AddSprite("Mark", _pill, art.Of(drawing), art.SpriteMaterial, letteringOrder).transform;
                picture.localPosition = new Vector3(-width / 2f + MarkInFromEnd + MarkSize / 2f, LabelAboveMiddle, 0f);
                picture.localScale = new Vector3(MarkSize / MarkBox, MarkSize / MarkBox, 1f);
                labelLeft = -width / 2f + MarkInFromEnd + MarkSize + GapMarkToLabel;
            }

            float labelRight = width / 2f - LabelInFromEnds;
            WriteLabel(look, label.Split('\n'), labelSize, (labelLeft + labelRight) / 2f, labelRight - labelLeft);
        }

        /// <summary>The middle of the pill, in the points of what the button stands on.</summary>
        public Vector2 Middle => _root.localPosition;

        /// <summary>Puts the middle of the pill at a place of what the button stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale = 1f)
        {
            _root.localPosition = new Vector3(middle.x, middle.y, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Shows the button, or takes it out of sight; a button out of sight is not under any finger.</summary>
        public void Show(bool isShown) => _root.gameObject.SetActive(isShown);

        /// <summary>Takes the button away for good.</summary>
        public void Remove() => SceneObjects.DestroyObjectOf(_root);

        /// <summary>Sets a red badge with a count on the pill's upper right corner.</summary>
        public void AddBadge(int count)
        {
            Transform badge = CountBadge.Add("Badge", _pill, _art, count, _letteringOrder + 1);
            badge.localPosition = new Vector3(_width / 2f - BadgeInFromEnd, Height / 2f + BadgeOverTop, 0f);
        }

        /// <param name="point">A point of what the button stands on.</param>
        public bool IsUnder(Vector2 point)
        {
            if (!_root.gameObject.activeSelf)
            {
                return false;
            }

            Vector2 fromMiddle = (point - Middle) / _root.localScale.x;
            bool isBesideIt = Mathf.Abs(fromMiddle.x) > _width / 2f;
            bool isOverOrUnderIt = fromMiddle.y > Height / 2f || fromMiddle.y < -Height / 2f - Lip;
            return !isBesideIt && !isOverOrUnderIt;
        }

        public void ShowPressed(bool isPressed)
        {
            _pill.localPosition = new Vector3(0f, isPressed ? -PressedDown : 0f, 0f);
            _face.color = isPressed ? PressedShade : Color.white;
        }

        /// <summary>Writes the label's lines around a middle, each no wider than the room it has.</summary>
        private void WriteLabel(ButtonLook look, string[] lines, float size, float middle, float room)
        {
            float top = LabelAboveMiddle + (lines.Length - 1) * LineApart * size / 2f;
            for (int line = 0; line < lines.Length; line++)
            {
                TextMeshPro written = Lettering.Write($"Label {line + 1}", _pill, _art, look.Lettering, size, Color.white, _letteringOrder);
                written.transform.localPosition = new Vector3(middle, top - line * LineApart * size, 0f);
                written.characterSpacing = LabelSpacing;
                Lettering.SayWithin(written, lines[line], size, room);
            }
        }
    }
}
