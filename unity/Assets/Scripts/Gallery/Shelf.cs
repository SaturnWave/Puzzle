#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The wooden shelf under the board, with a line of lettering on it. Sizes are in points around the middle of
    /// the plank at its full size; a short screen draws the shelf smaller, by the layout's chrome scale.
    /// </summary>
    public sealed class Shelf : IDisposable
    {
        /// <summary>The shelf's picture is its plank and the room around it for its edge and shadow: 28 points wider, 96 high.</summary>
        private const float PictureMargin = 28f;
        private const float PictureHeight = 96f;

        private const float LineSize = 13f;

        /// <summary>The line keeps this far from each end of the plank, where the plank is rounded.</summary>
        private const float LineInFromEnds = 20f;

        /// <summary>The plank's foot is a dark lip, so what is written on it stands a little above its middle.</summary>
        private const float LineAboveMiddle = 1.5f;

        private readonly Transform _root;
        private readonly SpriteRenderer _plank;
        private readonly TextMeshPro _line;
        private string _words = string.Empty;

        /// <summary>The plank's width in the shelf's own points, as the screen last allowed it; the line is fitted to it.</summary>
        private float _plankWidth = ScreenLayout.WidestShelf;

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
            float scale = layout.ChromeScale;
            _root.localPosition = new Vector3(0f, layout.ShelfMiddle, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);

            // From here on in the shelf's own points.
            _plankWidth = layout.ShelfWidth / scale;
            _plank.size = new Vector2(_plankWidth + PictureMargin, PictureHeight);
            WriteLine();
        }

        /// <summary>Changes what the shelf says.</summary>
        public void Say(string words)
        {
            _words = words;
            WriteLine();
        }

        /// <summary>The line is set at its design size, or smaller on a plank too short for it.</summary>
        private void WriteLine()
        {
            Lettering.SayWithin(_line, _words, LineSize, room: _plankWidth - 2f * LineInFromEnds);
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_root);
    }
}
