#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Client.Words;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using Puzzle.Museum;
using UnityEngine;

namespace Puzzle.Client.Flow
{
    /// <summary>Where a session begins.</summary>
    public enum SessionOpening
    {
        /// <summary>In the museum's hall, in the lit room: what the Editor's walks through the game begin with.</summary>
        InTheMuseum,

        /// <summary>
        /// In the museum's hall, out of the dark the entry page left behind, as the game starts for a player: the room
        /// lights up as it does when the hall is come back to from a level, and the hall is otherwise as it opens lit.
        /// </summary>
        InTheMuseumOutOfTheDark,

        /// <summary>At the board of the level the player stands on: what the Editor's board studies begin with.</summary>
        AtTheBoard,
    }

    /// <summary>
    /// A player's way through the game: the stage they are looking at (the museum, the board of the attempt they
    /// are in, or a painting's jigsaw) and the progress that decides what comes next. The game opens in the
    /// museum's hall. A level has one door: its card, which the hall's play button brings down, where the player
    /// chooses the start boosters to take into it; its green button opens the level's board with them. A won
    /// card's green button leads to the hall, where the coins arrive and the next level waits behind the play
    /// button; a lost card's leads to the hall with the same level's card coming down, and the level then deals
    /// the next seed of its calibrated pool, so a retry is another board of the same measured difficulty (the web
    /// test server deals attempts the same way). A painting's jigsaw opens from a won level's card and from the
    /// museum's pages while pieces wait; its way back leads to where it was opened from, but for the jigsaw in
    /// which the fifth painting of a room became whole: from there the player is led to the hall, which shows them
    /// the restored room before it moves on. A level can be left for the museum at any time (the board asks first).
    /// Like the stages it owns, the session only moves when it is told the time (<see cref="Advance"/>): the
    /// running game tells it every frame, the Editor's storyboards whenever they want.
    /// </summary>
    /// <remarks>
    /// Progress is kept in the store the moment it changes. What the board's stage reports of the attempt goes
    /// through the economy's rules (<see cref="AttemptLedger"/>) at the time the wall clock tells: the first move
    /// makes the attempt count (so its board is not dealt again); the move that wins moves the player on and pays
    /// the win's coins in the same save, before the win is even shown, so closing the game during the celebration
    /// loses neither; a loss for good, or leaving a level after a move, costs a life. A jigsaw piece is kept when it
    /// has dropped into its place; pieces that fly to their places together are kept together, once the last of them
    /// has landed, so forty pieces are one save and not forty. A piece that is won and not placed needs no
    /// saving: the levels won say that it waits, so it can never be lost. The attempt in progress itself
    /// lives in memory only: a game that is closed in the middle of a level comes back in the museum, costs no life,
    /// and the level starts again with its next board. A store that cannot be written does not stop the game; the
    /// progress is kept here and saved again at the next change, or when <see cref="SaveProgress"/> is called.
    /// <para>
    /// An attempt that ends for good (won, lost for good, or left after a move, which the record calls abandoned) has
    /// its record written to the phone's attempt log at that moment, with what the player did in it, so that it can be
    /// played again exactly (Puzzle.Core's AttemptReplay); never while something could still bring the attempt back.
    /// An attempt left before its first move, or cut short by closing the game, is no attempt and has no record. A log
    /// that cannot be written does not stop the game either: the record waits and is written with the next one, or
    /// when <see cref="WriteWaitingRecords"/> is called.
    /// </para>
    /// <para>
    /// The museum is told the player's wallet at every step, as the rules have it at that instant: its row counts the
    /// lives back and the wait down as the clock runs, and a player without a life is shown the lives card instead of
    /// a level; with the wallet it is told the shop and the gift as the rules have them on the player's calendar day.
    /// Coins won on a board arrive in the museum's row when the player comes back to it: the row opens with the amount
    /// it showed last and counts up to the new one. What the museum asks of the wallet (a booster bought on a card, a
    /// set bought in the shop, a pack of coins, the coins of an ad that was watched, the day's gift) is done here by
    /// the economy's rules (<see cref="WalletLedger"/>) and saved at once; the museum is told what the rules said, and
    /// a refusal changes nothing. The row counts the coins down or up.
    /// </para>
    /// <para>
    /// What the store sells for money is bought through the session's purchase course (<see cref="Checkout"/>) over
    /// the platform's store: the store is asked to open when the session opens, and asked again when the player comes
    /// to the shop while it could not be reached. A product pressed in the shop is asked about on a card; its green
    /// button opens the store's own payment sheet; and a payment the store took is given by the rules
    /// (<see cref="WalletLedger.Deliver"/>), saved at once with the payment counted, and confirmed to the store only
    /// once the save holds it. The store's news is taken at every step, at a board too, so a payment approved while the
    /// player plays is given then and its card waits for the museum. The museum is told where the purchase stands with
    /// the shop. The Editor's tools have a scripted store that sells at the US prices.
    /// </para>
    /// <para>
    /// The board is told the wallet at every step too, with what the extra moves cost on the player's calendar day,
    /// which the card of a game out of moves shows. Extra moves the engine gave are paid here by the rules (the coins
    /// charged, or the day's ad counted) and saved as a move is kept; until the offer is answered nothing ends the
    /// attempt, so no life goes and no record is written. Its shelf counts the boosters of play, and one bought on the
    /// card its plus hangs is bought here by the same rules, saved at once, and armed on the board. A booster of play the
    /// engine accepts is spent and saved as a move is kept; the first of them counts as the attempt's first move. A
    /// session that opens on a save from before the boosters of play opened one at a time first gives the gifts that
    /// save is owed, once (AttemptLedger.WithTheOpeningGiftsDue), and a win gives the gift of a booster that opens on the
    /// level it leads to.
    /// </para>
    /// <para>
    /// The piece that completes a painting on its jigsaw gives the painting's reward (coins, and the booster of the
    /// rotation) in the same save as the piece, so it is given once: a painting opened again once whole gives nothing.
    /// The jigsaw shows that reward when its last piece is in place.
    /// </para>
    /// <para>
    /// The start boosters chosen on a level's card are taken into its attempt: their specials lie on the board as it
    /// opens and their moves are on the plaque, and they leave the shelf with the attempt's first move, so a card
    /// closed, or a level left before its first move, spends nothing. A board with no free gem for a special opens
    /// without it, and that booster is not spent. The attempt's record names them, so it replays to the same end.
    /// </para>
    /// <para>
    /// The museum's settings card shows the phone: the player's settings, kept in the device file the moment one is
    /// turned; the attempt records, which the player may delete there after a question; the id, the versions and the
    /// build's marks. What it asks is done here and the museum is told the phone after it. Deleting the records is
    /// asked only in the museum, where no attempt is open, so no record is on its way meanwhile; the progress and the
    /// wallet are in another file and are not touched.
    /// </para>
    /// <para>
    /// In a test build the player's settings may hold the tester mode (<see cref="TesterMode"/>), turned on from the
    /// support card: while it holds, the wallet is held unlimited (<see cref="WalletHandling.Unlimited"/>) wherever the
    /// rules are asked of it here, by both ledgers, for the extra moves' terms and for the shop, and the stages are told
    /// so with the wallet: no loss takes a life, no level waits for one, and no price leaves the coins. It holds from
    /// the step after it is turned on to the step it is turned off, which is only ever done in the museum, so an
    /// attempt is played wholly in it or wholly out of it, and its record says which.
    /// </para>
    /// <para>
    /// What the stage that is seen sounds (a turn's pops, a card coming down, a press, coins counted; Play/SoundScore.cs
    /// says which cue falls when) is played at every step through the session's cue player, as far as the player's
    /// settings are on at that step: sound off, nothing is heard; vibration off, nothing is felt. The game's cue player
    /// is the phone's speaker and vibrator; the Editor's tools give a silent one.
    /// </para>
    /// <para>
    /// The tutorial (docs/GAME_DESIGN.md §19) is shown by the stages, each step once to a player: a board is opened with
    /// the steps that may come on its level for what the player has seen (TutorialWatch.ForTheBoard), a jigsaw with what
    /// the player has seen of its two steps. A step that a stage reports done is the player's for good and is saved at
    /// once; a step cut short (a board left, the game closed) comes again. The moves a board showed by hand are counted
    /// in its attempt's record beside its hints. The booster intro (#7) is part of it: a board has the booster that
    /// opens on its level used by hand, free (its record names it among the guided boosters, and the ledger spends
    /// nothing for it), and the museum's card before a level shows the start boosters after the level's first loss
    /// (TutorialPlan.IntroducesTheStartBoosters).
    /// </para>
    /// <para>
    /// One stage is seen at a time, and each has its own camera: the stage that is left is taken down before the
    /// next one is built, in the dark both of them share, so the change is not seen. The one exception is a board
    /// whose card led to the jigsaw: it steps aside, out of sight, and comes back with its card when the player
    /// returns.
    /// </para>
    /// </remarks>
    public sealed class GameSession : IDisposable, ITouchable
    {
        /// <summary>The program that plays, as the attempt records name it.</summary>
        private const string ThisClient = "unity";

