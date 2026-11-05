#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Stage;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The tutorial's soft spotlight: the screen is dimmed with the room's dark teal, and each place a step points at
    /// is cut out of the dark as a rounded window whose edge glows with the frame's gilt and fades into the dark, as a
    /// lamp's pool of light would. Everything under it is seen dimmed; what lies in a window is seen as it is. The dark
    /// is painted as a net of faces whose knots take their darkness from the nearest window, so it is painted again
    /// only when the windows or the screen change; it comes and goes by its strength alone.
    /// </summary>
    public sealed class Spotlight : IDisposable
    {
        /// <summary>The net's faces are about this many points across: fine enough that a window's soft edge reads as a curve.</summary>
        private const float FacePoints = 5f;

        /// <summary>A window's corners are rounded by this much unless told otherwise, and never by more than half its shorter side.</summary>
        public const float Rounding = 16f;

        /// <summary>The gilt glows this far inside a window's edge, and fades into the dark this far outside it.</summary>
        private const float GlowInside = 6f;
        private const float FadeOutside = 14f;

        /// <summary>The dark: the room's teal at the veil's strength in the middle of the screen, so what lies under it stays faintly seen.</summary>
        private static readonly Color Dark = Tint.Of(0x04242B, 0.66f);

        /// <summary>The gilt at a window's edge: the frame's warm gold, half seen.</summary>
        private static readonly Color Gilt = new Color(1f, 0.82f, 0.38f, 0.5f);

        private readonly PaintedShape _shape;

        /// <summary>The places cut out of the dark, each with how much its corners are rounded.</summary>
        private readonly List<(Box Window, float Rounding)> _windows = new List<(Box Window, float Rounding)>();
        private Rect _screen;

        private Spotlight(Transform parent, ArtCatalogue art, DrawOrder order)
        {
            _shape = PaintedShape.Create("Spotlight", parent, art.PaintMaterial, order);
            Show(0f);
        }

        /// <summary>The spotlight over a stage: under its cards, over everything else.</summary>
        public static Spotlight Create(Transform parent, ArtCatalogue art) => new Spotlight(parent, art, DrawOrder.LessonDim);

        /// <summary>The spotlight over a card that hangs in front of the room's dark (the card before a level): over everything of the card.</summary>
        public static Spotlight CreateOverACard(Transform parent, ArtCatalogue art) => new Spotlight(parent, art, DrawOrder.CardLessonDim);

        /// <summary>Spreads the dark over a screen, with the windows it has.</summary>
        public void Fit(ScreenView view)
        {
            _screen = new Rect(view.Left, view.Bottom, view.Width, view.Height);
            Paint();
        }

        /// <summary>Cuts these places out of the dark, in the layout's points; none dims the whole screen.</summary>
        /// <param name="rounding">How much the windows' corners are rounded, in points: a square window rounded by half its side is a round one.</param>
        public void LightUp(IReadOnlyList<Box> windows, float rounding = Rounding)
        {
            _windows.Clear();
            foreach (Box window in windows)
            {
                _windows.Add((window, rounding));
            }

            Paint();
        }

        /// <summary>Cuts these places out of the dark, each rounded by its own amount (a round window over a disc beside a rounded one over the board).</summary>
        public void LightUp(IReadOnlyList<(Box Window, float Rounding)> windows)
        {
            _windows.Clear();
            _windows.AddRange(windows);
            Paint();
        }

        /// <summary>How strongly the spotlight is seen: 0 not at all, 1 fully.</summary>
        public void Show(float strength)
        {
            _shape.Transform.gameObject.SetActive(strength > 0f);
            _shape.Fade(strength);
        }

        public void Dispose() => _shape.Dispose();

        private void Paint()
        {
            var sketch = new MeshSketch();
            if (_screen.width > 0f)
            {
                int columns = Mathf.Max(1, Mathf.CeilToInt(_screen.width / FacePoints));
                int rows = Mathf.Max(1, Mathf.CeilToInt(_screen.height / FacePoints));
                sketch.AddNet(_screen, columns, rows, ColourAt);
            }

            _shape.Show(sketch.ToMesh("Spotlight"));
        }

        /// <summary>The dark at a point, by how far it lies outside the nearest window (inside counts below 0).</summary>
        private Color ColourAt(Vector2 point)
        {
            float outside = float.MaxValue;
            foreach ((Box window, float rounding) in _windows)
            {
                outside = Mathf.Min(outside, DistanceOutside(window, point, rounding));
            }

            if (outside <= -GlowInside)
            {
                return Color.clear;
            }

            if (outside <= 0f)
            {
                float glow = Easing.In(1f - outside / -GlowInside);
                return new Color(Gilt.r, Gilt.g, Gilt.b, Gilt.a * glow);
            }

            float dark = Easing.InOut(Mathf.Clamp01(outside / FadeOutside));
            return Color.Lerp(Gilt, Dark, dark);
        }

        /// <summary>How far a point lies outside a window with rounded corners: below 0 inside it, 0 on its edge.</summary>
        private static float DistanceOutside(Box window, Vector2 point, float roundedBy)
        {
            float rounding = Mathf.Min(roundedBy, Mathf.Min(window.Width, window.Height) / 2f);
            float beyondX = Mathf.Abs(point.x - window.MiddleX) - (window.Width / 2f - rounding);
            float beyondY = Mathf.Abs(point.y - window.MiddleY) - (window.Height / 2f - rounding);
            float outsideTheCorner = new Vector2(Mathf.Max(beyondX, 0f), Mathf.Max(beyondY, 0f)).magnitude;
            return outsideTheCorner + Mathf.Min(Mathf.Max(beyondX, beyondY), 0f) - rounding;
        }
    }
}
