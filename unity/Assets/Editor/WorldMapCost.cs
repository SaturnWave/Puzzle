#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Maps;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Core.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What the world map costs to draw (#4), measured in the Editor's batch mode on this computer's graphics:
    /// <c>Unity -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.WorldMapCost.Run -logFile out/unity/worldmap-cost.log</c>.
    /// It builds the map's meshes once and says how many triangles and corners they hold and how long they took; it
    /// opens the map page as the game does, for the player of the design (level 287), and measures how long a page
    /// takes to build, against the collection's page; then, through a glide of the camera from the world to the sixth era, how long a step of the
    /// stage takes on the processor and how long the camera takes to draw a frame and the graphics card to finish
    /// it, against the hall's page as a yardstick. The figures go to the log and to out/unity/worldmap-cost.json. A
    /// phone is slower than this computer; what it shows is the share the map adds to a frame, and that the camera's
    /// moves build nothing.
    /// </summary>
    public static class WorldMapCost
    {
        private const int Frames = 90;
        private const float Step = 1f / 60f;
        private const int Builds = 5;

        public static void Run()
        {
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath)
                    ?? throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                var studio = new Studio(art, levels, StudioPaintings.Open(null), string.Empty);
                var screen = new ScreenShape(1080, 2424);
                var report = new List<string>();

                (int triangles, int vertices) = WorldMapMeshes.Size;
                report.Add(Pair("meshTriangles", triangles));
                report.Add(Pair("meshVertices", vertices));
                report.Add(Pair("meshBuildMs", WorldMapMeshes.BuildMilliseconds));

                PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(levels, 287), levels);
                PlayerProgress placed = standing.WithWaitingPiecesPlaced(standing.Museum.PiecesWaiting);
                double pageMs = 0;
                double collectionMs = 0;
                (double hallCpu, double hallGpu) = (0, 0);
                (double mapCpu, double mapGpu) = (0, 0);
                int renderers = 0;
                for (int build = 0; build < Builds; build++)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    using var session = new GameSession(
                        levels, art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(placed), StoryStudio.AttemptsKeptInMemory(),
                        screen, SessionOpening.InTheMuseum);
                    StoryStudio.AdvanceUntilSettled(session);
                    MuseumStage museum = session.Museum ?? throw new InvalidOperationException("The session did not open in the museum.");
                    if (build == 0)
                    {
                        (hallCpu, hallGpu) = Frame(session, museum.Camera, screen);
                    }

                    var yardstick = Stopwatch.StartNew();
                    session.PointerDown(museum.PixelOf(Destination.Collection));
                    session.PointerUp();
                    collectionMs += yardstick.Elapsed.TotalMilliseconds;
                    StoryStudio.AdvanceUntilSettled(session);

                    var clock = Stopwatch.StartNew();
                    session.PointerDown(museum.PixelOf(Destination.Map));
                    session.PointerUp();
                    pageMs += clock.Elapsed.TotalMilliseconds;
                    StoryStudio.AdvanceUntilSettled(session);
                    if (build == 0)
                    {
                        renderers = museum.Map!.Root.GetComponentsInChildren<Renderer>().Length;
                        StoryStudio.AdvanceBy(Tempo.MapOpensAfter - 0.3f, session);
                        (mapCpu, mapGpu) = Frame(session, museum.Camera, screen);
                    }
                }

                report.Add(Pair("pageBuildMs", pageMs / Builds));
                report.Add(Pair("collectionPageBuildMs", collectionMs / Builds));
                report.Add(Pair("renderersOnTheMapPage", renderers));
                report.Add(Pair("hallStepCpuMs", hallCpu));
                report.Add(Pair("hallFrameGpuMs", hallGpu));
                report.Add(Pair("mapGlideStepCpuMs", mapCpu));
                report.Add(Pair("mapGlideFrameGpuMs", mapGpu));
                report.Add($"\"device\": \"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})\"");
                report.Add($"\"screen\": \"{screen}\"");
                report.Add($"\"frames\": {Frames}");

                string json = "{\n  " + string.Join(",\n  ", report) + "\n}\n";
                Directory.CreateDirectory("../out/unity");
                File.WriteAllText("../out/unity/worldmap-cost.json", json);
                Debug.Log($"World map cost:\n{json}");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException || failure is ArgumentException)
            {
                Debug.LogError($"World map cost failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Steps the stage a frame at a time and draws each frame into a picture as large as the screen, waiting for the graphics card each time.</summary>
        /// <returns>The mean milliseconds of a step on the processor, and of drawing a frame to its end.</returns>
        private static (double Cpu, double Gpu) Frame(GameSession session, Camera camera, ScreenShape screen)
        {
            RenderTexture target = RenderTexture.GetTemporary(screen.PixelWidth, screen.PixelHeight, 24, RenderTextureFormat.ARGB32);
            var picture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            double cpu = 0, gpu = 0;
            try
            {
                camera.targetTexture = target;
                for (int frame = 0; frame < Frames; frame++)
                {
                    var step = Stopwatch.StartNew();
                    session.Advance(Step);
                    cpu += step.Elapsed.TotalMilliseconds;

                    var draw = Stopwatch.StartNew();
                    camera.Render();
                    RenderTexture.active = target;
                    picture.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false); // waits for the frame to be drawn
                    RenderTexture.active = null;
                    gpu += draw.Elapsed.TotalMilliseconds;
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(picture);
            }

            return (cpu / Frames, gpu / Frames);
        }

        private static string Pair(string name, double value) => $"\"{name}\": {value.ToString("0.###", CultureInfo.InvariantCulture)}";
    }
}
