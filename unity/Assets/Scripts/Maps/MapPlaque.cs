#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// The plaque over the world map, as the design canvas draws it (MapWorldTR): in the view of the whole world
    /// "20 DÖNEM · 100 TABLO" over "Dünya Haritası", the player's room and a bead for each of the twenty eras; in an
    /// era's view the era's room and years over its name, how many of its five paintings are restored (or the level
    /// it opens at) and a bead for each of them. A bead is gilt for what is restored, red for where the player is,
    /// sunk cream for what is ahead.
    /// </summary>
    public sealed class MapPlaque : IDisposable
    {
        /// <summary>The two lines at the left stand this far over and under the plaque's middle; so do the label and the beads at the right.</summary>
        private const float LabelUp = 9.5f;
        private const float TitleDown = 6f;
        private const float RightLabelUp = 6.5f;
        private const float BeadsDown = 8f;

        /// <summary>Lettering begins a little further in than the plaque's padding, as the design has it for a plaque without a badge.</summary>
        private const float LinesBeginIn = 17f;

        /// <summary>The title keeps this much room clear of what stands at the right.</summary>
        private const float GapToTheRight = 12f;

        /// <summary>A bead: a small square of 6 in a dark line, over its drop; the world's twenty stand 2.6 apart, an era's five 4.</summary>
        private const float BeadSide = 6f;
        private const float WorldBeadGap = 2.6f;
        private const float EraBeadGap = 4f;
        private const float BeadLine = 1.4f;
        private const float BeadDrop = 2.5f;
        private static readonly Color GiltLight = Tint.Of(0xFFF4C4);
        private static readonly Color Gilt = Tint.Of(0xF6B93C);
        private static readonly Color RedLight = Tint.Of(0xFFA283);
        private static readonly Color Red = Tint.Of(0xF4472F);
        private static readonly Color DarkLine = Tint.Of(0x5E3305);
        private static readonly Color RedLine = Tint.Of(0x6E1208);
        private static readonly Color Shadow = Tint.Of(0x5C3810, 0.3f);
        private static readonly Color SunkFace = Tint.Of(0xE3CC9C);
        private static readonly Color SunkTop = Tint.Of(0xC4A777);
        private static readonly Color SunkLine = Tint.Of(0x5E3305, 0.6f);

        private readonly PagePlaque _plaque;
        private readonly TextMeshPro _label;
        private readonly TextMeshPro _title;
        private readonly TextMeshPro _rightLabel;
        private readonly PaintedShape _beads;
        private readonly float _left = -PagePlaque.DesignWidth / 2f + LinesBeginIn;
        private readonly float _right = PagePlaque.DesignWidth / 2f - PagePlaque.SidePadding - 1f;

        private MapPlaque(Transform parent, ArtCatalogue art)
        {
            _plaque = new PagePlaque("Map plaque", parent, art);
            Transform content = _plaque.Content;
            _label = PagePlaque.WriteLabel("Label", content, art, DrawOrder.PlaqueLettering);
            _title = Lettering.Write("Title", content, art, LetteringStyle.Title, PagePlaque.TitleSize, Lettering.Ink, DrawOrder.PlaqueLettering);
            _rightLabel = PagePlaque.WriteLabel("Right label", content, art, DrawOrder.PlaqueLettering);
            _beads = PaintedShape.Create("Beads", content, art.PaintMaterial, DrawOrder.PlaqueArt);
        }

        public static MapPlaque Create(Transform parent, ArtCatalogue art) => new MapPlaque(parent, art);

        /// <summary>What the plaque says now, for the walk's checks.</summary>
        public string Says => $"{_label.text} / {_title.text} / {_rightLabel.text}";

        public void Place(PageLayout page) => _plaque.Place(page.Plaque);

        /// <summary>The plaque of a view of the map.</summary>
        public void Show(MapView view, MapAtlas atlas)
        {
            MapStanding standing = atlas.Standing;
            if (!(view.Era is RoomNumber era))
            {
                Say(MapWords.WorldSummary, MapWords.WorldTitle, MuseumWords.RoomOfRooms(standing.Room), MuseumPlan.RoomCount, WorldBeadGap);
                var beads = new List<MapMark>();
                foreach (RoomNumber room in RoomNumber.All)
                {
                    beads.Add(standing.MarkOf(room));
                }

                _beads.Show(Beads(beads, WorldBeadGap));
                return;
            }

            MuseumRoom shown = atlas.Catalogue.Room(era);
            bool isAhead = standing.MarkOf(era) == MapMark.Ahead && era.Value > standing.Room.Value;
            string right = isAhead ? MapWords.EraOpensAt(era) : MuseumWords.PaintingsWhole(standing.RestoredIn(era));
            Say(MapWords.EraLabel(shown), MapWords.EraName(shown), right, MuseumPlan.PaintingsPerRoom, EraBeadGap);
            var marks = new List<MapMark>();
            foreach (PaintingNumber painting in era.Paintings)
            {
                marks.Add(standing.MarkOf(painting));
            }

            _beads.Show(Beads(marks, EraBeadGap));
        }

        public void Dispose()
        {
            _beads.Dispose();
            SceneObjects.DestroyObjectOf(_plaque.Root);
        }

        /// <param name="beads">How many beads stand under the right label, and how far apart: the title keeps clear of both.</param>
        private void Say(string label, string title, string right, int beads, float gap)
        {
            Lettering.SayFromLeft(_label, label, _left, LabelUp);
            Lettering.SayUpToRight(_rightLabel, right, _right, RightLabelUp);
            float rightWidth = Math.Max(Lettering.WidthOf(_rightLabel, right), beads * BeadSide + (beads - 1) * gap);
            float room = _right - rightWidth - GapToTheRight - _left;
            Lettering.SayWithin(_title, title, PagePlaque.TitleSize, room);
            _title.transform.localPosition = new Vector3(_left + Lettering.WidthOf(_title, title) / 2f, -TitleDown, 0f);
        }

        /// <summary>A bead for each mark, the row ending at the plaque's right.</summary>
        private Mesh Beads(IReadOnlyList<MapMark> marks, float gap)
        {
            var sketch = new MeshSketch();
            float rowWidth = marks.Count * BeadSide + (marks.Count - 1) * gap;
            float bottom = -BeadsDown - BeadSide / 2f;
            var anywhere = new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero);
            for (int bead = 0; bead < marks.Count; bead++)
            {
                var face = new Rect(_right - rowWidth + bead * (BeadSide + gap), bottom, BeadSide, BeadSide);
                var edge = new Rect(face.x - BeadLine, face.y - BeadLine, face.width + 2f * BeadLine, face.height + 2f * BeadLine);
                switch (marks[bead])
                {
                    case MapMark.Restored:
                        sketch.AddRectangle(new Rect(edge.x, edge.y - BeadDrop, edge.width, edge.height), Shadow);
                        sketch.AddRectangle(edge, DarkLine);
                        sketch.AddRectangle(face, Gilt, GiltLight, anywhere);
                        break;
                    case MapMark.Here:
                        sketch.AddRectangle(new Rect(edge.x, edge.y - BeadDrop, edge.width, edge.height), Shadow);
                        sketch.AddRectangle(edge, RedLine);
                        sketch.AddRectangle(face, Red, RedLight, anywhere);
                        break;
                    case MapMark.Ahead:
                        sketch.AddRectangle(edge, SunkLine);
                        sketch.AddRectangle(face, SunkFace, SunkTop, anywhere);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(marks), marks[bead], "A mark the plaque has no bead for.");
                }
            }

            return sketch.ToMesh("Map beads");
        }
    }
}
