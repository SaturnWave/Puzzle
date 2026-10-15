#nullable enable
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>The measures of a pill-shaped button's drawing, in points at the button's full size.</summary>
    internal readonly struct PillMeasures
    {
        /// <param name="height">The pill's height.</param>
        /// <param name="lip">The dark lip under the pill, which is part of what a finger may hit.</param>
        /// <param name="pictureMargin">How much wider than the pill its picture is: the room at its sides for its rim and shadow.</param>
        /// <param name="pictureHeight">The picture's height: the pill, its lip and the room for its rim and shadow.</param>
        /// <param name="pressedDown">How far a held pill goes down towards its lip.</param>
        public PillMeasures(float height, float lip, float pictureMargin, float pictureHeight, float pressedDown)
        {
            Height = height;
            Lip = lip;
            PictureMargin = pictureMargin;
            PictureHeight = pictureHeight;
            PressedDown = pressedDown;
        }

        public float Height { get; }

        public float Lip { get; }

        public float PictureMargin { get; }

        public float PictureHeight { get; }

        public float PressedDown { get; }
    }

    /// <summary>
    /// What the game's pill-shaped buttons have in common (<see cref="PushButton"/>, <see cref="SmallButton"/>):
    /// a glossy pill of any width that stands on a dark lip, goes down onto it and loses a little light while a
    /// finger holds it, is dimmed and under no finger while it cannot be used, and can be placed, taken out of
    /// sight and removed. What a button says and shows on its pill is the button's own, added under
    /// <see cref="Pill"/>. Sizes are in points around the middle of the pill at its full size.
    /// </summary>
    internal sealed class ButtonPill
    {
        private readonly Transform _root;
        private readonly SpriteRenderer _face;
        private readonly PillMeasures _measures;
        private readonly float _width;
        private bool _isHeld;

        /// <param name="face">The drawing of the pill; its middle stretches sideways to any width.</param>
        /// <param name="width">The pill's width at full size.</param>
        public ButtonPill(string name, Transform parent, Sprite face, Material material, PillMeasures measures, float width, DrawOrder faceOrder)
        {
            _measures = measures;
            _width = width;
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            Pill = new GameObject("Pill").transform;
            Pill.SetParent(_root, worldPositionStays: false);

            _face = SceneObjects.AddSprite("Face", Pill, face, material, faceOrder);
            _face.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
            _face.size = new Vector2(width + measures.PictureMargin, measures.PictureHeight);
        }

        /// <summary>The pill with all that stands on it: what goes down while the button is held.</summary>
        public Transform Pill { get; }

        /// <summary>The middle of the pill, in the points of what the button stands on.</summary>
        public Vector2 Middle => _root.localPosition;

        /// <summary>True while the button cannot be used: it is drawn dimmed and is under no finger.</summary>
        public bool IsDimmed { get; private set; }

        /// <summary>Puts the middle of the pill at a place of what the button stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale)
        {
            _root.localPosition = new Vector3(middle.x, middle.y, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Shows the button, or takes it out of sight; a button out of sight is not under any finger.</summary>
        public void Show(bool isShown) => _root.gameObject.SetActive(isShown);

        /// <summary>Takes the button away for good.</summary>
        public void Remove() => SceneObjects.DestroyObjectOf(_root);

        /// <summary>Draws the pill's face dimmed, or lit again; what stands on the pill is the button's to dim.</summary>
        public void Dim(bool isDimmed)
        {
            IsDimmed = isDimmed;
            ShowTheFace();
        }

        /// <param name="point">A point of what the button stands on.</param>
        public bool IsUnder(Vector2 point) => !IsDimmed && Covers(point);

        /// <summary>True when a point lies on the pill or its lip of a button in sight, whether it is dimmed or not.</summary>
        /// <param name="point">A point of what the button stands on.</param>
        public bool Covers(Vector2 point)
        {
            if (!_root.gameObject.activeSelf)
            {
                return false;
            }

            Vector2 fromMiddle = (point - Middle) / _root.localScale.x;
            bool isBesideIt = Mathf.Abs(fromMiddle.x) > _width / 2f;
            bool isOverOrUnderIt = fromMiddle.y > _measures.Height / 2f || fromMiddle.y < -_measures.Height / 2f - _measures.Lip;
            return !isBesideIt && !isOverOrUnderIt;
        }

        public void ShowPressed(bool isPressed)
        {
            _isHeld = isPressed;
            Pill.localPosition = new Vector3(0f, isPressed ? -_measures.PressedDown : 0f, 0f);
            ShowTheFace();
        }

        private void ShowTheFace() => _face.color = Dimming.Of(IsDimmed, _isHeld);
    }
}
