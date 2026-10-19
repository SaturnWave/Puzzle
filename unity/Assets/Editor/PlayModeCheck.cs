#nullable enable
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Play;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Starts the game's scene the way a built game starts it, lets it run for a second and a half and writes its
    /// last frame: the check that the scene file and the code it starts belong together. Halfway it sounds every cue
    /// once through the running game's player. The pictures of the game are
    /// taken by RenderHarness, which needs no running game; this only proves that the running game gets there too.
    /// usage: Unity.exe -batchmode -projectPath unity -executeMethod Puzzle.Client.EditorTools.PlayModeCheck.Run
    /// Without -quit: the command ends the Editor itself, with exit code 1 when the game failed.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeCheck
    {
        private const string PicturePath = "../out/unity/shots/play-mode.png";
        private const int FramesToRun = 90;

        /// <summary>Halfway: the game has opened its session, and errors of its sounds still fail the check.</summary>
        private const int FramesBeforeTheSounds = FramesToRun / 2;

        /// <summary>Entering play mode loads the scripts anew and forgets every static field; the Editor's session state survives it.</summary>
        private const string IsRunningKey = "Puzzle.Client.PlayModeCheck.IsRunning";

        /// <summary>
        /// Stands in for the screen. A batch-mode Editor has no game window, and a camera without a target is then
        /// never rendered; the game must be drawn every frame from its first one, as a phone draws it.
        /// </summary>
        private static RenderTexture? _screen;

        private static bool _gameFailed;
        private static bool _isDone;
        private static bool _hasSounded;

        static PlayModeCheck()
        {
            if (SessionState.GetBool(IsRunningKey, false))
            {
                Application.logMessageReceived += NoteTheGamesErrors;
                EditorApplication.update += WatchTheGame;
                RenderPipelineManager.endCameraRendering += KeepTheLastFrame;
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

            if (_screen == null && Camera.allCamerasCount == 1)
            {
                _screen = new RenderTexture(Screen.width, Screen.height, 24);
                Camera.allCameras[0].targetTexture = _screen;
            }

            if (!_hasSounded && Time.frameCount >= FramesBeforeTheSounds)
            {
                _hasSounded = true;
                SoundEveryCueOnce();
            }

            bool hasNoPictureInTime = _screen == null && Time.frameCount > FramesToRun;
            if (hasNoPictureInTime)
            {
                Debug.LogError($"Play mode check: the running game has {Camera.allCamerasCount} cameras; it draws everything with one.");
            }

            if (_isDone || hasNoPictureInTime)
            {
                SessionState.EraseBool(IsRunningKey);
                EditorApplication.Exit(_gameFailed || hasNoPictureInTime ? 1 : 0);
            }
        }

        /// <summary>
        /// Plays every cue and every tick once through the running game's own speaker and vibrator, as the session
        /// does when the settings are on, and says what the phone's settings are: a cue the catalogue has no sound for,
        /// or a sound that fails to play, is an error and fails the check. (A batch-mode Editor may have no sound device:
        /// then nothing is heard, but the clips are still loaded and handed to the sources.)
        /// </summary>
        private static void SoundEveryCueOnce()
        {
            GameRoot? root = Object.FindAnyObjectByType<GameRoot>();
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

        private static void KeepTheLastFrame(ScriptableRenderContext context, Camera camera)
        {
            if (_isDone || _screen == null || camera.targetTexture != _screen || Time.frameCount < FramesToRun)
            {
                return;
            }

            CameraPicture.Save(_screen, PicturePath);
            int sprites = Object.FindObjectsByType<SpriteRenderer>().Length;
            Debug.Log($"Play mode check: frame {Time.frameCount} of a {_screen.width}x{_screen.height} screen with {sprites} sprites is at {PicturePath}; game errors: {_gameFailed}.");
            _isDone = true;
        }
    }
}
