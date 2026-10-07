#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The gallery wall behind everything, as the web test page paints it (gallery.css, body::before): teal paint that
    /// darkens towards the floor, a damask printed on it, a pool of warm light in the middle and darker corners. It
    /// fills the whole screen, so it is built again for every screen it is fitted to.
    /// </summary>
    public sealed class Wall : IDisposable
    {
        /// <summary>The damask's motif is 48 x 72 points; its picture holds six across and four down, a square of 288 points.</summary>
        private const float MotifWidth = 48f;
        private const float DamaskPictureSide = 288f;

        /// <summary>The light and the dark corners change slowly: a net of this many faces follows them closely enough.</summary>
        private const int LightColumns = 20;
        private const int LightRows = 32;

        /// <summary>The paint, from the top of the wall (0) to its foot (1).</summary>
        private static readonly ColourRamp Paint = new ColourRamp((0f, Tint.Of(0x1F858C)), (0.55f, Tint.Of(0x17666F)), (1f, Tint.Of(0x11505A)));

        /// <summary>The pool of light, from its middle (0) to the edge of its ellipse (1).</summary>
        private static readonly ColourRamp Light = new ColourRamp(
            (0f, Tint.Of(0xFFF1CC, 0.3f)), (0.55f, Tint.Of(0xFFF1CC, 0.08f)), (1f, Tint.Of(0xFFF1CC, 0f)));

        /// <summary>The light's ellipse: 62 % of the screen wide and high at most, never wider than 300 points; its middle is a little above the screen's.</summary>
        private const float LightRadius = 0.62f;
        private const float WidestLightRadius = 300f;
        private const float LightMiddleDown = 0.46f;

        /// <summary>The dark of the corners, from the middle of its ellipse (0) to its edge (1) and beyond.</summary>
        private static readonly ColourRamp Corners = new ColourRamp((0.55f, Tint.Of(0x04242B, 0f)), (1f, Tint.Of(0x04242B, 0.62f)));
        private const float CornersRadius = 0.75f;
        private const float CornersMiddleDown = 0.48f;

        private readonly Transform _root;
        private readonly PaintedShape _paint;
        private readonly PaintedShape _damask;
        private readonly PaintedShape _light;

        private Wall(Transform parent, ArtCatalogue art)
        {
            _root = new GameObject("Wall").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _paint = PaintedShape.Create("Paint", _root, art.PaintMaterial, DrawOrder.WallPaint);
            _damask = PaintedShape.Create("Damask", _root, art.PaintMaterial, DrawOrder.WallDamask, art.Damask);
            _light = PaintedShape.Create("Light", _root, art.PaintMaterial, DrawOrder.WallLight);
        }

        public static Wall Create(Transform parent, ArtCatalogue art) => new Wall(parent, art);

        /// <summary>Paints the wall for a screen of the layout's size.</summary>
        public void Fit(ScreenLayout layout)
        {
            var screen = new Rect(-layout.ViewWidth / 2f, -layout.ViewHeight / 2f, layout.ViewWidth, layout.ViewHeight);
            _paint.Show(BuildPaint(screen));
            _damask.Show(BuildDamask(screen));
            _light.Show(BuildLight(screen));
        }

        public void Dispose()
        {
            _paint.Dispose();
            _damask.Dispose();
            _light.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>How far down the screen a height is: 0 at the top edge, 1 at the foot.</summary>
        private static float Down(Rect screen, float y) => (screen.yMax - y) / screen.height;

        private static Mesh BuildPaint(Rect screen) => BuildNet(screen, columns: 1, rows: 20, point => Paint.At(Down(screen, point.y)), "Wall paint");

        /// <summary>One face with the damask repeated over it: a motif stands in the middle of the screen's width, and one begins at its top.</summary>
        private static Mesh BuildDamask(Rect screen)
        {
            float left = (screen.xMin + MotifWidth / 2f) / DamaskPictureSide;
            float top = 1f;
            var inPicture = new Rect(left, top - screen.height / DamaskPictureSide, screen.width / DamaskPictureSide, screen.height / DamaskPictureSide);
            var sketch = new MeshSketch();
            sketch.AddRectangle(screen, Color.white, Color.white, inPicture);
            return sketch.ToMesh("Wall damask");
        }

        private static Mesh BuildLight(Rect screen)
        {
            var lightRadii = new Vector2(Mathf.Min(LightRadius * screen.width, WidestLightRadius), LightRadius * screen.height);
            var cornerRadii = new Vector2(CornersRadius * screen.width, CornersRadius * screen.height);
            var lightMiddle = new Vector2(screen.center.x, screen.yMax - LightMiddleDown * screen.height);
            var cornersMiddle = new Vector2(screen.center.x, screen.yMax - CornersMiddleDown * screen.height);

            Color LightAndCornersAt(Vector2 point) => Tint.Over(
                Corners.At(FromMiddle(point, cornersMiddle, cornerRadii)),
                Light.At(FromMiddle(point, lightMiddle, lightRadii)));

            return BuildNet(screen, LightColumns, LightRows, LightAndCornersAt, "Wall light");
        }

        /// <summary>How far a point is from the middle of an ellipse: 0 in the middle, 1 on its edge.</summary>
        private static float FromMiddle(Vector2 point, Vector2 middle, Vector2 radii) =>
            new Vector2((point.x - middle.x) / radii.x, (point.y - middle.y) / radii.y).magnitude;

        private static Mesh BuildNet(Rect screen, int columns, int rows, Func<Vector2, Color> colourAt, string name)
        {
            var sketch = new MeshSketch();
            sketch.AddNet(screen, columns, rows, colourAt);
            return sketch.ToMesh(name);
        }
    }
}
