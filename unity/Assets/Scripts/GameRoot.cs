#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Client.Sound;
using Puzzle.Client.Stage;
using Puzzle.Client.Statistics;
using Puzzle.Client.Words;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Puzzle.Client
{
    /// <summary>
    /// The one object of the game's scene. When the game starts it shows the entry page (<see cref="EntryStage"/>)
    /// and starts the game behind it, one step a frame, so the page's bar follows the work: the levels, the museum's
    /// catalogue, the economy, and the player's session, which opens the museum's hall with its pictures in the dark.
    /// Once the player has touched the ready page and it has gone dark, the page is taken down and the hall comes out
    /// of the dark. From then on the root only passes on what the device tells it: the time of each frame, the shape
    /// of the screen, where a finger touches it, and when the game is put away. Everything that is seen is built in code by the session's stage, so the scene file
    /// stays a few lines and the Editor can build the same pictures without it. What is heard and felt goes the other
    /// way: the root holds the phone's speaker and vibrator (<see cref="SoundPlayer"/>), which the session plays its
    /// cues on; the Editor's sessions have none, and are silent. The root also holds the game's music
    /// (<see cref="MusicPlayer"/>), which no screen reaches: it follows only the player's switch, every frame, and
    /// gives way to another app's music, which the root asks the device about (<see cref="OtherAudio"/>). The root
    /// also chooses where the play statistics go (<see cref="StatisticsOfThisBuild"/>): the session asks the player
    /// first, over the hall, and lets nothing out before a yes.
    /// </summary>
    /// <remarks>
    /// The screen may change while the game runs: a tablet or an open foldable turns with the device, a fold opens or
    /// closes, a window beside another app is resized. A finger on the glass is then let go, the rule of what turns
    /// is applied again (<see cref="DeviceScreen"/>), and the page or the session is laid out anew for the new shape.
    /// Being put away and brought back needs little: Unity stops calling <see cref="Update"/> while the game is in
    /// the background, the session and its stage stay in memory as they are, and the first frame after coming back
    /// is no longer than any other (<see cref="LongestStepSeconds"/>). What is done on the way out is to let go of
    /// a finger that was down, to hold the music where it is (it fades in from there on the way back, unless another
    /// app's music plays by then), and to save progress and attempt records that could not be saved before. If the
    /// device closes the game while it is away, the attempt in progress is gone: the game comes back in the museum, and
    /// the level starts again with its next board. The entry page is shown at a cold start only: a game brought back
    /// from the background comes back where it was.
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

        /// <summary>
        /// The share of the start-up each step takes: the milliseconds the Editor's play mode timed on 2026-10-24 (each
        /// step is logged with its time on a phone too, "Start-up: ... took ... ms"). Opening the session, which paints
        /// the effects, builds the hall and loads its five pictures, is most of it.
        /// </summary>
        private const float LevelsWeight = 200f;
        private const float CatalogueWeight = 10f;
        private const float EconomyWeight = 50f;
        private const float SessionWeight = 650f;

        [SerializeField] private ArtCatalogue? _art;

        private GameSession? _session;
        private SoundPlayer? _sounds;
        private IOtherAudio? _otherAudio;
        private MusicPlayer? _music;
        private ISettingsStore? _settings;
        private ScreenShape _fittedTo;

        /// <summary>The kind of screen the rule of turning was last applied for; nothing before that, and in the Editor and on a desktop.</summary>
        private ScreenClass? _turnsAs;

        /// <summary>The entry page while it is shown; nothing once the player has left it, or when the game could not start.</summary>
        private EntryStage? _entry;

        /// <summary>The work of the start-up behind the entry page; nothing once it is done or has failed, so what its steps held is let go.</summary>
        private StartUpWork? _startUp;

        /// <summary>True once a frame of the entry page has been drawn: the start-up's first step waits for it.</summary>
        private bool _isTheEntryDrawn;

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
            SayTheScreen(_fittedTo);
            (DeviceId phone, ISettingsStore settings) = ThisPhone();
            _settings = settings;
            Haptics haptics = Haptics.OfThisDevice();
            DeviceAbilities abilities = haptics.DeviceHasNone ? DeviceAbilities.OfThisBuildWithoutVibrator : DeviceAbilities.OfThisBuild;
            _sounds = new SoundPlayer(gameObject, _art, haptics);
            _otherAudio = OtherAudio.OfThisDevice();
            _music = MusicOf(gameObject, _art, settings, _otherAudio);

            // The game speaks before anything is built, the entry page first.
            SpeakThePlayersLanguage(settings);
            _entry = EntryStage.Open(_art, Application.version, _fittedTo);
            _startUp = StartUpOf(_art, phone, settings, abilities, _sounds);

            // Last, as after every change of the screen: the page and its start-up are there whatever the device answers.
            LetTheScreenTurnAsTheDeviceAllows();
        }

        /// <summary>
        /// Where the running game's cues go: the phone's speaker and vibrator; nothing before the game has started.
        /// For the Editor's play-mode check, which sounds every cue once through it.
        /// </summary>
        public ICuePlayer? Cues => _sounds;

        /// <summary>
        /// The game's music; nothing before the game has started, or from a catalogue without music. For the Editor's
        /// music check, which follows it across the screens and the switch.
        /// </summary>
        public MusicPlayer? Music => _music;

        /// <summary>
        /// What the music asks whether another app's music plays; nothing before the game has started. In the Editor a
        /// stand-in (<see cref="OtherAudioStandIn"/>), which the music check sets playing to see the music give way.
        /// </summary>
        public IOtherAudio? OtherAudioOfTheDevice => _otherAudio;

        /// <summary>
        /// The running game's session; nothing before the player has left the entry page, though it is opened behind
        /// it. For the Editor's music check, which presses the game's buttons as a finger does.
        /// </summary>
        public GameSession? Session => _entry == null ? _session : null;

        /// <summary>The entry page while it is shown; nothing once the player has left it. For the Editor's checks, which touch it as a finger does.</summary>
        public EntryStage? Entry => _entry;

        private void Update()
        {
            ScreenShape screen = CurrentScreen();
            if (_fittedTo != screen)
            {
                // The device turned, a fold opened, a window was resized: a finger still on the glass began its gesture
                // on the screen before, whose pixels no longer mean the same places. It is let go, and followed again
                // only once it comes down anew. A fold that opened or closed may also have changed what turns; that is
                // asked last, once the page is laid out for the new shape, so a device that cannot answer leaves the
                // page fitted and is not asked again until the screen changes once more.
                LetGoOfTheFinger();
                _entry?.Fit(screen);
                _session?.Fit(screen);
                _fittedTo = screen;
                SayTheScreen(screen);
                LetTheScreenTurnAsTheDeviceAllows();
            }

            float step = Mathf.Min(Time.deltaTime, LongestStepSeconds);
            if (_entry is EntryStage entry)
            {
                FollowTheEntry(entry, step);
            }
            else if (_session is GameSession session)
            {
                FollowTheSession(session, step);
            }
            else
            {
                return; // Start failed and said why; there is nothing to show.
            }

            // After the page or the session: a switch flipped this frame is kept by now, and the music follows it in the same frame.
            _music?.Advance(step);
        }

        /// <summary>
        /// A frame of the entry page: one step of the start-up behind it (none before the page's first frame is drawn),
        /// then the touches, the page's clock and its sounds. Once the player has left it, the hall comes out of its dark.
        /// </summary>
        private void FollowTheEntry(EntryStage entry, float step)
        {
            if (_isTheEntryDrawn)
            {
                TakeTheNextStepOfTheStartUp(entry);
            }

            ReadTouches(entry);
            ReadMouse(entry);
            ReadTheWayBack(entry);
            entry.Advance(step);
            PlayWhatSounded(entry);
            _isTheEntryDrawn = true;
            if (entry.TakeTheWishToQuit())
            {
                Quit(_session);
            }

            if (entry.HasLeft)
            {
                LeaveTheEntry(entry);
            }
        }

        private void FollowTheSession(GameSession session, float step)
        {
            ReadTouches(session);
            ReadMouse(session);
            ReadTheWayBack(session);
            session.Advance(step);
            if (session.TakeTheWishToQuit())
            {
                Quit(session);
            }
        }

        /// <summary>
        /// Does the next step of the start-up and tells the entry page how far the work has come. A step that fails
        /// throws once (Unity logs what it threw) and the start-up goes no further: in the next frame the page is told,
        /// and says that the game cannot open where its bar's line was; its way back still asks whether to close the
        /// game. Once the work is done or has failed it is let go, and with it what its steps held on the way.
        /// </summary>
        private void TakeTheNextStepOfTheStartUp(EntryStage entry)
        {
            if (!(_startUp is StartUpWork work))
            {
                return;
            }

            if (work.HasFailed)
            {
                entry.TellTheStartUpFailed();
                _startUp = null;
                return;
            }

            Stopwatch watch = Stopwatch.StartNew();
            StartUpStep done = work.DoTheNext();
            Debug.Log($"Start-up: {done.Name} took {watch.ElapsedMilliseconds} ms.");
            entry.Tell(work.Done);
            if (work.IsDone)
            {
                _startUp = null;
            }
        }

        /// <summary>
        /// The player has left the ready entry page and it is dark: it is taken down, the hall that waited behind it
        /// is shown from the next frame on, out of the same dark, and the logo's picture is let go.
        /// </summary>
        private void LeaveTheEntry(EntryStage entry)
        {
            GameSession session = _session ?? throw new InvalidOperationException("The entry page was left before the session behind it was opened.");
            entry.Dispose();
            _entry = null;
            session.Front.Camera.enabled = true;
            if (_art != null)
            {
                Resources.UnloadAsset(_art.EntryLogo);
            }
        }

        /// <summary>What the entry page sounded, heard and felt as the player's settings are at this moment, as the session plays its stages' cues.</summary>
        private void PlayWhatSounded(EntryStage entry)
        {
            if (!(_settings is ISettingsStore settings) || !(_sounds is SoundPlayer cues))
            {
                return;
            }

            foreach (SoundCue cue in entry.TakeWhatSounded())
            {
                (SoundCue? sound, Tick? tick) = SoundScore.Reaching(cue, settings.Settings);
                if (sound is SoundCue heard)
                {
                    cues.Sound(heard);
                }

                if (tick is Tick felt)
                {
                    cues.Buzz(felt);
                }
            }
        }

        /// <summary>The game is put away (another app, the home screen, the screen off) or brought back.</summary>
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                LetGoOfTheFinger();
                _music?.PutAway();
                _session?.SaveProgress();
                _session?.WriteWaitingRecords();
            }
            else
            {
                _music?.ComeBack();
            }
        }

        /// <summary>
        /// Something else takes the touches (a system dialog, the notification shade): a finger that was down is not
        /// followed further. When the game has them back, the music asks at once whether the player started or stopped
        /// their own music meanwhile (the shade has its controls).
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                _music?.ComeBack();
            }
            else
            {
                LetGoOfTheFinger();
            }
        }

        private void OnDestroy()
        {
            _entry?.Dispose();
            _session?.Dispose();
            _sounds?.Dispose();
            (_otherAudio as IDisposable)?.Dispose();
        }

        /// <summary>
        /// The language the game speaks, from before anything is built: the player's choice, or the phone's language
        /// where the game offers it.
        /// </summary>
        private static void SpeakThePlayersLanguage(ISettingsStore settings)
        {
            Language phoneLanguage = PhoneLanguage.OfThisDevice();
            Language speaking = WordsPack.InEffect(settings.Settings, phoneLanguage);
            WordsPack.Speak(speaking);
            Debug.Log($"The game speaks {Languages.CodeOf(speaking)} (the phone's language: {PhoneLanguage.Tag()}; chosen: {(settings.Settings.Language is Language chosen ? Languages.CodeOf(chosen) : "none")}).");
        }

        /// <summary>
        /// The start-up behind the entry page, step by step: the levels, the museum's catalogue, the economy (whose
        /// marks the page's foot is told), and the player's session, opened in the hall out of the dark and kept unseen
        /// (its camera off) until the page is left.
        /// </summary>
        private StartUpWork StartUpOf(ArtCatalogue art, DeviceId phone, ISettingsStore settings, DeviceAbilities abilities, ICuePlayer cues)
        {
            IReadOnlyList<LevelDefinition>? levels = null;
            PaintingGallery? paintings = null;
            EconomyRules? rules = null;
            void OpenTheSessionBehindTheEntry()
            {
                _session = OpenSession(
                    levels ?? throw new InvalidOperationException("The session was opened before the levels were read."),
                    art,
                    paintings ?? throw new InvalidOperationException("The session was opened before the museum's catalogue was read."),
                    rules ?? throw new InvalidOperationException("The session was opened before the economy was read."),
                    phone,
                    settings,
                    abilities,
                    cues,
                    _fittedTo);
                _session.Front.Camera.enabled = false;
            }

            // The entry page's foot says "test sürümü" for a build that sells nothing for money, which the economy's marks say.
            void ReadTheEconomy()
            {
                EconomyRules read = EconomyPack.LoadPacked();
                rules = read;
                _entry?.TellTheBuild(read.Shop.RealMoney);
            }

            return new StartUpWork(new[]
            {
                new StartUpStep("the levels", LevelsWeight, () => levels = LevelPack.LoadPacked()),
                new StartUpStep("the museum's catalogue", CatalogueWeight, () => paintings = PaintingGallery.OfThePackedMuseum()),
                new StartUpStep("the economy", EconomyWeight, ReadTheEconomy),
                new StartUpStep("the session in the hall with its pictures", SessionWeight, OpenTheSessionBehindTheEntry),
            });
        }

        /// <summary>
        /// The session on the progress file in the game's own data folder, kept by the economy's rules on the
        /// device's clock, with the attempt log in the same folder under the phone's id and the player's settings in
        /// the device file beside it; its settings card offers a switch for each of the device's abilities, and its
        /// play statistics go where this build sends them (<see cref="StatisticsOfThisBuild"/>). If that file is there but
        /// cannot be read from the device, the player can still play: this run starts at the first level as a new
        /// player, keeps its progress in memory only, and leaves the file as it is. If a later version of the game wrote the file, this run goes on from where that file says the
        /// player stands, as far as this version can read it, and likewise leaves the file alone: the later game
        /// finds its progress as it left it. Either run keeps its progress for itself only
        /// (<see cref="ProgressInMemory.ForThisRunOnly"/>), so its shop sells nothing for money: a payment it gave
        /// would be lost when the game closes, and one the store holds from an earlier run is left unconfirmed for a
        /// run that can save it.
        /// </summary>
        private static GameSession OpenSession(
            IReadOnlyList<LevelDefinition> levels, ArtCatalogue art, PaintingGallery paintings, EconomyRules rules, DeviceId phone, ISettingsStore settings,
            DeviceAbilities abilities, ICuePlayer cues, ScreenShape screen)
        {
            var clock = new SystemWallClock();
            var attempts = new AttemptLogFile(Application.persistentDataPath, phone);
            IStore sales = UnityStore.OfThisPlatform();
            PlayStatistics statistics = StatisticsOfThisBuild();
            GameSession SessionOn(IProgressStore store) =>
                new GameSession(levels, art, paintings, rules, clock, store, attempts, settings, abilities, cues, sales, statistics, screen, SessionOpening.InTheMuseumOutOfTheDark);

            try
            {
                return SessionOn(new ProgressFile(Application.persistentDataPath, rules.StartingWallet));
            }
            catch (ProgressOfALaterGameException later)
            {
                PlayerProgress start = later.Understood ?? PlayerProgress.NewPlayer(rules.StartingWallet);
                Debug.LogError($"{later.Message} This run goes on from \"{start}\", does not save and sells nothing.");
                return SessionOn(ProgressInMemory.ForThisRunOnly(start));
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                Debug.LogError($"The saved progress cannot be read, so this run starts at the first level, does not save and sells nothing: {failure.Message}");
                return SessionOn(ProgressInMemory.ForThisRunOnly(PlayerProgress.NewPlayer(rules.StartingWallet)));
            }
        }

        /// <summary>
        /// Where the play statistics the player allows go. In a built game whose settings file holds a key of
        /// Aptabase's European region, to Aptabase (<see cref="AptabaseNotes"/>). With no key, with a key the game
        /// refuses, and always in the Editor (whose play mode the checks run, a key or not), only to the device's log,
        /// which says what would have gone: nothing leaves. The question is asked all the same, so that a build without
        /// a key shows the player what one with a key will.
        /// </summary>
        private static PlayStatistics StatisticsOfThisBuild()
        {
            StatisticsKey key = AptabaseNotes.KeyOfThisBuild();
            if (key is StatisticsKey.Refused refused)
            {
                Debug.LogError($"The play statistics' App Key is refused, so no note leaves the phone: {refused.Why}.");
            }

            if (Application.isEditor)
            {
                return PlayStatistics.Through(new NotesToTheLog(Debug.Log, $"the Editor sends nothing; the build has {key}"));
            }

            if (key is StatisticsKey.OfTheEu)
            {
                Debug.Log($"The play statistics go through {key} once the player allows them.");
                return PlayStatistics.Through(new AptabaseNotes());
            }

            return PlayStatistics.Through(new NotesToTheLog(Debug.Log, $"this build has {key}"));
        }

        /// <summary>The game's music on the root, following the player's switch and giving way to another app's music; none from a catalogue built before the music, which is said once.</summary>
        private static MusicPlayer? MusicOf(GameObject root, ArtCatalogue art, ISettingsStore settings, IOtherAudio otherAudio)
        {
            if (!(art.Music is MusicClips music))
            {
                Debug.LogWarning("The art catalogue has no music, so none is heard; run ProjectSetup.Run in the Editor's batch mode.");
                return null;
            }

            return new MusicPlayer(root, music, settings, otherAudio);
        }

        /// <summary>
        /// The random id of this phone and the player's settings, from the device file in the game's own data folder,
        /// made there on the first start. If the file can be neither read nor written, the player can still play:
        /// this run's attempt records carry an id of their own, and its settings are a first start's, kept for the run.
        /// </summary>
        private static (DeviceId Phone, ISettingsStore Settings) ThisPhone()
        {
            try
            {
                DeviceFile file = DeviceFile.LoadOrMake(Application.persistentDataPath);
                return (file.Device, file);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                Debug.LogError($"The device file can be neither read nor written, so this run's attempt records carry an id of their own and its settings are kept for the run only: {failure.Message}");
                return (DeviceDocument.NewId(), new SettingsInMemory(DeviceSettings.Default));
            }
        }

        private void LetGoOfTheFinger()
        {
            _finger = null;
            ((ITouchable?)_entry ?? _session)?.PointerCancelled();
        }

        private void ReadTouches(ITouchable session)
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

        private void FollowTheFinger(Touch touch, ITouchable session)
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
        /// itself. The game never closes on it alone: in the hall and on the entry page, where there is no way further
        /// back, it asks first whether to close the game. iOS has no way back of the kind, and never asks.
        /// </summary>
        private static void ReadTheWayBack(ITouchable session)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                session.GoBack();
            }
        }

        /// <summary>
        /// The player answered the question of the hall or of the entry page with "Çık". What could not be saved or
        /// written yet gets its last chance, as on the way out to the background (on the entry page there may be no
        /// session yet, and nothing to save); then Android closes the game. Anywhere else (the Editor's play mode above
        /// all) it stays open and says so: the question comes only from Android's way back.
        /// </summary>
        private static void Quit(GameSession? session)
        {
            session?.SaveProgress();
            session?.WriteWaitingRecords();
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.Log($"The player asked to close the game; it closes on Android only, so on {Application.platform} it stays open.");
                return;
            }

            Application.Quit();
        }

        /// <summary>A mouse is a finger too: in the Editor and on a desktop the game is played with it.</summary>
        private static void ReadMouse(ITouchable session)
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

        /// <summary>
        /// The rule of turning (<see cref="ScreenTurns"/>), applied when the game starts and again whenever the screen
        /// changes: a foldable that opens becomes a tablet that turns, one that folds a phone that stands upright.
        /// Unity is told only when the kind of screen changes; the Editor and a desktop are never told.
        /// </summary>
        private void LetTheScreenTurnAsTheDeviceAllows()
        {
            ScreenClass? screen = DeviceScreen.ClassOfThisDevice();
            if (screen is ScreenClass known && screen != _turnsAs)
            {
                DeviceScreen.LetTurn(known);
            }

            _turnsAs = screen;
        }

        /// <summary>
        /// The screen the pages are laid out for, in the device's log with the safe area it was read from: at the start
        /// and after every change, since an Android tablet or foldable opens upright and turns only once the game runs.
        /// A phone test reads there the strips a camera takes at the sides of a device held sideways (T460).
        /// </summary>
        private static void SayTheScreen(ScreenShape screen) =>
            Debug.Log($"The screen is {screen} (width x height + insets at the top, foot, left and right), from {Screen.width}x{Screen.height} with the safe area {Screen.safeArea}.");

        /// <summary>
        /// The screen as the device reports it; the safe area leaves out camera cutouts and system bars, at the top and
        /// the foot, and at the sides of a device held sideways.
        /// </summary>
        private static ScreenShape CurrentScreen()
        {
            Rect safe = Screen.safeArea;
            int topInset = Mathf.RoundToInt(Screen.height - safe.yMax);
            int bottomInset = Mathf.RoundToInt(safe.yMin);
            int leftInset = Mathf.RoundToInt(safe.xMin);
            int rightInset = Mathf.RoundToInt(Screen.width - safe.xMax);
            return new ScreenShape(Screen.width, Screen.height, topInset, bottomInset, leftInset, rightInset);
        }
    }
}
