#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Core.Levels;
using Puzzle.Museum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Whether the collection keeps its memory bounded however long the player browses it (A42 item 4, A14): the
    /// 23 October phone run saw the game grow from 401 MB to 649 MB while it turned through the 20 rooms, because
    /// every room's pictures stayed loaded after the room was taken down.
    /// usage: <c>Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.CollectionMemoryCheck.Run -logFile out/unity/collection-memory.log</c>.
    /// On the game's own session, for a player in the last room, so the collection shows all 20 rooms, it walks
    /// from the last room to the first and back, three times by the plaque's round buttons and twice by swipes on
    /// the plaque, and counts after every turn (and with the finger down, while two rooms hang) the museum's
    /// pictures that are in memory. It fails when more than <see cref="PicturesAtMost"/> of them are ever in memory
    /// at once, and when a walk leaves more pictures, textures, meshes, materials or scene objects behind than
    /// the first walk did: what a room makes is destroyed with it. At the first room of every walk a
    /// restored painting comes forward and goes back. The figures go to the log and to
    /// out/unity/collection-memory.json. The Editor's own sizes are not a phone's: it holds a picture in about
    /// 27 MB (a phone in about 2.5 MB), it frees a texture's graphics memory some frames after the texture is
    /// unloaded, and its allocated memory counts the Editor's own work too (up to about 2 MB more after each walk,
    /// with the old code as with the new); so those two figures are written down but not judged: the counts are.
    /// </summary>
    public static class CollectionMemoryCheck
    {
        /// <summary>
        /// The most of the museum's pictures in memory at any moment of the walk: a room's five, the five of the
        /// room a finger drags in beside it or of the one that slides away, and the few the game keeps of the
        /// rooms just left, so turning back is instant. A quarter of the museum's hundred.
        /// </summary>
        private const int PicturesAtMost = 25;

        private const int WalksByButtons = 3;
        private const int WalksBySwipes = 2;

        /// <summary>A swipe on the plaque goes this share of the screen's width in this long, a step a frame.</summary>
        private const float SwipeShare = 0.45f;
        private const float SwipeSeconds = 0.5f;

        private const string ReportPath = "../out/unity/collection-memory.json";

        public static void Run()
        {
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath)
                    ?? throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                var screen = new ScreenShape(1080, 2424);
                Walk walk = WalkTheCollection(art, levels, screen);
                File.WriteAllText(ReportPath, walk.ToJson(screen));

                List<string> faults = walk.Faults();
                if (faults.Count > 0)
                {
                    Debug.LogError($"Collection memory check: FAILED; {string.Join("; ", faults)}. {walk.Summary()}");
                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log($"Collection memory check: passed; {walk.Summary()}");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException || failure is ArgumentException)
            {
                Debug.LogError($"Collection memory check: FAILED; {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        private static Walk WalkTheCollection(ArtCatalogue art, IReadOnlyList<LevelDefinition> levels, ScreenShape screen)
        {
            int lastRoomsFirstLevel = (MuseumPlan.RoomCount - 1) * MuseumPlan.LevelsPerRoom + 1;
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(levels, lastRoomsFirstLevel), levels);
            PlayerProgress placed = standing.WithWaitingPiecesPlaced(standing.Museum.PiecesWaiting);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorUtility.UnloadUnusedAssetsImmediate();
            var walk = new Walk(Census.Take("before the session"));
            using var session = new GameSession(
                levels, art, PaintingGallery.OfThePackedMuseum(), StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(placed), StoryStudio.AttemptsKeptInMemory(),
                screen, SessionOpening.InTheMuseum);
            StoryStudio.AdvanceUntilSettled(session);
            MuseumStage museum = session.Museum ?? throw new InvalidOperationException("The session did not open in the museum.");
            walk.Note(Census.Take("in the hall"));

            Press(session, museum.PixelOf(Destination.Collection));
            Require(museum.Showing == Destination.Collection, $"the bar's collection button showed {museum.Showing}");
            int rooms = CollectionRoom(museum).Value;
            Require(rooms == MuseumPlan.RoomCount, $"the collection opened on room {rooms}, not on the last room {MuseumPlan.RoomCount}");
            walk.Note(Census.Take("the collection opened"));

            var finger = new PlaqueFinger(session, ScreenView.Of(screen));
            for (int round = 1; round <= WalksByButtons + WalksBySwipes; round++)
            {
                bool bySwipes = round > WalksByButtons;
                for (int room = rooms; room > 1; room--)
                {
                    Turn(session, museum, finger, toTheRoomBefore: true, bySwipes, walk);
                }

                LookAtAPainting(session, museum, walk);
                for (int room = 1; room < rooms; room++)
                {
                    Turn(session, museum, finger, toTheRoomBefore: false, bySwipes, walk);
                }

                walk.Note(Census.Take($"walk {round} ({(bySwipes ? "swipes" : "buttons")})"), endsAWalk: true);
            }

            Press(session, museum.PixelOf(Destination.Home));
            Require(museum.Showing == Destination.Home, $"the bar's hall button showed {museum.Showing}");
            walk.Note(Census.Take("back in the hall"));
            return walk;
        }

        /// <summary>Turns the collection one room on, by the round button or by a swipe on the plaque, and counts the pictures with the finger down and once the room stands.</summary>
        private static void Turn(GameSession session, MuseumStage museum, PlaqueFinger finger, bool toTheRoomBefore, bool bySwipe, Walk walk)
        {
            int from = CollectionRoom(museum).Value;
            if (bySwipe)
            {
                finger.Swipe(PixelOrFail(museum.PlaquePixel, "the plaque"), toTheRoomBefore ? SwipeShare : -SwipeShare, () => walk.Count(PicturesInMemory()));
            }
            else
            {
                Press(session, PixelOrFail(toTheRoomBefore ? museum.PreviousRoomPixel : museum.NextRoomPixel, "a round button of the plaque"));
            }

            int to = CollectionRoom(museum).Value;
            int expected = toTheRoomBefore ? from - 1 : from + 1;
            Require(to == expected, $"a {(bySwipe ? "swipe" : "press")} from room {from} showed room {to}, not room {expected}");
            walk.Count(PicturesInMemory());
        }

        /// <summary>The first painting of the room shown, restored, comes forward to be looked at by itself and goes back to the wall.</summary>
        private static void LookAtAPainting(GameSession session, MuseumStage museum, Walk walk)
        {
            PaintingNumber painting = CollectionRoom(museum).Paintings.First();
            Press(session, PixelOrFail(museum.PixelOfExhibit(painting), $"painting {painting}"));
            Require(museum.PaintingViewed?.Value == painting.Value, $"a touch on painting {painting} brought {museum.PaintingViewed?.ToString() ?? "nothing"} forward");
            walk.Count(PicturesInMemory());
            Press(session, PixelOrFail(museum.PixelOfExhibit(painting), $"painting {painting}"));
            Require(museum.PaintingViewed == null, $"a touch on painting {museum.PaintingViewed} looked at by itself did not put it back");
        }

        private static void Press(GameSession session, Vector2 pixel)
        {
            session.PointerDown(pixel);
            session.PointerUp();
            StoryStudio.AdvanceUntilSettled(session);
        }

        private static RoomNumber CollectionRoom(MuseumStage museum) =>
            museum.Door.CollectionRoom ?? throw new InvalidOperationException($"The museum shows {museum.Showing}, not the collection.");

        private static Vector2 PixelOrFail(Vector2? pixel, string what) =>
            pixel ?? throw new InvalidOperationException($"The collection shows no {what} to touch.");

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }

        /// <summary>The museum's pictures that are in memory now: the textures loaded from the paintings' folder.</summary>
        private static Texture2D[] PicturesInMemory() => Resources.FindObjectsOfTypeAll<Texture2D>()
            .Where(texture => AssetDatabase.GetAssetPath(texture).StartsWith(PaintingImport.PaintingsFolder + "/", StringComparison.Ordinal))
            .ToArray();

        /// <summary>What is in memory at one moment of the walk.</summary>
        private sealed class Census
        {
            private Census(string moment, Texture2D[] pictures)
            {
                Moment = moment;
                Pictures = pictures.Length;
                PictureBytes = pictures.Sum(picture => Profiler.GetRuntimeMemorySizeLong(picture));
                Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
                Textures = textures.Length;
                TextureBytes = textures.Sum(texture => Profiler.GetRuntimeMemorySizeLong(texture));
                Meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
                Materials = Resources.FindObjectsOfTypeAll<Material>().Length;
                SceneObjects = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
                AllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
                GraphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            }

            public string Moment { get; }

            public int Pictures { get; }

            public long PictureBytes { get; }

            public int Textures { get; }

            public long TextureBytes { get; }

            public int Meshes { get; }

            public int Materials { get; }

            public int SceneObjects { get; }

            public long AllocatedBytes { get; }

            public long GraphicsDriverBytes { get; }

            public static Census Take(string moment) => new Census(moment, PicturesInMemory());

            /// <summary>The ways this census holds more than an earlier one; nothing when it holds no more.</summary>
            public IEnumerable<string> GrowthSince(Census earlier)
            {
                if (Pictures > earlier.Pictures)
                {
                    yield return $"{Pictures} pictures {Moment}, {earlier.Pictures} {earlier.Moment}";
                }

                if (Textures > earlier.Textures)
                {
                    yield return $"{Textures} textures {Moment}, {earlier.Textures} {earlier.Moment}";
                }

                if (Meshes > earlier.Meshes)
                {
                    yield return $"{Meshes} meshes {Moment}, {earlier.Meshes} {earlier.Moment}";
                }

                if (Materials > earlier.Materials)
                {
                    yield return $"{Materials} materials {Moment}, {earlier.Materials} {earlier.Moment}";
                }

                if (SceneObjects > earlier.SceneObjects)
                {
                    yield return $"{SceneObjects} scene objects {Moment}, {earlier.SceneObjects} {earlier.Moment}";
                }
            }

            public string ToJson() =>
                "{ "
                + $"\"moment\": \"{Moment}\", \"pictures\": {Pictures}, \"pictureMB\": {Megabytes(PictureBytes)}, \"textures\": {Textures}, \"textureMB\": {Megabytes(TextureBytes)}, "
                + $"\"meshes\": {Meshes}, \"materials\": {Materials}, \"sceneObjects\": {SceneObjects}, "
                + $"\"allocatedMB\": {Megabytes(AllocatedBytes)}, \"graphicsDriverMB\": {Megabytes(GraphicsDriverBytes)}"
                + " }";

            public override string ToString() =>
                $"{Moment}: {Pictures} pictures ({Megabytes(PictureBytes)} MB), {Textures} textures ({Megabytes(TextureBytes)} MB), {Meshes} meshes, {Materials} materials, "
                + $"{SceneObjects} scene objects, {Megabytes(AllocatedBytes)} MB allocated, {Megabytes(GraphicsDriverBytes)} MB for the graphics driver";

            private static string Megabytes(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>The censuses of the walk, and the most pictures counted in memory at any moment of it.</summary>
        private sealed class Walk
        {
            private readonly List<Census> _censuses = new List<Census>();
            private readonly List<Census> _walksEnded = new List<Census>();
            private int _turnsCounted;

            public Walk(Census first)
            {
                _censuses.Add(first);
            }

            /// <summary>The most of the museum's pictures in memory at once, counted after every turn and with the finger down.</summary>
            public int MostPictures { get; private set; }

            public void Note(Census census, bool endsAWalk = false)
            {
                _censuses.Add(census);
                MostPictures = Math.Max(MostPictures, census.Pictures);
                if (endsAWalk)
                {
                    _walksEnded.Add(census);
                }

                Debug.Log($"Collection memory check: {census}.");
            }

            public void Count(Texture2D[] pictures)
            {
                MostPictures = Math.Max(MostPictures, pictures.Length);
                _turnsCounted++;
            }

            public List<string> Faults()
            {
                var faults = new List<string>();
                if (MostPictures > PicturesAtMost)
                {
                    faults.Add($"{MostPictures} of the museum's pictures were in memory at once, more than {PicturesAtMost}");
                }

                Census first = _walksEnded[0];
                foreach (Census later in _walksEnded.Skip(1))
                {
                    faults.AddRange(later.GrowthSince(first));
                }

                return faults;
            }

            public string Summary()
            {
                Census opened = _censuses.First(census => census.Moment == "the collection opened");
                Census last = _walksEnded[_walksEnded.Count - 1];
                return $"{_walksEnded.Count} walks through all {MuseumPlan.RoomCount} rooms ({WalksByButtons} by the plaque's buttons, {WalksBySwipes} by swipes), {_turnsCounted} counts; "
                    + $"at most {MostPictures} pictures in memory at once (allowed {PicturesAtMost}); {opened}; {last}.";
            }

            public string ToJson(ScreenShape screen) =>
                "{\n"
                + $"  \"screen\": \"{screen}\",\n"
                + $"  \"mostPicturesAtOnce\": {MostPictures},\n"
                + $"  \"picturesAtMost\": {PicturesAtMost},\n"
                + "  \"censuses\": [\n    " + string.Join(",\n    ", _censuses.Select(census => census.ToJson())) + "\n  ]\n"
                + "}\n";
        }

        /// <summary>A finger that swipes the plaque: it comes down, slides a step a frame as the clock runs, is counted with while still down, and lifts.</summary>
        private sealed class PlaqueFinger
        {
            private readonly GameSession _session;
            private readonly ScreenView _view;

            public PlaqueFinger(GameSession session, ScreenView view)
            {
                _session = session;
                _view = view;
            }

            /// <param name="acrossShare">How far, as a share of the screen's width; more than nothing to the right.</param>
            /// <param name="whileDown">Called at the end of the way, before the finger lifts.</param>
            public void Swipe(Vector2 from, float acrossShare, Action whileDown)
            {
                var way = new Vector2(acrossShare * _view.Width * _view.PixelsPerPoint, 0f);
                int frames = Math.Max(1, Mathf.RoundToInt(SwipeSeconds / StoryStudio.FrameSeconds));
                _session.PointerDown(from);
                for (int frame = 1; frame <= frames; frame++)
                {
                    _session.Advance(StoryStudio.FrameSeconds);
                    _session.PointerMoved(from + way * ((float)frame / frames));
                }

                whileDown();
                _session.PointerUp();
                _session.Advance(StoryStudio.FrameSeconds);
                whileDown(); // the room that was let go slides away beside the one that comes in
                StoryStudio.AdvanceUntilSettled(_session);
            }
        }
    }
}
