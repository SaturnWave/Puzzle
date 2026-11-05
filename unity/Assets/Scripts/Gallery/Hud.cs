#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// What hangs on the wall above the board: a plaque at each side of the round moves medallion (HAMLE), each on
    /// thin picture wires that run up out of the screen. What the plaques show the jigsaw piece the level gives
    /// decides (<see cref="BoardPlaques"/>): with a piece, the design canvas' arrangement (BoardTR), "BÖLÜM 47" over
    /// the goals at the left and "PARÇA 7 / 10" over the veiled painting with that piece lit at the right; without
    /// one, the web test page's, the level's number at the left and the goals under "HEDEF" at the right. Sizes and
    /// places are the canvas' at full size, in points around the medallion's middle; a screen on which the board
    /// needs the height draws the whole row smaller (<see cref="ScreenLayout.HudScale"/>).
    /// </summary>
    public sealed class Hud : IDisposable
    {
        private const float MedallionSize = ScreenLayout.HudHeight;
        private const float PlaqueHeight = 84f;
        private const float GapBetween = 8f;

        /// <summary>The plaque's picture is its body and the room around it for its rim and shadow: 28 points wider, 118 high.</summary>
        private const float PlaquePictureMargin = 28f;
        private const float PlaquePictureHeight = 118f;

        /// <summary>A plaque's face has 7 points of rim and padding over it and 6 under it, so its middle lies this far above the plaque's.</summary>
        private const float FaceMiddle = 0.5f;

        /// <summary>The labels, as the design sets them (BoardTR, "HAMLE"): 10 points, their capitals apart by this many hundredths of their size.</summary>
        private const float LabelSize = 10f;
        private const float LabelSpacing = 9f;

        /// <summary>The label over the painting is a little smaller and closer set (BoardTR, "PARÇA 7 / 10": 9.5 points, 7 hundredths), 8.5 over the picture.</summary>
        private const float PieceLabelSize = 9.5f;
        private const float PieceLabelSpacing = 7f;
        private const float GapUnderPieceLabel = 8.5f;

        /// <summary>The moves are the design's (BoardTR, "14": 40 points); the level's number alone on its plaque is the web page's.</summary>
        private const float LevelNumberSize = 31f;
        private const float MovesNumberSize = 40f;

        /// <summary>
        /// The room between a label and what stands under it: the level's number, the moves, the goals under
        /// "HEDEF" (the web page's), the goals under "BÖLÜM 47" (BoardTR, 3).
        /// </summary>
        private const float GapUnderLevelLabel = 6f;
        private const float GapUnderMovesLabel = 2f;
        private const float GapUnderGoalsLabel = 4f;
        private const float GapUnderLevelOverGoals = 3f;

        /// <summary>The moves stand this far above the foot of the medallion's dial, more than above its head.</summary>
        private const float RoomUnderMoves = 3f;

        /// <summary>At the height of its swell the number of moves is drawn this much larger than at rest.</summary>
        private const float MovesSwellBy = 0.18f;

        /// <summary>Where a plaque's two wires meet its top edge.</summary>
        private const float WireInFromPlaqueSide = 27f;

        private readonly Transform _root;
        private readonly PaintedShape _wires;
        private readonly Plaque _left;
        private readonly Plaque _right;
        private readonly TextMeshPro _movesNumber;

        /// <summary>The label over the goals: "HEDEF" on the right plaque, or "BÖLÜM 47" on the left one; the goals stand under it.</summary>
        private readonly TextMeshPro _goalsLabel;
        private readonly float _gapUnderGoalsLabel;
        private readonly GoalRow _goals;

        /// <summary>The painting of the piece the level gives, on the right plaque; nothing where it gives none.</summary>
        private readonly RewardThumb? _thumb;

        /// <summary>The labels with their sizes. They keep their size on the screen when a phone draws the row smaller, as the web page keeps them.</summary>
        private readonly List<(TextMeshPro Label, float Size)> _labels = new List<(TextMeshPro, float)>();

        /// <summary>True when the goals stand on the left plaque (under "BÖLÜM 47"), false when on the right one (under "HEDEF").</summary>
        private readonly bool _areTheGoalsOnTheLeft;

        private Hud(Transform parent, ArtCatalogue art, BoardPlaques plaques, PaintingGallery paintings)
        {
            _root = new GameObject("Hud").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wires = PaintedShape.Create("Wires", _root, art.PaintMaterial, DrawOrder.Wire);
            _left = new Plaque("Left plaque", _root, art);
            AddMedallion(art);
            (float movesLabelAt, float movesNumberAt) = Stacked(0f, LabelSize, GapUnderMovesLabel, MovesNumberSize, RoomUnderMoves);
            WriteLabel(BoardWords.Moves, _root, art, LabelSize, LabelSpacing, movesLabelAt);
            _movesNumber = WriteNumber("Moves", _root, art, MovesNumberSize, movesNumberAt);
            _right = new Plaque("Right plaque", _root, art);

            switch (plaques)
            {
                case BoardPlaques.LevelBesideGoals beside:
                    (float levelLabelAt, float levelNumberAt) = Stacked(FaceMiddle, LabelSize, GapUnderLevelLabel, LevelNumberSize);
                    WriteLabel(beside.LevelLabel, _left.Group, art, LabelSize, LabelSpacing, levelLabelAt);
                    Lettering.Say(WriteNumber("Level", _left.Group, art, LevelNumberSize, levelNumberAt), beside.LevelFigure);
                    _goalsLabel = WriteLabel(beside.GoalsLabel, _right.Group, art, LabelSize, LabelSpacing, 0f);
                    _goals = new GoalRow(_right.Group, art);
                    _gapUnderGoalsLabel = GapUnderGoalsLabel;
                    _areTheGoalsOnTheLeft = false;
                    break;
                case BoardPlaques.LevelOverGoalsBesideThePiece withThePiece:
                    _goalsLabel = WriteLabel(withThePiece.GoalsLabel, _left.Group, art, LabelSize, LabelSpacing, 0f);
                    _goals = new GoalRow(_left.Group, art);
                    _gapUnderGoalsLabel = GapUnderLevelOverGoals;
                    _areTheGoalsOnTheLeft = true;
                    _thumb = RewardThumb.Create(_right.Group, art, paintings, withThePiece.Reward);
                    (float pieceLabelAt, float thumbAt) = Stacked(FaceMiddle, PieceLabelSize, GapUnderPieceLabel, _thumb.Size.y);
                    WriteLabel(withThePiece.PieceLabel, _right.Group, art, PieceLabelSize, PieceLabelSpacing, pieceLabelAt);
                    _thumb.Transform.localPosition = new Vector3(0f, thumbAt, 0f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plaques), plaques, "Plaques the row does not know how to hang.");
            }
        }

        /// <summary>Builds the row of a level's plaques, as the piece the level gives arranges them.</summary>
        /// <param name="paintings">Where the painting of the piece comes from.</param>
        public static Hud Create(Transform parent, ArtCatalogue art, BoardPlaques plaques, PaintingGallery paintings) => new Hud(parent, art, plaques, paintings);

        /// <summary>Shows the numbers of a moment of the game.</summary>
        public void Show(HudFigures figures)
        {
            _movesNumber.color = figures.MovesAreFew ? Lettering.Danger : Lettering.Ink;
            Lettering.Say(_movesNumber, BoardWords.Number(figures.MovesLeft));

            _goals.Show(figures.Goals);
            (float labelAt, float rowAt) = Stacked(FaceMiddle, LabelSize, _gapUnderGoalsLabel, _goals.Height);
            _goalsLabel.transform.localPosition = new Vector3(0f, labelAt, 0f);
            _goals.Transform.localPosition = new Vector3(0f, rowAt, 0f);
        }

        /// <summary>
        /// Swells the number of moves around its middle: 0 is its size at rest, 1 the height of the swell. In a won
        /// game's finale it swells with every move that leaves the medallion for the board.
        /// </summary>
        public void SwellTheMoves(float swell)
        {
            float size = 1f + MovesSwellBy * swell;
            _movesNumber.transform.localScale = new Vector3(size, size, 1f);
        }

        /// <summary>Where the plaque with the goals hangs, in the layout's points; nothing before the row was hung.</summary>
        public Box? GoalsPlaque { get; private set; }

        /// <summary>Where the moves medallion hangs, in the layout's points; nothing before the row was hung.</summary>
        public Box? MovesMedallion { get; private set; }

        /// <summary>Hangs the row where the layout has it: centred on the screen, as wide and as large as the screen allows.</summary>
        public void Place(ScreenLayout layout)
        {
            float scale = layout.HudScale;
            _root.localPosition = new Vector3(0f, layout.HudMiddle, 0f);
            _root.localScale = new Vector3(scale, scale, 1f);

            // From here on in the row's own points: the medallion keeps its size, the two plaques share what is left.
            float rowWidth = layout.HudWidth / scale;
            float plaqueWidth = (rowWidth - MedallionSize - 2f * GapBetween) / 2f;
            float plaqueMiddle = MedallionSize / 2f + GapBetween + plaqueWidth / 2f;
            _left.Hang(-plaqueMiddle, plaqueWidth);
            _right.Hang(plaqueMiddle, plaqueWidth);
            float goalsMiddle = (_areTheGoalsOnTheLeft ? -plaqueMiddle : plaqueMiddle) * scale;
            GoalsPlaque = Box.Around(goalsMiddle, layout.HudMiddle, plaqueWidth * scale, PlaqueHeight * scale);
            MovesMedallion = Box.Around(0f, layout.HudMiddle, MedallionSize * scale, MedallionSize * scale);

            // Down to the tight phone's row the labels keep their size on the screen. A compact screen draws the
            // row smaller than that; there they shrink with it from that size on, or they would crowd the numbers.
            float keptBy = 1f / Mathf.Max(scale, ScreenLayout.TightChromeScale);
            foreach ((TextMeshPro label, float size) in _labels)
            {
                label.fontSize = size * keptBy;
                label.ForceMeshUpdate();
            }

            _thumb?.Show(softEdge: 1f / (layout.PixelsPerPoint * scale));
            float topOfTheScreen = (layout.ViewHeight / 2f - layout.HudMiddle) / scale;
            _wires.Show(Wires(plaqueMiddle, plaqueWidth, topOfTheScreen));
        }

        public void Dispose()
        {
            _wires.Dispose();
            _goals.Dispose();
            _thumb?.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>
        /// Where a label and what stands under it go when both are centred together on a middle, as a stylesheet
        /// centres a column: the heights of the label's line and of the body's.
        /// </summary>
        /// <param name="roomBelow">Empty room that counts as part of the column, under the body.</param>
        private static (float LabelAt, float BodyAt) Stacked(float middle, float labelSize, float gap, float bodyHeight, float roomBelow = 0f)
        {
            float top = middle + (labelSize + gap + bodyHeight + roomBelow) / 2f;
            return (top - labelSize / 2f, top - labelSize - gap - bodyHeight / 2f);
        }

        private TextMeshPro WriteLabel(string words, Transform parent, ArtCatalogue art, float size, float spacing, float height)
        {
            TextMeshPro label = Lettering.Write(words, parent, art, LetteringStyle.Label, size, Lettering.LabelInk, DrawOrder.PlaqueLettering);
            label.characterSpacing = spacing;
            label.transform.localPosition = new Vector3(0f, height, 0f);
            Lettering.Say(label, words);
            _labels.Add((label, size));
            return label;
        }

        /// <param name="lineMiddle">The middle of the number's line; the digits stand a little under it.</param>
        private static TextMeshPro WriteNumber(string name, Transform parent, ArtCatalogue art, float size, float lineMiddle)
        {
            TextMeshPro number = Lettering.Write(name, parent, art, LetteringStyle.Figure, size, Lettering.Ink, DrawOrder.PlaqueLettering);
            number.transform.localPosition = new Vector3(0f, lineMiddle - Lettering.FigureDrop(LetteringStyle.Figure) * size, 0f);
            return number;
        }

        private void AddMedallion(ArtCatalogue art)
        {
            var medallion = new GameObject("Moves medallion").AddComponent<SpriteRenderer>();
            medallion.transform.SetParent(_root, worldPositionStays: false);
            medallion.sprite = art.Of(GalleryArt.Medallion);
            medallion.sharedMaterial = art.SpriteMaterial;
            medallion.sortingOrder = (int)DrawOrder.Plaque;
        }

        /// <summary>The wires of all three: two to each plaque, one to the medallion, from their top edges up out of the screen.</summary>
        private static Mesh Wires(float plaqueMiddle, float plaqueWidth, float topOfTheScreen)
        {
            var sketch = new MeshSketch();
            float fromMiddleToWire = plaqueWidth / 2f - WireInFromPlaqueSide;
            foreach (float plaque in new[] { -plaqueMiddle, plaqueMiddle })
            {
                PictureWire.Add(sketch, plaque - fromMiddleToWire, PlaqueHeight / 2f, topOfTheScreen);
                PictureWire.Add(sketch, plaque + fromMiddleToWire - PictureWire.Width, PlaqueHeight / 2f, topOfTheScreen);
            }

            PictureWire.Add(sketch, -PictureWire.Width / 2f, MedallionSize / 2f, topOfTheScreen);
            return sketch.ToMesh("Hud wires");
        }

        /// <summary>A plaque and what is written on it: one group, hung at a place along the row.</summary>
        private sealed class Plaque
        {
            private readonly SpriteRenderer _face;

            public Plaque(string name, Transform row, ArtCatalogue art)
            {
                Group = new GameObject(name).transform;
                Group.SetParent(row, worldPositionStays: false);
                _face = new GameObject("Face").AddComponent<SpriteRenderer>();
                _face.transform.SetParent(Group, worldPositionStays: false);
                _face.sprite = art.Of(GalleryArt.Plaque);
                _face.sharedMaterial = art.SpriteMaterial;
                _face.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its rim and corners keep their shape
                _face.sortingOrder = (int)DrawOrder.Plaque;
            }

            public Transform Group { get; }

            public void Hang(float middle, float width)
            {
                Group.localPosition = new Vector3(middle, 0f, 0f);
                _face.size = new Vector2(width + PlaquePictureMargin, PlaquePictureHeight);
            }
        }
    }
}
