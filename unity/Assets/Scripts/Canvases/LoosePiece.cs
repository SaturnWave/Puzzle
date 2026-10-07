#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Painting;
using Puzzle.Museum.Jigsaw;
using UnityEngine;

namespace Puzzle.Client.Canvases
{
    /// <summary>
    /// One jigsaw piece by itself, out of its painting, as the design canvas draws a piece that was just won (WinTR)
    /// or waits to be placed (HomeTR): its part of the picture in a white rim, on a brown copy of itself that shows
    /// as the thickness of the cardboard. The piece is drawn around the middle of its cell, so it can be stood,
    /// turned and scaled as one thing.
    /// </summary>
    public sealed class LoosePiece : IDisposable
    {
        /// <summary>The cardboard shows this far to the lower right of the face, as a share of the cell's shorter side.</summary>
        private static readonly Vector2 CardboardOffset = new Vector2(0.03f, -0.05f);
        private static readonly Color Cardboard = Tint.Of(0x8A5524);

        /// <summary>The rim, as a share of the cell's shorter side.</summary>
        private const float RimWidth = 0.028f;
        private static readonly Color Rim = Tint.Of(0xFFFFFF, 0.9f);

        /// <summary>A piece of a canvas that has no picture.</summary>
        private static readonly Color PrimedFace = Tint.Of(0xD8C8A2);

        /// <summary>
        /// The shadow of a piece that is held over the page: the design's drop shadow under the waiting piece. At
        /// the piece's full lift it lies this far to the lower right, as a share of the cell's shorter side, and
        /// fades out over this share of it.
        /// </summary>
        private static readonly Color ShadowColour = Tint.Of(0x031E24, 0.42f);
        private static readonly Vector2 ShadowAtFullLift = new Vector2(0.06f, -0.1f);
        private const float ShadowFade = 0.07f;

        private readonly PaintedShape _shadow;
        private readonly PaintedShape _cardboard;
        private readonly PaintedShape _face;
        private readonly PaintedShape _rim;
        private readonly float _shorterSide;

        private LoosePiece(Transform parent, ArtCatalogue art, PaintingLook look, int pieceNumber, float paintingWidth, float softEdge, DrawOrder under)
        {
            var paintingSize = new Vector2(paintingWidth, paintingWidth / look.Aspect);
            JigsawPiece piece = look.CutAt(paintingSize).Pieces[pieceNumber];
            Number = pieceNumber;
            Size = new Vector2((float)piece.Cell.Width, (float)piece.Cell.Height);
            Vector2 toTheCellsMiddle = -JigsawShapes.InScene(piece.Home, paintingSize);
            float shorterSide = Mathf.Min(Size.x, Size.y);
            _shorterSide = shorterSide;

            Transform = new GameObject($"Piece {pieceNumber}").transform;
            Transform.SetParent(parent, worldPositionStays: false);

            // Under the piece, one place below the given one, its shadow: out of sight until the piece is lifted.
            _shadow = PaintedShape.Create("Shadow", Transform, art.PaintMaterial, under - 1);
            var shadow = new MeshSketch();
            JigsawShapes.AddShade(shadow, piece, paintingSize, toTheCellsMiddle, ShadowColour, ShadowFade * shorterSide);
            _shadow.Show(shadow.ToMesh("Piece shadow"));
            Lift(0f);

            // Three layers from the given place in the drawing order up: the cardboard, the face on it, the rim around the face.
            _cardboard = PaintedShape.Create("Cardboard", Transform, art.PaintMaterial, under);
            _face = PaintedShape.Create("Face", Transform, art.PaintMaterial, under + 1, look.Picture);
            _rim = PaintedShape.Create("Rim", Transform, art.PaintMaterial, under + 2);

            var cardboard = new MeshSketch();
            JigsawShapes.AddFace(cardboard, piece, paintingSize, toTheCellsMiddle + CardboardOffset * shorterSide, Cardboard);
            _cardboard.Show(cardboard.ToMesh("Piece cardboard"));

            var face = new MeshSketch();
            JigsawShapes.AddFace(face, piece, paintingSize, toTheCellsMiddle, look.Picture != null ? Color.white : PrimedFace);
            _face.Show(face.ToMesh("Piece face"));

            var rim = new MeshSketch();
            List<Vector2> outline = JigsawShapes.OutlineOf(piece, paintingSize);
            for (int point = 0; point < outline.Count; point++)
            {
                outline[point] += toTheCellsMiddle;
            }

            Stroke.Add(rim, outline, isClosed: true, RimWidth * shorterSide, softEdge, Rim);
            _rim.Show(rim.ToMesh("Piece rim"));
        }

        /// <summary>The piece's place is the middle of its cell.</summary>
        public Transform Transform { get; }

        /// <summary>Which piece of its painting's cut this is.</summary>
        public int Number { get; }

        /// <summary>The piece's cell, knobs aside, at the size it was built for.</summary>
        public Vector2 Size { get; }

        /// <summary>Builds a piece of a painting.</summary>
        /// <param name="pieceNumber">Which piece of the painting's cut.</param>
        /// <param name="paintingWidth">How wide the whole painting would be at the size the piece is drawn.</param>
        /// <param name="softEdge">The width of a pixel, in the piece's own units: its rim fades out over it.</param>
        /// <param name="under">The lowest of the three places in the drawing order the piece takes; its shadow, while it is lifted, takes the place below that.</param>
        /// <exception cref="ArgumentOutOfRangeException">The painting has no such piece.</exception>
        public static LoosePiece Create(Transform parent, ArtCatalogue art, PaintingLook look, int pieceNumber, float paintingWidth, float softEdge, DrawOrder under)
        {
            if (pieceNumber < 0 || pieceNumber >= look.Number.Size.PieceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceNumber), pieceNumber, $"Painting {look.Number} has the pieces 0 to {look.Number.Size.PieceCount - 1}.");
            }

            return new LoosePiece(parent, art, look, pieceNumber, paintingWidth, softEdge, under);
        }

        /// <summary>Moves the piece in the drawing order: it takes the three places from the given one up, its shadow the place below.</summary>
        public void DrawFrom(DrawOrder under)
        {
            _shadow.DrawAt(under - 1);
            _cardboard.DrawAt(under);
            _face.DrawAt(under + 1);
            _rim.DrawAt(under + 2);
        }

        /// <summary>
        /// How high the piece is held over the page: 0 lies on it and throws no shadow, 1 is in the player's hand,
        /// with its shadow far out to the lower right.
        /// </summary>
        public void Lift(float height)
        {
            _shadow.Transform.gameObject.SetActive(height > 0f);
            Vector2 offset = ShadowAtFullLift * (_shorterSide * Mathf.Clamp01(height));
            _shadow.Transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        }

        public void Dispose()
        {
            _shadow.Dispose();
            _cardboard.Dispose();
            _face.Dispose();
            _rim.Dispose();
            SceneObjects.DestroyObjectOf(Transform);
        }
    }
}
