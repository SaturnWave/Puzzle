#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The switch of a setting, as the design canvas draws it on the settings card (SettingsTR): a pill-shaped track,
    /// lit green with a white tick when the switch is on and dull cream when it is off, and a cream knob that stands
    /// at the track's right end when on and at its left end when off. A press flips it (<see cref="Flip"/>), and its
    /// owner slides the knob across over the time it chooses (<see cref="Slide"/>) while the lit track fades in or
    /// out (<see cref="ToggleLook"/>). Sizes are the canvas' points around the middle of the switch's button, 70 by
    /// 44, whose track lies a little over its middle.
    /// </summary>
    public sealed class Toggle : IPressable
    {
        /// <summary>The button a finger presses: the canvas' 70 by 44 around the track.</summary>
        public const float ButtonWidth = 70f;
        public const float ButtonHeight = 44f;

        /// <summary>The track's middle lies this far over the button's middle (SettingsTR: the track from 9 to 32 of 44).</summary>
        private const float TrackOverMiddle = 1.5f;

        /// <summary>A held switch's knob goes this far down and loses a little of its light.</summary>
        private const float PressedDown = 1f;

        private readonly Transform _root;
        private readonly SpriteRenderer _offTrack;
        private readonly SpriteRenderer _onTrack;
        private readonly SpriteRenderer _knob;
        private float _pressedDown;

        /// <param name="order">The dull track's place in the drawing order; the lit track takes the place above it, and the knob the one above that.</param>
        public Toggle(string name, Transform parent, ArtCatalogue art, bool isOn, DrawOrder order)
        {
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            _offTrack = SceneObjects.AddSprite("Track off", _root, art.Of(GalleryArt.ToggleOff), art.SpriteMaterial, order);
            _onTrack = SceneObjects.AddSprite("Track on", _root, art.Of(GalleryArt.ToggleOn), art.SpriteMaterial, order + 1);
            _knob = SceneObjects.AddSprite("Knob", _root, art.Of(GalleryArt.ToggleKnob), art.SpriteMaterial, order + 2);
            _offTrack.transform.localPosition = new Vector3(0f, TrackOverMiddle, 0f);
            _onTrack.transform.localPosition = new Vector3(0f, TrackOverMiddle, 0f);
            Set(isOn);
        }

        /// <summary>The state the switch is in, or is sliding to.</summary>
        public bool IsOn { get; private set; }

        /// <summary>The middle of the switch's button, in the points of what it stands on.</summary>
        public Vector2 Middle => _root.localPosition;

        /// <summary>Puts the middle of the switch's button at a place of what it stands on.</summary>
        public void Place(Vector2 middle) => _root.localPosition = new Vector3(middle.x, middle.y, 0f);

        /// <summary>Shows the switch, or takes it out of sight (a card whose build has no such setting).</summary>
        public void ShowInSight(bool isInSight) => _root.gameObject.SetActive(isInSight);

        /// <summary>Puts the switch in a state at once, its knob at rest.</summary>
        public void Set(bool isOn)
        {
            IsOn = isOn;
            Show(ToggleLook.AtRest(isOn));
        }

        /// <summary>The switch goes to the other state; its knob still stands where it was until it is slid (<see cref="Slide"/>).</summary>
        public void Flip() => IsOn = !IsOn;

        /// <summary>Shows the knob so far on its way to the state the switch is in, from 0 (where it stood) to 1 (at rest).</summary>
        public void Slide(float progress) => Show(ToggleLook.Sliding(IsOn, progress));

        /// <param name="point">A point of what the switch stands on.</param>
        public bool IsUnder(Vector2 point) =>
            Mathf.Abs(point.x - Middle.x) <= ButtonWidth / 2f && Mathf.Abs(point.y - Middle.y) <= ButtonHeight / 2f;

        public void ShowPressed(bool isPressed)
        {
            _pressedDown = isPressed ? PressedDown : 0f;
            _knob.color = Dimming.Of(isDimmed: false, isHeld: isPressed);
            Vector3 knob = _knob.transform.localPosition;
            _knob.transform.localPosition = new Vector3(knob.x, TrackOverMiddle - _pressedDown, 0f);
        }

        private void Show(ToggleLook look)
        {
            _knob.transform.localPosition = new Vector3(look.KnobOffset, TrackOverMiddle - _pressedDown, 0f);
            _onTrack.color = new Color(1f, 1f, 1f, look.LitTrack);
        }
    }
}