        private readonly IReadOnlyList<LevelDefinition> _levels;
        private readonly ArtCatalogue _art;
        private readonly PaintingGallery _paintings;
        private readonly IProgressStore _store;
        private readonly IAttemptLog _attempts;
        private readonly ISettingsStore _settings;
        private readonly DeviceAbilities _abilities;
        private readonly ICuePlayer _cues;

        /// <summary>The newest version of the levels the game carries, as the support card tells it.</summary>
        private readonly ContentVersion _content;
        private readonly AttemptLedger _ledger;
        private readonly WalletLedger _walletLedger;
        private readonly EconomyRules _rules;
        private readonly IWallClock _clock;

        /// <summary>The course of a purchase for money between the museum and the platform's store.</summary>
        private readonly Checkout _checkout;

        /// <summary>How long the session has run, by its own steps: the time the purchase's course waits by.</summary>
        private TimeSpan _running;

        /// <summary>What is thrown over the board, painted once for all the stages of this session: painting it anew for every level would hold up each level's first frame.</summary>
        private readonly EffectArt _effectArt;
        private ScreenShape _screen;
        private IStage _front;
        private PlayerProgress _progress;

        /// <summary>What the store was last given: the progress is saved again when it differs from this.</summary>
        private PlayerProgress _saved;

