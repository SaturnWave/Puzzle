#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Museum;
using UnityEngine;

namespace Puzzle.Client.Flow
{
    /// <summary>Where a session begins.</summary>
    public enum SessionOpening
    {
        /// <summary>In the museum's hall, as the game starts for a player.</summary>
        InTheMuseum,

        /// <summary>At the board of the level the player stands on: what the Editor's board studies begin with.</summary>
        AtTheBoard,
    }

    /// <summary>
    /// A player's way through the game: the stage they are looking at (the museum, the board of the attempt they
    /// are in, or a painting's jigsaw) and the progress that decides what comes next. The game opens in the
    /// museum's hall; its play button opens the level the player stands on. A win opens the next level of the pack,
    /// a loss the same level again with the next seed of its calibrated pool, so a retry is another board of the
    /// same measured difficulty (the web test server deals attempts the same way); from either result's card the
    /// player can also go back to the museum. A painting's jigsaw opens from a won level's card and from the
    /// museum's pages while pieces wait; its way back leads to where it was opened from, but for the jigsaw in
    /// which the fifth painting of a room became whole: from there the player is led to the hall, which shows them
    /// the restored room before it moves on. A level can be left for the museum at any time (the board asks first).
    /// Like the stages it owns,
    /// the session only moves when it is told the time (<see cref="Advance"/>): the running game tells it every
    /// frame, the Editor's storyboards whenever they want.
    /// </summary>
    /// <remarks>
    /// Progress is kept in the store the moment it changes: when the first move of an attempt is played (the attempt
    /// then counts, so its board is not dealt again), and when the engine says a level is won (before the win is
    /// even shown, so closing the game during the celebration does not lose it), and when a jigsaw piece has
    /// dropped into its place; pieces that fly to their places together are kept together, once the last of them
    /// has landed, so forty pieces are one save and not forty. A piece that is won and not placed needs no
    /// saving: the levels won say that it waits, so it can never be lost. The attempt in progress itself
    /// lives in memory only: a game that is closed in the middle of a level comes back in the museum, and the level
    /// starts again with its next board. A store that cannot be written does not stop the game; the progress is kept
    /// here and saved again at the next change, or when <see cref="SaveProgress"/> is called.
    /// <para>
    /// One stage is seen at a time, and each has its own camera: the stage that is left is taken down before the
    /// next one is built, in the dark both of them share, so the change is not seen. The one exception is a board
    /// whose card led to the jigsaw: it steps aside, out of sight, and comes back with its card when the player
    /// returns.
    /// </para>
    /// </remarks>
    public sealed class GameSession : IDisposable
    {
        private readonly IReadOnlyList<LevelDefinition> _levels;
        private readonly ArtCatalogue _art;
        private readonly PaintingGallery _paintings;
        private readonly IProgressStore _store;

        /// <summary>What is thrown over the board, painted once for all the stages of this session: painting it anew for every level would hold up each level's first frame.</summary>
        private readonly EffectArt _effectArt;
        private ScreenShape _screen;
        private IStage _front;
        private PlayerProgress _progress;

        /// <summary>What the store was last given: the progress is saved again when it differs from this.</summary>
        private PlayerProgress _saved;

        /// <summary>How many moves of the open attempt the progress already knows of.</summary>
        private int _movesKept;

        /// <summary>What winning the open attempt gives: the painting its card's jigsaw button leads to. Nothing in the museum, and for a level that gives nothing.</summary>
        private PieceReward? _rewardOfTheBoard;

        /// <summary>Where the way back from the open jigsaw leads; nothing while no jigsaw is open.</summary>
        private WayBack? _wayBack;

        /// <summary>How many pieces of the open jigsaw the progress already knows to be in place.</summary>
        private int _piecesKept;

        /// <summary>The room whose fifth painting became whole on the open jigsaw; nothing otherwise. The hall shows it restored when the jigsaw is left.</summary>
        private RoomNumber? _restoredJustNow;

        /// <summary>Loads the player's progress and opens the game where the opening says, laid out for a screen.</summary>
        /// <param name="levels">The levels in the order they are played.</param>
        /// <param name="paintings">The museum's paintings, for the museum's pages and for the pieces a win gives.</param>
        /// <exception cref="ArgumentException">There is no level to play.</exception>
        /// <exception cref="IOException">The store's progress is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read the store.</exception>
        public GameSession(
            IReadOnlyList<LevelDefinition> levels, ArtCatalogue art, PaintingGallery paintings, IProgressStore store, ScreenShape screen, SessionOpening opening)
        {
            if (levels.Count == 0)
            {
                throw new ArgumentException("A session needs a level to play.", nameof(levels));
            }

            _levels = levels;
            _art = art;
            _paintings = paintings;
            _store = store;
            _screen = screen;
            _progress = store.Load();
            _saved = _progress;
            _effectArt = EffectArt.Paint();
            _front = opening switch
            {
                SessionOpening.InTheMuseum => OpenTheMuseum(StageOpening.InTheLitRoom, MuseumDoor.Home),
                SessionOpening.AtTheBoard => OpenTheBoard(StageOpening.InTheLitRoom),
                _ => throw new ArgumentOutOfRangeException(nameof(opening), opening, "A session opens in the museum or at the board."),
            };
        }

