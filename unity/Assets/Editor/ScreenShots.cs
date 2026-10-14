#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Core.Levels;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>What the picture command can show. A closed set.</summary>
    internal enum ShotPage
    {
        /// <summary>The museum's hall.</summary>
        Home,

        /// <summary>The museum's floor plan.</summary>
        Map,

        /// <summary>A room's wall in the collection.</summary>
        Collection,

        /// <summary>The collection with a restored painting brought forward to be looked at.</summary>
        Viewer,

        /// <summary>A painting's jigsaw, at the moments <c>-jigsawMoments</c> names.</summary>
        Jigsaw,

        /// <summary>The hall of the last room the player has restored, as it is shown right after its fifth painting became whole.</summary>
        Restored,

        /// <summary>The board of the level the player stands on, with the card that asks whether they mean to leave it.</summary>
        Leaving,
    }

    /// <summary>A player as the picture command is told of one: so many levels won, so many of the pieces they gave in place.</summary>
    internal readonly struct PlayerState
    {
        private static readonly Regex Pattern = new Regex(@"^(\d+)(?:\+(\d+))?$");

        private PlayerState(PlayerProgress progress, string label)
        {
            Progress = progress;
            Label = label;
        }

        public PlayerProgress Progress { get; }

        /// <summary>The state as it is written into a picture's file name.</summary>
        public string Label { get; }

        /// <summary>
        /// Reads "46" (46 levels won, no piece placed) or "46+42" (and 42 pieces in place, in the museum's order:
        /// painting by painting, each painting's pieces by their numbers). Asking for more pieces than the levels
        /// gave places them all.
        /// </summary>
        /// <exception cref="ArgumentException">The text is neither.</exception>
        public static PlayerState Parse(string text)
        {
            Match match = Pattern.Match(text);
            if (!match.Success)
            {
                throw new ArgumentException($"\"{text}\" is not a player's state: write the levels won, such as 46, or 46+42 with the pieces in place.");
            }

            var won = new PlayerProgress(ShotPlan.WholeNumber(match.Groups[1].Value), attemptsOnLevel: 0);
            int asked = match.Groups[2].Success ? ShotPlan.WholeNumber(match.Groups[2].Value) : 0;
            PlayerProgress progress = won.WithWaitingPiecesPlaced(Math.Min(asked, won.Museum.PiecesWaiting));
            return new PlayerState(progress, $"won{progress.LevelsWon}-placed{progress.Museum.PiecesPlaced}");
        }
    }

    /// <summary>What a run of the picture command is asked for.</summary>
    internal sealed class ShotPlan
    {
        private static readonly Regex ScreenPattern = new Regex(@"^(\d+)x(\d+)(?:\+(\d+)\+(\d+))?$");

        /// <summary>What <c>-screens</c> calls the sheets of the controls several screens share: they show no player, so they are no page of <see cref="ShotPage"/>.</summary>
        private const string Controls = "controls";

        private ShotPlan(string[] arguments)
        {
            string[] screens = ListAfter("-screens", arguments, "home,map,collection");
            ShowsTheControls = screens.Contains(Controls);
            Pages = screens.Where(text => text != Controls).Select(text => OneOf<ShotPage>(text, $"a page, nor \"{Controls}\"")).ToArray();
            States = ListAfter("-screenStates", arguments, "0,46+42,50+50,640+3000").Select(PlayerState.Parse).ToArray();
            Screens = ListAfter("-screenSizes", arguments, "1080x2424,2076x2152").Select(ScreenFrom).ToArray();
            JigsawMoments = ListAfter("-jigsawMoments", arguments, "rest,carried,placed,flying,restored").Select(text => OneOf<JigsawMoment>(text, "a moment of the jigsaw")).ToArray();
            Painting = ValueAfter("-jigsawPainting", arguments) is string painting ? new PaintingNumber(WholeNumber(painting)) : (PaintingNumber?)null;
            CollectionRoom = ValueAfter("-collectionRoom", arguments) is string room ? new RoomNumber(WholeNumber(room)) : (RoomNumber?)null;
            PaintingsFolder = ValueAfter("-paintingsFrom", arguments);
            Walks = Array.IndexOf(arguments, "-screenWalk") >= 0;
        }

        public IReadOnlyList<ShotPage> Pages { get; }

        /// <summary>True when the sheets of the shared controls are pictured on each screen: every control in every face, on each ground the game draws on.</summary>
        public bool ShowsTheControls { get; }

        public IReadOnlyList<PlayerState> States { get; }

        public IReadOnlyList<ScreenShape> Screens { get; }

        public IReadOnlyList<JigsawMoment> JigsawMoments { get; }

        /// <summary>The painting whose jigsaw is shown; nothing for the painting each player is restoring.</summary>
        public PaintingNumber? Painting { get; }

        /// <summary>The room the collection shows; nothing for the room each player stands in.</summary>
        public RoomNumber? CollectionRoom { get; }

        /// <summary>A folder with the paintings' pictures to show instead of the ones the game carries; nothing for the game's own.</summary>
        public string? PaintingsFolder { get; }

        /// <summary>True when a walk through the game is pictured from each state on each screen.</summary>
        public bool Walks { get; }

        /// <exception cref="ArgumentException">An argument is not what its name asks for.</exception>
        public static ShotPlan FromCommandLine(string[] arguments) => new ShotPlan(arguments);

        public static int WholeNumber(string text) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                ? number
                : throw new ArgumentException($"\"{text}\" is not a whole number.");

        /// <summary>The member of a closed set that is written like the text, in small letters.</summary>
        private static T OneOf<T>(string text, string what)
            where T : struct, Enum
        {
            foreach (T member in (T[])Enum.GetValues(typeof(T)))
            {
                if (member.ToString().ToLowerInvariant() == text)
                {
                    return member;
                }
            }

            string known = string.Join(", ", ((T[])Enum.GetValues(typeof(T))).Select(member => member.ToString().ToLowerInvariant()));
            throw new ArgumentException($"\"{text}\" is not {what}: write one of {known}.");
        }

        private static string[] ListAfter(string name, string[] arguments, string byDefault) => (ValueAfter(name, arguments) ?? byDefault).Split(',');

        private static string? ValueAfter(string name, string[] arguments)
        {
            int at = Array.IndexOf(arguments, name);
            if (at >= 0 && at + 1 >= arguments.Length)
            {
                throw new ArgumentException($"{name} needs a value.");
            }

            return at >= 0 ? arguments[at + 1] : null;
        }

        private static ScreenShape ScreenFrom(string text)
        {
            Match match = ScreenPattern.Match(text);
            if (!match.Success)
            {
                throw new ArgumentException($"\"{text}\" is not a screen: write 1080x2340, or 1080x2340+96+48 with top and bottom insets.");
            }

            int Number(int group) => match.Groups[group].Success ? WholeNumber(match.Groups[group].Value) : 0;
            return new ScreenShape(Number(1), Number(2), topInsetPixels: Number(3), bottomInsetPixels: Number(4));
        }
    }

    /// <summary>
    /// Pictures of the museum's screens for the Unity Editor's batch mode, of any player on any screen, with no game
    /// running:
    /// <c>Unity -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.ScreenShots.Run
    /// [-screens home,map,collection,viewer,jigsaw,restored,leaving,controls] [-screenStates 0,46+42,50+50] [-screenSizes 1080x2424,2076x2152]
    /// [-collectionRoom 2] [-jigsawPainting 5] [-jigsawMoments rest,carried,goingback,placed,flying,restored]
    /// [-paintingsFrom ../art/paintings] [-screenWalk]</c>.
    /// A state is the levels won, or the levels won and the pieces in place. Each page, state and screen gives
    /// one picture in out/unity/shots, named <c>screen-&lt;page&gt;-&lt;state&gt;-&lt;size&gt;.png</c>; the jigsaw
    /// gives one per moment. <c>-paintingsFrom</c> shows the paintings from picture files of a folder (laid out as
    /// the catalogue names them) instead of the ones the game carries: it is how the screens are looked at with
    /// full-size pictures before those are in the game; such pictures end in <c>-full</c>.
    /// <c>controls</c> is no page of a player: it gives, for each screen, the sheets of the controls several
    /// screens share (<see cref="ControlsSpecimen"/>), every control in every face on the wall, on a card's cream
    /// and on the shelf's wood, named <c>screen-controls-&lt;sheet&gt;-&lt;ground&gt;-&lt;size&gt;.png</c>.
    /// </summary>
    /// <remarks>
    /// <c>-screenWalk</c> also walks through the game from each state on each screen, on the game's own session and
    /// by touches alone (<see cref="GameWalk"/>): through the museum's pages, into the level, through a whole
    /// level, from a won level's card to the jigsaw, where the piece just won is carried to its place, and back.
    /// The walk fails when a touch does not lead where it should.
    /// </remarks>
    public static class ScreenShots
    {
        /// <summary>A restored room is pictured while the light runs over its painting, and again when its sparkles are all out.</summary>
        private const float RestoredRoomLightMoment = 0.75f;
        private const float RestoredRoomRestMoment = 2.4f;

        public static void Run()
        {
            try
            {
                ShotPlan plan = ShotPlan.FromCommandLine(Environment.GetCommandLineArgs());
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                var studio = new Studio(art, levels, StudioPaintings.Open(plan.PaintingsFolder), plan.PaintingsFolder == null ? string.Empty : "-full");
                int pictures = 0;
                foreach (ScreenShape screen in plan.Screens)
                {
                    if (plan.ShowsTheControls)
                    {
                        pictures += ShootTheControls(screen, studio);
                    }

                    foreach (PlayerState state in plan.States)
                    {
                        pictures += plan.Pages.Sum(page => Shoot(page, plan, state, screen, studio));
                        if (plan.Walks)
                        {
                            GameWalk.Run(state, screen, studio);
                        }
                    }
                }

                Debug.Log($"Screen shots: {pictures} pictures of the museum's screens{(plan.Walks ? " and a walk through the game from each state on each screen" : string.Empty)} are in {StoryStudio.ShotsFolder}.");
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException
                || failure is IOException || failure is LevelFormatException || failure is CatalogueFormatException)
            {
                Debug.LogError($"Screen shots failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <returns>How many pictures were taken.</returns>
        private static int Shoot(ShotPage page, ShotPlan plan, PlayerState state, ScreenShape screen, Studio studio) => page switch
        {
            ShotPage.Home => ShootThePage(new MuseumDoor(Destination.Home), "home", state, screen, studio),
            ShotPage.Map => ShootThePage(new MuseumDoor(Destination.Map), "map", state, screen, studio),
            ShotPage.Collection => ShootThePage(CollectionDoor(plan, state, studio), CollectionName(plan), state, screen, studio),
            ShotPage.Viewer => ShootTheViewer(CollectionDoor(plan, state, studio), state, screen, studio),
            ShotPage.Jigsaw => JigsawShots.Shoot(plan.Painting ?? StandingOf(state, studio).Painting, plan.JigsawMoments, state, screen, studio),
            ShotPage.Restored => ShootTheRestoredRoom(state, screen, studio),
            ShotPage.Leaving => ShootTheLeavingCard(state, screen, studio),
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, "A page the picture command does not know."),
        };

        /// <summary>The sheets of the shared controls on one screen: each sheet on each ground, one picture each.</summary>
        /// <returns>How many pictures were taken.</returns>
        private static int ShootTheControls(ScreenShape screen, Studio studio)
        {
            int pictures = 0;
            foreach (ControlsPage page in (ControlsPage[])Enum.GetValues(typeof(ControlsPage)))
            {
                foreach (ControlsGround ground in (ControlsGround[])Enum.GetValues(typeof(ControlsGround)))
                {
                    // A scene of its own that is never saved: nothing of an earlier picture is left in the next.
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    Transform root = new GameObject("Controls").transform;
                    using ControlsSpecimen specimen = ControlsSpecimen.Build(root, studio.Art, page, ground, screen);
                    Camera camera = StageCamera.Add(root);
                    StageCamera.Show(camera, ScreenView.Of(screen));

                    string file = $"screen-controls-{page.ToString().ToLowerInvariant()}-{ground.ToString().ToLowerInvariant()}-{screen}{studio.FileNameEnding}.png";
                    CameraPicture.Write(camera, screen.PixelWidth, screen.PixelHeight, Path.Combine(StoryStudio.ShotsFolder, file));
                    Debug.Log($"Screen shots: {file}");
                    pictures++;
                }
            }

            return pictures;
        }

        private static MuseumStanding StandingOf(PlayerState state, Studio studio) => MuseumStanding.Of(state.Progress.Museum, studio.Paintings.Catalogue);

        /// <summary>The collection at the room that was asked for, or at the room the player stands in; never at a room it does not show that player.</summary>
        private static MuseumDoor CollectionDoor(ShotPlan plan, PlayerState state, Studio studio)
        {
            MuseumProgress museum = state.Progress.Museum;
            MuseumCatalogue catalogue = studio.Paintings.Catalogue;
            RoomNumber room = plan.CollectionRoom ?? StandingOf(state, studio).Room;
            return new MuseumDoor(Destination.Collection, CollectionRooms.Shows(room, museum, catalogue) ? room : CollectionRooms.LastToVisit(museum, catalogue));
        }

        /// <summary>
        /// The hall as it shows the last room the player has restored, a moment into its celebration and again once
        /// the sparkles are out; no picture for a player who has restored no room.
        /// </summary>
        private static int ShootTheRestoredRoom(PlayerState state, ScreenShape screen, Studio studio)
        {
            RoomNumber? restored = null;
            foreach (RoomNumber room in RoomNumber.All)
            {
                if (RoomWall.Of(room, state.Progress.Museum, studio.Paintings.Catalogue).IsRestored)
                {
                    restored = room;
                }
            }

            if (restored is not RoomNumber whole)
            {
                Debug.Log($"Screen shots: the player {state.Label} has restored no room to celebrate.");
                return 0;
            }

            using MuseumStage museum = OpenTheMuseum(MuseumDoor.AtTheRestored(whole), state, screen, studio);
            StageSteps.Advance(museum, RestoredRoomLightMoment);
            WritePicture(museum, screen, studio.FileName($"restored-room{whole.Value:D2}-light", state, screen));
            StageSteps.Advance(museum, RestoredRoomRestMoment - RestoredRoomLightMoment);
            WritePicture(museum, screen, studio.FileName($"restored-room{whole.Value:D2}", state, screen));
            return 2;
        }

        /// <summary>The board of the level the player stands on, after the round button on its shelf was pressed: the card that asks whether to leave.</summary>
        private static int ShootTheLeavingCard(PlayerState state, ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(state.Progress), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.AtTheBoard);
            StoryStudio.AdvanceUntilSettled(session);
            if (session.Stage.LeaveButtonPixel is not Vector2 button)
            {
                throw new InvalidOperationException("A board at rest has no button that leads out of the level.");
            }

            session.PointerDown(button);
            session.PointerUp();
            StoryStudio.AdvanceUntilSettled(session);
            if (!session.Stage.IsAskingWhetherToLeave)
            {
                throw new InvalidOperationException("A press on the shelf's round button did not ask whether to leave the level.");
            }

            WritePicture(session.Front, screen, studio.FileName("leaving", state, screen));
            return 1;
        }

        private static string CollectionName(ShotPlan plan) =>
            plan.CollectionRoom is RoomNumber room ? $"collection-room{room.Value:D2}" : "collection";

        /// <summary>One picture of a page as a player's progress has it.</summary>
        private static int ShootThePage(MuseumDoor door, string name, PlayerState state, ScreenShape screen, Studio studio)
        {
            using MuseumStage museum = OpenTheMuseum(door, state, screen, studio);
            WritePicture(museum, screen, studio.FileName(name, state, screen));
            return 1;
        }

        /// <summary>The collection with the first restored painting of its room touched and come forward; no picture where the room has none.</summary>
        private static int ShootTheViewer(MuseumDoor door, PlayerState state, ScreenShape screen, Studio studio)
        {
            using MuseumStage museum = OpenTheMuseum(door, state, screen, studio);
            RoomNumber room = door.CollectionRoom ?? StandingOf(state, studio).Room;
            foreach (HungPainting hung in RoomWall.Of(room, state.Progress.Museum, studio.Paintings.Catalogue).Paintings)
            {
                PaintingNumber painting = hung.Number;
                if (hung.Standing == PaintingStanding.Whole && museum.PixelOfExhibit(painting) is Vector2 pixel)
                {
                    museum.PointerDown(pixel);
                    museum.PointerUp();
                    StageSteps.Settle(museum);
                    if (museum.PaintingViewed != painting)
                    {
                        throw new InvalidOperationException($"A touch on painting {painting} of the collection did not bring it forward.");
                    }

                    WritePicture(museum, screen, studio.FileName($"viewer-p{painting.Value:D2}", state, screen));
                    return 1;
                }
            }

            Debug.Log($"Screen shots: room {room} has no restored painting to look at for the player {state.Label}.");
            return 0;
        }

        internal static MuseumStage OpenTheMuseum(MuseumDoor door, PlayerState state, ScreenShape screen, Studio studio)
        {
            // A scene of its own that is never saved: nothing of an earlier picture is left in the next.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerProgress progress = state.Progress;
            var visit = new MuseumVisit(
                progress.Museum, studio.Paintings.Catalogue, studio.Levels[progress.LevelIndexIn(studio.Levels.Count)].Id.Value,
                hasWonEveryLevel: progress.LevelsWon >= studio.Levels.Count);
            MuseumStage museum = MuseumStage.Open(visit, studio.Paintings, studio.Art, door);
            museum.Fit(screen);
            return museum;
        }

        internal static void WritePicture(IStage stage, ScreenShape screen, string file)
        {
            CameraPicture.Write(stage.Camera, screen.PixelWidth, screen.PixelHeight, Path.Combine(StoryStudio.ShotsFolder, file));
            Debug.Log($"Screen shots: {file}");
        }
    }

    /// <summary>What every picture of a run is taken with: the game's drawings, its levels, and the paintings as the run shows them.</summary>
    internal sealed class Studio
    {
        /// <param name="fileNameEnding">What the names of the run's pictures end in, before their extension: it tells the pictures of a run with other paintings apart.</param>
        public Studio(ArtCatalogue art, IReadOnlyList<LevelDefinition> levels, PaintingGallery paintings, string fileNameEnding)
        {
            Art = art;
            Levels = levels;
            Paintings = paintings;
            FileNameEnding = fileNameEnding;
        }

        public ArtCatalogue Art { get; }

        public IReadOnlyList<LevelDefinition> Levels { get; }

        public PaintingGallery Paintings { get; }

        public string FileNameEnding { get; }

        /// <summary>The name of the file of one picture of a screen.</summary>
        public string FileName(string what, PlayerState state, ScreenShape screen) => $"screen-{what}-{state.Label}-{screen}{FileNameEnding}.png";
    }

    /// <summary>The paintings a run of the picture command shows: the game's own, or picture files of a folder.</summary>
    internal static class StudioPaintings
    {
        /// <param name="folder">A folder that holds the pictures as the catalogue names them (room-01/water-lilies.jpg); nothing for the game's own pictures.</param>
        /// <exception cref="IOException">The folder is not there.</exception>
        public static PaintingGallery Open(string? folder)
        {
            if (folder == null)
            {
                return PaintingGallery.OfThePackedMuseum();
            }

            if (!Directory.Exists(folder))
            {
                throw new IOException($"The folder of paintings \"{folder}\" is not there.");
            }

            var loaded = new Dictionary<string, Texture2D?>();
            return new PaintingGallery(MuseumPack.LoadPacked(), artwork =>
            {
                if (!loaded.TryGetValue(artwork.ImageFile, out Texture2D? picture))
                {
                    picture = Load(Path.Combine(folder, artwork.ImageFile));
                    loaded.Add(artwork.ImageFile, picture);
                }

                return picture;
            });
        }

        /// <summary>A picture file as a texture with the smaller copies the game's own paintings are imported with; nothing when the file is missing.</summary>
        private static Texture2D? Load(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            // Kept through the new scene every picture is taken in: nothing but this run's own list refers to the texture.
            var picture = new Texture2D(2, 2, TextureFormat.RGB24, mipChain: true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (!picture.LoadImage(File.ReadAllBytes(path)))
            {
                throw new IOException($"\"{path}\" is not a picture the Editor can read.");
            }

            return picture;
        }
    }

    /// <summary>Moves a stage's clock for the picture commands that hold a stage and no session.</summary>
    internal static class StageSteps
    {
        private const float LongestSettleSeconds = 30f;

        /// <summary>
        /// How long a turn is shown at the most when so much of its script is still to come: the script, and the
        /// give of the pieces that landed last, which a script does not count; a few frames more is no turn any longer.
        /// </summary>
        public static float ATurnIsOverWithin(float scriptSeconds) => scriptSeconds + Tempo.Land + 4f * StoryStudio.FrameSeconds;

        /// <summary>
        /// Moves a clock frame by frame to the step in which the winning move's turn ends and the won game's
        /// finale begins: the finale's first frame.
        /// </summary>
        /// <param name="stepAFrame">Moves the clock one frame on: the stage's own, or that of the session that owns the stage.</param>
        /// <param name="turnStillTakes">How much of the winning turn's script is still to come.</param>
        /// <exception cref="InvalidOperationException">The turn came to its end and no finale began.</exception>
        public static void AdvanceToTheFinale(GameStage stage, Action stepAFrame, float turnStillTakes)
        {
            float longest = ATurnIsOverWithin(turnStillTakes);
            for (float waited = 0f; !stage.IsShowingTheFinale; waited += StoryStudio.FrameSeconds)
            {
                if (waited > longest)
                {
                    throw new InvalidOperationException(
                        $"The winning move of level {stage.Game.Level.Id.Value} came to rest and no finale began: the game is {stage.Game.Status} with {stage.Game.MovesLeft} moves left.");
                }

                stepAFrame();
            }
        }

        public static void Advance(IStage stage, float seconds)
        {
            for (float clock = 0f; clock + StoryStudio.FrameSeconds / 2f < seconds; clock += StoryStudio.FrameSeconds)
            {
                stage.Advance(StoryStudio.FrameSeconds);
            }
        }

        /// <exception cref="InvalidOperationException">The stage did not come to rest.</exception>
        public static void Settle(IStage stage)
        {
            for (float waited = 0f; !stage.IsSettled; waited += StoryStudio.FrameSeconds)
            {
                if (waited > LongestSettleSeconds)
                {
                    throw new InvalidOperationException($"The stage is still moving after {LongestSettleSeconds} s.");
                }

                stage.Advance(StoryStudio.FrameSeconds);
            }
        }
    }
}
