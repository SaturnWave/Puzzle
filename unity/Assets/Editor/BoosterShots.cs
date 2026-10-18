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
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Pictures of the boosters used during play, each reached on the game's own session by the touches a player
    /// makes, for the owner to look at: the shelf of a new player on levels 1, 7, 12, 16, 20 and 47 (locked, opening
    /// with its word, all open); each booster that strikes armed, with its pill and the finger down on its target;
    /// each booster at work in three frames; a refused spatula (its shake and the pill that says why); the plus and
    /// the card it offers (bought, and dimmed for a player without the coins); and on level 47 a shuffle the first rule
    /// refused, before, while and after it is dealt. Pictures go to out/unity/shots/booster, each group also as a sheet.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.BoosterShots.Run
    ///            [-boosterSizes 1080x2340,1080x1920,2076x2152]
    /// Do not pass -nographics: the pictures need a graphics device.
    /// </summary>
    public static class BoosterShots
    {
        /// <summary>The pictures' folder under the shots: booster, or booster-en and booster-ko in another language than Turkish.</summary>
        private static string Subfolder => "booster" + ToolLanguage.FileNameEnding;
        private const int OpenLevel = 47;
        private static readonly int[] ShelfLevels = { 1, 7, 12, 16, 20, 47 };

        /// <summary>A booster at work is pictured at these moments after the lift that used it.</summary>
        private static readonly float[] EffectMoments = { 0.12f, 0.3f, 0.6f };

        /// <summary>A refused disc is pictured in its shake and after it, while the pill says why.</summary>
        private static readonly float[] RefusalMoments = { 0.08f, 0.5f };

        private static readonly ScreenShape[] DefaultScreens = { new ScreenShape(1080, 2340), new ScreenShape(1080, 1920), new ScreenShape(2076, 2152) };

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ScreenShape[] screens = ScreensOf(Environment.GetCommandLineArgs());
                Directory.CreateDirectory(Path.Combine(StoryStudio.ShotsFolder, Subfolder));
                var studio = new Studio();
                int pictures = 0;
                foreach (ScreenShape screen in screens)
                {
                    pictures += studio.TheShelfOnItsLevels(screen);
                    pictures += studio.EachBoosterArmed(screen);
                    pictures += studio.ARefusedSpatula(screen);
                    pictures += studio.ThePlusAndItsCard(screen);
                }

                pictures += studio.EachBoosterAtWork(screens[0]);
                pictures += studio.AShuffleTheFirstRuleRefused(screens[0]);
                Debug.Log($"Booster shots: {pictures} pictures in {Path.Combine(StoryStudio.ShotsFolder, Subfolder)}.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is ArgumentException || failure is IOException)
            {
                Debug.LogError($"Booster shots failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        private static ScreenShape[] ScreensOf(string[] arguments)
        {
            int at = Array.IndexOf(arguments, "-boosterSizes");
            if (at < 0 || at + 1 >= arguments.Length)
            {
                return DefaultScreens;
            }

            return arguments[at + 1].Split(',').Select(size =>
            {
                string[] sides = size.Split('x');
                return new ScreenShape(int.Parse(sides[0]), int.Parse(sides[1]));
            }).ToArray();
        }

        private static string Size(ScreenShape screen) => $"{screen.PixelWidth}x{screen.PixelHeight}";

        /// <summary>What the pictures are taken with: the packed levels, art and museum.</summary>
        private sealed class Studio
        {
            private readonly IReadOnlyList<LevelDefinition> _levels = LevelPack.LoadPacked();
            private readonly ArtCatalogue _art;
            private readonly PaintingGallery _paintings = PaintingGallery.OfThePackedMuseum();

            public Studio()
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                _art = art != null ? art : throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
            }

            /// <summary>The shelf of a new player who stands on each level, at rest: what is locked, what opens with its word, what is open.</summary>
            public int TheShelfOnItsLevels(ScreenShape screen)
            {
                var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
                foreach (int level in ShelfLevels)
                {
                    using GameSession session = SessionAt(level, StoryStudio.Economy.StartingWallet, screen);
                    StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/shelf-level-{level:D4}-{Size(screen)}.png");
                }

                StoryStudio.WriteSheet(sheet, $"{Subfolder}/shelf-{Size(screen)}-sheet.png");
                return ShelfLevels.Length;
            }

            /// <summary>Each booster that strikes armed by its disc, the finger down on a cell of the board: the pill, the ring, the lit target.</summary>
            public int EachBoosterArmed(ScreenShape screen)
            {
                var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
                int pictures = 0;
                foreach (BoosterKind booster in BoosterKinds.InLevel.Where(kind => kind != BoosterKind.Shuffle))
                {
                    using GameSession session = SessionAt(OpenLevel, GiftedWallet(), screen);
                    GameStage stage = session.Stage;
                    Press(session, stage.BoosterDiscPixel(booster), $"the disc of {booster.Name()}");
                    session.Advance(StoryStudio.FrameSeconds);
                    StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/armed-{booster.Name()}-{Size(screen)}.png");
                    session.PointerDown(stage.PixelOf(ATarget(stage.Game)));
                    session.Advance(StoryStudio.FrameSeconds);
                    StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/armed-{booster.Name()}-finger-down-{Size(screen)}.png");
                    session.PointerCancelled();
                    pictures += 2;
                }

                StoryStudio.WriteSheet(sheet, $"{Subfolder}/armed-{Size(screen)}-sheet.png");
                return pictures;
            }

            /// <summary>Each booster at work, at three moments after the lift that used it, and at rest after it.</summary>
            public int EachBoosterAtWork(ScreenShape screen)
            {
                int pictures = 0;
                foreach (BoosterKind booster in BoosterKinds.InLevel)
                {
                    var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
                    using GameSession session = SessionAt(OpenLevel, GiftedWallet(), screen);
                    GameStage stage = session.Stage;
                    Press(session, stage.BoosterDiscPixel(booster), $"the disc of {booster.Name()}");
                    if (booster != BoosterKind.Shuffle)
                    {
                        Press(session, stage.PixelOf(ATarget(stage.Game)), "the target");
                    }

                    float clock = 0f;
                    foreach (float moment in EffectMoments)
                    {
                        clock = StoryStudio.AdvanceTo(moment, session, clock);
                        StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/at-work-{booster.Name()}-{moment:0.00}s-{Size(screen)}.png");
                        pictures++;
                    }

                    StoryStudio.AdvanceUntilSettled(session);
                    StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/at-work-{booster.Name()}-rest-{Size(screen)}.png");
                    StoryStudio.WriteSheet(sheet, $"{Subfolder}/at-work-{booster.Name()}-{Size(screen)}-sheet.png");
                    pictures++;
                }

                return pictures;
            }

            /// <summary>A spatula aimed at an item, which it never harms: the disc shakes, and the pill says why.</summary>
            public int ARefusedSpatula(ScreenShape screen)
            {
                (int levelNumber, Position item) = ALevelWithAnItemAtTheStart();
                var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
                using GameSession session = SessionAt(levelNumber, GiftedWallet(), screen);
                GameStage stage = session.Stage;
                Press(session, stage.BoosterDiscPixel(BoosterKind.HitCell), "the spatula's disc");
                Press(session, stage.PixelOf(item), "the item");
                float clock = 0f;
                foreach (float moment in RefusalMoments)
                {
                    clock = StoryStudio.AdvanceTo(moment, session, clock);
                    StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/refused-spatula-level-{levelNumber:D4}-{moment:0.00}s-{Size(screen)}.png");
                }

                StoryStudio.WriteSheet(sheet, $"{Subfolder}/refused-{Size(screen)}-sheet.png");
                return RefusalMoments.Length;
            }

            /// <summary>The plus on the shuffle's disc (BoardTR holds none of it): the card it hangs, the booster bought, and the card of a player short of the price.</summary>
            public int ThePlusAndItsCard(ScreenShape screen)
            {
                var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
                Wallet withoutAShuffle = GiftedWallet().WithShelf(StoryStudio.CanvasWallet.Shelf);
                using (GameSession session = SessionAt(OpenLevel, withoutAShuffle.WithCoins(new Coins(1250)), screen))
                {
                    GameStage stage = session.Stage;
                    StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/plus-before-{Size(screen)}.png");
                    Press(session, stage.BoosterDiscPixel(BoosterKind.Shuffle), "the shuffle's plus");
                    StoryStudio.AdvanceUntilSettled(session);
                    StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/plus-card-{Size(screen)}.png");
                    Press(session, stage.OfferBuyPixel, "the card's green button");
                    StoryStudio.AdvanceUntilSettled(session);
                    StoryStudio.Shoot(session, screen, sheet, $"{Subfolder}/plus-bought-{Size(screen)}.png");
                }

                using (GameSession poor = SessionAt(OpenLevel, withoutAShuffle.WithCoins(new Coins(120)), screen))
                {
                    Press(poor, poor.Stage.BoosterDiscPixel(BoosterKind.Shuffle), "the shuffle's plus");
                    StoryStudio.AdvanceUntilSettled(poor);
                    StoryStudio.Shoot(poor, screen, sheet, $"{Subfolder}/plus-card-short-{Size(screen)}.png");
                }

                StoryStudio.WriteSheet(sheet, $"{Subfolder}/plus-{Size(screen)}-sheet.png");
                return 4;
            }

            /// <summary>On level 47, a board the first rule refused to shuffle: before the press, while the gems are dealt, and after.</summary>
            public int AShuffleTheFirstRuleRefused(ScreenShape screen)
            {
                LevelDefinition level = LevelPack.Numbered(_levels, OpenLevel);
                (int attempt, int moves) = FirstRuleRefusals.On(level);
                var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
                using GameSession session = SessionAt(OpenLevel, GiftedWallet(), screen, attempt);
                FirstRuleRefusals.PlayTheFirstMoves(session, moves);
                string name = $"{Subfolder}/shuffle-refused-before-attempt-{attempt}-after-{moves}-moves";
                StoryStudio.Shoot(session, screen, sheet, $"{name}-before-{Size(screen)}.png");
                Press(session, session.Stage.BoosterDiscPixel(BoosterKind.Shuffle), "the shuffle's disc");
                float clock = StoryStudio.AdvanceTo(0.45f, session, 0f);
                StoryStudio.Shoot(session, screen, sheet, $"{name}-dealt-{Size(screen)}.png");
                StoryStudio.AdvanceUntilSettled(session);
                StoryStudio.Shoot(session, screen, sheet, $"{name}-after-{Size(screen)}.png");
                StoryStudio.WriteSheet(sheet, $"{Subfolder}/shuffle-large-board-{Size(screen)}-sheet.png");
                return 3;
            }

            /// <summary>The game's own session at the board of a level, for a new player who holds this wallet, laid out and at rest.</summary>
            private GameSession SessionAt(int levelNumber, Wallet wallet, ScreenShape screen, int attemptsOnLevel = 0)
            {
                PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(_levels, levelNumber), _levels);
                var progress = new PlayerProgress(standing.LevelsWon, attemptsOnLevel, standing.Museum.Placed, wallet);
                var session = new GameSession(
                    _levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(progress), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.AtTheBoard);
                StoryStudio.AdvanceUntilSettled(session);
                return session;
            }

            /// <summary>The first level of the pack on which the spatula is open and an item lies on the starting board, and that item's cell.</summary>
            private (int Level, Position Item) ALevelWithAnItemAtTheStart()
            {
                foreach (LevelDefinition level in _levels.Where(level => level.Id.Value >= 7))
                {
                    Game game = LevelStart.Attempt(level, attempt: 0);
                    BoardShape shape = game.Board.Shape;
                    for (int y = 0; y < shape.Height; y++)
                    {
                        for (int x = 0; x < shape.Width; x++)
                        {
                            var cell = new Position(x, y);
                            if (shape.IsPlayable(cell) && game.Board.TileAt(cell).Kind == TileKind.Item)
                            {
                                return (level.Id.Value, cell);
                            }
                        }
                    }
                }

                throw new InvalidOperationException("No level of the pack starts with an item on its board.");
            }

            private static Wallet GiftedWallet() => StoryStudio.Economy.Unlocks.GivenTo(StoryStudio.Economy.StartingWallet, level: OpenLevel);

            /// <summary>A gem a little under the board's middle: a target every booster finds something on.</summary>
            private static Position ATarget(Game game)
            {
                BoardShape shape = game.Board.Shape;
                for (int y = shape.Height * 2 / 3; y > 0; y--)
                {
                    for (int x = shape.Width / 2; x < shape.Width; x++)
                    {
                        var cell = new Position(x, y);
                        if (shape.IsPlayable(cell) && game.Board.TileAt(cell).IsGem)
                        {
                            return cell;
                        }
                    }
                }

                throw new InvalidOperationException($"Level {game.Level.Id} has no gem to aim at.");
            }

            private static void Press(GameSession session, Vector2? pixel, string what)
            {
                session.PointerDown(pixel ?? throw new InvalidOperationException($"There is no {what} to press."));
                session.PointerUp();
            }
        }
    }
}
