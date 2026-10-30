#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Screens;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using Puzzle.Museum.Places;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>What a press on a painting's card of the world map asks for. A closed set.</summary>
    public enum MapCardPress
    {
        Close,
        Previous,
        Next,

        /// <summary>The painting being restored: the card before its level.</summary>
        Play,

        /// <summary>A restored painting: its room in the collection.</summary>
        SeeInTheCollection,
    }

    /// <summary>
    /// The card of a painting on the world map, over the foot of the sheet (MapWorldTR): the picture in a gilt frame,
    /// or a veil for a painting still ahead; its room and place in the room, its title, painter and year, its place
    /// with a pin, and the note on the place or the museum that holds it today; a pill that says how it stands
    /// (restored, the pieces of the one being restored and its level, or the levels that open it), "Oyna" for the one
    /// being restored and "Koleksiyonda gör" for a restored one; a cross that closes the card and two small buttons
    /// that step to the era's painting before and after, between them its place among the five.
    /// </summary>
    public sealed class MapCard : IDisposable
    {
        /// <summary>The design's card (MapWorldTR): 188 points high, 7 in from the sheet's sides and 6 over its foot.</summary>
        public const float Height = 188f;
        public const float InFromTheSides = 7f;
        public const float OverTheFoot = 6f;

        /// <summary>
        /// The card is never wider than this: on the wide sheet of a screen held sideways it stands in the middle of the
        /// sheet's foot, a little wider than on a phone (324), and the map stays seen at its sides.
        /// </summary>
        public const float WidestCard = 420f;

        private const float Inside = 13f;
        private const float FrameSize = 92f;
        private const float PictureSize = 85f;
        private const float ColumnGap = 10f;
        private const float RoomForTheSteps = 26f;
        private const float LineGap = 3f;
        private const float EyebrowSize = 8.5f;
        private const float TitleSize = 14.5f;
        private const float BylineSize = 11f;
        private const float PlaceSize = 11f;
        private const float NoteSize = 9.5f;
        private const float PlacePinWidth = 8f;
        private const float GapPinToPlace = 3f;
        private const float RowHeight = 30f;
        private const float PillHeight = 21f;
        private const float PillPadding = 8f;
        private const float PillSize = 10.5f;
        private const float PillIcon = 10f;
        private const float PlayLabelSize = 13f;
        private const float PlayPadding = 13f;
        private const float LinkSize = 11f;

        /// <summary>A block of words is set no smaller than this share of its size; a long title, which has the most room under it, down to the next.</summary>
        private const float LeastShare = 0.7f;
        private const float LeastTitleShare = 0.6f;

        /// <summary>The cross: a teal disc of 26 points around (22, 22) from the card's top right; the steps: discs of 28 at 22 from the right, 86 and 126 down, the place among the five between them.</summary>
        private const float CloseDisc = 26f;
        private const float CloseFromTheCorner = 22f;
        private const float StepDisc = 28f;
        private const float StepsFromTheRight = 22f;
        private const float PreviousDown = 86f;
        private const float PositionDown = 106f;
        private const float NextDown = 126f;
        private const float PositionSize = 9f;

        /// <summary>The design's drawings: the teal disc 44 across in its picture, the cross 22, the green button 72 high with its lip, the pill 20 high, the chevron 12.</summary>
        private const float TealDiscDrawn = 44f;
        private const float GreenButtonDrawn = PushButton.Height;
        private const float PillDrawn = 20f;
        private const float StepDrawn = 28f;
        private const float ChevronDrawn = 10f;
        private const float ChevronBox = 12f;

        /// <summary>The tick is drawn 18 points across; the padlock fills about 62 of its box of 100.</summary>
        private const float TickDrawn = 18f;
        private const float LockBoxDrawn = 62f;

        /// <summary>The card's picture is the card and the room around it for its shadow: 30 points at the sides and top, 40 at the foot, the card's middle 110 points down a picture 230 high.</summary>
        private const float PictureMarginAcross = 60f;
        private const float PictureMarginUpright = 70f;
        private const float PictureTopMargin = 30f;
        private const float PivotDownThePicture = 110f / 230f;

        private static readonly Color EyebrowInk = Tint.Of(0x8A5A2B);
        private static readonly Color BylineInk = Tint.Of(0x6B4A2A);
        private static readonly Color PlaceInk = Tint.Of(0x0F5E66);
        private static readonly Color NoteInk = Tint.Of(0x7A5A3A);
        private static readonly Color AheadPillInk = Tint.Of(0x5A3A1C);
        private static readonly Color VeilTop = Tint.Of(0xFBF2DA);
        private static readonly Color VeilFoot = Tint.Of(0xEBD9AE);
        private static readonly Color VeilInk = Tint.Of(0x9C7A43);
        private static readonly Color DarkFace = Tint.Of(0x2A1606);
        private const float HereImageStrength = 0.7f;

        private readonly Transform _root;
        private readonly List<PaintedShape> _shapes = new List<PaintedShape>();

        /// <summary>The painting whose picture the card shows: the card holds it while it is up; nothing for a painting ahead, which is veiled.</summary>
        private PaintingLook? _look;
        private readonly List<(MapCardPress Press, MapButton Button)> _buttons = new List<(MapCardPress, MapButton)>();
        private readonly ArtCatalogue _art;

        private MapCard(Transform parent, ArtCatalogue art)
        {
            _art = art;
            _root = new GameObject("Painting card").transform;
            _root.SetParent(parent, worldPositionStays: false);
        }

        /// <summary>The card in the page's points.</summary>
        public Box Box { get; private set; }

        public PaintingNumber Painting { get; private set; }

        /// <summary>What the card's pill says: how the painting stands.</summary>
        public string Status { get; private set; } = string.Empty;

        /// <summary>The card's buttons, each with what it asks for.</summary>
        public IReadOnlyList<(MapCardPress Press, MapButton Button)> Buttons => _buttons;

        /// <param name="sheet">The sheet the card stands on, in the page's points.</param>
        public static MapCard Show(Transform parent, ArtCatalogue art, MapAtlas atlas, PaintingNumber painting, Box sheet)
        {
            var card = new MapCard(parent, art);
            card.Build(atlas, painting, sheet);
            return card;
        }

        public bool Covers(Vector2 point) => Box.Contains(point.x, point.y);

        public void Dispose()
        {
            foreach (PaintedShape shape in _shapes)
            {
                shape.Dispose();
            }

            _look?.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        private void Build(MapAtlas atlas, PaintingNumber painting, Box sheet)
        {
            Painting = painting;
            float width = Math.Min(WidestCard, sheet.Width - 2f * InFromTheSides);
            Box = new Box(sheet.MiddleX - width / 2f, sheet.Bottom + OverTheFoot, width, Height);
            _root.localPosition = new Vector3(Box.Left, Box.Top, 0f); // the card's own points: x to the right, y up from its top left corner

            SpriteRenderer face = SceneObjects.AddSprite("Card", _root, _art.Of(GalleryArt.MapCard), _art.SpriteMaterial, DrawOrder.MapCard);
            face.drawMode = SpriteDrawMode.Sliced;
            face.size = new Vector2(width + PictureMarginAcross, Height + PictureMarginUpright);
            float pivotDown = PivotDownThePicture * (Height + PictureMarginUpright);
            face.transform.localPosition = new Vector3(width / 2f, -(pivotDown - PictureTopMargin), 0f);

            MapMark mark = atlas.Standing.MarkOf(painting);
            Artwork? artwork = atlas.ArtworkAt(painting);
            AddPicture(atlas, painting, mark);
            float columnLeft = Inside + FrameSize + ColumnGap;
            float columnWidth = width - columnLeft - Inside - RoomForTheSteps;
            if (artwork != null)
            {
                AddWords(atlas, artwork, painting, columnLeft, columnWidth);
            }

            AddTheRow(atlas, painting, mark, width);
            AddCloseAndSteps(painting, width);
        }

        private void AddPicture(MapAtlas atlas, PaintingNumber painting, MapMark mark)
        {
            var middle = new Vector2(Inside + FrameSize / 2f, -(Inside + FrameSize / 2f));
            var picture = new Rect(middle.x - PictureSize / 2f, middle.y - PictureSize / 2f, PictureSize, PictureSize);
            var anywhere = new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero);
            PaintingLook? look = mark == MapMark.Ahead ? null : atlas.LookOf(painting);
            _look = look;
            if (look?.Picture is Texture2D texture)
            {
                // The painting being restored is seen dimmed, as at 70 % over the frame's dark ground.
                float aspect = look.Aspect;
                float across = Math.Min(1f, 1f / aspect);
                float upright = Math.Min(1f, aspect);
                Color light = mark == MapMark.Here ? MapOverlay.DimmedTo(HereImageStrength) : Color.white;
                var sketch = new MeshSketch();
                sketch.AddRectangle(picture, light, light, new Rect((1f - across) / 2f, (1f - upright) / 2f, across, upright));
                Keep(PaintedShape.Create("Picture", _root, _art.PaintMaterial, DrawOrder.MapCardPicture, texture)).Show(sketch.ToMesh("Card picture"));
            }
            else
            {
                var ground = new MeshSketch();
                ground.AddRectangle(picture, mark == MapMark.Ahead ? VeilFoot : DarkFace, mark == MapMark.Ahead ? VeilTop : DarkFace, anywhere);
                Keep(PaintedShape.Create("Picture ground", _root, _art.PaintMaterial, DrawOrder.MapCardPicture)).Show(ground.ToMesh("Card picture ground"));
            }

            if (mark == MapMark.Ahead)
            {
                TextMeshPro question = Lettering.Write("Veiled", _root, _art, LetteringStyle.Title, 26f, VeilInk, DrawOrder.MapCardLettering);
                Lettering.Say(question, "?");
                question.transform.localPosition = new Vector3(middle.x, middle.y + 7f, 0f);
                TextMeshPro veiled = Lettering.Write("Veiled label", _root, _art, LetteringStyle.Label, 8.5f, VeilInk, DrawOrder.MapCardLettering);
                veiled.characterSpacing = 6f;
                Lettering.SayWithin(veiled, MapWords.Veiled, 8.5f, PictureSize - 8f);
                veiled.transform.localPosition = new Vector3(middle.x, middle.y - 14f, 0f);
            }

            SpriteRenderer frame = SceneObjects.AddSprite("Frame", _root, _art.Of(GalleryArt.MapCardFrame), _art.SpriteMaterial, DrawOrder.MapCardArt);
            frame.transform.localPosition = middle;
        }

        private void AddWords(MapAtlas atlas, Artwork artwork, PaintingNumber painting, float left, float width)
        {
            float top = -Inside;
            top -= Block("Eyebrow", MapWords.CardEyebrow(painting), LetteringStyle.Label, EyebrowSize, EyebrowInk, left, width, top, 1, spacing: 9f) + LineGap;
            string title = MapWords.Title(artwork);
            float titleSize = title.Length > 34 ? 12.5f : title.Length > 24 ? 13.5f : TitleSize;
            top -= Block("Title", title, LetteringStyle.Title, titleSize, Lettering.Ink, left, width, top, 2, leastShare: LeastTitleShare) + LineGap;
            top -= Block("Byline", MapWords.ByLine(artwork), LetteringStyle.Soft, BylineSize, BylineInk, left, width, top, 1) + LineGap;

            PlaceWords words = atlas.WordsOf(artwork);
            Transform pin = SceneObjects.AddSprite("Place pin", _root, _art.Of(GalleryArt.IconPin), _art.SpriteMaterial, DrawOrder.MapCardArt).transform;
            pin.localScale = Vector3.one * (PlacePinWidth / 32f);
            pin.localPosition = new Vector3(left + PlacePinWidth / 2f, top - 1f - 11f * 35f / 44f, 0f);
            float placeLeft = left + PlacePinWidth + GapPinToPlace;
            top -= Block("Place", words.Place.Text, LetteringStyle.Label, PlaceSize, PlaceInk, placeLeft, width - (placeLeft - left), top, 2) + LineGap;
            string note = words.Note is PlaceLabel given ? given.Text : MapWords.Today(words.Museum.Text);
            Block("Note", note, LetteringStyle.Soft, NoteSize, NoteInk, left, width, top, 2);
        }

        /// <summary>The pill that says how the painting stands, at the left of the card's foot, and what it leads to at the right.</summary>
        private void AddTheRow(MapAtlas atlas, PaintingNumber painting, MapMark mark, float width)
        {
            float middle = -(Height - Inside - RowHeight / 2f);
            Status = mark switch
            {
                MapMark.Restored => MapWords.StatusRestored,
                MapMark.Here => atlas.Standing.PiecesHere is { } pieces ? MapWords.StatusPieces(pieces.Held, pieces.All, atlas.Standing.Level) : MapWords.BeingRestored,
                _ => MapWords.StatusOpensAt(painting),
            };
            (GalleryArt pillArt, Color ink, GalleryArt? icon) = mark switch
            {
                MapMark.Restored => (GalleryArt.MapPillRestored, Color.white, GalleryArt.GlyphTick),
                MapMark.Here => (GalleryArt.MapPillHere, Color.white, (GalleryArt?)null),
                _ => (GalleryArt.MapPillAhead, AheadPillInk, GalleryArt.IconLock),
            };

            float actionWidth = mark switch
            {
                MapMark.Here => AddPlay(width, middle),
                MapMark.Restored => AddTheLink(width, middle),
                _ => 0f,
            };

            TextMeshPro said = Lettering.Write("Status", _root, _art, LetteringStyle.Label, PillSize, ink, DrawOrder.MapCardButtonLettering);
            float iconRoom = icon != null ? PillIcon + 4f : 0f;
            float room = width - 2f * Inside - actionWidth - (actionWidth > 0f ? 8f : 0f) - 2f * PillPadding - iconRoom;
            Lettering.SayWithin(said, Status, PillSize, room);
            float pillWidth = Lettering.WidthOf(said, Status) + 2f * PillPadding + iconRoom;
            float pillLeft = Inside;
            said.transform.localPosition = new Vector3(pillLeft + PillPadding + iconRoom + Lettering.WidthOf(said, Status) / 2f, middle, 0f);
            SpriteRenderer pill = SceneObjects.AddSprite("Status pill", _root, _art.Of(pillArt), _art.SpriteMaterial, DrawOrder.MapCardButton);
            pill.drawMode = SpriteDrawMode.Sliced;
            float scale = PillHeight / PillDrawn;
            pill.size = new Vector2(pillWidth / scale + 4f, 24f);
            pill.transform.localScale = Vector3.one * scale;
            pill.transform.localPosition = new Vector3(pillLeft + pillWidth / 2f, middle, 0f);
            if (icon is GalleryArt drawing)
            {
                SpriteRenderer iconMark = SceneObjects.AddSprite("Status icon", _root, _art.Of(drawing), _art.SpriteMaterial, DrawOrder.MapCardButtonLettering);
                float drawn = drawing == GalleryArt.GlyphTick ? TickDrawn : LockBoxDrawn;
                iconMark.transform.localScale = Vector3.one * (PillIcon / drawn);
                iconMark.transform.localPosition = new Vector3(pillLeft + PillPadding - 1f + PillIcon / 2f, middle, 0f);
            }
        }

        /// <returns>How wide the button is.</returns>
        private float AddPlay(float width, float middle)
        {
            string label = MapWords.Play;
            TextMeshPro measure = Lettering.Write("Measure", _root, _art, LetteringStyle.OnButton, PlayLabelSize, Color.white, DrawOrder.MapCardButtonLettering);
            float labelWidth = Lettering.WidthOf(measure, label);
            SceneObjects.DestroyObjectOf(measure.transform);
            float buttonWidth = labelWidth + 2f * PlayPadding;
            float scale = RowHeight / GreenButtonDrawn;
            var button = new PushButton("Play", _root, _art, ButtonLook.Green, label, buttonWidth / scale, PlayLabelSize / scale, DrawOrder.MapCardButton, DrawOrder.MapCardButtonLettering);
            var at = new Vector2(width - Inside - buttonWidth / 2f, middle);
            button.Place(at, scale);
            AddButton(MapCardPress.Play, label, button.Face, at, buttonWidth, RowHeight);
            return buttonWidth;
        }

        /// <returns>How wide the link is.</returns>
        private float AddTheLink(float width, float middle)
        {
            Transform holder = new GameObject("To the collection").transform;
            holder.SetParent(_root, worldPositionStays: false);
            TextMeshPro said = Lettering.Write("Link", holder, _art, LetteringStyle.Label, LinkSize, PlaceInk, DrawOrder.MapCardButtonLettering);
            string words = MapWords.SeeInTheCollection;
            Lettering.Say(said, words);
            float linkWidth = Lettering.WidthOf(said, words);
            var at = new Vector2(width - Inside - linkWidth / 2f, middle);
            holder.localPosition = at;
            var underline = new MeshSketch();
            float baseline = -Lettering.CapitalsHeight(LetteringStyle.Label) * LinkSize / 2f;
            underline.AddRectangle(new Rect(-linkWidth / 2f, baseline - 2.5f, linkWidth, 1f), PlaceInk);
            Keep(PaintedShape.Create("Underline", holder, _art.PaintMaterial, DrawOrder.MapCardButtonLettering)).Show(underline.ToMesh("Link underline"));
            AddButton(MapCardPress.SeeInTheCollection, words, holder, at, linkWidth, RowHeight);
            return linkWidth;
        }

        private void AddCloseAndSteps(PaintingNumber painting, float width)
        {
            Transform close = new GameObject("Close").transform;
            close.SetParent(_root, worldPositionStays: false);
            var closeAt = new Vector2(width - CloseFromTheCorner, -CloseFromTheCorner);
            close.localPosition = closeAt;
            SpriteRenderer disc = SceneObjects.AddSprite("Disc", close, _art.Of(GalleryArt.TealDisc), _art.SpriteMaterial, DrawOrder.MapCardButton);
            disc.transform.localScale = Vector3.one * (CloseDisc / TealDiscDrawn);
            SpriteRenderer cross = SceneObjects.AddSprite("Cross", close, _art.Of(GalleryArt.GlyphClose), _art.SpriteMaterial, DrawOrder.MapCardButtonLettering);
            cross.transform.localScale = Vector3.one * (CloseDisc / TealDiscDrawn);
            AddButton(MapCardPress.Close, MapWords.CloseTheCard, close, closeAt, CloseDisc, CloseDisc);

            AddStep(MapCardPress.Previous, MapWords.PreviousPainting, width, PreviousDown, isDown: false);
            AddStep(MapCardPress.Next, MapWords.NextPainting, width, NextDown, isDown: true);
            TextMeshPro position = Lettering.Write("Position", _root, _art, LetteringStyle.Label, PositionSize, EyebrowInk, DrawOrder.MapCardLettering);
            Lettering.Say(position, MapWords.OfAll(painting.PlaceInRoom, MuseumPlan.PaintingsPerRoom));
            position.transform.localPosition = new Vector3(width - StepsFromTheRight, -PositionDown, 0f);
        }

        private void AddStep(MapCardPress press, string label, float width, float down, bool isDown)
        {
            Transform step = new GameObject(press.ToString()).transform;
            step.SetParent(_root, worldPositionStays: false);
            var at = new Vector2(width - StepsFromTheRight, -down);
            step.localPosition = at;
            SceneObjects.AddSprite("Disc", step, _art.Of(GalleryArt.MapStep), _art.SpriteMaterial, DrawOrder.MapCardButton).transform.localScale = Vector3.one * (StepDisc / StepDrawn);
            SpriteRenderer chevron = SceneObjects.AddSprite("Chevron", step, _art.Of(GalleryArt.MapGlyphChevron), _art.SpriteMaterial, DrawOrder.MapCardButtonLettering);
            chevron.transform.localScale = new Vector3(ChevronDrawn / ChevronBox, (isDown ? -1f : 1f) * ChevronDrawn / ChevronBox, 1f);
            AddButton(press, label, step, at, StepDisc, StepDisc);
        }

        /// <summary>A button of the card: its target is in the page's points, the card's own points moved to where it stands.</summary>
        private void AddButton(MapCardPress press, string label, Transform drawing, Vector2 onTheCard, float width, float height)
        {
            var button = new MapButton(label, drawing);
            button.Aim(new Vector2(Box.Left + onTheCard.x, Box.Top + onTheCard.y), width, height);
            _buttons.Add((press, button));
        }

        /// <summary>Words from the left of a width, in lines no more than a count, set smaller where they need it; the block's top at a height.</summary>
        /// <returns>How high the block is.</returns>
        /// <param name="leastShare">The smallest share of its size the block is set at; past that it takes another line.</param>
        private float Block(string name, string words, LetteringStyle style, float size, Color ink, float left, float width, float top, int mostLines, float spacing = 0f, float leastShare = LeastShare)
        {
            TextMeshPro line = Lettering.Write(name, _root, _art, style, size, ink, DrawOrder.MapCardLettering);
            line.characterSpacing = spacing;
            float lineHeight = line.GetPreferredValues("Ag").y;
            line.textWrappingMode = TextWrappingModes.Normal;
            for (float smaller = size; smaller >= size * leastShare; smaller -= 0.25f)
            {
                line.fontSize = smaller;
                lineHeight = line.GetPreferredValues("Ag").y;
                if (line.GetPreferredValues(LineBreaks.ReadyForWrapping(words), width, 0f).y <= (mostLines + 0.1f) * lineHeight)
                {
                    break;
                }
            }

            return Lettering.SayInLines(line, words, width, left, top);
        }

        private PaintedShape Keep(PaintedShape shape)
        {
            _shapes.Add(shape);
            return shape;
        }
    }
}
