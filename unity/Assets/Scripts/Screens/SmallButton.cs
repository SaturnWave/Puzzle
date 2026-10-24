#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>The colours the design draws its small buttons in. A closed set.</summary>
    public enum SmallButtonColour
    {
        /// <summary>Green: buy this (ShopTR, a price).</summary>
        Green,

        /// <summary>Blue: watch an advertisement (ShopTR, "İzle").</summary>
        Blue,
    }

    /// <summary>The small drawing a small button may have before its label. A closed set.</summary>
    public enum SmallButtonMark
    {
        /// <summary>Words alone.</summary>
        None,

        /// <summary>A coin: the price that follows is in the game's coins.</summary>
        Coin,

        /// <summary>The white triangle of "watch".</summary>
        Play,
    }

    /// <summary>
    /// The small button of the design canvas, 44 points high (ShopTR): the large button's glossy pill on its dark
    /// lip with every measure of its own, a label on one line, and before the label a coin or the triangle of
    /// "watch" where the button has one. The mark and the label are centred together on the pill. What the button
    /// says can be said anew, and a button that cannot be used just now is dimmed. Sizes are in points around
    /// the middle of the pill at its full size; whoever places the button may draw it smaller.
    /// </summary>
    public sealed class SmallButton : IPressable
    {
        /// <summary>The pill's height, and the lip under it that is part of what a finger may hit.</summary>
        public const float Height = 44f;
        public const float Lip = 5f;

        /// <summary>
        /// The button's picture is its pill and the room around it for its rim, lip and shadow: 28 points wider,
        /// 72 high. A pressed button goes 2 points down towards its lip.
        /// </summary>
        private static readonly PillMeasures Measures = new PillMeasures(Height, Lip, pictureMargin: 28f, pictureHeight: 72f, pressedDown: 2f);

        /// <summary>The label keeps this far from each end of the pill, where the pill is round.</summary>
        private const float LabelInFromEnds = 11f;

        /// <summary>The label's line is centred this far above the pill's middle (ShopTR: 3 points more under it than over it): the pill's foot is its dark edge.</summary>
        private const float LabelAboveMiddle = 1.5f;

        private readonly ButtonPill _pill;
        private readonly SpriteRenderer? _mark;
        private readonly MarkPlace _markPlace;
        private readonly TextMeshPro _label;
        private readonly LetteringStyle _lettering;
        private readonly float _width;
        private readonly float _labelSize;

        /// <param name="label">What the button says, on one line.</param>
        /// <param name="width">The pill's width at full size; the design's is 100.</param>
        /// <param name="labelSize">The label's type size (ShopTR: 16 for a price, 18 for "İzle"); a label too long for the pill is set smaller.</param>
        public SmallButton(
            string name, Transform parent, ArtCatalogue art, SmallButtonColour colour, string label, float width, float labelSize,
            DrawOrder faceOrder, DrawOrder letteringOrder, SmallButtonMark mark = SmallButtonMark.None)
        {
            _width = width;
            _labelSize = labelSize;
            _lettering = LetteringOn(colour);
            _pill = new ButtonPill(name, parent, art.Of(FaceOf(colour)), art.SpriteMaterial, Measures, width, faceOrder);

            _markPlace = PlaceOf(mark);
            if (_markPlace.Drawing is GalleryArt drawing)
            {
                _mark = SceneObjects.AddSprite("Mark", _pill.Pill, art.Of(drawing), art.SpriteMaterial, letteringOrder);
                _mark.transform.localScale = new Vector3(_markPlace.Scale, _markPlace.Scale, 1f);
            }

            _label = Lettering.Write("Label", _pill.Pill, art, _lettering, labelSize, Color.white, letteringOrder);
            Say(label);
        }

        /// <summary>The middle of the pill, in the points of what the button stands on.</summary>
        public Vector2 Middle => _pill.Middle;

        /// <summary>True while the button is drawn dimmed: it cannot be used just now.</summary>
        public bool IsDimmed => _pill.IsDimmed;

        /// <summary>Puts the middle of the pill at a place of what the button stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale = 1f) => _pill.Place(middle, scale);

        /// <summary>Shows the button, or takes it out of sight; a button out of sight is not under any finger.</summary>
        public void Show(bool isShown) => _pill.Show(isShown);

        /// <summary>Takes the button away for good.</summary>
        public void Remove() => _pill.Remove();

        /// <summary>What the button says now.</summary>
        public string Label => _label.text;

        /// <summary>Changes what the button says, where it stands; its mark and its words are centred together anew.</summary>
        public void Say(string label)
        {
            // As a stylesheet lays a row: the mark's room, a gap, the words; the row is centred on the pill.
            float besideTheWords = _mark != null ? _markPlace.Room + _markPlace.GapToLabel : 0f;
            Lettering.SayWithin(_label, label, _labelSize, _width - 2f * LabelInFromEnds - besideTheWords);
            float wordsWidth = Lettering.WidthOf(_label, label);
            float rowLeft = -(besideTheWords + wordsWidth) / 2f;
            if (_mark != null)
            {
                _mark.transform.localPosition = new Vector3(rowLeft + _markPlace.MiddleInItsRoom, _markPlace.AboveMiddle, 0f);
            }

            _label.transform.localPosition = new Vector3(
                rowLeft + besideTheWords + wordsWidth / 2f, LabelAboveMiddle - Lettering.FigureDrop(_lettering) * _label.fontSize, 0f);
        }

        /// <summary>
        /// Draws the button dimmed, or lit again: a dimmed button cannot be used just now. It stays where it is and
        /// says what it says, and it is not under any finger.
        /// </summary>
        public void Dim(bool isDimmed)
        {
            _pill.Dim(isDimmed);
            Color colour = Dimming.Of(isDimmed);
            Lettering.DrawIn(_label, colour);
            if (_mark != null)
            {
                _mark.color = colour;
            }
        }

        /// <param name="point">A point of what the button stands on.</param>
        public bool IsUnder(Vector2 point) => _pill.IsUnder(point);

        /// <summary>True when a point lies on the button in sight, whether it is dimmed or not: what a page that explains a dimmed button asks.</summary>
        /// <param name="point">A point of what the button stands on.</param>
        public bool Covers(Vector2 point) => _pill.Covers(point);

        public void ShowPressed(bool isPressed) => _pill.ShowPressed(isPressed);

        private static GalleryArt FaceOf(SmallButtonColour colour) => colour switch
        {
            SmallButtonColour.Green => GalleryArt.SmallButton,
            SmallButtonColour.Blue => GalleryArt.SmallBlueButton,
            _ => throw new ArgumentOutOfRangeException(nameof(colour), colour, "A colour no small button is drawn in."),
        };

        private static LetteringStyle LetteringOn(SmallButtonColour colour) => colour switch
        {
            SmallButtonColour.Green => LetteringStyle.OnButton,
            SmallButtonColour.Blue => LetteringStyle.OnBlue,
            _ => throw new ArgumentOutOfRangeException(nameof(colour), colour, "A colour no small button is drawn in."),
        };

        /// <summary>How each mark stands before a label, as the design sets it (ShopTR).</summary>
        private static MarkPlace PlaceOf(SmallButtonMark mark) => mark switch
        {
            SmallButtonMark.None => new MarkPlace(drawing: null, scale: 1f, room: 0f, middleInItsRoom: 0f, aboveMiddle: 0f, gapToLabel: 0f),

            // The coin's box of 100 is 23 points across; it hangs 3 points over the row's left end and 1 into the gap, and stands 2 higher than the row.
            SmallButtonMark.Coin => new MarkPlace(GalleryArt.IconCoin, scale: 23f / 100f, room: 19f, middleInItsRoom: 8.5f, aboveMiddle: 2f, gapToLabel: 4f),

            // The triangle, drawn 24 points across, is 21 here and stands on the row's middle.
            SmallButtonMark.Play => new MarkPlace(GalleryArt.GlyphPlay, scale: 21f / 24f, room: 21f, middleInItsRoom: 10.5f, aboveMiddle: 0f, gapToLabel: 3f),
            _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, "A mark no small button is drawn with."),
        };

        /// <summary>Where a mark stands in the row it shares with the label.</summary>
        private readonly struct MarkPlace
        {
            public MarkPlace(GalleryArt? drawing, float scale, float room, float middleInItsRoom, float aboveMiddle, float gapToLabel)
            {
                Drawing = drawing;
                Scale = scale;
                Room = room;
                MiddleInItsRoom = middleInItsRoom;
                AboveMiddle = aboveMiddle;
                GapToLabel = gapToLabel;
            }

            /// <summary>The mark's drawing; nothing for a button of words alone.</summary>
            public GalleryArt? Drawing { get; }

            /// <summary>The share of its own drawing's size the mark is drawn at.</summary>
            public float Scale { get; }

            /// <summary>How much of the row's width the mark takes, which may be less than it is across.</summary>
            public float Room { get; }

            /// <summary>Where the mark's middle lies from the left end of its room.</summary>
            public float MiddleInItsRoom { get; }

            /// <summary>How far above the pill's middle the mark's middle stands.</summary>
            public float AboveMiddle { get; }

            public float GapToLabel { get; }
        }
    }
}
