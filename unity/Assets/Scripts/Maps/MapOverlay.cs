#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using Puzzle.Museum.Places;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// What stands on the world map over the land for one view at rest (MapWorldTR): in the world's view the route
    /// through the eras in their order (gilt as far as the player's era, dotted beyond), the oceans' names, every
    /// painting as a dot and the pin of where the player is; in an era's view the other eras' paintings as dots and
    /// the era's five paintings as pins over their places, with a line to each place that a pin stands away from,
    /// and over the pin of the painting being restored the marker "Buradasın · Bölüm N". It is built for a view once
    /// the camera has come to rest there and fades in; it fades out as the camera leaves.
    /// </summary>
    public sealed class MapOverlay : IDisposable
    {
        private const float PinPictureSize = 32f;

        /// <summary>The middle of a pin's picture over the tip of its pointer, and its badges' places, in the design's points (MapWorldTR: the 46 by 52 pin with its tip at (23, 47)).</summary>
        private const float PictureOverTheTip = 26f;
        private static readonly Vector2 TickOverTheTip = new Vector2(15.5f, 42.5f);
        private const float CountLeftOfTheTip = -1f;
        private const float CountOverTheTip = 46.75f;
        private const float CountHeight = 12.5f;
        private const float CountSize = 8.5f;
        private const float CountPadding = 4f;
        private const float ChosenScale = 1.18f;
        private const float HereImageStrength = 0.62f;

        private const float DotMargin = 8f;
        private const float LeaderWidth = 1.6f;
        private static readonly Color LeaderColour = Tint.Of(0x5E3305, 0.75f);

        /// <summary>The route: the part walked gilt (2 points) over a dark line (3.6 at 50 %), the part ahead dotted in cream (1.6 at 75 %, dots of 2 every 6).</summary>
        private static readonly Color RouteShade = Tint.Of(0x5E3305, 0.5f);
        private static readonly Color RouteGilt = Tint.Of(0xFFD25E);
        private static readonly Color RouteAhead = Tint.Of(0xFFF4C4, 0.75f);
        private const float SoftEdge = 0.6f;

        /// <summary>The oceans' names, as the design writes them in the world's view, and where.</summary>
        private static readonly (double Longitude, double Latitude, bool IsTheAtlantic)[] Oceans = { (-36, 22, true), (72, -26, false) };
        private const float OceanSize = 8.5f;
        private const float OceanSpacing = 18f;
        private static readonly Color OceanInk = Tint.Of(0xE6F5F1, 0.62f);

        /// <summary>The marker of where the player is: a cream label 17 points high over a red pin 20 by 27 whose point is 21.5 points under its top.</summary>
        private const float HereLabelSize = 10f;
        private const float HereLabelHeight = 17f;
        private const float HereLabelPadding = 8f;
        private const float HereLabelAbovePinTop = 40f;
        private const float HerePinWidth = 20f;
        private const float HerePinPoint = 21.5f;
        private const float HereLabelFromTheSide = 64f;
        private const float WorldPinOverTheDot = 4.5f;
        private const float PinIconWidth = 32f;

        /// <summary>The pill's drawing: 112 by 24 inside its 128 by 40 picture.</summary>
        private const float PillDrawnHeight = 24f;
        private const float PillPictureMarginAcross = 16f;
        private const float PillPictureHeight = 40f;

        private static readonly Color VeilTop = Tint.Of(0xFBF2DA);
        private static readonly Color VeilFoot = Tint.Of(0xEBD9AE);
        private static readonly Color VeilInk = Tint.Of(0x9C7A43);
        private static readonly Color DarkFace = Tint.Of(0x2A1606);

        private readonly Transform _root;
        private readonly List<PaintedShape> _shapes = new List<PaintedShape>();

        /// <summary>The paintings whose pictures the pins show: the overlay holds them while it is up.</summary>
        private readonly List<PaintingLook> _looks = new List<PaintingLook>();

        private MapOverlay(Transform parent)
        {
            _root = new GameObject("Map overlay").transform;
            _root.SetParent(parent, worldPositionStays: false);
        }

        public Fading Fading { get; } = new Fading();

        /// <param name="chosen">The painting whose card is open; its pin is ringed and larger, and the marker of where the player is gives way to the card.</param>
        /// <param name="pins">The buttons of an era's five pins, by the painting's place in its room: each one whose pin is drawn is aimed at it and shown.</param>
        public static MapOverlay Build(
            Transform parent, ArtCatalogue art, MapAtlas atlas, MapLayout layout, MapView view, MapFrame frame, PaintingNumber? chosen, IReadOnlyList<MapButton> pins)
        {
            var overlay = new MapOverlay(parent);
            if (view.Era is RoomNumber era)
            {
                overlay.BuildTheEra(art, atlas, layout, era, frame, chosen, pins);
            }
            else
            {
                overlay.BuildTheWorld(art, atlas, layout, frame);
            }

            return overlay;
        }

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

        private void BuildTheWorld(ArtCatalogue art, MapAtlas atlas, MapLayout layout, MapFrame frame)
        {
            MapStanding standing = atlas.Standing;
            var route = new List<Vector2>();
            foreach (RoomNumber room in RoomNumber.All)
            {
                if (atlas.CentreOf(room) is MapPoint centre)
                {
                    route.Add(PageOf(layout, frame, centre));
                }
            }

            int walked = Math.Min(standing.Room.Value, route.Count);
            var sketch = new MeshSketch();
            if (walked < route.Count)
            {
                Stroke.AddDashed(sketch, route.GetRange(walked - 1, route.Count - walked + 1), 1.6f, SoftEdge, RouteAhead, 2f, 4f);
            }

            Stroke.Add(sketch, route.GetRange(0, walked), isClosed: false, 3.6f, SoftEdge, RouteShade);
            Stroke.Add(sketch, route.GetRange(0, walked), isClosed: false, 2f, SoftEdge, RouteGilt);
            Shape("Route", art, DrawOrder.MapRoute).Show(sketch.ToMesh("Map route"));

            foreach ((double longitude, double latitude, bool isTheAtlantic) in Oceans)
            {
                TextMeshPro name = Lettering.Write("Ocean", _root, art, LetteringStyle.Title, OceanSize, OceanInk, DrawOrder.MapOceanName);
                name.fontStyle = FontStyles.Italic;
                name.characterSpacing = OceanSpacing;
                Lettering.Say(name, isTheAtlantic ? MapWords.AtlanticOcean : MapWords.IndianOcean);
                name.transform.localPosition = PageOf(layout, frame, NaturalEarthProjection.Project(longitude, latitude));
                Fading.Add(name);
            }

            AddDots(art, atlas, layout, frame, exceptTheEra: null);
            if (standing.Here is PaintingNumber here && atlas.PlaceOf(here) is MapPoint place)
            {
                Vector2 dot = PageOf(layout, frame, place);
                AddHerePin(art, new Vector2(dot.x, dot.y + WorldPinOverTheDot));
            }
        }

        private void BuildTheEra(ArtCatalogue art, MapAtlas atlas, MapLayout layout, RoomNumber era, MapFrame frame, PaintingNumber? chosen, IReadOnlyList<MapButton> pins)
        {
            AddDots(art, atlas, layout, frame, era);
            var places = new List<(int Painting, float X, float Y)>();
            foreach ((PaintingNumber painting, MapPoint place) in atlas.PlacesIn(era))
            {
                (float x, float y) = frame.SheetOf(place);
                places.Add((painting.Value, x, y));
            }

            var leaders = new MeshSketch();
            MapStanding standing = atlas.Standing;
            Vector2? herePinTop = null;
            foreach (PinPlace pin in PinRows.Of(places, layout.MapWidth, layout.MapHeight))
            {
                var painting = new PaintingNumber(pin.Painting);
                Vector2 tip = Page(layout, pin.TipX, pin.TipY);
                Vector2 anchor = Page(layout, pin.PlaceX, pin.PlaceY);
                if (pin.HasALeader)
                {
                    Stroke.Add(leaders, new[] { tip, anchor }, isClosed: false, LeaderWidth, SoftEdge, LeaderColour);
                }

                Transform anchorDot = Fading.Add(SceneObjects.AddSprite("Anchor", _root, art.Of(GalleryArt.MapAnchor), art.SpriteMaterial, DrawOrder.MapPinFoot)).transform;
                anchorDot.localPosition = anchor;
                MapMark mark = standing.MarkOf(painting);
                bool isChosen = painting == chosen;
                AddPin(art, atlas, painting, mark, tip, isChosen, pins[painting.PlaceInRoom - 1]);
                if (mark == MapMark.Here && chosen == null)
                {
                    herePinTop = new Vector2(tip.x, tip.y + PinRows.PinHeight - 5f);
                }
            }

            Shape("Leaders", art, DrawOrder.MapLeader).Show(leaders.ToMesh("Map leaders"));
            if (herePinTop is Vector2 top && standing.Room == era)
            {
                AddHereLabel(art, layout, atlas.Standing.Level, top);
            }
        }

        private void AddDots(ArtCatalogue art, MapAtlas atlas, MapLayout layout, MapFrame frame, RoomNumber? exceptTheEra)
        {
            foreach (PaintingNumber painting in PaintingNumber.All)
            {
                if (painting.Room == exceptTheEra || !(atlas.PlaceOf(painting) is MapPoint place))
                {
                    continue;
                }

                (float x, float y) = frame.SheetOf(place);
                bool isSeen = x > -DotMargin && x < layout.MapWidth + DotMargin && y > -DotMargin && y < layout.MapHeight + DotMargin;
                if (!isSeen)
                {
                    continue;
                }

                GalleryArt dot = atlas.Standing.MarkOf(painting) switch
                {
                    MapMark.Restored => GalleryArt.MapDotRestored,
                    MapMark.Here => GalleryArt.MapDotHere,
                    _ => GalleryArt.MapDotAhead,
                };
                Transform drawn = Fading.Add(SceneObjects.AddSprite($"Dot {painting}", _root, art.Of(dot), art.SpriteMaterial, DrawOrder.MapDot)).transform;
                drawn.localPosition = Page(layout, x, y);
            }
        }

        private void AddPin(ArtCatalogue art, MapAtlas atlas, PaintingNumber painting, MapMark mark, Vector2 tip, bool isChosen, MapButton button)
        {
            Transform pin = new GameObject($"Pin {painting}").transform;
            pin.SetParent(_root, worldPositionStays: false);
            pin.localPosition = tip;
            pin.localScale = Vector3.one * (isChosen ? ChosenScale : 1f);

            var picture = new Rect(-PinPictureSize / 2f, PictureOverTheTip - PinPictureSize / 2f, PinPictureSize, PinPictureSize);
            var anywhere = new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero);
            PaintingLook? look = mark == MapMark.Ahead ? null : atlas.LookOf(painting);
            if (look != null)
            {
                _looks.Add(look);
            }

            if (look?.Picture is Texture2D texture)
            {
                // The painting being restored is seen dimmed, as at 62 % over the frame's dark ground.
                Color light = mark == MapMark.Here ? DimmedTo(HereImageStrength) : Color.white;
                var sketch = new MeshSketch();
                sketch.AddRectangle(picture, light, light, SquareOf(look.Aspect));
                PaintedShape image = PaintedShape.Create("Picture", pin, art.PaintMaterial, DrawOrder.MapPinPicture, texture);
                _shapes.Add(image);
                Fading.Add(image);
                image.Show(sketch.ToMesh("Pin picture"));
            }
            else
            {
                var face = new MeshSketch();
                face.AddRectangle(picture, mark == MapMark.Ahead ? VeilFoot : DarkFace, mark == MapMark.Ahead ? VeilTop : DarkFace, anywhere);
                Shape("Face", art, DrawOrder.MapPinPicture, pin).Show(face.ToMesh("Pin face"));
            }

            GalleryArt frame = isChosen ? GalleryArt.MapPinChosen : mark == MapMark.Here ? GalleryArt.MapPinHere : GalleryArt.MapPin;
            Fading.Add(SceneObjects.AddSprite("Frame", pin, art.Of(frame), art.SpriteMaterial, DrawOrder.MapPinFrame));

            if (mark == MapMark.Ahead)
            {
                TextMeshPro veiled = Lettering.Write("Veiled", pin, art, LetteringStyle.Title, 15f, VeilInk, DrawOrder.MapPinVeil);
                Lettering.Say(veiled, "?");
                veiled.transform.localPosition = new Vector3(0f, PictureOverTheTip, 0f);
                Fading.Add(veiled);
            }
            else if (mark == MapMark.Restored)
            {
                Transform tick = Fading.Add(SceneObjects.AddSprite("Restored", pin, art.Of(GalleryArt.MapTickBadge), art.SpriteMaterial, DrawOrder.MapPinBadge)).transform;
                tick.localPosition = TickOverTheTip;
            }
            else if (atlas.Standing.PiecesHere is { } pieces)
            {
                AddCount(art, pin, MapWords.OfAll(pieces.Held, pieces.All));
            }

            Artwork? artwork = atlas.ArtworkAt(painting);
            string status = mark switch
            {
                MapMark.Restored => MapWords.StatusRestored,
                MapMark.Here => MapWords.BeingRestored,
                _ => MapWords.StatusOpensAt(painting),
            };
            string label = artwork != null ? MapWords.PinLabel(MapWords.Title(artwork), atlas.WordsOf(artwork).Place.Text, status) : status;
            button.Attach(pin, label);
            button.Aim(new Vector2(tip.x, tip.y + PinRows.PinHeight / 2f - 5f), PinRows.PinWidth, PinRows.PinHeight);
            button.IsShown = true;
        }

        /// <summary>The red badge with the pieces won on the pin being restored, its left end over the pin's middle.</summary>
        private void AddCount(ArtCatalogue art, Transform pin, string count)
        {
            TextMeshPro said = Lettering.Write("Count", pin, art, LetteringStyle.Label, CountSize, Color.white, DrawOrder.MapPinBadgeLettering);
            Lettering.Say(said, count);
            float width = Lettering.WidthOf(said, count) + 2f * CountPadding;
            float middle = CountLeftOfTheTip + 2f + width / 2f;
            said.transform.localPosition = new Vector3(middle, CountOverTheTip, 0f);
            SpriteRenderer badge = SceneObjects.AddSprite("Count badge", pin, art.Of(GalleryArt.MapCountBadge), art.SpriteMaterial, DrawOrder.MapPinBadge);
            badge.drawMode = SpriteDrawMode.Sliced;
            float scale = CountHeight / 13f;
            badge.size = new Vector2(width / scale + 4f, 17f);
            badge.transform.localScale = Vector3.one * scale;
            badge.transform.localPosition = new Vector3(middle, CountOverTheTip, 0f);
            Fading.Add(badge);
            Fading.Add(said);
        }

        private void AddHereLabel(ArtCatalogue art, MapLayout layout, int level, Vector2 pinTop)
        {
            float pinPoint = pinTop.y + 1f;
            AddHerePin(art, new Vector2(pinTop.x, pinPoint));
            float labelMiddleY = pinPoint + HerePinPoint + 1f + HereLabelHeight / 2f;
            (float sheetX, float sheetY) = layout.SheetOf(pinTop.x, labelMiddleY);
            bool keepsClearOfTheButtons = sheetY - HereLabelHeight / 2f < PinRows.ButtonsFoot;
            float least = HereLabelFromTheSide;
            float most = layout.MapWidth - HereLabelFromTheSide - (keepsClearOfTheButtons ? PinRows.PinWidth : 0f);
            float labelX = layout.PageOf(Math.Min(Math.Max(sheetX, least), Math.Max(least, most)), sheetY).X;

            string words = MapWords.YouAreHere(level);
            TextMeshPro said = Lettering.Write("You are here", _root, art, LetteringStyle.Label, HereLabelSize, Lettering.Ink, DrawOrder.MapHereLettering);
            Lettering.Say(said, words);
            said.transform.localPosition = new Vector3(labelX, labelMiddleY, 0f);
            float width = Lettering.WidthOf(said, words) + 2f * HereLabelPadding;
            float scale = HereLabelHeight / PillDrawnHeight;
            SpriteRenderer pill = SceneObjects.AddSprite("You are here pill", _root, art.Of(GalleryArt.Pill), art.SpriteMaterial, DrawOrder.MapHereLabel);
            pill.drawMode = SpriteDrawMode.Sliced;
            pill.size = new Vector2(width / scale + PillPictureMarginAcross, PillPictureHeight);
            pill.transform.localScale = Vector3.one * scale;
            pill.transform.localPosition = new Vector3(labelX, labelMiddleY, 0f);
            Fading.Add(pill);
            Fading.Add(said);
        }

        /// <summary>The red pin of where the player is, its point at a place.</summary>
        private void AddHerePin(ArtCatalogue art, Vector2 point)
        {
            Transform pin = Fading.Add(SceneObjects.AddSprite("Here", _root, art.Of(GalleryArt.IconPin), art.SpriteMaterial, DrawOrder.MapHerePin)).transform;
            pin.localPosition = point;
            pin.localScale = Vector3.one * (HerePinWidth / PinIconWidth);
        }

        private PaintedShape Shape(string name, ArtCatalogue art, DrawOrder order, Transform? parent = null)
        {
            PaintedShape shape = PaintedShape.Create(name, parent ?? _root, art.PaintMaterial, order);
            _shapes.Add(shape);
            Fading.Add(shape);
            return shape;
        }

        /// <summary>A picture drawn at a share of its strength over the dark ground of a frame: its colours darkened toward that ground.</summary>
        public static Color DimmedTo(float strength) => Color.Lerp(DarkFace, Color.white, strength);

        /// <summary>The square in the middle of a picture of given proportions, as shares of its sides.</summary>
        private static Rect SquareOf(float aspect)
        {
            float across = Math.Min(1f, 1f / aspect);
            float upright = Math.Min(1f, aspect);
            return new Rect((1f - across) / 2f, (1f - upright) / 2f, across, upright);
        }

        private static Vector2 PageOf(MapLayout layout, MapFrame frame, MapPoint point)
        {
            (float x, float y) = frame.SheetOf(point);
            return Page(layout, x, y);
        }

        private static Vector2 Page(MapLayout layout, float sheetX, float sheetY)
        {
            (float x, float y) = layout.PageOf(sheetX, sheetY);
            return new Vector2(x, y);
        }
    }
}
