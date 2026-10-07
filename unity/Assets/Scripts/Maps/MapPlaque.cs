#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Museum;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// The plaque over the museum's map, as the design canvas draws it (MapTR): what the museum holds in all, the
    /// page's title, which room the player stands in, and a bead for every room, gilt up to the player's.
    /// </summary>
    public sealed class MapPlaque : IDisposable
    {
        /// <summary>The two lines at the left stand this far over and under the plaque's middle; so do the label and the beads at the right.</summary>
        private const float SummaryUp = 9.5f;
        private const float TitleDown = 6f;
        private const float RoomLabelUp = 6.5f;
        private const float BeadsDown = 8f;

        /// <summary>Lettering begins a little further in than the plaque's padding, as the design has it for a plaque without a badge.</summary>
        private const float LinesBeginIn = 17f;

        /// <summary>A bead: a small square of gilt in a dark line, over its drop; an unlit one is sunk cream.</summary>
        private const float BeadSide = 6f;
        private const float BeadGap = 2.6f;
        private const float BeadLine = 1.4f;
        private const float BeadDrop = 2.5f;
        private static readonly Color BeadLight = Tint.Of(0xFFF4C4);
        private static readonly Color BeadGilt = Tint.Of(0xF6B93C);
        private static readonly Color BeadEdge = Tint.Of(0x5E3305);
        private static readonly Color BeadShadow = Tint.Of(0x5C3810, 0.3f);
        private static readonly Color UnlitFace = Tint.Of(0xE3CC9C);
        private static readonly Color UnlitFaceTop = Tint.Of(0xC4A777);
        private static readonly Color UnlitEdge = Tint.Of(0x5E3305, 0.6f);

        private readonly PagePlaque _plaque;
        private readonly PaintedShape _beads;

        private MapPlaque(Transform parent, ArtCatalogue art, RoomNumber here)
        {
            _plaque = new PagePlaque("Map plaque", parent, art);
            Transform content = _plaque.Content;
            float left = -PagePlaque.DesignWidth / 2f + LinesBeginIn;
            float right = PagePlaque.DesignWidth / 2f - PagePlaque.SidePadding - 1f;

            TextMeshPro summary = PagePlaque.WriteLabel("Summary", content, art, DrawOrder.PlaqueLettering);
            Lettering.SayFromLeft(summary, MuseumWords.MapSummary, left, SummaryUp);
            TextMeshPro title = Lettering.Write("Title", content, art, LetteringStyle.Label, PagePlaque.TitleSize, Lettering.Ink, DrawOrder.PlaqueLettering);
            Lettering.SayFromLeft(title, MuseumWords.MapTitle, left, -TitleDown);

            TextMeshPro whichRoom = PagePlaque.WriteLabel("Which room", content, art, DrawOrder.PlaqueLettering);
            Lettering.SayUpToRight(whichRoom, MuseumWords.RoomOfRooms(here), right, RoomLabelUp);
            _beads = PaintedShape.Create("Beads", content, art.PaintMaterial, DrawOrder.PlaqueArt);
            _beads.Show(Beads(here, right));
        }

        /// <param name="here">The room the player stands in.</param>
        public static MapPlaque Create(Transform parent, ArtCatalogue art, RoomNumber here) => new MapPlaque(parent, art, here);

        public void Place(PageLayout page) => _plaque.Place(page.Plaque);

        public void Dispose()
        {
            _beads.Dispose();
            SceneObjects.DestroyObjectOf(_plaque.Root);
        }

        /// <summary>A bead for every room of the museum, the row ending at a place; the rooms up to the player's are gilt.</summary>
        private static Mesh Beads(RoomNumber here, float right)
        {
            var sketch = new MeshSketch();
            float rowWidth = MuseumPlan.RoomCount * BeadSide + (MuseumPlan.RoomCount - 1) * BeadGap;
            float bottom = -BeadsDown - BeadSide / 2f;
            foreach (RoomNumber room in RoomNumber.All)
            {
                var face = new Rect(right - rowWidth + (room.Value - 1) * (BeadSide + BeadGap), bottom, BeadSide, BeadSide);
                var edge = new Rect(face.x - BeadLine, face.y - BeadLine, face.width + 2f * BeadLine, face.height + 2f * BeadLine);
                if (room.Value <= here.Value)
                {
                    sketch.AddRectangle(new Rect(edge.x, edge.y - BeadDrop, edge.width, edge.height), BeadShadow);
                    sketch.AddRectangle(edge, BeadEdge);
                    sketch.AddRectangle(face, BeadGilt, BeadLight, new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));
                }
                else
                {
                    sketch.AddRectangle(edge, UnlitEdge);
                    sketch.AddRectangle(face, UnlitFace, UnlitFaceTop, new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));
                }
            }

            return sketch.ToMesh("Room beads");
        }
    }
}
