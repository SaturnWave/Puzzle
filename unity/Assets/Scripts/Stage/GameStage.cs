#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Boards;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Stage
{
    /// <summary>How a stage is first seen.</summary>
    public enum StageOpening
    {
        /// <summary>In the lit room, as the game's first board is.</summary>
        InTheLitRoom,

        /// <summary>Out of the dark a result's card left behind: the room lights up while the board comes on.</summary>
        OutOfTheDark,
    }

    /// <summary>
    /// Everything of one game that is seen and touched: the camera, the gallery wall with its lamps, the plaques, the
    /// board in its frame, the shelf, and the game they show. The stage has a clock of its own that only
    /// <see cref="Advance"/> moves. The running game feeds it the time of every frame; the Editor's screenshot and
    /// storyboard commands feed it whatever moment they want a picture of, with no game running. Both see the same stage.
    /// </summary>
    /// <remarks>
    /// A move is played on the engine at once and shown afterwards: the engine's events of the move become a script
    /// (<see cref="TurnChoreographer"/>) that is played on the stage's timeline. While a turn is shown no move is
    /// played, but the finger is still followed: what it does then waits, and is acted on the moment the board has
    /// come to rest, so a player who is a little ahead of the pieces does not have to touch twice
    /// (<see cref="Tempo.TouchWaits"/>).
    /// <para>
    /// When a turn is over, what is seen must be the engine's board and the engine's numbers. That is checked after
    /// every turn, and it is also the stage's last resort: a turn that could not be shown, or that did not end on
    /// the engine's board, is reported (<see cref="LastTurnFaults"/>, and an error in the log) and the board is laid
    /// out anew from the engine's. A mistake in the showing of a turn costs that turn's picture, never the game.
    /// </para>
    /// <para>
    /// A stage shows one attempt and no more. When its game is decided it darkens the room and hangs the result's
    /// card; once the player has pressed one of the card's buttons and the card is gone, <see cref="PlayerChose"/>
    /// says where they go on to, and whoever owns the stage opens what comes next: another board, or the museum.
    /// </para>
    /// <para>
    /// A game that is won with moves left over has a finale before its card: when the winning move has come to
    /// rest, every move left flies from the medallion onto a gem, which turns into a rocket; the rockets fire, the
    /// board settles, and then the card comes as after any win. It is shown as a turn is: the engine plays it at
    /// once (Game.PlayFinale), and its events become a script on the same timeline, so the game is in its final
    /// state from the finale's first moment. That is why a finger that comes down anywhere, or the way back, ends
    /// it on the spot at no loss: the engine's board is laid out and the card comes. A finale that cannot be shown,
    /// or does not end on the engine's board, is a fault like a turn's and costs its picture, never the card. The
    /// win itself is kept by whoever owns the stage the moment the winning move is played, long before any of this.
    /// </para>
    /// <para>
    /// A level can be left at any time. The round button on the shelf's left end, and the device's way back
    /// (<see cref="GoBack"/>), ask first: a card comes down that says what leaving costs, and the player stays or
    /// goes to the museum. Asking first matters on a phone, where a swipe from the screen's edge is the way back
    /// and the board reaches that edge. Under a result's card the same round button stays lit and leads to the
    /// museum, so every card has that way out whatever its two buttons offer.
    /// </para>
    /// </remarks>
    public sealed class GameStage : IStage
    {
        /// <summary>How far two pieces go towards each other's cell when their trade is refused, and how far a piece that has no partner to trade with.</summary>
        private const float RefusedTradeReach = 0.42f;
        private const float RefusedNudgeReach = 0.14f;

        /// <summary>The skipped finale's board is shown under one blink of light, this many times the tray's longer side across.</summary>
        private const float SkipBlinkAcross = 1.5f;

        /// <summary>What a finger can press on the stage beside the board and a card's buttons. A closed set.</summary>
        private enum StageControl
        {
            /// <summary>The round button on the shelf: out of the level.</summary>
            Leave,
        }

        /// <summary>What the stage's timeline is showing of the game. A closed set.</summary>
        private enum Playback
        {
            /// <summary>Nothing of the game: the board is at rest, or only a card comes and goes.</summary>
            Nothing,

            /// <summary>The move that was played last.</summary>
            ATurn,

            /// <summary>The finale of the won game.</summary>
            TheFinale,
        }

        private readonly GameObject _root;
        private readonly ArtCatalogue _art;
        private readonly PaintingGallery _paintings;
        private readonly AttemptBrief _brief;
        private readonly Game _game;
        private readonly PieceLedger _ledger;
        private readonly BoardShape _shape;
        private readonly BoardFootprint _footprint;
        private readonly Wall _wall;
        private readonly WallLamps _lamps;
        private readonly Hud _hud;
        private readonly BoardFrame _frame;
        private readonly BoardView _board;
        private readonly Shelf _shelf;
        private readonly Veil _veil;
        private readonly EffectArt _effectArt;
        private readonly PickedMark _pickedMark;
        private readonly BoardEffects _effects;
        private readonly TurnPlayer _turnPlayer;
        private readonly BoardPointer _pointer;
        private readonly TweenSet _tweens = new TweenSet();
        private readonly Timeline _timeline = new Timeline();
        private readonly DiscButton _leave;
        private readonly PressSet<StageControl> _controls = new PressSet<StageControl>();

        private ScreenLayout? _layout;

        /// <summary>The card that hangs in front of the board: the game's result, or the question whether the player means to leave. Nothing while the game is simply being played.</summary>
        private ResultPanel? _result;

        /// <summary>True while the card hangs in its place: from the moment it has come down until a button of it is pressed.</summary>
        private bool _isCardDown;

        /// <summary>True when the player asked to leave while the board was still moving: the question is put once it has come to rest.</summary>
        private bool _isAskedToLeave;
        private HudFigures _figures;
        private Playback _playing = Playback.Nothing;

        /// <summary>What the engine did in the won game's finale; nothing until it has played it.</summary>
        private FinaleOutcome? _finale;

        /// <summary>How fast the finale's script is played against the stage's clock, and where on the timeline that script began; nothing until a finale is shown.</summary>
        private FinalePace? _finalePace;
        private float _finaleBeganAt;
        private float _clock;

        /// <summary>What the finger did while the board was still moving, and when; nothing when no gesture waits.</summary>
        private WaitingGesture? _waiting;

        /// <summary>What went wrong in the showing of the turn that is on, for <see cref="LastTurnFaults"/>.</summary>
        private readonly List<string> _faultsOfTheTurn = new List<string>();

        private GameStage(Game game, ArtCatalogue art, EffectArt effectArt, PaintingGallery paintings, AttemptBrief brief)
        {
            bool hasItemsToDrop = game.Level.Goals.Any(goal => goal is GoalSpec.DropItems);
            _art = art;
            _paintings = paintings;
            _brief = brief;
            _game = game;
            _ledger = PieceLedger.Of(game.Board);
            _shape = game.Board.Shape;
            _footprint = new BoardFootprint(_shape.Width, _shape.Height, hasExits: hasItemsToDrop);
            _figures = HudFigures.Of(game);
            _root = new GameObject("Stage");
            Camera = StageCamera.Add(_root.transform);
            _wall = Wall.Create(_root.transform, art);
            _lamps = WallLamps.Create(_root.transform, art);
            _hud = Hud.Create(_root.transform, art, game.Level.Id.Value);
            _hud.Show(_figures);
            _frame = BoardFrame.Create(_root.transform, art, _shape);
            _board = BoardView.Create(_root.transform, art, _ledger, showExits: hasItemsToDrop);
            _shelf = Shelf.Create(_root.transform, art);
            _shelf.Say(AttemptWords.OnTheShelf(brief));
            _leave = new DiscButton("Leave", _root.transform, art, DiscGlyph.Back, DrawOrder.ShelfButton);
            _controls.Add(_leave, StageControl.Leave);
            _veil = Veil.Create(_root.transform, art);
            _effectArt = effectArt;
            _pickedMark = new PickedMark(_board.Root, _effectArt, art);
            _effects = new BoardEffects(_board.Root, _shape, _effectArt, art, _timeline);
            _turnPlayer = new TurnPlayer(_board, _effects, _timeline, CountTheMove, CountForAGoal, CountAMoveThrown);
            _pointer = new BoardPointer(game);
        }

        /// <summary>The one camera everything is drawn by, text included, so its picture is the whole screen.</summary>
        public Camera Camera { get; }

        /// <summary>The game this stage shows. Moves are made through the stage (<see cref="TryPlay"/>), never on the game itself.</summary>
        public Game Game => _game;

        /// <summary>
        /// True when nothing on the stage is moving any more: the board's entrance, the last turn and a won game's
        /// finale are over, and the result's card, if there is one, hangs in its place. What only twinkles does
        /// not count. A turn or a finale that has run out counts as moving until the stage has finished it.
        /// </summary>
        public bool IsSettled => _tweens.IsIdle && _timeline.IsDone && _playing == Playback.Nothing;

        /// <summary>
        /// Where the player goes on to, once they have pressed a button of the result's card and the card has gone up
        /// out of sight: this stage is then over, and the room is dark for what comes next to come out of. Nothing
        /// before that.
        /// </summary>
        public ResultChoice? PlayerChose { get; private set; }

        /// <summary>True once the player has chosen where to go on to and the result's card is gone.</summary>
        public bool PlayerWentOn => PlayerChose.HasValue;

        /// <summary>True while the card that asks whether the player means to leave hangs in front of the board, or is coming or going.</summary>
        public bool IsAskingWhetherToLeave => _result != null && _game.Status == GameStatus.Playing;

        /// <summary>
        /// The pixel of the screen in the middle of the round button on the shelf, which leads out of the level:
        /// while the game is played it asks first, under a result's card it leads to the museum. Nothing while the
        /// button is not listening.
        /// </summary>
        public Vector2? LeaveButtonPixel => ListensForTheLeaveButton ? PixelOf(_leave.Middle) : null;

        /// <summary>
        /// The pixel of the screen in the middle of the result card's green button, which leads on with the levels,
        /// counted from the screen's lower left corner: where a finger presses it. Nothing while no card hangs at
        /// rest in its place.
        /// </summary>
        public Vector2? ResultButtonPixel => _result != null && ListensToTheCard ? PixelOf(_result.GoOnButtonMiddle) : null;

        /// <summary>The same for the card's teal button, which leads back to the museum.</summary>
        public Vector2? MuseumButtonPixel => _result?.MuseumButtonMiddle is Vector2 button && ListensToTheCard ? PixelOf(button) : null;

        /// <summary>
        /// The pixel of the screen in the middle of the card's blue button, which leads to the jigsaw where the
        /// pieces just won are placed; nothing while the card has no such button, or is not listening.
        /// </summary>
        public Vector2? PlaceButtonPixel => _result?.PlaceButtonMiddle is Vector2 button && ListensToTheCard ? PixelOf(button) : null;

        /// <summary>The script of the turn that was played last; nothing before the first move. A finale is not a turn: after a win this stays the winning move's.</summary>
        public TurnScript? LastTurn { get; private set; }

        /// <summary>The script of the won game's finale; nothing while the game has had none, and for a game that has none (lost, or won with its last move).</summary>
        public TurnScript? Finale { get; private set; }

        /// <summary>True while a won game's finale is shown: from the step that ends the winning move's turn until its last piece has landed, or a touch has ended it.</summary>
        public bool IsShowingTheFinale => _playing == Playback.TheFinale;

        /// <summary>
        /// The line on the shelf under the board: how the game is played, or which try this is. No words at all
        /// from the moment a won game's finale begins.
        /// </summary>
        public string WordsOnTheShelf => _shelf.Words;

        /// <summary>
        /// Where the stage did not show the game in the last turn and, after a win, in its finale, one line per
        /// fault: a turn that could not be shown at all, a cell that showed another piece than the engine has there
        /// when the turn was over, a piece not at rest, a plaque counted wrong. A finale's faults begin with
        /// "finale:". Empty when the turn ended on the engine's board, as every turn must. After a fault the board
        /// was laid out anew from the engine's, so the game goes on; the fault is a mistake to be found all the
        /// same, and is written to the log as an error.
        /// </summary>
        public IReadOnlyList<string> LastTurnFaults { get; private set; } = Array.Empty<string>();

        /// <summary>The board listens to the player while the game goes on and nothing is moving.</summary>
        private bool TakesInput => IsSettled && _game.Status == GameStatus.Playing;

        /// <summary>
        /// The card listens to the player from the moment it hangs in its place: what is still popping up in its
        /// alcove does not hold up a finger that already knows where it wants to go.
        /// </summary>
        private bool ListensToTheCard => _result != null && _isCardDown && !PlayerWentOn;

        /// <summary>
        /// The shelf's round button listens while the game is played (also while pieces move: the question then
        /// waits for them) and under a result's card. Under the card that asks whether to leave it does not: that
        /// card's own buttons answer.
        /// </summary>
        private bool ListensForTheLeaveButton =>
            !PlayerWentOn && (_result == null ? _game.Status == GameStatus.Playing : ListensToTheCard && !IsAskingWhetherToLeave);

        /// <summary>Builds the stage of a game and starts the board's entrance. Call <see cref="Fit"/> before the first picture.</summary>
        /// <param name="game">A game that records its events (GameOptions.RecordEvents): the stage plays them back.</param>
        /// <param name="effectArt">
        /// The pictures of what is thrown over the board. They are the same for every stage and take a quarter of
        /// a second to paint, so whoever opens stages paints them once and lends them to each; the stage does not
        /// dispose of them.
        /// </param>
        /// <param name="paintings">The museum's paintings: the result's card shows the jigsaw pieces a win gives.</param>
        /// <param name="brief">What the player's progress knows about this attempt, for the shelf and the result's card.</param>
        public static GameStage Open(Game game, ArtCatalogue art, EffectArt effectArt, PaintingGallery paintings, AttemptBrief brief, StageOpening opening = StageOpening.InTheLitRoom)
        {
            var stage = new GameStage(game, art, effectArt, paintings, brief);
            BoardEntrance.Play(stage._board, stage._tweens);
            if (opening == StageOpening.OutOfTheDark)
            {
                stage._tweens.Start(new Tween(0f, Tempo.RoomLightens, Easing.Out, lit => stage._veil.Darken(1f - lit)));
            }

            return stage;
        }

        /// <summary>Lays the stage out for a screen; call again whenever the screen changes (a fold opens, a bar appears).</summary>
        public void Fit(ScreenShape screen)
        {
            ScreenLayout layout = ScreenLayout.Of(screen, _footprint);
            _layout = layout;
            StageCamera.Show(Camera, layout.View);
            _wall.Fit(layout.View);
            _lamps.Place(layout, _shape);
            _hud.Place(layout);
            _frame.Place(layout);
            _board.Place(layout);
            _effects.Place(layout);
            _shelf.Place(layout);
            _leave.Place(new Vector2(layout.LeaveButton.MiddleX, layout.LeaveButton.MiddleY), layout.LeaveButton.Width / DiscButton.Size);
            _veil.Fit(layout.View);
            _result?.Place(layout);
        }

        /// <summary>
        /// Moves the stage's clock on. Nothing on the stage changes with time in any other way. A gesture that was
        /// made while the board was moving is acted on here, in the step that brings the board to rest, so this can
        /// play a move (<see cref="Game.MovesUsed"/> tells).
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            _clock += deltaSeconds;
            _tweens.Advance(deltaSeconds);
            ShowThePickedPiece();
            _result?.Twinkle(_clock);
            _timeline.Advance(deltaSeconds * PaceOfTheTimeline());
            if (_timeline.IsDone)
            {
                FinishWhatWasShown();
            }

            ActOnTheWaitingGesture();
            AskWhetherToLeaveOnceAtRest();
        }

        /// <summary>
        /// The player asked for the way back, by the device's back button or gesture. While the game is played that
        /// asks whether they mean to leave; under that question it means "no"; under a result's card it leads to
        /// the museum; and a won game's finale it ends, as a touch does.
        /// </summary>
        public void GoBack()
        {
            if (PlayerWentOn)
            {
                return;
            }

            if (IsShowingTheFinale)
            {
                SkipTheFinale();
            }
            else if (_result == null)
            {
                AskToLeave();
            }
            else if (ListensToTheCard)
            {
                SendTheCardUp(_result, IsAskingWhetherToLeave ? ResultChoice.Stay : ResultChoice.ToTheMuseum);
            }
        }

        /// <summary>
        /// Plays a move and starts showing it. A move the engine refuses costs nothing: two pieces that cannot be
        /// traded go towards each other and back.
        /// </summary>
        /// <returns>False when the move was not played: the stage is busy, the game is over, or the engine refused it.</returns>
        public bool TryPlay(Move move)
        {
            if (!TakesInput)
            {
                return false;
            }

            _pointer.ClearSelection();
            ShowThePickedPiece();
            if (!_game.TryPlay(move))
            {
                ShowRefused(move);
                return false;
            }

            _playing = Playback.ATurn;
            try
            {
                TurnScript script = TurnChoreographer.Compose(_ledger, _game.Level.Goals, _game.LastEvents, _game.Board);
                LastTurn = script;
                _turnPlayer.Play(script);
            }
            catch (InvalidOperationException fault)
            {
                // The engine has played the move, so it counts. Only its picture is lost: the turn ends at once on
                // the engine's board, and the next step of the clock finishes it as it finishes every turn.
                _faultsOfTheTurn.Add($"the turn could not be shown: {fault.Message}");
                _timeline.Clear();
                ShowTheEnginesBoardAnew();
            }

            return true;
        }

        /// <summary>The pixel of the screen in the middle of a cell, counted from the screen's lower left corner: where a finger touches that cell.</summary>
        /// <exception cref="InvalidOperationException">The stage was not fitted to a screen yet.</exception>
        public Vector2 PixelOf(Position cell)
        {
            if (_layout is not ScreenLayout layout)
            {
                throw new InvalidOperationException("The stage has no screen yet; call Fit first.");
            }

            (float pixelX, float pixelY) = layout.PixelAt(cell.X + 0.5f, cell.Y + 0.5f);
            return new Vector2(pixelX, pixelY);
        }

        /// <summary>
        /// A finger came down on the screen. Pixels are counted from the screen's lower left corner. While the game
        /// goes on the finger is the board's, whether the pieces are at rest or not; once the result's card hangs,
        /// it is the card's. While a won game's finale is shown, a finger that comes down anywhere ends it, and is
        /// given to nothing else: it presses no button of the card that then comes.
        /// </summary>
        public void PointerDown(Vector2 pixel)
        {
            if (IsShowingTheFinale)
            {
                SkipTheFinale();
                return;
            }

            if (_layout is not ScreenLayout layout)
            {
                return;
            }

            if (ListensForTheLeaveButton)
            {
                _controls.FingerDown(PointAt(pixel, layout));
            }

            if (_controls.IsHolding)
            {
                return; // the finger is the round button's until it lifts
            }

            if (_result != null)
            {
                if (ListensToTheCard)
                {
                    _result.FingerDown(PointAt(pixel, layout));
                }
            }
            else if (_game.Status == GameStatus.Playing)
            {
                (float column, float row) = layout.BoardPointAt(pixel.x, pixel.y);
                _pointer.Down(column, row);
            }
        }

        /// <summary>
        /// One more finger came down while the one the stage follows is still on the screen. It is given to
        /// nothing, as ever; but a won game's finale ends for it as for the first, so a touch anywhere skips the
        /// finale also while the finger that made the winning move rests on the glass.
        /// </summary>
        public void AnotherFingerDown()
        {
            if (IsShowingTheFinale)
            {
                SkipTheFinale();
            }
        }

        /// <summary>The finger moved while it was down.</summary>
        public void PointerMoved(Vector2 pixel)
        {
            if (_layout is not ScreenLayout layout)
            {
                return;
            }

            if (_controls.IsHolding)
            {
                _controls.FingerMovedTo(PointAt(pixel, layout));
                return;
            }

            if (_result != null)
            {
                if (ListensToTheCard)
                {
                    _result.FingerMovedTo(PointAt(pixel, layout));
                }
            }
            else
            {
                (float column, float row) = layout.BoardPointAt(pixel.x, pixel.y);
                if (_pointer.MovedTo(column, row) is BoardGesture swipe)
                {
                    Take(swipe);
                }
            }
        }

        /// <summary>The finger lifted.</summary>
        public void PointerUp()
        {
            if (_controls.IsHolding)
            {
                if (_controls.FingerUp(out StageControl _))
                {
                    GoBack(); // the round button and the device's way back mean the same, on every card and on none
                }

                return;
            }

            if (_result != null)
            {
                if (ListensToTheCard && _result.FingerUp(out ResultChoice choice))
                {
                    SendTheCardUp(_result, choice);
                }
            }
            else if (_pointer.Up() is BoardGesture tap)
            {
                Take(tap);
            }
        }

        /// <summary>The touch was taken away by the device, or the game lost the screen while a finger was down.</summary>
        public void PointerCancelled()
        {
            _pointer.Cancel();
            _waiting = null;
            _controls.FingerGone();
            _result?.FingerGone();
        }

        public void Dispose()
        {
            _wall.Dispose();
            _lamps.Dispose();
            _hud.Dispose();
            _frame.Dispose();
            _board.Dispose();
            _shelf.Dispose();
            _veil.Dispose();
            _result?.Dispose();
            SceneObjects.Destroy(_root);
        }

        private void CountTheMove()
        {
            _figures = _figures.AfterAMove();
            _hud.Show(_figures);
        }

        private void CountForAGoal(int goalIndex)
        {
            _figures = _figures.WithOneCounted(goalIndex);
            _hud.Show(_figures);
        }

        /// <summary>
        /// A move left over leaves the medallion in the finale: the number drops by one, and jumps up as it does
        /// and comes back. Many moves in quick succession keep it up, each jump beginning where the last one was.
        /// </summary>
        private void CountAMoveThrown()
        {
            _figures = _figures.WithMovesTurnedIntoRockets(1);
            _hud.Show(_figures);

            // A tween and not an act: this is called by an act, which may not add acts to the timeline it is shown by.
            _tweens.Start(new Tween(0f, Tempo.MovesSwell, progress => 1f - Easing.In(progress), _hud.SwellTheMoves));
        }

        /// <summary>
        /// A gesture the finger made on the board: acted on now when the board is at rest, kept for the moment it
        /// comes to rest otherwise. Only the latest gesture waits; in a game that is decided none does.
        /// </summary>
        private void Take(BoardGesture gesture)
        {
            if (TakesInput)
            {
                ActOn(gesture);
            }
            else if (_game.Status == GameStatus.Playing)
            {
                _waiting = new WaitingGesture(gesture, _clock);
            }
        }

        private void ActOn(BoardGesture gesture)
        {
            if (_pointer.MoveMeantBy(gesture) is Move move)
            {
                TryPlay(move);
            }
        }

        /// <summary>
        /// Once the board has come to rest: what the finger did while it moved is acted on, unless that was too
        /// long ago to be meant for the board as it now lies, or the game is decided.
        /// </summary>
        private void ActOnTheWaitingGesture()
        {
            if (_waiting is not WaitingGesture waiting || !IsSettled)
            {
                return;
            }

            _waiting = null;
            if (TakesInput && _clock - waiting.MadeAt <= Tempo.TouchWaits)
            {
                ActOn(waiting.Gesture);
            }
        }

        /// <summary>The timeline has run out: if it was showing a turn or a finale, that is finished now.</summary>
        private void FinishWhatWasShown()
        {
            switch (_playing)
            {
                case Playback.Nothing:
                    break;
                case Playback.ATurn:
                    FinishTheTurn();
                    break;
                case Playback.TheFinale:
                    FinishTheFinale();
                    break;
                default:
                    throw new InvalidOperationException($"The stage does not know how to finish showing {_playing}.");
            }
        }

        private void FinishTheTurn()
        {
            _playing = Playback.Nothing;
            LastTurnFaults = ComeToRestOnTheEnginesBoard();
            if (LastTurnFaults.Count > 0)
            {
                Debug.LogError(
                    $"Move {_game.MovesUsed} of level {_game.Level.Id} was not shown as the game played it; the board was laid out anew from the engine's. "
                    + string.Join("; ", LastTurnFaults));
            }

            if (_game.Status == GameStatus.Playing)
            {
                return;
            }

            // Whether the engine has played a finale already is not asked: it would refuse a second one, and the
            // stage would then go to the card by the way every finale's fault goes.
            if (_game.Status == GameStatus.Won && _game.MovesLeft > 0)
            {
                BeginTheFinale();
            }
            else
            {
                HangTheResult();
            }
        }

        /// <summary>
        /// What every turn and every finale ends with: what was taken from the board is cleared away, what is seen
        /// is compared with the engine's board and made good where it differs, and the plaques show the game's own
        /// numbers again. Whether a move decided the game is only known by now: few moves stop being a warning.
        /// </summary>
        /// <returns>What went wrong in the showing, one line per fault; empty when it ended on the engine's board.</returns>
        private IReadOnlyList<string> ComeToRestOnTheEnginesBoard()
        {
            _board.ClearAwayWhatWasTaken();
            _effects.ClearAway();
            IReadOnlyList<string> faults = FaultsOfTheTurnMadeGood();
            _figures = FiguresOfTheGame();
            _hud.Show(_figures);
            return faults;
        }

        /// <summary>
        /// The game is won with moves left over and the winning move has come to rest: the engine plays its finale
        /// at once, and the stage starts showing it. If that cannot be done, the finale ends before it began, on
        /// whatever board the engine has, and the card comes.
        /// </summary>
        private void BeginTheFinale()
        {
            // First of all: whatever goes wrong below, the next step of the clock finds a finale to finish, and the card comes.
            _playing = Playback.TheFinale;
            _isAskedToLeave = false;
            _controls.FingerGone();

            // The shelf told how to play, or which try this is. The game is decided, and a touch now ends the finale:
            // the plank is left bare rather than asking for the very touch that throws the reward away.
            _shelf.Say(string.Empty);
            try
            {
                FinaleOutcome finale = _game.PlayFinale();
                _finale = finale;
                TurnScript script = TurnChoreographer.ComposeFinale(_ledger, _game.LastEvents, _game.Board);
                Finale = script;
                _finalePace = FinalePace.Of(script, finale.RocketsFired);
                _finaleBeganAt = _timeline.Now;
                _turnPlayer.Play(script);
            }
            catch (InvalidOperationException fault)
            {
                _faultsOfTheTurn.Add($"the finale could not be shown: {fault.Message}");
                EndTheFinaleOnTheEnginesBoard();
            }
        }

        /// <summary>
        /// A touch or the way back ends the finale where it is: the engine's final board is laid out at rest under
        /// one blink of light, and the card comes as after any win. Nothing is lost by it: the engine played the
        /// whole finale before its first picture. Skipping is no fault, and nothing is written of it.
        /// </summary>
        private void SkipTheFinale()
        {
            EndTheFinaleOnTheEnginesBoard();
            var middleOfTheTray = new Vector2(_shape.Width / 2f, _shape.Height / 2f);
            _effects.Flash(0f, middleOfTheTray, SkipBlinkAcross * Mathf.Max(_shape.Width, _shape.Height), BoardEffects.WarmLight, 0.55f);
        }

        /// <summary>The one way a finale ends before its time, skipped or at fault: nothing of it stays on the timeline, and the engine's board is shown.</summary>
        private void EndTheFinaleOnTheEnginesBoard()
        {
            _timeline.Clear();
            ShowTheEnginesBoardAnew();
            FinishTheFinale();
        }

        /// <summary>
        /// How much faster than the stage's clock the timeline runs. Only a finale is ever hurried, and only where
        /// it would take too long as written (<see cref="FinalePace"/>); the card that follows it on the same
        /// timeline comes at the clock's own pace again.
        /// </summary>
        private float PaceOfTheTimeline() => IsShowingTheFinale && _finalePace is FinalePace pace
            ? pace.At(_timeline.Now - _finaleBeganAt)
            : 1f;

        private void FinishTheFinale()
        {
            _playing = Playback.Nothing;
            IReadOnlyList<string> faults = ComeToRestOnTheEnginesBoard();
            if (faults.Count > 0)
            {
                Debug.LogError(
                    $"The finale of level {_game.Level.Id} was not shown as the game played it; the board was laid out anew from the engine's. "
                    + string.Join("; ", faults));
                LastTurnFaults = LastTurnFaults.Concat(faults.Select(fault => $"finale: {fault}")).ToArray();
            }

            HangTheResult();
        }

        /// <summary>
        /// The numbers the plaques must show when nothing moves: the game's own, and after a finale the game's
        /// with the moves counted down that turned into rockets, which the game itself goes on counting as left.
        /// </summary>
        private HudFigures FiguresOfTheGame() => _finale is FinaleOutcome finale
            ? HudFigures.Of(_game).WithMovesTurnedIntoRockets(finale.RocketsFired)
            : HudFigures.Of(_game);

        /// <summary>
        /// What went wrong in the turn that is over: what was noted while it was shown, and where the stage does not
        /// show the game now. In the second case the board is laid out anew, so that the next move is played on what
        /// the engine has.
        /// </summary>
        private IReadOnlyList<string> FaultsOfTheTurnMadeGood()
        {
            var faults = new List<string>(_faultsOfTheTurn);
            _faultsOfTheTurn.Clear();
            string[] differences = WhereTheStageDoesNotShowTheGame().ToArray();
            if (differences.Length > 0)
            {
                faults.AddRange(differences);
                ShowTheEnginesBoardAnew();
            }

            return faults;
        }

        /// <summary>
        /// The stage's last resort: whatever it showed of the board is taken away, and the engine's board and
        /// numbers are shown as they stand, every piece at rest. Nothing may be left on the timeline that still
        /// holds a piece of the board that is given up here.
        /// </summary>
        private void ShowTheEnginesBoardAnew()
        {
            _effects.ClearAway();
            _ledger.StartAgainFrom(_game.Board);
            _board.ShowAnew(_ledger);
            _figures = FiguresOfTheGame();
            _hud.Show(_figures);
        }

        /// <summary>
        /// The game is decided: the board is left to be seen for a moment, then the room goes dark, the result's
        /// card comes down on its wires, and what it shows in its alcove pops up as it settles.
        /// </summary>
        private void HangTheResult()
        {
            // The button to the jigsaw speaks of every piece that waits on the painting's tray once the level's own are won.
            int piecesWaiting = _game.Status == GameStatus.Won ? _brief.Reward?.WaitingWithThem ?? 0 : 0;
            _isAskedToLeave = false;
            _controls.FingerGone();

            // The round button stays lit in the dark room: under a result it is the way to the museum.
            _leave.DrawAt(DrawOrder.ShelfButtonOverTheDark);
            HangACard(LevelResult.Of(_game, _brief), piecesWaiting, comesIn: Tempo.PauseBeforeResult);
        }

        /// <summary>The room goes dark and a card comes down on its wires; what it shows in its alcove pops up as it settles.</summary>
        /// <param name="comesIn">How long the board is left to be seen before the room darkens.</param>
        private void HangACard(LevelResult tells, int piecesWaiting, float comesIn)
        {
            ResultPanel card = ResultPanel.Create(_root.transform, _art, _effectArt, _paintings, tells, piecesWaiting);
            if (_layout is ScreenLayout layout)
            {
                card.Place(layout);
            }

            _result = card;
            _isCardDown = false;
            float cardComes = comesIn + Tempo.CardAfterDark;
            float exhibitsSeconds = card.ExhibitsSeconds;
            _timeline.Add(comesIn, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)), _lamps.PutOut);
            _timeline.Add(cardComes, Tempo.CardComesDown, card.ComeDown, () => _isCardDown = true);
            _timeline.Add(cardComes + Tempo.ExhibitsAfterCard, exhibitsSeconds, progress => card.ShowExhibits(progress * exhibitsSeconds));
        }

        /// <summary>
        /// The player wants to leave the level. If the board is at rest the question is put at once; if pieces are
        /// still moving it waits for them, and is dropped if their move decides the game.
        /// </summary>
        private void AskToLeave()
        {
            _isAskedToLeave = true;
            AskWhetherToLeaveOnceAtRest();
        }

        private void AskWhetherToLeaveOnceAtRest()
        {
            if (!_isAskedToLeave || !IsSettled)
            {
                return;
            }

            _isAskedToLeave = false;
            if (_result != null || _game.Status != GameStatus.Playing)
            {
                return;
            }

            _pointer.Cancel();
            _pointer.ClearSelection();
            _waiting = null;
            HangACard(LevelResult.ForLeaving(_game, _brief), piecesWaiting: 0, comesIn: 0f);
        }

        /// <summary>
        /// The player stays: while the card goes up the room lights again, and once it is gone the card is taken
        /// down and the board is the player's as before.
        /// </summary>
        private void GoOnWithTheGame(ResultPanel card)
        {
            _timeline.Add(0f, Tempo.CardGoesUp, card.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => _veil.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    card.Dispose();
                    _result = null;
                    _lamps.LightAgain();
                });
        }

        /// <summary>
        /// Takes the stage out of sight while the player is at the jigsaw the card led to: nothing of it is drawn,
        /// and it stays as it is, dark and with its card drawn up, until <see cref="ComeBackToTheCard"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The player did not ask for the jigsaw.</exception>
        public void StepAside()
        {
            if (PlayerChose != ResultChoice.PlaceThePieces)
            {
                throw new InvalidOperationException("A board steps aside only for the jigsaw its card led to.");
            }

            _root.SetActive(false);
        }

        /// <summary>
        /// The player is back from the jigsaw: the stage is seen again, and its card comes down once more, with
        /// the button to the jigsaw only if pieces still wait there.
        /// </summary>
        /// <param name="piecesWaiting">How many pieces of the level's painting still wait to be placed.</param>
        /// <exception cref="InvalidOperationException">The stage did not step aside for a jigsaw.</exception>
        public void ComeBackToTheCard(int piecesWaiting)
        {
            if (PlayerChose != ResultChoice.PlaceThePieces || !(_result is ResultPanel card))
            {
                throw new InvalidOperationException("Only a board that stepped aside for a jigsaw comes back to its card.");
            }

            _root.SetActive(true);
            PlayerChose = null;
            card.OfferThePlacing(piecesWaiting);
            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, card.ComeDown, () => _isCardDown = true);
        }

        /// <summary>
        /// The player pressed one of the card's buttons: the card goes up out of sight, and then this stage is over,
        /// unless the choice was to stay in the game.
        /// </summary>
        private void SendTheCardUp(ResultPanel card, ResultChoice choice)
        {
            _isCardDown = false;
            if (choice == ResultChoice.Stay)
            {
                GoOnWithTheGame(card);
            }
            else
            {
                _timeline.Add(0f, Tempo.CardGoesUp, card.GoUp, () => PlayerChose = choice);
            }
        }

        /// <summary>The pixel of the screen at which a point of the layout lies; nothing before the stage has a screen.</summary>
        private Vector2? PixelOf(Vector2 point)
        {
            if (_layout is not ScreenLayout layout)
            {
                return null;
            }

            (float pixelX, float pixelY) = layout.PixelOfPoint(point.x, point.y);
            return new Vector2(pixelX, pixelY);
        }

        /// <summary>Where a pixel of the screen lies in the layout's points, which are the stage's own.</summary>
        private static Vector2 PointAt(Vector2 pixel, ScreenLayout layout)
        {
            (float x, float y) = layout.PointAt(pixel.x, pixel.y);
            return new Vector2(x, y);
        }

        /// <summary>
        /// The check at the end of a turn: the board that is seen is the engine's board with every piece at rest, the
        /// view's ledger says the same, and the plaques were counted down to the engine's numbers. After a finale
        /// that includes the count of moves that left the medallion: as many as the engine made rockets of.
        /// </summary>
        private IEnumerable<string> WhereTheStageDoesNotShowTheGame()
        {
            foreach (string fault in _board.DifferencesFrom(_game.Board))
            {
                yield return fault;
            }

            foreach (string fault in _ledger.DifferencesFrom(_game.Board))
            {
                yield return $"ledger: {fault}";
            }

            foreach (string fault in _figures.DifferencesFrom(FiguresOfTheGame()))
            {
                yield return fault;
            }
        }

        private void ShowThePickedPiece()
        {
            PieceSprite? picked = _pointer.Selected is Position cell && _ledger.PieceAt(cell) != PieceLedger.NoPiece
                ? _board.Piece(_ledger.PieceAt(cell))
                : null;
            _pickedMark.Show(picked, _clock);
        }

        /// <summary>
        /// The engine refused a trade: the two pieces go towards each other's cell and come back. A piece that
        /// cannot be traded at all (a crate, a piece under tape) stays, and the other only nudges it.
        /// </summary>
        private void ShowRefused(Move move)
        {
            if (move.Kind != MoveKind.Swap)
            {
                return;
            }

            bool bothMove = CanBeMoved(move.From) && CanBeMoved(move.To);
            float reach = bothMove ? RefusedTradeReach : RefusedNudgeReach;
            NudgeAndReturn(move.From, move.To, reach, isCarried: true);
            if (bothMove)
            {
                NudgeAndReturn(move.To, move.From, reach, isCarried: false);
            }
        }

        private bool CanBeMoved(Position cell) =>
            _ledger.PieceAt(cell) != PieceLedger.NoPiece && _ledger.TileAt(cell).Kind.IsSwappable() && _ledger.TapeAt(cell) == 0;

        private void NudgeAndReturn(Position cell, Position toward, float reach, bool isCarried)
        {
            if (!CanBeMoved(cell))
            {
                return;
            }

            PieceSprite piece = _board.Piece(_ledger.PieceAt(cell));
            Vector2 from = PieceSprite.MiddleOf(cell);
            Vector2 to = PieceSprite.MiddleOf(toward);
            _timeline.Add(
                0f,
                2f * Tempo.RefusedSwap,
                progress =>
                {
                    piece.Lift(isCarried);
                    piece.Place = Vector2.Lerp(from, to, reach * Easing.Arch(progress));
                },
                () => piece.Rest(cell));
        }

        /// <summary>A gesture that waits for the board to come to rest, and when on the stage's clock it was made.</summary>
        private readonly struct WaitingGesture
        {
            public WaitingGesture(BoardGesture gesture, float madeAt)
            {
                Gesture = gesture;
                MadeAt = madeAt;
            }

            public BoardGesture Gesture { get; }

            public float MadeAt { get; }
        }
    }
}
