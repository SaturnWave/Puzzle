#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Meta;
using Puzzle.Museum.Catalogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line, with no game running, what the game makes of a screen that changes under it: a
    /// tablet turned, an open foldable turned, a foldable opened. The game's own session is driven as a phone drives it,
    /// with its touches and a 60 Hz clock, and the screen is changed at the moments where that is most likely to go
    /// wrong: while the pieces fall after a move, in the middle of a drag, with each card of the board and of the
    /// museum hanging, during a won game's finale, while a piece of a jigsaw is carried, and on the entry page while its
    /// bar fills, while a finger rests on it, and with its card hanging. After every change it requires that the board
    /// the stage comes to rest on is the engine's, that what the player is to press next lies on the usable screen,
    /// that a finger which was down when the screen changed plays nothing, presses nothing and places nothing, that a
    /// carried piece is back in its tray, and that nothing was logged as an error. Pictures of the moments after the
    /// changes land in out/unity/shots/rotation, one sheet a screen.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.RotationCheck.Run
    /// </summary>
    public static class RotationCheck
    {
        /// <summary>A level with moves enough for every part of the check and room for long cascades (TouchWhileMovingCheck's).</summary>
        private const int LevelNumber = 40;

        /// <summary>The out-of-moves card comes after so many moves of the level cut short; the finale comes after a level whose goals are cut to this share.</summary>
        private const int MovesOfTheShortLevel = 3;
        private const float GoalsOfTheEasyLevel = 0.1f;

        private const ulong BotSeed = 20261024;
        private const ulong BotStream = 17;

        /// <summary>The screen changes so long after a move is played: its pieces are falling then.</summary>
        private const float MidCascade = 0.3f;

        /// <summary>...and so far into a finale, and so far into a page's turn.</summary>
        private const float MidFinale = 0.6f;

        /// <summary>A screen-change check that waits for a press to lead somewhere gives up after this long.</summary>
        private const float LongestWait = 6f;

        /// <summary>The player of the museum's parts: 46 levels won and 42 pieces placed, so four pieces wait (ScreenShots' state).</summary>
        private const string MuseumPlayer = "46+42";

        private const string Folder = "rotation";

        /// <summary>
        /// The changes: the 12.9" iPad turned from upright to sideways, both with the status bar and the home
        /// indicator; the open foldable turned, its camera then at a side; and the foldable opened from its cover display.
        /// </summary>
        private static readonly Turn[] Turns =
        {
            new Turn("ipad", new ScreenShape(2048, 2732, 48, 40), new ScreenShape(2732, 2048, 48, 40)),
            new Turn("fold", new ScreenShape(2076, 2152), new ScreenShape(2152, 2076, 0, 0, 90, 0)),
            new Turn("unfold", new ScreenShape(1080, 2424), new ScreenShape(2076, 2152)),
        };

        private static readonly List<string> Errors = new List<string>();

        public static void Run()
        {
            Application.logMessageReceived += KeepAnError;
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                var bench = new Bench(art, LevelPack.LoadPacked(), PaintingGallery.OfThePackedMuseum());
                int changes = 0;
                foreach (Turn turn in Turns)
                {
                    changes += TurningWhilePiecesFall(bench, turn);
                    changes += TurningInTheMiddleOfADrag(bench, turn);
                    changes += TurningWithTheBoardsCards(bench, turn);
                    changes += TurningOutOfMoves(bench, turn);
                    changes += TurningDuringTheFinale(bench, turn);
                    changes += TurningWithTheMuseumsCards(bench, turn);
                    changes += TurningWhileAPieceIsCarried(bench, turn);
                    changes += TurningTheEntryPage(bench, turn);
                }

                bench.WriteSheets();
                if (Errors.Count > 0)
                {
                    throw new InvalidOperationException($"{Errors.Count} errors were logged while the screen changed, the first: {Errors[0]}");
                }

                Debug.Log($"Rotation check passed: {changes} changes of the screen on {string.Join(", ", Turns.Select(turn => turn.ToString()))}: "
                    + "while pieces fell the stage came to rest on the engine's board; a finger that was down in the middle of a drag played nothing; "
                    + "the leaving card, the booster offer, the out-of-moves card, the lost and the won cards, the level's card, the lives, settings, "
                    + "privacy, support and language cards and the hall's question of the way back each hung with their buttons on the usable screen and answered a press; "
                    + "a finale turned under ended on the engine's board with its card; a carried piece went back to its tray and was not placed by the finger that carried it; "
                    + "the entry page refilled, dropped a resting finger and kept its card; and nothing was logged as an error.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is ArgumentException
                || failure is IOException || failure is LevelFormatException || failure is CatalogueFormatException)
            {
                Debug.LogError($"Rotation check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
            finally
            {
                Application.logMessageReceived -= KeepAnError;
            }
        }

        private static void KeepAnError(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                Errors.Add(message);
            }
        }

        /// <summary>The screen changes while the pieces of a move fall, and back while those of the next fall.</summary>
        private static int TurningWhilePiecesFall(Bench bench, Turn turn)
        {
            using GameSession session = bench.AtTheBoard(turn.From, bench.Level);
            var player = new Player(session, turn.From);
            player.Settle("the board coming in");

            player.PlayTheBotsMove();
            player.Advance(MidCascade);
            session.Fit(turn.To);
            bench.Shoot(session, turn, turn.To, "falling");
            player.ComeToRest("a move whose pieces fell as the screen changed");
            player.RequireOnTheScreen(session.Stage.PauseButtonPixel, "the shelf's pause disc", turn.To);

            player.PlayTheBotsMove();
            player.Advance(MidCascade);
            session.Fit(turn.From);
            player.ComeToRest("a move whose pieces fell as the screen changed back");
            player.RequireOnTheScreen(session.Stage.PauseButtonPixel, "the shelf's pause disc", turn.From);
            return 2;
        }

        /// <summary>
        /// A finger comes down on a piece and has begun to drag it when the screen changes; it goes on to where the
        /// piece's neighbour is on the new screen and lifts. That plays nothing: its drag began on another screen. A
        /// swipe made afresh plays at once.
        /// </summary>
        private static int TurningInTheMiddleOfADrag(Bench bench, Turn turn)
        {
            using GameSession session = bench.AtTheBoard(turn.From, bench.Level);
            var player = new Player(session, turn.From);
            player.Settle("the board coming in");

            Move trade = player.ALegalTrade();
            Vector2 from = session.Stage.PixelOf(trade.From);
            session.PointerDown(from);
            session.PointerMoved(Vector2.Lerp(from, session.Stage.PixelOf(trade.To), 0.15f));
            session.Fit(turn.To);
            session.PointerMoved(session.Stage.PixelOf(trade.To));
            session.PointerUp();
            player.Advance(0.5f);
            player.Expect(movesPlayed: 0, "after a drag that began before the screen changed and ended on the neighbour's place on the new screen");

            player.Swipe(player.ALegalTrade());
            player.Expect(movesPlayed: 1, "after a swipe made afresh on the new screen");
            player.ComeToRest("a swipe made after the screen changed");
            return 1;
        }

        /// <summary>The leaving card and the booster offer hang as the screen changes, and answer a press after.</summary>
        private static int TurningWithTheBoardsCards(Bench bench, Turn turn)
        {
            using GameSession session = bench.AtTheBoard(turn.From, bench.Level, holdsNoBooster: true);
            var player = new Player(session, turn.From);
            player.Settle("the board coming in");

            player.Press(session.Stage.PauseButtonPixel, "the shelf's pause disc");
            player.Settle("the leaving card coming down");
            player.Require(session.Stage.IsAskingWhetherToLeave, "the pause disc brought no leaving card");
            session.Fit(turn.To);
            player.Settle("the leaving card on the changed screen");
            bench.Shoot(session, turn, turn.To, "leaving");
            player.Require(session.Stage.IsAskingWhetherToLeave, "the leaving card was gone after the screen changed");
            player.RequireOnTheScreen(session.Stage.ResultButtonPixel, "the leaving card's Oyna", turn.To);
            player.RequireOnTheScreen(session.Stage.MuseumButtonPixel, "the leaving card's Müze", turn.To);
            player.Press(session.Stage.ResultButtonPixel, "the leaving card's Oyna");
            player.Settle("the leaving card going up");
            player.Require(!session.Stage.IsAskingWhetherToLeave && session.Stage.PauseButtonPixel != null, "the leaving card's Oyna did not give the board back");

            // A disc of a booster the player holds none of, and that is open on this level, offers it on a card.
            BoosterKind[] missing = ShelfBrief.Boosters
                .Where(booster => !session.Progress.Wallet.Shelf.Has(booster) && session.Stage.BoosterDiscPixel(booster) != null)
                .ToArray();
            if (missing.Length > 0)
            {
                BoosterKind offered = missing[0];
                player.Press(session.Stage.BoosterDiscPixel(offered), $"the disc of {offered.Name()}, of which none is held");
                player.Settle("the booster offer coming down");
                player.Require(session.Stage.OfferedBooster == offered, $"the disc of {offered.Name()} offered no booster");
                session.Fit(turn.From);
                player.Settle("the booster offer on the changed screen");
                player.RequireOnTheScreen(session.Stage.OfferBuyPixel, "the booster offer's buy button", turn.From);
                player.RequireOnTheScreen(session.Stage.OfferClosePixel, "the booster offer's cross", turn.From);
                player.Press(session.Stage.OfferClosePixel, "the booster offer's cross");
                player.Settle("the booster offer going up");
                player.Require(session.Stage.OfferedBooster == null, "the booster offer's cross did not take it up");
                return 2;
            }

            return 1;
        }

        /// <summary>The out-of-moves card, and the card of the loss for good after it, hang as the screen changes.</summary>
        private static int TurningOutOfMoves(Bench bench, Turn turn)
        {
            using GameSession session = bench.AtTheBoard(turn.From, bench.ShortLevel);
            var player = new Player(session, turn.From);
            player.Settle("the board coming in");
            while (session.Stage.Game.Status == GameStatus.Playing)
            {
                player.PlayTheBotsMove();
                player.Settle("a move of the short level");
            }

            player.Require(session.Stage.IsOfferingExtraMoves, $"level {LevelNumber} cut to {MovesOfTheShortLevel} moves ended {session.Stage.Game.Status} with no offer of more moves");
            session.Fit(turn.To);
            player.Settle("the out-of-moves card on the changed screen");
            bench.Shoot(session, turn, turn.To, "out-of-moves");
            player.RequireOnTheScreen(session.Stage.TakeTheMovesForCoinsPixel, "the out-of-moves card's +5 hamle", turn.To);
            player.RequireOnTheScreen(session.Stage.GiveUpPixel, "the out-of-moves card's Vazgeç", turn.To);
            AtTheOffer.GiveUp(session);

            session.Fit(turn.From);
            player.Settle("the lost card on the changed screen");
            player.Require(session.Stage.CardTells is LevelResult.Lost, "the lost card was gone after the screen changed");
            player.RequireOnTheScreen(session.Stage.ResultButtonPixel, "the lost card's button", turn.From);
            player.Press(session.Stage.ResultButtonPixel, "the lost card's button");
            player.WaitFor(() => session.Museum != null, "the lost card's button led nowhere");
            return 2;
        }

        /// <summary>A won game's finale goes on as the screen changes, ends on the engine's board, and its card hangs on the changed screen and back.</summary>
        private static int TurningDuringTheFinale(Bench bench, Turn turn)
        {
            using GameSession session = bench.AtTheBoard(turn.From, bench.EasyLevel);
            var player = new Player(session, turn.From);
            player.Settle("the board coming in");
            TurnScript winning = FlowStory.PlayTheGame(session, player.Bot, player.Rng);
            player.Require(session.Stage.Game.Status == GameStatus.Won && session.Stage.Game.MovesLeft > 0, $"the easy level ended {session.Stage.Game.Status} with {session.Stage.Game.MovesLeft} moves left; a finale needs a win with moves left");
            StageSteps.AdvanceToTheFinale(session.Stage, () => session.Advance(StoryStudio.FrameSeconds), winning.Seconds);
            player.Advance(MidFinale);
            player.Require(session.Stage.IsShowingTheFinale, "the finale was over before the screen changed");

            session.Fit(turn.To);
            bench.Shoot(session, turn, turn.To, "finale");
            player.ComeToRest("a finale the screen changed under");
            player.Require(session.Stage.CardTells is LevelResult.Won, "no card of the win hung after the finale the screen changed under");
            player.RequireOnTheScreen(session.Stage.ResultButtonPixel, "the won card's button", turn.To);
            bench.Shoot(session, turn, turn.To, "won");

            session.Fit(turn.From);
            player.Settle("the won card on the changed screen");
            player.RequireOnTheScreen(session.Stage.ResultButtonPixel, "the won card's button", turn.From);
            player.Press(session.Stage.ResultButtonPixel, "the won card's button");
            player.WaitFor(() => session.Board == null, "the won card's button led nowhere");
            return 2;
        }

        /// <summary>Each card of the museum hangs as the screen changes, keeps its buttons on the usable screen, and answers a press after.</summary>
        private static int TurningWithTheMuseumsCards(Bench bench, Turn turn)
        {
            using GameSession session = bench.InTheMuseum(turn.From);
            var player = new Player(session, turn.From);
            player.Settle("the hall coming in");
            int changes = 0;

            // The hall's question of the way back.
            session.GoBack();
            player.Settle("the hall's question coming down");
            player.Require(player.Museum.QuitQuestionShown != null, "the way back in the hall asked nothing");
            changes += player.TurnTo(turn.To);
            player.Require(player.Museum.QuitQuestionShown != null, "the hall's question was gone after the screen changed");
            session.GoBack();
            player.Settle("the hall's question going up");
            player.Require(player.Museum.QuitQuestionShown == null, "the way back did not take the hall's question up");

            // The lives card.
            player.Press(player.Museum.LivesPixel, "the wallet row's heart");
            player.Settle("the lives card coming down");
            player.Require(player.Museum.ShowsTheLivesCard, "the heart brought no lives card");
            changes += player.TurnTo(turn.From);
            player.RequireOnTheScreen(player.Museum.LivesCardDonePixel, "the lives card's Tamam", turn.From);
            player.Press(player.Museum.LivesCardDonePixel, "the lives card's Tamam");
            player.Settle("the lives card going up");
            player.Require(!player.Museum.ShowsTheLivesCard, "the lives card's Tamam did not take it up");

            // The settings card, and the privacy, support and language cards in front of it.
            player.Press(player.Museum.GearPixel, "the wallet row's gear");
            player.Settle("the settings card coming down");
            changes += player.TurnTo(turn.To);
            bench.Shoot(session, turn, turn.To, "settings");
            player.RequireOnTheScreen(player.Museum.CardClosePixel, "the settings card's cross", turn.To);
            foreach ((Func<Vector2?> row, string name) in new (Func<Vector2?>, string)[]
            {
                (() => player.Museum.PrivacyRowPixel, "privacy"),
                (() => player.Museum.SupportRowPixel, "support"),
                (() => player.Museum.LanguageRowPixel, "language"),
            })
            {
                player.Press(row(), $"the settings card's {name} row");
                player.Settle($"the {name} card coming down");
                ScreenShape other = player.Screen == turn.To ? turn.From : turn.To;
                changes += player.TurnTo(other);
                player.Require(player.Museum.SettingsCardInFront != null, $"the {name} card was gone after the screen changed");
                player.RequireOnTheScreen(player.Museum.CardClosePixel, $"the {name} card's cross", other);
                session.GoBack();
                player.Settle($"the {name} card going up");
            }

            session.GoBack();
            player.Settle("the settings card going up");
            player.Require(!player.Museum.ShowsTheSettings, "the way back did not take the settings card up");

            // The level's card: its Oyna opens the level on the changed screen.
            ThroughTheCard.BringDownTheCard(session);
            ScreenShape before = player.Screen;
            ScreenShape after = before == turn.To ? turn.From : turn.To;
            changes += player.TurnTo(after);
            bench.Shoot(session, turn, after, "level-card");
            player.RequireOnTheScreen(player.Museum.StartCardPlayPixel, "the level's card's Oyna", after);
            player.RequireOnTheScreen(player.Museum.StartCardClosePixel, "the level's card's cross", after);
            player.Press(player.Museum.StartCardPlayPixel, "the level's card's Oyna");
            player.WaitFor(() => session.Board != null, "the level's card's Oyna opened no board");
            return changes;
        }

        /// <summary>
        /// A piece carried from the tray towards its place when the screen changes: it is back in the tray, the finger
        /// that carried it places nothing when it lifts on the piece's place on the new screen, and the piece is then
        /// carried home afresh; the screen changes back while it drops in, and it is in its place.
        /// </summary>
        private static int TurningWhileAPieceIsCarried(Bench bench, Turn turn)
        {
            using GameSession session = bench.InTheMuseum(turn.From);
            var player = new Player(session, turn.From);
            player.Settle("the hall coming in");
            player.Press(player.Museum.JigsawButtonPixel, "the hall's Tablo");
            player.WaitFor(() => session.Jigsaw != null, "the hall's Tablo opened no jigsaw");
            player.Settle("the jigsaw coming in");
            JigsawStage jigsaw = session.Jigsaw ?? throw new InvalidOperationException("The jigsaw went away.");
            int piece = jigsaw.PiecesInTheTray.Count > 0 ? jigsaw.PiecesInTheTray[0] : throw new InvalidOperationException("The jigsaw's tray shows no piece.");

            Vector2 inTheTray = Player.PixelOrFail(jigsaw.PixelOfTheTrayPiece(piece), "the piece in the tray");
            session.PointerDown(inTheTray);
            player.Settle("the piece lifted");
            session.PointerMoved(Vector2.Lerp(inTheTray, Player.PixelOrFail(jigsaw.PixelToLetGoAt(piece, Vector2.zero), "the piece's place"), 0.5f));
            player.Advance(StoryStudio.FrameSeconds);
            player.Require(jigsaw.PieceInTheHand == piece, $"piece {piece} was not carried");
            bench.Shoot(session, turn, turn.From, "carried");

            session.Fit(turn.To);
            player.Require(jigsaw.PieceInTheHand == null && jigsaw.PiecesInTheTray.Contains(piece), $"piece {piece} carried as the screen changed is not back in the tray");
            session.PointerMoved(Player.PixelOrFail(jigsaw.PixelToLetGoAt(piece, Vector2.zero), "the piece's place on the new screen"));
            session.PointerUp();
            player.Advance(Tempo.PieceDropsIn + 0.2f);
            player.Require(!jigsaw.Table.InPlace.Contains(piece), $"piece {piece}, carried as the screen changed, was placed by the finger that carried it");
            player.Settle("the tray after the screen changed");
            bench.Shoot(session, turn, turn.To, "back-in-the-tray");

            Vector2 again = Player.PixelOrFail(jigsaw.PixelOfTheTrayPiece(piece), "the piece in the tray on the new screen");
            session.PointerDown(again);
            player.Settle("the piece lifted again");
            session.PointerMoved(Player.PixelOrFail(jigsaw.PixelToLetGoAt(piece, Vector2.zero), "the piece's place on the new screen"));
            player.Advance(StoryStudio.FrameSeconds);
            session.PointerUp();
            player.Advance(Tempo.PieceDropsIn / 2f);
            session.Fit(turn.From);
            player.Require(jigsaw.Table.InPlace.Contains(piece), $"piece {piece}, dropping in as the screen changed, is not in its place");
            player.Settle("the jigsaw on the screen changed back");
            return 2;
        }

        /// <summary>The entry page as its bar fills, with a finger resting on it, and with its card hanging, as the screen changes.</summary>
        private static int TurningTheEntryPage(Bench bench, Turn turn)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using EntryStage entry = EntryStage.Open(bench.Art, Application.version, turn.From);
            entry.Tell(0.45f);
            StageSteps.Advance(entry, 0.4f);
            entry.Fit(turn.To);
            entry.Tell(1f);
            StageSteps.Settle(entry);
            Require(entry.IsReady, "the entry page was not ready after the screen changed while its bar filled");

            entry.PointerDown(entry.MiddlePixel);
            entry.Fit(turn.From);
            entry.PointerUp();
            StageSteps.Advance(entry, 1f);
            Require(!entry.HasLeft, "a finger that rested on the entry page as the screen changed left the page when it lifted");

            entry.GoBack();
            StageSteps.Settle(entry);
            entry.Fit(turn.To);
            StageSteps.Settle(entry);
            bench.Shoot(entry, turn, turn.To, "entry-quit");
            Require(entry.QuitQuestionShown != null, "the entry page's card was gone after the screen changed");
            Vector2 stay = entry.StayInTheGamePixel ?? throw new InvalidOperationException("The entry page's card has no Vazgeç to press after the screen changed.");
            RequireOnTheScreen(stay, "the entry card's Vazgeç", turn.To);
            entry.PointerDown(stay);
            entry.PointerUp();
            StageSteps.Settle(entry);
            Require(entry.QuitQuestionShown == null, "the entry card's Vazgeç did not take it up");

            entry.PointerDown(entry.MiddlePixel);
            entry.PointerUp();
            StageSteps.Advance(entry, 1f);
            Require(entry.HasLeft, "a touch on the entry page after the screen changed did not leave it");
            return 3;
        }

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }

        /// <summary>A pixel lies on the usable screen: inside its edges and clear of the strips a cutout or a bar takes.</summary>
        private static void RequireOnTheScreen(Vector2 pixel, string what, ScreenShape screen)
        {
            bool isUsable = pixel.x >= screen.LeftInsetPixels && pixel.x <= screen.PixelWidth - screen.RightInsetPixels
                && pixel.y >= screen.BottomInsetPixels && pixel.y <= screen.PixelHeight - screen.TopInsetPixels;
            Require(isUsable, $"{what} is at ({pixel.x:0}, {pixel.y:0}), off the usable screen {screen}");
        }

        /// <summary>A change of the screen and back: from the first shape to the second.</summary>
        private sealed class Turn
        {
            public Turn(string name, ScreenShape from, ScreenShape to)
            {
                Name = name;
                From = from;
                To = to;
            }

            public string Name { get; }

            public ScreenShape From { get; }

            public ScreenShape To { get; }

            public override string ToString() => $"{From} and {To}";
        }

        /// <summary>What every part of the check opens its sessions with, and the sheets of their pictures.</summary>
        private sealed class Bench
        {
            private readonly IReadOnlyList<LevelDefinition> _levels;
            private readonly PaintingGallery _paintings;
            private readonly Dictionary<string, ContactSheet> _sheets = new Dictionary<string, ContactSheet>();

            public Bench(ArtCatalogue art, IReadOnlyList<LevelDefinition> levels, PaintingGallery paintings)
            {
                Art = art;
                _levels = levels;
                _paintings = paintings;
                Level = LevelPack.Numbered(levels, LevelNumber);
                ShortLevel = new LevelDefinition(Level.Id, Level.Difficulty, Level.Layout, Level.Goals, MovesOfTheShortLevel, Level.Spawn, Level.Attempts, Level.ContentVersion);
                EasyLevel = EarlyWin.Of(Level, GoalsOfTheEasyLevel);
            }

            public ArtCatalogue Art { get; }

            public LevelDefinition Level { get; }

            /// <summary>The level with so few moves that the bot runs out of them.</summary>
            public LevelDefinition ShortLevel { get; }

            /// <summary>The level with its goals cut, which the bot wins with moves left for a finale.</summary>
            public LevelDefinition EasyLevel { get; }

            /// <summary>
            /// A session at the board of a version of the check's level, on a scene of its own; a player who holds no
            /// booster is offered one by its disc.
            /// </summary>
            public GameSession AtTheBoard(ScreenShape screen, LevelDefinition level, bool holdsNoBooster = false)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                LevelDefinition[] levels = _levels.Select(each => each.Id.Value == LevelNumber ? level : each).ToArray();
                PlayerProgress standing = StoryStudio.StandingOn(level, levels);
                var progress = new ProgressInMemory(holdsNoBooster ? standing.WithWallet(standing.Wallet.WithShelf(BoosterShelf.Empty)) : standing);
                return new GameSession(levels, Art, _paintings, StoryStudio.Economy, StoryStudio.Clock, progress, StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.AtTheBoard);
            }

            /// <summary>A session in the hall of the museum's player, whose painting has pieces waiting.</summary>
            public GameSession InTheMuseum(ScreenShape screen)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var progress = new ProgressInMemory(PlayerState.Parse(MuseumPlayer).Progress);
                return new GameSession(_levels, Art, _paintings, StoryStudio.Economy, StoryStudio.Clock, progress, StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            }

            public void Shoot(GameSession session, Turn turn, ScreenShape screen, string moment) => Shoot(session.Front, turn, screen, moment);

            /// <summary>Writes what the stage shows now, after a change of the screen, and adds it to the sheet of its screen.</summary>
            public void Shoot(IStage stage, Turn turn, ScreenShape screen, string moment)
            {
                string key = screen.ToString();
                if (!_sheets.TryGetValue(key, out ContactSheet? sheet))
                {
                    sheet = new ContactSheet(shrinkBy: 4, mostInARow: 6);
                    _sheets.Add(key, sheet);
                }

                StoryStudio.Shoot(stage, screen, sheet, Path.Combine(Folder, $"rotation-{turn.Name}-{moment}-{screen}.png"));
            }

            public void WriteSheets()
            {
                foreach ((string screen, ContactSheet sheet) in _sheets)
                {
                    StoryStudio.WriteSheet(sheet, Path.Combine(Folder, $"rotation-sheet-{screen}.png"));
                }
            }
        }

        /// <summary>The session of a part of the check, with a bot to make moves and a finger to touch the screen.</summary>
        private sealed class Player
        {
            public Player(GameSession session, ScreenShape screen)
            {
                Session = session;
                Screen = screen;
                Bot = BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot)
                    ? bot
                    : throw new InvalidOperationException($"The bots' list has no {SkillProfile.Expert.Name}.");
                Rng = new Rng(BotSeed, BotStream);
            }

            public GameSession Session { get; }

            /// <summary>The screen the session was last fitted to by <see cref="TurnTo"/>.</summary>
            public ScreenShape Screen { get; private set; }

            public IBotPolicy Bot { get; }

            public Rng Rng { get; }

            public MuseumStage Museum => Session.Museum ?? throw new InvalidOperationException("The session is not in the museum, where the check expected it.");

            private Game Game => Session.Stage.Game;

            public static Vector2 PixelOrFail(Vector2? pixel, string what) =>
                pixel ?? throw new InvalidOperationException($"The screen does not show {what}, which the check was to touch.");

            /// <summary>Changes the screen while what is shown stands, and lets it come to rest there; one change.</summary>
            public int TurnTo(ScreenShape screen)
            {
                Session.Fit(screen);
                Screen = screen;
                Settle($"the screen changed to {screen}");
                return 1;
            }

            public void Require(bool holds, string otherwise) => RotationCheck.Require(holds, otherwise);

            public void RequireOnTheScreen(Vector2? pixel, string what, ScreenShape screen) =>
                RotationCheck.RequireOnTheScreen(PixelOrFail(pixel, what), what, screen);

            /// <summary>A finger comes down on a control and lifts there.</summary>
            public void Press(Vector2? pixel, string what)
            {
                Vector2 at = PixelOrFail(pixel, what);
                Session.PointerDown(at);
                Session.PointerUp();
            }

            public void Advance(float seconds) => StoryStudio.AdvanceBy(seconds, Session);

            public void Settle(string what)
            {
                try
                {
                    StoryStudio.AdvanceUntilSettled(Session);
                }
                catch (InvalidOperationException failure)
                {
                    throw new InvalidOperationException($"After {what}: {failure.Message}", failure);
                }
            }

            /// <summary>Moves the clock until something has happened that a press leads to.</summary>
            public void WaitFor(Func<bool> hasHappened, string otherwise)
            {
                for (float waited = 0f; !hasHappened(); waited += StoryStudio.FrameSeconds)
                {
                    Require(waited <= LongestWait, $"after {LongestWait} s {otherwise}");
                    Session.Advance(StoryStudio.FrameSeconds);
                }
            }

            /// <summary>A trade of two neighbours that the engine would play on its board as it stands.</summary>
            public Move ALegalTrade()
            {
                foreach (Move move in Game.FindLegalMoves())
                {
                    if (move.Kind == MoveKind.Swap)
                    {
                        return move;
                    }
                }

                throw new InvalidOperationException($"Level {LevelNumber} has no trade to make at move {Game.MovesUsed}; pick another level for this check.");
            }

            public void Swipe(Move trade)
            {
                Session.PointerDown(Session.Stage.PixelOf(trade.From));
                Session.PointerMoved(Session.Stage.PixelOf(trade.To));
                Session.PointerUp();
            }

            /// <summary>The bot's move, played on the stage at rest; the clock stands at the move.</summary>
            public void PlayTheBotsMove()
            {
                Move move = Bot.ChooseMove(Game, Game.FindLegalMoves(), Rng);
                Require(Session.TryPlay(move), $"the stage refused the bot's move {move} at move {Game.MovesUsed + 1}");
            }

            public void Expect(int movesPlayed, string when) =>
                Require(Game.MovesUsed == movesPlayed, $"{Game.MovesUsed} moves were played {when}; {movesPlayed} were expected");

            /// <exception cref="InvalidOperationException">What the stage shows at rest is not the engine's board.</exception>
            public void ComeToRest(string after)
            {
                Settle(after);
                Require(Session.Stage.LastTurnFaults.Count == 0, $"after {after} the stage did not show the engine's board: {string.Join("; ", Session.Stage.LastTurnFaults)}");
            }
        }
    }
}
