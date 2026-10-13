#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// A painting's pieces as a row of pips on a cream ground, as the design canvas sets it under a won level's
    /// new piece (WinTR): gilt for a piece won, gilt in rings of light and red for one just won, a hollow for one
    /// still to win. Where each pip stands is <see cref="PipRowPlan"/>'s; this draws them. Sizes are in points
    /// around the middle of the row at its full size; whoever places the row may draw it smaller.
    /// </summary>
    public sealed class PipRow
    {
        private readonly Transform _root;
        private readonly ArtCatalogue _art;
        private readonly DrawOrder _order;
        private readonly List<SpriteRenderer> _pips = new List<SpriteRenderer>();

        /// <param name="order">The pips' place in the drawing order.</param>
        public PipRow(string name, Transform parent, ArtCatalogue art, PipRowPlan plan, DrawOrder order)
        {
            _art = art;
            _order = order;
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            Plan = plan;
            Show(plan);
        }

        /// <summary>The row's place is its middle.</summary>
        public Transform Transform => _root;

        /// <summary>What the row shows now.</summary>
        public PipRowPlan Plan { get; private set; }

        /// <summary>Puts the middle of the row at a place of what it stands on, drawn at a share of its full size.</summary>
        public void Place(Vector2 middle, float scale = 1f)
        {
            _root.localPosition = new Vector3(middle.x, middle.y, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Changes what the row shows, where it stands: more pieces won, or another painting's pieces.</summary>
        public void Show(PipRowPlan plan)
        {
            Plan = plan;
            while (_pips.Count > plan.Faces.Count)
            {
                SceneObjects.DestroyObjectOf(_pips[_pips.Count - 1]);
                _pips.RemoveAt(_pips.Count - 1);
            }

            while (_pips.Count < plan.Faces.Count)
            {
                _pips.Add(SceneObjects.AddSprite($"Pip {_pips.Count + 1}", _root, _art.Of(GalleryArt.CardPipOff), _art.SpriteMaterial, _order));
            }

            for (int pip = 0; pip < _pips.Count; pip++)
            {
                _pips[pip].sprite = _art.Of(DrawingOf(plan.Faces[pip]));
                _pips[pip].transform.localPosition = new Vector3(plan.MiddleOf(pip), 0f, 0f);
            }
        }

        private static GalleryArt DrawingOf(PipFace face) => face switch
        {
            PipFace.Won => GalleryArt.CardPipOn,
            PipFace.JustWon => GalleryArt.CardPipNew,
            PipFace.NotWon => GalleryArt.CardPipOff,
            _ => throw new ArgumentOutOfRangeException(nameof(face), face, "A pip the design does not draw."),
        };
    }
}
