#nullable enable
using System;
using System.Globalization;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Museum;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Hall
{
    /// <summary>
    /// What a page's plaque is made of, whatever is written on it: the cream plaque of the board's HUD, drawn as
    /// wide and as low as the page has it, and the measures of the design canvas' plaque (362 by 58 points), in
    /// which everything on it is placed. The game's plaque is a little smaller, so what stands on it is drawn
    /// smaller by the same share.
    /// </summary>
    public sealed class PagePlaque
    {
        /// <summary>The design's plaque, in its own points; what stands on a plaque is placed in these.</summary>
        public const float DesignWidth = 362f;
        public const float DesignHeight = 58f;

        /// <summary>The plaque's rim and padding: lettering begins this far in from its left edge and ends as far from its right.</summary>
        public const float SidePadding = 13f;

        /// <summary>The sizes of a plaque's two kinds of line: the small capitals that label, and the name or title under them.</summary>
        public const float LabelSize = 9f;
        public const float TitleSize = 16f;
        private const float LabelSpacing = 9f;
        private static readonly Color LabelInk = Tint.Of(0x8A5A2B);

        /// <summary>The plaque's picture is its body and the room around it for its rim and shadow: 28 points wider, 34 higher.</summary>
        private const float PictureMarginAcross = 28f;
        private const float PictureMarginUpright = 34f;

        /// <summary>
        /// The picture's pivot is the middle of the body at the height the HUD draws it. At another height the pivot
        /// stays the same share down the picture (46 of its 118 points) while the body, 4 points under the picture's
        /// top, does not: the picture is moved by the difference.
        /// </summary>
        private const float PivotDownThePicture = 46f / 118f;
        private const float BodyUnderPictureTop = 4f;

        private readonly SpriteRenderer _face;

        public PagePlaque(string name, Transform parent, ArtCatalogue art)
        {
            Root = new GameObject(name).transform;
            Root.SetParent(parent, worldPositionStays: false);
            _face = SceneObjects.AddSprite("Face", Root, art.Of(GalleryArt.Plaque), art.SpriteMaterial, DrawOrder.Plaque);
            _face.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches both ways; its rim and corners keep their shape
            Content = new GameObject("Content").transform;
            Content.SetParent(Root, worldPositionStays: false);
        }

        /// <summary>The plaque's place is the middle of its body.</summary>
        public Transform Root { get; }

        /// <summary>
        /// What stands on the plaque hangs under this, in the design's points around the middle of the plaque: x
        /// from -181 at the left edge to 181 at the right, y from -29 at the foot to 29 at the top.
        /// </summary>
        public Transform Content { get; }

        /// <summary>Hangs the plaque where a page has it.</summary>
        public void Place(Box plaque)
        {
            Root.localPosition = new Vector3(plaque.MiddleX, plaque.MiddleY, 0f);
            float pictureHeight = plaque.Height + PictureMarginUpright;
            float bodyMiddleOverPivot = PivotDownThePicture * pictureHeight - (BodyUnderPictureTop + plaque.Height / 2f);
            _face.transform.localPosition = new Vector3(0f, -bodyMiddleOverPivot, 0f);
            _face.size = new Vector2(plaque.Width + PictureMarginAcross, pictureHeight);
            float scale = plaque.Height / DesignHeight;
            Content.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>A line of small capitals, as the plaques label what stands under them.</summary>
        public static TextMeshPro WriteLabel(string name, Transform parent, ArtCatalogue art, DrawOrder order)
        {
            TextMeshPro label = Lettering.Write(name, parent, art, LetteringStyle.Label, LabelSize, LabelInk, order);
            label.characterSpacing = LabelSpacing;
            return label;
        }
    }

    /// <summary>
    /// The plaque of a room, as the design canvas draws it over the hall (HomeTR): the room's number on a teal
    /// badge, which room of the museum it is and what it is called, how many of its paintings are restored, and
    /// the five paintings as a row of small framed pictures.
    /// </summary>
    public sealed class RoomPlaque : IDisposable
    {
        /// <summary>From the plaque's left edge: the middle of the badge, and where the two lines beside it begin.</summary>
        private const float BadgeMiddleIn = 29f;
        private const float LinesBeginIn = 55f;
        private const float BadgeNumberSize = 18f;

        /// <summary>The two lines at the left stand this far over and under the plaque's middle; so do the label and the pictures at the right.</summary>
        private const float RoomLabelUp = 9.5f;
        private const float RoomNameDown = 6f;
        private const float RestoredLabelUp = 13.5f;
        private const float PicturesDown = 7.5f;

        /// <summary>The row of pictures ends this far from the plaque's right edge: past the padding, clear of the last frame's seal.</summary>
        private const float PicturesEndIn = 18f;

        private readonly PagePlaque _plaque;
        private readonly PaintingThumbs _pictures;

        private RoomPlaque(Transform parent, ArtCatalogue art, PaintingGallery paintings, RoomWall room, PaintingNumber? beingRestored)
        {
            _plaque = new PagePlaque("Room plaque", parent, art);
            Transform content = _plaque.Content;
            float left = -PagePlaque.DesignWidth / 2f;
            float right = PagePlaque.DesignWidth / 2f;

            Transform badge = SceneObjects.AddSprite("Badge", content, art.Of(GalleryArt.RoomBadge), art.SpriteMaterial, DrawOrder.PlaqueArt).transform;
            badge.localPosition = new Vector3(left + BadgeMiddleIn, 0f, 0f);
            TextMeshPro number = Lettering.Write("Room number", badge, art, LetteringStyle.OnTeal, BadgeNumberSize, Color.white, DrawOrder.PlaqueLettering);
            number.transform.localPosition = new Vector3(0f, -Hud.FigureDrop * BadgeNumberSize, 0f);
            Lettering.Say(number, room.Number.Value.ToString(CultureInfo.InvariantCulture));

            TextMeshPro whichRoom = PagePlaque.WriteLabel("Which room", content, art, DrawOrder.PlaqueLettering);
            Lettering.SayFromLeft(whichRoom, MuseumWords.RoomOfRooms(room.Number), left + LinesBeginIn, RoomLabelUp);

            float picturesRight = right - PicturesEndIn;
            float picturesLeft = picturesRight - PaintingThumbs.RowWidth;
            TextMeshPro name = Lettering.Write("Room name", content, art, LetteringStyle.Label, PagePlaque.TitleSize, Lettering.Ink, DrawOrder.PlaqueLettering);
            float roomForTheName = picturesLeft - PagePlaque.SidePadding - (left + LinesBeginIn);
            name.fontSize = PagePlaque.TitleSize * Mathf.Min(1f, roomForTheName / Lettering.WidthOf(name, room.Name));
            Lettering.SayFromLeft(name, room.Name, left + LinesBeginIn, -RoomNameDown);

            TextMeshPro restored = PagePlaque.WriteLabel("Restored", content, art, DrawOrder.PlaqueLettering);
            Lettering.SayUpToRight(restored, MuseumWords.Restored(room.PaintingsWhole), right - PagePlaque.SidePadding, RestoredLabelUp);

            _pictures = PaintingThumbs.Create(content, art, paintings, room, beingRestored, ThumbOrders.OnAPlaque);
            _pictures.Transform.localPosition = new Vector3(picturesRight - PaintingThumbs.RowWidth / 2f, -PicturesDown, 0f);
        }

        /// <param name="beingRestored">The painting the player is restoring: its picture is ringed. Nothing when the player is restoring none.</param>
        public static RoomPlaque Create(Transform parent, ArtCatalogue art, PaintingGallery paintings, RoomWall room, PaintingNumber? beingRestored) =>
            new RoomPlaque(parent, art, paintings, room, beingRestored);

        public void Place(PageLayout page) => _plaque.Place(page.Plaque);

        public void Dispose()
        {
            _pictures.Dispose();
            SceneObjects.DestroyObjectOf(_plaque.Root);
        }
    }
}