        /// <summary>What the player is looking at: the museum, a level's board, or a painting's jigsaw. It changes as the player goes from one to the other.</summary>
        public IStage Front => _front;

        /// <summary>The stage of the jigsaw the player is at; nothing while the player is elsewhere.</summary>
        public JigsawStage? Jigsaw => _front as JigsawStage;

        /// <summary>The museum's stage; nothing while the player is at a board.</summary>
        public MuseumStage? Museum => _front as MuseumStage;

        /// <summary>The stage of the attempt the player is in; nothing while the player is in the museum or at a jigsaw.</summary>
        public GameStage? Board => _front as GameStage;

        /// <summary>The stage of the attempt the player is in, for whoever knows the player is at a board. It changes when the player goes on from a result.</summary>
        /// <exception cref="InvalidOperationException">The player is in the museum: no board is open.</exception>
        public GameStage Stage => Board ?? throw new InvalidOperationException("The player is in the museum; no board is open.");

        /// <summary>The player's progress as it stands now, saved or not.</summary>
        public PlayerProgress Progress => _progress;

        /// <summary>True while the store does not hold the progress, because the last save failed.</summary>
        public bool HasUnsavedProgress => !_progress.Equals(_saved);

        /// <summary>Lays the stage out for a screen, and every later stage too; call again whenever the screen changes.</summary>
        public void Fit(ScreenShape screen)
        {
            _screen = screen;
            _front.Fit(screen);
        }

        /// <summary>
        /// Moves the stage's clock on. A touch that waited for the board to come to rest may play its move here,
        /// which is kept as any other move is. When the player has left the stage (through the museum's play button,
        /// or a button of a result's card), what they left it for is opened here.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            _front.Advance(deltaSeconds);
            KeepWhatWasPlayed();
            KeepWhatWasPlaced();
            FollowThePlayer();
        }

        /// <summary>Plays a move on the board's stage, as <see cref="GameStage.TryPlay"/> does, and keeps what it means for the progress.</summary>
        /// <exception cref="InvalidOperationException">The player is in the museum: no board is open.</exception>
        public bool TryPlay(Move move)
        {
            bool isPlayed = Stage.TryPlay(move);
            KeepWhatWasPlayed();
            return isPlayed;
        }

        /// <summary>A finger came down on the screen. Pixels are counted from the screen's lower left corner.</summary>
        public void PointerDown(Vector2 pixel) => _front.PointerDown(pixel);

        /// <summary>The finger moved while it was down; on a board, a drag towards a neighbouring cell plays a move at once when the board is at rest.</summary>
        public void PointerMoved(Vector2 pixel)
        {
            _front.PointerMoved(pixel);
            KeepWhatWasPlayed();
        }

        /// <summary>The finger lifted; on a board, a tap on a neighbour of the picked piece, or on a special piece, plays a move.</summary>
        public void PointerUp()
        {
            _front.PointerUp();
            KeepWhatWasPlayed();
        }

        /// <summary>The touch was taken away by the device, or the game lost the screen while a finger was down.</summary>
        public void PointerCancelled() => _front.PointerCancelled();

        /// <summary>The player asked for the way back, by the device's back button or gesture: the stage they look at takes it.</summary>
        public void GoBack() => _front.GoBack();

        /// <summary>
        /// Saves the progress if the store does not hold it yet. The game calls this when it is put away, so a save
        /// that failed while playing gets another chance before the game may be closed.
        /// </summary>
        public void SaveProgress()
        {
            TakeInThePiecesInPlace();
            if (!HasUnsavedProgress)
            {
                return;
            }

            try
            {
                _store.Save(_progress);
                _saved = _progress;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                // Playing goes on: the progress is still held here, and the next change or the next pause saves it again.
                Debug.LogWarning($"The progress ({_progress}) could not be saved and will be saved again later: {failure.Message}");
            }
        }

        public void Dispose()
        {
            _front.Dispose();
            (_wayBack as WayBack.ToTheCard)?.Board.Dispose();
            _effectArt.Dispose();
        }

