#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The dark that falls over the room when a game is decided, as the web test page lays it under its result panel
    /// (gallery.css, .banner::before): the whole screen, plaques and shelf included, a little less dark in the middle
    /// where the card hangs. The next board comes out of the same dark. It covers the whole screen, so it is painted
    /// again for every screen it is fitted to and every strength it is shown at.
    /// </summary>
    public sealed class Veil : IDisposable
    {
        /// <summary>The dark changes slowly over the screen: a net of this many faces follows it closely enough.</summary>
        private const int Columns = 12;
        private const int Rows = 16;

        /// <summary>The ellipse of the lighter middle: 85 % of the screen wide and 48 % high, around the screen's middle.</summary>
        private const float MiddleRadiusAcross = 0.85f;
        private const float MiddleRadiusUpright = 0.48f;

        /// <summary>The dark at full strength, from the middle of its ellipse (0) to its edge (1) and beyond.</summary>
        private static readonly ColourRamp Dark = new ColourRamp((0f, Tint.Of(0x04242B, 0.6f)), (1f, Tint.Of(0x031A20, 0.84f)));

        private readonly PaintedShape _shape;
        private Rect _screen;
        private float _strength;

        private Veil(Transform parent, ArtCatalogue art)
        {
            _shape = PaintedShape.Create("Veil", parent, art.PaintMaterial, DrawOrder.Veil);
        }

        public static Veil Create(Transform parent, ArtCatalogue art) => new Veil(parent, art);

        /// <summary>Spreads the veil over a screen, at the strength it has.</summary>
        public void Fit(ScreenView view)
        {
            _screen = new Rect(view.Left, view.Bottom, view.Width, view.Height);
            Paint();
        }

        /// <summary>How dark the room is: 0 is the lit room, 1 the full dark behind a result's card.</summary>
        public void Darken(float strength)
        {
            _strength = Mathf.Clamp01(strength);
            Paint();
        }

        public void Dispose() => _shape.Dispose();

        private void Paint()
        {
            var sketch = new MeshSketch();
            if (_strength > 0f && _screen.width > 0f)
            {
                var radii = new Vector2(MiddleRadiusAcross * _screen.width, MiddleRadiusUpright * _screen.height);
                sketch.AddNet(_screen, Columns, Rows, point => DarkAt(point, radii));
            }

            _shape.Show(sketch.ToMesh("Veil"));
        }

        private Color DarkAt(Vector2 point, Vector2 radii)
        {
            float fromMiddle = new Vector2((point.x - _screen.center.x) / radii.x, (point.y - _screen.center.y) / radii.y).magnitude;
            Color dark = Dark.At(fromMiddle);
            dark.a *= _strength;
            return dark;
        }
    }
}
