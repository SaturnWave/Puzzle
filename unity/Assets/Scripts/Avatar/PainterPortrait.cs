#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using UnityEngine;

namespace Puzzle.Client.Avatar
{
    /// <summary>
    /// A bust of the painter in the gallery's small gilt frame, on the teal velvet the gift frame and the shop's packs
    /// show their pictures on (§22): beside a step's words of the tutorial and of the booster intro, and on the wall of
    /// a won level's alcove. His shoulders end at the frame's lower edge, as a portrait's do. It shows one look at a
    /// time (<see cref="Show"/>) and holds only that bust; it shows none until it is told one, and lets go of its bust
    /// when it is shown none or taken down. Sizes are the canvas' points: the frame of the gift (60 by 60, its hole
    /// 50) around the portrait's middle, drawn at a share of them.
    /// </summary>
    public sealed class PainterPortrait : IDisposable
    {
        /// <summary>The frame at its full size, and the hole in it the velvet and the bust fill.</summary>
        public const float FrameSize = 60f;
        private const float FrameHole = 10f;

        /// <summary>The small frame's picture is its body and 28 more points of width, 56 of height (its rim, its glow and its shadow).</summary>
        private const float FramePictureMargin = 28f;
        private const float FramePictureHeightMargin = 56f;

        /// <summary>The velvet is a box of 100 points; the bust is too (<see cref="PainterPictures.BustBox"/>).</summary>
        private const float VelvetBox = 100f;

        private readonly Transform _root;
        private readonly SpriteRenderer _velvet;
        private readonly SpriteRenderer _bust;
        private readonly SpriteRenderer _frame;
        private readonly PainterGallery _painter;

        /// <summary>The look shown and the hold on its bust; nothing while no look is shown.</summary>
        private (PainterMood Mood, PictureShelf<PainterPicture, Sprite>.PictureHold Hold)? _shown;

        public PainterPortrait(string name, Transform parent, ArtCatalogue art, PainterGallery painter, DrawOrder order)
        {
            _painter = painter;
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);

            float hole = FrameSize - FrameHole;
            _velvet = SceneObjects.AddSprite("Velvet", _root, art.Of(GalleryArt.Velvet), art.SpriteMaterial, order + (int)PortraitPart.Velvet);
            _velvet.transform.localScale = new Vector3(hole / VelvetBox, hole / VelvetBox, 1f);
            _bust = SceneObjects.AddSprite("Bust", _root, null, art.SpriteMaterial, order + (int)PortraitPart.Bust);
            _bust.transform.localScale = new Vector3(hole / PainterPictures.BustBox, hole / PainterPictures.BustBox, 1f);
            _frame = SceneObjects.AddSprite("Frame", _root, art.Of(GalleryArt.SmallFrame), art.SpriteMaterial, order + (int)PortraitPart.Frame);
            _frame.drawMode = SpriteDrawMode.Sliced; // the frame's middle stretches both ways; its corners keep their shape
            _frame.size = new Vector2(FrameSize + FramePictureMargin, FrameSize + FramePictureHeightMargin);
            _root.gameObject.SetActive(false);
        }

        /// <summary>The look shown; nothing while the portrait is out of sight.</summary>
        public PainterMood? Mood => _shown?.Mood;

        /// <summary>
        /// Shows the painter with a look, holding that bust and letting go of the one shown before; nothing takes the
        /// portrait out of sight and lets go of its bust.
        /// </summary>
        public void Show(PainterMood? mood)
        {
            if (_shown?.Mood == mood)
            {
                return;
            }

            _shown?.Hold.Dispose();
            _shown = null;
            _bust.sprite = null;
            if (mood is PainterMood look)
            {
                PictureShelf<PainterPicture, Sprite>.PictureHold hold = _painter.Hold(PainterPictures.BustOf(look));
                _shown = (look, hold);
                _bust.sprite = hold.Picture;
            }

            _root.gameObject.SetActive(_shown != null);
        }

        /// <summary>Hangs the portrait with its middle on a point of its parent, its frame so many points wide and high.</summary>
        public void Place(Vector2 middle, float frameSize)
        {
            _root.localPosition = new Vector3(middle.x, middle.y, 0f);
            float scale = frameSize / FrameSize;
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Draws the portrait this strongly: what lies under it shows through where it is less than 1.</summary>
        public void Fade(float strength)
        {
            var colour = new Color(1f, 1f, 1f, Mathf.Clamp01(strength));
            _velvet.color = colour;
            _bust.color = colour;
            _frame.color = colour;
        }

        public void Dispose()
        {
            _shown?.Hold.Dispose();
            _shown = null;
            SceneObjects.DestroyObjectOf(_root);
        }
    }
}