        /// <summary>The level the player stands on.</summary>
        private LevelDefinition LevelToPlay => _levels[_progress.LevelIndexIn(_levels.Count)];

        /// <summary>
        /// If the player has left the stage that is open, opens what they left it for, out of the dark the stage
        /// left behind: the board of their level from the museum; from a result's card, the next board or the museum.
        /// </summary>
        private void FollowThePlayer()
        {
            switch (_front)
            {
                case MuseumStage { PlayerLeftFor: MuseumExit.ToPlay _ }:
                    ChangeTheStage(() => OpenTheBoard(StageOpening.OutOfTheDark));

                    // The hall's paintings are not seen on a board; the result's card loads the one it shows by itself.
                    PaintingGallery.LetGoOfUnusedPictures();
                    break;
                case MuseumStage { PlayerLeftFor: MuseumExit.ToAJigsaw jigsaw } museum:
                    _wayBack = new WayBack.ToTheMuseum(museum.Door);
                    ChangeTheStage(() => OpenTheJigsaw(jigsaw.Painting));
                    break;
                case GameStage { PlayerChose: ResultChoice.GoOn }:
                    ChangeTheStage(() => OpenTheBoard(StageOpening.OutOfTheDark));
                    break;
                case GameStage { PlayerChose: ResultChoice.ToTheMuseum }:
                    ChangeTheStage(() => OpenTheMuseum(StageOpening.OutOfTheDark, MuseumDoor.Home));
                    break;
                case GameStage { PlayerChose: ResultChoice.PlaceThePieces } board:
                    LeaveTheCardForTheJigsaw(board);
                    break;
                case JigsawStage { PlayerLeft: true } jigsaw:
                    LeaveTheJigsaw(jigsaw);
                    break;
            }
        }

        /// <summary>
        /// The card's blue button was pressed: the board steps aside as it is, and the jigsaw of the painting the
        /// level belongs to opens in front of it.
        /// </summary>
        private void LeaveTheCardForTheJigsaw(GameStage board)
        {
            PaintingNumber painting = (_rewardOfTheBoard ?? throw new InvalidOperationException("A card led to the jigsaw of a level that gives no piece.")).Painting;
            board.StepAside();
            _wayBack = new WayBack.ToTheCard(board);
            _front = OpenTheJigsaw(painting);
        }

        /// <summary>The player left the jigsaw: it is taken down, and what it was opened from comes out of the dark it left behind.</summary>
        private void LeaveTheJigsaw(JigsawStage jigsaw)
        {
            PaintingNumber painting = jigsaw.Table.Painting;
            WayBack back = _wayBack ?? throw new InvalidOperationException("A jigsaw is open that was opened from nowhere.");
            SaveProgress(); // whatever lies in place is the player's, also a piece that landed in the step that left
            _wayBack = null;
            jigsaw.Dispose();
            if (_restoredJustNow is RoomNumber restored)
            {
                // The room is whole: wherever the jigsaw was opened from, the hall shows it first.
                _restoredJustNow = null;
                (back as WayBack.ToTheCard)?.Board.Dispose();
                _front = OpenTheMuseum(StageOpening.OutOfTheDark, MuseumDoor.AtTheRestored(restored));
                PaintingGallery.LetGoOfUnusedPictures();
                return;
            }

            switch (back)
            {
                case WayBack.ToTheCard toTheCard:
                    _front = toTheCard.Board;
                    toTheCard.Board.Fit(_screen); // the screen may have changed while the board stood aside
                    toTheCard.Board.ComeBackToTheCard(_progress.Museum.WaitingIn(painting).Count);
                    break;
                case WayBack.ToTheMuseum toTheMuseum:
                    _front = OpenTheMuseum(StageOpening.OutOfTheDark, toTheMuseum.Door);
                    break;
                default:
                    throw new InvalidOperationException($"A way back of the kind {back.GetType().Name} leads nowhere.");
            }

            PaintingGallery.LetGoOfUnusedPictures();
        }

        /// <summary>Takes the open stage down and then builds the next one: never two cameras at once.</summary>
        private void ChangeTheStage(Func<IStage> openTheNext)
        {
            _front.Dispose();
            _front = openTheNext();
        }

        /// <summary>Opens the museum as the progress has it, on one of its pages.</summary>
        private IStage OpenTheMuseum(StageOpening opening, MuseumDoor door)
        {
            var visit = new MuseumVisit(_progress.Museum, _paintings.Catalogue, LevelToPlay.Id.Value, hasWonEveryLevel: _progress.LevelsWon >= _levels.Count);
            MuseumStage museum = MuseumStage.Open(visit, _paintings, _art, door, opening);
            museum.Fit(_screen); // at once: the first frame is drawn before the next step of the clock
            _rewardOfTheBoard = null;
            return museum;
        }

