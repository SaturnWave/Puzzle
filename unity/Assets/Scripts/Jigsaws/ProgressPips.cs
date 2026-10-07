#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Jigsaws
{
    /// <summary>
    /// A painting's pieces as a row of pips under its brass plate, as the design canvas draws them (JigsawTR): one
    /// pip for every piece, gilt for a piece in its place, a hollow in a ring of light for one that waits in the
    /// tray, a plain hollow for one not won yet. The pips stand in a dark bar that hangs from the plate on two
    /// short wires; a painting of more than twenty pieces has two bars, one above the other. Sizes are in points
    /// around the middle of the bars at full size.
    /// </summary>
    public sealed class ProgressPips : IDisposable
    {
        /// <summary>A bar's picture is the bar and the room around it for its ring and drop: 12 points wider, 32 high.</summary>
        private const float BarPictureMargin = 12f;
        private const float BarPictureHeight = 32f;

        /// <summary>The pips keep this far from the bar's round ends; a pip is 13 by 9 points and never closer to the next than this.</summary>
        private const float PipsInFromEnds = 11f;
        private const float PipWidth = 13f;
        private const float NarrowestGap = 2f;

        /// <summary>The wires from the plate hang this far to each side of the middle.</summary>
        private const float WireFromMiddle = 70f;

        private readonly Transform _root;
        private readonly ArtCatalogue _art;
        private readonly PaintedShape _wires;
        private readonly List<SpriteRenderer> _bars = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _pips = new List<SpriteRenderer>();

        private ProgressPips(Transform parent, ArtCatalogue art, int pieceCount)
        {
            _art = art;
            _wires = PaintedShape.Create("Pip wires", parent, art.PaintMaterial, DrawOrder.Wire);
            _root = new GameObject("Progress pips").transform;
            _root.SetParent(parent, worldPositionStays: false);
            for (int piece = 0; piece < pieceCount; piece++)
            {
                _pips.Add(SceneObjects.AddSprite($"Pip {piece}", _root, art.Of(GalleryArt.PipOff), art.SpriteMaterial, DrawOrder.Pip));
            }
        }

        /// <param name="pieceCount">How many pieces the painting has: one pip each.</param>
        public static ProgressPips Create(Transform parent, ArtCatalogue art, int pieceCount) => new ProgressPips(parent, art, pieceCount);

        /// <summary>Hangs the bars where the layout has them; call again whenever the screen changes.</summary>
        public void Place(JigsawLayout layout)
        {
            float scale = layout.PipScale;
            _root.localPosition = new Vector3(layout.Pips.MiddleX, layout.Pips.MiddleY, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);

            // From here on in the bars' own points.
            float barWidth = layout.PipBarWidth / scale;
            float rowsApart = layout.PipRows > 1 ? (layout.Pips.Height / scale - JigsawLayout.PipBarHeight) / (layout.PipRows - 1) : 0f;
            HangTheBars(layout.PipRows, barWidth, rowsApart);
            SetOutThePips(layout.PipRows, barWidth, rowsApart);

            var wires = new MeshSketch();
            float wire = Mathf.Min(WireFromMiddle * scale, layout.Hanging.WidestPlate / 2f);
            PictureWire.Add(wires, layout.Pips.MiddleX - wire, layout.Pips.Top, layout.Hanging.PlateBottom);
            PictureWire.Add(wires, layout.Pips.MiddleX + wire - PictureWire.Width, layout.Pips.Top, layout.Hanging.PlateBottom);
            _wires.Show(wires.ToMesh("Pip wires"));
        }

        /// <summary>Shows every piece's pip as the table has the piece.</summary>
        public void Show(JigsawTable table)
        {
            var inPlace = new HashSet<int>(table.InPlace);
            var waiting = new HashSet<int>(table.Waiting);
            for (int piece = 0; piece < _pips.Count; piece++)
            {
                GalleryArt pip = inPlace.Contains(piece) ? GalleryArt.PipOn : waiting.Contains(piece) ? GalleryArt.PipNext : GalleryArt.PipOff;
                _pips[piece].sprite = _art.Of(pip);
            }
        }

        public void Dispose()
        {
            _wires.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        private void HangTheBars(int rows, float barWidth, float rowsApart)
        {
            while (_bars.Count < rows)
            {
                SpriteRenderer bar = SceneObjects.AddSprite($"Bar {_bars.Count + 1}", _root, _art.Of(GalleryArt.PipBar), _art.SpriteMaterial, DrawOrder.PipBar);
                bar.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
                _bars.Add(bar);
            }

            for (int row = 0; row < _bars.Count; row++)
            {
                _bars[row].gameObject.SetActive(row < rows);
                _bars[row].transform.localPosition = new Vector3(0f, RowMiddle(row, rows, rowsApart), 0f);
                _bars[row].size = new Vector2(barWidth + BarPictureMargin, BarPictureHeight);
            }
        }

        /// <summary>The pips row by row, each row spread over the bar's width; a row of many pips draws them narrower.</summary>
        private void SetOutThePips(int rows, float barWidth, float rowsApart)
        {
            int perRow = (_pips.Count + rows - 1) / rows;
            float room = barWidth - 2f * PipsInFromEnds;
            float width = Mathf.Min(PipWidth, (room - (perRow - 1) * NarrowestGap) / perRow);
            float step = perRow > 1 ? (room - width) / (perRow - 1) : 0f;
            for (int piece = 0; piece < _pips.Count; piece++)
            {
                int row = piece / perRow;
                int inRow = piece % perRow;
                int pipsInThisRow = Mathf.Min(perRow, _pips.Count - row * perRow);
                float rowWidth = width + (pipsInThisRow - 1) * step;
                Transform pip = _pips[piece].transform;
                pip.localPosition = new Vector3(-rowWidth / 2f + width / 2f + inRow * step, RowMiddle(row, rows, rowsApart), 0f);
                pip.localScale = new Vector3(width / PipWidth, 1f, 1f);
            }
        }

        private static float RowMiddle(int row, int rows, float rowsApart) => ((rows - 1) / 2f - row) * rowsApart;
    }
}
