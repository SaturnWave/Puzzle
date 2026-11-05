#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// One of the two arrows that turn the pages of a card's text (the privacy card, the credits card): the settings
    /// rows' small teal disc with its white chevron, the one to the page before turned round. A finger anywhere within
    /// a row's half height of its middle presses it; the arrow at the first or the last page is dimmed and is not
    /// listened to.
    /// </summary>
    internal sealed class PageArrow : IPressable
    {
        /// <summary>A finger presses the arrow this far from its middle: half a settings row, as large a reach as a row's.</summary>
        private const float Reach = SettingsLayout.RowHeight / 2f;

        private const float PressedDown = 1.5f;

        private readonly Transform _arrow;
        private readonly Vector2 _middle;
        private bool _isDimmed;

        /// <param name="raisedBy">Added to the arrow's places in the drawing order: that of the card it stands on.</param>
        public PageArrow(string name, Transform card, ArtCatalogue art, Vector2 middle, bool pointsBack, int raisedBy)
        {
            _middle = middle;
            _arrow = new GameObject(name).transform;
            _arrow.SetParent(card, worldPositionStays: false);
            _arrow.localPosition = middle;
            if (pointsBack)
            {
                _arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
            }

            SceneObjects.AddSprite("Disc", _arrow, art.Of(GalleryArt.TinyTealDisc), art.SpriteMaterial, DrawOrder.CardDisc + raisedBy);
            SceneObjects.AddSprite("Glyph", _arrow, art.Of(GalleryArt.GlyphChevron), art.SpriteMaterial, DrawOrder.CardDisc + (int)DiscPart.Picture + raisedBy);
        }

        /// <summary>The arrow's middle, in the card's points.</summary>
        public Vector2 Middle => _middle;

        public bool IsUnder(Vector2 point) => (point - _middle).sqrMagnitude <= Reach * Reach;

        public void ShowPressed(bool isPressed)
        {
            _arrow.localPosition = _middle - new Vector2(0f, isPressed ? PressedDown : 0f);
            Colour(isHeld: isPressed);
        }

        public void Dim(bool isDimmed)
        {
            _isDimmed = isDimmed;
            Colour(isHeld: false);
        }

        private void Colour(bool isHeld)
        {
            foreach (SpriteRenderer part in _arrow.GetComponentsInChildren<SpriteRenderer>())
            {
                part.color = Dimming.Of(_isDimmed, isHeld);
            }
        }
    }
}
