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
    /// A specimen of the game's lettering for the Unity Editor's batch mode: every style on the ground it is meant
    /// for, saying the design canvas' own words at the canvas' sizes, and after them the letters Turkish adds to
    /// plain Latin. The sheet is as wide as the canvas (390 points), so its lines can be laid beside the same
    /// words in the canvas' pictures.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.LetteringSheet.Run
    ///            [-sheetPixelsPerPoint 3]
    ///   -sheetPixelsPerPoint  how many pixels a point of the design is drawn with: 3 is a phone's screen (the
    ///                         default), 2 the size of the canvas' own pictures
    /// The picture goes to out/unity/shots/lettering-sheet-Nx.png, and the log tells how wide each line is in
    /// points: the measure a label's room is checked against. Do not pass -nographics: the picture needs a
    /// graphics device.
    /// </summary>
    public static class LetteringSheet
    {
        private const float SheetWidth = 390f;
        private const float Margin = 10f;
        private const float CaptionSize = 7f;
        private const float CaptionHeight = 13f;
        private const float GapBetweenSamples = 14f;
        private const float GapBetweenStyles = 6f;

        /// <summary>A row of samples is this share of its largest type size high: room for accents above and tails below.</summary>
        private const float RowOfTypeSize = 1.7f;

        /// <summary>What Turkish adds to plain Latin, in both cases, with the marks the game's sentences use.</summary>
        private const string TurkishLetters = "ÇçĞğİıÖöŞşÜü ÂâÎîÛû ·…’“”–×";

        private static readonly Color Paper = Tint.Of(0xE9DCC0);
        private static readonly Color Cream = Tint.Of(0xFBF0D6);
        private static readonly Color Brass = Tint.Of(0xF6B93C);
        private static readonly Color Green = Tint.Of(0x4FBE3A);
        private static readonly Color Blue = Tint.Of(0x3C74EA);
        private static readonly Color Teal = Tint.Of(0x1D8890);
        private static readonly Color Red = Tint.Of(0xF4472F);
        private static readonly Color DarkGreen = Tint.Of(0x1D6B26);
        private static readonly Color DarkTeal = Tint.Of(0x0F5560);
        private static readonly Color Wood = Tint.Of(0x8E5730);

        /// <summary>Each style on its ground, with the words the design canvas sets in it and their sizes there.</summary>
        private static readonly Specimen[] Specimens =
        {
            new Specimen(LetteringStyle.Figure, Cream, Lettering.Ink, typicalSize: 24f,
                new Sample("14", 40f), new Sample("12", 24f), new Sample("16", 24f), new Sample("7 / 10", 31f), new Sample("7 / 10", 23f),
                new Sample("1.250", 16.5f), new Sample("Dolu", 15.5f), new Sample("+45", 19f), new Sample("0123456789", 19f)),
            new Specimen(LetteringStyle.Figure, Red, Color.white, typicalSize: 14f, new Sample("1", 14f), new Sample("3", 12f), new Sample("+", 15f)),
            new Specimen(LetteringStyle.Label, Cream, Lettering.LabelInk, typicalSize: 10f,
                new Sample("BÖLÜM 47", 10f, 9f), new Sample("HAMLE", 10f, 9f), new Sample("PARÇA 7 / 10", 9.5f, 7f), new Sample("ODA 1 / 20", 9f, 9f),
                new Sample("4 / 5 ONARILDI", 9f, 8f), new Sample("CLAUDE MONET · 1890–91", 8.5f, 12f)),
            new Specimen(LetteringStyle.Label, Cream, Lettering.Ink, typicalSize: 13f,
                new Sample("Yeni parça:", 17f), new Sample("Parçayı yerine sürükle", 13f), new Sample("Buradasın · Bölüm 47", 11.5f),
                new Sample("Bölüm 51'de açılır", 11f), new Sample("Hediye", 11f), new Sample("3g 12s", 11f)),
            new Specimen(LetteringStyle.Soft, Cream, Lettering.SoftInk, typicalSize: 12.5f,
                new Sample("Kalan 3 hamle", 12.5f), new Sample("fırçaya dönüştü", 12.5f), new Sample("1 can gider", 12.5f), new Sample("Türkçe", 14.5f)),
            // The serif's figures are its lining ones, as high as its capitals ("10." must not read as "ıo."): they are shown where the game sets them.
            new Specimen(LetteringStyle.Title, Brass, Lettering.BrassInk, typicalSize: 16f,
                new Sample("Bölüm tamamlandı!", 20.5f), new Sample("Hamle bitti!", 20.5f), new Sample("Bölüm 47", 22f),
                new Sample("Buğday Yığınları (Yaz Sonu)", 11f), new Sample("10. tablo", 11f), new Sample("0123456789", 11f)),
            new Specimen(LetteringStyle.Title, Cream, Lettering.Ink, typicalSize: 16f,
                new Sample("Empresyonizm", 16f), new Sample("Müze Haritası", 16f), new Sample("Koleksiyon", 16f), new Sample("Post-empresyonizm", 16f),
                new Sample("Bölüm 51'de açılır", 13.5f)),
            new Specimen(LetteringStyle.OnButton, Green, Color.white, typicalSize: 20f,
                new Sample("Bölüm 47", 29f), new Sample("Devam", 28f), new Sample("Oyna", 34f), new Sample("+5 hamle", 20f)),
            new Specimen(LetteringStyle.OnBlue, Blue, Color.white, typicalSize: 18f,
                new Sample("Tablo", 19f), new Sample("Parçayı", 17.5f), new Sample("yerleştir", 17.5f), new Sample("Otomatik tamamla", 18f), new Sample("Reklam izle", 17f)),
            new Specimen(LetteringStyle.OnTeal, Teal, Color.white, typicalSize: 14.5f,
                new Sample("Müze", 14.5f), new Sample("Harita", 14.5f), new Sample("Koleksiyon", 13f), new Sample("Mağaza", 14.5f), new Sample("1", 18f), new Sample("20", 18f)),
            new Specimen(LetteringStyle.OnHeart, Red, Color.white, typicalSize: 18f, new Sample("5", 18f), new Sample("0", 18f)),
            new Specimen(LetteringStyle.OnPricePill, DarkGreen, Color.white, typicalSize: 13.5f, new Sample("[fiyat]", 13.5f), new Sample("900", 13.5f)),
            new Specimen(LetteringStyle.OnDarkTeal, DarkTeal, Lettering.CreamOnTeal, typicalSize: 11.5f, new Sample("Oda 2", 11.5f), new Sample("Oda 3", 11.5f)),
            new Specimen(LetteringStyle.OnWood, Wood, Lettering.OnDark, typicalSize: 13f,
                new Sample("Sürükle ya da iki komşu taşa sırayla dokun", 13f), new Sample("2. deneme · taşlar yeniden dizildi", 13f)),
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
                Transform sheet = new GameObject("Lettering sheet").transform;
                using PaintedShape grounds = PaintedShape.Create("Grounds", sheet, art.PaintMaterial, DrawOrder.Plaque);
                float height = Lay(sheet, grounds, art);

                Camera camera = StageCamera.Add(sheet);
                camera.backgroundColor = Paper;
                camera.orthographicSize = height / 2f;
                camera.transform.localPosition = new Vector3(SheetWidth / 2f, -height / 2f, camera.transform.localPosition.z);
                string file = $"lettering-sheet-{pixelsPerPoint}x.png";
                CameraPicture.Write(camera, Mathf.RoundToInt(SheetWidth * pixelsPerPoint), Mathf.RoundToInt(height * pixelsPerPoint), Path.Combine(StoryStudio.ShotsFolder, file));
                Debug.Log($"Lettering sheet: {Specimens.Length} specimens of {Enum.GetValues(typeof(LetteringStyle)).Length} styles are in {StoryStudio.ShotsFolder}/{file}.");
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException || failure is IOException)
            {
                Debug.LogError($"Lettering sheet failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <exception cref="ArgumentException">The argument is not a whole number from 1 to 8.</exception>
        private static int PixelsPerPointFrom(string[] arguments)
        {
            int at = Array.IndexOf(arguments, "-sheetPixelsPerPoint");
            if (at < 0)
            {
                return 3;
            }

            if (at + 1 < arguments.Length && int.TryParse(arguments[at + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int pixels) && pixels >= 1 && pixels <= 8)
            {
                return pixels;
            }

            throw new ArgumentException("-sheetPixelsPerPoint needs a whole number from 1 to 8.");
        }

        /// <summary>
        /// Lays every specimen under the one before it, from the sheet's top left corner down: the sheet's points
        /// run to the right and downwards from there.
        /// </summary>
        /// <returns>The height of the sheet, in points.</returns>
        private static float Lay(Transform sheet, PaintedShape grounds, ArtCatalogue art)
        {
            var sketch = new MeshSketch();
            float top = Margin;
            foreach (Specimen specimen in Specimens)
            {
                TextMeshPro caption = Lettering.Write("Caption", sheet, art, LetteringStyle.Label, CaptionSize, Lettering.SoftInk, DrawOrder.PlaqueLettering);
                Lettering.SayFromLeft(caption, $"{specimen.Style} · {Typefaces.Of(specimen.Style)}", Margin, -(top + CaptionHeight / 2f));
                top += CaptionHeight;

                float groundTop = top;
                var samples = new List<Sample>(specimen.Samples) { new Sample(TurkishLetters, specimen.TypicalSize) };
                top = LayInRows(samples, specimen, sheet, art, top);
                sketch.AddRectangle(new Rect(Margin / 2f, -top, SheetWidth - Margin, top - groundTop), specimen.Ground);
                top += GapBetweenStyles;
            }

            grounds.Show(sketch.ToMesh("Grounds"));
            return top + Margin - GapBetweenStyles;
        }

        /// <summary>Writes samples side by side, beginning a new row where the next would run over the sheet's edge.</summary>
        /// <returns>The foot of the last row.</returns>
        private static float LayInRows(IReadOnlyList<Sample> samples, Specimen specimen, Transform sheet, ArtCatalogue art, float top)
        {
            float left = Margin;
            float rowHeight = 0f;
            foreach (Sample sample in samples)
            {
                TextMeshPro line = Lettering.Write(sample.Words, sheet, art, specimen.Style, sample.Size, specimen.Ink, DrawOrder.PlaqueLettering);
                line.characterSpacing = sample.Spacing;
                Lettering.Say(line, sample.Words);
                float width = Lettering.WidthOf(line, sample.Words);
                if (left + width > SheetWidth - Margin && left > Margin)
                {
                    top += rowHeight;
                    left = Margin;
                    rowHeight = 0f;
                }

                rowHeight = Mathf.Max(rowHeight, RowOfTypeSize * sample.Size);
                line.transform.localPosition = new Vector3(left + width / 2f, -(top + RowOfTypeSize * sample.Size / 2f), 0f);
                left += width + GapBetweenSamples;
                Debug.Log(
                    $"Lettering sheet: {specimen.Style} at {sample.Size.ToString("0.#", CultureInfo.InvariantCulture)} says \"{sample.Words}\" "
                    + $"{width.ToString("0.0", CultureInfo.InvariantCulture)} points wide.");
            }

            return top + rowHeight;
        }

        /// <summary>Words at a type size, their letters apart by so many hundredths of it.</summary>
        private readonly struct Sample
        {
            public Sample(string words, float size, float spacing = 0f)
            {
                Words = words;
                Size = size;
                Spacing = spacing;
            }

            public string Words { get; }

            public float Size { get; }

            public float Spacing { get; }
        }

        /// <summary>A style on a ground it is meant for, in an ink, with what it says there.</summary>
        private readonly struct Specimen
        {
            /// <param name="typicalSize">The size the Turkish letters are shown at.</param>
            public Specimen(LetteringStyle style, Color ground, Color ink, float typicalSize, params Sample[] samples)
            {
                Style = style;
                Ground = ground;
                Ink = ink;
                TypicalSize = typicalSize;
                Samples = samples;
            }

            public LetteringStyle Style { get; }

            public Color Ground { get; }

            public Color Ink { get; }

            public float TypicalSize { get; }

            public IReadOnlyList<Sample> Samples { get; }
        }
    }
}
