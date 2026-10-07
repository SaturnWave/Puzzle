#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using Puzzle.Museum.Jigsaw;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The museum from the command line, before it has screens:
    /// it prints a player's museum after a number of levels won (rooms, paintings, pieces), checks that every painting
    /// the catalogue names is in the game and lies the way the catalogue says, checks that the jigsaw cutter cuts
    /// inside Unity exactly as tests/fixtures/jigsaw-cuts.txt pins it for the tools, and paints each layout's cut as
    /// a picture: the painting with its cut lines, and beside it the pieces pulled apart.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.MuseumReport.Run
    ///        [-museumLevelsWon 47] [-museumPlaced 12]
    /// -museumPlaced puts that many of the won pieces in place, in the museum's order; the rest wait on the trays.
    /// The text also goes to out/unity/museum-report.txt, the pictures to out/unity/shots/jigsaw-cut-LAYOUT-WAY.png.
    /// </summary>
    public static class MuseumReport
    {
        private const string ReportPath = "../out/unity/museum-report.txt";
        private const string PinnedCutsPath = "../tests/fixtures/jigsaw-cuts.txt";
        private const string ShotsFolder = "../out/unity/shots";

        /// <summary>A picture smaller than this on its longer side cannot fill a phone screen: it is a stand-in for the real file.</summary>
        private const int SmallestRealPicture = 1000;

        public static void Run()
        {
            try
            {
                MuseumProgress progress = ProgressFrom(Environment.GetCommandLineArgs());
                MuseumCatalogue catalogue = MuseumPack.LoadPacked();

                var lines = new List<string>(MuseumSummary.Of(progress, catalogue));
                lines.Add("Pictures:");
                lines.AddRange(CheckedPictures(catalogue));
                lines.Add("Jigsaw cuts, as pinned for the tools:");
                lines.AddRange(CheckedCuts());
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllLines(ReportPath, lines);
                Debug.Log(string.Join("\n", lines));

                int pictures = PaintTheCuts(catalogue);
                Debug.Log($"Museum report: the museum after {progress.LevelsWon} levels is in {ReportPath}, every picture is in the game, "
                    + $"the cutter cuts as pinned, and {pictures} cuts are painted in {ShotsFolder}.");
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException
                || failure is IOException || failure is CatalogueFormatException)
            {
                Debug.LogError($"Museum report failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <exception cref="ArgumentException">A switch has no whole number after it, or more pieces are to be placed than the levels give.</exception>
        private static MuseumProgress ProgressFrom(string[] arguments)
        {
            var progress = new MuseumProgress(NumberAfter("-museumLevelsWon", arguments), PlacedPieces.None);
            int toPlace = NumberAfter("-museumPlaced", arguments);
            if (toPlace > progress.PiecesWon)
            {
                throw new ArgumentException($"{progress.LevelsWon} levels won give {progress.PiecesWon} pieces; {toPlace} cannot be in place.");
            }

            foreach (PaintingNumber painting in PaintingNumber.All)
            {
                foreach (int piece in progress.WaitingIn(painting).Take(toPlace - progress.PiecesPlaced))
                {
                    progress = progress.WithPiecePlaced(painting, piece);
                }
            }

            return progress;
        }

        /// <summary>The whole number that follows a switch; 0 when the switch is not given.</summary>
        private static int NumberAfter(string name, string[] arguments)
        {
            int at = Array.IndexOf(arguments, name);
            if (at < 0)
            {
                return 0;
            }

            return at + 1 < arguments.Length && int.TryParse(arguments[at + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                ? number
                : throw new ArgumentException($"{name} needs a whole number.");
        }

        /// <summary>One line for every painting the catalogue names: its picture's size as the game loads it.</summary>
        /// <exception cref="InvalidOperationException">A picture is not in the game, or lies the other way than the catalogue says.</exception>
        private static IEnumerable<string> CheckedPictures(MuseumCatalogue catalogue)
        {
            var lines = new List<string>();
            foreach (Artwork painting in catalogue.Rooms.SelectMany(room => room.Artworks))
            {
                Texture2D? picture = Resources.Load<Texture2D>(MuseumPack.PictureResourceOf(painting));
                if (picture == null)
                {
                    throw new InvalidOperationException($"The picture of \"{painting.Id}\" is not in the game: {PaintingImport.PaintingsFolder}/{painting.ImageFile} is missing.");
                }

                (int width, int height) = (picture.width, picture.height);
                Resources.UnloadAsset(picture);
                if (OrientationOf(width, height) != painting.Orientation)
                {
                    throw new InvalidOperationException($"The picture of \"{painting.Id}\" is {width}x{height}, but the catalogue says the painting hangs {painting.Orientation}.");
                }

                string standIn = Math.Max(width, height) < SmallestRealPicture ? " (a stand-in: too small for a screen)" : string.Empty;
                lines.Add($"  {painting.ImageFile}: {width}x{height}{standIn}");
            }

            return lines;
        }

        private static PaintingOrientation OrientationOf(int width, int height) =>
            width >= height ? PaintingOrientation.Landscape : PaintingOrientation.Portrait;

        /// <exception cref="InvalidOperationException">Unity's runtime cuts a picture differently than the tools' runtime.</exception>
        private static string[] CheckedCuts()
        {
            string[] cut = JigsawTrace.Lines();
            string[] pinned = File.ReadAllLines(PinnedCutsPath);
            if (!cut.SequenceEqual(pinned))
            {
                throw new InvalidOperationException($"The jigsaw cutter cuts differently inside Unity; compare these lines with {PinnedCutsPath}:\n{string.Join("\n", cut)}");
            }

            return cut;
        }

        /// <summary>
        /// Paints every size's cut both ways up, on the first room's last painting of each orientation and with that
        /// painting's own seed; so the ten-piece pictures show those paintings as the game will cut them.
        /// </summary>
        /// <returns>How many pictures were written.</returns>
        private static int PaintTheCuts(MuseumCatalogue catalogue)
        {
            int written = 0;
            IReadOnlyList<Artwork> firstRoom = catalogue.Rooms[0].Artworks;
            foreach (PaintingOrientation orientation in new[] { PaintingOrientation.Landscape, PaintingOrientation.Portrait })
            {
                Artwork painting = firstRoom.LastOrDefault(candidate => candidate.Orientation == orientation)
                    ?? throw new InvalidOperationException($"The first room has no {orientation} painting to show a cut on.");
                Texture2D picture = ReadPicture(painting);
                try
                {
                    foreach (JigsawSize size in JigsawSize.All)
                    {
                        string file = $"jigsaw-cut-{size.Name}-{orientation.ToString().ToLowerInvariant()}.png";
                        Texture2D study = CutStudy.Paint(picture, painting.Id.CutSeed, size, orientation);
                        try
                        {
                            CameraPicture.WritePng(study, Path.Combine(ShotsFolder, file));
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(study);
                        }

                        Debug.Log($"Museum report: {file}, {size} of \"{painting.Id}\"");
                        written++;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(picture);
                }
            }

            return written;
        }

        /// <summary>The painting's file read as it is, whatever the import made of it: the study needs its pixels.</summary>
        /// <exception cref="InvalidOperationException">The file is not a picture.</exception>
        private static Texture2D ReadPicture(Artwork painting)
        {
            string path = $"{PaintingImport.PaintingsFolder}/{painting.ImageFile}";
            var picture = new Texture2D(2, 2, TextureFormat.RGB24, mipChain: false);
            if (!picture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(picture);
                throw new InvalidOperationException($"{path} cannot be read as a picture.");
            }

            picture.wrapMode = TextureWrapMode.Clamp;
            return picture;
        }
    }
}
