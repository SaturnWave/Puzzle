#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Avatar;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Core.Levels;
using Puzzle.Meta;
using Puzzle.Museum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Whether every picture of the painter (§22) is loaded by the screen that shows it, and by no other, and let go
    /// of when that screen is taken down (the picture shelf's rules, A42):
    /// usage: <c>Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.AvatarCheck.Run -logFile out/unity/avatar-check.log</c>.
    /// On the 1080x2424 phone it opens the hall of a new player (he waves), turns to the shop and back, and takes the
    /// museum down; the hall of a player on level 47 (his palette); the hall of a room just restored (he cheers); a
    /// step's pill as the board, the jigsaw and the card before a level have it, with each of his looks in turn and then
    /// none; a won level's card (glad); and the jigsaw of a painting made whole by the button under the tray (he cheers
    /// in front of the tray). After each it counts the painter's pictures in memory: exactly the ones the screen shows
    /// while it stands, none once it is gone. It fails at the first count that differs, and says which.
    /// </summary>
    public static class AvatarCheck
    {
        private static readonly ScreenShape Phone = new ScreenShape(1080, 2424);

        public static void Run()
        {
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath)
                    ?? throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                var studio = new Studio(art, levels, StudioPaintings.Open(folder: null), string.Empty);
                var counts = new List<string>();

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorUtility.UnloadUnusedAssetsImmediate();
                Expect("before any screen", counts);

                LookAtTheHall(PlayerState.Parse("0"), PainterPicture.StandingWaving, studio, counts);
                LookAtTheHall(PlayerState.Parse("46+42"), PainterPicture.StandingWithPalette, studio, counts);
                LookAtTheRestoredRoom(studio, counts);
                LookAtTheLessonPill(studio, counts);
                LookAtTheWonCard(studio, counts);
                LookAtTheWholePainting(studio, counts);

                Debug.Log($"Avatar check: passed; {counts.Count} counts, each as the screen shows him: {string.Join("; ", counts)}.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException || failure is ArgumentException)
            {
                Debug.LogError($"Avatar check: FAILED; {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The hall holds the standing picture of its player and nothing else; the shop holds none; the hall again holds it again; the museum taken down holds none.</summary>
        private static void LookAtTheHall(PlayerState state, PainterPicture pose, Studio studio, List<string> counts)
        {
            using (MuseumStage museum = ScreenShots.OpenTheMuseum(new MuseumDoor(Destination.Home), state, Phone, studio))
            {
                StageSteps.Settle(museum);
                Expect($"in the hall of {state.Label}", counts, pose);
                Press(museum, museum.PixelOf(Destination.Shop));
                Require(museum.Showing == Destination.Shop, $"the bar's shop button showed {museum.Showing}");
                Expect($"in the shop of {state.Label}", counts);
                Press(museum, museum.PixelOf(Destination.Home));
                Require(museum.Showing == Destination.Home, $"the bar's hall button showed {museum.Showing}");
                Expect($"back in the hall of {state.Label}", counts, pose);
            }

            Expect($"once the museum of {state.Label} is taken down", counts);
        }

        /// <summary>The hall of the first room just restored holds the cheering painter, and lets him go with it.</summary>
        private static void LookAtTheRestoredRoom(Studio studio, List<string> counts)
        {
            using (MuseumStage museum = ScreenShots.OpenTheMuseum(MuseumDoor.AtTheRestored(new RoomNumber(1)), PlayerState.Parse("50+50"), Phone, studio))
            {
                StageSteps.Advance(museum, 1f);
                Expect("in the hall of the first room restored", counts, PainterPicture.StandingCheering);
            }

            Expect("once the restored room's hall is taken down", counts);
        }

        /// <summary>A step's pill holds the bust of the look it shows, and only that one; no look lets it go, and so does the pill taken down.</summary>
        private static void LookAtTheLessonPill(Studio studio, List<string> counts)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Lesson");
            ShelfPill pill = ShelfPill.CreateForLessons(root.transform, studio.Art, studio.Paintings.Painter);
            try
            {
                pill.Stand(0f, ShelfPill.PillEdge.Middle, 1f, ScreenLayout.WidestShelf);
                pill.Show(TutorialWords.Of(TutorialStep.Match, asksForATouch: false));
                Expect("by a step's pill before the painter is shown", counts);
                foreach (PainterMood mood in Enum.GetValues(typeof(PainterMood)))
                {
                    pill.ShowThePainter(mood);
                    Require(pill.PainterShown == mood, $"the pill shows the painter {pill.PainterShown?.ToString() ?? "not at all"}, not {mood}");
                    Expect($"by a step's pill, the painter {mood}", counts, PainterPictures.BustOf(mood));
                }

                pill.ShowThePainter(null);
                Expect("by a step's pill once the painter is taken away", counts);
                pill.ShowThePainter(PainterMood.Guiding);
            }
            finally
            {
                pill.Dispose();
                SceneObjects.Destroy(root);
            }

            Expect("once the step's pill is taken down", counts);
        }

        /// <summary>A won level's card holds the glad bust in its alcove, and lets it go with the card.</summary>
        private static void LookAtTheWonCard(Studio studio, List<string> counts)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Result");
            var won = new LevelResult.Won(levelNumber: 2, movesUsed: 9, movesLeft: 14, isLastLevel: false, reward: null, coins: new Coins(60));
            ResultPanel card = ResultPanel.Create(root.transform, studio.Art, EffectArt.Paint(), studio.Paintings, won, piecesWaiting: 0, WalletHandling.Normal);
            try
            {
                Expect("on a won level's card", counts, PainterPicture.BustJoyful);
            }
            finally
            {
                card.Dispose();
                SceneObjects.Destroy(root);
            }

            Expect("once the won level's card is taken down", counts);
        }

        /// <summary>
        /// The jigsaw of the first painting with its last pieces waiting holds no picture of the painter; made whole
        /// by the button under the tray, it holds the cheering painter; taken down, none.
        /// </summary>
        private static void LookAtTheWholePainting(Studio studio, List<string> counts)
        {
            PlayerState state = PlayerState.Parse("10+6");
            var painting = new PaintingNumber(1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using (JigsawStage jigsaw = JigsawStage.Open(
                painting, state.Progress.Museum, studio.Paintings, studio.Art, reward: new WalletLedger(StoryStudio.Economy).RewardForCompleting(state.Progress, painting)))
            {
                jigsaw.Fit(Phone);
                StageSteps.Settle(jigsaw);
                Expect("on the first painting's jigsaw with pieces waiting", counts);
                Require(JigsawShots.PlaceThemAll(jigsaw) && jigsaw.Table.IsWhole, "the button under the tray did not make the first painting whole");
                StageSteps.Settle(jigsaw);
                Expect("on the first painting's jigsaw made whole", counts, PainterPicture.StandingCheering);
            }

            Expect("once the whole painting's jigsaw is taken down", counts);
        }

        private static void Press(MuseumStage museum, Vector2 pixel)
        {
            museum.PointerDown(pixel);
            museum.PointerUp();
            StageSteps.Settle(museum);
        }

        /// <summary>The painter's pictures in memory are exactly the ones a moment of the walk shows; the count is noted.</summary>
        /// <exception cref="InvalidOperationException">Another picture is in memory, or one that is shown is not.</exception>
        private static void Expect(string moment, List<string> counts, params PainterPicture[] shown)
        {
            string[] expected = shown.Select(PainterPictures.FileNameOf).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] loaded = PicturesInMemory();
            counts.Add($"{moment}: {(loaded.Length == 0 ? "none" : string.Join(", ", loaded))}");
            Require(
                loaded.SequenceEqual(expected),
                $"{moment} the painter's pictures in memory are [{string.Join(", ", loaded)}], not [{string.Join(", ", expected)}]");
        }

        /// <summary>The painter's pictures that are in memory now, by file name.</summary>
        private static string[] PicturesInMemory() => Resources.FindObjectsOfTypeAll<Texture2D>()
            .Select(texture => AssetDatabase.GetAssetPath(texture))
            .Where(path => path.StartsWith(AvatarArtImport.AvatarFolder + "/", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }
    }
}
