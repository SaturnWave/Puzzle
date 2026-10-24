#nullable enable
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Starts the game's scene the way a built game starts it and follows it through its entry page into the hall:
    /// it pictures the entry page once the page is ready, touches it as a finger would, and writes the hall's
    /// frame a second and a half after the hall has come: the check that the scene file and the code it starts belong
    /// together, and that the start-up behind the entry page opens the hall. Halfway through the hall's second it
    /// sounds every cue once through the running game's player. The pictures of the game are taken by RenderHarness,
    /// which needs no running game; this only proves that the running game gets there too.
    /// usage: Unity.exe -batchmode -projectPath unity -executeMethod Puzzle.Client.EditorTools.PlayModeCheck.Run
    /// Without -quit: the command ends the Editor itself, with exit code 1 when the game failed.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeCheck
    {
        private const string EntryPicturePath = "../out/unity/shots/play-mode-entry.png";
        private const string PicturePath = "../out/unity/shots/play-mode.png";

        /// <summary>The hall is pictured this many frames after the entry page was left.</summary>
        private const int FramesInTheHall = 90;

        /// <summary>Halfway: the game has opened its hall, and errors of its sounds still fail the check.</summary>
        private const int FramesBeforeTheSounds = FramesInTheHall / 2;

        /// <summary>A game that has not reached its hall through the entry page in this long after play mode began has failed.</summary>
        private const float LongestStartSeconds = 60f;

        /// <summary>Entering play mode loads the scripts anew and forgets every static field; the Editor's session state survives it.</summary>
        private const string IsRunningKey = "Puzzle.Client.PlayModeCheck.IsRunning";

        /// <summary>
        /// Stands in for the screen. A batch-mode Editor has no game window, and a camera without a target is then
        /// never rendered; the game must be drawn every frame from its first one, as a phone draws it. It follows the
        /// one camera the game draws with: the entry page's, then the hall's.
        /// </summary>
        private static RenderTexture? _screen;

        private static bool _gameFailed;
        private static bool _isDone;
        private static bool _hasSounded;
        private static bool _isTheEntryPictured;
        private static bool _isTheEntryTouched;

        /// <summary>The frame in which the hall was first the game's; nothing before.</summary>
        private static int? _hallFrom;

        private static float? _began;

        static PlayModeCheck()
        {
            if (SessionState.GetBool(IsRunningKey, false))
            {
                Application.logMessageReceived += NoteTheGamesErrors;
                EditorApplication.update += WatchTheGame;
                RenderPipelineManager.endCameraRendering += KeepTheFrames;
            }
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath);
            SessionState.SetBool(IsRunningKey, true);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// An error fails the check unless it comes out of the Editor's own code, which keeps working beside the
        /// game (its search index, for one, reports an exception on a project it has not indexed yet).
        /// </summary>
        private static void NoteTheGamesErrors(string message, string stackTrace, LogType type)
        {
            bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            bool isTheEditorsOwn = stackTrace.Contains("UnityEditor.");
            _gameFailed |= isError && !isTheEditorsOwn;
        }

        private static void WatchTheGame()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            _began ??= Time.realtimeSinceStartup;
            AimTheScreenAtTheOneCamera();
            GameRoot? root = Object.FindAnyObjectByType<GameRoot>();
            if (root?.Entry is EntryStage entry)
            {
                if (!_isTheEntryPictured && entry.IsSettled)
                {
                    // Rendered once more as it stands: a frame read back while it is drawn may be one the game has already left.
                    CameraPicture.Write(entry.Camera, Screen.width, Screen.height, EntryPicturePath);
                    _isTheEntryPictured = true;
                    Debug.Log($"Play mode check: the entry page, ready in frame {Time.frameCount} of a {Screen.width}x{Screen.height} screen, is at {EntryPicturePath}; game errors: {_gameFailed}.");
                }
                else if (_isTheEntryPictured && !_isTheEntryTouched)
                {
                    entry.PointerDown(entry.MiddlePixel);
                    entry.PointerUp();
                    _isTheEntryTouched = true;
                    Debug.Log($"Play mode check: the ready entry page was touched in frame {Time.frameCount}.");
                }
            }
            else if (root?.Session != null && _hallFrom == null)
            {
                _hallFrom = Time.frameCount;
                Debug.Log($"Play mode check: the hall is the game's from frame {Time.frameCount}.");
            }

            if (!_hasSounded && _hallFrom is int hall && Time.frameCount >= hall + FramesBeforeTheSounds)
            {
                _hasSounded = true;
                SoundEveryCueOnce(root);
            }

            bool isLate = !_isDone && Time.realtimeSinceStartup - _began > LongestStartSeconds;
            if (isLate)
            {
                Debug.LogError(
                    $"Play mode check: the running game did not reach its hall through the entry page in {LongestStartSeconds} s "
                    + $"(entry pictured: {_isTheEntryPictured}, touched: {_isTheEntryTouched}; {Camera.allCamerasCount} cameras; it draws everything with one).");
            }

            if (_isDone || isLate)
            {
                SessionState.EraseBool(IsRunningKey);
                EditorApplication.Exit(_gameFailed || isLate ? 1 : 0);
            }
        }

        /// <summary>The stand-in screen follows the one camera the game draws with, from the frame it has only one.</summary>
        private static void AimTheScreenAtTheOneCamera()
        {
            if (Camera.allCamerasCount != 1)
            {
                return;
            }

            _screen ??= new RenderTexture(Screen.width, Screen.height, 24);
            Camera camera = Camera.allCameras[0];
            if (camera.targetTexture != _screen)
            {
                camera.targetTexture = _screen;
            }
        }

        /// <summary>
        /// Plays every cue and every tick once through the running game's own speaker and vibrator, as the session
        /// does when the settings are on, and says what the phone's settings are: a cue the catalogue has no sound for,
        /// or a sound that fails to play, is an error and fails the check. (A batch-mode Editor may have no sound device:
        /// then nothing is heard, but the clips are still loaded and handed to the sources.)
        /// </summary>
        private static void SoundEveryCueOnce(GameRoot? root)
        {
            if (root?.Cues is not ICuePlayer cues)
            {
                Debug.LogError("Play mode check: the running game has no cue player.");
                return;
            }

            SoundCue[] everyCue = ((SoundCue[])System.Enum.GetValues(typeof(SoundCue))).Where(SoundScore.IsHeard).ToArray();
            ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
            SoundCue[] withoutASound = everyCue.Where(cue => art.SoundOf(cue) == null).ToArray();
            if (withoutASound.Length > 0)
            {
                Debug.LogError($"Play mode check: the game's catalogue has no sound for {string.Join(", ", withoutASound)}.");
            }

            foreach (SoundCue cue in everyCue)
            {
                cues.Sound(cue);
            }

            cues.Buzz(Tick.Light);
            cues.Buzz(Tick.Firm);
            DeviceSettings settings = DeviceFile.LoadOrMake(Application.persistentDataPath).Settings;
            Debug.Log($"Play mode check: the running game's player sounded all {everyCue.Length} cues that are heard and both ticks; the phone's settings are {settings}.");
        }

        /// <summary>The hall's frame a second and a half after it came, as the game drew it.</summary>
        private static void KeepTheFrames(ScriptableRenderContext context, Camera camera)
        {
            if (_isDone || _screen == null || camera.targetTexture != _screen)
            {
                return;
            }

            if (_hallFrom is int hall && Time.frameCount >= hall + FramesInTheHall)
            {
                CameraPicture.Save(_screen, PicturePath);
                int sprites = Object.FindObjectsByType<SpriteRenderer>().Length;
                Debug.Log($"Play mode check: frame {Time.frameCount} of a {_screen.width}x{_screen.height} screen with {sprites} sprites is at {PicturePath}; game errors: {_gameFailed}.");
                _isDone = true;
            }
        }
    }
}
