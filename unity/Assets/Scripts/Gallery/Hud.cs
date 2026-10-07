#nullable enable
using System;
using System.Globalization;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// What hangs on the wall above the board: the level's plaque (BÖLÜM), the round moves medallion (HAMLE) and the
    /// goals' plaque (HEDEF), each on thin picture wires that run up out of the screen. Sizes and places are the web
    /// test page's HUD (gallery.css) at its full size, in points around the medallion's middle; a screen on
    /// which the board needs the height draws the whole row smaller (<see cref="ScreenLayout.HudScale"/>).
    /// </summary>
    public sealed class Hud : IDisposable
    {
        /// <summary>
        /// The typeface sets figures a little under the middle of their line: this part of the type size. Numbers
        /// are placed by the middle of their digits, so they are moved down by it to stand as a stylesheet sets them.
        /// </summary>
        public const float FigureDrop = 0.045f;

        private const string LevelLabel = "BÖLÜM";
        private const string MovesLabel = "HAMLE";
        private const string GoalsLabel = "HEDEF";

        private const float MedallionSize = ScreenLayout.HudHeight;
        private const float PlaqueHeight = 84f;
        private const float GapBetween = 8f;

        /// <summary>The plaque's picture is its body and the room around it for its rim and shadow: 28 points wider, 118 high.</summary>
        private const float PlaquePictureMargin = 28f;
        private const float PlaquePictureHeight = 118f;

        /// <summary>A plaque's face has 7 points of rim and padding over it and 6 under it, so its middle lies this far above the plaque's.</summary>
        private const float FaceMiddle = 0.5f;

        private const float LabelSize = 10f;

        /// <summary>The labels' capitals stand apart by this many hundredths of their size.</summary>
        private const float LabelSpacing = 9f;
        private const float LevelNumberSize = 31f;
        private const float MovesNumberSize = 40f;

        /// <summary>The room between a label and what stands under it: the level's number, the moves, the goals.</summary>
        private const float GapUnderLevelLabel = 6f;
        private const float GapUnderMovesLabel = 2f;
        private const float GapUnderGoalsLabel = 4f;

        /// <summary>The moves stand this far above the foot of the medallion's dial, more than above its head.</summary>
        private const float RoomUnderMoves = 3f;

        /// <summary>Where a plaque's two wires meet its top edge.</summary>
        private const float WireInFromPlaqueSide = 27f;

        private readonly Transform _root;
        private readonly PaintedShape _wires;
        private readonly Plaque _levelPlaque;
        private readonly Plaque _goalsPlaque;
        private readonly TextMeshPro _movesNumber;
        private readonly TextMeshPro _goalsLabel;

        /// <summary>The three labels. They keep their size on the screen when a phone draws the row smaller, as the web page keeps them.</summary>
        private readonly TextMeshPro[] _labels;
        private readonly GoalRow _goals;

        private Hud(Transform parent, ArtCatalogue art, int levelNumber)
        {
            _root = new GameObject("Hud").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wires = PaintedShape.Create("Wires", _root, art.PaintMaterial, DrawOrder.Wire);

            _levelPlaque = new Plaque("Level plaque", _root, art);
            (float levelLabelAt, float levelNumberAt) = Stacked(FaceMiddle, GapUnderLevelLabel, LevelNumberSize);
            TextMeshPro levelLabel = WriteLabel(LevelLabel, _levelPlaque.Group, art, levelLabelAt);
            TextMeshPro levelFigure = WriteNumber("Level", _levelPlaque.Group, art, LevelNumberSize, levelNumberAt);
            Lettering.Say(levelFigure, levelNumber.ToString(CultureInfo.InvariantCulture));

            AddMedallion(art);
            (float movesLabelAt, float movesNumberAt) = Stacked(0f, GapUnderMovesLabel, MovesNumberSize, RoomUnderMoves);
            TextMeshPro movesLabel = WriteLabel(MovesLabel, _root, art, movesLabelAt);
            _movesNumber = WriteNumber("Moves", _root, art, MovesNumberSize, movesNumberAt);

            _goalsPlaque = new Plaque("Goals plaque", _root, art);
            _goalsLabel = WriteLabel(GoalsLabel, _goalsPlaque.Group, art, 0f);
            _goals = new GoalRow(_goalsPlaque.Group, art);
            _labels = new[] { levelLabel, movesLabel, _goalsLabel };
        }

        public static Hud Create(Transform parent, ArtCatalogue art, int levelNumber) => new Hud(parent, art, levelNumber);

        /// <summary>Shows the numbers of a moment of the game.</summary>
        public void Show(HudFigures figures)
        {
            _movesNumber.color = figures.MovesAreFew ? Lettering.Danger : Lettering.Ink;
            Lettering.Say(_movesNumber, figures.MovesLeft.ToString(CultureInfo.InvariantCulture));

            _goals.Show(figures.Goals);
            (float labelAt, float rowAt) = Stacked(FaceMiddle, GapUnderGoalsLabel, _goals.Height);
            _goalsLabel.transform.localPosition = new Vector3(0f, labelAt, 0f);
            _goals.Transform.localPosition = new Vector3(0f, rowAt, 0f);
        }

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
            _levelPlaque.Hang(-plaqueMiddle, plaqueWidth);
            _goalsPlaque.Hang(plaqueMiddle, plaqueWidth);

            // Down to the tight phone's row the labels keep their size on the screen. A compact screen draws the
            // row smaller than that; there they shrink with it from that size on, or they would crowd the numbers.
            float labelSize = LabelSize / Mathf.Max(scale, ScreenLayout.TightChromeScale);
            foreach (TextMeshPro label in _labels)
            {
                label.fontSize = labelSize;
                label.ForceMeshUpdate();
            }

            float topOfTheScreen = (layout.ViewHeight / 2f - layout.HudMiddle) / scale;
            _wires.Show(Wires(plaqueMiddle, plaqueWidth, topOfTheScreen));
        }

        public void Dispose()
        {
            _wires.Dispose();
            _goals.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>
        /// Where a label and what stands under it go when both are centred together on a middle, as a stylesheet
        /// centres a column: the heights of the label's line and of the body's.
        /// </summary>
        /// <param name="roomBelow">Empty room that counts as part of the column, under the body.</param>
        private static (float LabelAt, float BodyAt) Stacked(float middle, float gap, float bodyHeight, float roomBelow = 0f)
        {
            float top = middle + (LabelSize + gap + bodyHeight + roomBelow) / 2f;
            return (top - LabelSize / 2f, top - LabelSize - gap - bodyHeight / 2f);
        }

        private static TextMeshPro WriteLabel(string words, Transform parent, ArtCatalogue art, float height)
        {
            TextMeshPro label = Lettering.Write(words, parent, art, LetteringStyle.Label, LabelSize, Lettering.SoftInk, DrawOrder.PlaqueLettering);
            label.characterSpacing = LabelSpacing;
            label.transform.localPosition = new Vector3(0f, height, 0f);
            Lettering.Say(label, words);
            return label;
        }

        /// <param name="lineMiddle">The middle of the number's line; the digits stand a little under it.</param>
        private static TextMeshPro WriteNumber(string name, Transform parent, ArtCatalogue art, float size, float lineMiddle)
        {
            TextMeshPro number = Lettering.Write(name, parent, art, LetteringStyle.Figure, size, Lettering.Ink, DrawOrder.PlaqueLettering);
            number.transform.localPosition = new Vector3(0f, lineMiddle - FigureDrop * size, 0f);
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
