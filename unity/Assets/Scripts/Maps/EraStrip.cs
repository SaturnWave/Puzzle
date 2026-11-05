#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// The strip of the twenty eras across the world map's foot (MapWorldTR): on a cream band, for each era a
    /// medallion with its room's number (gilt with a tick when restored, teal in a red ring where the player is,
    /// ivory with a padlock ahead) and its short name under it, the era the map shows lit from behind. A tap on a
    /// medallion takes the map there; a finger dragged sideways scrolls the strip (<see cref="EraStripScroll"/>).
    /// What scrolls past the sheet's sides fades out before it reaches the frame.
    /// </summary>
    public sealed class EraStrip : IDisposable
    {
        /// <summary>The design's strip: medallions 58 by 64 points, 7 under the band's top; a medal of 34 two points into its button; the name 3 under the medal, two lines of 9 points in 56.</summary>
        private const float MedalTop = 7f;
        private const float ButtonHeight = 64f;
        private const float MedalSize = 34f;
        private const float MedalInto = 2f;
        private const float NameUnderTheMedal = 3f;
        private const float NameSize = 9f;
        private const float NameWidth = 56f;

        /// <summary>A name set smaller than this share of its size would not be read; a longer one runs a little wider.</summary>
        private const float LeastNameShare = 0.6f;
        private const float NumberSize = 15f;
        private const float BadgeSize = 13f;

        /// <summary>How large the badges' drawings draw their badge at full size: the tick 15 points across, the padlock 13.</summary>
        private const float TickBadgeDrawn = 15f;
        private const float LockBadgeDrawn = 13f;

        /// <summary>A medallion fades out over this far as its middle nears the sheet's side.</summary>
        private const float FadesOver = EraStripScroll.MedalWidth / 2f;

        /// <summary>The band: cream from #FFFDF4 through #F6EACB at 60 % to #EBD9AE, a light line at its top, a dark edge over it and a soft shadow above.</summary>
        private static readonly ColourRamp Band = new ColourRamp((0f, Tint.Of(0xFFFDF4)), (0.6f, Tint.Of(0xF6EACB)), (1f, Tint.Of(0xEBD9AE)));
        private static readonly Color TopLight = Tint.Of(0xFFF4C4);
        private static readonly Color TopEdge = Tint.Of(0x8A5512);
        private static readonly Color ShadowAbove = Tint.Of(0x04202B, 0.28f);
        private const float ShadowHeight = 12f;
        private const float EdgeHeight = 1.5f;
        private const float LightHeight = 2f;

        private static readonly Color InkAhead = Tint.Of(0x8A6A4A);
        private static readonly Color NumberAhead = Tint.Of(0x9C7A43);

        private readonly Transform _root;
        private readonly PaintedShape _band;
        private readonly List<Medal> _medals = new List<Medal>();
        private readonly SpriteRenderer _chosen;
        private Box _strip;
        private RoomNumber? _shown;

        public EraStrip(Transform parent, ArtCatalogue art, MapAtlas atlas)
        {
            _root = new GameObject("Era strip").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _band = PaintedShape.Create("Band", _root, art.PaintMaterial, DrawOrder.MapStrip);
            _chosen = SceneObjects.AddSprite("Chosen", _root, art.Of(GalleryArt.EraChosen), art.SpriteMaterial, DrawOrder.MapStripChosen);
            foreach (RoomNumber room in RoomNumber.All)
            {
                _medals.Add(new Medal(_root, art, atlas, room));
            }

            Scroll = new EraStripScroll(MuseumPlan.RoomCount, 1f);
        }

        public EraStripScroll Scroll { get; }

        /// <summary>The medallions, in the order of the eras, each the button that takes the map there.</summary>
        public IReadOnlyList<MapButton> Buttons => _medals.ConvertAll(medal => medal.Button);

        /// <summary>The band in the page's points.</summary>
        public Box Box => _strip;

        public void Fit(Box strip)
        {
            _strip = strip;
            Scroll.Fit(strip.Width);
            var sketch = new MeshSketch();
            var anywhere = new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero);
            sketch.AddRectangle(new Rect(strip.Left, strip.Top, strip.Width, ShadowHeight), ShadowAbove, new Color(ShadowAbove.r, ShadowAbove.g, ShadowAbove.b, 0f), anywhere);
            sketch.AddNet(new Rect(strip.Left, strip.Bottom, strip.Width, strip.Height), 1, 4, place => Band.At((strip.Top - place.y) / strip.Height));
            sketch.AddRectangle(new Rect(strip.Left, strip.Top - LightHeight, strip.Width, LightHeight), TopLight);
            sketch.AddRectangle(new Rect(strip.Left, strip.Top, strip.Width, EdgeHeight), TopEdge);
            _band.Show(sketch.ToMesh("Era strip"));
        }

        /// <summary>Lights the era the map shows; nothing in the view of the whole world.</summary>
        public void Choose(RoomNumber? shown)
        {
            _shown = shown;
            foreach (Medal medal in _medals)
            {
                medal.Choose(medal.Room == shown);
            }
        }

        /// <summary>Puts every medallion where the strip is scrolled at a moment, faded near the sheet's sides.</summary>
        public void ScrollAt(float now)
        {
            float offset = Scroll.At(now);
            _chosen.enabled = false;
            foreach (Medal medal in _medals)
            {
                float middle = _strip.Left + EraStripScroll.LeftOf(medal.Room.Value) - offset + EraStripScroll.MedalWidth / 2f;
                float fromTheSide = Math.Min(middle - _strip.Left, _strip.Right - middle);
                float shown = Mathf.Clamp01(fromTheSide / FadesOver);
                float top = _strip.Top - MedalTop;
                medal.Place(new Vector2(middle, top), shown, _strip);
                if (medal.Room == _shown && shown > 0f)
                {
                    _chosen.enabled = true;
                    _chosen.transform.localPosition = new Vector3(middle, top - ButtonHeight / 2f, 0f);
                    _chosen.color = new Color(1f, 1f, 1f, shown);
                }
            }
        }

        public void Dispose()
        {
            _band.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>One era's medallion with its number, its badge, its short name, and the button it is.</summary>
        private sealed class Medal
        {
            private readonly Transform _root;
            private readonly SpriteRenderer _medal;
            private readonly SpriteRenderer? _badge;
            private readonly TextMeshPro _number;
            private readonly TextMeshPro _name;
            private readonly Color _numberColour;
            private readonly Color _nameColour;
            private readonly string _nameWords;
            private float _shown = 1f;

            public Medal(Transform parent, ArtCatalogue art, MapAtlas atlas, RoomNumber room)
            {
                Room = room;
                _root = new GameObject($"Era {room}").transform;
                _root.SetParent(parent, worldPositionStays: false);
                MapMark mark = atlas.Standing.MarkOf(room);
                MuseumRoom era = atlas.Catalogue.Room(room);
                Transform drawing = new GameObject("Drawing").transform;
                drawing.SetParent(_root, worldPositionStays: false);
                float medalMiddle = -(MedalInto + MedalSize / 2f);
                _medal = SceneObjects.AddSprite("Medal", drawing, art.Of(MedalOf(mark)), art.SpriteMaterial, DrawOrder.MapMedal);
                _medal.transform.localPosition = new Vector3(0f, medalMiddle, 0f);

                LetteringStyle numberStyle = mark == MapMark.Here ? LetteringStyle.OnTeal : LetteringStyle.Figure;
                _numberColour = mark == MapMark.Here ? Color.white : mark == MapMark.Ahead ? NumberAhead : Lettering.Ink;
                _number = Lettering.Write("Number", drawing, art, numberStyle, NumberSize, _numberColour, DrawOrder.MapMedalLettering);
                Lettering.Say(_number, room.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _number.transform.localPosition = new Vector3(0f, medalMiddle, 0f);

                if (mark != MapMark.Here)
                {
                    bool isRestored = mark == MapMark.Restored;
                    _badge = SceneObjects.AddSprite("Badge", drawing, art.Of(isRestored ? GalleryArt.MapTickBadge : GalleryArt.MapLockBadge), art.SpriteMaterial, DrawOrder.MapMedalBadge);
                    _badge.transform.localScale = Vector3.one * (BadgeSize / (isRestored ? TickBadgeDrawn : LockBadgeDrawn));
                    _badge.transform.localPosition = new Vector3(MedalSize / 2f + 3f - BadgeSize / 2f, medalMiddle - MedalSize / 2f - 2f + BadgeSize / 2f, 0f);
                }

                _nameColour = mark == MapMark.Ahead ? InkAhead : Lettering.Ink;
                _name = Lettering.Write("Name", drawing, art, LetteringStyle.Soft, NameSize, _nameColour, DrawOrder.MapMedalLettering);
                _name.alignment = TextAlignmentOptions.Center;
                _nameWords = MapWords.EraShortName(era);
                SayTheName(isChosen: false);

                string status = mark switch
                {
                    MapMark.Restored => MuseumWords.PaintingsWhole(atlas.Standing.RestoredIn(room)),
                    MapMark.Here => MapWords.LegendHere,
                    _ => MuseumWords.OpensAt(room),
                };
                Button = new MapButton(MapWords.EraButtonLabel(era, status), drawing);
            }

            public RoomNumber Room { get; }

            public MapButton Button { get; }

            public void Choose(bool isChosen) => SayTheName(isChosen);

            /// <summary>Puts the medallion's button with its top middle at a place, drawn at a share of its strength; a medallion half out of the strip is no button.</summary>
            public void Place(Vector2 topMiddle, float shown, Box strip)
            {
                _root.localPosition = new Vector3(topMiddle.x, topMiddle.y, 0f);
                _root.gameObject.SetActive(shown > 0f); // a medallion scrolled past the sheet's side is not drawn at all
                if (!Mathf.Approximately(shown, _shown))
                {
                    _shown = shown;
                    _medal.color = new Color(1f, 1f, 1f, shown);
                    if (_badge != null)
                    {
                        _badge.color = new Color(1f, 1f, 1f, shown);
                    }

                    Lettering.DrawIn(_number, new Color(_numberColour.r, _numberColour.g, _numberColour.b, shown));
                    Lettering.DrawIn(_name, new Color(_nameColour.r, _nameColour.g, _nameColour.b, shown));
                }

                Button.IsShown = shown >= 0.5f;
                Button.Aim(new Vector2(topMiddle.x, topMiddle.y - ButtonHeight / 2f), EraStripScroll.MedalWidth, ButtonHeight);
            }

            /// <summary>The short name in two lines at most under the medal, a weight heavier for the era shown.</summary>
            private void SayTheName(bool isChosen)
            {
                _name.fontStyle = isChosen ? FontStyles.Bold : FontStyles.Normal;
                _name.textWrappingMode = TextWrappingModes.NoWrap;
                _name.fontSize = NameSize;
                float WidthOf(string line) => _name.GetPreferredValues(line).x;

                // One line where it fits; else two, broken where the longer is the shortest (after a hyphen too), never inside a word.
                string said = _nameWords;
                if (WidthOf(_nameWords) > NameWidth)
                {
                    (string first, string second) = BalancedLines.SplitInTwo(_nameWords.Replace("-", "- "), WidthOf);
                    said = second.Length == 0 ? _nameWords : $"{first.Replace("- ", "-")}\n{second.Replace("- ", "-")}";
                }

                float widest = 0f;
                foreach (string line in said.Split('\n'))
                {
                    widest = Math.Max(widest, WidthOf(line));
                }

                _name.fontSize = NameSize * Math.Max(LeastNameShare, Math.Min(1f, NameWidth / widest));
                Lettering.Say(_name, said);
                float nameTop = -(MedalInto + MedalSize + NameUnderTheMedal);
                _name.transform.localPosition = new Vector3(0f, nameTop - _name.GetPreferredValues(said).y / 2f, 0f);
            }

            private static GalleryArt MedalOf(MapMark mark) => mark switch
            {
                MapMark.Restored => GalleryArt.EraRestored,
                MapMark.Here => GalleryArt.EraHere,
                MapMark.Ahead => GalleryArt.EraAhead,
                _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, "A mark without a medallion."),
            };
        }
    }
}
