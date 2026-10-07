#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The bar at the foot of the museum's pages, as the design canvas draws it (HomeTR, MapTR): a dark teal band
    /// under a gilt line, with the mark of every destination on it. The destination that is shown stands out: its
    /// mark is larger and rises over the bar's edge, on a raised teal plate, with its name under it. Sizes are in
    /// points from the middle of the bar's top edge at full size; a short screen draws the bar smaller
    /// (<see cref="PageLayout.BarScale"/>). The design's bar also leads to a shop, a collection and a calendar;
    /// the bar shows the destinations the game has (<see cref="Destinations.OnTheBar"/>).
    /// </summary>
    public sealed class NavigationBar : IDisposable
    {
        /// <summary>The band's paint from its top edge (0) to the height of the bar at full size (1), and below.</summary>
        private static readonly ColourRamp Band = new ColourRamp((0f, Tint.Of(0x1C7C85)), (0.34f, Tint.Of(0x136069)), (1f, Tint.Of(0x0C4650)));

        /// <summary>The gilt line along the top edge, between two dark hairlines, and the bar's shadow on what lies above it.</summary>
        private const float GiltLineHeight = 5f;
        private const float Hairline = 1.4f;
        private static readonly Color GiltTop = Tint.Of(0xFFF4C4);
        private static readonly Color GiltMiddle = Tint.Of(0xF6B93C);
        private static readonly Color GiltFoot = Tint.Of(0xC98A1E);
        private static readonly Color GiltEdge = Tint.Of(0x5E3305);
        private const float ShadowHeight = 14f;
        private static readonly Color Shadow = Tint.Of(0x1E0E02, 0.3f);

        /// <summary>A mark is drawn in a box of 100 points; on the bar it is this large, and larger where its destination is shown.</summary>
        private const float MarkBox = 100f;
        private const float MarkSize = 46f;
        private const float ChosenMarkSize = 58f;

        /// <summary>How far under the bar's top edge the middle of each thing lies (less than nothing: above the edge).</summary>
        private const float MarkDown = 40f;
        private const float ChosenMarkDown = 4f;
        private const float PlateDown = 32f;
        private const float NameDown = 47.5f;
        private const float NameSize = 14.5f;
        private const float NameSpacing = 3f;

        /// <summary>
        /// The chosen destination's name is no wider than the plate it stands on, less the plate's rim and a
        /// letter's width of teal at each side of it: a longer name ("Koleksiyon") is set smaller.
        /// </summary>
        private const float NameRoom = 54f;

        private readonly Transform _root;
        private readonly PaintedShape _band;
        private readonly SpriteRenderer _plate;
        private readonly TextMeshPro _name;
        private readonly Dictionary<Destination, Tab> _tabs = new Dictionary<Destination, Tab>();
        private Destination _chosen;
        private PageLayout? _page;

        private NavigationBar(Transform parent, ArtCatalogue art, Destination chosen)
        {
            _chosen = chosen;
            _root = new GameObject("Navigation bar").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _band = PaintedShape.Create("Band", _root, art.PaintMaterial, DrawOrder.Bar);
            _plate = SceneObjects.AddSprite("Chosen plate", _root, art.Of(GalleryArt.Tab), art.SpriteMaterial, DrawOrder.BarTab);
            _name = Lettering.Write("Chosen name", _root, art, LetteringStyle.OnTeal, NameSize, Color.white, DrawOrder.BarLettering);
            _name.characterSpacing = NameSpacing;
            foreach (Destination destination in Destinations.OnTheBar)
            {
                SpriteRenderer mark = SceneObjects.AddSprite($"{destination} mark", _root, art.Of(Destinations.MarkOf(destination)), art.SpriteMaterial, DrawOrder.BarIcon);
                _tabs[destination] = new Tab(this, destination, mark.transform);
            }
        }

        /// <summary>Builds the bar with one destination shown as the one the player is at. Call <see cref="Place"/> before the first picture.</summary>
        public static NavigationBar Create(Transform parent, ArtCatalogue art, Destination chosen) => new NavigationBar(parent, art, chosen);

        /// <summary>Lays the bar along the foot of a page; call again whenever the screen changes.</summary>
        public void Place(PageLayout page)
        {
            _page = page;
            float scale = page.BarScale;
            _root.localPosition = new Vector3(0f, page.BarTop, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);

            // From here on in the bar's own points.
            float left = page.View.Left / scale;
            float width = page.View.Width / scale;
            float foot = (page.View.Bottom - page.BarTop) / scale;
            _band.Show(BuildBand(left, width, foot));
            ShowTheChosen();
        }

        /// <summary>Marks another destination as the one the player is at.</summary>
        public void Choose(Destination destination)
        {
            _chosen = destination;
            ShowTheChosen();
        }

        /// <summary>Tells a set of pressable controls about the bar's places: pressing one asks for its page.</summary>
        public void AddPressables(PressSet<MuseumPress> presses)
        {
            foreach (Destination destination in Destinations.OnTheBar)
            {
                presses.Add(_tabs[destination], new MuseumPress.Visit(destination));
            }
        }

        /// <summary>The middle of a destination's place on the bar, in the layout's points: where a finger presses it.</summary>
        /// <exception cref="InvalidOperationException">The bar was not placed on a screen yet.</exception>
        public Vector2 MiddleOf(Destination destination)
        {
            Box place = PlaceOf(destination);
            return new Vector2(place.MiddleX, place.MiddleY);
        }

        public void Dispose()
        {
            _band.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>A destination's place on the bar, in the layout's points: from the screen's foot up to the bar's top edge.</summary>
        private Box PlaceOf(Destination destination)
        {
            if (_page is not PageLayout page)
            {
                throw new InvalidOperationException("The navigation bar has no screen yet; call Place first.");
            }

            int count = Destinations.OnTheBar.Count;
            float middle = page.TabMiddle(Destinations.PlaceOf(destination), count);
            float width = page.TabWidth(count);
            return new Box(middle - width / 2f, page.View.Bottom, width, page.BarTop - page.View.Bottom);
        }

        private void ShowTheChosen()
        {
            if (_page is not PageLayout page)
            {
                return; // nothing is placed yet; Place shows the chosen destination
            }

            int count = Destinations.OnTheBar.Count;
            foreach (Destination destination in Destinations.OnTheBar)
            {
                float middle = page.TabMiddle(Destinations.PlaceOf(destination), count) / page.BarScale;
                bool isChosen = destination == _chosen;
                _tabs[destination].Show(middle, isChosen ? ChosenMarkDown : MarkDown, (isChosen ? ChosenMarkSize : MarkSize) / MarkBox);
                if (isChosen)
                {
                    _plate.transform.localPosition = new Vector3(middle, -PlateDown, 0f);
                    _name.transform.localPosition = new Vector3(middle, -NameDown, 0f);
                    Lettering.SayWithin(_name, Destinations.NameOf(destination), NameSize, NameRoom);
                }
            }
        }

        /// <summary>The band from its top edge down to the screen's foot, its gilt line, and its shadow above it; in the bar's own points.</summary>
        private static Mesh BuildBand(float left, float width, float foot)
        {
            var sketch = new MeshSketch();
            sketch.AddRectangle(new Rect(left, 0f, width, ShadowHeight), Shadow, new Color(Shadow.r, Shadow.g, Shadow.b, 0f), new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));
            sketch.AddNet(new Rect(left, foot, width, -foot), columns: 1, rows: 8, point => Band.At(-point.y / PageLayout.BarHeight));
            sketch.AddRectangle(new Rect(left, -GiltLineHeight - Hairline, width, GiltLineHeight + 2f * Hairline), GiltEdge);
            float giltMiddle = -0.55f * GiltLineHeight;
            AddUprightBlend(sketch, new Rect(left, giltMiddle, width, -giltMiddle), GiltMiddle, GiltTop);
            AddUprightBlend(sketch, new Rect(left, -GiltLineHeight, width, GiltLineHeight + giltMiddle), GiltFoot, GiltMiddle);
            return sketch.ToMesh("Navigation band");
        }

        private static void AddUprightBlend(MeshSketch sketch, Rect area, Color lower, Color upper) =>
            sketch.AddRectangle(area, lower, upper, new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));

        /// <summary>One destination's place on the bar: its mark, and the stretch of the bar a finger may press for it.</summary>
        private sealed class Tab : IPressable
        {
            /// <summary>A mark that is held down loses a little of its light.</summary>
            private static readonly Color PressedShade = new Color(0.8f, 0.8f, 0.8f, 1f);

            private readonly NavigationBar _bar;
            private readonly Destination _destination;
            private readonly Transform _mark;
            private readonly SpriteRenderer _markPicture;

            public Tab(NavigationBar bar, Destination destination, Transform mark)
            {
                _bar = bar;
                _destination = destination;
                _mark = mark;
                _markPicture = mark.GetComponent<SpriteRenderer>();
            }

            /// <summary>Puts the mark at a place along the bar, in the bar's own points, at a share of its drawing's size.</summary>
            public void Show(float middle, float down, float scale)
            {
                _mark.localPosition = new Vector3(middle, -down, 0f);
                _mark.localScale = new Vector3(scale, scale, 1f);
            }

            public bool IsUnder(Vector2 point) => _bar.PlaceOf(_destination).Contains(point.x, point.y);

            public void ShowPressed(bool isPressed) => _markPicture.color = isPressed ? PressedShade : Color.white;
        }
    }
}
