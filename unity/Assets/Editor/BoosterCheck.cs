#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line, by the touches a player makes on the game's own session, that the boosters used
    /// during play do what stage 10 promises. They open one at a time (levels 6/7, 11/12, 15/16, 19/20): before its
    /// level a disc is locked and a press on it does nothing, on its level it is guided once and its gift is in the
    /// wallet, once. Each kind is armed by its disc, aimed with a finger that slides, used on the lift, spent and saved
    /// in the save file at once; the first of them counts as the attempt's first move, and leaving after it costs a
    /// life and writes the attempt's record, which replays to the end seen. A use that changes nothing is refused,
    /// shakes its disc, says why and spends nothing. A disc pressed while pieces move arms its booster, and a use made
    /// then waits for the board to come to rest. The armed disc, the way back, a lift off the board and the pause disc
    /// put a booster down. The plus on a disc of which none is held offers one for its price, which a press buys and
    /// arms, and which a player without the coins cannot buy. A shuffle the first rule refused on a large open board
    /// is accepted. It works in a folder of its own (out/unity/booster-check).
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.BoosterCheck.Run
    /// </summary>
    public static class BoosterCheck
    {
        private const string Folder = "../out/unity/booster-check";

        /// <summary>Every booster is open on this level, and its gifts are due.</summary>
        private const int OpenLevel = 40;

        /// <summary>A large open board, where the first shuffle rule was often refused.</summary>
        private const int LargeLevel = 47;

        /// <summary>A press that leads nowhere within this long led nowhere.</summary>
        private const float LongestWait = 3f;

        private static readonly ScreenShape Screen = new ScreenShape(1080, 2340);

        private static readonly (BoosterKind Booster, int Level)[] Openings =
        {
            (BoosterKind.HitCell, 7), (BoosterKind.HitRow, 12), (BoosterKind.HitColumn, 16), (BoosterKind.Shuffle, 20),
        };

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (Directory.Exists(Folder))
                {
                    Directory.Delete(Folder, recursive: true);
                }

                Directory.CreateDirectory(Folder);
                var bench = new Bench();
                var passed = new List<string>();
                passed.Add(TheBoostersOpenOneAtATimeWithTheirGiftOnce(bench));
                passed.Add(EachBoosterIsArmedAimedUsedSpentAndSaved(bench));
                passed.Add(TheFirstBoosterIsTheFirstMoveAndAnUntouchedBoardIsLeftFree(bench));
                passed.Add(ABoosterIsPutDownByItsDiscTheWayBackALiftOffTheBoardAndThePause(bench));
                passed.Add(ADiscPressedWhilePiecesMoveArmsAndItsUseWaitsForRest(bench));
                passed.Add(ARefusedUseShakesSaysWhyAndSpendsNothing(bench));
                passed.Add(ThePlusOffersOneWhichAPressBuysAndArms(bench));
                passed.Add(ABoosterThatWinsGoesOnToTheFinaleAndTheCard(bench));
                passed.Add(AShuffleTheFirstRuleRefusedIsAccepted(bench));
                Debug.Log($"Booster check passed: {string.Join("; ", passed)}.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is ArgumentException || failure is IOException || failure is AttemptReplayException)
            {
                Debug.LogError($"Booster check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Before its level a disc is locked and does nothing; on it the booster is guided on the first try and its gift is held, once.</summary>
        private static string TheBoostersOpenOneAtATimeWithTheirGiftOnce(Bench bench)
        {
            foreach ((BoosterKind booster, int level) in Openings)
            {
                using (GameSession before = bench.SessionAt(level - 1, bench.NewPlayersWallet()))
                {
                    GameStage stage = before.Stage;
                    PlayerProgress was = before.Progress;
                    Require(stage.ShelfFaces[booster].Equals(BoosterFace.LockedUntil(level)), $"on level {level - 1} the disc of {booster.Name()} shows {stage.ShelfFaces[booster]}");
                    Require(stage.BoosterDiscPixel(booster) == null, $"the locked disc of {booster.Name()} takes a finger");
                    Require(was.Wallet.Shelf.Count(booster) == 0, $"a new player on level {level - 1} holds {was.Wallet.Shelf.Count(booster)} {booster.Name()} before it opens");
                    Tap(before, stage.DiscPixel(booster), $"the locked disc of {booster.Name()}");
                    StoryStudio.AdvanceUntilSettled(before);
                    Require(stage.ArmedBooster == null && stage.OfferedBooster == null && before.Progress.Equals(was), $"a press on the locked disc of {booster.Name()} did something");
                }

                var store = new ProgressInMemory(bench.StandingOn(level, bench.NewPlayersWallet()));
                using (GameSession onIt = bench.SessionOver(store))
                {
                    GameStage stage = onIt.Stage;
                    Require(onIt.Progress.Wallet.Shelf.Count(booster) == 3, $"on level {level} the player holds {onIt.Progress.Wallet.Shelf.Count(booster)} {booster.Name()}, not the gift of 3");
                    Require(onIt.Progress.Wallet.Rewards.UnlockGifts.Contains(booster), $"the gift of {booster.Name()} is not kept as given");
                    Require(stage.GuidedBooster == booster, $"on level {level} the guided booster is {stage.GuidedBooster}");
                    string? aside = booster == BoosterKind.HitCell ? BoosterWords.EveryLevelWithout : null;
                    Require(stage.WhatThePillSays.Equals(new PillWords.Guide(BoosterWords.FirstUse(booster), aside)), $"on level {level} the pill says \"{stage.WhatThePillSays}\"");
                }

                using (GameSession again = bench.SessionOver(store))
                {
                    Require(again.Progress.Wallet.Shelf.Count(booster) == 3, $"a second session on level {level} gave the gift of {booster.Name()} again");
                }

                using (GameSession retry = bench.SessionAt(level, bench.NewPlayersWallet(), attemptsOnLevel: 1))
                {
                    Require(retry.Stage.GuidedBooster == null, $"a retry of level {level} guides {retry.Stage.GuidedBooster} again");
                }
            }

            return "locked before 7, 12, 16, 20 and open on them, each guided once with its gift once";
        }

        /// <summary>Each kind by touches on level 40: armed by its disc, aimed with a finger that slides, used on the lift, spent and saved at once.</summary>
        private static string EachBoosterIsArmedAimedUsedSpentAndSaved(Bench bench)
        {
            string folder = Path.Combine(Folder, "save");
            Directory.CreateDirectory(folder);
            var store = new ProgressFile(folder, StoryStudio.Economy.StartingWallet);
            store.Save(bench.StandingOn(OpenLevel, bench.NewPlayersWallet()));
            var log = new AttemptsInMemory(StoryStudio.Device);
            using GameSession session = bench.SessionOver(store, log);
            GameStage stage = session.Stage;
            int attemptsBefore = session.Progress.AttemptsOnLevel;
            foreach (BoosterKind booster in BoosterKinds.InLevel)
            {
                int held = session.Progress.Wallet.Shelf.Count(booster);
                Require(held == 3, $"the player holds {held} {booster.Name()} on level {OpenLevel}");
                Tap(session, stage.BoosterDiscPixel(booster), $"the disc of {booster.Name()}");
                if (booster != BoosterKind.Shuffle)
                {
                    Require(stage.ArmedBooster == booster, $"a press on the disc of {booster.Name()} armed {stage.ArmedBooster}");
                    Require(stage.WhatThePillSays.Equals(new PillWords.Armed(BoosterWords.Armed(booster), BoosterWords.WhatToPick(booster))), $"the pill of the armed {booster.Name()} says \"{stage.WhatThePillSays}\"");
                    Require(stage.ShelfFaces.All(face => face.Key == booster ? face.Value.Choice == BoosterChoice.Armed : face.Value.IsFaint), "the other discs are not set aside while one is armed");
                    (Position first, Position aimed) = TwoCellsToHit(stage.Game);
                    session.PointerDown(stage.PixelOf(first));
                    Require(stage.AimedAt == first, $"a finger down on {first} aims at {stage.AimedAt}");
                    session.PointerMoved(stage.PixelOf(aimed));
                    Require(stage.AimedAt == aimed, $"the finger slid to {aimed} and the aim stayed at {stage.AimedAt}");
                    session.PointerUp();
                }

                StoryStudio.AdvanceUntilSettled(session);
                PlayerProgress saved = new ProgressFile(folder, StoryStudio.Economy.StartingWallet).Load();
                Require(session.Progress.Wallet.Shelf.Count(booster) == held - 1, $"the use of {booster.Name()} left {session.Progress.Wallet.Shelf.Count(booster)} of {held}");
                Require(saved.Equals(session.Progress), $"the save file after {booster.Name()} holds {saved}, the session {session.Progress}");
                Require(stage.Game.BoostersUsed.Last() == booster.Name() && stage.Game.MovesUsed == 0, $"the game did not use {booster.Name()} without a move");
                Require(session.Progress.AttemptsOnLevel == attemptsBefore + 1, $"after {booster.Name()} the level counts {session.Progress.AttemptsOnLevel} attempts, not {attemptsBefore + 1}");
                Require(stage.LastTurnFaults.Count == 0, $"the booster {booster.Name()} was not shown as the game played it: {string.Join("; ", stage.LastTurnFaults)}");
            }

            ulong boardAtTheEnd = stage.Game.Board.ComputeHash();
            Leave(session);
            AttemptRecord record = log.Records.Single();
            Game replayed = AttemptReplay.Of(record, bench.Level(OpenLevel));
            Require(record.Start.ShuffleRule == ShuffleBoosterRule.LaidWithoutAMatch && AttemptLine.Of(record).Contains("\"shuffleRule\":2"), "the record does not name the shuffle rule of today");
            Require(replayed.Board.ComputeHash() == boardAtTheEnd && replayed.BoostersUsed.SequenceEqual(record.Outcome.BoostersUsed), "the record did not replay to the board the attempt was left on, with its boosters");
            return "the four kinds by touches, each spent and saved at once, the record of the attempt replayed";
        }

        /// <summary>A booster that is the first thing the engine accepts counts as the first move: the start boosters are spent, and leaving costs a life and writes a record. An untouched board is left free.</summary>
        private static string TheFirstBoosterIsTheFirstMoveAndAnUntouchedBoardIsLeftFree(Bench bench)
        {
            var log = new AttemptsInMemory(StoryStudio.Device);
            using (GameSession untouched = bench.SessionAt(OpenLevel, bench.GiftedWallet(), log: log))
            {
                PlayerProgress was = untouched.Progress;
                Leave(untouched);
                Require(untouched.Progress.Equals(was) && log.Records.Count == 0, "leaving a board nobody touched cost something or wrote a record");
            }

            using GameSession session = bench.SessionInTheHall(OpenLevel, bench.GiftedWallet(), log);
            PlayerProgress before = session.Progress;
            ThroughTheCard.IntoTheLevel(session, new[] { BoosterKind.StartExtraMoves });
            GameStage stage = session.Stage;
            Require(session.Progress.Equals(before), "going into the level with a start booster chosen changed the progress before anything was done");
            Tap(session, stage.BoosterDiscPixel(BoosterKind.HitCell), "the spatula's disc");
            (Position cell, _) = TwoCellsToHit(stage.Game);
            Tap(session, stage.PixelOf(cell), $"the cell {cell}");
            StoryStudio.AdvanceUntilSettled(session);
            Require(session.Progress.AttemptsOnLevel == before.AttemptsOnLevel + 1, "the spatula, used first, did not count the attempt");
            Require(session.Progress.Wallet.Shelf.Count(BoosterKind.StartExtraMoves) == before.Wallet.Shelf.Count(BoosterKind.StartExtraMoves) - 1, "the start booster was not spent with the spatula used first");
            Require(stage.Game.MovesUsed == 0, "a move was played");

            int lives = session.Progress.Wallet.Lives.LivesUnder(StoryStudio.Economy.Lives);
            Leave(session);
            Require(session.Progress.Wallet.Lives.LivesUnder(StoryStudio.Economy.Lives) == lives - 1, "leaving after the spatula alone cost no life");
            Require(log.Records.Count == 1 && log.Records[0].Outcome.Result == AttemptResult.Abandoned, "leaving after the spatula alone wrote no record of an abandoned attempt");
            AttemptReplay.Of(log.Records[0], bench.Level(OpenLevel));
            return "a spatula used first counts the attempt, spends the start booster, and leaving after it costs a life and writes a record that replays";
        }

        /// <summary>The armed disc, the way back, a lift off the board and the pause disc each put an armed booster down; nothing is spent.</summary>
        private static string ABoosterIsPutDownByItsDiscTheWayBackALiftOffTheBoardAndThePause(Bench bench)
        {
            using GameSession session = bench.SessionAt(OpenLevel, bench.GiftedWallet());
            GameStage stage = session.Stage;
            PlayerProgress was = session.Progress;
            Vector2 disc = stage.BoosterDiscPixel(BoosterKind.HitRow) ?? throw new InvalidOperationException("The row brush's disc is not there to press.");

            Tap(session, disc, "the row brush's disc");
            Tap(session, stage.BoosterDiscPixel(BoosterKind.HitRow), "the armed row brush's disc");
            Require(stage.ArmedBooster == null, "a second press on the armed disc did not put it down");

            Tap(session, disc, "the row brush's disc");
            session.GoBack();
            Require(stage.ArmedBooster == null && !stage.IsAskingWhetherToLeave, "the way back did not put the armed booster down, or asked to leave");

            Tap(session, disc, "the row brush's disc");
            (Position cell, _) = TwoCellsToHit(stage.Game);
            session.PointerDown(stage.PixelOf(cell));
            session.PointerMoved(stage.PixelOf(new Position(cell.X, -3))); // up over the plaques, off the board's cells
            Require(stage.AimedAt == null, "a finger off the board still aims");
            session.PointerUp();
            Require(stage.ArmedBooster == null, "a lift off the board did not put the armed booster down");

            Tap(session, disc, "the row brush's disc");
            Tap(session, stage.PauseButtonPixel, "the pause disc");
            StoryStudio.AdvanceUntilSettled(session);
            Require(stage.ArmedBooster == null && stage.IsAskingWhetherToLeave, "the pause disc did not put the armed booster down and ask whether to leave");
            session.GoBack();
            StoryStudio.AdvanceUntilSettled(session);
            Require(session.Progress.Equals(was) && stage.Game.BoostersUsed.Count == 0, "putting a booster down spent something");
            return "put down by its disc, the way back, a lift off the board and the pause, nothing spent";
        }

        /// <summary>A disc pressed while a move is shown arms its booster; the lift over the board waits for rest, and the use is made then.</summary>
        private static string ADiscPressedWhilePiecesMoveArmsAndItsUseWaitsForRest(Bench bench)
        {
            using GameSession session = bench.SessionAt(OpenLevel, bench.GiftedWallet());
            GameStage stage = session.Stage;
            (Position aimed, _) = TwoCellsToHit(stage.Game);
            Move move = stage.Game.FindLegalMoves()[0];
            Require(session.TryPlay(move), $"the move {move} was refused");
            session.Advance(StoryStudio.FrameSeconds);
            Require(!stage.IsSettled, "the board did not move after a move");

            Tap(session, stage.BoosterDiscPixel(BoosterKind.HitColumn), "the column brush's disc while pieces move");
            Require(stage.ArmedBooster == BoosterKind.HitColumn, "a press while pieces move did not arm the column brush");
            Tap(session, stage.PixelOf(aimed), "a cell while pieces move");
            Require(stage.ArmedBooster == BoosterKind.HitColumn && stage.Game.BoostersUsed.Count == 0, "the use was made before the board came to rest");
            StoryStudio.AdvanceUntilSettled(session);
            Require(stage.Game.BoostersUsed.Contains(BoosterNames.Of(new BoosterUse.HitColumn(default))), "the use that waited was not made once the board came to rest");
            Require(session.Progress.Wallet.Shelf.Count(BoosterKind.HitColumn) == 2, "the use that waited was not spent");
            return "a disc pressed while pieces moved armed, and the use waited for rest";
        }

        /// <summary>A spatula on an item and a shuffle of two alike gems change nothing: the disc shakes, the pill says why, and nothing is spent or told.</summary>
        private static string ARefusedUseShakesSaysWhyAndSpendsNothing(Bench bench)
        {
            using EffectArt effectArt = EffectArt.Paint();
            using (GameStage stage = bench.HandMadeStage(effectArt, Filler(7, 7).With(0, 0, 'H').With(3, 3, 'I')))
            {
                Tap(stage, stage.BoosterDiscPixel(BoosterKind.HitCell), "the spatula's disc");
                Tap(stage, stage.PixelOf(new Position(3, 3)), "the item's cell");
                Require(stage.ArmedBooster == BoosterKind.HitCell, "a refused spatula was put down; it stays armed for another aim");
                Require(stage.WhatThePillSays.Equals(new PillWords.Line(BoosterWords.Refused(BoosterKind.HitCell))), $"the pill of a refused spatula says \"{stage.WhatThePillSays}\"");
                Require(stage.TakeWhatHappened().Count == 0 && stage.Game.BoostersUsed.Count == 0, "a refused spatula was reported or used");
                Require(stage.ShelfFaces[BoosterKind.HitCell].Owned == 3, "a refused spatula was spent");
            }

            string[] twoAlike = Blank(3, 3, 'H').With(0, 0, 'r').With(2, 2, 'r');
            using (GameStage stage = bench.HandMadeStage(effectArt, twoAlike))
            {
                Tap(stage, stage.BoosterDiscPixel(BoosterKind.Shuffle), "the shuffle's disc");
                Require(stage.ArmedBooster == null, "a refused shuffle stays armed");
                Require(stage.WhatThePillSays.Equals(new PillWords.Line(BoosterWords.Refused(BoosterKind.Shuffle))), $"the pill of a refused shuffle says \"{stage.WhatThePillSays}\"");
                Require(stage.TakeWhatHappened().Count == 0 && stage.ShelfFaces[BoosterKind.Shuffle].Owned == 3, "a refused shuffle was reported or spent");
            }

            return "a spatula on an item and a shuffle of two alike gems refused, said why, spent nothing";
        }

        /// <summary>The plus on a disc of which none is held offers one; a press buys it for its price and arms it. A player short of the price cannot buy, and the cross closes the card.</summary>
        private static string ThePlusOffersOneWhichAPressBuysAndArms(Bench bench)
        {
            Wallet withoutARowBrush = bench.GiftedWallet().WithShelf(bench.GiftedWallet().Shelf.WithOneFewer(BoosterKind.HitRow).WithOneFewer(BoosterKind.HitRow).WithOneFewer(BoosterKind.HitRow));
            Coins price = StoryStudio.Economy.Shop.PriceOf(BoosterKind.HitRow);
            using (GameSession session = bench.SessionAt(OpenLevel, withoutARowBrush.WithCoins(new Coins(2000))))
            {
                GameStage stage = session.Stage;
                Tap(session, stage.BoosterDiscPixel(BoosterKind.HitRow), "the plus of the row brush");
                StoryStudio.AdvanceUntilSettled(session);
                Require(stage.OfferedBooster == BoosterKind.HitRow, "the plus offered nothing");
                Tap(session, stage.OfferBuyPixel, "the offer's green button");
                StoryStudio.AdvanceUntilSettled(session);
                Require(session.Progress.Wallet.Coins == new Coins(2000).Less(price), $"the row brush cost {2000 - session.Progress.Wallet.Coins.Amount} coins, not {price}");
                Require(stage.OfferedBooster == null && stage.ArmedBooster == BoosterKind.HitRow, "the bought row brush was not armed with the card gone");
                Require(session.Progress.Wallet.Shelf.Count(BoosterKind.HitRow) == 1, "the bought row brush is not held");
            }

            using (GameSession poor = bench.SessionAt(OpenLevel, withoutARowBrush.WithCoins(Coins.None)))
            {
                GameStage stage = poor.Stage;
                Tap(poor, stage.BoosterDiscPixel(BoosterKind.HitRow), "the plus of the row brush");
                StoryStudio.AdvanceUntilSettled(poor);
                Tap(poor, stage.OfferBuyPixel, "the dimmed green button");
                StoryStudio.AdvanceUntilSettled(poor);
                Require(poor.Progress.Wallet.Shelf.Count(BoosterKind.HitRow) == 0 && stage.OfferedBooster == BoosterKind.HitRow, "a player without the coins bought a row brush, or the card went");
                Tap(poor, stage.OfferClosePixel, "the offer's cross");
                StoryStudio.AdvanceUntilSettled(poor);
                Require(stage.OfferedBooster == null && stage.ArmedBooster == null, "the cross did not take the card up");
            }

            return $"the plus offered a row brush for {price}, the press bought and armed it, a player without the coins could not";
        }

        /// <summary>A row brush that clears the last of the goal wins the level as a move does: reported won, its finale shown, its card hung.</summary>
        private static string ABoosterThatWinsGoesOnToTheFinaleAndTheCard(Bench bench)
        {
            using EffectArt effectArt = EffectArt.Paint();

            // The filler's fourth row holds the board's only two red gems at the start; the goal asks for two.
            using GameStage stage = bench.HandMadeStage(effectArt, Filler(7, 7).With(0, 0, 'H'), new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 2) });
            Tap(stage, stage.BoosterDiscPixel(BoosterKind.HitRow), "the row brush's disc");
            stage.PointerDown(stage.PixelOf(new Position(3, 3)));
            stage.PointerUp();
            Require(stage.Game.Status == GameStatus.Won, $"the row brush through the two red gems left the game {stage.Game.Status}");
            IReadOnlyList<AttemptEvent> happened = stage.TakeWhatHappened();
            Require(
                happened.Count == 3 && happened[0] is AttemptEvent.FirstMove && happened[1] is AttemptEvent.BoosterAccepted && happened[2] is AttemptEvent.Won,
                $"the winning brush reported [{string.Join(", ", happened)}], not its first move, the booster and the win");
            StageSteps.Settle(stage);
            Require(stage.Finale != null && stage.ResultButtonPixel != null, "the win by a booster had no finale, or no card hangs after it");
            Require(stage.LastTurnFaults.Count == 0, $"the winning brush or its finale was not shown as the game played it: {string.Join("; ", stage.LastTurnFaults)}");
            return "a row brush that won showed its finale and hung the card";
        }

        /// <summary>On level 47 a board the first rule refused to shuffle is shuffled by the disc: the rule of today always shuffles there.</summary>
        private static string AShuffleTheFirstRuleRefusedIsAccepted(Bench bench)
        {
            LevelDefinition level = bench.Level(LargeLevel);
            (int attempt, int moves) = FirstRuleRefusals.On(level);
            using GameSession session = bench.SessionAt(LargeLevel, bench.GiftedWallet(), attemptsOnLevel: attempt);
            GameStage stage = session.Stage;
            FirstRuleRefusals.PlayTheFirstMoves(session, moves);
            ulong before = stage.Game.Board.ComputeHash();
            Tap(session, stage.BoosterDiscPixel(BoosterKind.Shuffle), "the shuffle's disc");
            StoryStudio.AdvanceUntilSettled(session);
            Require(stage.Game.BoostersUsed.Contains("shuffle") && stage.Game.Board.ComputeHash() != before, $"the shuffle of attempt {attempt} of level {LargeLevel} was refused");
            Require(stage.LastTurnFaults.Count == 0, $"the shuffle was not shown as the game played it: {string.Join("; ", stage.LastTurnFaults)}");
            return $"attempt {attempt} of level {LargeLevel} after {moves} moves, refused by the first rule, shuffled";
        }

        /// <summary>Two neighbouring cells of the board with a gem each, low on the board: what a strike hits wherever it is aimed.</summary>
        private static (Position First, Position Second) TwoCellsToHit(Game game)
        {
            BoardShape shape = game.Board.Shape;
            for (int y = shape.Height - 2; y >= 1; y--)
            {
                for (int x = 1; x < shape.Width - 1; x++)
                {
                    var cell = new Position(x, y);
                    var next = new Position(x + 1, y);
                    if (shape.IsPlayable(cell) && shape.IsPlayable(next) && game.Board.TileAt(cell).IsGem && game.Board.TileAt(next).IsGem && game.Board.ChainAt(next) == 0)
                    {
                        return (cell, next);
                    }
                }
            }

            throw new InvalidOperationException($"Level {game.Level.Id} has no two neighbouring gems to aim at.");
        }

        private static void Leave(GameSession session)
        {
            StoryStudio.AdvanceUntilSettled(session);
            Tap(session, session.Stage.PauseButtonPixel, "the shelf's pause disc");
            StoryStudio.AdvanceUntilSettled(session);
            Require(session.Stage.IsAskingWhetherToLeave, "the shelf's pause disc did not ask whether to leave the level");
            Tap(session, session.Stage.MuseumButtonPixel, "the leaving card's museum button");
            for (float waited = 0f; session.Museum == null; waited += StoryStudio.FrameSeconds)
            {
                Require(waited <= LongestWait, "the leaving card's museum button opened no museum");
                session.Advance(StoryStudio.FrameSeconds);
            }
        }

        private static void Tap(GameSession session, Vector2? pixel, string what)
        {
            session.PointerDown(pixel ?? throw new InvalidOperationException($"There is no {what} to press."));
            session.PointerUp();
        }

        private static void Tap(GameStage stage, Vector2? pixel, string what)
        {
            stage.PointerDown(pixel ?? throw new InvalidOperationException($"There is no {what} to press."));
            stage.PointerUp();
            StageSteps.Settle(stage);
        }

        private static string[] Filler(int width, int height) =>
            Enumerable.Range(0, height).Select(y => new string(Enumerable.Range(0, width).Select(x => "rgby"[(x + 2 * y) % 4]).ToArray())).ToArray();

        private static string[] Blank(int width, int height, char glyph) => Enumerable.Repeat(new string(glyph, width), height).ToArray();

        private static string[] With(this string[] rows, int x, int y, char glyph)
        {
            var copy = (string[])rows.Clone();
            char[] row = copy[y].ToCharArray();
            row[x] = glyph;
            copy[y] = new string(row);
            return copy;
        }

        /// <exception cref="InvalidOperationException">What the game relies on did not hold; the message says what happened instead.</exception>
        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(char.ToUpperInvariant(otherwise[0]) + otherwise.Substring(1) + ".");
            }
        }

        /// <summary>What the check plays on: the packed levels, art and museum.</summary>
        private sealed class Bench
        {
            private readonly IReadOnlyList<LevelDefinition> _levels = LevelPack.LoadPacked();
            private readonly ArtCatalogue _art;
            private readonly PaintingGallery _paintings = PaintingGallery.OfThePackedMuseum();

            /// <exception cref="InvalidOperationException">The art catalogue is missing.</exception>
            public Bench()
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                _art = art != null ? art : throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
            }

            public LevelDefinition Level(int number) => LevelPack.Numbered(_levels, number);

            /// <summary>A new player's wallet: the welcome gift, no opening gift given yet.</summary>
            public Wallet NewPlayersWallet() => StoryStudio.Economy.StartingWallet;

            /// <summary>A wallet that was given every opening gift: three of each booster used during play.</summary>
            public Wallet GiftedWallet() => StoryStudio.Economy.Unlocks.GivenTo(StoryStudio.Economy.StartingWallet, level: OpenLevel);

            public PlayerProgress StandingOn(int levelNumber, Wallet wallet, int attemptsOnLevel = 0)
            {
                PlayerProgress standing = StoryStudio.StandingOn(Level(levelNumber), _levels);
                return new PlayerProgress(standing.LevelsWon, attemptsOnLevel, standing.Museum.Placed, wallet);
            }

            public GameSession SessionAt(int levelNumber, Wallet wallet, int attemptsOnLevel = 0, IAttemptLog? log = null) =>
                SessionOver(new ProgressInMemory(StandingOn(levelNumber, wallet, attemptsOnLevel)), log);

            /// <summary>The game's own session at the board of the level the store's progress stands on, at rest.</summary>
            public GameSession SessionOver(IProgressStore store, IAttemptLog? log = null)
            {
                var session = new GameSession(_levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, store, log ?? StoryStudio.AttemptsKeptInMemory(), Screen, SessionOpening.AtTheBoard);
                StoryStudio.AdvanceUntilSettled(session);
                return session;
            }

            public GameSession SessionInTheHall(int levelNumber, Wallet wallet, IAttemptLog log) =>
                new GameSession(_levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(StandingOn(levelNumber, wallet)), log, Screen, SessionOpening.InTheMuseum);

            /// <summary>A stage of a hand-made level, as level 40 opens its shelf, with three of each booster held; laid out and at rest.</summary>
            public GameStage HandMadeStage(EffectArt effectArt, string[] tiles, GoalSpec[]? asked = null)
            {
                LevelLayout layout = LevelLayout.Parse(new LayoutLayers { Cells = Blank(tiles[0].Length, tiles.Length, '#'), Tiles = tiles });
                bool hasItems = tiles.Any(row => row.Contains('I'));
                GoalSpec[] goals = asked ?? (hasItems ? new GoalSpec[] { new GoalSpec.DropItems(1) } : new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 99) });
                var level = new LevelDefinition(new LevelId(OpenLevel), DifficultyTier.Normal, layout, goals, 30, new SpawnRules { Palette = GemColors.First(6), Items = ItemSpawnRules.None });
                Game game = Game.Start(level, 1, new GameOptions { RecordEvents = true, RecordActions = true });
                GameStage stage = GameStage.Open(game, _art, effectArt, _paintings, AttemptBrief.FirstTry(StoryStudio.Economy.Win), ShelfBrief.Of(OpenLevel, attemptsBefore: 1, StoryStudio.Economy));
                stage.Fit(Screen);
                stage.Tell(WalletView.Of(GiftedWallet(), StoryStudio.Economy.Lives, StoryStudio.Clock.Now), StoryStudio.ExtraMovesFor(GiftedWallet()));
                StageSteps.Settle(stage);
                return stage;
            }
        }
    }

    /// <summary>
    /// Where the shuffle booster's first rule refused a shuffle on a real level that the rule of today shuffles: the
    /// try of the level, and how many moves were played on it first, each the first legal move of the board as it lay.
    /// </summary>
    internal static class FirstRuleRefusals
    {
        private const int MostAttempts = 80;
        private const int MostMoves = 6;

        /// <exception cref="InvalidOperationException">The first rule refused no shuffle within the tries and moves searched.</exception>
        public static (int Attempt, int Moves) On(LevelDefinition level)
        {
            for (int attempt = 0; attempt < MostAttempts; attempt++)
            {
                Game firstRule = Game.Start(level, LevelStart.SeedsOf(level, attempt), new GameOptions { ShuffleBoosterRule = ShuffleBoosterRule.RandomOrders });
                for (int moves = 0; moves <= MostMoves && firstRule.Status == GameStatus.Playing; moves++)
                {
                    if (!firstRule.CreateReplica().TryUseBooster(new BoosterUse.Shuffle()))
                    {
                        return (attempt, moves);
                    }

                    firstRule.TryPlay(firstRule.FindLegalMoves()[0]);
                }
            }

            throw new InvalidOperationException($"The first shuffle rule refused no shuffle on level {level.Id} in {MostAttempts} tries of up to {MostMoves} moves.");
        }

        /// <summary>Plays the first legal move of the board, as it lies, so many times on the session's board, each to its rest.</summary>
        public static void PlayTheFirstMoves(GameSession session, int moves)
        {
            for (int played = 0; played < moves; played++)
            {
                Game game = session.Stage.Game;
                if (!session.TryPlay(game.FindLegalMoves()[0]))
                {
                    throw new InvalidOperationException($"The first legal move of level {game.Level.Id} was refused.");
                }

                StoryStudio.AdvanceUntilSettled(session);
            }
        }
    }
}
