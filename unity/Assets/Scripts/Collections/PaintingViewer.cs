#nullable enable
using System;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Collections
{
    /// <summary>
    /// A restored painting of the collection looked at by itself: the room goes dark behind it, and the painting
    /// comes forward from its place on the wall to hang as large as the screen allows, in the hall's own frame,
    /// with its brass plate. A touch anywhere puts it back. The viewer has no clock of its own: whoever shows it
    /// says how far it has come forward.
    /// </summary>
    public sealed class PaintingViewer : IPressable, IDisposable
    {
        private const float LineSize = 12f;

        private readonly Transform _root;
        private readonly PaintingLook _look;
        private readonly Veil _dark;
        private readonly FramedPainting _painting;
        private readonly TextMeshPro _line;

        /// <summary>Where the painting hangs on the wall, which it comes forward from; and where it hangs when it is forward.</summary>
        private Box _onTheWall;
        private ViewerLayout? _layout;
        private float _forward;

        private PaintingViewer(Transform parent, ArtCatalogue art, PaintingLook look, Box onTheWall)
        {
            _look = look;
            _onTheWall = onTheWall;
            _root = new GameObject("Painting viewer").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _dark = Veil.Create(_root, art);

            int raisedBy = (int)DrawOrder.OverTheDark;
            _painting = FramedPainting.Create(_root, art, look, raisedBy);
            _painting.Show(Enumerable.Range(0, look.Number.Size.PieceCount).ToArray());
            _line = Lettering.Write("How to close", _painting.Root, art, LetteringStyle.Label, LineSize, Lettering.OnDark, DrawOrder.CaptionLettering + raisedBy);
            Lettering.Say(_line, MuseumWords.TouchToClose);
        }

        /// <param name="look">The painting; the viewer takes it over, and disposes of it with itself.</param>
        /// <param name="onTheWall">The painting's picture as it hangs on the collection's wall, in the page's points.</param>
        public static PaintingViewer Create(Transform parent, ArtCatalogue art, PaintingLook look, Box onTheWall) =>
            new PaintingViewer(parent, art, look, onTheWall);

        /// <summary>Lays the viewer out for a screen; call again whenever the screen changes.</summary>
        /// <param name="onTheWall">Where the painting hangs on the wall of that screen.</param>
        public void Fit(ScreenShape screen, Box onTheWall)
        {
            _onTheWall = onTheWall;
            ViewerLayout layout = ViewerLayout.Of(screen, _look.Aspect);
            _layout = layout;
            _dark.Fit(layout.View);
            _painting.Place(layout.Hanging, layout.View);
            _line.transform.localPosition = new Vector3(layout.Hanging.Frame.MiddleX, layout.LineMiddle, 0f);
            ComeForward(_forward);
        }

        /// <summary>How far the painting has come forward: 0 hangs on the wall in the lit room, 1 hangs large in front of the dark.</summary>
        public void ComeForward(float progress)
        {
            _forward = Mathf.Clamp01(progress);
            _dark.Darken(_forward);
            if (_layout is not ViewerLayout layout)
            {
                return;
            }

            // The painting is drawn at the share of its size it has on the wall, around the place it has there, and grows from that.
            Box large = layout.Hanging.Picture;
            float scale = Mathf.Lerp(_onTheWall.Width / large.Width, 1f, _forward);
            var middleOnTheWall = new Vector2(_onTheWall.MiddleX, _onTheWall.MiddleY);
            var middleWhenLarge = new Vector2(large.MiddleX, large.MiddleY);
            Vector2 middle = Vector2.Lerp(middleOnTheWall, middleWhenLarge, _forward);
            Vector2 moved = middle - middleWhenLarge * scale;
            _painting.Root.localPosition = new Vector3(moved.x, moved.y, 0f);
            _painting.Root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>The whole screen puts the painting back.</summary>
        public bool IsUnder(Vector2 point) => true;

        public void ShowPressed(bool isPressed)
        {
            // Nothing of the viewer is a button that goes down: any touch that lifts closes it.
        }

        public void Dispose()
        {
            _dark.Dispose();
            _painting.Dispose();
            _look.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }
    }
}