        /// <summary>What winning the open attempt gives: the painting its card's jigsaw button leads to. Nothing in the museum, and for a level that gives nothing.</summary>
        private PieceReward? _rewardOfTheBoard;

        /// <summary>How the open attempt began, for its record; nothing in the museum, and once the attempt's record is written.</summary>
        private AttemptStart? _startOfTheBoard;

        /// <summary>Where the way back from the open jigsaw leads; nothing while no jigsaw is open.</summary>
        private WayBack? _wayBack;

        /// <summary>The amount the museum's row last showed; nothing before the museum was first seen. A museum opened later counts up from it.</summary>
        private Coins? _coinsLastShown;

        /// <summary>How many pieces of the open jigsaw the progress already knows to be in place.</summary>
        private int _piecesKept;

        /// <summary>The room whose fifth painting became whole on the open jigsaw; nothing otherwise. The hall shows it restored when the jigsaw is left.</summary>
        private RoomNumber? _restoredJustNow;

        /// <summary>
        /// A session whose settings are kept in memory as a first start has them, with the switches this build honours,
        /// and that sounds nothing: the Editor's tools', which must not touch a phone's device file and play whole games
        /// in a moment. Otherwise as the session the game opens.
        /// </summary>
        /// <exception cref="ArgumentException">There is no level to play.</exception>
        /// <exception cref="IOException">The store's progress is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read the store.</exception>
        public GameSession(
            IReadOnlyList<LevelDefinition> levels,
            ArtCatalogue art,
            PaintingGallery paintings,
            EconomyRules rules,
            IWallClock clock,
            IProgressStore store,
            IAttemptLog attempts,
            ScreenShape screen,
            SessionOpening opening)
            : this(levels, art, paintings, rules, clock, store, attempts, new SettingsInMemory(DeviceSettings.Default), DeviceAbilities.OfThisBuild, CuePlayer.Silent, screen, opening)
        {
        }

        /// <summary>As the session the game opens, with the Editor's scripted store, which sells at the US prices (<see cref="ToolsStore"/>).</summary>
        /// <exception cref="ArgumentException">There is no level to play.</exception>
        /// <exception cref="IOException">The store's progress is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read the store.</exception>
        public GameSession(
            IReadOnlyList<LevelDefinition> levels,
            ArtCatalogue art,
            PaintingGallery paintings,
            EconomyRules rules,
            IWallClock clock,
            IProgressStore store,
            IAttemptLog attempts,
            ISettingsStore settings,
            DeviceAbilities abilities,
            ICuePlayer cues,
            ScreenShape screen,
            SessionOpening opening)
            : this(levels, art, paintings, rules, clock, store, attempts, settings, abilities, cues, ToolsStore(rules), screen, opening)
        {
        }

