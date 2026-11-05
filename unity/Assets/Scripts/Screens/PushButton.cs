#nullable enable
using System;
using System.Collections.Generic;
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
    /// right corner. A button that sells something has one line and under it a dark pill sunk into its face, with
    /// a coin and the price (LoseTR, "+5 hamle"). What a button says can be said anew, and a button that cannot be
    /// used just now is dimmed. Sizes are in points around the middle of the pill at its full size; whoever
    /// places the button may draw it smaller.
    /// </summary>
    /// <remarks>
    /// A mark is a drawing in a box of 100 points, 46 across, at the pill's left end. A glyph is drawn at its own
    /// size and centred together with the words of a one-line label, as the design's blue "Reklam izle" has its white
    /// triangle (LoseTR).
    /// </remarks>
    public sealed class PushButton : IPressable
    {
        /// <summary>The pill's height, and the lip under it that is part of what a finger may hit.</summary>
        public const float Height = 72f;
        public const float Lip = 7f;

        /// <summary>
        /// The button's picture is its pill and the room around it for its rim, lip and shadow: 40 points wider,
        /// 112 high. A pressed button goes 3 points down towards its lip.
        /// </summary>
        private static readonly PillMeasures Measures = new PillMeasures(Height, Lip, pictureMargin: 40f, pictureHeight: 112f, pressedDown: 3f);

        /// <summary>The label keeps this far from each end of the pill, where the pill is round.</summary>
        private const float LabelInFromEnds = 22f;

        /// <summary>
        /// The label's line is centred a little above the pill's middle, as the design has it (HomeTR, "Bölüm 47":
        /// the line stands on the face with 5 points more under it than over it): the pill's foot is its dark edge.
        /// </summary>
        private const float LabelAboveMiddle = 2.5f;

        /// <summary>The two lines of a label stand this far apart, as a share of their type size.</summary>
        private const float LineApart = 1.02f;

        /// <summary>A mark is drawn in a box of 100 points; on a button it is this large unless the button says otherwise, begins this far from the pill's end, and its label begins this far after it.</summary>
        private const float MarkBox = 100f;
        public const float MarkSize = 46f;
        private const float MarkInFromEnd = 10f;
        private const float GapMarkToLabel = 2f;

        /// <summary>The badge: its middle lies this far in from the pill's right end and this far over its top edge.</summary>
        private const float BadgeInFromEnd = 10f;
        private const float BadgeOverTop = 1f;

        /// <summary>
        /// The button that sells (LoseTR, "+5 hamle" over "[fiyat]"): the middle of the label's line and the
        /// middle of the price's pill, from the middle of the button's pill. The design stacks a line of 20 points
        /// and a pill of 21 with 5 between them, and centres that with 5 more under it than over it.
        /// </summary>
        private const float LabelAboveMiddleOverAPrice = 15.5f;
        private const float PriceUnderMiddle = 10f;

        /// <summary>A glyph stands this far before the words it is centred with (LoseTR, the triangle of "Reklam izle").</summary>
        private const float GapGlyphToWords = 5f;

        private readonly ButtonPill _pill;
        private readonly SpriteRenderer? _mark;
        private readonly PricePill? _price;

        /// <summary>The glyph centred together with the words, and how wide it is drawn; nothing on a button without one.</summary>
        private readonly (SpriteRenderer Picture, float Width)? _glyph;
        private readonly ArtCatalogue _art;
        private readonly ButtonLook _look;
        private readonly DrawOrder _letteringOrder;
        private readonly float _width;
        private readonly float _labelSize;

        /// <summary>How far apart the label's words stand, as a part of the type size; nothing where the label's style says it.</summary>
        private readonly float? _wordsApart;

        /// <summary>Where the label's lines are centred along the pill, and how much room they have there.</summary>
        private readonly float _labelMiddle;
        private readonly float _labelRoom;

        /// <summary>How far over its usual height the label stands, to leave room under it on the face.</summary>
        private readonly float _labelLift;

        /// <summary>Where the mark ends at its right, along the face; the pill's left end where the button has no mark.</summary>
        private readonly float _markRight;

        /// <summary>The label's lines as they are written now, from the upper one down.</summary>
        private readonly List<TextMeshPro> _lines = new List<TextMeshPro>();

        /// <param name="label">What the button says; a line break in it makes two lines.</param>
        /// <param name="width">The pill's width at full size.</param>
        /// <param name="labelSize">The label's type size; a line too long for the pill is set smaller.</param>
        /// <param name="mark">A drawing at the left of the label; none for a button of words alone.</param>
        /// <param name="wordsApart">
        /// How much farther apart than its typeface sets them the label's words stand, as a part of the type size,
        /// where the label's drawing states a gap of its own (JigsawTR, "Otomatik tamamla"); nothing for the gap
        /// of the label's style, which is what the design gives most labels.
        /// </param>
        /// <param name="labelLift">
        /// How far over its usual height the label stands, where something stands under it on the face (HomeTR:
        /// "Tablo" over its pips, <see cref="Face"/>); nothing for a label alone.
        /// </param>
        /// <param name="markSize">How large the mark is drawn: <see cref="MarkSize"/>, or less where the face holds more beside it (HomeTR: the piece over "Tablo" and its pips is 38).</param>
        public PushButton(
            string name, Transform parent, ArtCatalogue art, ButtonLook look, string label, float width, float labelSize,
            DrawOrder faceOrder, DrawOrder letteringOrder, GalleryArt? mark = null, float? wordsApart = null, float labelLift = 0f, float markSize = MarkSize)
            : this(name, parent, art, look, label, price: null, glyph: null, width, labelSize, faceOrder, letteringOrder, mark, wordsApart, labelLift, markSize)
        {
        }

        private PushButton(
            string name, Transform parent, ArtCatalogue art, ButtonLook look, string label, string? price, (GalleryArt Drawing, float Width)? glyph, float width,
            float labelSize, DrawOrder faceOrder, DrawOrder letteringOrder, GalleryArt? mark, float? wordsApart, float labelLift, float markSize)
        {
            _art = art;
            _look = look;
            _letteringOrder = letteringOrder;
            _width = width;
            _labelSize = labelSize;
            _wordsApart = wordsApart;
            _labelLift = labelLift;
            _pill = new ButtonPill(name, parent, art.Of(look.Face), art.SpriteMaterial, Measures, width, faceOrder);

            float labelLeft = -width / 2f + LabelInFromEnds;
            _markRight = -width / 2f;
            if (mark is GalleryArt drawing)
            {
                _mark = SceneObjects.AddSprite("Mark", _pill.Pill, art.Of(drawing), art.SpriteMaterial, letteringOrder);
                _mark.transform.localPosition = new Vector3(-width / 2f + MarkInFromEnd + markSize / 2f, LabelAboveMiddle, 0f);
                _mark.transform.localScale = new Vector3(markSize / MarkBox, markSize / MarkBox, 1f);
                _markRight = -width / 2f + MarkInFromEnd + markSize;
                labelLeft = _markRight + GapMarkToLabel;
            }

            float labelRight = width / 2f - LabelInFromEnds;
            _labelMiddle = (labelLeft + labelRight) / 2f;
            _labelRoom = labelRight - labelLeft;
            if (price != null)
            {
                _price = new PricePill(_pill.Pill, art, faceOrder, letteringOrder);
                _price.Transform.localPosition = new Vector3(0f, -PriceUnderMiddle, 0f);
                _price.Say(price, room: width - 2f * LabelInFromEnds);
            }

            if (glyph.HasValue)
            {
                _glyph = (SceneObjects.AddSprite("Glyph", _pill.Pill, art.Of(glyph.Value.Drawing), art.SpriteMaterial, letteringOrder), glyph.Value.Width);
            }

            Say(label);
        }

        /// <summary>The middle of the pill, in the points of what the button stands on.</summary>
        public Vector2 Middle => _pill.Middle;

        /// <summary>The button's face: what stands on it is added under this, in the pill's points around its middle, and goes down with it when it is held.</summary>
        public Transform Face => _pill.Pill;

        /// <summary>Where the label's lines are centred along the face: in its middle, or between a mark and the right end.</summary>
        public float LabelMiddle => _labelMiddle;

        /// <summary>How wide something under the label may be, centred under it, without reaching the mark.</summary>
        public float RoomUnderTheLabel => 2f * (_labelMiddle - _markRight - GapMarkToLabel);

        /// <summary>
        /// A button that sells something for coins, as the design draws the offer of five more moves (LoseTR): its
        /// label on one line, and under it a dark pill sunk into its face with a coin and the price. The pill and
        /// its coin take the two places in the drawing order above the button's face, so the lettering must lie
        /// higher than those: a card's places do (<see cref="DrawOrder.CardArt"/>, <see cref="DrawOrder.CardLettering"/>),
        /// a page button's lie one apart and do not.
        /// </summary>
        /// <param name="label">What the button gives, on one line.</param>
        /// <param name="price">What it costs, as it is written beside the coin.</param>
        /// <exception cref="ArgumentException">The lettering's place leaves no two places between the face and itself.</exception>
        public static PushButton WithAPrice(
            string name, Transform parent, ArtCatalogue art, ButtonLook look, string label, string price, float width, float labelSize,
            DrawOrder faceOrder, DrawOrder letteringOrder)
        {
            if (letteringOrder <= faceOrder + PricePill.PlacesOverTheFace)
            {
                throw new ArgumentException(
                    $"A price's pill and its coin take the {PricePill.PlacesOverTheFace} places over the button's face ({faceOrder}); lettering at {letteringOrder} would be drawn among them.",
                    nameof(letteringOrder));
            }

            return new PushButton(name, parent, art, look, label, price, glyph: null, width, labelSize, faceOrder, letteringOrder, mark: null, wordsApart: null, labelLift: 0f, markSize: MarkSize);
        }

        /// <summary>
        /// A button whose one-line label has a glyph before its words, the two centred together on the pill, as the
        /// design draws the button that plays an ad (LoseTR, the triangle of "Reklam izle", 24 points across, 5 before
        /// the words). A glyph is drawn at its own size, as large as the design shows it.
        /// </summary>
        /// <param name="glyphWidth">How wide the glyph's drawing is, in points.</param>
        public static PushButton WithAGlyph(
            string name, Transform parent, ArtCatalogue art, ButtonLook look, string label, GalleryArt glyph, float glyphWidth, float width, float labelSize,
            DrawOrder faceOrder, DrawOrder letteringOrder) =>
            new PushButton(name, parent, art, look, label, price: null, (glyph, glyphWidth), width, labelSize, faceOrder, letteringOrder, mark: null, wordsApart: null, labelLift: 0f, markSize: MarkSize);

        /// <summary>Puts the middle of the pill at a place of what the button stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale = 1f) => _pill.Place(middle, scale);

        /// <summary>Shows the button, or takes it out of sight; a button out of sight is not under any finger.</summary>
        public void Show(bool isShown) => _pill.Show(isShown);

        /// <summary>Takes the button away for good.</summary>
        public void Remove() => _pill.Remove();

        /// <summary>
        /// Changes what the button says, where it stands: a line break in the words makes two lines, and a line
        /// too long for the pill is set smaller, as when the button was made. A button with a price says one line.
        /// </summary>
        /// <exception cref="ArgumentException">A button with a price or a glyph is given two lines: there is room for one beside them.</exception>
        public void Say(string label)
        {
            string[] words = label.Split('\n');
            if ((_price != null || _glyph.HasValue) && words.Length > 1)
            {
                throw new ArgumentException("A button with a price or a glyph has room for one line beside it.", nameof(label));
            }

            while (_lines.Count > words.Length)
            {
                SceneObjects.DestroyObjectOf(_lines[_lines.Count - 1]);
                _lines.RemoveAt(_lines.Count - 1);
            }

            while (_lines.Count < words.Length)
            {
                _lines.Add(WriteALine($"Label {_lines.Count + 1}"));
            }

            float middleOfTheLines = (_price != null ? LabelAboveMiddleOverAPrice : LabelAboveMiddle) + _labelLift;
            if (_glyph is (SpriteRenderer glyph, float glyphWidth))
            {
                SayBesideTheGlyph(_lines[0], words[0], glyph, glyphWidth, middleOfTheLines);
                return;
            }

            float top = middleOfTheLines + (words.Length - 1) * LineApart * _labelSize / 2f;
            for (int line = 0; line < words.Length; line++)
            {
                // Its letters stand in their line as a stylesheet sets them.
                TextMeshPro written = _lines[line];
                Lettering.SayWithin(written, words[line], _labelSize, _labelRoom);
                float lineMiddle = top - line * LineApart * _labelSize;
                written.transform.localPosition = new Vector3(_labelMiddle, lineMiddle - Lettering.FigureDrop(_look.Lettering) * written.fontSize, 0f);
            }
        }

        /// <summary>The glyph and the words after it, centred together on the label's place, the glyph's middle level with the line's.</summary>
        private void SayBesideTheGlyph(TextMeshPro line, string words, SpriteRenderer glyph, float glyphWidth, float lineMiddle)
        {
            Lettering.SayWithin(line, words, _labelSize, _labelRoom - glyphWidth - GapGlyphToWords);
            float wordsWidth = Lettering.WidthOf(line, words);
            float left = _labelMiddle - (glyphWidth + GapGlyphToWords + wordsWidth) / 2f;
            glyph.transform.localPosition = new Vector3(left + glyphWidth / 2f, lineMiddle, 0f);
            line.transform.localPosition = new Vector3(left + glyphWidth + GapGlyphToWords + wordsWidth / 2f, lineMiddle - Lettering.FigureDrop(_look.Lettering) * line.fontSize, 0f);
        }

        /// <summary>Changes the price the button states beside its coin; the dark pill widens or narrows with it.</summary>
        /// <exception cref="InvalidOperationException">The button states no price.</exception>
        public void SayPrice(string price)
        {
            if (_price == null)
            {
                throw new InvalidOperationException("Only a button that was made with a price states one.");
            }

            _price.Say(price, room: _width - 2f * LabelInFromEnds);
        }

        /// <summary>
        /// Draws the button dimmed, or lit again: a dimmed button cannot be used just now. It stays where it is and
        /// says what it says, and it is not under any finger.
        /// </summary>
        public void Dim(bool isDimmed)
        {
            _pill.Dim(isDimmed);
            Color colour = Dimming.Of(isDimmed);
            foreach (TextMeshPro line in _lines)
            {
                Lettering.DrawIn(line, colour);
            }

            if (_mark != null)
            {
                _mark.color = colour;
            }

            if (_glyph is (SpriteRenderer glyph, _))
            {
                glyph.color = colour;
            }

            _price?.DrawIn(colour);
        }

        /// <summary>Sets a red badge with a count on the pill's upper right corner.</summary>
        public void AddBadge(int count)
        {
            Transform badge = CountBadge.Add("Badge", _pill.Pill, _art, BadgeFace.Waiting(count), _letteringOrder + 1).Transform;
            badge.localPosition = new Vector3(_width / 2f - BadgeInFromEnd, Height / 2f + BadgeOverTop, 0f);
        }

        /// <param name="point">A point of what the button stands on.</param>
        public bool IsUnder(Vector2 point) => _pill.IsUnder(point);

        public void ShowPressed(bool isPressed) => _pill.ShowPressed(isPressed);

        /// <summary>A line of the label, in the button's lettering and in the light the button has now.</summary>
        private TextMeshPro WriteALine(string name)
        {
            TextMeshPro line = Lettering.Write(name, _pill.Pill, _art, _look.Lettering, _labelSize, Dimming.Of(_pill.IsDimmed), _letteringOrder);
            if (_wordsApart is float apart)
            {
                Lettering.SetWordsApart(line, apart);
            }

            return line;
        }

        /// <summary>
        /// The dark pill a price stands in, sunk into a button's face, as the design draws it (LoseTR): a coin at
        /// its left end and the price after it, the pill as wide as both need. Sizes are in points around the
        /// pill's middle.
        /// </summary>
        private sealed class PricePill
        {
            /// <summary>How many places of the drawing order above the button's face the pill takes: one for itself, one for its coin.</summary>
            public const int PlacesOverTheFace = 2;

            /// <summary>The pill is 21 points high; its picture is the pill and the room around it: 20 points wider, 32 high.</summary>
            private const float PictureMargin = 20f;
            private const float PictureHeight = 32f;

            /// <summary>
            /// The coin's box of 100 points is 20 across and begins 3 from the pill's left end, a little above the
            /// pill's middle; the price begins 3 after the coin and ends 9 before the pill's right end.
            /// </summary>
            private const float CoinBox = 100f;
            private const float CoinSize = 20f;
            private const float CoinInFromEnd = 3f;
            private const float CoinAboveMiddle = 0.5f;
            private const float GapCoinToPrice = 3f;
            private const float PriceInFromEnd = 9f;

            /// <summary>The price, as the design sets it ("[fiyat]": 13.5 points); a price too long for the button is set smaller.</summary>
            private const float PriceSize = 13.5f;

            private readonly SpriteRenderer _pill;
            private readonly SpriteRenderer _coin;
            private readonly TextMeshPro _words;

            /// <param name="faceOrder">The place of the button's face in the drawing order: the pill takes the place above it, the coin the next.</param>
            /// <param name="letteringOrder">The place of the price's words: above the coin's (<see cref="PlacesOverTheFace"/>).</param>
            public PricePill(Transform button, ArtCatalogue art, DrawOrder faceOrder, DrawOrder letteringOrder)
            {
                _pill = SceneObjects.AddSprite("Price", button, art.Of(GalleryArt.PricePill), art.SpriteMaterial, faceOrder + 1);
                _pill.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
                _coin = SceneObjects.AddSprite("Coin", _pill.transform, art.Of(GalleryArt.IconCoin), art.SpriteMaterial, faceOrder + PlacesOverTheFace);
                _coin.transform.localScale = new Vector3(CoinSize / CoinBox, CoinSize / CoinBox, 1f);
                _words = Lettering.Write("Amount", _pill.transform, art, LetteringStyle.OnPricePill, PriceSize, Color.white, letteringOrder);
            }

            public Transform Transform => _pill.transform;

            /// <param name="room">The widest the pill may be: the button's face between its round ends.</param>
            /// <exception cref="InvalidOperationException">The button is too narrow for a coin and a price.</exception>
            public void Say(string price, float room)
            {
                float besideThePrice = CoinInFromEnd + CoinSize + GapCoinToPrice + PriceInFromEnd;
                if (room <= besideThePrice)
                {
                    throw new InvalidOperationException($"A button with {room} points between its round ends has no room for a coin and a price.");
                }

                Lettering.SayWithin(_words, price, PriceSize, room - besideThePrice);
                float priceWidth = Lettering.WidthOf(_words, price);
                float width = besideThePrice + priceWidth;
                _pill.size = new Vector2(width + PictureMargin, PictureHeight);
                _coin.transform.localPosition = new Vector3(-width / 2f + CoinInFromEnd + CoinSize / 2f, CoinAboveMiddle, 0f);
                _words.transform.localPosition = new Vector3(
                    width / 2f - PriceInFromEnd - priceWidth / 2f, -Lettering.FigureDrop(LetteringStyle.OnPricePill) * _words.fontSize, 0f);
            }

            public void DrawIn(Color colour)
            {
                _pill.color = colour;
                _coin.color = colour;
                Lettering.DrawIn(_words, colour);
            }
        }
    }
}
