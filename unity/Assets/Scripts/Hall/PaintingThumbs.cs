#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Painting;
using Puzzle.Museum;
using UnityEngine;

namespace Puzzle.Client.Hall
{
    /// <summary>The three places in the drawing order a row of small pictures takes: their frames, the pictures, and the marks on them.</summary>
    public readonly struct ThumbOrders
    {
        public ThumbOrders(DrawOrder frames, DrawOrder pictures, DrawOrder marks)
        {
            Frames = frames;
            Pictures = pictures;
            Marks = marks;
        }

        /// <summary>For a row on a plaque.</summary>
        public static ThumbOrders OnAPlaque => new ThumbOrders(DrawOrder.PlaqueArt, DrawOrder.PlaquePicture, DrawOrder.PlaquePictureMark);

        public DrawOrder Frames { get; }

        public DrawOrder Pictures { get; }

        public DrawOrder Marks { get; }
    }

    /// <summary>
    /// A room's five paintings as a row of small framed pictures, as the design canvas draws them on the room's
    /// plaque (HomeTR) and in the room on the map (MapTR). A restored painting shows its picture and a green seal;
    /// one that is being restored shows as much of its picture as is in place, the rest under a linen veil; one
    /// with no piece in place is an empty frame. The painting the player is restoring is ringed in red. Sizes are
    /// in points around the middle of the row.
    /// </summary>
    public sealed class PaintingThumbs : IDisposable
    {
        public const float PictureWidth = 26f;
        public const float PictureHeight = 21f;
        public const float Gap = 7.5f;

        /// <summary>The whole row, from the first picture's left edge to the last one's right edge.</summary>
        public const float RowWidth = MuseumPlan.PaintingsPerRoom * PictureWidth + (MuseumPlan.PaintingsPerRoom - 1) * Gap;

        /// <summary>A frame: gilt around the picture, a dark line around the gilt, and a soft drop under it.</summary>
        private const float GiltBand = 2f;
        private const float DarkLine = 1f;
        private static readonly Color Gilt = Tint.Of(0xF6B93C);
        private static readonly Color GiltEdge = Tint.Of(0x5E3305);
        private static readonly Color Drop = Tint.Of(0x46260F, 0.3f);
        private const float DropDown = 2f;

        /// <summary>The frame of the painting being restored: cream, a red band, a dark red line.</summary>
        private const float RedBand = 1.6f;
        private static readonly Color RingInside = Tint.Of(0xFFF6CC);
        private static readonly Color Red = Tint.Of(0xF4472F);
        private static readonly Color RedEdge = Tint.Of(0x5E0A0C);

        private static readonly Color Linen = Tint.Of(0xF1E6CC);
        private static readonly Color LinenVeil = Tint.Of(0xF1E6CC, 0.82f);

        /// <summary>The seal of a restored painting hangs over the picture's lower right corner; its drawing is 20 points across.</summary>
        private const float SealSize = 12f;
        private const float SealDrawnSize = 20f;
        private const float SealPastCorner = 4f;

        private readonly Transform _root;
        private readonly List<PaintedShape> _shapes = new List<PaintedShape>();

        /// <summary>The paintings whose pictures the row shows: it holds them while it is up.</summary>
        private readonly List<PaintingLook> _looks = new List<PaintingLook>();

        private PaintingThumbs(Transform parent, ArtCatalogue art, PaintingGallery paintings, RoomWall room, PaintingNumber? beingRestored, ThumbOrders orders)
        {
            _root = new GameObject("Paintings").transform;
            _root.SetParent(parent, worldPositionStays: false);

            var frames = new MeshSketch();
            var marks = new MeshSketch();
            for (int place = 0; place < room.Paintings.Count; place++)
            {
                HungPainting painting = room.Paintings[place];
                var picture = new Rect(-RowWidth / 2f + place * (PictureWidth + Gap), -PictureHeight / 2f, PictureWidth, PictureHeight);
                AddFrame(frames, picture, isBeingRestored: painting.Number == beingRestored);
                AddPicture(picture, painting, paintings, art, orders.Pictures);
                if (painting.IsWhole)
                {
                    AddSeal(picture, art, orders.Marks);
                }
                else
                {
                    AddVeil(marks, picture, painting.ShareInPlace);
                }
            }

            Keep(PaintedShape.Create("Frames", _root, art.PaintMaterial, orders.Frames)).Show(frames.ToMesh("Thumbnail frames"));
            Keep(PaintedShape.Create("Veils", _root, art.PaintMaterial, orders.Marks)).Show(marks.ToMesh("Thumbnail veils"));
        }

