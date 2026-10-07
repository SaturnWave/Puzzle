#nullable enable
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Starts the game's scene the way a built game starts it, lets it run for a second and a half and writes its
    /// last frame: the check that the scene file and the code it starts belong together. The pictures of the game are
    /// taken by RenderHarness, which needs no running game; this only proves that the running game gets there too.
    /// usage: Unity.exe -batchmode -projectPath unity -executeMethod Puzzle.Client.EditorTools.PlayModeCheck.Run
    /// Without -quit: the command ends the Editor itself, with exit code 1 when the game failed.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeCheck
    {
        private const string PicturePath = "../out/unity/shots/play-mode.png";
        private const int FramesToRun = 90;

        /// <summary>Entering play mode loads the scripts anew and forgets every static field; the Editor's session state survives it.</summary>
        private const string IsRunningKey = "Puzzle.Client.PlayModeCheck.IsRunning";

        /// <summary>
        /// Stands in for the screen. A batch-mode Editor has no game window, and a camera without a target is then
        /// never rendered; the game must be drawn every frame from its first one, as a phone draws it.
        /// </summary>
        private static RenderTexture? _screen;

        private static bool _gameFailed;
        private static bool _isDone;

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
