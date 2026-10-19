#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Stage;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>How a card hangs in front of the dark, as the design canvas draws its two kinds of card. A closed set.</summary>
    public enum CardHanging
    {
        /// <summary>
        /// On two picture wires that run up out of the screen and end in pins on the nameplate's top edge: the
        /// cards that tell how a level went (WinTR, LoseTR).
        /// </summary>
        OnWiresFromAbove,

        /// <summary>
        /// On two short wires from pins in the wall just over its top corners: the cards the player opens and
        /// closes (LevelStartTR, SettingsTR). The pins come and go with the card.
        /// </summary>
        FromPinsInTheWall,
    }

    /// <summary>
    /// A cream card that hangs in front of the darkened room with a brass nameplate riding on its top edge, as the
    /// design canvas draws every card of the game (WinTR, LoseTR, LevelStartTR, SettingsTR): a body of any size,
    /// a nameplate of any width with one line or two, the wires it hangs on, and, where the card is one the
    /// player closes, a round button with a cross on its upper right corner. The card comes down into its place
    /// and goes up out of sight; a screen too small for it draws all of it smaller. What stands on the card is its
    /// user's: it is added under <see cref="Content"/>, in the card's own points around the middle of its body,
    /// and what of it can be pressed is told to the card, which answers what a finger pressed.
    /// </summary>
    /// <remarks>
    /// The card has no clock of its own: whoever hangs it tells it how far it has come down (<see cref="ComeDown"/>,
    /// <see cref="GoUp"/>) and where a finger is. Where it hangs is a layout's to say: the board's between the
    /// plaques and the shelf (<see cref="ScreenLayout.PlaceOfACard"/>), a museum page's on the whole usable screen
    /// (<see cref="PageLayout.PlaceOfACard"/>).
    /// <para>
    /// The card's own measures (its nameplate's (<see cref="BrassNameplate"/>), the pins in the wall, the close
    /// button's place) are the design canvas' points. The canvas' cards are 362 points wide on a canvas of 390, wider than the game's
    /// narrow screen leaves room for: a card whose content is laid out in the canvas' points is drawn there at
    /// the share its place gives it, with everything on it. The result's card is older than this shell and is
    /// 332 points wide in the game's own.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPress">What a press on the card asks for.</typeparam>
    public sealed class HangingCard<TPress> : IDisposable
    {
        /// <summary>The card's picture is its body and the room around it for its rim and its dark glow: 60 points wider and higher.</summary>
        private const float CardPictureMargin = 60f;

        /// <summary>Pins in the wall stand this far in from the card's sides and this far over its top edge (LevelStartTR).</summary>
        private const float WallPinInFromSide = 58f;
        private const float WallPinOverTheCard = 20f;

        /// <summary>The button that closes a card: its middle this far in from the card's right side and this far under its top edge (LevelStartTR).</summary>
        private const float CloseInFromSide = 18f;
        private const float CloseUnderTop = 8f;

        /// <summary>
        /// A card out of sight hangs with its lowest point, which is what rides under its lower edge where
        /// something does, this far above the screen's top edge, so that its glow is out of sight too.
        /// </summary>
        private const float OutOfSightAbove = 36f;

        private readonly ArtCatalogue _art;
        private readonly Transform _root;

        /// <summary>The card with all that is on it and its wires: what comes down and goes up, inside the card's fixed place.</summary>
        private readonly Transform _hanging;
        private readonly PaintedShape _wires;
        private readonly Vector2 _body;
        private readonly CardNameplate _nameplate;
        private readonly BrassNameplate _plate;
        private readonly CardHanging _hangs;
        private readonly float _ridesBelow;
        private readonly int _raisedBy;
        private readonly PressSet<TPress> _presses = new PressSet<TPress>();

        /// <summary>The button that closes the card, with what a press on it asks for; nothing on a card that has none.</summary>
        private (DiscButton Button, TPress Asks)? _close;

        /// <summary>Where the card was last placed, in the layout's points, and how large it is drawn there.</summary>
        private Vector2 _place;
        private float _scale = 1f;

        /// <summary>How far up, in the card's own points, the card hangs when it is out of sight.</summary>
        private float _wayUp;

        /// <summary>How far up the card hangs now.</summary>
        private float _raised;

        /// <summary>Builds a card, out of sight above the screen. Call <see cref="Place"/> before the first picture.</summary>
        /// <param name="body">The width and the height of the card's cream body.</param>
        /// <param name="ridesBelow">How far what stands on the card reaches under its lower edge (LevelStartTR: the play button); nothing for a card that holds all of its own.</param>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> for a card in front of another card.</param>
        public HangingCard(
            string name, Transform parent, ArtCatalogue art, Vector2 body, CardNameplate nameplate, CardHanging hangs,
            float ridesBelow = 0f, int raisedBy = 0)
        {
            _art = art;
            _body = body;
            _nameplate = nameplate;
            _hangs = hangs;
            _ridesBelow = ridesBelow;
            _raisedBy = raisedBy;
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            _hanging = new GameObject("Card").transform;
            _hanging.SetParent(_root, worldPositionStays: false);

            _wires = PaintedShape.Create("Wires", _hanging, art.PaintMaterial, DrawOrder.CardWire + raisedBy);
            SpriteRenderer face = SceneObjects.AddSprite("Body", _hanging, art.Of(GalleryArt.Card), art.SpriteMaterial, DrawOrder.Card + raisedBy);
            face.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its rim and corners keep their shape
            face.size = body + new Vector2(CardPictureMargin, CardPictureMargin);

            // The plate rides on the card's top edge, half of it above the card.
            _plate = new BrassNameplate(
                "Nameplate", _hanging, art, nameplate,
                DrawOrder.CardNameplate + raisedBy, DrawOrder.CardNameplateLettering + raisedBy, DrawOrder.CardNameplatePin + raisedBy);
            _plate.Transform.localPosition = At(0f);
            AddThePins();
        }

        /// <summary>The room the card takes on a screen: its body's width, and its body's height with what rides over its top edge and under its lower one.</summary>
        public float Width => _body.x;

        public float Height => _body.y + RidesAbove + _ridesBelow;

        /// <summary>What stands on the card is added under this, in the card's own points around the middle of its body.</summary>
        public Transform Content => _hanging;

        /// <summary>What the card adds to the places in the drawing order of all that stands on it.</summary>
        public int RaisedBy => _raisedBy;

        /// <summary>How large the card is drawn where it was last placed.</summary>
        public float Scale => _scale;

        /// <summary>The height in the layout of the lower edge of the card's body, where it hangs in its place.</summary>
        public float Foot => _place.y - _scale * _body.y / 2f;

        /// <summary>The middle of the button that closes the card, in the layout's points: where a finger presses it. Nothing on a card that has none.</summary>
        public Vector2? CloseButtonMiddle => _close.HasValue ? InTheLayout(_close.Value.Button.Middle) : (Vector2?)null;

        /// <summary>The nameplate rides half above the card; pins in the wall stand higher than a low plate.</summary>
        private float RidesAbove => _hangs == CardHanging.FromPinsInTheWall
            ? Mathf.Max(_nameplate.Height / 2f, WallPinOverTheCard)
            : _nameplate.Height / 2f;

        /// <summary>Where something lies on the card, in the card's own points, that is this far under the middle of its top edge.</summary>
        public Vector3 At(float down) => new Vector3(0f, _body.y / 2f - down, 0f);

        /// <summary>Sets the round button with a cross on the card's upper right corner.</summary>
        /// <param name="asks">What a press on it asks for.</param>
        /// <exception cref="InvalidOperationException">The card has such a button already.</exception>
        public void AddCloseButton(TPress asks)
        {
            if (_close.HasValue)
            {
                throw new InvalidOperationException("A card has one button that closes it.");
            }

            var button = new DiscButton("Close", _hanging, _art, DiscGlyph.Close, DrawOrder.CardCloseButton + _raisedBy);
            button.Place(new Vector2(_body.x / 2f - CloseInFromSide, _body.y / 2f - CloseUnderTop));
            _close = (button, asks);
            _presses.Add(button, asks);
        }

        /// <summary>
        /// Tells the card of something on it that can be pressed, and what a press on it asks for. What is told
        /// later lies over what was told before where the two overlap.
        /// </summary>
        public void Listen(IPressable control, TPress asks) => _presses.Add(control, asks);

        /// <summary>Forgets what was told to be pressable, before the card's user builds its controls anew; the button that closes the card stays.</summary>
        public void ForgetTheControls()
        {
            _presses.Clear();
            if (_close.HasValue)
            {
                _presses.Add(_close.Value.Button, _close.Value.Asks);
            }
        }

        /// <summary>
        /// The place where the card's body stands on a foot at a height of the layout, drawn at a scale: where a card
        /// that hangs in front of another stands on that one's foot, so the nameplate of the card behind stays in
        /// sight above it.
        /// </summary>
        public CardPlace PlaceOnAFoot(float foot, float scale) => new CardPlace(foot + scale * (_body.y / 2f + (RidesAbove - _ridesBelow) / 2f), scale);

        /// <summary>Hangs the card at a place a layout has for it; call again whenever the screen changes.</summary>
        /// <param name="place">The middle of the room the card takes (<see cref="Height"/>), and how large it is drawn.</param>
        public void Place(CardPlace place, ScreenView view)
        {
            _scale = place.Scale;

            // The room is the card's with what rides on its edges: the body's own middle lies lower than the room's by
            // half of what rides above it, and higher by half of what rides below.
            _place = new Vector2(0f, place.Middle - _scale * (RidesAbove - _ridesBelow) / 2f);
            _root.localPosition = new Vector3(_place.x, _place.y, 0f);
            _root.localScale = new Vector3(_scale, _scale, 1f);

            // From here on in the card's own points.
            float topOfTheScreen = (view.Height / 2f - _place.y) / _scale;
            float wasRaisedBy = _wayUp > 0f ? _raised / _wayUp : 1f;
            _wayUp = topOfTheScreen + _body.y / 2f + _ridesBelow + OutOfSightAbove;
            Raise(wasRaisedBy * _wayUp);
            _wires.Show(Wires(until: topOfTheScreen + view.Height / _scale));
        }

        /// <summary>The card on its way down: 0 is out of sight above the screen, 1 is in its place, which it meets with a little give.</summary>
        public void ComeDown(float progress) => Raise((1f - Easing.Settle(progress)) * _wayUp);

        /// <summary>The card on its way back up: 0 is in its place, 1 is out of sight.</summary>
        public void GoUp(float progress) => Raise(Easing.In(progress) * _wayUp);

        /// <summary>A finger came down at a point of the layout.</summary>
        public void FingerDown(Vector2 point) => _presses.FingerDown(OnTheCard(point));

        /// <summary>The finger moved while it was down: a control it has slid off is not pressed, one it came back to is.</summary>
        public void FingerMovedTo(Vector2 point) => _presses.FingerMovedTo(OnTheCard(point));

        /// <summary>The finger lifted.</summary>
        /// <returns>True when that pressed a control: the finger came down on it and lifted on it. <paramref name="asks"/> is then what that press asks for.</returns>
        public bool FingerUp(out TPress asks) => _presses.FingerUp(out asks);

        /// <summary>What the control a finger holds on the card asks for, while the finger is still on it (<see cref="PressSet{TAction}.TryGetHeld"/>).</summary>
        public bool TryGetHeld(out TPress asks) => _presses.TryGetHeld(out asks);

        /// <summary>The touch ended without a lift the game saw: nothing is pressed.</summary>
        public void FingerGone() => _presses.FingerGone();

        /// <summary>Where a point of the card as it hangs now lies in the layout.</summary>
        public Vector2 InTheLayout(Vector2 onTheCard) => _place + _scale * (onTheCard + new Vector2(0f, _raised));

        public void Dispose()
        {
            _wires.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        private void Raise(float by)
        {
            _raised = by;
            _hanging.localPosition = new Vector3(0f, by, 0f);
        }

        /// <summary>Where a point of the layout lies on the card as it hangs now, in the card's own points.</summary>
        private Vector2 OnTheCard(Vector2 point) => (point - _place) / _scale - new Vector2(0f, _raised);

        /// <summary>How far to each side of the card's middle its two wires hang.</summary>
        private float WiresFromMiddle => _hangs == CardHanging.FromPinsInTheWall
            ? _body.x / 2f - WallPinInFromSide
            : _plate.PinsFromMiddle;

        /// <summary>The height of the middle of a pin in the wall over the middle of the card's body.</summary>
        private float WallPinHeight => _body.y / 2f + WallPinOverTheCard;

        /// <summary>The two pins the card's wires end in: on the nameplate's top edge, or in the wall over the card's corners.</summary>
        private void AddThePins()
        {
            if (_hangs == CardHanging.OnWiresFromAbove)
            {
                _plate.AddThePins();
                return;
            }

            foreach (float side in new[] { -1f, 1f })
            {
                SpriteRenderer pin = SceneObjects.AddSprite("Pin", _hanging, _art.Of(GalleryArt.WirePin), _art.SpriteMaterial, DrawOrder.CardNameplatePin + _raisedBy);
                pin.transform.localPosition = new Vector3(side * WiresFromMiddle, WallPinHeight, 0f);
            }
        }

        /// <summary>
        /// The two wires: from the nameplate's top edge up to a height far above the screen, for a card that is let
        /// down a long way on them; or the short ones from the card's top edge up to its pins in the wall.
        /// </summary>
        private Mesh Wires(float until)
        {
            var sketch = new MeshSketch();
            bool hangsFromTheWall = _hangs == CardHanging.FromPinsInTheWall;
            float from = hangsFromTheWall ? _body.y / 2f : _body.y / 2f + _nameplate.Height / 2f;
            float upTo = hangsFromTheWall ? WallPinHeight : until;
            PictureWire.Add(sketch, -WiresFromMiddle - PictureWire.Width / 2f, from, upTo);
            PictureWire.Add(sketch, WiresFromMiddle - PictureWire.Width / 2f, from, upTo);
            return sketch.ToMesh("Card wires");
        }
    }
}
