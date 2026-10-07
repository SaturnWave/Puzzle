#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The card that is hung in front of the darkened room once a game is decided, as the web test page shows its
    /// result panel (result-panel.js, gallery.css): a brass nameplate that says what happened, an alcove with what
    /// the game left behind, two lines about the level, and the one green button that leads on. The card hangs on
    /// two picture wires and comes down and goes up on them. Sizes are in points around the middle of the card at
    /// its full size; a screen too small for that draws the whole card smaller.
    /// </summary>
    /// <remarks>
    /// The panel has no clock of its own. The stage tells it how far it has come down (<see cref="ComeDown"/>), how
    /// long its exhibits have been coming (<see cref="ShowExhibits"/>), what moment its sparkles are at
    /// (<see cref="Twinkle"/>), and where a finger is; the panel answers whether the finger pressed the button.
    /// </remarks>
    public sealed class ResultPanel : IDisposable
    {
        /// <summary>The card's body. Its height is what stands on it, from the top edge down, as the measures below place it.</summary>
        private const float CardWidth = 332f;
        private const float CardHeight = 429f;

        /// <summary>The nameplate is 40 points high and rides on the card's top edge, half of it above the card.</summary>
        private const float NameplateAboveCard = 20f;

        /// <summary>The room a card takes on a screen: its body and the half of the nameplate above it.</summary>
        public const float Width = CardWidth;
        public const float Height = CardHeight + NameplateAboveCard;

        /// <summary>The card's picture is its body and the room around it for its rim and its dark glow: 60 points wider and higher.</summary>
        private const float CardPictureMargin = 60f;

        /// <summary>How far under the card's top edge the middle of each thing lies.</summary>
        private const float AlcoveDown = 131f;
        private const float HeadlineDown = 257.5f;
        private const float FootnoteDown = 281f;
        private const float RuleDown = 306.5f;
        private const float ButtonDown = 366f;

        private const float TitleSize = 19.5f;

        /// <summary>The title keeps clear of the nameplate's two studs.</summary>
        private const float TitleRoom = 232f;
        private const float HeadlineSize = 19f;
        private const float FootnoteSize = 13.5f;

        /// <summary>The lines under the alcove keep this far from the card's sides.</summary>
        private const float LineRoom = CardWidth - 2f * 24f;

        /// <summary>The design's rule: a gilt line with a cut diamond in its middle.</summary>
        private const float RuleWidth = 180f;
        private const float RuleThickness = 1.4f;
        private const float RuleGapAroundDiamond = 10f;
        private const float DiamondEdgeSize = 11f;
        private const float DiamondSize = 8.6f;
        private static readonly Color RuleGilt = Tint.Of(0xE0A63A);
        private static readonly Color DiamondEdge = Tint.Of(0x5E3305);
        private static readonly Color DiamondLight = Tint.Of(0xFFF0B8);
        private static readonly Color DiamondGilt = Tint.Of(0xF6B93C);
        private static readonly Color DiamondDark = Tint.Of(0xC8841A);

        /// <summary>The button's pill, and the lip under it that is part of what a finger may hit.</summary>
        private const float ButtonWidth = 290f;
        private const float ButtonHeight = 72f;
        private const float ButtonLip = 7f;

        /// <summary>The button's picture is its pill and the room around it for its rim, lip and shadow: 40 points wider, 112 high.</summary>
        private const float ButtonPictureMargin = 40f;
        private const float ButtonPictureHeight = 112f;
        private const float ButtonLabelSize = 28f;

        /// <summary>The label is drawn in an outline, which brings its letters closer: they stand apart by this many hundredths of their size.</summary>
        private const float ButtonLabelSpacing = 3f;
        private const float ButtonLabelRoom = ButtonWidth - 2f * 26f;

        /// <summary>The label stands a little above the pill's middle: the pill's foot is its dark edge.</summary>
        private const float ButtonLabelAboveMiddle = 2.5f;

        /// <summary>A pressed button goes down towards its lip and loses a little of its light.</summary>
        private const float PressedDown = 3f;
        private static readonly Color PressedShade = new Color(0.88f, 0.88f, 0.88f, 1f);

        /// <summary>The card's two wires end in the nameplate's pins, this far to each side of the middle.</summary>
        private const float WireFromMiddle = 54f;

        /// <summary>A card out of sight hangs this far above the screen's top edge, so that its glow is out of sight too.</summary>
        private const float OutOfSightAbove = 36f;

        private readonly Transform _root;

        /// <summary>The card with all that is on it and its wires: what comes down and goes up, inside the panel's fixed place.</summary>
        private readonly Transform _hanging;
        private readonly PaintedShape _wires;
        private readonly PaintedShape _rule;
        private readonly ResultAlcove _alcove;
        private readonly Transform _button;
        private readonly SpriteRenderer _buttonFace;

        /// <summary>Where the panel was last placed, in the layout's points, and how large it is drawn there.</summary>
        private Vector2 _place;
        private float _scale = 1f;

        /// <summary>How far up, in the card's own points, the card hangs when it is out of sight.</summary>
        private float _wayUp;

        /// <summary>How far up the card hangs now.</summary>
        private float _raised;

        /// <summary>A finger came down on the button and has not lifted; and whether it is still on the button.</summary>
        private bool _isFingerHeld;
        private bool _isFingerOnButton;

        private ResultPanel(Transform parent, ArtCatalogue art, EffectArt effects, LevelResult result)
        {
            _root = new GameObject("Result").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _hanging = new GameObject("Card").transform;
            _hanging.SetParent(_root, worldPositionStays: false);

            _wires = PaintedShape.Create("Wires", _hanging, art.PaintMaterial, DrawOrder.CardWire);
            SpriteRenderer body = SceneObjects.AddSprite("Body", _hanging, art.Of(GalleryArt.Card), art.SpriteMaterial, DrawOrder.Card);
            body.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its rim and corners keep their shape
            body.size = new Vector2(CardWidth + CardPictureMargin, CardHeight + CardPictureMargin);

            _alcove = ResultAlcove.Create(_hanging, art, effects, result);
            _alcove.Transform.localPosition = At(AlcoveDown);

            WriteLine("Headline", ResultWords.Headline(result), art, HeadlineSize, Lettering.Ink, HeadlineDown);
            WriteLine("Footnote", ResultWords.Footnote(result), art, FootnoteSize, Lettering.SoftInk, FootnoteDown);
            _rule = PaintedShape.Create("Rule", _hanging, art.PaintMaterial, DrawOrder.CardArt);
            _rule.Transform.localPosition = At(RuleDown);
            _rule.Show(Rule());

            _button = new GameObject("Button").transform;
            _button.SetParent(_hanging, worldPositionStays: false);
            _buttonFace = AddButtonFace(art, ResultWords.GoOn(result));
            AddNameplate(art, ResultWords.Title(result));
            ShowButton();
        }

        /// <summary>The middle of the button, in the layout's points: where a finger presses it.</summary>
        public Vector2 ButtonMiddle => _place + _scale * (Vector2)(At(ButtonDown) + new Vector3(0f, _raised, 0f));

        /// <summary>Builds the card of a result, out of sight above the screen. Call <see cref="Place"/> before the first picture.</summary>
        public static ResultPanel Create(Transform parent, ArtCatalogue art, EffectArt effects, LevelResult result) =>
            new ResultPanel(parent, art, effects, result);

        /// <summary>Hangs the card where the layout has room for it; call again whenever the screen changes.</summary>
        public void Place(ScreenLayout layout)
        {
            CardPlace place = layout.PlaceOfACard(Width, Height);
            _scale = place.Scale;

            // The room is the card's and the nameplate's above it: the card's own middle lies half the nameplate's rise lower.
            _place = new Vector2(0f, place.Middle - _scale * NameplateAboveCard / 2f);
            _root.localPosition = new Vector3(_place.x, _place.y, 0f);
            _root.localScale = new Vector3(_scale, _scale, 1f);

            // From here on in the card's own points.
            float topOfTheScreen = (layout.ViewHeight / 2f - _place.y) / _scale;
            float wasRaisedBy = _wayUp > 0f ? _raised / _wayUp : 1f;
            _wayUp = topOfTheScreen + CardHeight / 2f + OutOfSightAbove;
            Raise(wasRaisedBy * _wayUp);
            _wires.Show(Wires(until: topOfTheScreen + layout.ViewHeight / _scale));
        }

        /// <summary>The card on its way down: 0 is out of sight above the screen, 1 is in its place, which it meets with a little give.</summary>
        public void ComeDown(float progress) => Raise((1f - Easing.Settle(progress)) * _wayUp);

        /// <summary>The card on its way back up: 0 is in its place, 1 is out of sight.</summary>
        public void GoUp(float progress) => Raise(Easing.In(progress) * _wayUp);

        /// <summary>How long the alcove's exhibits take to pop up.</summary>
        public float ExhibitsSeconds => _alcove.PopSeconds;

        /// <summary>Shows the alcove's exhibits as they are a while after they began to pop up.</summary>
        public void ShowExhibits(float secondsIn) => _alcove.Pop(secondsIn);

        /// <summary>What never stands still on the card, as it is at a moment of the stage's clock.</summary>
        public void Twinkle(float clockSeconds) => _alcove.Twinkle(clockSeconds);

        /// <summary>A finger came down at a point of the layout.</summary>
        public void FingerDown(Vector2 point)
        {
            _isFingerHeld = IsOnButton(point);
            _isFingerOnButton = _isFingerHeld;
            ShowButton();
        }

        /// <summary>The finger moved while it was down: a button it has slid off is not pressed, one it came back to is.</summary>
        public void FingerMovedTo(Vector2 point)
        {
            _isFingerOnButton = IsOnButton(point);
            ShowButton();
        }

        /// <summary>The finger lifted.</summary>
        /// <returns>True when that pressed the button: the finger came down on it and lifted on it.</returns>
        public bool FingerUp()
        {
            bool isPressed = _isFingerHeld && _isFingerOnButton;
            FingerGone();
            return isPressed;
        }

        /// <summary>The touch ended without a lift the game saw: nothing is pressed.</summary>
        public void FingerGone()
        {
            _isFingerHeld = false;
            _isFingerOnButton = false;
            ShowButton();
        }

        public void Dispose()
        {
            _wires.Dispose();
            _rule.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>Where something lies on the card, in the card's own points, that is this far under its top edge.</summary>
        private static Vector3 At(float down) => new Vector3(0f, CardHeight / 2f - down, 0f);

        private void Raise(float by)
        {
            _raised = by;
            _hanging.localPosition = new Vector3(0f, by, 0f);
        }

        private bool IsOnButton(Vector2 point)
        {
            Vector2 onCard = (point - _place) / _scale - new Vector2(0f, _raised);
            Vector2 fromButton = onCard - (Vector2)At(ButtonDown);
            bool isBesideIt = Mathf.Abs(fromButton.x) > ButtonWidth / 2f;
            bool isOverOrUnderIt = fromButton.y > ButtonHeight / 2f || fromButton.y < -ButtonHeight / 2f - ButtonLip;
            return !isBesideIt && !isOverOrUnderIt;
        }

        private void ShowButton()
        {
            bool isPressed = _isFingerHeld && _isFingerOnButton;
            _button.localPosition = At(ButtonDown) + new Vector3(0f, isPressed ? -PressedDown : 0f, 0f);
            _buttonFace.color = isPressed ? PressedShade : Color.white;
        }

        private SpriteRenderer AddButtonFace(ArtCatalogue art, string label)
        {
            SpriteRenderer face = SceneObjects.AddSprite("Face", _button, art.Of(GalleryArt.Button), art.SpriteMaterial, DrawOrder.CardArt);
            face.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
            face.size = new Vector2(ButtonWidth + ButtonPictureMargin, ButtonPictureHeight);

            TextMeshPro line = Lettering.Write("Label", _button, art, LetteringStyle.OnButton, ButtonLabelSize, Color.white, DrawOrder.CardLettering);
            line.transform.localPosition = new Vector3(0f, ButtonLabelAboveMiddle, 0f);
            line.characterSpacing = ButtonLabelSpacing;
            Lettering.SayWithin(line, label, ButtonLabelSize, ButtonLabelRoom);
            return face;
        }

        private void AddNameplate(ArtCatalogue art, string title)
        {
            SpriteRenderer plate = SceneObjects.AddSprite("Nameplate", _hanging, art.Of(GalleryArt.Nameplate), art.SpriteMaterial, DrawOrder.CardNameplate);
            plate.transform.localPosition = At(0f);
            TextMeshPro line = Lettering.Write("Title", plate.transform, art, LetteringStyle.Label, TitleSize, Lettering.Ink, DrawOrder.CardNameplateLettering);
            Lettering.SayWithin(line, title, TitleSize, TitleRoom);
        }

        private void WriteLine(string name, string words, ArtCatalogue art, float size, Color colour, float down)
        {
            TextMeshPro line = Lettering.Write(name, _hanging, art, LetteringStyle.Label, size, colour, DrawOrder.CardLettering);
            line.transform.localPosition = At(down);
            Lettering.SayWithin(line, words, size, LineRoom);
        }

        /// <summary>The two wires, from the nameplate's top edge up to a height far above the screen: the card is let down a long way on them.</summary>
        private static Mesh Wires(float until)
        {
            var sketch = new MeshSketch();
            float from = CardHeight / 2f + NameplateAboveCard;
            PictureWire.Add(sketch, -WireFromMiddle - PictureWire.Width / 2f, from, until);
            PictureWire.Add(sketch, WireFromMiddle - PictureWire.Width / 2f, from, until);
            return sketch.ToMesh("Card wires");
        }

        /// <summary>The rule, around its own middle: a line to each side of a diamond, which is lit from its upper left.</summary>
        private static Mesh Rule()
        {
            var sketch = new MeshSketch();
            float lineLength = RuleWidth / 2f - RuleGapAroundDiamond;
            sketch.AddRectangle(new Rect(-RuleWidth / 2f, -RuleThickness / 2f, lineLength, RuleThickness), RuleGilt);
            sketch.AddRectangle(new Rect(RuleGapAroundDiamond, -RuleThickness / 2f, lineLength, RuleThickness), RuleGilt);
            AddDiamond(sketch, DiamondEdgeSize, DiamondEdge, DiamondEdge, DiamondEdge);
            AddDiamond(sketch, DiamondSize, DiamondLight, DiamondGilt, DiamondDark);
            return sketch.ToMesh("Rule");
        }

        /// <summary>A square on its corner, of a given width, whose colour runs from its left corner over its upper and lower ones to its right corner.</summary>
        private static void AddDiamond(MeshSketch sketch, float size, Color left, Color upperAndLower, Color right)
        {
            float reach = size / 2f;
            int leftCorner = sketch.AddCorner(new Vector2(-reach, 0f), left);
            int upperCorner = sketch.AddCorner(new Vector2(0f, reach), upperAndLower);
            int rightCorner = sketch.AddCorner(new Vector2(reach, 0f), right);
            int lowerCorner = sketch.AddCorner(new Vector2(0f, -reach), upperAndLower);
            sketch.AddQuad(leftCorner, upperCorner, rightCorner, lowerCorner);
        }
    }
}
