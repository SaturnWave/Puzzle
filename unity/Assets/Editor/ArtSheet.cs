#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using Puzzle.Client.Painting;
using Puzzle.Client.Stage;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// A sheet of the gallery's drawings for the Unity Editor's batch mode: every sprite of <see cref="GalleryArt"/>
    /// as the game draws it from its catalogue, at the sizes the design canvas shows it, three times over: on the
    /// wall's teal, on a card's cream and on the shelf's wood. It is the picture to lay beside the canvas' own
    /// after a drawing was added or changed: a sprite's rim, gloss and shadow, and for one that stretches the
    /// borders it stretches from, show here before any screen uses it.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.ArtSheet.Run
    ///            [-sheetPixelsPerPoint 2]
    ///   -sheetPixelsPerPoint  how many pixels a point of the design is drawn with: 2 is the size of the canvas'
    ///                         own pictures (the default), 3 a phone's screen
    /// The picture goes to out/unity/shots/art-sheet-Nx.png, and the log names every drawing with the size its
    /// picture has there, in points. Do not pass -nographics: the picture needs a graphics device.
    /// </summary>
    public static class ArtSheet
    {
        /// <summary>A ground is as wide as the widest showing with a margin at each side: the board's plank, 366 points and 14 of drop at each end.</summary>
        private const float GroundWidth = 420f;
        private const float Margin = 10f;
        private const float GapBetweenDrawings = 12f;
        private const float GapBetweenRows = 8f;
        private const float CaptionSize = 5.5f;
        private const float CaptionHeight = 10f;

        /// <summary>The three grounds the game draws on (the wall's teal, a card's cream, the shelf's wood), each with an ink a caption can be read in.</summary>
        private static readonly Ground[] Grounds =
        {
            new Ground(Tint.Of(0x17666F), Lettering.CreamOnTeal),
            new Ground(Tint.Of(0xFBF0D6), Lettering.SoftInk),
            new Ground(Tint.Of(0x8E5730), Lettering.OnDark),
        };

        /// <summary>
        /// How a drawing is shown where its own picture is not its size on a screen: a mark drawn in a box of 100
        /// points at the sizes the canvas gives it, a surface that stretches at the sizes the canvas stretches it
        /// to. A drawing this does not name is shown once, as it is drawn.
        /// </summary>
        private static readonly IReadOnlyDictionary<GalleryArt, Showing[]> Showings = new Dictionary<GalleryArt, Showing[]>
        {
            // The navigation bar (HomeTR): 58 points where chosen, 46 elsewhere.
            [GalleryArt.IconMuseum] = new[] { Showing.Across(58f), Showing.Across(46f) },
            [GalleryArt.IconMap] = new[] { Showing.Across(58f), Showing.Across(46f) },
            [GalleryArt.IconCollection] = new[] { Showing.Across(58f), Showing.Across(46f) },
            [GalleryArt.IconShop] = new[] { Showing.Across(58f), Showing.Across(46f) },
            [GalleryArt.IconLock] = new[] { Showing.Across(19f) },
            [GalleryArt.IconPiece] = new[] { Showing.Across(40f) },

            // The wallet (HomeTR), a price on a button (ShopTR, LoseTR), the life a give-up costs (LoseTR).
            [GalleryArt.IconCoin] = new[] { Showing.Across(46f), Showing.Across(23f), Showing.Across(20f) },
            [GalleryArt.IconHeart] = new[] { Showing.Across(46f), Showing.Across(20f) },
            [GalleryArt.IconGear] = new[] { Showing.Across(27f) },
            [GalleryArt.GiftPicture] = new[] { Showing.Across(50f) },
            [GalleryArt.IconBack] = new[] { Showing.Across(27f) },

            // The boosters: 48 points on the board's shelf (BoardTR), 54 before a level (LevelStartTR), 36 in the shop (ShopTR).
            [GalleryArt.IconPause] = new[] { Showing.Across(26f) },
            [GalleryArt.IconSpatula] = new[] { Showing.Across(48f), Showing.Across(36f) },
            [GalleryArt.IconShuffle] = new[] { Showing.Across(48f), Showing.Across(36f) },
            [GalleryArt.IconThreeMoves] = new[] { Showing.Across(54f), Showing.Across(36f) },
            [GalleryArt.IconFiveMoves] = new[] { Showing.Across(154f) },
            [GalleryArt.CoinPile] = new[] { Showing.Across(54f), Showing.Across(46f) },
            [GalleryArt.CoinPouch] = new[] { Showing.Across(62f) },
            [GalleryArt.CoinChest] = new[] { Showing.Across(80f) },

            // The marks of the settings' rows (SettingsTR), 21 points on a disc of 32.
            [GalleryArt.IconSound] = new[] { Showing.Across(21f) },
            [GalleryArt.IconMusic] = new[] { Showing.Across(21f) },
            [GalleryArt.IconBuzz] = new[] { Showing.Across(21f) },
            [GalleryArt.IconGlobe] = new[] { Showing.Across(21f) },
            [GalleryArt.IconShield] = new[] { Showing.Across(21f) },
            [GalleryArt.IconHelp] = new[] { Showing.Across(21f) },
            [GalleryArt.IconStatistics] = new[] { Showing.Across(21f) },

            // The discs: 60 on the board's shelf, 66 before a level with the ring of a chosen one, 44 in the shop; the pause disc is 46.
            [GalleryArt.CreamDisc] = new[] { Showing.AsDrawn, Showing.At(66f / 60f), Showing.At(44f / 60f) },
            [GalleryArt.ChosenRing] = new[] { Showing.At(66f / 60f) },
            [GalleryArt.TealDisc] = new[] { Showing.AsDrawn, Showing.At(46f / 44f) },
            [GalleryArt.TealBadge] = new[] { Showing.AsDrawn, Showing.WithBody(new Vector2(22f, 22f), new Vector2(30f, 22f)) },

            // What stretches, at the sizes the canvas has it.
            [GalleryArt.PricePill] = new[] { Showing.WithBody(new Vector2(44f, 21f), new Vector2(81f, 21f)) },
            [GalleryArt.SmallFrame] = new[] { Showing.AsDrawn, Showing.WithBody(new Vector2(60f, 60f), new Vector2(100f, 78f)) },
            [GalleryArt.Velvet] = new[] { Showing.SqueezedTo(50f, 50f), Showing.SqueezedTo(90f, 68f) },
            [GalleryArt.Plank] = new[]
            {
                Showing.WithBody(new Vector2(100f, 80f), new Vector2(366f, 78f)),
                Showing.WithBody(new Vector2(100f, 80f), new Vector2(328f, 88f)),
                Showing.WithBody(new Vector2(100f, 80f), new Vector2(220f, 58f), scale: 0.75f),
            },
            [GalleryArt.ChipWell] = new[] { Showing.WithBody(new Vector2(56f, 36f), new Vector2(240f, 36f)) },
            [GalleryArt.Chip] = new[] { Showing.WithBody(new Vector2(56f, 28f), new Vector2(151f, 28f)) },
            [GalleryArt.BrassPlate] = new[]
            {
                Showing.WithBody(new Vector2(40f, 40f), new Vector2(176f, 40f)),
                Showing.WithBody(new Vector2(40f, 40f), new Vector2(212f, 50f)),
                Showing.WithBody(new Vector2(40f, 40f), new Vector2(280f, 40f)),
            },
            [GalleryArt.WalletPill] = new[]
            {
                Showing.WithBody(new Vector2(68f, 36f), new Vector2(100f, 36f)),
                Showing.WithBody(new Vector2(68f, 36f), new Vector2(86f, 36f)),
            },
        };

        public static void Run()
        {
            try
            {
                int pixelsPerPoint = PixelsPerPointFrom(Environment.GetCommandLineArgs());
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                // A scene of its own that is never saved.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Transform sheet = new GameObject("Art sheet").transform;
                TextMeshPro ruler = Lettering.Write("Ruler", sheet, art, LetteringStyle.Label, CaptionSize, Color.clear, DrawOrder.PlaqueLettering);
                IReadOnlyList<Cell> cells = Lay(art, ruler, out float height);

                using PaintedShape grounds = PaintedShape.Create("Grounds", sheet, art.PaintMaterial, DrawOrder.WallPaint);
                var sketch = new MeshSketch();
                for (int index = 0; index < Grounds.Length; index++)
                {
                    float left = index * GroundWidth;
                    sketch.AddRectangle(new Rect(left, -height, GroundWidth, height), Grounds[index].Colour);
                    Draw(cells, sheet, art, left, Grounds[index]);
                }

                grounds.Show(sketch.ToMesh("Grounds"));

                float width = Grounds.Length * GroundWidth;
                Camera camera = StageCamera.Add(sheet);
                camera.orthographicSize = height / 2f;
                camera.transform.localPosition = new Vector3(width / 2f, -height / 2f, camera.transform.localPosition.z);
                string file = $"art-sheet-{pixelsPerPoint}x.png";
                CameraPicture.Write(camera, Mathf.RoundToInt(width * pixelsPerPoint), Mathf.RoundToInt(height * pixelsPerPoint), Path.Combine(StoryStudio.ShotsFolder, file));
                foreach (Cell cell in cells)
                {
                    Debug.Log($"Art sheet: {cell.Caption}");
                }

                Debug.Log($"Art sheet: {cells.Count} showings of {Enum.GetValues(typeof(GalleryArt)).Length} drawings on {Grounds.Length} grounds are in {StoryStudio.ShotsFolder}/{file}.");
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException || failure is IOException)
            {
                Debug.LogError($"Art sheet failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <exception cref="ArgumentException">The argument is not a whole number from 1 to 4.</exception>
        private static int PixelsPerPointFrom(string[] arguments)
        {
            int at = Array.IndexOf(arguments, "-sheetPixelsPerPoint");
            if (at < 0)
            {
                return 2;
            }

            if (at + 1 < arguments.Length && int.TryParse(arguments[at + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int pixels) && pixels >= 1 && pixels <= 4)
            {
                return pixels;
            }

            throw new ArgumentException("-sheetPixelsPerPoint needs a whole number from 1 to 4.");
        }

        /// <summary>
        /// Gives every showing of every drawing its place on a ground, in the order of the list of drawings: side
        /// by side from the ground's top left corner, a new row where the next would run over the ground's edge.
        /// The places are the same on each ground.
        /// </summary>
        /// <param name="height">The height of a ground, in points.</param>
        private static IReadOnlyList<Cell> Lay(ArtCatalogue art, TextMeshPro ruler, out float height)
        {
            var cells = new List<Cell>();
            float left = Margin;
            float top = Margin;
            float rowHeight = 0f;
            foreach (GalleryArt drawing in (GalleryArt[])Enum.GetValues(typeof(GalleryArt)))
            {
                Sprite sprite = art.Of(drawing);
                foreach (Showing showing in Showings.TryGetValue(drawing, out Showing[] named) ? named : new[] { Showing.AsDrawn })
                {
                    Vector2 size = showing.SizeOf(sprite);
                    string caption = $"{drawing} {Points(size.x)}×{Points(size.y)}";
                    float width = Mathf.Max(size.x, Lettering.WidthOf(ruler, caption));
                    if (left + width > GroundWidth - Margin && left > Margin)
                    {
                        top += rowHeight + GapBetweenRows;
                        left = Margin;
                        rowHeight = 0f;
                    }

                    cells.Add(new Cell(sprite, showing, new Rect(left, top, width, size.y), caption));
                    rowHeight = Mathf.Max(rowHeight, size.y + CaptionHeight);
                    left += width + GapBetweenDrawings;
                }
            }

            height = top + rowHeight + Margin;
            return cells;
        }

        /// <summary>Draws every cell on one ground: the sprite in the middle of its cell's width, its caption under it.</summary>
        private static void Draw(IReadOnlyList<Cell> cells, Transform sheet, ArtCatalogue art, float groundLeft, Ground ground)
        {
            foreach (Cell cell in cells)
            {
                SpriteRenderer drawn = SceneObjects.AddSprite(cell.Caption, sheet, cell.Sprite, art.SpriteMaterial, DrawOrder.Plaque);
                Vector2 size = cell.Showing.Show(drawn);

                // A sprite is placed by its pivot, which lies this share of its picture from the left and from the foot.
                Vector2 pivot = cell.Sprite.pivot / cell.Sprite.rect.size;
                float pictureLeft = groundLeft + cell.Place.x + (cell.Place.width - size.x) / 2f;
                drawn.transform.localPosition = new Vector3(pictureLeft + pivot.x * size.x, -(cell.Place.y + (1f - pivot.y) * size.y), 0f);

                TextMeshPro caption = Lettering.Write("Caption", sheet, art, LetteringStyle.Label, CaptionSize, ground.Ink, DrawOrder.PlaqueLettering);
                Lettering.SayFromLeft(caption, cell.Caption, groundLeft + cell.Place.x, -(cell.Place.yMax + CaptionHeight / 2f));
            }
        }

        private static string Points(float length) => length.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>A ground of the sheet and the ink captions are written in on it.</summary>
        private readonly struct Ground
        {
            public Ground(Color colour, Color ink)
            {
                Colour = colour;
                Ink = ink;
            }

            public Color Colour { get; }

            public Color Ink { get; }
        }

        /// <summary>One showing of a drawing where it lies on a ground: from the ground's top left corner, in points, y downwards.</summary>
        private readonly struct Cell
        {
            public Cell(Sprite sprite, Showing showing, Rect place, string caption)
            {
                Sprite = sprite;
                Showing = showing;
                Place = place;
                Caption = caption;
            }

            public Sprite Sprite { get; }

            public Showing Showing { get; }

            /// <summary>As wide as the wider of picture and caption, as high as the picture.</summary>
            public Rect Place { get; }

            public string Caption { get; }
        }

        /// <summary>
        /// How a sprite is drawn on the sheet: its picture made larger around its middle as a stretching sprite
        /// is, then the whole scaled, evenly or each way on its own.
        /// </summary>
        private readonly struct Showing
        {
            /// <summary>Added to the picture's own size, in the picture's points; nothing for a sprite that is not stretched.</summary>
            private readonly Vector2 _grownBy;

            /// <summary>The width the picture is scaled to, its height following; absent when the scale is stated another way.</summary>
            private readonly float? _across;

            /// <summary>The width and the height the picture is scaled to, each on its own; absent when the picture keeps its shape.</summary>
            private readonly Vector2? _squeezedTo;

            private readonly float _scale;

            private Showing(Vector2 grownBy, float? across, Vector2? squeezedTo, float scale)
            {
                _grownBy = grownBy;
                _across = across;
                _squeezedTo = squeezedTo;
                _scale = scale;
            }

            /// <summary>The picture as its source draws it: one unit of it is one point.</summary>
            public static Showing AsDrawn => new Showing(Vector2.zero, across: null, squeezedTo: null, scale: 1f);

            /// <summary>The whole picture at this width: a mark drawn in a box of 100 at the size a place gives it.</summary>
            public static Showing Across(float points) => new Showing(Vector2.zero, points, squeezedTo: null, scale: 1f);

            public static Showing At(float scale) => new Showing(Vector2.zero, across: null, squeezedTo: null, scale);

            /// <summary>The whole picture at this width and this height, whatever its own shape.</summary>
            public static Showing SqueezedTo(float width, float height) => new Showing(Vector2.zero, across: null, new Vector2(width, height), scale: 1f);

            /// <summary>
            /// A sprite that stretches, with the body its source draws <paramref name="drawn"/> large shown
            /// <paramref name="shown"/> large; at a scale other than 1 its corners and rim are that much smaller.
            /// </summary>
            public static Showing WithBody(Vector2 drawn, Vector2 shown, float scale = 1f) => new Showing(shown / scale - drawn, across: null, squeezedTo: null, scale);

            /// <summary>How large the sprite's picture is when shown this way, in points.</summary>
            public Vector2 SizeOf(Sprite sprite) => Vector2.Scale(PictureOf(sprite), ScaleOf(sprite));

            /// <summary>Draws the sprite this way.</summary>
            /// <returns>The size of its picture, in points.</returns>
            public Vector2 Show(SpriteRenderer drawn)
            {
                if (_grownBy != Vector2.zero)
                {
                    drawn.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its corners and rim keep their shape
                    drawn.size = PictureOf(drawn.sprite);
                }

                Vector2 scale = ScaleOf(drawn.sprite);
                drawn.transform.localScale = new Vector3(scale.x, scale.y, 1f);
                return SizeOf(drawn.sprite);
            }

            /// <summary>The picture's size in its own points, before any scale.</summary>
            private Vector2 PictureOf(Sprite sprite) => (Vector2)sprite.bounds.size + _grownBy;

            private Vector2 ScaleOf(Sprite sprite)
            {
                Vector2 picture = PictureOf(sprite);
                if (_squeezedTo is Vector2 size)
                {
                    return new Vector2(size.x / picture.x, size.y / picture.y);
                }

                float even = _across is float points ? points / picture.x : _scale;
                return new Vector2(even, even);
            }
        }
    }
}
