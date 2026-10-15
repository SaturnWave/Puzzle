#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Pictures of the offer of five more moves, with no game running, on stages of rehearsed games (RehearsedGame)
    /// that run out of moves as late as they can with every goal still open: levels of one, two and three goals, each
    /// for a player whose coins cover the price and who has the day's free ads, and for one whose coins fall short
    /// and whose ads are used up; on the level of one goal also the card that stands in for an ad as it begins and as
    /// it ends, the five moves counted up and the board going on, and the card of the loss for good after "Vazgeç";
    /// and the card of a board no shuffle brings back, whose moves were refused. Pictures go to
    /// out/unity/shots/offer-...-SIZE.png, with one sheet of a size's pictures.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.OfferShots.Run
    ///            [-offerSizes 1080x2340,1080x1920,2076x2152] [-offerLevels 47,11,280]
    /// </summary>
    public static class OfferShots
    {
        /// <summary>The plaques show the piece a player with so many levels won is given (painting 5, piece 7 of 10), as the canvas draws them.</summary>
        private const int PieceAfterLevelsWon = 46;

        /// <summary>The moments after "Vazgeç", and after the ad's end, that are pictured.</summary>
        private const float CardsChangingMoment = 0.3f;
        private const float MovesCountedMoment = 0.62f;

        public static void Run()
        {
            try
            {
                string[] arguments = Environment.GetCommandLineArgs();
                ScreenShape[] screens = ListAfter("-offerSizes", arguments, "1080x2340,1080x1920,2076x2152").Select(ScreenOf).ToArray();
                int[] levelNumbers = ListAfter("-offerLevels", arguments, "47,11,280").Select(WholeNumber).ToArray();
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                PaintingGallery paintings = PaintingGallery.OfThePackedMuseum();
                PieceReward reward = PieceReward.ForWinning(new PlayerProgress(PieceAfterLevelsWon, attemptsOnLevel: 0), levels.Count, paintings.Catalogue)
                    ?? throw new InvalidOperationException($"A player with {PieceAfterLevelsWon} levels won is given no piece.");
                var brief = new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, StoryStudio.Economy.Win, reward);
                RehearsedGame[] rehearsed = levelNumbers.Select(number => RehearsedGame.Of(LevelPack.Numbered(levels, number), Expert())).ToArray();
                int pictures = 0;
                foreach (ScreenShape screen in screens)
                {
                    var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
                    var studio = new Studio(art, paintings, brief, screen, sheet);
                    foreach (RehearsedGame game in rehearsed)
                    {
                        pictures += studio.ShootTheOffers(game);
                    }

                    pictures += studio.ShootTheWayOn(rehearsed[0]);
                    pictures += studio.ShootTheRefusal();
                    StoryStudio.WriteSheet(sheet, $"offer-sheet-{screen}.png");
                }

                Debug.Log($"Offer shots: {pictures} pictures of the offer of extra moves are in {StoryStudio.ShotsFolder}.");
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException || failure is IOException || failure is LevelFormatException)
            {
                Debug.LogError($"Offer shots failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        private static IBotPolicy Expert() => BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot)
            ? bot
            : throw new InvalidOperationException($"The bots' list has no {SkillProfile.Expert.Name}.");

        private static string[] ListAfter(string name, string[] arguments, string byDefault)
        {
            int at = Array.IndexOf(arguments, name);
            return (at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : byDefault).Split(',');
        }

        private static int WholeNumber(string text) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : throw new ArgumentException($"\"{text}\" is not a whole number.");

        private static ScreenShape ScreenOf(string text)
        {
            string[] sides = text.Split('x');
            return sides.Length == 2 ? new ScreenShape(WholeNumber(sides[0]), WholeNumber(sides[1])) : throw new ArgumentException($"\"{text}\" is not a screen such as 1080x2340.");
        }

        /// <summary>The wallets the offer is pictured for: one whose coins cover the price with every ad of the day, one short of the price with none.</summary>
        private static IEnumerable<(string Name, Wallet Wallet)> Wallets()
        {
            Wallet start = StoryStudio.CanvasPlayersWallet;
            yield return ("coins-ads", start.WithCoins(new Coins(1250)));
            yield return ("short-noads", start.WithCoins(new Coins(100)).WithAds(new AdsWatched(DayStamp.Of(StoryStudio.Clock.Now), forExtraMoves: 3, forCoins: 0)));
        }

        /// <summary>What the pictures of one screen are made with.</summary>
        private sealed class Studio
        {
            private readonly ArtCatalogue _art;
            private readonly PaintingGallery _paintings;
            private readonly AttemptBrief _brief;
            private readonly ScreenShape _screen;
            private readonly ContactSheet _sheet;

            public Studio(ArtCatalogue art, PaintingGallery paintings, AttemptBrief brief, ScreenShape screen, ContactSheet sheet)
            {
                _art = art;
                _paintings = paintings;
                _brief = brief;
                _screen = screen;
                _sheet = sheet;
            }

            /// <summary>The offer on a level, at rest, for each of the wallets.</summary>
            /// <returns>How many pictures were taken.</returns>
            public int ShootTheOffers(RehearsedGame rehearsed)
            {
                int pictures = 0;
                foreach ((string name, Wallet wallet) in Wallets())
                {
                    using OfferScene scene = OutOfMoves(rehearsed, wallet);
                    Shoot(scene.Stage, $"offer-L{rehearsed.Level.Id.Value:D4}-g{rehearsed.Level.Goals.Count}-{name}");
                    pictures++;
                }

                return pictures;
            }

            /// <summary>
            /// On a level, for the player with coins and ads: the ad's card as it begins and as it ends, the moves
            /// counted up, the board going on; and, on another attempt, "Vazgeç" and the card of the loss for good.
            /// </summary>
            public int ShootTheWayOn(RehearsedGame rehearsed)
            {
                string level = $"L{rehearsed.Level.Id.Value:D4}";
                (string _, Wallet rich) = Wallets().First();
                using (OfferScene scene = OutOfMoves(rehearsed, rich))
                {
                    GameStage stage = scene.Stage;
                    ThroughTheStage.Press(stage, stage.TakeTheMovesForAnAdPixel, "the offer's Reklam izle");
                    AdvanceUntil(stage, () => stage.AdProgress > 0f, "the ad's card did not come down and begin");
                    Shoot(stage, $"offer-{level}-ad-begins");
                    AdvanceUntil(stage, () => stage.AdProgress > 0.5f, "the ad did not run");
                    Shoot(stage, $"offer-{level}-ad-half");
                    AdvanceUntil(stage, () => stage.AdProgress >= 1f, "the ad did not end");
                    Shoot(stage, $"offer-{level}-ad-ends");
                    StageSteps.Advance(stage, MovesCountedMoment);
                    Shoot(stage, $"offer-{level}-moves-counted-t{StoryStudio.Seconds(MovesCountedMoment)}");
                    StageSteps.Settle(stage);
                    Shoot(stage, $"offer-{level}-moves-taken");
                    Require(stage.Game.Status == GameStatus.Playing && stage.Plaques.MovesLeft == ExtraMovesOffer.Moves, "after the ad the board does not go on with five more moves");
                    Require(stage.TryPlay(stage.Game.FindLegalMoves()[0]), "the board refused a move after the extra moves");
                    StageSteps.Settle(stage);
                    Shoot(stage, $"offer-{level}-board-goes-on");
                }

                using (OfferScene scene = OutOfMoves(rehearsed, rich))
                {
                    GameStage stage = scene.Stage;
                    ThroughTheStage.Press(stage, stage.GiveUpPixel, "the offer's Vazgeç");
                    StageSteps.Advance(stage, CardsChangingMoment);
                    Shoot(stage, $"offer-{level}-giving-up-t{StoryStudio.Seconds(CardsChangingMoment)}");
                    StageSteps.Settle(stage);
                    Require(stage.CardTells is LevelResult.Lost { Reason: LossReason.GaveUp }, $"after Vazgeç the card tells {stage.CardTells}");
                    Shoot(stage, $"lost-{level}-gave-up");
                }

                return 8;
            }

            /// <summary>The board made by hand that no shuffle brings back: its moves refused, and the card that says so.</summary>
            public int ShootTheRefusal()
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                using var effectArt = EffectArt.Paint();
                Game game = Game.Start(HandMadeBoards.NoShuffleBringsBack(), 1UL, new GameOptions { RecordEvents = true, RecordActions = true });
                using GameStage stage = FinaleStory.OpenAtRest(game, _art, effectArt, _paintings, _screen, AttemptBrief.FirstTry(StoryStudio.Economy.Win));
                Require(stage.TryPlay(Move.Tap(HandMadeBoards.TheRocket)), "the rocket of the board made by hand was not fired");
                StageSteps.Settle(stage);
                ThroughTheStage.Press(stage, stage.TakeTheMovesForCoinsPixel, "the offer's +5 hamle");
                StageSteps.Settle(stage);
                Require(stage.CardTells is LevelResult.Lost { Reason: LossReason.ExtraMovesRefused }, $"the board made by hand was not refused its moves: the card tells {stage.CardTells}");
                Shoot(stage, "lost-refused");
                return 1;
            }

            /// <summary>
            /// A stage of the rehearsed game started with as many moves as it can have and still lose with every goal
            /// open, its last move played and the offer at rest, told the wallet.
            /// </summary>
            /// <exception cref="InvalidOperationException">No such loss exists, or the offer did not come.</exception>
            private OfferScene OutOfMoves(RehearsedGame rehearsed, Wallet wallet)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                int moves = LatestLossWithEveryGoalOpen(rehearsed);
                var effectArt = EffectArt.Paint();
                GameStage stage = FinaleStory.OpenAtRest(rehearsed.BeforeItsDecidingMoveWith(moves), _art, effectArt, _paintings, _screen, _brief);
                stage.Tell(WalletView.Of(wallet, StoryStudio.Economy.Lives, StoryStudio.Clock.Now), StoryStudio.ExtraMovesFor(wallet));
                if (!stage.TryPlay(rehearsed.DecidingMoveWith(moves)) || !OutOfMovesOffer.IsMadeIn(stage.Game))
                {
                    stage.Dispose();
                    effectArt.Dispose();
                    throw new InvalidOperationException($"Level {rehearsed.Level.Id.Value} with {moves} moves did not run out of them.");
                }

                StageSteps.Settle(stage);
                Require(stage.IsOfferingExtraMoves, $"level {rehearsed.Level.Id.Value} ran out of moves and no offer hangs");
                Debug.Log($"Offer shots: level {rehearsed.Level.Id.Value} out of moves after {moves} of the {rehearsed.MovesToWin} its win took, {stage.Game.Goals.OverallProgress:P0} of its goals done; the card says \"{ResultWords.Headline(stage.CardTells!)}\".");
                return new OfferScene(stage, effectArt);
            }

            private void Shoot(GameStage stage, string name)
            {
                StoryStudio.Shoot(stage, _screen, _sheet, $"{name}-{_screen}.png");
            }

            /// <summary>The most moves the rehearsed game can have and still run out of them with every goal open: the latest loss the offer can be made at with all its goals.</summary>
            private static int LatestLossWithEveryGoalOpen(RehearsedGame rehearsed)
            {
                for (int moves = rehearsed.MovesToWin - 1; moves >= 1; moves--)
                {
                    Game game = rehearsed.BeforeItsDecidingMoveWith(moves);
                    game.TryPlay(rehearsed.DecidingMoveWith(moves));
                    bool everyGoalOpen = Enumerable.Range(0, game.Goals.Count).All(goal => !game.Goals[goal].IsComplete);
                    if (game.Status == GameStatus.LostOutOfMoves && everyGoalOpen)
                    {
                        return moves;
                    }
                }

                throw new InvalidOperationException($"The rehearsed game of level {rehearsed.Level.Id.Value} never runs out of moves with every goal open.");
            }

            private static void AdvanceUntil(GameStage stage, Func<bool> hasHappened, string otherwise)
            {
                for (float waited = 0f; !hasHappened(); waited += StoryStudio.FrameSeconds)
                {
                    Require(waited <= 3f * SimulatedAd.Seconds, otherwise);
                    stage.Advance(StoryStudio.FrameSeconds);
                }
            }
        }

        /// <summary>A stage and the pictures it lends from, disposed of together.</summary>
        private sealed class OfferScene : IDisposable
        {
            private readonly EffectArt _effectArt;

            public OfferScene(GameStage stage, EffectArt effectArt)
            {
                Stage = stage;
                _effectArt = effectArt;
            }

            public GameStage Stage { get; }

            public void Dispose()
            {
                Stage.Dispose();
                _effectArt.Dispose();
            }
        }

        /// <exception cref="InvalidOperationException">What the pictures rely on did not hold.</exception>
        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(char.ToUpperInvariant(otherwise[0]) + otherwise.Substring(1) + ".");
            }
        }
    }
}