        /// <summary>The row's place is its middle.</summary>
        public Transform Transform => _root;

        /// <param name="beingRestored">The painting the player is restoring: it is ringed if it hangs in this room. Nothing when the player is restoring none.</param>
        public static PaintingThumbs Create(Transform parent, ArtCatalogue art, PaintingGallery paintings, RoomWall room, PaintingNumber? beingRestored, ThumbOrders orders) =>
            new PaintingThumbs(parent, art, paintings, room, beingRestored, orders);

        public void Dispose()
        {
            foreach (PaintedShape shape in _shapes)
            {
                shape.Dispose();
            }

            foreach (PaintingLook look in _looks)
            {
                look.Dispose();
            }

            SceneObjects.DestroyObjectOf(_root);
        }

        private PaintedShape Keep(PaintedShape shape)
        {
            _shapes.Add(shape);
            return shape;
        }

        private static void AddFrame(MeshSketch sketch, Rect picture, bool isBeingRestored)
        {
            float band = isBeingRestored ? GiltBand + RedBand : GiltBand;
            Rect outer = Grown(picture, band + DarkLine);
            sketch.AddRectangle(new Rect(outer.x, outer.y - DropDown, outer.width, outer.height), Drop);
            sketch.AddRectangle(outer, isBeingRestored ? RedEdge : GiltEdge);
            if (isBeingRestored)
            {
                sketch.AddRectangle(Grown(picture, band), Red);
                sketch.AddRectangle(Grown(picture, GiltBand), RingInside);
            }
            else
            {
                sketch.AddRectangle(Grown(picture, band), Gilt);
            }
        }

        /// <summary>
        /// The picture in its frame: the middle of the painting, cut to the frame's proportions. A painting with no
        /// piece in place, or with no picture, is bare linen.
        /// </summary>
        private void AddPicture(Rect picture, HungPainting painting, PaintingGallery paintings, ArtCatalogue art, DrawOrder order)
        {
            Texture2D? texture = painting.PiecesInPlace.Count > 0 ? Hold(paintings.LookOf(painting.Number)).Picture : null;
            var sketch = new MeshSketch();
            if (texture == null)
            {
                sketch.AddRectangle(picture, Linen);
            }
            else
            {
                sketch.AddRectangle(picture, Color.white, Color.white, MiddleOf(texture, picture.width / picture.height));
            }

            Keep(PaintedShape.Create($"Painting {painting.Number}", _root, art.PaintMaterial, order, texture)).Show(sketch.ToMesh("Thumbnail"));
        }

        /// <summary>The part of a picture, in shares of its sides, that has given proportions and is as large as the picture allows, around its middle.</summary>
        private static Rect MiddleOf(Texture2D picture, float aspect)
        {
            float pictureAspect = (float)picture.width / picture.height;
            float shareAcross = Mathf.Min(1f, aspect / pictureAspect);
            float shareUpright = Mathf.Min(1f, pictureAspect / aspect);
            return new Rect((1f - shareAcross) / 2f, (1f - shareUpright) / 2f, shareAcross, shareUpright);
        }

        /// <summary>The linen over what is not in place yet: it covers the picture from its foot up.</summary>
        private static void AddVeil(MeshSketch sketch, Rect picture, float shareInPlace)
        {
            if (shareInPlace > 0f)
            {
                sketch.AddRectangle(new Rect(picture.x, picture.y, picture.width, picture.height * (1f - shareInPlace)), LinenVeil);
            }
        }

        private void AddSeal(Rect picture, ArtCatalogue art, DrawOrder order)
        {
            Transform seal = SceneObjects.AddSprite("Restored", _root, art.Of(GalleryArt.GoalDone), art.SpriteMaterial, order).transform;
            float fromCorner = SealPastCorner - SealSize / 2f;
            seal.localPosition = new Vector3(picture.xMax + fromCorner, picture.yMin - fromCorner, 0f);
            seal.localScale = new Vector3(SealSize / SealDrawnSize, SealSize / SealDrawnSize, 1f);
        }

        private PaintingLook Hold(PaintingLook look)
        {
            _looks.Add(look);
            return look;
        }

        private static Rect Grown(Rect rect, float by) => new Rect(rect.x - by, rect.y - by, rect.width + 2f * by, rect.height + 2f * by);
    }
}