        /// <summary>Loads the player's progress and opens the game where the opening says, laid out for a screen.</summary>
        /// <param name="levels">The levels in the order they are played.</param>
        /// <param name="paintings">The museum's paintings, for the museum's pages and for the pieces a win gives.</param>
        /// <param name="rules">The economy the attempts are kept by: what a win pays, what a loss costs.</param>
        /// <param name="clock">The time the rules count lives and days by, and the attempt records tell: the device's, or a tool's fixed one.</param>
        /// <param name="attempts">Where the record of every attempt that ends for good goes: the phone's log, or a tool's in memory.</param>
        /// <param name="settings">Where the player's settings are kept: the phone's device file, or a tool's in memory.</param>
        /// <param name="abilities">What the build honours on the phone, whose switches the settings card shows: <see cref="DeviceAbilities.OfThisBuild"/> in the game.</param>
        /// <param name="cues">Where what the stages sound goes, as far as the settings let it through: the phone's speaker and vibrator in the game, <see cref="CuePlayer.Silent"/> in the tools.</param>
        /// <param name="sales">The platform's store (UnityStore in the game), or a scripted one; a build that sells nothing for money never asks it.</param>
        /// <exception cref="ArgumentException">There is no level to play.</exception>
        /// <exception cref="IOException">The store's progress is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read the store.</exception>
        public GameSession(
            IReadOnlyList<LevelDefinition> levels,
            ArtCatalogue art,
            PaintingGallery paintings,
            EconomyRules rules,
            IWallClock clock,
            IProgressStore store,
            IAttemptLog attempts,
            ISettingsStore settings,
            DeviceAbilities abilities,
            ICuePlayer cues,
            IStore sales,
            ScreenShape screen,
            SessionOpening opening)
        {
            if (levels.Count == 0)
            {
                throw new ArgumentException("A session needs a level to play.", nameof(levels));
            }

            _levels = levels;
            _art = art;
            _paintings = paintings;
            _ledger = new AttemptLedger(rules, levels.Count);
            _walletLedger = new WalletLedger(rules);
            _rules = rules;
            _clock = clock;
            _store = store;
            _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            _cues = cues ?? throw new ArgumentNullException(nameof(cues));
            _content = levels.Select(level => level.ContentVersion).Max();
            _screen = screen;
            _saved = store.Load();

            // A save from before the boosters used during play opened one at a time is owed their gifts once.
            _progress = _ledger.WithTheOpeningGiftsDue(_saved);
            if (HasUnsavedProgress)
            {
                SaveProgress();
            }

            _checkout = new Checkout(rules.Shop.Store, rules.Shop.RealMoney, Application.identifier, sales ?? throw new ArgumentNullException(nameof(sales)));
            _checkout.Open(_running);
            _effectArt = EffectArt.Paint();
            _front = opening switch
            {
                SessionOpening.InTheMuseum => OpenTheMuseum(StageOpening.InTheLitRoom, MuseumDoor.Home),
                SessionOpening.InTheMuseumOutOfTheDark => OpenTheMuseum(StageOpening.OutOfTheDark, MuseumDoor.Home),
                SessionOpening.AtTheBoard => OpenTheBoard(StageOpening.InTheLitRoom, StartChoice.None),
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

        /// <summary>The course of a purchase for money, as the Editor's checks look at it.</summary>
        public Checkout Checkout => _checkout;

        /// <summary>True while the store does not hold the progress, because the last save failed.</summary>
        public bool HasUnsavedProgress => !_progress.Equals(_saved);

        /// <summary>
        /// Lays the stage out for a screen, and every later stage too; call again whenever the screen changes. A finger
        /// that is down when the screen changes (the device turned, a fold opened) is let go first, as when the device
        /// takes the touch away: its gesture began on the screen before, whose pixels no longer mean the same places,
        /// so a drag goes on to no other cell and a carried piece goes back to its tray.
        /// </summary>
        public void Fit(ScreenShape screen)
        {
            if (screen != _screen)
            {
                _front.PointerCancelled();
            }

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
            _running += TimeSpan.FromSeconds(deltaSeconds);
            TellTheStageTheWallet();
            _front.Advance(deltaSeconds);
            KeepWhatHappened();
            DoWhatTheStageAskedFor();
            DoWhatWasAskedOfThePhone();
            DoWhatWasAskedOfTheStore();
            KeepWhatWasPlaced();
            KeepTheTutorialStepsDone();
            PlayWhatSounded();
            FollowThePlayer();
        }

        /// <summary>Plays a move on the board's stage, as <see cref="GameStage.TryPlay"/> does, and keeps what it means for the progress.</summary>
        /// <exception cref="InvalidOperationException">The player is in the museum: no board is open.</exception>
        public bool TryPlay(Move move)
        {
            bool isPlayed = Stage.TryPlay(move);
            KeepWhatHappened();
            return isPlayed;
        }

        /// <summary>A finger came down on the screen. Pixels are counted from the screen's lower left corner.</summary>
        public void PointerDown(Vector2 pixel) => _front.PointerDown(pixel);

        /// <summary>The finger moved while it was down; on a board, a drag towards a neighbouring cell plays a move at once when the board is at rest.</summary>
        public void PointerMoved(Vector2 pixel)
        {
            _front.PointerMoved(pixel);
            KeepWhatHappened();
        }

        /// <summary>The finger lifted; on a board, a tap on a neighbour of the picked piece, or on a special piece, plays a move.</summary>
        public void PointerUp()
        {
            _front.PointerUp();
            KeepWhatHappened();
            DoWhatTheStageAskedFor();
            DoWhatWasAskedOfThePhone();
            DoWhatWasAskedOfTheStore();
        }

        /// <summary>
        /// One more finger came down while the finger the game follows is still on the screen. The game follows one
        /// finger and listens to no other, with one exception: a touch anywhere ends a won game's finale, and that
        /// holds while the finger of the winning move still rests on the glass (<see cref="GameStage.AnotherFingerDown"/>).
        /// </summary>
        public void AnotherFingerDown() => Board?.AnotherFingerDown();

        /// <summary>The touch was taken away by the device, or the game lost the screen while a finger was down.</summary>
        public void PointerCancelled() => _front.PointerCancelled();

        /// <summary>The player asked for the way back, by the device's back button or gesture: the stage they look at takes it.</summary>
        public void GoBack() => _front.GoBack();

        /// <summary>
        /// True once since this was asked last when the player answered the hall's question of the way back with "Çık"
        /// (<see cref="MuseumStage.TakeTheWishToQuit"/>): the game is to close. The running game closes it; the
        /// Editor's tools only read it.
        /// </summary>
        public bool TakeTheWishToQuit() => Museum?.TakeTheWishToQuit() == true;

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

        /// <summary>
        /// Writes the attempt records that could not be written when their attempts ended. The game calls this when it
        /// is put away, so a record that waits gets another chance before the game may be closed.
        /// </summary>
        public void WriteWaitingRecords()
        {
            try
            {
                _attempts.WriteWaiting();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                // Playing goes on: the records still wait in the log, and the next record or the next pause writes them.
                Debug.LogWarning($"Attempt records that wait could not be written now either and wait on: {failure.Message}");
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

        /// <summary>How the wallet is held to the rules now: unlimited while the tester mode holds, as the rules have it otherwise.</summary>
        private WalletHandling HandlingNow => TesterMode.HandlingFor(_settings.Settings, _rules.Ads, _rules.Shop.RealMoney);

        /// <summary>The player's wallet as the rules have it now: lives that came back since the last loss are there.</summary>
        private WalletView WalletNow => WalletView.Of(_progress.Wallet, _rules.Lives, _clock.Now, HandlingNow);

        /// <summary>What the extra moves cost the player now: coins, or one of the free ads left on the player's calendar day.</summary>
        private ExtraMovesTerms ExtraMovesNow =>
            _rules.ExtraMoves.TermsFor(HandlingNow.ToPay(_rules.ExtraMoves.Price, _progress.Wallet), DayStamp.Of(_clock.Now));

        /// <summary>The shop and the gift as the rules have them for the player now, on their calendar day, with the store as the purchase's course has it.</summary>
        private ShopView ShopNow => ShopView.Of(_rules, _progress.Wallet, _clock.Now, HandlingNow, _checkout.ViewFor(_progress.Purchases, LevelNumberNow));

        /// <summary>The level the player stands on, counted from 1: the starter pack is offered from its level on.</summary>
        private int LevelNumberNow => _progress.LevelIndexIn(_levels.Count) + 1;

        /// <summary>
        /// The Editor's tools' store, never the game's: it opens at once with the US prices (worded as the US store
        /// words them, "$1.99"), takes every payment, and offers no restore.
        /// </summary>
        private static IStore ToolsStore(EconomyRules rules) => ScriptedStore.AtUsPrices(rules.Shop.Store, Application.identifier, offersRestore: false);

        /// <summary>The phone as the settings card shows it now: the settings kept, the records there are, this build's versions and marks.</summary>
        private DeviceView DeviceNow => new DeviceView(
            _settings.Settings, _abilities.Settings, _attempts.Device, Application.version, _content,
            RecordsKeptNow(), _attempts.FilePath, _rules.Ads, _rules.Shop.RealMoney, Speech.Language, WordsPack.Offered);

        /// <summary>Tells the museum or the board, whichever is the stage that is seen, of the wallet as it is at this step.</summary>
        private void TellTheStageTheWallet()
        {
            if (Museum is MuseumStage museum)
            {
                WalletView wallet = WalletNow;
                museum.Tell(wallet, ShopNow);
                _coinsLastShown = wallet.Coins;
            }
            else if (Board is GameStage board)
            {
                board.Tell(WalletNow, ExtraMovesNow);
            }
        }

        /// <summary>
        /// If the player has left the stage that is open, opens what they left it for, out of the dark the stage
        /// left behind: from the museum the board of their level with the start boosters chosen on its card, or a
        /// painting's jigsaw; from a result's card the museum: the hall, with the level's card coming down where the
        /// card's green button tries the level again.
        /// </summary>
        private void FollowThePlayer()
        {
            switch (_front)
            {
                case MuseumStage { PlayerLeftFor: MuseumExit.ToPlay toPlay }:
                    ChangeTheStage(() => OpenTheBoard(StageOpening.OutOfTheDark, toPlay.StartBoosters));

                    // The hall's paintings are not seen on a board; the result's card loads the one it shows by itself.
                    _paintings.LetGoOfUnusedPictures();
                    break;
                case MuseumStage { PlayerLeftFor: MuseumExit.ToAJigsaw jigsaw } museum:
                    _wayBack = new WayBack.ToTheMuseum(museum.Door);
                    ChangeTheStage(() => OpenTheJigsaw(jigsaw.Painting));
                    break;
                case GameStage { PlayerChose: ResultChoice.GoOn } board:
                    MuseumDoor goesOnTo = board.GoingOnLeadsTo();
                    ChangeTheStage(() => OpenTheMuseum(StageOpening.OutOfTheDark, goesOnTo));
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
                _paintings.LetGoOfUnusedPictures();
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

            _paintings.LetGoOfUnusedPictures();
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
            LevelDefinition level = LevelToPlay;
            var visit = new MuseumVisit(_progress.Museum, _paintings.Catalogue, level.Id.Value, hasWonEveryLevel: _progress.LevelsWon >= _levels.Count);
            LevelBrief brief = LevelBrief.Of(level, PieceReward.ForWinning(_progress, _levels.Count, _paintings.Catalogue), _rules, LevelStart.Rules);
            WalletView wallet = WalletNow;
            MuseumStage museum = MuseumStage.Open(
                visit, _paintings, _art, door, _coinsLastShown is Coins shown ? wallet.WithCoins(shown) : wallet, ShopNow, DeviceNow, brief, opening,
                TutorialPlan.IntroducesTheStartBoosters(_progress.Tutorial, _progress.AttemptsOnLevel), _effectArt);
            museum.Fit(_screen); // at once: the first frame is drawn before the next step of the clock
            _rewardOfTheBoard = null;
            _startOfTheBoard = null;
            return museum;
        }

        /// <summary>
        /// Opens the jigsaw of a painting as the progress has it, out of the dark the stage before left behind, with
        /// what completing the painting will give for its finished face to show; nothing for a painting whole already.
        /// </summary>
        private IStage OpenTheJigsaw(PaintingNumber painting)
        {
            JigsawStage jigsaw = JigsawStage.Open(
                painting, _progress.Museum, _paintings, _art, StageOpening.OutOfTheDark, _walletLedger.RewardForCompleting(_progress, painting), _progress.Tutorial);
            jigsaw.Fit(_screen); // at once: the first frame is drawn before the next step of the clock
            _piecesKept = jigsaw.Table.InPlace.Count;
            return jigsaw;
        }

        /// <summary>
        /// Opens the stage of the attempt the progress asks for: the level the player stands on, with the seed its
        /// pool has for this try and the start boosters chosen on its card, whose specials lie on the board as it
        /// opens, the game's hints by the economy's rules, and the steps of the tutorial that may come on the level. The
        /// attempt begins now, as its record will say; what it took leaves the shelf with its first move.
        /// </summary>
        /// <param name="startBoosters">The start boosters chosen on the level's card; none for a board opened without the card (the Editor's studies).</param>
        private IStage OpenTheBoard(StageOpening opening, StartChoice startBoosters)
        {
            LevelDefinition level = LevelToPlay;
            int attempt = _progress.AttemptsOnLevel;
            _rewardOfTheBoard = PieceReward.ForWinning(_progress, _levels.Count, _paintings.Catalogue);
            Game game = LevelStart.Attempt(level, attempt, startBoosters.ForTheEngine());
            ShelfBrief shelf = ShelfBrief.Of(level.Id.Value, _rules);
            AttemptSeeds seeds = LevelStart.SeedsOf(level, attempt);
            _startOfTheBoard = new AttemptStart(
                level.Id,
                level.ContentVersion,
                level.MoveLimit,
                seeds,
                attempt,
                _attempts.Device,
                ThisClient,
                _clock.Now,
                game.ShuffleBoosterRule);
            var brief = new AttemptBrief(attempt + 1, _progress.IsOnLastLevelOf(_levels.Count), _rules.Win, _rewardOfTheBoard, TakenBy(game, startBoosters), _rules.Ads);
            GameStage stage = GameStage.Open(
                game, _art, _effectArt, _paintings, brief, shelf, opening, HintBrief.Of(_rules.Hints, seeds.Play), TutorialWatch.ForTheBoard(level.Id.Value, _progress.Tutorial, seeds.Play, _rules.Unlocks));
            stage.Fit(_screen); // at once: the first frame of a level is drawn before the next step of the clock
            stage.Tell(WalletNow, ExtraMovesNow);
            return stage;
        }

        /// <summary>
        /// The start boosters the game took of the ones chosen: all of them, but where the board had no free gem for
        /// a special, whose booster is put back and not spent (LevelStart.Attempt).
        /// </summary>
        private StartChoice TakenBy(Game game, StartChoice chosen)
        {
            StartChoice taken = chosen;
            foreach (BoosterKind booster in chosen.Boosters)
            {
                bool isTaken = game.BoostersUsed.Contains(booster.Name());
                if (!isTaken && _rules.StartBoosters.Toggled(taken, booster, _progress.Wallet.Shelf) is StartToggle.Changed putBack)
                {
                    Debug.LogWarning($"Level {game.Level.Id} had no free gem for the special of {booster.Name()}: the board opened without it, and it is not spent.");
                    taken = putBack.Choice;
                }
            }

            return taken;
        }

        /// <summary>
        /// What the presses of the museum or of the board asked of the wallet since it was last asked, done by the
        /// economy's rules at the player's time and saved at once, as one save however much it was: a booster or a set
        /// bought, a pack of coins, the coins of an ad, the day's gift. A request the rules refuse changes nothing. The
        /// stage is told the wallet after it at once (the museum's row counts the coins, and a booster bought on the
        /// board is armed), and the museum is told what the rules said to each request, for its cards to say.
        /// </summary>
        private void DoWhatTheStageAskedFor()
        {
            IReadOnlyList<WalletRequest> asked = _front switch
            {
                MuseumStage museum => museum.TakeWhatWasAskedFor(),
                GameStage board => board.TakeWhatWasAskedFor(),
                _ => Array.Empty<WalletRequest>(),
            };
            if (asked.Count == 0)
            {
                return;
            }

            DateTimeOffset now = _clock.Now;
            WalletHandling handling = HandlingNow;
            var answered = new List<(WalletRequest Request, WalletAnswer Answer)>();
            foreach (WalletRequest request in asked)
            {
                WalletAnswer answer = _walletLedger.Answer(request, _progress.Wallet, now, handling);
                switch (answer)
                {
                    case WalletAnswer.Received received:
                        _progress = _progress.WithWallet(received.Wallet);
                        break;
                    case WalletAnswer.Refused refused:
                        Debug.Log($"The rules refused \"{request}\" for a wallet of {_progress.Wallet.Coins} coins: {refused.Why}; nothing changed.");
                        break;
                    default:
                        throw new InvalidOperationException($"The session does not know the answer \"{answer}\".");
                }

                answered.Add((request, answer));
            }

            SaveProgress();
            TellTheStageTheWallet();
            if (Museum is MuseumStage asking)
            {
                foreach ((WalletRequest request, WalletAnswer answer) in answered)
                {
                    asking.Hear(request, answer);
                }
            }
        }

        /// <summary>
        /// What the museum asked of the store since it was last asked (a product pressed, the green button of the card
        /// that asks, a card closed, the restore row), handed to the purchase's course in order; then the store's news
        /// is taken, a payment given and saved and confirmed once saved, at a board too. An unreachable store is asked
        /// again while the player is in the shop.
        /// </summary>
        private void DoWhatWasAskedOfTheStore()
        {
            (PurchaseState State, RestoreState Restore, StoreStatus Status, Coins Coins) before = (_checkout.State, _checkout.Restore, _checkout.Status, _progress.Wallet.Coins);
            if (Museum is MuseumStage museum)
            {
                int levelNumber = LevelNumberNow;
                foreach (StoreRequest request in museum.TakeWhatWasAskedOfTheStore())
                {
                    bool isTaken = request switch
                    {
                        StoreRequest.Ask ask => _checkout.Ask(ask.Product, _progress.Purchases, levelNumber),
                        StoreRequest.Pay _ => _checkout.Pay(),
                        StoreRequest.Decline _ => _checkout.Decline(),
                        StoreRequest.Dismiss _ => _checkout.Dismiss(),
                        StoreRequest.Restore _ => _checkout.RestorePurchases(),
                        StoreRequest.DismissTheRestore _ => _checkout.DismissTheRestore(),
                        _ => throw new InvalidOperationException($"The session does not know the request \"{request}\" of the store."),
                    };
                    if (!isTaken)
                    {
                        Debug.Log($"The purchase's course let \"{request}\" go: it stands at {_checkout.State}.");
                    }
                }

                if (museum.Showing == Destination.Shop)
                {
                    _checkout.OpenAgainIfUnreachable(_running);
                }
            }

            _checkout.Step(new KeeperOfThePurchases(this));

            // The stage is told at once, as after a request of the wallet's: its card comes down in this step, and its row counts the coins.
            bool hasMoved = !ReferenceEquals(before.State, _checkout.State) || !ReferenceEquals(before.Restore, _checkout.Restore)
                || !ReferenceEquals(before.Status, _checkout.Status) || before.Coins != _progress.Wallet.Coins;
            if (hasMoved)
            {
                TellTheStageTheWallet();
            }
        }

        /// <summary>
        /// What the museum's settings card asked of the phone since it was last asked: a setting turned, or a language
        /// chosen, is kept in the device file at once, the attempt records are deleted. A file that cannot be written
        /// does not stop the game: the setting or the language holds for this run, and the records that could not be
        /// deleted are still counted. The museum is told the phone after it; after a language, it speaks anew, every
        /// page and card at once. A board is never open meanwhile (the settings card hangs in the museum only), and
        /// the next board is built in the language spoken by then.
        /// </summary>
        private void DoWhatWasAskedOfThePhone()
        {
            if (!(Museum is MuseumStage museum))
            {
                return;
            }

            IReadOnlyList<DeviceRequest> asked = museum.TakeWhatWasAskedOfThePhone();
            if (asked.Count == 0)
            {
                return;
            }

            Language spokenBefore = Speech.Language;
            foreach (DeviceRequest request in asked)
            {
                try
                {
                    Do(request);
                }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                {
                    // Playing goes on: the card shows the phone as it is, which is what came of the request.
                    Debug.LogWarning($"\"{request}\" could not be done on the phone: {failure.Message}");
                }
            }

            if (Speech.Language != spokenBefore)
            {
                museum.SpeakAnew(DeviceNow);
            }
            else
            {
                museum.Tell(DeviceNow);
            }
        }

        /// <exception cref="IOException">The device file or the attempt log could not be written.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not write them.</exception>
        private void Do(DeviceRequest request)
        {
            switch (request)
            {
                case DeviceRequest.TurnASetting turn:
                    _settings.Keep(_settings.Settings.With(turn.Setting, turn.IsOn));
                    break;
                case DeviceRequest.DeleteTheRecords _:
                    _attempts.DeleteAll();
                    Debug.Log("The attempt records on this phone were deleted at the player's wish.");
                    break;
                case DeviceRequest.ChooseALanguage choose:
                    // Spoken first: a device file that cannot be written leaves the language chosen for this run.
                    WordsPack.Speak(choose.Language);
                    _settings.Keep(_settings.Settings.WithLanguage(choose.Language));
                    break;
                default:
                    throw new InvalidOperationException($"The session does not know the request \"{request}\".");
            }
        }

        /// <summary>
        /// What the stage that is seen sounded since the last step, heard and felt as the player's settings are at this
        /// moment: a switch turned on the settings card counts from the next cue on, its own click included.
        /// </summary>
        private void PlayWhatSounded()
        {
            DeviceSettings settings = _settings.Settings;
            foreach (SoundCue cue in _front.TakeWhatSounded())
            {
                (SoundCue? sound, Tick? tick) = SoundScore.Reaching(cue, settings);
                if (sound is SoundCue heard)
                {
                    _cues.Sound(heard);
                }

                if (tick is Tick felt)
                {
                    _cues.Buzz(felt);
                }
            }
        }

        /// <summary>How many attempt records the phone keeps; nothing when the log cannot be read to count them.</summary>
        private int? RecordsKeptNow()
        {
            try
            {
                return _attempts.CountRecords();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                Debug.LogWarning($"The attempt records could not be counted: {failure.Message}");
                return null;
            }
        }

        /// <summary>
        /// What the board's stage reported of the attempt since the progress last looked, kept by the economy's rules
        /// in the order it happened and saved at once, as one save however much it was. If the attempt ended for good
        /// in it, its record follows the save.
        /// </summary>
        private void KeepWhatHappened()
        {
            if (!(Board is GameStage board))
            {
                return;
            }

            IReadOnlyList<AttemptEvent> happened = board.TakeWhatHappened();
            if (happened.Count == 0)
            {
                return;
            }

            DateTimeOffset now = _clock.Now;
            WalletHandling handling = HandlingNow;
            bool hasEnded = false;
            foreach (AttemptEvent each in happened)
            {
                _progress = _ledger.Apply(_progress, each, now, handling);
                hasEnded |= each.EndsTheAttempt;
            }

            SaveProgress();
            if (hasEnded)
            {
                RecordTheAttempt(board, now, handling);
            }
        }

        /// <summary>
        /// Writes the record of the open attempt, which has just ended for good, to the phone's log: once, with the
        /// game as it stood when it ended, the hints its board showed, the moves its tutorial showed by hand and the boosters it had used by hand, free, marked where it was played in the tester
        /// mode. A clock that was set back since the attempt began (a time sync) gives an attempt that ends as it began
        /// rather than one that ends before. A log that cannot be written does not stop the game: the record waits there
        /// and is written with the next one.
        /// </summary>
        /// <param name="handling">How the wallet was held while the attempt was played; the tester mode cannot change on a board.</param>
        /// <exception cref="InvalidOperationException">The attempt's record was written before, or the attempt never began.</exception>
        private void RecordTheAttempt(GameStage board, DateTimeOffset now, WalletHandling handling)
        {
            Game game = board.Game;
            AttemptStart start = _startOfTheBoard ?? throw new InvalidOperationException("An attempt ended a second time, or without having begun.");
            _startOfTheBoard = null;
            DateTimeOffset ended = now < start.StartedUtc ? start.StartedUtc : now;
            var record = new AttemptRecord(
                start, AttemptOutcome.Of(game, ended), lab: null, game.Actions, testerMode: handling == WalletHandling.Unlimited, hintsShown: board.HintsShown,
                guidedMoves: board.GuidedMoves, guidedBoosters: board.GuidedBoosters);
            try
            {
                _attempts.Append(record);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                // Playing goes on: the record waits in the log, and the next record or the next pause writes it.
                Debug.LogWarning($"The record of the attempt at level {start.Level} could not be written and waits: {failure.Message}");
            }
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

        /// <summary>
        /// The steps of the tutorial that the stage that is seen has shown to their end since the progress last looked:
        /// each is the player's for good, saved at once, so that it is never shown again.
        /// </summary>
        private void KeepTheTutorialStepsDone()
        {
            IReadOnlyList<TutorialStep> done = _front switch
            {
                GameStage board => board.TakeTheTutorialStepsDone(),
                JigsawStage jigsaw => jigsaw.TakeTheTutorialStepsDone(),
                MuseumStage museum => museum.TakeTheTutorialStepsDone(),
                _ => Array.Empty<TutorialStep>(),
            };
            if (done.Count == 0)
            {
                return;
            }

            foreach (TutorialStep step in done)
            {
                _progress = _progress.WithTutorialStepSeen(step);
            }

            SaveProgress();
        }

        /// <summary>
        /// The pieces that lie in place on the open jigsaw become the progress' own, with the painting's reward where
        /// they complete it; a room whose last painting they complete is remembered for the hall.
        /// </summary>
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
            _progress = _walletLedger.WithPiecesInPlace(_progress, painting, jigsaw.Table.InPlace);

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

        /// <summary>The session as the purchase's course needs it: the rules give the payment, the session keeps and saves the progress after it.</summary>
        private sealed class KeeperOfThePurchases : IPurchaseKeeper
        {
            private readonly GameSession _session;

            public KeeperOfThePurchases(GameSession session)
            {
                _session = session;
            }

            public bool IsSaved => !_session.HasUnsavedProgress;

            public Delivery Deliver(StoreProduct product, StorePayment payment)
            {
                Delivery delivery = _session._walletLedger.Deliver(_session._progress, product, payment.TransactionId);
                switch (delivery)
                {
                    case Delivery.Given given:
                        _session._progress = given.Progress;
                        Debug.Log($"The store's {payment} gave {given.What}.");
                        break;
                    case Delivery.SoldOut soldOut:
                        _session._progress = soldOut.Progress;
                        Debug.Log($"The store's {payment} is for a product sold out to this player; nothing was given.");
                        break;
                    case Delivery.AlreadyGiven _:
                        break;
                    default:
                        throw new InvalidOperationException($"The session does not know the delivery \"{delivery}\".");
                }

                _session.SaveProgress();
                return delivery;
            }
        }
    }
}
