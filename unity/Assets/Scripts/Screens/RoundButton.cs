#nullable enable
using Puzzle.Client.Art;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>Which way a round button's arrow points.</summary>
    public enum ArrowWay
    {
        /// <summary>To the left: back, or to what comes before.</summary>
        Back,

        /// <summary>To the right: on to what comes after.</summary>
        Forward,
    }

    /// <summary>
    /// The round teal button with an arrow, as the design canvas draws the way back from a screen (JigsawTR): a disc
    /// in a gilt ring that goes down a little while it is held. Sizes are in points; the disc is 44 across at full size.
    /// </summary>
    public sealed class RoundButton : IPressable
    {
        /// <summary>The disc at full size; a finger may land a little outside it and still press it.</summary>
        public const float Size = 44f;
        private const float FingerBeyondTheDisc = 7f;

        /// <summary>A pressed button goes down towards its lip and loses a little of its light; one that leads nowhere is faint.</summary>
        private const float PressedDown = 2f;
        private static readonly Color PressedShade = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color Faint = new Color(1f, 1f, 1f, 0.32f);

        private readonly Transform _root;
        private readonly SpriteRenderer _face;
        private bool _leadsSomewhere = true;

        public RoundButton(string name, Transform parent, ArtCatalogue art, ArrowWay way, DrawOrder order)
        {
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            _face = SceneObjects.AddSprite("Face", _root, art.Of(GalleryArt.BackButton), art.SpriteMaterial, order);
            _face.flipX = way == ArrowWay.Forward; // the same drawing, turned around; its light still comes from above
        }

        /// <summary>The middle of the disc, in the points of what the button stands on.</summary>
        public Vector2 Middle => _root.localPosition;

        /// <summary>Puts the middle of the disc at a place of what the button stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale = 1f)
        {
            _root.localPosition = new Vector3(middle.x, middle.y, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Moves the button to another place in the drawing order.</summary>
        public void DrawAt(DrawOrder order) => _face.sortingOrder = (int)order;

        /// <summary>Shows the button, or takes it out of sight; a button out of sight is not under any finger.</summary>
        public void Show(bool isShown) => _root.gameObject.SetActive(isShown);

        /// <summary>A button that leads nowhere just now is drawn faint and is not under any finger.</summary>
        public void LeadSomewhere(bool leadsSomewhere)
        {
            _leadsSomewhere = leadsSomewhere;
            _face.color = leadsSomewhere ? Color.white : Faint;
        }

        /// <param name="point">A point of what the button stands on.</param>
        public bool IsUnder(Vector2 point)
        {
            float reach = Size / 2f * _root.localScale.x + FingerBeyondTheDisc;
            return _leadsSomewhere && _root.gameObject.activeSelf && (point - Middle).sqrMagnitude <= reach * reach;
        }

        public void ShowPressed(bool isPressed)
        {
            _face.transform.localPosition = new Vector3(0f, isPressed ? -PressedDown : 0f, 0f);
            if (_leadsSomewhere)
            {
                _face.color = isPressed ? PressedShade : Color.white;
            }
        }
    }
}