        /// <summary>Opens the jigsaw of a painting as the progress has it, out of the dark the stage before left behind.</summary>
        private IStage OpenTheJigsaw(PaintingNumber painting)
        {
            JigsawStage jigsaw = JigsawStage.Open(painting, _progress.Museum, _paintings, _art, StageOpening.OutOfTheDark);
            jigsaw.Fit(_screen); // at once: the first frame is drawn before the next step of the clock
            _piecesKept = jigsaw.Table.InPlace.Count;
            return jigsaw;
        }

        /// <summary>
        /// Opens the stage of the attempt the progress asks for: the level the player stands on, with the seed its
        /// pool has for this try.
        /// </summary>
        private IStage OpenTheBoard(StageOpening opening)
        {
            _rewardOfTheBoard = PieceReward.ForWinning(_progress, _levels.Count, _paintings.Catalogue);
            var brief = new AttemptBrief(_progress.AttemptsOnLevel + 1, _progress.IsOnLastLevelOf(_levels.Count), _rewardOfTheBoard);
            GameStage stage = GameStage.Open(LevelStart.Attempt(LevelToPlay, _progress.AttemptsOnLevel), _art, _effectArt, _paintings, brief, opening);
            stage.Fit(_screen); // at once: the first frame of a level is drawn before the next step of the clock
            _movesKept = 0;
            return stage;
        }

        /// <summary>
        /// If the board's game has moved on since the progress last looked: its first move makes the attempt count,
        /// and a move that won the level moves the player on to the next. Either is saved at once.
        /// </summary>
        private void KeepWhatWasPlayed()
        {
            if (!(Board is GameStage board) || board.Game.MovesUsed == _movesKept)
            {
                return;
            }

            if (_movesKept == 0)
            {
                _progress = _progress.WithAttemptStarted();
            }

            if (board.Game.Status == GameStatus.Won)
            {
                _progress = _progress.WithLevelWon(_levels.Count);
            }

            _movesKept = board.Game.MovesUsed;
            SaveProgress();
        }

        /// <summary>
        /// If pieces have dropped into their place on the open jigsaw since the progress last looked, they are in
        /// place for good: saved at once, unless more pieces are still on their way, which are then waited for and
        /// saved with them.
        /// </summary>
        private void KeepWhatWasPlaced()
        {
            if (_front is JigsawStage jigsaw && !jigsaw.HasPiecesOnTheirWay)
            {
                SaveProgress();
            }
        }

        /// <summary>The pieces that lie in place on the open jigsaw become the progress' own; a room whose last painting they complete is remembered for the hall.</summary>
        private void TakeInThePiecesInPlace()
        {
            // Asked in every step of the clock: nothing is looked through while no piece has come into place since the last time.
            if (!(_front is JigsawStage jigsaw) || jigsaw.Table.InPlace.Count == _piecesKept)
            {
                return;
            }

            PaintingNumber painting = jigsaw.Table.Painting;
            PlayerProgress before = _progress;
            _piecesKept = jigsaw.Table.InPlace.Count;
            foreach (int piece in jigsaw.Table.InPlace)
            {
                if (!_progress.Museum.Placed.IsPlaced(painting, piece))
                {
                    _progress = _progress.WithPiecePlaced(painting, piece);
                }
            }

            bool isNewlyPlaced = !ReferenceEquals(before, _progress);
            if (isNewlyPlaced && !IsRestored(painting.Room, before) && IsRestored(painting.Room, _progress))
            {
                _restoredJustNow = painting.Room;
            }
        }

        private bool IsRestored(RoomNumber room, PlayerProgress progress) => RoomWall.Of(room, progress.Museum, _paintings.Catalogue).IsRestored;

        /// <summary>Where the way back from a jigsaw leads. A closed set (private constructor).</summary>
        private abstract class WayBack
        {
            private WayBack()
            {
            }

            /// <summary>To the page of the museum the jigsaw was opened from.</summary>
            public sealed class ToTheMuseum : WayBack
            {
                public ToTheMuseum(MuseumDoor door)
                {
                    Door = door;
                }

                public MuseumDoor Door { get; }
            }

            /// <summary>To the card of the won level whose button led to the jigsaw; its board stands aside meanwhile.</summary>
            public sealed class ToTheCard : WayBack
            {
                public ToTheCard(GameStage board)
                {
                    Board = board;
                }

                public GameStage Board { get; }
            }
        }
    }
}
