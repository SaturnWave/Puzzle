#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Core.Levels;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>A screen of the game that the design canvas draws, by the name the pair command knows it by. A closed set; a screen is added here when the game gets it.</summary>
    internal enum PairedScreen
    {
        /// <summary>The museum's hall (HomeTR).</summary>
        Home,

        /// <summary>The floor plan (MapTR).</summary>
        Map,

        /// <summary>A room's wall in the collection (CollectionTR).</summary>
        Collection,

        /// <summary>A painting's jigsaw at rest (JigsawTR).</summary>
        Jigsaw,

        /// <summary>The board of the level the player stands on, at rest (BoardTR).</summary>
        Board,

        /// <summary>The card of that level won (WinTR).</summary>
        Won,

        /// <summary>The card of that level lost (LoseTR).</summary>
        Lost,
    }

    /// <summary>
    /// A screen of the game beside its drawing, for the Unity Editor's batch mode, with no game running: the
    /// design canvas' own picture of the screen at the left, scaled to the same height, and at the right what the
    /// game's camera shows of the same screen on the phone the canvas is drawn for.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.DesignPairs.Run
    ///            [-pairScreens home,map,collection,jigsaw,board,won,lost] [-pairState 46+45] [-pairDrawings ../out/design/next/project]
    ///   -pairScreens   which screens (all of them when left out)
    ///   -pairState     the player, as the picture command is told of one: levels won, or levels won + pieces in
    ///                  place. Left out, it is the player the canvas draws: on level 47, with the fifth painting's
    ///                  sixth piece waiting to be placed
    ///   -pairDrawings  the folder of the canvas' pictures (HomeTR.png and its fellows); they are not part of
    ///                  the repository, so a machine without them names what is missing and writes nothing
    /// The pictures go to out/unity/shots/pair-&lt;screen&gt;-1080x2340.png. A pair compares what is built, how it is
    /// proportioned and how it is lettered; the canvas' numbers are samples, and a real level has other goals.
    /// Do not pass -nographics: the pictures need a graphics device.
    /// </summary>
    public static class DesignPairs
    {
        private const string DrawingsFolder = "../out/design/next/project";

        /// <summary>The canvas draws a phone of 390 by 844 points; this is the game's screen of that shape.</summary>
        private static readonly ScreenShape Screen = new ScreenShape(1080, 2340);

        /// <summary>The player of the canvas: 46 levels won, four paintings whole and five pieces of the fifth in place, its sixth waiting.</summary>
        private const string PlayerOfTheCanvas = "46+45";

        /// <summary>The two pictures stand this many pixels apart, on the sheet's own dark.</summary>
        private const int Gap = 24;
        private static readonly Color32 Paper = new Color32(0x12, 0x1A, 0x1C, 0xFF);

        public static void Run()
        {
            try
            {
                string[] arguments = Environment.GetCommandLineArgs();
                PairedScreen[] screens = After("-pairScreens", arguments, AllScreens()).Split(',').Select(ScreenNamed).ToArray();
                PlayerState state = PlayerState.Parse(After("-pairState", arguments, PlayerOfTheCanvas));
                string drawings = After("-pairDrawings", arguments, DrawingsFolder);
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                var studio = new Studio(art, levels, StudioPaintings.Open(folder: null), fileNameEnding: string.Empty);
                foreach (PairedScreen screen in screens)
                {
                    WriteThePair(screen, state, studio, drawings);
                }

                Debug.Log($"Design pairs: {screens.Length} screens beside their drawings are in {StoryStudio.ShotsFolder}.");
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException
                || failure is IOException || failure is LevelFormatException || failure is CatalogueFormatException)
            {
                Debug.LogError($"Design pairs failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        private static string AllScreens() =>
            string.Join(",", ((PairedScreen[])Enum.GetValues(typeof(PairedScreen))).Select(screen => screen.ToString().ToLowerInvariant()));

        /// <exception cref="ArgumentException">No screen goes by that name.</exception>
        private static PairedScreen ScreenNamed(string name)
        {
            foreach (PairedScreen screen in (PairedScreen[])Enum.GetValues(typeof(PairedScreen)))
            {
                if (screen.ToString().ToLowerInvariant() == name)
                {
                    return screen;
                }
            }

            throw new ArgumentException($"\"{name}\" is not a screen the canvas draws and the game has: write one of {AllScreens()}.");
        }

        private static string After(string name, string[] arguments, string byDefault)
        {
            int at = Array.IndexOf(arguments, name);
            if (at >= 0 && at + 1 >= arguments.Length)
            {
                throw new ArgumentException($"{name} needs a value.");
            }

            return at >= 0 ? arguments[at + 1] : byDefault;
        }

        /// <summary>The canvas' picture of each screen, by its file's name without the ending.</summary>
        private static string DrawingOf(PairedScreen screen) => screen switch
        {
            PairedScreen.Home => "HomeTR",
            PairedScreen.Map => "MapTR",
            PairedScreen.Collection => "CollectionTR",
            PairedScreen.Jigsaw => "JigsawTR",
            PairedScreen.Board => "BoardTR",
            PairedScreen.Won => "WinTR",
            PairedScreen.Lost => "LoseTR",
            _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, "A screen the pair command does not know."),
        };

        private static void WriteThePair(PairedScreen screen, PlayerState state, Studio studio, string drawings)
        {
            Texture2D drawing = LoadTheDrawing(Path.Combine(drawings, DrawingOf(screen) + ".png"));
            Texture2D game = Take(screen, state, studio);
            Texture2D pair = SideBySide(drawing, game);
            try
            {
                string file = $"pair-{screen.ToString().ToLowerInvariant()}-{Screen}.png";
                CameraPicture.WritePng(pair, Path.Combine(StoryStudio.ShotsFolder, file));
                Debug.Log($"Design pairs: {file} ({DrawingOf(screen)} beside the game's {screen.ToString().ToLowerInvariant()} for the player {state.Label})");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(drawing);
                UnityEngine.Object.DestroyImmediate(game);
                UnityEngine.Object.DestroyImmediate(pair);
            }
        }

        /// <summary>The canvas' picture of a screen. The caller owns it and destroys it.</summary>
        /// <exception cref="IOException">The picture is not there, or is no picture.</exception>
        private static Texture2D LoadTheDrawing(string path)
        {
            if (!File.Exists(path))
            {
                throw new IOException($"The drawing \"{path}\" is not there; the canvas' pictures are kept outside the repository (-pairDrawings names their folder).");
            }

            var drawing = new Texture2D(2, 2, TextureFormat.RGB24, mipChain: false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            if (!drawing.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(drawing);
                throw new IOException($"\"{path}\" is not a picture the Editor can read.");
            }

            return drawing;
        }

        /// <summary>What the game's camera shows of a screen for a player. The caller owns the picture and destroys it.</summary>
        private static Texture2D Take(PairedScreen screen, PlayerState state, Studio studio)
        {
            switch (screen)
            {
                case PairedScreen.Home:
                    return TakeThePage(new MuseumDoor(Destination.Home), state, studio);
                case PairedScreen.Map:
                    return TakeThePage(new MuseumDoor(Destination.Map), state, studio);
                case PairedScreen.Collection:
                    return TakeThePage(new MuseumDoor(Destination.Collection), state, studio);
                case PairedScreen.Jigsaw:
                    return TakeTheJigsaw(state, studio);
                case PairedScreen.Board:
                    return TakeTheBoard(state, studio, ending: null);
                case PairedScreen.Won:
                    return TakeTheBoard(state, studio, FlowEnding.Won);
                case PairedScreen.Lost:
                    return TakeTheBoard(state, studio, FlowEnding.Lost);
                default:
                    throw new ArgumentOutOfRangeException(nameof(screen), screen, "A screen the pair command does not know.");
            }
        }

        private static Texture2D TakeThePage(MuseumDoor door, PlayerState state, Studio studio)
        {
            using MuseumStage museum = ScreenShots.OpenTheMuseum(door, state, Screen, studio);
            return StoryStudio.Take(museum, Screen);
        }

        /// <summary>The jigsaw of the painting the player is restoring, at rest.</summary>
        private static Texture2D TakeTheJigsaw(PlayerState state, Studio studio)
        {
            // A scene of its own that is never saved: nothing of an earlier picture is left in the next.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PaintingNumber painting = MuseumStanding.Of(state.Progress.Museum, studio.Paintings.Catalogue).Painting;
            using JigsawStage jigsaw = JigsawStage.Open(painting, state.Progress.Museum, studio.Paintings, studio.Art);
            jigsaw.Fit(Screen);
            return StoryStudio.Take(jigsaw, Screen);
        }

        /// <summary>
        /// The board of the level the player stands on: at rest, or with the card of the level won or lost, which
        /// a bot plays it to on the game's own session (an expert for a win, one that moves at random for a loss).
        /// </summary>
        /// <param name="ending">How the level ends before the picture is taken; nothing for the board as it opens.</param>
        private static Texture2D TakeTheBoard(PlayerState state, Studio studio, FlowEnding? ending)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, new ProgressInMemory(state.Progress), Screen, SessionOpening.AtTheBoard);
            StoryStudio.AdvanceUntilSettled(session);
            if (ending is FlowEnding asked)
            {
                FlowStory.PlayToTheEnding(session, asked, asked == FlowEnding.Won ? Expert() : new RandomPolicy());
                StoryStudio.AdvanceUntilSettled(session);
            }

            return StoryStudio.Take(session.Front, Screen);
        }

        private static IBotPolicy Expert() => BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot)
            ? bot
            : throw new InvalidOperationException($"The bots' list has no {SkillProfile.Expert.Name}.");

        /// <summary>The drawing at the left, scaled to the game's height, and the game's picture at the right. The caller owns the picture and destroys it.</summary>
        private static Texture2D SideBySide(Texture2D drawing, Texture2D game)
        {
            int height = game.height;
            int drawingWidth = Mathf.RoundToInt(drawing.width * (float)height / drawing.height);
            Color32[] left = Scaled(drawing, drawingWidth, height);
            Color32[] right = game.GetPixels32();

            int width = drawingWidth + Gap + game.width;
            var pixels = new Color32[width * height];
            Array.Fill(pixels, Paper);
            for (int row = 0; row < height; row++)
            {
                Array.Copy(left, row * drawingWidth, pixels, row * width, drawingWidth);
                Array.Copy(right, row * game.width, pixels, row * width + drawingWidth + Gap, game.width);
            }

            var pair = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false);
            pair.SetPixels32(pixels);
            pair.Apply(updateMipmaps: false);
            return pair;
        }

        /// <summary>A picture's pixels at another size, as the graphics card scales it.</summary>
        private static Color32[] Scaled(Texture2D picture, int width, int height)
        {
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture activeBefore = RenderTexture.active;
            var read = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false);
            try
            {
                Graphics.Blit(picture, target);
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                read.Apply(updateMipmaps: false);
                return read.GetPixels32();
            }
            finally
            {
                RenderTexture.active = activeBefore;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(read);
            }
        }
    }
}
