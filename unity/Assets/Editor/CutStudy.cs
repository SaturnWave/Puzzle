#nullable enable
using System.Collections.Generic;
using System.Linq;
using Puzzle.Museum;
using Puzzle.Museum.Jigsaw;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// A study picture of one jigsaw cut, to look at the pieces' shapes before any screen draws them: on the left
    /// the painting whole with its cut lines, on the right its pieces pulled apart, each lying over its shadow with
    /// a light edge. The picture is painted in memory from the cut's own outlines, so what it shows is the model.
    /// </summary>
    public static class CutStudy
    {
        /// <summary>The painting's longer side in the study, in pixels, whatever the size of its file.</summary>
        private const float LongerSide = 1000f;

        /// <summary>The wall around and between the two panels.</summary>
        private const int Margin = 48;

        /// <summary>How far the pieces are pulled apart: each moves away from the middle by this share of its distance from it.</summary>
        private const float Spread = 0.3f;

        private const int PointsPerCurve = 10;
        private const float CutLineWidth = 2f;
        private const float PieceEdgeWidth = 2.5f;

        private static readonly Color Wall = new Color32(0x12, 0x46, 0x52, 0xFF);
        private static readonly Color CutLine = new Color32(0x2A, 0x16, 0x06, 0xFF);
        private static readonly Color PieceEdge = new Color32(0xFF, 0xF2, 0xC4, 0xFF);
        private static readonly Color Shadow = new Color(0.01f, 0.08f, 0.1f, 0.5f);
        private static readonly Vector2 ShadowFall = new Vector2(5f, 8f);

        /// <summary>Paints the study of a picture cut with a seed. The caller owns the result and destroys it.</summary>
        /// <param name="picture">The painting, readable; its proportions are the cut's.</param>
        public static Texture2D Paint(Texture2D picture, ulong seed, JigsawSize size, PaintingOrientation orientation)
        {
            float scale = LongerSide / Mathf.Max(picture.width, picture.height);
            var shown = new Vector2(picture.width * scale, picture.height * scale);
            JigsawCut cut = JigsawCut.Of(seed, size, orientation, new PictureSize(shown.x, shown.y));
            List<Vector2>[] outlines = cut.Pieces.Select(OutlineOf).ToArray();

            var apartPanel = shown * (1f + Spread);
            var canvas = new PixelCanvas(
                Mathf.CeilToInt(Margin + shown.x + Margin + apartPanel.x + Margin),
                Mathf.CeilToInt(Margin + apartPanel.y + Margin),
                Wall);

            var wholeAt = new Vector2(Margin, Margin + (apartPanel.y - shown.y) / 2f);
            PaintWhole(canvas, picture, shown, outlines, wholeAt);

            var apartAt = new Vector2(Margin + shown.x + Margin, Margin);
            Vector2[] places = cut.Pieces.Select(piece => apartAt + Spread * ToVector(piece.Home)).ToArray();
            PaintApart(canvas, picture, shown, outlines, places);
            return canvas.ToPicture();
        }

        private static void PaintWhole(PixelCanvas canvas, Texture2D picture, Vector2 shown, List<Vector2>[] outlines, Vector2 at)
        {
            foreach (List<Vector2> outline in outlines)
            {
                canvas.Fill(Moved(outline, at), pixel => ColourAt(picture, shown, pixel - at));
            }

            foreach (List<Vector2> outline in outlines)
            {
                canvas.Stroke(Moved(outline, at), CutLineWidth, CutLine);
            }
        }

        private static void PaintApart(PixelCanvas canvas, Texture2D picture, Vector2 shown, List<Vector2>[] outlines, Vector2[] places)
        {
            // Every shadow first: a piece's shadow may fall on the wall beside its neighbour, never on the neighbour.
            for (int piece = 0; piece < outlines.Length; piece++)
            {
                canvas.Fill(Moved(outlines[piece], places[piece] + ShadowFall), _ => Shadow);
            }

            for (int piece = 0; piece < outlines.Length; piece++)
            {
                Vector2 place = places[piece];
                List<Vector2> moved = Moved(outlines[piece], place);
                canvas.Fill(moved, pixel => ColourAt(picture, shown, pixel - place));
                canvas.Stroke(moved, PieceEdgeWidth, PieceEdge);
            }
        }

        private static List<Vector2> OutlineOf(JigsawPiece piece) => piece.Outline.Flatten(PointsPerCurve).Select(ToVector).ToList();

        private static Vector2 ToVector(Point2 point) => new Vector2((float)point.X, (float)point.Y);

        private static List<Vector2> Moved(List<Vector2> outline, Vector2 by) => outline.Select(point => point + by).ToList();

        /// <summary>The painting's colour at a point of the shown picture, counted from its top left corner; a texture counts from the foot.</summary>
        private static Color ColourAt(Texture2D picture, Vector2 shown, Vector2 point) =>
            picture.GetPixelBilinear(point.x / shown.x, 1f - point.y / shown.y);
    }
}
