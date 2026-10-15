#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// Words a finger presses, underlined as a link is, with a heart and a smaller line after them that say what the
    /// press costs, as the design draws the quiet way out under a card's buttons (LoseTR: "Vazgeç", and "1 can gider"
    /// beside the heart); a way out that costs nothing has its words alone. The words are Gabarito ExtraBold of 15.5 with a line of 1.5 five points under their baseline;
    /// 12 after them the heart of 20, and 3 after that the cost in Gabarito Bold of 12.5. The row is centred on its
    /// place, and the finger may press anywhere in a band as high as a finger needs around it. Sizes are in points of
    /// what the link stands on, around its middle.
    /// </summary>
    public sealed class TextLink : IPressable, IDisposable
    {
        private const float WordsSize = 15.5f;
        private const float CostSize = 12.5f;
        private const float UnderlineThickness = 1.5f;
        private const float UnderlineUnderBaseline = 5f;
        private const float GapWordsToHeart = 12f;
        private const float HeartSize = 20f;
        private const float GapHeartToCost = 3f;

        /// <summary>The heart is a drawing in a box of 100 points.</summary>
        private const float MarkBox = 100f;

        /// <summary>The band a finger presses the link in (LoseTR: the link's box is 44 high) and the room it has beside the row.</summary>
        private const float FingerBand = 44f;
        private const float RoomBesideForAFinger = 12f;

        /// <summary>A held link goes this far down, as a button goes onto its lip.</summary>
        private const float PressedDown = 1.5f;

        private readonly Transform _root;
        private readonly Transform _row;
        private readonly PaintedShape _underline;
        private readonly float _width;

        /// <param name="words">What the link says, underlined.</param>
        /// <param name="cost">What a press costs, beside the heart; nothing for a way out that costs nothing, which has its words alone.</param>
        /// <param name="order">The place of the lettering in the drawing order; the underline and the heart take the same.</param>
        public TextLink(string name, Transform parent, ArtCatalogue art, string words, string? cost, DrawOrder order)
        {
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            _row = new GameObject("Row").transform;
            _row.SetParent(_root, worldPositionStays: false);

            TextMeshPro said = Lettering.Write("Words", _row, art, LetteringStyle.Label, WordsSize, Lettering.Ink, order);
            Lettering.Say(said, words);
            float wordsWidth = Lettering.WidthOf(said, words);
            _width = wordsWidth;
            TextMeshPro? costs = null;
            if (cost != null)
            {
                costs = Lettering.Write("Cost", _row, art, LetteringStyle.Soft, CostSize, Lettering.SoftInk, order);
                Lettering.Say(costs, cost);
                _width += GapWordsToHeart + HeartSize + GapHeartToCost + Lettering.WidthOf(costs, cost);
            }

            // Each line is centred on the row by the middle of its capitals, as a row of boxes of one line each centres them.
            float left = -_width / 2f;
            Lettering.SayFromLeft(said, words, left, 0f);
            if (costs != null && cost != null)
            {
                Transform heart = SceneObjects.AddSprite("Heart", _row, art.Of(GalleryArt.IconHeart), art.SpriteMaterial, order).transform;
                heart.localPosition = new Vector3(left + wordsWidth + GapWordsToHeart + HeartSize / 2f, 0f, 0f);
                heart.localScale = new Vector3(HeartSize / MarkBox, HeartSize / MarkBox, 1f);
                Lettering.SayFromLeft(costs, cost, left + wordsWidth + GapWordsToHeart + HeartSize + GapHeartToCost, 0f);
            }

            float baseline = -Lettering.CapitalsHeight(LetteringStyle.Label) * WordsSize / 2f;
            _underline = PaintedShape.Create("Underline", _row, art.PaintMaterial, order);
            var sketch = new MeshSketch();
            sketch.AddRectangle(new Rect(left, baseline - UnderlineUnderBaseline - UnderlineThickness, wordsWidth, UnderlineThickness), Lettering.Ink);
            _underline.Show(sketch.ToMesh("Underline"));
        }

        /// <summary>The middle of the row, in the points of what the link stands on.</summary>
        public Vector2 Middle => _root.localPosition;

        /// <summary>Puts the middle of the row at a place of what the link stands on.</summary>
        public void Place(Vector2 middle) => _root.localPosition = new Vector3(middle.x, middle.y, 0f);

        /// <param name="point">A point of what the link stands on.</param>
        public bool IsUnder(Vector2 point)
        {
            Vector2 fromMiddle = point - Middle;
            return Mathf.Abs(fromMiddle.x) <= _width / 2f + RoomBesideForAFinger && Mathf.Abs(fromMiddle.y) <= FingerBand / 2f;
        }

        public void ShowPressed(bool isPressed) => _row.localPosition = new Vector3(0f, isPressed ? -PressedDown : 0f, 0f);

        /// <summary>Lets go of the underline's mesh; the rest goes with the card the link stands on.</summary>
        public void Dispose() => _underline.Dispose();
    }
}
