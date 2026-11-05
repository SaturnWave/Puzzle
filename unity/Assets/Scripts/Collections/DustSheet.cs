#nullable enable
using System.Collections.Generic;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Collections
{
    /// <summary>
    /// The cloth over a painting the player has not reached: a sheet of warm linen thrown over the frame, in the
    /// cream and beige of the gallery's plaques and canvases. It lies flat where it rests on the frame's top bar and
    /// falls in soft upright folds that deepen towards its hem; the hem droops between the folds and ends on the
    /// frame's lower bar, so the gilt under it and the brass plate still say that a painting hangs there. The cloth
    /// is painted as one net of corners whose colours are the folds; no texture is needed. Sizes are in points.
    /// </summary>
    public static class DustSheet
    {
        /// <summary>The cloth on the ridge of a fold, in the bottom of one, and the line around its edge.</summary>
        private static readonly Color Ridge = Tint.Of(0xF7EEDB);
        private static readonly Color Valley = Tint.Of(0xC6B189);
        private static readonly Color EdgeLine = Tint.Of(0x7A5A2E, 0.55f);
        private const float EdgeLineWidth = 1f;

        /// <summary>The cloth reaches this far past the frame's sides and top, and is this much wider at its hem than at its top.</summary>
        private const float PastTheSides = 2.5f;
        private const float OverTheTop = 2f;
        private const float WiderAtTheHem = 3f;

        /// <summary>The hem ends this share of the frame's width into the frame's lower bar, so most of that bar is seen under it, and droops this far between two folds.</summary>
        private const float HemIntoTheLowerBar = 0.1f;
        private const float HemDroop = 4.5f;

        /// <summary>About this much cloth lies between two folds; a sheet has at least this many.</summary>
        private const float ClothPerFold = 34f;
        private const int FewestFolds = 3;
        private const int ColumnsPerFold = 8;
        private const int Rows = 10;

        /// <summary>How much of a fold's depth is seen at the top, where the cloth lies on the frame; it is all seen at the hem.</summary>
        private const float FoldAtTheTop = 0.3f;

        /// <summary>The cloth is in a little more shade towards its hem, and throws a soft shadow of this height and strength under it.</summary>
        private const float DarkerAtTheHem = 0.09f;
        private const float ShadowHeight = 6f;
        private static readonly Color Shadow = Tint.Of(0x2A1606, 0.3f);

        /// <summary>The sheet over a painting, in the points of what the painting hangs on.</summary>
        /// <param name="picture">The painting's picture inside its frame.</param>
        /// <param name="frameWidth">How wide the frame is on every side of the picture.</param>
        /// <param name="softEdge">The width of a pixel, in points: the line around the cloth fades out over it.</param>
        public static Mesh Over(Box picture, float frameWidth, float softEdge)
        {
            float left = picture.Left - frameWidth - PastTheSides;
            float right = picture.Right + frameWidth + PastTheSides;
            float top = picture.Top + frameWidth + OverTheTop;
            float hem = picture.Bottom - HemIntoTheLowerBar * frameWidth;
            int folds = Mathf.Max(FewestFolds, Mathf.RoundToInt((right - left) / ClothPerFold));
            int columns = folds * ColumnsPerFold;

            var sketch = new MeshSketch();
            AddShadow(sketch, left, right, hem, folds, columns);
            var corners = new int[Rows + 1, columns + 1];
            for (int row = 0; row <= Rows; row++)
            {
                float down = (float)row / Rows;
                for (int column = 0; column <= columns; column++)
                {
                    float across = (float)column / columns;
                    corners[row, column] = sketch.AddCorner(PlaceOnTheCloth(left, right, top, hem, folds, across, down), ColourOnTheCloth(folds, across, down));
                }
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    sketch.AddQuad(corners[row, column], corners[row, column + 1], corners[row + 1, column + 1], corners[row + 1, column]);
                }
            }

            Stroke.Add(sketch, Edge(left, right, top, hem, folds, columns), isClosed: true, EdgeLineWidth, softEdge, EdgeLine);
            return sketch.ToMesh("Dust sheet");
        }

        /// <summary>How deep in a fold a place across the cloth lies: 1 on a ridge, 0 in the bottom of a fold. The cloth begins and ends on a ridge, where the frame's corners hold it out.</summary>
        private static float RidgeAt(int folds, float across) => 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * folds * across);

        /// <summary>Where a place on the cloth lies: across it from 0 to 1, down it from 0 at the top to 1 at the hem. The hem hangs lowest in the folds.</summary>
        private static Vector2 PlaceOnTheCloth(float left, float right, float top, float hem, int folds, float across, float down)
        {
            float x = Mathf.Lerp(left, right, across) + (across - 0.5f) * WiderAtTheHem * down;
            float hemHere = hem - HemDroop * (1f - RidgeAt(folds, across));
            return new Vector2(x, Mathf.Lerp(top, hemHere, down));
        }

        private static Color ColourOnTheCloth(int folds, float across, float down)
        {
            float depthSeen = Mathf.Lerp(FoldAtTheTop, 1f, down * down);
            Color cloth = Color.Lerp(Valley, Ridge, Mathf.Lerp(0.72f, RidgeAt(folds, across), depthSeen));
            float light = 1f - DarkerAtTheHem * down;
            return new Color(cloth.r * light, cloth.g * light, cloth.b * light, 1f);
        }

        /// <summary>The cloth's edge as a closed row of points: down its left side, along the hem, up its right side.</summary>
        private static List<Vector2> Edge(float left, float right, float top, float hem, int folds, int columns)
        {
            var edge = new List<Vector2>(columns + 3) { PlaceOnTheCloth(left, right, top, hem, folds, 0f, 0f) };
            for (int column = 0; column <= columns; column++)
            {
                edge.Add(PlaceOnTheCloth(left, right, top, hem, folds, (float)column / columns, 1f));
            }

            edge.Add(PlaceOnTheCloth(left, right, top, hem, folds, 1f, 0f));
            return edge;
        }

        /// <summary>The shadow the hem throws on the frame and the wall under it: strongest at the hem, gone a little below.</summary>
        private static void AddShadow(MeshSketch sketch, float left, float right, float hem, int folds, int columns)
        {
            Color gone = new Color(Shadow.r, Shadow.g, Shadow.b, 0f);
            int hemBefore = -1;
            int belowBefore = -1;
            for (int column = 0; column <= columns; column++)
            {
                Vector2 onTheHem = PlaceOnTheCloth(left, right, hem, hem, folds, (float)column / columns, 1f);
                int hemHere = sketch.AddCorner(onTheHem, Shadow);
                int belowHere = sketch.AddCorner(onTheHem - new Vector2(0f, ShadowHeight), gone);
                if (column > 0)
                {
                    sketch.AddQuad(hemBefore, hemHere, belowHere, belowBefore);
                }

                hemBefore = hemHere;
                belowBefore = belowHere;
            }
        }
    }
}
