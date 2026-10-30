#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Screens;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Hall
{
    /// <summary>
    /// The hall's gift frame, as the design canvas draws it (HomeTR), on the wall at the workbench's left (the canvas
    /// hangs it at the right, where the painter now stands, §22): a small
    /// gilt frame with the gift's picture in it, on a wire from a pin, a cream pill under it, and while the daily
    /// reward waits a red dot at its upper right and "Hediye" on the pill. Once the day's reward is taken the dot
    /// goes and the pill says how long until the next one. A press asks for the gift; the rules give it, or say when
    /// the next one comes. Sizes are the canvas' points around the frame's middle, drawn at the layout's share of them.
    /// </summary>
    public sealed class GiftFrame : IPressable, IDisposable
    {
        /// <summary>A picture of the canvas is drawn in a box of 100 points; the frame's picture is 28 wider and 56 higher than its body, the red badge's body 22 across.</summary>
        private const float MarkBox = 100f;
        private const float FramePictureMargin = 28f;
        private const float FramePictureHeightMargin = 56f;
        private const float RedBadgeBody = 22f;

        /// <summary>The canvas' pill of 21 is the pill of 24 at seven eighths; its words are Gabarito ExtraBold of 11, 7 from its ends.</summary>
        private const float PillScale = 21f / 24f;
        private const float WordsSize = 11f / PillScale;
        private const float PillPadding = 7f / PillScale;
        private const float PillPictureMargin = 16f;
        private const float PillPictureHeight = 40f;

        /// <summary>A held frame goes this far down, and loses a little light.</summary>
        private const float PressedDown = 2f;

        private readonly Transform _root;
        private readonly Transform _cap;
        private readonly PaintedShape _wire;
        private readonly SpriteRenderer _picture;
        private readonly SpriteRenderer _frame;
        private readonly SpriteRenderer _pill;
        private readonly TextMeshPro _words;
        private readonly SpriteRenderer _dot;
        private Box _button;
        private string _said = string.Empty;

        public GiftFrame(Transform parent, ArtCatalogue art)
        {
            _root = new GameObject("Gift frame").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wire = PaintedShape.Create("Wire", _root, art.PaintMaterial, DrawOrder.Wire);
            var sketch = new MeshSketch();
            var pin = new Vector2(0f, HallLayout.GiftPinOverMiddle);
            foreach (float side in new[] { -1f, 1f })
            {
                PictureWire.AddSlanting(sketch, pin, new Vector2(side * HallLayout.GiftWireEndsAcross, HallLayout.GiftWireEndsOverMiddle));
            }

            _wire.Show(sketch.ToMesh("Gift wire"));
            SceneObjects.AddSprite("Pin", _root, art.Of(GalleryArt.WirePin), art.SpriteMaterial, DrawOrder.GiftFrame).transform.localPosition = pin;

            _cap = new GameObject("Cap").transform;
            _cap.SetParent(_root, worldPositionStays: false);
            _picture = SceneObjects.AddSprite("Picture", _cap, art.Of(GalleryArt.GiftPicture), art.SpriteMaterial, DrawOrder.GiftPicture);
            _picture.transform.localScale = new Vector3(HallLayout.GiftPictureSize / MarkBox, HallLayout.GiftPictureSize / MarkBox, 1f);
            _frame = SceneObjects.AddSprite("Frame", _cap, art.Of(GalleryArt.SmallFrame), art.SpriteMaterial, DrawOrder.GiftFrame);
            _frame.drawMode = SpriteDrawMode.Sliced; // the frame's middle stretches both ways; its corners keep their shape
            _frame.size = new Vector2(HallLayout.GiftFrameSize + FramePictureMargin, HallLayout.GiftFrameSize + FramePictureHeightMargin);

            _pill = SceneObjects.AddSprite("Pill", _cap, art.Of(GalleryArt.Pill), art.SpriteMaterial, DrawOrder.GiftPill);
            _pill.drawMode = SpriteDrawMode.Tiled; // the picture's middle repeats, its round ends keep their shape
            _pill.transform.localPosition = new Vector3(0f, -HallLayout.GiftPillUnderMiddle, 0f);
            _pill.transform.localScale = new Vector3(PillScale, PillScale, 1f);
            _words = Lettering.Write("Words", _pill.transform, art, LetteringStyle.Label, WordsSize, Lettering.Ink, DrawOrder.GiftLettering);

            _dot = SceneObjects.AddSprite("Waits", _cap, art.Of(GalleryArt.RedBadge), art.SpriteMaterial, DrawOrder.GiftDot);
            _dot.transform.localPosition = new Vector3(HallLayout.GiftDotFromMiddle.Across, HallLayout.GiftDotFromMiddle.Up, 0f);
            float dotScale = HallLayout.GiftDotSize / RedBadgeBody;
            _dot.transform.localScale = new Vector3(dotScale, dotScale, 1f);
        }

        /// <summary>The middle of the frame, in the layout's points: where a finger presses it.</summary>
        public Vector2 Middle => _root.localPosition;

        /// <summary>True while the red dot says that a gift waits.</summary>
        public bool ShowsAGiftWaiting => _dot.enabled;

        /// <summary>What the pill says now.</summary>
        public string Says => _said;

        /// <summary>Hangs the frame where the hall's layout has it.</summary>
        public void Place(HallLayout layout)
        {
            _root.localPosition = new Vector3(layout.GiftMiddle.X, layout.GiftMiddle.Y, 0f);
            _root.localScale = new Vector3(layout.GiftScale, layout.GiftScale, 1f);
            _button = layout.GiftButton;
        }

        /// <summary>Shows whether the day's gift waits, or how long until the next one; the pill is written anew only when its words change.</summary>
        public void Show(GiftView gift)
        {
            string words;
            switch (gift)
            {
                case GiftView.Due _:
                    words = ShopWords.Gift;
                    _dot.enabled = true;
                    break;
                case GiftView.Waiting waiting:
                    words = ShopWords.GiftIn(waiting.Left);
                    _dot.enabled = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gift), gift, "A gift the frame cannot show.");
            }

            if (words == _said)
            {
                return;
            }

            _said = words;
            Lettering.Say(_words, words);
            _words.transform.localPosition = new Vector3(0f, -Lettering.FigureDrop(LetteringStyle.Label) * WordsSize, 0f);
            _pill.size = new Vector2(Lettering.WidthOf(_words, words) + 2f * PillPadding + PillPictureMargin, PillPictureHeight);
        }

        /// <param name="point">A point of the hall.</param>
        public bool IsUnder(Vector2 point) => _root.gameObject.activeSelf && _button.Contains(point.x, point.y);

        public void ShowPressed(bool isPressed)
        {
            _cap.localPosition = new Vector3(0f, isPressed ? -PressedDown : 0f, 0f);
            Color light = Dimming.Of(isDimmed: false, isHeld: isPressed);
            _picture.color = light;
            _frame.color = light;
        }

        public void Dispose()
        {
            _wire.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }
    }
}
