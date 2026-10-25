#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Avatar;
using Puzzle.Client.Boards;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Meta;
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
    /// A level can be left at any time. The pause disc on the shelf's left end, and the device's way back
    /// (<see cref="GoBack"/>), ask first: a card comes down that says what leaving costs (a life once a move was
    /// played or a booster used, nothing before), and the player stays or goes to the museum. From the press on the
    /// board takes no touch: what the finger did before is forgotten, an armed booster is put down, and a move still
    /// being shown is finished before the card
    /// comes. Asking first matters on a phone, where a swipe from the screen's edge is the way back and the board
    /// reaches that edge. Under any card the pause disc lies in the dark with the shelf and listens to nothing:
    /// every card has its own way out.
    /// </para>
    /// <para>
    /// The four boosters used during play stand on the shelf after the pause disc (<see cref="ShelfBrief"/>). A
    /// press on a disc arms its booster: it is ringed, the others are set aside, and a pill over the shelf says what
    /// it is, what it does and what to pick. While a finger is down on the board the cell, row or column it aims at
    /// is lit and follows the finger as it slides; lifting over the board uses the booster there, and lifting off it,
    /// the armed disc, the pause disc or the way back put it down again. The shuffle is used at once. A disc may be
    /// armed while pieces move; a use made then waits for the board to come to rest, as a gesture does. The engine
    /// decides (Game.TryUseBooster): a use it accepts is shown as a turn is, with the tool's own picture and no move
    /// spent (TurnChoreographer.ComposeBooster); one that would change nothing is refused, the disc shakes, the pill
    /// says why and nothing is spent. A disc of which none is held shows a plus, which hangs a card over the dimmed
    /// board that sells one for its single price; once the wallet holds it the card goes up and the booster is
    /// armed. A booster not open yet is dimmed with the level it opens on and takes no finger; on that level it is
    /// used once by hand, a step of the tutorial (see below). The stage does not hold the wallet: it is told it
    /// (<see cref="Tell"/>), and what it asks of it waits to be taken (<see cref="TakeWhatWasAskedFor"/>).
    /// </para>
    /// <para>
    /// A finger held on a booster's disc for <see cref="Tempo.LongPress"/> is a long press (#7): the room darkens and
    /// a card comes down that shows what the booster does on a tiny board of its own, over and over
    /// (<see cref="BoosterCard{TPress}"/>). The lift of that finger presses nothing, so a long press never arms or uses
    /// a booster; an armed one is put down. A touch anywhere, or the way back, takes the card up. While it hangs the
    /// board and the shelf take no finger, no hint shows and no step of the tutorial begins; a step that shows waits
    /// under it.
    /// </para>
    /// <para>
    /// When the moves run out with goals open for the first time in the attempt, the game is not lost yet: the
    /// card that hangs offers five more (Puzzle.Meta's terms, which the owner of the wallet tells the stage at every
    /// step with the wallet). Its green button pays coins and its blue one plays an ad, each dimmed with the reason
    /// where the terms do not allow it; "Vazgeç" gives up. The engine is asked first (Game.TryTakeExtraMoves): when it
    /// gives the moves, the payment is reported, the card goes up, the room lights, the medallion counts the five up
    /// and play goes on (a board with no move left is dealt anew with them); paid by an ad, a card that stands in for
    /// one hangs in front for its seconds first, and the payment is reported when it is over. When the engine refuses
    /// (no shuffle can bring the board back), nothing is paid and the card of a loss for good comes down in its place,
    /// as it does after giving up. While the offer hangs the board takes no touch, nothing ends the attempt, and the
    /// way back does nothing: it costs no life. A second run-out, and a board with no move possible, are lost for
    /// good at once.
    /// </para>
    /// <para>
    /// What happens in the attempt that changes the player's progress is reported, in order, at the moment the
    /// engine says so: the first move (a booster the engine accepts counts as one), a booster used, the extra moves
    /// paid for, the move or the booster that wins, the one after which the game is lost for good (or the answer to
    /// the offer that loses it), and the press that leaves the level (<see cref="TakeWhatHappened"/>). The stage keeps
    /// no progress itself; whoever owns it takes the reports and keeps them.
    /// </para>
    /// <para>
    /// The game's hint has no button (the owner's decision of 2026-10-19). When the board has lain quiet and untouched
    /// for the settings' wait (at rest and taking a finger, no card over it, no booster armed, no piece picked, no
    /// finale, moves left), the two pieces of a move glow and nudge towards each other (<see cref="HintMark"/>), and
    /// the phone ticks where vibration is on. The move is Puzzle.Bot's HintAdvisor's, thought out on a copy of the game
    /// so that its streams are never touched. A touch anywhere, the way back and every move take the hint away at once
    /// and start the wait again; an attempt shows no more hints than the settings allow (<see cref="HintWatch"/>), and
    /// its record counts them (<see cref="HintsShown"/>). A stage opened without a <see cref="HintBrief"/> shows none.
    /// </para>
    /// <para>
    /// The tutorial's steps that may come on the board (the owner's decision of 2026-10-19; <see cref="TutorialWatch"/>)
    /// begin at a quiet moment, never while a card hangs, and only when the board offers what they teach: a step that
    /// waits says nothing. The board is asked once each time it comes to rest changed, and again when a step ends. A
    /// step dims the stage but for what it points at (<see cref="Spotlight"/>) and says what to look at or do on its own
    /// pill over the dark. A move shown by hand lights its two pieces as a hint does, and the board takes that trade and
    /// no other (another is refused as a trade that cannot be made is) until it is played; the shelf's boosters take no
    /// finger meanwhile, the pause disc and the way back do. A step that is only looked at (a plaque, an obstacle) ends
    /// with a touch anywhere, which does nothing else. No hint shows while a step does. The steps done are reported
    /// (<see cref="TakeTheTutorialStepsDone"/>), and the moves shown by hand are counted for the record (<see cref="GuidedMoves"/>).
    /// A stage opened without a <see cref="TutorialWatch"/> shows no step.
    /// </para>
    /// <para>
    /// On the level a booster used during play opens on, the tutorial has it used once by hand (#7): the dark is cut
    /// open around its disc, which a ring of light breathes around, and around its target (the cell, row or column
    /// worth the most to the goals; none for the shuffle), and its pill says to press the disc. Pressed, the booster
    /// is armed whether the player holds one or not, the ring goes, the target shows the aim it waits for, and the pill
    /// says to touch it; put down again (a lift off the board, the way back), it asks for the disc again. The shelf
    /// takes that disc and no other, and the board that use and no other move or aim (another is refused with the
    /// disc's shake). The use is free: nothing leaves the player's stock (<see cref="AttemptEvent.GuidedBoosterUsed"/>),
    /// and it begins no attempt by itself: leaving after it alone costs nothing and writes no record; once the player's
    /// own move or booster begins the attempt, its record names the free use too (<see cref="GuidedBoosters"/>). After the
    /// first booster used by hand, its disc is lit once more and the pill says that holding a booster shows what it does
    /// (the long press's step, a touch goes on).
    /// </para>
    /// </remarks>
    public sealed class GameStage : IStage
    {
        /// <summary>How far two pieces go towards each other's cell when their trade is refused, and how far a piece that has no partner to trade with.</summary>
        private const float RefusedTradeReach = 0.42f;
        private const float RefusedNudgeReach = 0.14f;

        /// <summary>The skipped finale's board is shown under one blink of light, this many times the tray's longer side across.</summary>
        private const float SkipBlinkAcross = 1.5f;

        /// <summary>The extra moves are counted up on the medallion one after another, each with a swell of its number, this far apart.</summary>
        private const float ExtraMovesCountedApart = Tempo.MovesSwell;

        /// <summary>A step of the tutorial dims the stage over this long as it comes, and again when it comes back after a card.</summary>
        private const float LessonComesIn = 0.35f;

        /// <summary>A window of the tutorial's spotlight reaches this share of a cell past the cells it lights, and this many points past a plaque.</summary>
        private const float LessonCellMargin = 0.16f;
        private const float LessonPlaqueMargin = 6f;

        /// <summary>The tutorial's pill hangs this many points under the plaques when it speaks of them, or when what it lights lies under it over the shelf.</summary>
        private const float LessonPillUnderThePlaques = 10f;

        /// <summary>The tutorial's pill is drawn at the shelf's size, but never smaller than this: on a compact screen the shelf is drawn small, and the pill is what the step says.</summary>
        private const float LessonPillLeastScale = 1f;

        /// <summary>The window over a booster's disc used by hand is this many times the disc across: its ring of light (1.42 across) lies inside it.</summary>
        private const float LessonDiscWindowAcross = 1.62f;

        /// <summary>The tutorial's pill stands this many points over the window of a booster's disc it speaks of.</summary>
        private const float LessonPillOverTheDisc = 6f;

        /// <summary>What a finger can press on the stage beside the board and a card's buttons. A closed set (private constructor).</summary>
        private abstract class StageControl
        {
            private StageControl()
            {
            }

            /// <summary>The pause disc on the shelf: it asks whether to leave the level.</summary>
            public sealed class Pause : StageControl
            {
            }

            /// <summary>A booster's disc on the shelf: it arms the booster, puts it down again, uses the shuffle, or offers one more where none is held.</summary>
            public sealed class Disc : StageControl
            {
                public Disc(BoosterKind booster)
                {
                    Booster = booster;
                }

                public BoosterKind Booster { get; }
            }
        }

        /// <summary>What the boosters of the shelf are doing. A closed set (private constructor).</summary>
        private abstract class Arming
        {
            private Arming()
            {
            }

            /// <summary>No booster is armed: a finger on the board moves pieces.</summary>
            public static Arming None { get; } = new Nothing();

            public sealed class Nothing : Arming
            {
            }

            /// <summary>A booster that strikes is armed: a finger on the board aims it, and lifting over the board uses it there.</summary>
            public sealed class Armed : Arming
            {
                public Armed(BoosterKind booster)
                {
                    Booster = booster;
                }

                public BoosterKind Booster { get; }
            }

            /// <summary>A use was made while the board was moving: it is made the moment the board comes to rest, its aim still shown.</summary>
            public sealed class Waiting : Arming
            {
                public Waiting(BoosterUse use)
                {
                    Use = use;
                }

                public BoosterUse Use { get; }
            }
        }

        /// <summary>What a press on the card that offers a booster asks for. A closed set (private constructor).</summary>
        private abstract class OfferPress
        {
            private OfferPress()
            {
            }

            /// <summary>The round button with the cross, and the way back: the card goes up.</summary>
            public sealed class Close : OfferPress
            {
            }

            /// <summary>The green button: one of the booster, for its price.</summary>
            public sealed class Buy : OfferPress
            {
                public Buy(BoosterKind booster)
                {
                    Booster = booster;
                }

                public BoosterKind Booster { get; }
            }
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

            /// <summary>The booster that was used last: its tool, and what it set off.</summary>
            ABooster,
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
        private readonly DiscButton _pause;
        private readonly PressSet<StageControl> _controls = new PressSet<StageControl>();
        private readonly ShelfBrief _shelfBrief;
        private readonly ShelfDiscs _discs;
        private readonly ShelfPill _pill;

        /// <summary>The dark over the card of the offer while the card that stands in for an ad hangs in front of it.</summary>
        private readonly Veil _secondVeil;

        /// <summary>What the extra moves cost the player now, as the stage was last told; nothing before it was told.</summary>
        private ExtraMovesTerms? _extraMoves;

        /// <summary>The ad that pays for the extra moves the engine gave, while it is shown; nothing otherwise.</summary>
        private AdOnTheStage? _ad;

        /// <summary>What the stage asked of the player's wallet since it was last asked: a booster bought on the offer's card.</summary>
        private readonly List<WalletRequest> _asked = new List<WalletRequest>();

        /// <summary>The boosters the player holds, as the stage was last told, less what it has used since; and the wallet as it was told, whose coins the offer's card prices.</summary>
        private BoosterShelf _owned = BoosterShelf.Empty;
        private WalletView _wallet;
        private Arming _arming = Arming.None;

        /// <summary>The booster whose use the engine refused last, and until when on the stage's clock the pill says why; nothing while it says nothing of it.</summary>
        private (BoosterKind Booster, float Until)? _refused;

        /// <summary>The card that offers a booster held none of, over the dimmed board; nothing while none hangs.</summary>
        private OfferCard<OfferPress>? _offer;

        /// <summary>True while the offer's card hangs in its place: from the moment it has come down until a button of it is pressed.</summary>
        private bool _isOfferDown;

        /// <summary>The booster bought on the offer's card, which is armed the moment the wallet is told to hold it; nothing otherwise.</summary>
        private BoosterKind? _armOnceHeld;

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

        /// <summary>What happened in the attempt that changes the player's progress and nobody has taken yet, in the order it happened.</summary>
        private readonly List<AttemptEvent> _happened = new List<AttemptEvent>();

        /// <summary>What the stage sounded that its owner has not taken yet: the cues of what it shows and of the presses on it.</summary>
        private readonly CuesSounded _sounded = new CuesSounded();

        /// <summary>The game's hints on this board and when one is due; nothing in a stage opened without hints.</summary>
        private readonly HintBrief? _hintBrief;
        private readonly HintWatch? _hintWatch;
        private readonly HintMark _hintMark;

        /// <summary>The move the hint that shows points at; nothing while none shows.</summary>
        private Move? _hint;

        /// <summary>The tutorial's steps on this board; nothing on a board that shows none.</summary>
        private readonly TutorialWatch? _tutorial;

        /// <summary>What a step of the tutorial shows: the dark with its windows, its pill, and the light on the two pieces of a move shown by hand.</summary>
        private readonly Spotlight _spotlight;
        private readonly ShelfPill _lessonPill;
        private readonly HintMark _guideMark;

        /// <summary>The step the spotlight and the pill are laid out for; nothing while none is.</summary>
        private Lesson? _lessonLaid;

        /// <summary>When on the stage's clock the step that shows was last seen to come: as it began, or as a card over it went.</summary>
        private float _lessonSeenSince;
        private bool _wasLessonSeen;

        /// <summary>The board as it stood when the tutorial was last asked for a step (moves, boosters, extra moves); nothing to ask again at the next quiet moment.</summary>
        private (int Moves, int Boosters, bool ExtraMoves)? _tutorialAskedOf;

        /// <summary>Whether the booster used by hand was armed when its step was last laid out: its pill and its light change when it is.</summary>
        private bool _lessonLaidArmed;

        /// <summary>The card a long press on a booster's disc brought down, over the dimmed board; nothing while none hangs.</summary>
        private BoosterCard<StageControl>? _boosterCard;

        /// <summary>True while that card hangs in its place: from the moment it has come down until it is taken up.</summary>
        private bool _isBoosterCardDown;

        /// <summary>True once a finger came down on the hanging card: its lift takes the card up. The lift of the finger whose long press brought it does not.</summary>
        private bool _isATouchOnTheBoosterCard;

        /// <summary>When on the stage's clock the finger came down on a control of the shelf that it still holds; nothing while it holds none.</summary>
        private float? _heldSince;

        private GameStage(
            Game game, ArtCatalogue art, EffectArt effectArt, PaintingGallery paintings, AttemptBrief brief, ShelfBrief shelf, HintBrief? hints, TutorialWatch? tutorial)
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
            _hud = Hud.Create(_root.transform, art, BoardPlaques.Of(game.Level.Id.Value, brief.Reward), paintings);
            _hud.Show(_figures);
            _frame = BoardFrame.Create(_root.transform, art, _shape);
            _board = BoardView.Create(_root.transform, art, _ledger, showExits: hasItemsToDrop);
            _shelf = Shelf.Create(_root.transform, art);
            _shelf.Say(string.Empty); // the plank is the boosters'; what the shelf has to say stands on the pill over it
            _pause = new DiscButton("Pause", _root.transform, art, DiscGlyph.Pause, DrawOrder.ShelfButton);
            _controls.Add(_pause, new StageControl.Pause());
            _shelfBrief = shelf;
            _discs = new ShelfDiscs(_root.transform, art, effectArt, shelf);
            foreach (KeyValuePair<BoosterKind, BoosterDisc> disc in _discs.All)
            {
                _controls.Add(disc.Value, new StageControl.Disc(disc.Key));
            }

            _pill = ShelfPill.Create(_root.transform, art);
            _veil = Veil.Create(_root.transform, art);
            _secondVeil = Veil.Create(_root.transform, art, DrawOrder.SecondVeil);
            _effectArt = effectArt;
            _pickedMark = new PickedMark(_board.Root, _effectArt, art);
            _hintBrief = hints;
            _hintWatch = hints == null ? null : new HintWatch(hints.Rules, clockSeconds: 0f);
            _hintMark = new HintMark(_board.Root, _effectArt, art);
            _tutorial = tutorial;
            _spotlight = Spotlight.Create(_root.transform, art);
            _lessonPill = ShelfPill.CreateForLessons(_root.transform, art, paintings.Painter);
            _guideMark = new HintMark(_board.Root, _effectArt, art);
            _effects = new BoardEffects(_board.Root, _shape, _effectArt, art, _timeline);
            _turnPlayer = new TurnPlayer(_board, _effects, _timeline, CountTheMove, CountForAGoal, CountAMoveThrown);
            _pointer = new BoardPointer(game);
            ShowTheShelf();
        }

        /// <summary>The one camera everything is drawn by, text included, so its picture is the whole screen.</summary>
        public Camera Camera { get; }

        /// <summary>The game this stage shows. Moves are made through the stage (<see cref="TryPlay"/>), never on the game itself.</summary>
        public Game Game => _game;

        /// <summary>
        /// True when nothing on the stage is moving any more: the board's entrance, the last turn and a won game's
        /// finale are over, the result's card, if there is one, hangs in its place, and no ad is being shown. What
        /// only twinkles does not count. A turn or a finale that has run out counts as moving until the stage has finished it.
        /// </summary>
        public bool IsSettled => _tweens.IsIdle && _timeline.IsDone && _playing == Playback.Nothing && _ad == null;

        /// <summary>
        /// Where the player goes on to, once they have pressed a button of the result's card and the card has gone up
        /// out of sight: this stage is then over, and the room is dark for what comes next to come out of. Nothing
        /// before that.
        /// </summary>
        public ResultChoice? PlayerChose { get; private set; }

        /// <summary>True once the player has chosen where to go on to and the result's card is gone.</summary>
        public bool PlayerWentOn => PlayerChose.HasValue;

        /// <summary>
        /// Where the green button of the decided game's card leads in the museum: to the hall after a win, to the hall
        /// with the level's card coming down after a loss (<see cref="LevelResult.GoingOnLeadsTo"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">The game is still being played: it has no result yet.</exception>
        public MuseumDoor GoingOnLeadsTo() => LevelResult.Of(_game, _brief).GoingOnLeadsTo();

        /// <summary>True while the card that asks whether the player means to leave hangs in front of the board, or is coming or going.</summary>
        public bool IsAskingWhetherToLeave => _result?.Tells is LevelResult.Leaving;

        /// <summary>True while the card that offers a game out of moves five more hangs in front of the board, or is coming or going.</summary>
        public bool IsOfferingExtraMoves => _result?.Tells is LevelResult.OutOfMoves;

        /// <summary>True while a card hangs in its place in front of the board and listens: a result's, the offer of extra moves, or the question whether to leave.</summary>
        public bool HangsACardInItsPlace => ListensToTheCard;

        /// <summary>What the card in front of the board tells; nothing while none hangs.</summary>
        public LevelResult? CardTells => _result?.Tells;

        /// <summary>The terms of the extra moves the offer's buttons show now; nothing while no offer hangs.</summary>
        public ExtraMovesTerms? ExtraMovesOnTheCard => _result?.TermsShown;

        /// <summary>True while the card that stands in for an ad hangs in front of the offer, or is coming or going.</summary>
        public bool IsShowingTheAd => _ad != null;

        /// <summary>How far the ad being shown has come, 0 to 1; nothing while none is shown.</summary>
        public float? AdProgress => _ad?.Ad.Progress;

        /// <summary>
        /// The pixel of the screen in the middle of the pause disc on the shelf, which asks whether to leave the
        /// level. Nothing while the disc is not listening: under a card, once it was pressed, and once the game is decided.
        /// </summary>
        public Vector2? PauseButtonPixel => ListensForThePauseButton ? PixelOf(_pause.Middle) : null;

        /// <summary>The coins a won level's card shows on its pill now, as far as they have counted up; nothing while no won level's card hangs.</summary>
        public long? CoinsOnTheCard => _result?.CoinsShown;

        /// <summary>
        /// The pixel of the screen in the middle of the result card's green button, which leads on with the levels,
        /// counted from the screen's lower left corner: where a finger presses it. Nothing while no card hangs at
        /// rest in its place, and on the card that offers extra moves, whose buttons pay for them.
        /// </summary>
        public Vector2? ResultButtonPixel => _result?.GoOnButtonMiddle is Vector2 button && ListensToTheCard ? PixelOf(button) : null;

        /// <summary>The pixel in the middle of the offer's green button, which pays coins for the moves, lit or dimmed; nothing while the offer does not hang at rest.</summary>
        public Vector2? TakeTheMovesForCoinsPixel => _result?.TakeForCoinsMiddle is Vector2 button && ListensToTheCard ? PixelOf(button) : null;

        /// <summary>The pixel in the middle of the offer's blue button, which plays an ad for them, lit or dimmed; nothing while the offer does not hang at rest.</summary>
        public Vector2? TakeTheMovesForAnAdPixel => _result?.TakeForAnAdMiddle is Vector2 button && ListensToTheCard ? PixelOf(button) : null;

        /// <summary>The pixel in the middle of the offer's "Vazgeç"; nothing while the offer does not hang at rest.</summary>
        public Vector2? GiveUpPixel => _result?.GiveUpMiddle is Vector2 link && ListensToTheCard ? PixelOf(link) : null;

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
        /// What the pill over the shelf says now, as one line of words: until the attempt has begun how the game is
        /// played, or which try this is; a booster's first use on its level; what an armed booster is and what to pick
        /// for it; why a use was refused. Nothing once the attempt has begun and nothing is armed, and nothing while a
        /// card hangs or a won game's finale is shown.
        /// </summary>
        public string WordsOnTheShelf => _pill.Words.ToString();

        /// <summary>What the pill over the shelf says now, as it sets its lines.</summary>
        public PillWords WhatThePillSays => _pill.Words;

        /// <summary>The booster that is armed, or whose use waits for the board to come to rest; nothing while none is.</summary>
        public BoosterKind? ArmedBooster => _arming switch
        {
            Arming.Armed armed => armed.Booster,
            Arming.Waiting waiting => BoosterKinds.Of(waiting.Use),
            _ => null,
        };

        /// <summary>The cell an armed booster is aimed at while a finger is down on the board; nothing otherwise.</summary>
        public Position? AimedAt => _pointer.AimedAt;

        /// <summary>The move the hint on the board shows now; nothing while none shows, and always in a stage without hints.</summary>
        public Move? HintShown => _hintMark.Shown;

        /// <summary>Hints shown so far in the attempt, which its record counts; 0 in a stage without hints.</summary>
        public int HintsShown => _hintWatch?.Shown ?? 0;

        /// <summary>Hints the attempt may still show; 0 in a stage without hints.</summary>
        public int HintsLeft => _hintWatch is HintWatch watch ? watch.PerAttempt - watch.Shown : 0;

        /// <summary>The step of the tutorial the stage shows now; nothing while none is seen (also while a card hangs over one that waits).</summary>
        public Lesson? LessonShown => IsTheLessonSeen ? _tutorial?.Showing : null;

        /// <summary>What the tutorial's pill says now; <see cref="PillWords.None"/> while no step is seen.</summary>
        public PillWords WhatTheLessonSays => _lessonPill.Words;

        /// <summary>The steps of the tutorial that may still come on this board, in their order; none on a board without them.</summary>
        public IReadOnlyList<TutorialStep> LessonsToCome => _tutorial?.MayStillCome ?? (IReadOnlyList<TutorialStep>)Array.Empty<TutorialStep>();

        /// <summary>The moves of the attempt that the tutorial showed by hand and the player played, which its record counts; 0 on a board without the tutorial.</summary>
        public int GuidedMoves => _tutorial?.GuidedMoves ?? 0;

        /// <summary>What each booster's disc on the shelf shows now.</summary>
        public IReadOnlyDictionary<BoosterKind, BoosterFace> ShelfFaces => _discs.All.ToDictionary(disc => disc.Key, disc => disc.Value.Face);

        /// <summary>The boosters the tutorial had used by hand in this attempt, free, by their record names: what its record names as guided.</summary>
        public IReadOnlyList<string> GuidedBoosters => _tutorial?.GuidedBoosters ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>The booster whose disc is ringed with light while the tutorial asks for it to be pressed; nothing for none.</summary>
        public BoosterKind? RingedDisc => _discs.Guided;

        /// <summary>The booster the card of a long press shows; nothing while no such card hangs, or comes or goes.</summary>
        public BoosterKind? BoosterCardShown => _boosterCard?.Booster;

        /// <summary>True while the card of a long press hangs in its place and a touch takes it up.</summary>
        public bool IsBoosterCardDown => _boosterCard != null && _isBoosterCardDown;

        /// <summary>How many times the card of a long press has shown its booster's effect through; 0 while none hangs.</summary>
        public int BoosterCardTimesShown => _boosterCard?.TimesShown ?? 0;

        /// <summary>The numbers the plaques show now, as far as they have been counted; the game's own whenever nothing moves.</summary>
        public HudFigures Plaques => _figures;

        /// <summary>The script of the booster used last; nothing before the first.</summary>
        public TurnScript? LastBooster { get; private set; }

        /// <summary>The booster the card over the board offers; nothing while no such card hangs.</summary>
        public BoosterKind? OfferedBooster { get; private set; }

        /// <summary>
        /// The pixel of the screen in the middle of a booster's disc on the shelf, where a finger presses it; nothing
        /// while the shelf is not listening, and for a disc that takes no finger (locked, or set aside while another
        /// is armed).
        /// </summary>
        public Vector2? BoosterDiscPixel(BoosterKind booster) =>
            ListensForTheShelf && !_discs[booster].Face.IsFaint ? PixelOf(_discs.MiddleOf(booster)) : null;

        /// <summary>The pixel of the screen in the middle of a booster's disc, whether it takes a finger or not: where a finger that tries a locked disc lands. Nothing before the stage has a screen.</summary>
        public Vector2? DiscPixel(BoosterKind booster) => PixelOf(_discs.MiddleOf(booster));

        /// <summary>The pixel in the middle of the offer card's green button; nothing while no such card hangs at rest, or its button is dimmed.</summary>
        public Vector2? OfferBuyPixel => _offer != null && _isOfferDown ? PixelOf(_offer.BuyButtonMiddle) : null;

        /// <summary>The pixel in the middle of the offer card's round button with the cross; nothing while no such card hangs at rest.</summary>
        public Vector2? OfferClosePixel => _offer?.Card.CloseButtonMiddle is Vector2 close && _isOfferDown ? PixelOf(close) : null;

        /// <summary>
        /// Where the stage did not show the game in the last turn and, after a win, in its finale, one line per
        /// fault: a turn that could not be shown at all, a cell that showed another piece than the engine has there
        /// when the turn was over, a piece not at rest, a plaque counted wrong. A finale's faults begin with
        /// "finale:". Empty when the turn ended on the engine's board, as every turn must. After a fault the board
        /// was laid out anew from the engine's, so the game goes on; the fault is a mistake to be found all the
        /// same, and is written to the log as an error.
        /// </summary>
        public IReadOnlyList<string> LastTurnFaults { get; private set; } = Array.Empty<string>();

        /// <summary>The board listens to the player while the game goes on, nothing is moving, no card hangs and the player has not asked to leave it.</summary>
        private bool TakesInput => IsSettled && _game.Status == GameStatus.Playing && _result == null && _offer == null && _boosterCard == null && !_isAskedToLeave;

        /// <summary>
        /// The card listens to the player from the moment it hangs in its place: what is still popping up in its
        /// alcove does not hold up a finger that already knows where it wants to go.
        /// </summary>
        private bool ListensToTheCard => _result != null && _isCardDown && !PlayerWentOn;

        /// <summary>
        /// The pause disc listens while the game is played, also while pieces move: the question then waits for
        /// them. Once pressed it has asked; under a card it does not listen: every card has its own way out.
        /// </summary>
        private bool ListensForThePauseButton =>
            !PlayerWentOn && _result == null && _offer == null && _boosterCard == null && _game.Status == GameStatus.Playing && !_isAskedToLeave;

        /// <summary>The boosters' discs listen when the pause disc does: while the game is played, also while pieces move; a use made then waits for rest.</summary>
        private bool ListensForTheShelf => ListensForThePauseButton;

        /// <summary>The offer's card listens from the moment it hangs in its place until a button of it is pressed.</summary>
        private bool ListensToTheOffer => _offer != null && _isOfferDown;

        /// <summary>Builds the stage of a game and starts the board's entrance. Call <see cref="Fit"/> before the first picture.</summary>
        /// <param name="game">A game that records its events (GameOptions.RecordEvents): the stage plays them back.</param>
        /// <param name="effectArt">
        /// The pictures of what is thrown over the board. They are the same for every stage and take a quarter of
        /// a second to paint, so whoever opens stages paints them once and lends them to each; the stage does not
        /// dispose of them.
        /// </param>
        /// <param name="paintings">The museum's paintings: the result's card shows the jigsaw pieces a win gives.</param>
        /// <param name="brief">What the player's progress knows about this attempt, for the shelf and the result's card.</param>
        /// <param name="shelf">Which boosters used during play are open on the level, the one shown for the first time, and their prices. What the player holds is told after (<see cref="Tell"/>).</param>
        /// <param name="hints">The game's hints for the attempt; nothing shows none (the Editor's studies).</param>
        /// <param name="tutorial">The tutorial's steps that may come on this board; nothing shows none (the Editor's studies, and a player who has seen them).</param>
        public static GameStage Open(
            Game game,
            ArtCatalogue art,
            EffectArt effectArt,
            PaintingGallery paintings,
            AttemptBrief brief,
            ShelfBrief shelf,
            StageOpening opening = StageOpening.InTheLitRoom,
            HintBrief? hints = null,
            TutorialWatch? tutorial = null)
        {
            var stage = new GameStage(game, art, effectArt, paintings, brief, shelf, hints, tutorial);
            // The engine's last events are its start's while no move is played: the specials of the start boosters.
            IReadOnlyList<GameEvent> startEvents = game.MovesUsed == 0 ? game.LastEvents : Array.Empty<GameEvent>();
            BoardEntrance.Play(stage._board, stage._tweens, stage._effects, startEvents);
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
            _pause.Place(new Vector2(layout.PauseButton.MiddleX, layout.PauseButton.MiddleY), layout.PauseButton.Width / DiscButton.Size);
            _discs.Place(layout);
            _pill.Place(layout);
            _veil.Fit(layout.View);
            _secondVeil.Fit(layout.View);
            _result?.Place(layout);
            _offer?.Card.Place(layout.PlaceOfACard(_offer.Card.Width, _offer.Card.Height), layout.View);
            _ad?.Card.Place(layout.PlaceOfACard(_ad.Card.Width, _ad.Card.Height), layout.View);
            _boosterCard?.Card.Place(layout.PlaceOfACard(_boosterCard.Card.Width, _boosterCard.Card.Height), layout.View);
            _spotlight.Fit(layout.View);
            if (_lessonLaid is Lesson laid)
            {
                LayTheLesson(laid, layout);
            }
        }

        /// <summary>
        /// Tells the stage what the player holds now, which the shelf's discs count and the offer's card prices, and
        /// what the extra moves cost them now, which the card of a game out of moves shows: the owner of the wallet
        /// tells it at every step. A booster bought on the offer's card is armed the moment it is held.
        /// </summary>
        public void Tell(WalletView wallet, ExtraMovesTerms extraMoves)
        {
            _extraMoves = extraMoves ?? throw new ArgumentNullException(nameof(extraMoves));
            _result?.ShowTerms(extraMoves);
            _owned = wallet.Shelf;
            _wallet = wallet;
            if (_arming is Arming.Armed armed && !_owned.Has(armed.Booster) && !IsUsedByHand(armed.Booster))
            {
                PutTheBoosterDown(); // the wallet holds none of it any more
            }

            if (_offer is OfferCard<OfferPress> offer && OfferedBooster is BoosterKind offered)
            {
                offer.ShowAffordable(_shelfBrief.CanBuy(offered, _wallet));
            }

            if (_armOnceHeld is BoosterKind bought && _owned.Has(bought))
            {
                _armOnceHeld = null;
                TakeTheOfferUp();
                if (bought != BoosterKind.Shuffle)
                {
                    Arm(bought);
                }
            }

            ShowTheShelf();
        }

        /// <summary>What the stage asked of the player's wallet since this was asked last, in the order it was asked, each once.</summary>
        public IReadOnlyList<WalletRequest> TakeWhatWasAskedFor()
        {
            WalletRequest[] taken = _asked.ToArray();
            _asked.Clear();
            return taken;
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
            ShowTheAd(deltaSeconds);
            _timeline.Advance(deltaSeconds * PaceOfTheTimeline());
            if (_timeline.IsDone)
            {
                FinishWhatWasShown();
            }

            // Before the gesture that waited: a step that begins at this rest decides whether the board takes it.
            ShowTheTutorial();
            ActOnTheWaitingGesture();
            ActOnTheWaitingUse();
            AskWhetherToLeaveOnceAtRest();
            TakeALongPress();
            _boosterCard?.Advance(deltaSeconds);
            _discs.Advance(_clock);
            ShowTheShelf();
            ShowTheHint();
        }

        /// <summary>
        /// The player asked for the way back, by the device's back button or gesture. While the game is played that
        /// asks whether they mean to leave; under that question it means "no"; under a result's card it leads to
        /// the museum; and a won game's finale it ends, as a touch does. Under the offer of extra moves, and while an
        /// ad is shown, it does nothing: nothing it could mean there costs nothing, and giving up costs a life.
        /// </summary>
        public void GoBack()
        {
            if (PlayerWentOn)
            {
                return;
            }

            TakeTheHintAway();
            if (_boosterCard != null)
            {
                if (_isBoosterCardDown)
                {
                    TakeTheBoosterCardUp();
                }
            }
            else if (IsShowingTheFinale)
            {
                SkipTheFinale();
            }
            else if (_offer != null)
            {
                if (ListensToTheOffer)
                {
                    TakeTheOfferUp();
                }
            }
            else if (_arming is Arming.Armed)
            {
                PutTheBoosterDown(); // the way back puts an armed booster down first
            }
            else if (_result == null)
            {
                AskToLeave();
            }
            else if (ListensToTheCard && !IsOfferingExtraMoves)
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

            TakeTheHintAway();
            _pointer.ClearSelection();
            ShowThePickedPiece();
            bool hadBegun = HasTheAttemptBegun;
            if (_tutorial?.Takes(move) == false || !_game.TryPlay(move))
            {
                ShowRefused(move);
                return false;
            }

            if (_tutorial?.Played(move) is TutorialStep)
            {
                PutTheLessonAway();
            }

            ReportTheBeginning(hadBegun);
            ReportTheStatus();
            _playing = Playback.ATurn;
            try
            {
                TurnScript script = TurnChoreographer.Compose(_ledger, _game.Level.Goals, _game.LastEvents, _game.Board);
                LastTurn = script;
                _turnPlayer.Play(script);
                Score(SoundScore.OfATurn(script));
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

        /// <summary>
        /// What happened in the attempt since this was asked last, in the order it happened, each reported once: the
        /// first move, a win or a loss for good the moment the engine decided it (long before it is shown), and the
        /// leaving card's press that leaves the level.
        /// </summary>
        public IReadOnlyList<AttemptEvent> TakeWhatHappened()
        {
            AttemptEvent[] taken = _happened.ToArray();
            _happened.Clear();
            return taken;
        }

        /// <summary>The cues the stage sounded since this was asked last, in the order they fell, each once.</summary>
        public IReadOnlyList<SoundCue> TakeWhatSounded() => _sounded.TakeAll();

        /// <summary>The steps of the tutorial done on this board since this was asked last, in the order they ended, each once.</summary>
        public IReadOnlyList<TutorialStep> TakeTheTutorialStepsDone() => _tutorial?.TakeWhatWasDone() ?? Array.Empty<TutorialStep>();

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
            TakeTheHintAway();
            if (_boosterCard != null)
            {
                _isATouchOnTheBoosterCard = _isBoosterCardDown; // the card is the finger's: its lift takes the card up
                return;
            }

            if (IsTheLessonSeen && _tutorial?.Touched() is TutorialStep)
            {
                PutTheLessonAway(); // a step that is only looked at ends with the touch, which does nothing else
                return;
            }

            if (IsShowingTheFinale)
            {
                SkipTheFinale();
                return;
            }

            if (_layout is not ScreenLayout layout)
            {
                return;
            }

            if (ListensForTheShelf)
            {
                _controls.FingerDown(PointAt(pixel, layout));
            }

            if (_controls.IsHolding)
            {
                _heldSince = _clock; // held long enough on a booster's disc, it is a long press
                return; // the finger is the pause disc's or a booster's disc's until it lifts
            }

            if (_offer != null)
            {
                if (ListensToTheOffer)
                {
                    _offer.Card.FingerDown(PointAt(pixel, layout));
                }
            }
            else if (_result != null)
            {
                if (ListensToTheCard)
                {
                    _result.FingerDown(PointAt(pixel, layout));
                }
            }
            else if (_game.Status == GameStatus.Playing && !_isAskedToLeave)
            {
                (float column, float row) = layout.BoardPointAt(pixel.x, pixel.y);
                if (_arming is Arming.Armed)
                {
                    _pointer.AimDown(column, row);
                }
                else
                {
                    _pointer.Down(column, row);
                }
            }
        }

        /// <summary>
        /// One more finger came down while the one the stage follows is still on the screen. It is given to
        /// nothing, as ever; but a won game's finale ends for it as for the first, so a touch anywhere skips the
        /// finale also while the finger that made the winning move rests on the glass.
        /// </summary>
        public void AnotherFingerDown()
        {
            TakeTheHintAway();
            if (IsShowingTheFinale)
            {
                SkipTheFinale();
            }
        }

        /// <summary>The finger moved while it was down.</summary>
        public void PointerMoved(Vector2 pixel)
        {
            if (_layout is not ScreenLayout layout || _boosterCard != null)
            {
                return;
            }

            if (_controls.IsHolding)
            {
                _controls.FingerMovedTo(PointAt(pixel, layout));
                return;
            }

            if (_offer != null)
            {
                if (ListensToTheOffer)
                {
                    _offer.Card.FingerMovedTo(PointAt(pixel, layout));
                }
            }
            else if (_result != null)
            {
                if (ListensToTheCard)
                {
                    _result.FingerMovedTo(PointAt(pixel, layout));
                }
            }
            else
            {
                (float column, float row) = layout.BoardPointAt(pixel.x, pixel.y);
                if (_arming is Arming.Armed)
                {
                    _pointer.AimMovedTo(column, row);
                }
                else if (_pointer.MovedTo(column, row) is BoardGesture swipe)
                {
                    Take(swipe);
                }
            }
        }

        /// <summary>The finger lifted.</summary>
        public void PointerUp()
        {
            _heldSince = null;
            if (_boosterCard != null)
            {
                if (_isATouchOnTheBoosterCard && _isBoosterCardDown)
                {
                    TakeTheBoosterCardUp();
                }

                _isATouchOnTheBoosterCard = false;
                return;
            }

            if (_controls.IsHolding)
            {
                if (_controls.FingerUp(out StageControl pressed))
                {
                    _sounded.Add(SoundCue.Press);
                    Press(pressed);
                }

                return;
            }

            if (_offer != null)
            {
                if (ListensToTheOffer && _offer.Card.FingerUp(out OfferPress asked))
                {
                    _sounded.Add(SoundCue.Press);
                    Answer(asked);
                }
            }
            else if (_result != null)
            {
                if (ListensToTheCard && _result.FingerUp(out ResultChoice choice))
                {
                    _sounded.Add(SoundCue.Press);
                    Choose(_result, choice);
                }
            }
            else if (_arming is Arming.Armed armed)
            {
                // Lifting over the board uses the booster where it was aimed; lifting off the board puts it down.
                if (_pointer.AimUp() is BoardGesture.Aim aim)
                {
                    Use(UseOf(armed.Booster, aim.Cell));
                }
                else
                {
                    PutTheBoosterDown();
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
            _heldSince = null;
            _isATouchOnTheBoosterCard = false;
            _pointer.Cancel();
            _waiting = null;
            _controls.FingerGone();
            _result?.FingerGone();
            _offer?.Card.FingerGone();
        }

        public void Dispose()
        {
            _wall.Dispose();
            _lamps.Dispose();
            _hud.Dispose();
            _frame.Dispose();
            _board.Dispose();
            _shelf.Dispose();
            _pill.Dispose();
            _veil.Dispose();
            _secondVeil.Dispose();
            _spotlight.Dispose();
            _lessonPill.Dispose();
            _boosterCard?.Dispose();
            _result?.Dispose();
            _offer?.Dispose();
            _ad?.Card.Dispose();
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
        /// The engine has just played a move or used a booster: if nothing had begun the attempt before it and it does,
        /// it is the attempt's first move, which is reported now, before any of it is shown (a booster counts as a move;
        /// the free one the tutorial has used by hand does not, <see cref="HasTheAttemptBegun"/>).
        /// </summary>
        private void ReportTheBeginning(bool hadBegun)
        {
            if (!hadBegun && HasTheAttemptBegun)
            {
                _happened.Add(new AttemptEvent.FirstMove(_brief.StartBoosters));
            }
        }

        /// <summary>
        /// True once the player's own move or booster has begun the attempt, or the game is decided: the boosters the
        /// tutorial had used by hand, free, begin nothing (#7), so leaving after them alone costs nothing.
        /// </summary>
        private bool HasTheAttemptBegun => AttemptBegun.In(_game, GuidedBoosters);

        /// <summary>
        /// The engine has just played a move or used a booster: if that decided the game, it is reported now, before
        /// any of it is shown. A game whose moves ran out for the first time is not decided yet: the offer of extra
        /// moves is answered first, and the answer reports what it leads to.
        /// </summary>
        private void ReportTheStatus()
        {
            switch (_game.Status)
            {
                case GameStatus.Playing:
                    break;
                case GameStatus.Won:
                    _happened.Add(new AttemptEvent.Won(_game.MovesLeft));
                    break;
                case GameStatus.LostOutOfMoves when OutOfMovesOffer.IsMadeIn(_game):
                    break;
                case GameStatus.LostOutOfMoves:
                    _happened.Add(new AttemptEvent.LostForGood(LossReason.OutOfMoves));
                    break;
                case GameStatus.LostNoMovesPossible:
                    _happened.Add(new AttemptEvent.LostForGood(LossReason.NoMovePossible));
                    break;
                default:
                    throw new InvalidOperationException($"A game that ended as {_game.Status} is not known to the stage.");
            }
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
            else if (_game.Status == GameStatus.Playing && !_isAskedToLeave)
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

        /// <summary>The timeline has run out: if it was showing a turn, a booster or a finale, that is finished now.</summary>
        private void FinishWhatWasShown()
        {
            switch (_playing)
            {
                case Playback.Nothing:
                    break;
                case Playback.ATurn:
                case Playback.ABooster:
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
            PutTheBoosterDown();

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
                Score(SoundScore.OfTheFinale(script));
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
        /// Puts what is heard of a script on the timeline beside what is seen of it: each cue sounds in the step that
        /// reaches its moment, at the pace the script is played (a hurried finale's sounds are hurried with it), and a
        /// script cut short (a finale skipped) takes the sounds it had not reached with it. Every moment lies within
        /// the script, so the turn ends when it did without them.
        /// </summary>
        private void Score(IReadOnlyList<SoundNote> notes)
        {
            foreach (SoundNote note in notes)
            {
                _timeline.At(note.At, () => _sounded.Add(note.Cue));
            }
        }

        /// <summary>
        /// The game is decided, or its moves have run out for the first time: the board is left to be seen for a
        /// moment, then the room goes dark, the result's card (or the offer of extra moves) comes down on its wires,
        /// and what it shows in its alcove pops up as it settles.
        /// </summary>
        private void HangTheResult()
        {
            // The button to the jigsaw speaks of every piece that waits on the painting's tray once the level's own are won.
            int piecesWaiting = _game.Status == GameStatus.Won ? _brief.Reward?.WaitingWithThem ?? 0 : 0;
            _isAskedToLeave = false;
            _controls.FingerGone();
            PutTheBoosterDown();
            ResultPanel card = OutOfMovesOffer.IsMadeIn(_game)
                ? ResultPanel.OfferTheMoves(_root.transform, _art, _effectArt, _paintings, LevelResult.OfferTo(_game, _brief), TermsOfTheExtraMoves, _wallet.Handling)
                : ResultPanel.Create(_root.transform, _art, _effectArt, _paintings, LevelResult.Of(_game, _brief), piecesWaiting, _wallet.Handling);
            HangACard(card, comesIn: Tempo.PauseBeforeResult, roomIsDark: false);
        }

        /// <summary>The room goes dark, unless it is already, and a card comes down on its wires; what it shows in its alcove pops up as it settles.</summary>
        /// <param name="comesIn">How long the board, or the card going up before it, is left to be seen before the room darkens and the card comes.</param>
        private void HangACard(ResultPanel card, float comesIn, bool roomIsDark)
        {
            if (_layout is ScreenLayout layout)
            {
                card.Place(layout);
            }

            _result = card;
            _isCardDown = false;
            float cardComes = comesIn + Tempo.CardAfterDark;
            float exhibitsSeconds = card.ExhibitsSeconds;
            if (!roomIsDark)
            {
                _timeline.Add(comesIn, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)), _lamps.PutOut);
            }

            _timeline.At(cardComes, () => _sounded.Add(SoundScore.OfACard(card.Tells)));
            _timeline.Add(cardComes, Tempo.CardComesDown, card.ComeDown, () => _isCardDown = true);
            _timeline.Add(cardComes + Tempo.ExhibitsAfterCard, exhibitsSeconds, progress => card.ShowExhibits(progress * exhibitsSeconds));
        }

        /// <summary>The terms of the extra moves the stage was told last, which the offer's card shows.</summary>
        /// <exception cref="InvalidOperationException">Nobody told the stage the terms (<see cref="Tell"/>): its owner must, before a game can run out of moves.</exception>
        private ExtraMovesTerms TermsOfTheExtraMoves => _extraMoves
            ?? throw new InvalidOperationException("The stage was never told what the extra moves cost; whoever owns the wallet tells it with Tell.");

        /// <summary>
        /// The player wants to leave the level (the pause disc, or the way back). From now on the board takes no
        /// touch: a piece picked is put down and a gesture that waits is forgotten. If the board is at rest the
        /// question is put at once; if pieces are still moving it waits for them, and is dropped if their move
        /// decides the game.
        /// </summary>
        private void AskToLeave()
        {
            _isAskedToLeave = true;
            _pointer.Cancel();
            _pointer.ClearSelection();
            _waiting = null;
            PutTheBoosterDown();
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

            HangACard(ResultPanel.Create(_root.transform, _art, _effectArt, _paintings, LevelResult.ForLeaving(_game, _brief, GuidedBoosters), piecesWaiting: 0, _wallet.Handling), comesIn: 0f, roomIsDark: false);
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
            _timeline.At(Tempo.CardAfterDark, () => _sounded.Add(SoundCue.CardDown)); // the win was heard as the card first came
            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, card.ComeDown, () => _isCardDown = true);
        }

        /// <summary>
        /// The player pressed one of the card's buttons: the card goes up out of sight, and then this stage is over,
        /// unless the choice was to stay in the game. A level that is left while it is played is reported left at
        /// the press: what leaving costs is the player's from that moment, not from the card's going up.
        /// </summary>
        private void SendTheCardUp(ResultPanel card, ResultChoice choice)
        {
            _isCardDown = false;
            if (choice == ResultChoice.Stay)
            {
                GoOnWithTheGame(card);
                return;
            }

            if (IsAskingWhetherToLeave)
            {
                _happened.Add(new AttemptEvent.Left(afterAMove: HasTheAttemptBegun));
            }

            _timeline.Add(0f, Tempo.CardGoesUp, card.GoUp, () => PlayerChose = choice);
        }

        // ---------- the offer of extra moves ----------

        /// <summary>A button of the card in front of the board was pressed: the offer's are answered here, every other card's lead on.</summary>
        private void Choose(ResultPanel card, ResultChoice choice)
        {
            switch (choice)
            {
                case ResultChoice.TakeTheMovesForCoins:
                    TakeTheMoves(ExtraMovesPayment.ByCoins);
                    break;
                case ResultChoice.TakeTheMovesForAnAd:
                    TakeTheMoves(ExtraMovesPayment.ByAd);
                    break;
                case ResultChoice.GiveUp:
                    _happened.Add(OutOfMovesOffer.GiveUp(_game));
                    ReplaceTheOfferWith(LevelResult.LostForGood(_game, _brief, LossReason.GaveUp));
                    break;
                default:
                    SendTheCardUp(card, choice);
                    break;
            }
        }

        /// <summary>
        /// The player asked for the extra moves, paid one way. A way the terms do not allow is dimmed and takes no
        /// finger, so a press that gets here anyway changes nothing. Otherwise the engine is asked: refused, nothing is
        /// paid and the card of the loss for good takes the offer's place; given and paid with coins, the payment is
        /// reported and the game goes on; given and paid with an ad, the ad is shown first.
        /// </summary>
        private void TakeTheMoves(ExtraMovesPayment paidBy)
        {
            switch (OutOfMovesOffer.Take(_game, paidBy, TermsOfTheExtraMoves))
            {
                case OfferAnswer.CannotPay _:
                    break;
                case OfferAnswer.Refused refused:
                    _happened.Add(refused.Loss);
                    ReplaceTheOfferWith(LevelResult.LostForGood(_game, _brief, refused.Loss.Reason));
                    break;
                case OfferAnswer.MovesGiven given when paidBy == ExtraMovesPayment.ByAd:
                    ShowAnAdFor(given.Payment);
                    break;
                case OfferAnswer.MovesGiven given:
                    _happened.Add(given.Payment);
                    GoOnWithTheExtraMoves();
                    break;
                default:
                    throw new InvalidOperationException("The offer of extra moves gave an answer the stage does not know.");
            }
        }

        /// <summary>The offer was turned down or refused: its card goes up in the dark room and the card of the loss for good comes down in its place.</summary>
        private void ReplaceTheOfferWith(LevelResult lost)
        {
            ResultPanel offer = _result ?? throw new InvalidOperationException("No offer hangs to be replaced.");
            _isCardDown = false;
            offer.FingerGone();
            _timeline.Add(0f, Tempo.CardGoesUp, offer.GoUp, offer.Dispose);
            HangACard(ResultPanel.Create(_root.transform, _art, _effectArt, _paintings, lost, piecesWaiting: 0, _wallet.Handling), comesIn: Tempo.CardGoesUp, roomIsDark: true);
        }

        /// <summary>
        /// The engine gave the moves: the offer's card goes up, the room lights, and the medallion counts the moves
        /// up one at a time, each with a swell of its number; a board that had no move left is dealt anew meanwhile.
        /// Once all of it is over the board is the player's again, and the shelf with it.
        /// </summary>
        private void GoOnWithTheExtraMoves()
        {
            ResultPanel offer = _result ?? throw new InvalidOperationException("No offer hangs to go up.");
            _isCardDown = false;
            offer.FingerGone();
            float roomLit = Tempo.CardGoesUp / 2f + Tempo.RoomLightens;
            _timeline.Add(0f, Tempo.CardGoesUp, offer.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => _veil.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    offer.Dispose();
                    _result = null;
                    _lamps.LightAgain();
                });

            HudFigures countedUp = HudFigures.Of(_game);
            int shownBefore = _figures.MovesLeft;
            for (int shown = shownBefore + 1; shown <= countedUp.MovesLeft; shown++)
            {
                HudFigures counted = countedUp.WithMovesShown(shown);
                _timeline.At(roomLit + (shown - shownBefore - 1) * ExtraMovesCountedApart, () => CountAnExtraMoveUp(counted));
            }

            ShowTheReshuffleOfTheExtraMoves();
        }

        /// <summary>One more of the extra moves is counted on the medallion, whose number swells as it does.</summary>
        private void CountAnExtraMoveUp(HudFigures counted)
        {
            _figures = counted;
            _hud.Show(_figures);

            // A tween and not an act: this is called by an act, which may not add acts to the timeline it is shown by.
            _tweens.Start(new Tween(0f, Tempo.MovesSwell, progress => 1f - Easing.In(progress), _hud.SwellTheMoves));
        }

        /// <summary>
        /// A board that had no move left was dealt anew with the extra moves: that is shown as a turn's reshuffle is,
        /// and the stage comes to rest on the engine's board after it. A board that had a move gets nothing to show.
        /// </summary>
        private void ShowTheReshuffleOfTheExtraMoves()
        {
            if (_game.LastEvents.Count == 0)
            {
                return;
            }

            _playing = Playback.ATurn;
            try
            {
                TurnScript script = TurnChoreographer.ComposeTheExtraMoves(_ledger, _game.LastEvents, _game.Board);
                _turnPlayer.Play(script);
                Score(SoundScore.OfATurn(script));
            }
            catch (InvalidOperationException fault)
            {
                // The engine has dealt the board anew: only its picture is lost, as a turn's would be.
                _faultsOfTheTurn.Add($"the reshuffle of the extra moves could not be shown: {fault.Message}");
                _effects.ClearAway();
                _ledger.StartAgainFrom(_game.Board);
                _board.ShowAnew(_ledger);
            }
        }

        /// <summary>
        /// The engine gave the moves for an ad: the room behind darkens once more and the card that stands in for an
        /// ad comes down in front of the offer's. Its seconds run once it hangs in its place; the payment is reported
        /// when it is over (<see cref="ShowTheAd"/>).
        /// </summary>
        private void ShowAnAdFor(AttemptEvent.ExtraMovesTaken payment)
        {
            _isCardDown = false;
            _result?.FingerGone();
            var card = new SimulatedAdCard(_root.transform, _art, new AdPrize.ExtraMoves(), (int)DrawOrder.OverACard);
            if (_layout is ScreenLayout layout)
            {
                card.Place(layout.PlaceOfACard(card.Width, card.Height), layout.View);
            }

            var ad = new AdOnTheStage(card, Ads.Show(_brief.AdsShownAs), payment);
            _ad = ad;
            _timeline.Add(0f, Tempo.RoomDarkens, progress => _secondVeil.Darken(Easing.Out(progress)));
            _timeline.At(Tempo.CardAfterDark, () => _sounded.Add(SoundCue.CardDown));
            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, card.ComeDown, () => ad.IsDown = true);
        }

        /// <summary>
        /// The ad's seconds run while its card hangs in its place, and its bar shows them. When it is over the
        /// payment is reported, its card goes up, the second dark lifts, and the game goes on with the extra moves.
        /// </summary>
        private void ShowTheAd(float deltaSeconds)
        {
            if (!(_ad is AdOnTheStage ad) || !ad.IsDown || ad.HasPaid)
            {
                return;
            }

            ad.Ad.Advance(deltaSeconds);
            ad.Card.Show(ad.Ad);
            if (!ad.Ad.IsOver)
            {
                return;
            }

            ad.HasPaid = true;
            _happened.Add(ad.Payment);
            _timeline.Add(0f, Tempo.CardGoesUp, ad.Card.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => _secondVeil.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    ad.Card.Dispose();
                    _ad = null;
                });
            GoOnWithTheExtraMoves();
        }

        // ---------- the boosters of the shelf ----------

        /// <summary>A finger pressed something on the shelf: the pause disc asks whether to leave, a booster's disc does what it does.</summary>
        private void Press(StageControl pressed)
        {
            switch (pressed)
            {
                case StageControl.Pause _:
                    AskToLeave(); // as the device's way back does while the game is played: the disc listens only then
                    break;
                case StageControl.Disc disc:
                    PressTheDisc(disc.Booster);
                    break;
                default:
                    throw new InvalidOperationException($"The shelf has nothing of the kind {pressed.GetType().Name}.");
            }
        }

        /// <summary>
        /// A booster's disc was pressed. With none held it offers one more; the armed one is put down; the shuffle is
        /// used at once; another booster is armed. While a use waits for the board to come to rest, a disc does nothing
        /// more. While the tutorial has a booster used by hand, only its disc does anything (<see cref="PressTheDiscUsedByHand"/>);
        /// while another step shows, none does.
        /// </summary>
        private void PressTheDisc(BoosterKind booster)
        {
            if (IsTheLessonSeen)
            {
                if (_tutorial?.TakesThePress(booster) == true)
                {
                    PressTheDiscUsedByHand(booster);
                }

                return; // the shelf's other boosters wait while a step of the tutorial shows
            }

            if (_arming is Arming.Waiting)
            {
                return;
            }

            if (!_owned.Has(booster))
            {
                HangTheOffer(booster);
            }
            else if (_arming is Arming.Armed armed && armed.Booster == booster)
            {
                PutTheBoosterDown();
            }
            else if (booster == BoosterKind.Shuffle)
            {
                Use(new BoosterUse.Shuffle());
            }
            else
            {
                Arm(booster);
            }
        }

        /// <summary>
        /// The disc of the booster the tutorial has used by hand: it is free, so it is armed (or, the shuffle, used)
        /// whether the player holds one or not, and a press on it while it is armed keeps it armed.
        /// </summary>
        private void PressTheDiscUsedByHand(BoosterKind booster)
        {
            if (booster == BoosterKind.Shuffle)
            {
                Use(new BoosterUse.Shuffle());
            }
            else if (ArmedBooster != booster)
            {
                Arm(booster);
            }
        }

        /// <summary>Arms a booster that strikes: a piece picked is put down and a gesture that waits is forgotten, and the board's finger aims from now on.</summary>
        private void Arm(BoosterKind booster)
        {
            _pointer.Cancel();
            _pointer.ClearSelection();
            _waiting = null;
            _refused = null;
            _arming = new Arming.Armed(booster);
            ShowTheShelf();
        }

        /// <summary>Puts an armed booster down again, and drops a use that waits; the board's finger moves pieces again.</summary>
        private void PutTheBoosterDown()
        {
            if (_arming is Arming.Nothing)
            {
                return;
            }

            _pointer.Cancel();
            _arming = Arming.None;
            ShowTheShelf();
        }

        /// <summary>A use is made at once on a board at rest; on a board that moves it waits for the rest, its aim shown meanwhile.</summary>
        private void Use(BoosterUse use)
        {
            if (TakesInput)
            {
                TryUse(use);
            }
            else if (_game.Status == GameStatus.Playing && !_isAskedToLeave)
            {
                _arming = new Arming.Waiting(use);
                ShowTheShelf();
            }
        }

        /// <summary>Once the board has come to rest, the use that waited for it is made, unless the game was decided meanwhile.</summary>
        private void ActOnTheWaitingUse()
        {
            if (!(_arming is Arming.Waiting waiting) || !IsSettled)
            {
                return;
            }

            if (TakesInput)
            {
                TryUse(waiting.Use);
            }
            else
            {
                PutTheBoosterDown();
            }
        }

        /// <summary>
        /// Asks the engine to use a booster the player holds. Refused, nothing is spent: the disc shakes and the pill
        /// says why, and a booster that strikes stays armed for another aim. Accepted, it is reported (as the first
        /// move when nothing began the attempt before) and shown as a turn is, with its tool and no move spent. The use
        /// the tutorial has made by hand is free, and needs no booster held; while it waits, any other aim is refused
        /// before the engine is asked, with the disc's shake.
        /// </summary>
        private void TryUse(BoosterUse use)
        {
            BoosterKind booster = BoosterKinds.Of(use);
            bool hadBegun = HasTheAttemptBegun;
            bool isFree = _tutorial?.IsFree(use) == true;
            TakeTheHintAway();
            if (_tutorial?.TakesTheUse(use) == false)
            {
                Refuse(booster);
                return;
            }

            if (!isFree && !_owned.Has(booster))
            {
                PutTheBoosterDown(); // the wallet was told it holds none since it was armed
                return;
            }

            if (!_game.TryUseBooster(use))
            {
                _refused = (booster, _clock + Tempo.RefusalSaid);
                Refuse(booster);
                return;
            }

            if (!isFree)
            {
                _owned = _owned.WithOneFewer(booster);
            }

            _refused = null;
            _arming = Arming.None;
            bool endsAStep = _tutorial?.Used(use) is TutorialStep; // named as used by hand first: then it begins nothing
            ReportTheBeginning(hadBegun);
            _happened.Add(isFree ? new AttemptEvent.GuidedBoosterUsed(booster) : (AttemptEvent)new AttemptEvent.BoosterAccepted(booster));
            if (endsAStep)
            {
                PutTheLessonAway();
            }

            ReportTheStatus();
            _playing = Playback.ABooster;
            try
            {
                TurnScript script = TurnChoreographer.ComposeBooster(_ledger, _game.Level.Goals, _game.LastEvents, _game.Board);
                LastBooster = script;
                _turnPlayer.Play(script);
                Score(SoundScore.OfABooster(script));
            }
            catch (InvalidOperationException fault)
            {
                // The engine has used the booster, so it counts. Only its picture is lost, as a turn's would be.
                _faultsOfTheTurn.Add($"the booster could not be shown: {fault.Message}");
                _timeline.Clear();
                ShowTheEnginesBoardAnew();
            }

            ShowTheShelf();
        }

        /// <summary>A use that was not made: the disc shakes, and a booster that strikes stays armed for another aim.</summary>
        private void Refuse(BoosterKind booster)
        {
            _sounded.Add(SoundCue.Refuse);
            _discs.Shake(booster, _clock);
            _arming = booster == BoosterKind.Shuffle ? Arming.None : new Arming.Armed(booster);
            ShowTheShelf();
        }

        /// <summary>The booster a disc of the shelf aims at a cell.</summary>
        private static BoosterUse UseOf(BoosterKind booster, Position cell) => booster switch
        {
            BoosterKind.HitCell => new BoosterUse.HitCell(cell),
            BoosterKind.HitRow => new BoosterUse.HitRow(cell),
            BoosterKind.HitColumn => new BoosterUse.HitColumn(cell),
            BoosterKind.Shuffle => new BoosterUse.Shuffle(),
            _ => throw new ArgumentException($"{booster.Name()} is taken before a level; nothing aims it on the board.", nameof(booster)),
        };

        /// <summary>The plus on a disc of which none is held: the room darkens and the card that sells one comes down over the board.</summary>
        private void HangTheOffer(BoosterKind booster)
        {
            PutTheBoosterDown();
            _pointer.Cancel();
            _pointer.ClearSelection();
            _waiting = null;
            _controls.FingerGone();
            var offer = new OfferCard<OfferPress>(
                _root.transform, _art, booster, BoosterWords.NameOf(booster), PlayBoosterPictures.Of(booster), BoosterWords.OnTheCard(booster),
                BoosterWords.CostsNoMove, _shelfBrief.PriceOf(booster), new OfferPress.Close(), new OfferPress.Buy(booster), raisedBy: 0);
            offer.ShowAffordable(_shelfBrief.CanBuy(booster, _wallet));
            if (_layout is ScreenLayout layout)
            {
                offer.Card.Place(layout.PlaceOfACard(offer.Card.Width, offer.Card.Height), layout.View);
            }

            _offer = offer;
            OfferedBooster = booster;
            _isOfferDown = false;
            _timeline.Add(0f, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)));
            _timeline.At(Tempo.CardAfterDark, () => _sounded.Add(SoundCue.CardDown));
            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, offer.Card.ComeDown, () => _isOfferDown = true);
            ShowTheShelf();
        }

        /// <summary>A button of the offer's card was pressed: the cross takes it up, the green button asks for one booster, which the wallet's answer arms.</summary>
        private void Answer(OfferPress asked)
        {
            switch (asked)
            {
                case OfferPress.Close _:
                    TakeTheOfferUp();
                    break;
                case OfferPress.Buy buy:
                    _asked.Add(new WalletRequest.BuyABooster(buy.Booster));
                    _armOnceHeld = buy.Booster;
                    break;
                default:
                    throw new InvalidOperationException($"The offer's card has no button of the kind {asked.GetType().Name}.");
            }
        }

        /// <summary>The offer's card goes up, the room lights again, and once it is gone the board is the player's as before.</summary>
        private void TakeTheOfferUp()
        {
            if (!(_offer is OfferCard<OfferPress> offer))
            {
                return;
            }

            _isOfferDown = false;
            _armOnceHeld = null;
            offer.Card.FingerGone();
            _timeline.Add(0f, Tempo.CardGoesUp, offer.Card.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => _veil.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    offer.Dispose();
                    if (ReferenceEquals(_offer, offer))
                    {
                        _offer = null;
                        OfferedBooster = null;
                    }
                });
        }

        /// <summary>
        /// Shows the shelf as the stage stands: each disc's face, the aim of an armed booster under the finger (or of
        /// a use that waits), and what the pill says. Asked at every step; what has not changed is not drawn anew.
        /// </summary>
        private void ShowTheShelf()
        {
            bool isPlayed = _game.Status == GameStatus.Playing && !PlayerWentOn && !IsShowingTheFinale;
            bool isAStepThatTakesNoDisc = IsTheLessonSeen && !(_tutorial?.Showing is Lesson.Booster || _tutorial?.Showing is Lesson.Look { Spot: LessonSpot.Disc _ });
            _discs.Show(_owned, ArmedBooster, isListening: isPlayed && ListensForTheShelf && !isAStepThatTakesNoDisc);
            BoosterUse? aimed = _arming switch
            {
                Arming.Armed armed when _pointer.AimedAt is Position cell => UseOf(armed.Booster, cell),
                Arming.Waiting waiting => waiting.Use,
                _ => null,
            };
            _effects.ShowTheAim(aimed ?? TargetWaitingForATouch, _clock);
            PillWords words = PillNow(isPlayed);
            if (!words.Equals(_pill.Words))
            {
                _pill.Show(words);
            }

            _pill.LetTheBoardShowThrough(_pointer.AimedAt != null);
        }

        /// <summary>
        /// What the pill says at this step: why a use was refused, for a moment after; what the armed booster is and
        /// what to pick; until the attempt begins, how to play or which try this is. Nothing while a card hangs or a
        /// step of the tutorial shows (it speaks on its own pill), once the game is decided, and once the attempt has begun.
        /// </summary>
        private PillWords PillNow(bool isPlayed)
        {
            if (!isPlayed || _result != null || _offer != null || _boosterCard != null || IsTheLessonSeen)
            {
                return PillWords.None; // a step of the tutorial speaks on its own pill
            }

            if (_refused.HasValue && _clock < _refused.Value.Until)
            {
                return new PillWords.Line(BoosterWords.Refused(_refused.Value.Booster));
            }

            if (ArmedBooster is BoosterKind armed)
            {
                return armed == BoosterKind.Shuffle
                    ? new PillWords.Line(BoosterWords.Armed(armed))
                    : new PillWords.Armed(BoosterWords.Armed(armed), BoosterWords.WhatToPick(armed));
            }

            if (HasTheAttemptBegun)
            {
                return PillWords.None;
            }

            return new PillWords.Line(AttemptWords.OnTheShelf(_brief));
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

        // ---------- the card of a long press ----------

        /// <summary>
        /// A finger held on a booster's disc for <see cref="Tempo.LongPress"/>: the lift will press nothing, and the
        /// card that shows the booster comes down, an armed booster put down first. A finger that slid off the disc,
        /// or holds the pause disc, makes no long press.
        /// </summary>
        private void TakeALongPress()
        {
            if (!(_heldSince is float since) || _clock - since < Tempo.LongPress)
            {
                return;
            }

            _heldSince = null;
            if (!_controls.TryGetHeld(out StageControl held) || !(held is StageControl.Disc disc))
            {
                return;
            }

            _controls.FingerGone();
            HangTheBoosterCard(disc.Booster);
        }

        /// <summary>The room darkens and the card that shows the booster comes down over the board; a step of the tutorial waits under it.</summary>
        private void HangTheBoosterCard(BoosterKind booster)
        {
            PutTheBoosterDown();
            _pointer.Cancel();
            _pointer.ClearSelection();
            _waiting = null;
            TakeTheHintAway();
            var card = new BoosterCard<StageControl>(_root.transform, _art, _effectArt, booster, LevelStart.Rules.StartBoosterExtraMoves, raisedBy: 0);
            if (_layout is ScreenLayout layout)
            {
                card.Card.Place(layout.PlaceOfACard(card.Card.Width, card.Card.Height), layout.View);
            }

            _boosterCard = card;
            _isBoosterCardDown = false;
            _isATouchOnTheBoosterCard = false;
            _timeline.Add(0f, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)));
            _timeline.At(Tempo.CardAfterDark, () => _sounded.Add(SoundCue.CardDown));
            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, card.Card.ComeDown, () => _isBoosterCardDown = true);
            ShowTheShelf();
        }

        /// <summary>A touch or the way back: the card goes up, the room lights again, and once it is gone the board is the player's as before.</summary>
        private void TakeTheBoosterCardUp()
        {
            if (!(_boosterCard is BoosterCard<StageControl> card))
            {
                return;
            }

            _isBoosterCardDown = false;
            _timeline.Add(0f, Tempo.CardGoesUp, card.Card.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => _veil.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    card.Dispose();
                    if (ReferenceEquals(_boosterCard, card))
                    {
                        _boosterCard = null;
                    }
                });
        }

        // ---------- the hint ----------

        /// <summary>
        /// The hint at this step: one is thought out and shown once the board has lain quiet and untouched for the
        /// wait, one that shows goes on breathing and nudging, and the light goes out the moment the board is not
        /// quiet any more. Quiet: the board takes a finger, no booster is armed or waits, no finger holds the board or
        /// a disc, and no piece is picked (the picked piece's mark would fight the hint's over the same piece).
        /// </summary>
        private void ShowTheHint()
        {
            if (_hintBrief is not HintBrief brief || _hintWatch is not HintWatch watch)
            {
                return;
            }

            bool isQuiet = IsQuietForTheTutorial && _pointer.Selected == null && _tutorial?.Showing == null;
            if (watch.IsDue(_clock, isQuiet))
            {
                ThinkOutAHint(brief, watch);
            }

            if (watch.IsShowing && _hint is Move hint)
            {
                _hintMark.Show(hint, _board.Piece(_ledger.PieceAt(hint.From)), hint.Kind == MoveKind.Tap ? null : _board.Piece(_ledger.PieceAt(hint.To)), _clock - watch.ShownAt);
            }
            else
            {
                _hintMark.Hide();
            }
        }

        /// <summary>The advisor thinks out the move on a copy of the game; a move whose cells hold pieces to light is shown, and the phone ticks for it.</summary>
        private void ThinkOutAHint(HintBrief brief, HintWatch watch)
        {
            if (brief.Advisor.MoveFor(_game, brief.AttemptSeed) is Move hint && HoldsAPiece(hint.From) && (hint.Kind == MoveKind.Tap || HoldsAPiece(hint.To)))
            {
                _hint = hint;
                watch.Show(_clock);
                _sounded.Add(SoundCue.Hint);
            }
            else
            {
                watch.FoundNoMove(_clock);
            }
        }

        private bool HoldsAPiece(Position cell) => _ledger.PieceAt(cell) != PieceLedger.NoPiece;

        /// <summary>A touch, the way back, a move or a booster: a hint that shows goes at once, its pieces at rest, and the wait for the next starts again.</summary>
        private void TakeTheHintAway()
        {
            _hintWatch?.Touched(_clock);
            _hintMark.Hide();
            _hint = null;
        }

        // ---------- the tutorial ----------

        /// <summary>The board lies at rest and takes a finger, nothing is armed and no finger holds the board or a disc: a step of the tutorial may begin.</summary>
        private bool IsQuietForTheTutorial => TakesInput && _arming is Arming.Nothing && !_controls.IsHolding && !_pointer.IsPressing;

        /// <summary>A step of the tutorial shows and is seen: no card hangs over it, nothing asks whether to leave, and the game is played.</summary>
        private bool IsTheLessonSeen =>
            _tutorial?.Showing != null && _result == null && _offer == null && _boosterCard == null && !_isAskedToLeave && _game.Status == GameStatus.Playing && !PlayerWentOn;

        /// <summary>True while the tutorial has this booster used by hand, and the step is seen.</summary>
        private bool IsUsedByHand(BoosterKind booster) => IsTheLessonSeen && _tutorial?.Showing is Lesson.Booster lesson && lesson.Kind == booster;

        /// <summary>The use the tutorial has made by hand once its booster is armed, which the target shows the aim of until it is touched; nothing otherwise.</summary>
        private BoosterUse? TargetWaitingForATouch =>
            IsTheLessonSeen && _tutorial?.Showing is Lesson.Booster lesson && ArmedBooster == lesson.Kind && !(lesson.Use is BoosterUse.Shuffle) ? lesson.Use : null;

        /// <summary>The board as the tutorial is asked of it: a step is due on it only when it changed or a step ended.</summary>
        private (int Moves, int Boosters, bool ExtraMoves) BoardForTheTutorial => (_game.MovesUsed, _game.BoostersUsed.Count, _game.ExtraMovesTaken);

        /// <summary>
        /// The tutorial at this step: at a quiet moment on a board it has not been asked of, a step that is due begins (a
        /// hint that shows goes, a picked piece is put down); the step that shows is seen dimming in, or hidden while a
        /// card hangs over it, and a move shown by hand has its two pieces lit while no finger is on the board.
        /// </summary>
        private void ShowTheTutorial()
        {
            if (_tutorial is not TutorialWatch watch)
            {
                return;
            }

            if (watch.Showing == null && watch.MayStillCome.Count > 0 && IsQuietForTheTutorial && _tutorialAskedOf != BoardForTheTutorial)
            {
                _tutorialAskedOf = BoardForTheTutorial;
                if (watch.BeginWhatIsDue(_game) != null)
                {
                    TakeTheHintAway();
                    _pointer.ClearSelection();
                    _waiting = null;
                }
            }

            bool isSeen = IsTheLessonSeen;
            if (isSeen && !_wasLessonSeen)
            {
                _lessonSeenSince = _clock;
            }

            _wasLessonSeen = isSeen;
            if (!isSeen || !(watch.Showing is Lesson lesson))
            {
                HideTheLesson();
                return;
            }

            bool isArmed = lesson is Lesson.Booster booster && ArmedBooster == booster.Kind;
            if ((!ReferenceEquals(_lessonLaid, lesson) || _lessonLaidArmed != isArmed) && _layout is ScreenLayout layout)
            {
                LayTheLesson(lesson, layout);
            }

            _discs.Guide(DiscRingedFor(lesson, isArmed));
            float seenFor = _clock - _lessonSeenSince;
            _spotlight.Show(Easing.Out(Mathf.Clamp01(seenFor / LessonComesIn)));
            bool isTheBoardUntouched = IsSettled && !_pointer.IsPressing && _pointer.Selected == null;
            if (lesson is Lesson.Guided guided && isTheBoardUntouched && HoldsAPiece(guided.Move.From) && HoldsAPiece(guided.Move.To))
            {
                _guideMark.Show(guided.Move, _board.Piece(_ledger.PieceAt(guided.Move.From)), _board.Piece(_ledger.PieceAt(guided.Move.To)), seenFor);
            }
            else
            {
                _guideMark.Hide();
            }
        }

        /// <summary>
        /// Lays out what a step shows: the windows of its spotlight around the two pieces of a move and the line they
        /// make, around an obstacle's cell or around a plaque, and its pill over the shelf, or under the plaques when it
        /// speaks of one or when what it lights lies where the pill would stand over the shelf.
        /// </summary>
        private void LayTheLesson(Lesson lesson, ScreenLayout layout)
        {
            _lessonLaid = lesson;
            _lessonLaidArmed = lesson is Lesson.Booster asked && ArmedBooster == asked.Kind;
            _lessonPill.ShowThePainter(PainterPictures.MoodOf(lesson.Step));
            _lessonPill.Show(lesson is Lesson.Booster used
                ? TutorialWords.OfTheBooster(used.Kind, _lessonLaidArmed)
                : TutorialWords.Of(lesson.Step, asksForATouch: lesson is Lesson.Look));
            float scale = Mathf.Max(layout.ShelfScale, LessonPillLeastScale);
            switch (lesson)
            {
                case Lesson.Booster booster:
                    LightUpTheDiscAndItsTarget(booster, layout, scale);
                    break;
                case Lesson.Guided guided:
                    StandThePillClearOf(LightUpTheTrade(guided.Move, layout), layout, scale);
                    break;
                case Lesson.Look { Spot: LessonSpot.Disc disc }:
                    LightUpTheDisc(disc.Booster, Array.Empty<Box>(), layout, scale);
                    break;
                case Lesson.Look { Spot: LessonSpot.Cell cell }:
                    Box[] windows = { WindowAround(cell.At, cell.At, layout) };
                    _spotlight.LightUp(windows);
                    StandThePillClearOf(windows, layout, scale);
                    break;
                case Lesson.Look look:
                    bool isTheMedallion = ReferenceEquals(look.Spot, LessonSpot.MovesMedallion);
                    Box plaque = (isTheMedallion ? _hud.MovesMedallion : _hud.GoalsPlaque) ?? throw new InvalidOperationException("The plaques were not hung before the tutorial pointed at one.");
                    Box window = Box.Around(plaque.MiddleX, plaque.MiddleY, plaque.Width + 2f * LessonPlaqueMargin, plaque.Height + 2f * LessonPlaqueMargin);
                    _spotlight.LightUp(new[] { window }, isTheMedallion ? window.Width / 2f : Spotlight.Rounding);
                    _lessonPill.Stand(window.Bottom - LessonPillUnderThePlaques, ShelfPill.PillEdge.Head, scale, layout.ShelfWidth);
                    break;
                default:
                    throw new InvalidOperationException($"The stage does not know how to show the step {lesson}.");
            }
        }

        /// <summary>The disc a step rings with light: the booster used by hand until it is armed, the long press's disc; nothing for another step.</summary>
        private static BoosterKind? DiscRingedFor(Lesson lesson, bool isArmed) => lesson switch
        {
            Lesson.Booster asked when !isArmed => asked.Kind,
            Lesson.Look { Spot: LessonSpot.Disc disc } => disc.Booster,
            _ => null,
        };

        /// <summary>
        /// A booster used by hand: a round window over its disc, wide enough for the ring of light around it, and a
        /// window over its target (the cell, or the whole row or column; none for the shuffle).
        /// </summary>
        private void LightUpTheDiscAndItsTarget(Lesson.Booster lesson, ScreenLayout layout, float scale) =>
            LightUpTheDisc(lesson.Kind, TargetWindowsOf(lesson.Use, layout), layout, scale);

        /// <summary>
        /// A round window over a booster's disc, wide enough for the ring of light around it, and windows over what it
        /// strikes, if any. The pill stands over the disc's window, unless something lit lies where it would stand:
        /// then it hangs under the plaques.
        /// </summary>
        private void LightUpTheDisc(BoosterKind booster, IReadOnlyList<Box> targets, ScreenLayout layout, float scale)
        {
            Box place = layout.ShelfDiscs[1 + IndexOnTheShelf(booster)];
            float across = place.Width * LessonDiscWindowAcross;
            Box disc = Box.Around(place.MiddleX, place.MiddleY, across, across);
            _spotlight.LightUp(targets.Select(target => (target, Spotlight.Rounding)).Append((disc, across / 2f)).ToArray());

            float foot = Mathf.Max(layout.ShelfTop + LessonPillOverTheDisc, disc.Top + LessonPillOverTheDisc);
            _lessonPill.Stand(foot, ShelfPill.PillEdge.Foot, scale, layout.ShelfWidth);
            if (targets.Any(target => target.Bottom < _lessonPill.Top && target.Top > foot))
            {
                _lessonPill.Stand(layout.HudBottom - LessonPillUnderThePlaques, ShelfPill.PillEdge.Head, scale, layout.ShelfWidth);
            }
        }

        /// <summary>The window over what a booster strikes: its cell, its whole row or its whole column; none for the shuffle.</summary>
        private Box[] TargetWindowsOf(BoosterUse use, ScreenLayout layout) => use switch
        {
            BoosterUse.HitCell cell => new[] { WindowAround(cell.At, cell.At, layout) },
            BoosterUse.HitRow row => new[] { WindowAround(new Position(0, row.At.Y), new Position(_shape.Width - 1, row.At.Y), layout) },
            BoosterUse.HitColumn column => new[] { WindowAround(new Position(column.At.X, 0), new Position(column.At.X, _shape.Height - 1), layout) },
            _ => Array.Empty<Box>(),
        };

        private static int IndexOnTheShelf(BoosterKind booster)
        {
            for (int index = 0; index < ShelfBrief.Boosters.Count; index++)
            {
                if (ShelfBrief.Boosters[index] == booster)
                {
                    return index;
                }
            }

            throw new ArgumentException($"{booster.Name()} has no disc on the board's shelf.", nameof(booster));
        }

        /// <summary>A trade's windows: each of its two pieces, and each line of three or more it makes (<see cref="TradeLine"/>), so a four or a five is seen whole.</summary>
        private Box[] LightUpTheTrade(Move move, ScreenLayout layout)
        {
            Box[] windows = TradeLine.RunsOf(_game.Board, move)
                .Select(run => WindowAround(run.First, run.Last, layout))
                .Append(WindowAround(move.From, move.From, layout))
                .Append(WindowAround(move.To, move.To, layout))
                .ToArray();
            _spotlight.LightUp(windows);
            return windows;
        }

        /// <summary>The pill stands over the shelf, unless something lit lies under it there: then it hangs under the plaques.</summary>
        private void StandThePillClearOf(IReadOnlyList<Box> windows, ScreenLayout layout, float scale)
        {
            _lessonPill.StandOverTheShelf(layout, scale);
            if (windows.Any(window => window.Bottom < _lessonPill.Top))
            {
                _lessonPill.Stand(layout.HudBottom - LessonPillUnderThePlaques, ShelfPill.PillEdge.Head, scale, layout.ShelfWidth);
            }
        }

        /// <summary>The window around the cells from one to the other (one cell, or the two of a trade), a little wider than they are, in the layout's points.</summary>
        private static Box WindowAround(Position one, Position other, ScreenLayout layout)
        {
            float margin = LessonCellMargin * layout.CellSize;
            float left = layout.BoardLeft + Mathf.Min(one.X, other.X) * layout.CellSize - margin;
            float top = layout.BoardTop - Mathf.Min(one.Y, other.Y) * layout.CellSize + margin;
            float width = (Mathf.Abs(one.X - other.X) + 1) * layout.CellSize + 2f * margin;
            float height = (Mathf.Abs(one.Y - other.Y) + 1) * layout.CellSize + 2f * margin;
            return new Box(left, top - height, width, height);
        }

        /// <summary>A step ended: its light and its pill go at once, and the board may be asked for the next step at the next quiet moment.</summary>
        private void PutTheLessonAway()
        {
            HideTheLesson();
            _lessonLaid = null;
            _wasLessonSeen = false;
            _tutorialAskedOf = null;
        }

        private void HideTheLesson()
        {
            _spotlight.Show(0f);
            _guideMark.Hide();
            _discs.Guide(null);
            if (!(_lessonPill.Words is PillWords.Silent))
            {
                _lessonPill.Show(PillWords.None);
                _lessonPill.ShowThePainter(null);
                _lessonLaid = null;
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

            _sounded.Add(SoundCue.Refuse);
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

        /// <summary>An ad shown for the extra moves the engine gave: its card, the ad as it runs, and the payment it is.</summary>
        private sealed class AdOnTheStage
        {
            public AdOnTheStage(SimulatedAdCard card, IAdShowing ad, AttemptEvent.ExtraMovesTaken payment)
            {
                Card = card;
                Ad = ad;
                Payment = payment;
            }

            public SimulatedAdCard Card { get; }

            public IAdShowing Ad { get; }

            public AttemptEvent.ExtraMovesTaken Payment { get; }

            /// <summary>True once the card hangs in its place: the ad's seconds run from then.</summary>
            public bool IsDown { get; set; }

            /// <summary>True once the ad was over and its payment reported: the card is on its way up.</summary>
            public bool HasPaid { get; set; }
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
