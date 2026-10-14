#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using UnityEngine;

namespace Puzzle.Client
{
    /// <summary>
    /// The one object of the game's scene. It opens the player's session, in the museum's hall, when the game starts
    /// and then only passes on what the device tells it: the time of each frame, the shape of the screen, where a finger touches it, and
    /// when the game is put away. Everything that is seen is built in code by the session's stage, so the scene file
    /// stays a few lines and the Editor can build the same pictures without it.
    /// </summary>
    /// <remarks>
    /// Being put away and brought back needs little: Unity stops calling <see cref="Update"/> while the game is in
    /// the background, the session and its stage stay in memory as they are, and the first frame after coming back
    /// is no longer than any other (<see cref="LongestStepSeconds"/>). What is done on the way out is to let go of
    /// a finger that was down, and to save progress and attempt records that could not be saved before. If the device
    /// closes the game while it is away, the attempt in progress is gone: the game comes back in the museum, and the
    /// level starts again with its next board.
    /// </remarks>
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>The name of the catalogue's field, for the Editor script that writes the scene.</summary>
        public const string ArtField = nameof(_art);

        /// <summary>Phones default to 30 frames a second; pieces that fall and pop need twice that to look smooth.</summary>
        private const int FramesPerSecond = 60;

        /// <summary>
        /// A frame that took longer than this is a hitch (the first frame after loading always is one, and so is the
        /// first after the game comes back from the background). The stage's clock does not jump over it: an
        /// animation goes on where it was instead of skipping to its end.
        /// </summary>
        private const float LongestStepSeconds = 1f / 20f;

        private const int MouseButton = 0;

        [SerializeField] private ArtCatalogue? _art;

        private GameSession? _session;
        private ScreenShape _fittedTo;

        /// <summary>
        /// The finger the game follows. A second finger that comes down meanwhile is not followed; the session is
        /// only told that it came down, which ends a won game's finale as any touch does.
        /// </summary>
        private int? _finger;

        private void Start()
        {
            if (_art == null)
            {
                throw new InvalidOperationException("The scene's game root has no art catalogue; run ProjectSetup.Run in the Editor's batch mode.");
            }

            Application.targetFrameRate = FramesPerSecond;

            // A touch is read as a touch; without this the device would report it a second time as a mouse.
            Input.simulateMouseWithTouches = false;
            _fittedTo = CurrentScreen();
            _session = OpenSession(LevelPack.LoadPacked(), _art, PaintingGallery.OfThePackedMuseum(), EconomyPack.LoadPacked(), _fittedTo);
        }

        private void Update()
        {
            if (_session == null)
            {
                return; // Start failed and said why; there is nothing to show.
            }

            ScreenShape screen = CurrentScreen();
            if (_fittedTo != screen)
            {
                _session.Fit(screen);
                _fittedTo = screen;
            }

            ReadTouches(_session);
            ReadMouse(_session);
            ReadTheWayBack(_session);
            _session.Advance(Mathf.Min(Time.deltaTime, LongestStepSeconds));
        }

