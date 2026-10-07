#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Hall
{
    /// <summary>
    /// The architecture of a room of the museum, as the design canvas draws it (HomeTR, MapTR): the cream cornice
    /// under the ceiling, the picture rail, and, where the room shows its floor, the skirting board and the wooden
    /// floor whose boards run away from the eye. All of it is plain paint in bands, built in code for the screen it
    /// is shown on, so it is as wide as any screen and its lines are sharp on every one.
    /// </summary>
    public sealed class RoomTrim : IDisposable
    {
        /// <summary>The cream of the mouldings from their top (0) to their foot (1), and the lines drawn on them.</summary>
        private static readonly ColourRamp Moulding = new ColourRamp((0f, Tint.Of(0xFFFDF4)), (0.6f, Tint.Of(0xF3E4C0)), (1f, Tint.Of(0xCDB280)));
        private static readonly Color MouldingGroove = Tint.Of(0xCDB280);
        private static readonly Color MouldingLight = Tint.Of(0xFFFFFF, 0.9f);
        private static readonly Color MouldingFoot = Tint.Of(0x8A6A3A);
        private static readonly Color SkirtingFoot = Tint.Of(0x6E4B1E);
        private const float Hairline = 1.2f;

        /// <summary>What a moulding shades of the wall under it, and how far; the cornice shades more than the rail.</summary>
        private static readonly Color Shade = Tint.Of(0x04242B, 0.42f);
        private const float ShadeUnderCornice = 26f;
        private const float ShadeUnderRail = 10f;
        private const float RailShadeStrength = 0.6f;

        /// <summary>The wall darkens towards the floor over this height above the skirting.</summary>
        private const float WallShadeAboveSkirting = 70f;
        private static readonly Color WallShade = Tint.Of(0x04242B, 0.4f);

        /// <summary>The floor's paint from the skirting (0) down to <see cref="FloorDepth"/> (1), its gloss and its shade along the skirting.</summary>
        private static readonly ColourRamp Boards = new ColourRamp((0f, Tint.Of(0xE9BC84)), (0.5f, Tint.Of(0xD7A468)), (1f, Tint.Of(0xB98148)));
        private const float FloorDepth = 212f;
        private static readonly Color Gloss = Tint.Of(0xFFF3DA, 0.5f);
        private const float GlossDepth = 52f;
        private const float ShadeOnFloor = 13f;

        /// <summary>
        /// The boards' seams fan out from a point far up the wall: they are this far apart along the skirting and
        /// this far apart <see cref="FloorDepth"/> below it. Every second board is a tone darker.
        /// </summary>
        private const float SeamsApartAtSkirting = 20.6f;
        private const float SeamsApartAtDepth = 53.5f;
        private static readonly Color Seam = Tint.Of(0x8A5524, 0.5f);
        private static readonly Color SeamLight = Tint.Of(0xFFE2B0, 0.35f);
        private static readonly Color DarkerBoard = Tint.Of(0x8A5524, 0.15f);
        private const float SeamWidth = 0.8f;

        /// <summary>A seam's light edge lies this far to its right at the foot of the screen (and one point at the skirting).</summary>
        private const float SeamLightBeside = 1.6f;

        private readonly PaintedShape _floor;
        private readonly PaintedShape _mouldings;

        private RoomTrim(Transform parent, ArtCatalogue art)
        {
            _floor = PaintedShape.Create("Floor", parent, art.PaintMaterial, DrawOrder.RoomFloor);
            _mouldings = PaintedShape.Create("Mouldings", parent, art.PaintMaterial, DrawOrder.RoomTrim);
        }

        public static RoomTrim Create(Transform parent, ArtCatalogue art) => new RoomTrim(parent, art);

        /// <summary>Draws the room for a page.</summary>
        /// <param name="floorLine">The height where the wall meets the floor; nothing for a room that shows no floor.</param>
        public void Show(PageLayout page, float? floorLine)
        {
            ScreenView view = page.View;
            float softEdge = 1f / view.PixelsPerPoint;
            var mouldings = new MeshSketch();
            AddShade(mouldings, view, page.CorniceBottom, ShadeUnderCornice, strength: 1f);
            AddMoulding(mouldings, view, view.Top, page.CorniceBottom, grooveDown: 0.62f, MouldingFoot);
            AddShade(mouldings, view, page.RailBottom, ShadeUnderRail, RailShadeStrength);
            AddMoulding(mouldings, view, page.RailTop, page.RailBottom, grooveDown: null, MouldingFoot);

            var floor = new MeshSketch();
            if (floorLine is float line)
            {
                float skirtingTop = line + HallLayout.SkirtingHeight;
                AddUprightBlend(mouldings, new Rect(view.Left, skirtingTop, view.Width, WallShadeAboveSkirting), WallShade, Clear(WallShade));
                AddMoulding(mouldings, view, skirtingTop, line, grooveDown: 0.3f, SkirtingFoot);
                AddFloor(floor, view, line, softEdge);
            }

            _mouldings.Show(mouldings.ToMesh("Mouldings"));
            _floor.Show(floor.ToMesh("Floor"));
        }

        public void Dispose()
        {
            _floor.Dispose();
            _mouldings.Dispose();
        }

        /// <summary>A moulding across the whole wall: cream that darkens towards its foot, a light line along its top, a groove, a dark line along its foot.</summary>
        /// <param name="grooveDown">How far down the moulding its groove lies, from 0 at its top to 1 at its foot; nothing for a moulding too narrow for one.</param>
        private static void AddMoulding(MeshSketch sketch, ScreenView view, float top, float foot, float? grooveDown, Color footLine)
        {
            sketch.AddNet(new Rect(view.Left, foot, view.Width, top - foot), columns: 1, rows: 6, point => Moulding.At((top - point.y) / (top - foot)));
            sketch.AddRectangle(new Rect(view.Left, top - Hairline, view.Width, Hairline), MouldingLight);
            if (grooveDown is float down)
            {
                sketch.AddRectangle(new Rect(view.Left, top - down * (top - foot) - Hairline / 2f, view.Width, Hairline), MouldingGroove);
            }

            sketch.AddRectangle(new Rect(view.Left, foot, view.Width, Hairline), footLine);
        }

        /// <summary>The shade of a moulding on the wall under it.</summary>
        private static void AddShade(MeshSketch sketch, ScreenView view, float under, float height, float strength)
        {
            Color shade = Shade;
            shade.a *= strength;
            AddUprightBlend(sketch, new Rect(view.Left, under - height, view.Width, height), Clear(shade), shade);
        }

        private static void AddFloor(MeshSketch sketch, ScreenView view, float floorLine, float softEdge)
        {
            float depth = floorLine - view.Bottom;
            var floor = new Rect(view.Left, view.Bottom, view.Width, depth);
            sketch.AddNet(floor, columns: 1, rows: 8, point => Boards.At((floorLine - point.y) / FloorDepth));

            // A seam's place along the skirting decides its slope: the fan is the same on every screen, wider screens
            // show more of it. Nothing of the floor is drawn past the screen's sides: a page that is turned slides
            // beside another page, and what stuck out of it would lie on that one.
            var fan = new SeamFan(view, floorLine);
            for (int seam = -fan.Outermost - 1; seam <= fan.Outermost; seam += 2)
            {
                AddBoard(sketch, fan, seam);
            }

            for (int seam = -fan.Outermost; seam <= fan.Outermost; seam++)
            {
                var line = new List<Vector2> { fan.StartOf(seam), fan.EndOf(seam) };
                Stroke.Add(sketch, line, isClosed: false, SeamWidth, softEdge, Seam);
                if (line[1].x + SeamLightBeside < view.Right)
                {
                    var light = new List<Vector2> { line[0] + Vector2.right, line[1] + SeamLightBeside * Vector2.right };
                    Stroke.Add(sketch, light, isClosed: false, SeamWidth, softEdge, SeamLight);
                }
            }

            AddUprightBlend(sketch, new Rect(view.Left, floorLine - GlossDepth, view.Width, GlossDepth), Clear(Gloss), Gloss);
            AddUprightBlend(sketch, new Rect(view.Left, floorLine - ShadeOnFloor, view.Width, ShadeOnFloor), Clear(Shade), Shade);
        }

        /// <summary>The darker tone of the board between a seam and the next one to its right, as far as the screen shows the board.</summary>
        private static void AddBoard(MeshSketch sketch, SeamFan fan, int leftSeam)
        {
            Vector2 leftEnd = fan.EndOf(leftSeam);
            Vector2 rightEnd = fan.EndOf(leftSeam + 1);
            var outline = new List<Vector2> { fan.StartOf(leftSeam), fan.StartOf(leftSeam + 1), rightEnd };
            if (fan.IsAtTheFoot(leftEnd) != fan.IsAtTheFoot(rightEnd))
            {
                // One seam ends at the screen's foot and the other at its side: the board has the screen's corner between them.
                outline.Add(fan.CornerBelow(fan.IsAtTheFoot(leftEnd) ? rightEnd : leftEnd));
            }

            outline.Add(leftEnd);
            int first = sketch.AddCorner(outline[0], DarkerBoard);
            int previous = sketch.AddCorner(outline[1], DarkerBoard);
            for (int corner = 2; corner < outline.Count; corner++)
            {
                int next = sketch.AddCorner(outline[corner], DarkerBoard);
                sketch.AddTriangle(first, previous, next);
                previous = next;
            }
        }

        /// <summary>
        /// The seams of the floor's boards on one screen: straight lines from the skirting towards the eye that fan
        /// out from a point far up the wall. Seam 0 runs down the middle of the screen; a seam ends at the foot of
        /// the screen, or at the screen's side where it runs out of the screen before that.
        /// </summary>
        private readonly struct SeamFan
        {
            private readonly ScreenView _view;
            private readonly float _floorLine;

            public SeamFan(ScreenView view, float floorLine)
            {
                _view = view;
                _floorLine = floorLine;
            }

            /// <summary>The number of the last seam at each side that begins inside the screen.</summary>
            public int Outermost => Mathf.FloorToInt(_view.Width / 2f / SeamsApartAtSkirting);

            /// <summary>Where a seam leaves the skirting; a seam that would begin past the screen's side begins at that side.</summary>
            public Vector2 StartOf(int seam) =>
                new Vector2(Mathf.Clamp(seam * SeamsApartAtSkirting, _view.Left, _view.Right), _floorLine);

            public Vector2 EndOf(int seam)
            {
                float atTheFoot = seam * ApartAt(_floorLine - _view.Bottom);
                if (Mathf.Abs(atTheFoot) <= _view.Width / 2f)
                {
                    return new Vector2(atTheFoot, _view.Bottom);
                }

                // The seams are as far apart as the screen's half width over this seam's number where it meets the side.
                float apartAtTheSide = _view.Width / 2f / Mathf.Abs(seam);
                float below = Mathf.Max(0f, (apartAtTheSide - SeamsApartAtSkirting) / (SeamsApartAtDepth - SeamsApartAtSkirting) * FloorDepth);
                return new Vector2(Mathf.Sign(atTheFoot) * _view.Width / 2f, _floorLine - below);
            }

            public bool IsAtTheFoot(Vector2 seamEnd) => Mathf.Approximately(seamEnd.y, _view.Bottom);

            /// <summary>The corner of the screen under a point of its side.</summary>
            public Vector2 CornerBelow(Vector2 onTheSide) => new Vector2(onTheSide.x, _view.Bottom);

            private static float ApartAt(float below) => Mathf.LerpUnclamped(SeamsApartAtSkirting, SeamsApartAtDepth, below / FloorDepth);
        }

        private static void AddUprightBlend(MeshSketch sketch, Rect area, Color lower, Color upper) =>
            sketch.AddRectangle(area, lower, upper, new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));

        private static Color Clear(Color colour) => new Color(colour.r, colour.g, colour.b, 0f);
    }
}
