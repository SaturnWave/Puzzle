#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Jigsaws
{
    /// <summary>
    /// The plaque over a painting's jigsaw, as the design canvas draws it (JigsawTR): a cream plaque hung from the
    /// cornice on two wires, with the word "PARÇA", the ivory piece that stands for a painting's pieces, and how
    /// many of them are in their place: how much of the painting is restored. The pieces that wait are told by
    /// the tray under the painting. Once the painting is whole the label says so.
    /// </summary>
    public sealed class PieceCounter : IDisposable
    {
        /// <summary>On the plaque, in the plaque's design points from its middle: the label over the mark and the figures.</summary>
        private const float LabelUp = 15.5f;
        private const float FiguresDown = 6.5f;

        /// <summary>
        /// The design sets the figures at 31 points on a plaque 84 points high (JigsawTR, "7 / 10"); the game's
        /// plaque is the pages' own, 58 high, and its figures are as large as stand well under the label there.
        /// </summary>
        private const float FigureSize = 25f;
        private const float MarkSize = 34f;
        private const float MarkBox = 100f;
        private const float GapMarkToFigures = 3f;

        /// <summary>The mark's drawing has its piece a little left of and above the middle of its box; it is moved to stand level with the figures.</summary>
        private const float MarkLower = 1f;

        /// <summary>The wires hang this far to each side of the plaque's middle.</summary>
        private const float WireFromMiddle = 44f;

        private readonly PagePlaque _plaque;
        private readonly PaintedShape _wires;
        private readonly TextMeshPro _label;
        private readonly Transform _mark;
        private readonly TextMeshPro _figures;

        private PieceCounter(Transform parent, ArtCatalogue art)
        {
            _wires = PaintedShape.Create("Counter wires", parent, art.PaintMaterial, DrawOrder.Wire);
            _plaque = new PagePlaque("Piece counter", parent, art);
            _label = PagePlaque.WriteLabel("Label", _plaque.Content, art, DrawOrder.PlaqueLettering);
            _label.transform.localPosition = new Vector3(0f, LabelUp, 0f);
            _mark = SceneObjects.AddSprite("Mark", _plaque.Content, art.Of(GalleryArt.IconPiece), art.SpriteMaterial, DrawOrder.PlaqueArt).transform;
            _mark.localScale = new Vector3(MarkSize / MarkBox, MarkSize / MarkBox, 1f);
            _figures = Lettering.Write("Figures", _plaque.Content, art, LetteringStyle.Figure, FigureSize, Lettering.Ink, DrawOrder.PlaqueLettering);
        }

        public static PieceCounter Create(Transform parent, ArtCatalogue art) => new PieceCounter(parent, art);

        /// <summary>Hangs the plaque where the layout has it; call again whenever the screen changes.</summary>
        public void Place(JigsawLayout layout)
        {
            _plaque.Place(layout.Plaque);
            var sketch = new MeshSketch();
            PictureWire.Add(sketch, layout.Plaque.MiddleX - WireFromMiddle, layout.Plaque.Top, layout.Page.CorniceBottom);
            PictureWire.Add(sketch, layout.Plaque.MiddleX + WireFromMiddle - PictureWire.Width, layout.Plaque.Top, layout.Page.CorniceBottom);
            _wires.Show(sketch.ToMesh("Counter wires"));
        }

        /// <summary>Says how many of the painting's pieces are in place, or that the painting is whole.</summary>
        public void Show(JigsawTable table)
        {
            Lettering.Say(_label, table.IsWhole ? MuseumWords.RestoredLabel : MuseumWords.PieceLabel);
            string figures = MuseumWords.PiecesOf(table.InPlace.Count, table.PieceCount);
            Lettering.Say(_figures, figures);

            // The mark and the figures stand side by side as one group around the plaque's middle.
            float figuresWidth = Lettering.WidthOf(_figures, figures);
            float left = -(MarkSize + GapMarkToFigures + figuresWidth) / 2f;
            _mark.localPosition = new Vector3(left + MarkSize / 2f, -FiguresDown - MarkLower, 0f);
            _figures.transform.localPosition = new Vector3(
                left + MarkSize + GapMarkToFigures + figuresWidth / 2f, -FiguresDown - Lettering.FigureDrop(LetteringStyle.Figure) * FigureSize, 0f);
        }

        public void Dispose()
        {
            _wires.Dispose();
            SceneObjects.DestroyObjectOf(_plaque.Root);
        }
    }
}