        /// <summary>The game is put away (another app, the home screen, the screen off) or brought back.</summary>
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                LetGoOfTheFinger();
                _session?.SaveProgress();
                _session?.WriteWaitingRecords();
            }
        }

        /// <summary>Something else takes the touches (a system dialog, the notification shade): a finger that was down is not followed further.</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                LetGoOfTheFinger();
            }
        }

        private void OnDestroy() => _session?.Dispose();

        /// <summary>
        /// The session on the progress file in the game's own data folder, kept by the economy's rules on the
        /// device's clock, with the attempt log in the same folder under the phone's id. If that file is there but
        /// cannot be read from the device, the player can still play: this run starts at the first level as a new
        /// player, keeps its progress in memory only, and leaves the file as it is. If a later version of the game wrote the file, this run goes on from where that file says the
        /// player stands, as far as this version can read it, and likewise leaves the file alone: the later game
        /// finds its progress as it left it.
        /// </summary>
        private static GameSession OpenSession(
            IReadOnlyList<LevelDefinition> levels, ArtCatalogue art, PaintingGallery paintings, EconomyRules rules, ScreenShape screen)
        {
            var clock = new SystemWallClock();
            var attempts = new AttemptLogFile(Application.persistentDataPath, IdOfThisPhone());
            GameSession SessionOn(IProgressStore store) => new GameSession(levels, art, paintings, rules, clock, store, attempts, screen, SessionOpening.InTheMuseum);

            try
            {
                return SessionOn(new ProgressFile(Application.persistentDataPath, rules.StartingWallet));
            }
            catch (ProgressOfALaterGameException later)
            {
                PlayerProgress start = later.Understood ?? PlayerProgress.NewPlayer(rules.StartingWallet);
                Debug.LogError($"{later.Message} This run goes on from \"{start}\" and does not save.");
                return SessionOn(new ProgressInMemory(start));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                Debug.LogError($"The saved progress cannot be read, so this run starts at the first level and does not save: {failure.Message}");
                return SessionOn(new ProgressInMemory(PlayerProgress.NewPlayer(rules.StartingWallet)));
            }
        }

        /// <summary>
        /// The random id of this phone, from the device file in the game's own data folder, made there on the first
        /// start. If the file can be neither read nor written, the player can still play: this run's attempt records
        /// carry an id of their own.
        /// </summary>
        private static DeviceId IdOfThisPhone()
        {
            try
            {
                return new DeviceFile(Application.persistentDataPath).LoadOrMake();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                Debug.LogError($"The device file can be neither read nor written, so this run's attempt records carry an id of their own: {failure.Message}");
                return DeviceDocument.NewId();
            }
        }

        private void LetGoOfTheFinger()
        {
            _finger = null;
            _session?.PointerCancelled();
        }

        private void ReadTouches(GameSession session)
        {
            for (int index = 0; index < Input.touchCount; index++)
            {
                Touch touch = Input.GetTouch(index);
                if (_finger == null && touch.phase == TouchPhase.Began)
                {
                    _finger = touch.fingerId;
                    session.PointerDown(touch.position);
                }
                else if (touch.fingerId == _finger)
                {
                    FollowTheFinger(touch, session);
                }
                else if (touch.phase == TouchPhase.Began)
                {
                    session.AnotherFingerDown();
                }
            }
        }

        private void FollowTheFinger(Touch touch, GameSession session)
        {
            switch (touch.phase)
            {
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    session.PointerMoved(touch.position);
                    break;
                case TouchPhase.Ended:
                    session.PointerMoved(touch.position);
                    session.PointerUp();
                    _finger = null;
                    break;
                case TouchPhase.Canceled:
                    session.PointerCancelled();
                    _finger = null;
                    break;
                case TouchPhase.Began:
                    break; // the finger is already followed; a device does not begin it twice
                default:
                    throw new InvalidOperationException($"A touch in the phase {touch.phase} is not known to the game.");
            }
        }

        /// <summary>
        /// Android reports its back button and its back gesture as the Escape key; on a desktop it is that key
        /// itself. The game never closes on it: in the hall, where there is no way back, it does nothing.
        /// </summary>
        private static void ReadTheWayBack(GameSession session)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                session.GoBack();
            }
        }

        /// <summary>A mouse is a finger too: in the Editor and on a desktop the game is played with it.</summary>
        private static void ReadMouse(GameSession session)
        {
            if (Input.GetMouseButtonDown(MouseButton))
            {
                session.PointerDown(Input.mousePosition);
            }
            else if (Input.GetMouseButton(MouseButton))
            {
                session.PointerMoved(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(MouseButton))
            {
                session.PointerUp();
            }
        }

        /// <summary>The screen as the device reports it; the safe area leaves out camera cutouts and system bars.</summary>
        private static ScreenShape CurrentScreen()
        {
            Rect safe = Screen.safeArea;
            int topInset = Mathf.RoundToInt(Screen.height - safe.yMax);
            int bottomInset = Mathf.RoundToInt(safe.yMin);
            return new ScreenShape(Screen.width, Screen.height, topInset, bottomInset);
        }
    }
}
