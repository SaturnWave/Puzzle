#nullable enable
using System;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Screens;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Collections
{
    /// <summary>
    /// One painting on the collection's wall, as the design canvas hangs a room's paintings (CollectionTR): its
    /// canvas in a narrow gilt frame with a brass plate under it. A painting that is whole shows its picture; one
    /// that is being restored shows the pieces that are in place, a pill that counts them, and a red badge when
    /// pieces wait to be placed; one the player has not reached hangs under a dust sheet, and its plate says which
    /// levels restore it; one no work is chosen for yet hangs under a dust sheet too, and its plate says that it
    /// is being prepared. Sizes are in points.
    /// </summary>
    public sealed class ExhibitFrame : IPressable, IDisposable
    {
        /// <summary>The plate is the hall's at this share of its size.</summary>
        private const float PlateScale = CollectionLayout.PlateHeight / HallLayout.PlateHeight;

        /// <summary>The wires meet the frame's top edge this far in from its corners.</summary>
        private const float WireInFromCorner = 22f;

        /// <summary>
        /// The pill that counts the pieces in place rides on the frame's top edge; the badge of the waiting pieces
        /// sits on the frame's upper right corner. The pill is the game's own: the design counts the pieces on a
        /// card beside the painting (CollectionTR, "6 / 10 parça"), which the collection's page does not have.
        /// </summary>
        private const float PillPictureMargin = 16f;
        private const float PillPictureHeight = 40f;
        private const float PillSideRoom = 10f;
        private const float PillScale = 0.8f;
        private const float PillWordsSize = 12f;
        private const float BadgeScale = 0.9f;

        /// <summary>A painting that is held down loses a little of its light.</summary>
        private static readonly Color PressedShade = new Color(0.85f, 0.85f, 0.85f, 1f);

        private readonly Transform _root;
        private readonly PaintingLook _look;
        private readonly JigsawCanvas _canvas;
        private readonly PaintedShape _cloth;
        private readonly PaintedShape _wires;
        private readonly SpriteRenderer _frame;
        private readonly CaptionPlate _plate;
        private readonly Transform? _pill;
        private readonly Transform? _badge;
        private ExhibitPlace? _place;

        private ExhibitFrame(Transform parent, ArtCatalogue art, PaintingLook look, HungPainting painting)
        {
            _look = look;
            Painting = painting;
            _root = new GameObject($"Painting {painting.Number}").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wires = PaintedShape.Create("Wires", _root, art.PaintMaterial, DrawOrder.Wire);
            _canvas = JigsawCanvas.Create(_root, art, look);
            _cloth = PaintedShape.Create("Dust sheet", _root, art.PaintMaterial, DrawOrder.DustSheet);

            _frame = SceneObjects.AddSprite("Frame", _root, art.Of(GalleryArt.PictureFrame), art.SpriteMaterial, DrawOrder.PictureFrame);
            _frame.drawMode = SpriteDrawMode.Tiled; // the middle of each side repeats, bead by bead; the corners keep their shape
            _frame.tileMode = SpriteTileMode.Adaptive;
            _frame.transform.localScale = new Vector3(CollectionLayout.FrameScale, CollectionLayout.FrameScale, 1f);
            _plate = new CaptionPlate(_root, art);

            if (painting.Standing == PaintingStanding.BeingRestored)
            {
                _pill = AddPill(art, MuseumWords.PiecesInPlace(painting.PiecesInPlace.Count, painting.PieceCount));
                _badge = painting.PiecesWaiting > 0 ? AddBadge(art, painting.PiecesWaiting) : null;
            }
        }

        /// <summary>The painting as the player's progress has it.</summary>
        public HungPainting Painting { get; }

        /// <summary>The middle of the painting's picture, in the points of what it hangs on: where a finger touches it.</summary>
        /// <exception cref="InvalidOperationException">The painting was not hung yet.</exception>
        public Vector2 Middle => _place is ExhibitPlace place
            ? new Vector2(place.Picture.MiddleX, place.Picture.MiddleY)
            : throw new InvalidOperationException("The painting hangs nowhere yet; call Place first.");

        /// <summary>The painting's picture on the wall; nothing before it is hung.</summary>
        public Box? Picture => _place?.Picture;

        public static ExhibitFrame Create(Transform parent, ArtCatalogue art, PaintingLook look, HungPainting painting) =>
            new ExhibitFrame(parent, art, look, painting);

        /// <summary>Hangs the painting where the layout has it; call again whenever the screen changes.</summary>
        /// <param name="railBottom">The foot of the picture rail, which the wires of the top row come down from.</param>
        public void Place(ExhibitPlace place, ScreenView view, float railBottom)
        {
            _place = place;
            Box picture = place.Picture;
            var size = new Vector2(picture.Width, picture.Height);
            var middle = new Vector3(picture.MiddleX, picture.MiddleY, 0f);
            float softEdge = 1f / view.PixelsPerPoint;

            _canvas.Transform.localPosition = middle;
            _canvas.Show(Painting.PiecesInPlace.ToArray(), size, softEdge);
            _cloth.Show(IsCovered ? DustSheet.Over(picture, place.FrameWidth, softEdge) : new MeshSketch().ToMesh("No dust sheet"));

            _frame.transform.localPosition = middle;
            _frame.size = size / CollectionLayout.FrameScale + new Vector2(FramedPainting.FramePictureMargin, FramedPainting.FramePictureMargin);
            _wires.Show(place.HangsFromTheRail ? Wires(place.Frame, railBottom) : new MeshSketch().ToMesh("No wires"));
            _plate.Place(new Vector2(place.Frame.MiddleX, place.PlateMiddle), place.WidestPlate, TitleOnThePlate(), ByLineOnThePlate(), PlateScale);

            if (_pill != null)
            {
                _pill.localPosition = new Vector3(place.Frame.MiddleX, place.Frame.Top, 0f);
            }

            if (_badge != null)
            {
                _badge.localPosition = new Vector3(place.BadgeMiddleX, place.BadgeMiddleY, 0f);
            }
        }

        public bool IsUnder(Vector2 point) => _place is ExhibitPlace place && place.Touch.Contains(point.x, point.y);

        public void ShowPressed(bool isPressed) => _frame.color = isPressed ? PressedShade : Color.white;

        public void Dispose()
        {
            _canvas.Dispose();
            _cloth.Dispose();
            _wires.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>True for a painting that hangs under its dust sheet: one the player has not reached, and one no work is chosen for yet.</summary>
        private bool IsCovered => Painting.Standing == PaintingStanding.NotReached || Painting.Standing == PaintingStanding.ToCome;

        /// <summary>A painting under its dust sheet does not give its name away: its plate says which painting of the museum it is.</summary>
        private string TitleOnThePlate() => IsCovered
            ? MuseumWords.TitleOf(Painting.Number, artwork: null)
            : MuseumWords.TitleOf(Painting.Number, Painting.Artwork);

        /// <summary>Under the title: the painter and the year; under a dust sheet, the levels that restore the painting, or that it is being prepared.</summary>
        private string ByLineOnThePlate() => Painting.Standing switch
        {
            PaintingStanding.ToCome => MuseumWords.PaintingToCome,
            PaintingStanding.NotReached => MuseumWords.LevelsOf(Painting.Number),
            PaintingStanding.BeingRestored => MuseumWords.ByLineOf(Painting.Artwork),
            PaintingStanding.Whole => MuseumWords.ByLineOf(Painting.Artwork),
            _ => throw new InvalidOperationException($"A painting that is {Painting.Standing} has no plate."),
        };

        /// <summary>The two wires from the picture rail down to the frame's top edge.</summary>
        private static Mesh Wires(Box frame, float railBottom)
        {
            var sketch = new MeshSketch();
            float inFromCorner = Mathf.Min(WireInFromCorner, frame.Width / 4f);
            PictureWire.Add(sketch, frame.Left + inFromCorner, frame.Top, railBottom);
            PictureWire.Add(sketch, frame.Right - inFromCorner - PictureWire.Width, frame.Top, railBottom);
            return sketch.ToMesh("Painting wires");
        }

        private Transform AddPill(ArtCatalogue art, string words)
        {
            SpriteRenderer pill = SceneObjects.AddSprite("Pieces in place", _root, art.Of(GalleryArt.Pill), art.SpriteMaterial, DrawOrder.ExhibitLabel);
            pill.drawMode = SpriteDrawMode.Tiled; // the picture's middle repeats, unsqueezed, for a pill narrower than its picture; its round ends keep their shape
            pill.transform.localScale = new Vector3(PillScale, PillScale, 1f);
            TextMeshPro line = Lettering.Write("Words", pill.transform, art, LetteringStyle.Label, PillWordsSize, Lettering.Ink, DrawOrder.ExhibitLabelLettering);
            Lettering.Say(line, words);
            pill.size = new Vector2(Lettering.WidthOf(line, words) + 2f * PillSideRoom + PillPictureMargin, PillPictureHeight);
            return pill.transform;
        }

        private Transform AddBadge(ArtCatalogue art, int piecesWaiting)
        {
            Transform badge = CountBadge.Add("Pieces waiting", _root, art, BadgeFace.Waiting(piecesWaiting), DrawOrder.ExhibitBadge).Transform;
            badge.localScale = new Vector3(BadgeScale, BadgeScale, 1f);
            return badge;
        }
    }
}
