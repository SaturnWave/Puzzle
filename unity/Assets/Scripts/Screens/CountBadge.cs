#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>What a small round badge says, and in which of the design's colours. A closed set of four.</summary>
    public readonly struct BadgeFace
    {
        private BadgeFace(BadgeColour colour, int count, string? words = null)
        {
            Colour = colour;
            Count = count;
            Words = words;
        }

        /// <summary>The green badge with a plus: none is owned, and more can be had (BoardTR, LevelStartTR).</summary>
        public static BadgeFace GetMore => new BadgeFace(BadgeColour.Green, count: 0);

        internal BadgeColour Colour { get; }

        internal int Count { get; }

        /// <summary>Words the badge says instead of a count; nothing for a badge with a count or a plus.</summary>
        internal string? Words { get; }

        /// <summary>The red badge with a count of what waits for the player (HomeTR, CollectionTR).</summary>
        public static BadgeFace Waiting(int count) => new BadgeFace(BadgeColour.Red, count);

        /// <summary>The teal badge with a count of what the player owns (BoardTR, LevelStartTR).</summary>
        public static BadgeFace Owned(int count) => new BadgeFace(BadgeColour.Teal, count);

        /// <summary>The teal badge with the level a booster opens on, "Bölüm 7", on a booster's disc that is locked until then.</summary>
        public static BadgeFace OpensOn(int level) => new BadgeFace(BadgeColour.Teal, level, BoardWords.OpensOn(level));
    }

    /// <summary>A badge's colour, which is also what its count is a count of.</summary>
    internal enum BadgeColour
    {
        Red,
        Teal,
        Green,
    }

    /// <summary>
    /// A small round badge on the corner of a button, a painting or a disc, as the design canvas draws them: a disc
    /// of colour in a white ring, 22 points across, with a number that says how many (red: pieces that wait; teal:
    /// boosters that are owned), or green with a plus where none is owned. It widens for a number of two figures,
    /// and for the level a locked booster opens on, which it says in smaller type; what it says can be changed where
    /// it stands.
    /// </summary>
    public sealed class CountBadge
    {
        /// <summary>The badge's body at its narrowest: a disc.</summary>
        public const float Size = 22f;

        /// <summary>Words on a teal badge are set this large, smaller than its count: "Bölüm 7" across a disc of 60.</summary>
        private const float WordsTypeSize = 10f;

        private readonly ArtCatalogue _art;
        private readonly SpriteRenderer _badge;
        private readonly TextMeshPro _line;

        private CountBadge(string name, Transform parent, ArtCatalogue art, DrawOrder order)
        {
            _art = art;
            _badge = SceneObjects.AddSprite(name, parent, art.Of(GalleryArt.RedBadge), art.SpriteMaterial, order);
            _badge.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches for a longer number; its round ends keep their shape
            _line = Lettering.Write("Count", _badge.transform, art, LetteringStyle.Figure, Size, Color.white, order + 1);
        }

        /// <summary>The badge's place is the middle of its body.</summary>
        public Transform Transform => _badge.transform;

        /// <summary>How wide the badge's body is with what it says now.</summary>
        public float Width { get; private set; } = Size;

        /// <summary>Adds a badge under a parent; its place is the middle of its body.</summary>
        /// <param name="order">The badge's place in the drawing order; what it says takes the place above.</param>
        public static CountBadge Add(string name, Transform parent, ArtCatalogue art, BadgeFace face, DrawOrder order)
        {
            var badge = new CountBadge(name, parent, art, order);
            badge.Show(face);
            return badge;
        }

        /// <summary>Changes what the badge says; its body widens or narrows around its middle.</summary>
        public void Show(BadgeFace face)
        {
            Look look = LookOf(face.Colour);
            string words = face.Words ?? (face.Colour == BadgeColour.Green ? "+" : MuseumWords.Count(face.Count));
            float typeSize = face.Words == null ? look.TypeSize : WordsTypeSize;
            _badge.sprite = _art.Of(look.Picture);
            _line.fontSize = typeSize;
            _line.transform.localPosition = new Vector3(0f, -Lettering.FigureDrop(LetteringStyle.Figure) * typeSize, 0f);
            Lettering.Say(_line, words);

            float wider = Mathf.Max(0f, Lettering.WidthOf(_line, words) - look.RoomForWords);
            Width = Size + wider;
            _badge.size = new Vector2(look.PictureSize.x + wider, look.PictureSize.y);
        }

        /// <summary>Draws the badge and what it says through a colour: white for its own colours, a shade for a badge on something dimmed.</summary>
        public void DrawIn(Color colour)
        {
            _badge.color = colour;
            Lettering.DrawIn(_line, colour);
        }

        /// <summary>The drawing of each colour's badge with the room it has, and the size of what is written on it.</summary>
        private static Look LookOf(BadgeColour colour) => colour switch
        {
            // HomeTR, the "1" on the button that leads to the jigsaw: 14 points; a wider number widens the badge.
            BadgeColour.Red => new Look(GalleryArt.RedBadge, new Vector2(32f, 32f), typeSize: 14f, roomForWords: 14f),

            // BoardTR, how many of a booster are owned: 12 points with 5 of room at each side; its plus is 15.
            BadgeColour.Teal => new Look(GalleryArt.TealBadge, new Vector2(32f, 34f), typeSize: 12f, roomForWords: 12f),
            BadgeColour.Green => new Look(GalleryArt.GreenBadge, new Vector2(32f, 34f), typeSize: 15f, roomForWords: 12f),
            _ => throw new ArgumentOutOfRangeException(nameof(colour), colour, "A badge of a colour the design does not draw."),
        };

        /// <summary>What a badge of one colour is drawn with.</summary>
        private readonly struct Look
        {
            public Look(GalleryArt picture, Vector2 pictureSize, float typeSize, float roomForWords)
            {
                Picture = picture;
                PictureSize = pictureSize;
                TypeSize = typeSize;
                RoomForWords = roomForWords;
            }

            public GalleryArt Picture { get; }

            /// <summary>The picture of the badge at its narrowest: its body of 22 and the room around it for its ring and drop.</summary>
            public Vector2 PictureSize { get; }

            public float TypeSize { get; }

            /// <summary>Words wider than this widen the badge.</summary>
            public float RoomForWords { get; }
        }
    }
}
