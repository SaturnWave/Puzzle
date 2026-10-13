#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>What a brass nameplate says, and how wide it is.</summary>
    public readonly struct CardNameplate
    {
        /// <summary>A plate with one line is 40 points high (WinTR), one with a second line 50 (LevelStartTR).</summary>
        private const float HeightWithOneLine = 40f;
        private const float HeightWithTwoLines = 50f;

        /// <param name="title">The name on the plate, in the serif of titles.</param>
        /// <param name="width">The plate's width: the design's are 280 (WinTR), 212 (LevelStartTR) and 176 (SettingsTR).</param>
        /// <param name="titleSize">The title's type size; a title too long for the plate is set smaller.</param>
        /// <param name="secondLine">A line of small capitals under the title (LevelStartTR, "ÖDÜL · PARÇA 7 / 10"); none for a plate with its title alone.</param>
        public CardNameplate(string title, float width, float titleSize, string? secondLine = null)
        {
            Title = title;
            Width = width;
            TitleSize = titleSize;
            SecondLine = secondLine;
        }

        public string Title { get; }

        public float Width { get; }

        public float TitleSize { get; }

        /// <summary>The line under the title; nothing for a plate with its title alone.</summary>
        public string? SecondLine { get; }

        public float Height => SecondLine == null ? HeightWithOneLine : HeightWithTwoLines;
    }

    /// <summary>
    /// The brass plate that names what it rides on, as the design canvas draws it on the top edge of every card
    /// (WinTR, LoseTR, LevelStartTR, SettingsTR): a plate of any width with a stud at each end, its title in the
    /// serif, a second line of small capitals where it has one, and, where picture wires end on it, their two
    /// pins on its top edge. The game draws every such plate with this: a card's, and the one on the banner of a
    /// restored room. Sizes are the canvas' points around the middle of the plate.
    /// </summary>
    public sealed class BrassNameplate
    {
        /// <summary>The plate's picture is the plate and the room around it for its dark line and its shadow: 24 points wider and higher.</summary>
        private const float PictureMargin = 24f;

        /// <summary>A stud stands at each end of the plate, its middle this far in (WinTR: 9 points to the stud's edge); the title keeps clear of both.</summary>
        private const float StudInFromEnd = 12f;
        private const float TitleInFromEnds = 24f;

        /// <summary>
        /// On a plate with two lines (LevelStartTR, 50 points high): the middle of the title's capitals this far
        /// above the plate's middle, and the second line, small capitals of 9 points set apart by 12 hundredths of
        /// their size, this far under it.
        /// </summary>
        private const float TitleAboveMiddleOverASecondLine = 7.7f;
        private const float SecondLineUnderMiddle = 11.7f;
        private const float SecondLineSize = 9f;
        private const float SecondLineSpacing = 12f;
        private static readonly Color SecondLineInk = Tint.Of(0x5E3305);

        /// <summary>
        /// The pins of two picture wires stand on the plate's top edge this far to each side of its middle on a
        /// plate wide enough, and never farther out than half-way to the plate's ends; a pin's middle lies a
        /// little above the edge.
        /// </summary>
        private const float WidestPinsFromMiddle = 54f;
        private const float PinOverThePlate = 0.6f;

        private readonly ArtCatalogue _art;
        private readonly CardNameplate _says;
        private readonly SpriteRenderer _plate;
        private readonly DrawOrder _studOrder;

        /// <param name="plateOrder">The plate's place in the drawing order.</param>
        /// <param name="letteringOrder">The place of what is written on it.</param>
        /// <param name="studOrder">The place of its studs and of the pins on its top edge.</param>
        public BrassNameplate(
            string name, Transform parent, ArtCatalogue art, CardNameplate says, DrawOrder plateOrder, DrawOrder letteringOrder, DrawOrder studOrder)
        {
            _art = art;
            _says = says;
            _studOrder = studOrder;
            _plate = SceneObjects.AddSprite(name, parent, art.Of(GalleryArt.BrassPlate), art.SpriteMaterial, plateOrder);
            _plate.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches both ways; its corners and its edges keep their shape
            _plate.size = new Vector2(says.Width + PictureMargin, says.Height + PictureMargin);
            foreach (float side in new[] { -1f, 1f })
            {
                SpriteRenderer stud = SceneObjects.AddSprite("Stud", _plate.transform, art.Of(GalleryArt.Stud), art.SpriteMaterial, studOrder);
                stud.transform.localPosition = new Vector3(side * (says.Width / 2f - StudInFromEnd), 0f, 0f);
            }

            TextMeshPro title = Lettering.Write("Title", _plate.transform, art, LetteringStyle.Title, says.TitleSize, Lettering.BrassInk, letteringOrder);
            Lettering.SayWithin(title, says.Title, says.TitleSize, says.Width - 2f * TitleInFromEnds);
            if (says.SecondLine is string secondLine)
            {
                title.transform.localPosition = new Vector3(0f, TitleAboveMiddleOverASecondLine, 0f);
                TextMeshPro under = Lettering.Write("Second line", _plate.transform, art, LetteringStyle.Label, SecondLineSize, SecondLineInk, letteringOrder);
                under.characterSpacing = SecondLineSpacing;
                under.transform.localPosition = new Vector3(0f, -SecondLineUnderMiddle, 0f);
                Lettering.SayWithin(under, secondLine, SecondLineSize, says.Width - 2f * TitleInFromEnds);
            }
        }

        /// <summary>The plate, to be put where it rides: its place is the plate's middle.</summary>
        public Transform Transform => _plate.transform;

        /// <summary>How far to each side of the plate's middle the pins of two picture wires stand on its top edge.</summary>
        public float PinsFromMiddle => Mathf.Min(WidestPinsFromMiddle, _says.Width / 4f);

        /// <summary>Sets the two pins on the plate's top edge.</summary>
        public void AddThePins()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                SpriteRenderer pin = SceneObjects.AddSprite("Pin", _plate.transform, _art.Of(GalleryArt.WirePin), _art.SpriteMaterial, _studOrder);
                pin.transform.localPosition = new Vector3(side * PinsFromMiddle, _says.Height / 2f + PinOverThePlate, 0f);
            }
        }
    }
}
