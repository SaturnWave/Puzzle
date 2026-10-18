#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Hall
{
    /// <summary>
    /// The brass plate under a painting, as the design canvas draws it (HomeTR, CollectionTR): two lines between two
    /// studs, the painting's title and under it its painter and year in small capitals. The plate is as wide as its
    /// longer line needs and no wider than it is allowed; a line too long for that is set smaller. A title that would
    /// be set smaller than <see cref="SmallestOneLineTitle"/> on one line ("Elizabeth Farren, Later Countess of Derby"
    /// under a portrait) is set on two balanced lines instead, and the plate grows by a line downwards, its top edge
    /// where it was. Sizes are in points around the middle of the plate at its full size, 35 points high; a wall of
    /// small paintings draws it smaller.
    /// </summary>
    public sealed class CaptionPlate
    {
        /// <summary>The plate's picture is the plate and the room around it for its edge and shadow: 16 points wider, 52 high.</summary>
        private const float PictureMargin = 16f;
        private const float PictureHeight = 52f;

        /// <summary>The plate has this much brass beside its longer line, and is never narrower than this.</summary>
        private const float BrassBesideTheLines = 22f;
        private const float Narrowest = 110f;

        /// <summary>
        /// The two lines on the plate, as the design sets them (HomeTR): the title in the serif at 11 points, the
        /// painter and year in capitals at 8.5, apart by 12 hundredths of their size; and how far over and under
        /// the plate's middle they stand.
        /// </summary>
        private const float TitleSize = 11f;
        private const float TitleAboveMiddle = 6f;
        private const float ByLineSize = 8.5f;
        private const float ByLineBelowMiddle = 7.5f;
        private const float ByLineSpacing = 12f;
        private static readonly Color ByLineInk = Tint.Of(0x6B3E08);

        /// <summary>
        /// A title set smaller than this on one line is set on two, each at most <see cref="LargestTwoLineTitle"/>,
        /// their baselines apart by <see cref="TwoTitleLinesApart"/> of their size; the plate grows by that much.
        /// </summary>
        private const float SmallestOneLineTitle = 8f;
        private const float LargestTwoLineTitle = 9.5f;
        private const float TwoTitleLinesApart = 1.2f;

        private readonly SpriteRenderer _plate;
        private readonly TextMeshPro _title;
        private readonly TextMeshPro _titleGoesOn;
        private readonly TextMeshPro _byLine;

        /// <param name="raisedBy">Added to the plate's places in the drawing order; nothing for a plate in the lit room.</param>
        public CaptionPlate(Transform parent, ArtCatalogue art, int raisedBy = 0)
        {
            _plate = SceneObjects.AddSprite("Plate", parent, art.Of(GalleryArt.CaptionPlate), art.SpriteMaterial, DrawOrder.Caption + raisedBy);
            _plate.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its ends with their studs keep their shape
            _title = Lettering.Write("Title", _plate.transform, art, LetteringStyle.Title, TitleSize, Lettering.BrassInk, DrawOrder.CaptionLettering + raisedBy);
            _title.transform.localPosition = new Vector3(0f, TitleAboveMiddle, 0f);
            _titleGoesOn = Lettering.Write("Title goes on", _plate.transform, art, LetteringStyle.Title, LargestTwoLineTitle, Lettering.BrassInk, DrawOrder.CaptionLettering + raisedBy);
            _byLine = Lettering.Write("Painter", _plate.transform, art, LetteringStyle.Label, ByLineSize, ByLineInk, DrawOrder.CaptionLettering + raisedBy);
            _byLine.transform.localPosition = new Vector3(0f, -ByLineBelowMiddle, 0f);
            _byLine.characterSpacing = ByLineSpacing;
        }

        /// <summary>Puts the plate at a place and writes its two lines.</summary>
        /// <param name="middle">The middle of the plate, in the points of what it hangs on.</param>
        /// <param name="widest">The widest the plate may be, in those points.</param>
        /// <param name="scale">How large the plate is drawn, as a share of its full size.</param>
        public void Place(Vector2 middle, float widest, string title, string byLine, float scale = 1f)
        {
            _title.fontSize = TitleSize;
            _byLine.fontSize = ByLineSize;
            float longerLine = Mathf.Max(Lettering.WidthOf(_title, title), Lettering.WidthOf(_byLine, byLine));
            float width = Mathf.Clamp(longerLine + 2f * BrassBesideTheLines, Narrowest, Mathf.Max(Narrowest, widest / scale));

            float room = width - 2f * BrassBesideTheLines;
            _plate.transform.localScale = new Vector3(scale, scale, 1f);
            Lettering.SayWithin(_title, title, TitleSize, room);
            Lettering.SayWithin(_byLine, byLine, ByLineSize, room);
            float grows = _title.fontSize >= SmallestOneLineTitle ? 0f : SayTheTitleOnTwoLines(title, room);
            if (grows == 0f)
            {
                Lettering.Say(_titleGoesOn, string.Empty);
                _title.transform.localPosition = new Vector3(0f, TitleAboveMiddle, 0f);
            }

            // A plate that grows keeps its top edge under the frame: its middle moves down by half of what it grew.
            _plate.transform.localPosition = new Vector3(middle.x, middle.y - scale * grows / 2f, 0f);
            _plate.size = new Vector2(width + PictureMargin, PictureHeight + grows);
            _byLine.transform.localPosition = new Vector3(0f, -ByLineBelowMiddle - grows / 2f, 0f);
        }

        /// <summary>The title on two balanced lines, as large as they fit up to their largest size; how much taller the plate grows for them.</summary>
        private float SayTheTitleOnTwoLines(string title, float room)
        {
            _titleGoesOn.fontSize = LargestTwoLineTitle;
            (string first, string second) = BalancedLines.SplitInTwo(title, words => Lettering.WidthOf(_titleGoesOn, words));
            float longer = Mathf.Max(Lettering.WidthOf(_titleGoesOn, first), Lettering.WidthOf(_titleGoesOn, second));
            float size = LargestTwoLineTitle * Mathf.Min(1f, room / longer);
            float apart = TwoTitleLinesApart * size;
            Lettering.SayWithin(_title, first, size, room);
            Lettering.SayWithin(_titleGoesOn, second, size, room);
            _title.transform.localPosition = new Vector3(0f, TitleAboveMiddle + apart / 2f, 0f);
            _titleGoesOn.transform.localPosition = new Vector3(0f, TitleAboveMiddle - apart / 2f, 0f);
            return apart;
        }
    }
}
