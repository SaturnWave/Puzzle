#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Painting;
using Puzzle.Museum.Jigsaw;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The painting a level's jigsaw piece belongs to, small on the board's plaque, as the design canvas draws it
    /// (BoardTR): the whole picture under a dark teal veil, the lines of its cut, and the pieces winning the level
    /// gives lit, unveiled and outlined in gilt, in a thin frame of brown, gilt and dark brown. It is as large as
    /// the painting's proportions allow in the canvas' room of 92 by 54 points, drawn around its own middle. Its
    /// parts are drawn as a painted canvas is (<see cref="JigsawCanvas"/>): from the painting's cut at its size.
    /// </summary>
    public sealed class RewardThumb : IDisposable
    {
        /// <summary>The room the picture has on the plaque (BoardTR, 92 by 54).</summary>
        public const float WidestPicture = 92f;
        public const float TallestPicture = 54f;

        /// <summary>The frame (BoardTR): a brown line of 1.5 around the picture, gilt out to 3.2, a dark line out to 4.4.</summary>
        private const float BrownLineOut = 1.5f;
        private const float GiltOut = 3.2f;
        private const float DarkLineOut = 4.4f;
        private static readonly Color BrownLine = Tint.Of(0x7A4A12);
        private static readonly Color Gilt = Tint.Of(0xF6B93C);
        private static readonly Color DarkLine = Tint.Of(0x5E3305);

        /// <summary>A painting with no picture yet is primed linen, as on a canvas (<see cref="JigsawCanvas"/>).</summary>
        private static readonly Color PrimedFace = Tint.Of(0xC9B78E);

        /// <summary>The veil over what the level does not give, and the hairlines of the cut over all of it.</summary>
        private static readonly Color Veil = Tint.Of(0x183E46, 0.5f);
        private static readonly Color CutLine = Tint.Of(0xFFFFFF, 0.34f);
        private const float CutLineWidth = 0.7f;

        /// <summary>A piece the level gives: a dark line under a gilt one along its outline.</summary>
        private static readonly Color LitShade = Tint.Of(0x5E3305, 0.8f);
        private static readonly Color LitLine = Tint.Of(0xFFE27A);
        private const float LitShadeWidth = 2.6f;
        private const float LitLineWidth = 1.5f;

        private readonly PaintingLook _look;
        private readonly IReadOnlyList<int> _lit;
        private readonly PaintedShape _frame;
        private readonly PaintedShape _picture;
        private readonly PaintedShape _veil;
        private readonly PaintedShape _litPieces;
        private readonly PaintedShape _lines;

        private RewardThumb(Transform parent, ArtCatalogue art, PaintingLook look, PieceReward reward)
        {
            _look = look;
            _lit = reward.Pieces;
            Size = SizeFor(look.Aspect);
            Transform = new GameObject("Painting of the piece").transform;
            Transform.SetParent(parent, worldPositionStays: false);
            _frame = PaintedShape.Create("Frame", Transform, art.PaintMaterial, DrawOrder.PlaqueArt);
            _picture = PaintedShape.Create("Picture", Transform, art.PaintMaterial, DrawOrder.PlaquePicture, look.Picture);
            _veil = PaintedShape.Create("Veil", Transform, art.PaintMaterial, DrawOrder.PlaquePictureMark);
            _litPieces = PaintedShape.Create("Pieces the level gives", Transform, art.PaintMaterial, DrawOrder.PlaquePictureLit, look.Picture);
            _lines = PaintedShape.Create("Cut", Transform, art.PaintMaterial, DrawOrder.PlaquePictureLines);
        }

        /// <summary>The thumbnail's place is the middle of its picture.</summary>
        public Transform Transform { get; }

        /// <summary>How large the picture is, without its frame.</summary>
        public Vector2 Size { get; }

        /// <summary>Builds the thumbnail of the painting a reward's pieces belong to. Call <see cref="Show"/> before the first picture.</summary>
        public static RewardThumb Create(Transform parent, ArtCatalogue art, PaintingGallery paintings, PieceReward reward) =>
            new RewardThumb(parent, art, paintings.LookOf(reward.Painting), reward);

        /// <summary>The largest picture of a painting's proportions in the room the plaque has for it.</summary>
        public static Vector2 SizeFor(float aspect) => aspect >= WidestPicture / TallestPicture
            ? new Vector2(WidestPicture, WidestPicture / aspect)
            : new Vector2(TallestPicture * aspect, TallestPicture);

        /// <summary>Draws the thumbnail for the size of a pixel on the screen, which its hairlines fade out over.</summary>
        /// <param name="softEdge">The width of a pixel, in the plaque's own points.</param>
        public void Show(float softEdge)
        {
            var picture = new Rect(-Size / 2f, Size);
            _frame.Show(Frame(picture));

            Color face = _look.Picture != null ? Color.white : PrimedFace;
            var whole = new MeshSketch();
            whole.AddRectangle(picture, face, face, new Rect(0f, 0f, 1f, 1f));
            _picture.Show(whole.ToMesh("Painting"));

            var veil = new MeshSketch();
            veil.AddRectangle(picture, Veil);
            _veil.Show(veil.ToMesh("Veil"));

            JigsawCut cut = _look.CutAt(Size);
            var lit = new MeshSketch();
            foreach (int number in _lit)
            {
                JigsawShapes.AddFace(lit, cut.Pieces[number], Size, Vector2.zero, face);
            }

            _litPieces.Show(lit.ToMesh("Pieces the level gives"));
            _lines.Show(Lines(cut, softEdge));
        }

        public void Dispose()
        {
            _frame.Dispose();
            _picture.Dispose();
            _veil.Dispose();
            _litPieces.Dispose();
            _lines.Dispose();
            _look.Dispose();
            SceneObjects.DestroyObjectOf(Transform);
        }

        private static Mesh Frame(Rect picture)
        {
            var sketch = new MeshSketch();
            sketch.AddRectangle(Grown(picture, DarkLineOut), DarkLine);
            sketch.AddRectangle(Grown(picture, GiltOut), Gilt);
            sketch.AddRectangle(Grown(picture, BrownLineOut), BrownLine);
            return sketch.ToMesh("Frame");
        }

        /// <summary>Every stretch of the cut once, as a hairline; then each piece the level gives, outlined.</summary>
        private Mesh Lines(JigsawCut cut, float softEdge)
        {
            var sketch = new MeshSketch();
            foreach (JigsawPiece piece in cut.Pieces)
            {
                foreach (PieceEdge edge in piece.Edges)
                {
                    // Two neighbours share a stretch of the cut; the one with the lower number draws it. The painting's edge is the frame's.
                    if (edge.Neighbour is int neighbour && neighbour > piece.Number)
                    {
                        Stroke.Add(sketch, JigsawShapes.LineOf(edge.Path, Size), isClosed: false, CutLineWidth, softEdge, CutLine);
                    }
                }
            }

            foreach (int number in _lit)
            {
                List<Vector2> outline = JigsawShapes.OutlineOf(cut.Pieces[number], Size);
                Stroke.Add(sketch, outline, isClosed: true, LitShadeWidth, softEdge, LitShade);
                Stroke.Add(sketch, outline, isClosed: true, LitLineWidth, softEdge, LitLine);
            }

            return sketch.ToMesh("Cut");
        }

        private static Rect Grown(Rect rect, float by) => new Rect(rect.x - by, rect.y - by, rect.width + 2f * by, rect.height + 2f * by);
    }
}
