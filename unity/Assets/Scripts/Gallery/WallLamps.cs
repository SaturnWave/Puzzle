#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The two lamps on the wall beside the board, each in the pool of its own light. Where they hang, and whether
    /// the wall has room for them at all, is the rule of <see cref="Sconces"/>; a lamp's place is its foot.
    /// </summary>
    public sealed class WallLamps : IDisposable
    {
        private readonly Transform _root;
        private readonly Transform _left;
        private readonly Transform _right;

        /// <summary>The wall has room for the lamps on the screen they were last placed for; and whether they were put out since.</summary>
        private bool _hasRoom;
        private bool _isPutOut;

        private WallLamps(Transform parent, ArtCatalogue art)
        {
            _root = new GameObject("Wall lamps").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _left = AddLamp("Left lamp", art);
            _right = AddLamp("Right lamp", art);
        }

        public static WallLamps Create(Transform parent, ArtCatalogue art) => new WallLamps(parent, art);

        /// <summary>Hangs the lamps beside the board as the layout places it, or takes them down where the wall is too short.</summary>
        public void Place(ScreenLayout layout, BoardShape board)
        {
            SconcePlace place = Sconces.PlaceBeside(board, layout.WallAboveBoard);
            _hasRoom = place != SconcePlace.None;
            _root.gameObject.SetActive(_hasRoom && !_isPutOut);
            if (!_hasRoom)
            {
                return;
            }

            float foot = place == SconcePlace.InTheCutCorners
                ? layout.BoardTop - Sconces.FootBelowTheCellsTop
                : layout.BoardTop + Sconces.FootAboveTheCells;
            float boardRight = layout.BoardLeft + board.Width * layout.CellSize;
            _left.localPosition = new Vector3(layout.BoardLeft + Sconces.MiddleInFromTheSide, foot, 0f);
            _right.localPosition = new Vector3(boardRight - Sconces.MiddleInFromTheSide, foot, 0f);
        }

        /// <summary>
        /// Puts the lamps out for good: under a result the room is dark, and a lit lamp beside the card would be the
        /// one thing in it that is not, as the web test page has it too.
        /// </summary>
        public void PutOut()
        {
            _isPutOut = true;
            _root.gameObject.SetActive(false);
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_root);

        private Transform AddLamp(string name, ArtCatalogue art)
        {
            var lamp = new GameObject(name).AddComponent<SpriteRenderer>();
            lamp.transform.SetParent(_root, worldPositionStays: false);
            lamp.sprite = art.Of(GalleryArt.Sconce);
            lamp.sharedMaterial = art.SpriteMaterial;
            lamp.sortingOrder = (int)DrawOrder.Sconce;
            return lamp.transform;
        }
    }
}
