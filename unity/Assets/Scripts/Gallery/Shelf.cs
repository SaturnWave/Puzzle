#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The wooden shelf under the board, with a line of lettering on it; the line keeps clear of the round button
    /// that stands on the shelf's left end (the stage's own, <see cref="ScreenLayout.LeaveButton"/>). Sizes are in
    /// points around the middle of the plank at its full size; a screen on which the board needs the height draws the shelf smaller
    /// (<see cref="ScreenLayout.ShelfScale"/>), down to a ledge half as high.
    /// </summary>
    public sealed class Shelf : IDisposable
    {
        /// <summary>The shelf's picture is its plank and the room around it for its edge and shadow: 28 points wider, 96 high.</summary>
        private const float PictureMargin = 28f;
        private const float PictureHeight = 96f;

        private const float LineSize = 13f;

        /// <summary>
        /// The line is not set smaller than this on the screen, however small the shelf is drawn: on the ledge of
        /// a compact screen it would be half its size, too small to read.
        /// </summary>
        private const float SmallestLineOnScreen = 9f;

        /// <summary>The line keeps this far from each end of the plank, where the plank is rounded.</summary>
        private const float LineInFromEnds = 20f;

        /// <summary>The plank's foot is a dark lip, so what is written on it stands a little above its middle.</summary>
        private const float LineAboveMiddle = 1.5f;

        private readonly Transform _root;
        private readonly SpriteRenderer _plank;
        private readonly TextMeshPro _line;
        private string _words = string.Empty;

        /// <summary>The line stands this far to the right of the round button on the shelf's left end.</summary>
        private const float LineAfterButton = 8f;

        /// <summary>The plank's width in the shelf's own points, as the screen last allowed it; the line is fitted to it.</summary>
        private float _plankWidth = ScreenLayout.WidestShelf;

        /// <summary>Where the line begins, from the plank's left end, in the shelf's own points: after the round button.</summary>
        private float _lineBeginsIn = LineInFromEnds;

        /// <summary>The line's type size in the shelf's own points, for the size the shelf was last drawn at.</summary>
        private float _lineSize = LineSize;

        private Shelf(Transform parent, ArtCatalogue art)
        {
            _root = new GameObject("Shelf").transform;
            _root.SetParent(parent, worldPositionStays: false);

            _plank = new GameObject("Plank").AddComponent<SpriteRenderer>();
            _plank.transform.SetParent(_root, worldPositionStays: false);
            _plank.sprite = art.Of(GalleryArt.Shelf);
            _plank.sharedMaterial = art.SpriteMaterial;
            _plank.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
            _plank.sortingOrder = (int)DrawOrder.Shelf;

            _line = Lettering.Write("Line", _root, art, LetteringStyle.OnWood, LineSize, Lettering.OnDark, DrawOrder.ShelfLettering);
            _line.transform.localPosition = new Vector3(0f, LineAboveMiddle, 0f);
        }

        public static Shelf Create(Transform parent, ArtCatalogue art) => new Shelf(parent, art);

        /// <summary>Puts the shelf where the layout has it: centred on the screen, as wide and as large as the screen allows.</summary>
        public void Place(ScreenLayout layout)
        {
            float scale = layout.ShelfScale;
            _root.localPosition = new Vector3(0f, layout.ShelfMiddle, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);

            // From here on in the shelf's own points.
            _plankWidth = layout.ShelfWidth / scale;
            _lineBeginsIn = (layout.LeaveButton.Right + layout.ShelfWidth / 2f) / scale + LineAfterButton;
            _lineSize = Mathf.Max(LineSize, SmallestLineOnScreen / scale);
            _plank.size = new Vector2(_plankWidth + PictureMargin, PictureHeight);
            WriteLine();
        }

        /// <summary>Changes what the shelf says.</summary>
        public void Say(string words)
        {
            _words = words;
            WriteLine();
        }

        /// <summary>The line is set at its size for this shelf, or smaller on a plank too short for it.</summary>
        private void WriteLine()
        {
            float left = -_plankWidth / 2f + _lineBeginsIn;
            float right = _plankWidth / 2f - LineInFromEnds;
            _line.transform.localPosition = new Vector3((left + right) / 2f, LineAboveMiddle, 0f);
            Lettering.SayWithin(_line, _words, _lineSize, room: right - left);
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_root);
    }
}
