#nullable enable
using System;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The entry page the game opens on (EntryTR) for the picture command, whatever the player: while the bar fills,
    /// once it is full and the line to touch has come in, and with the card the way back brings down. On the way it
    /// walks the page by touches alone, as a finger would: a touch while the bar fills leaves nothing, the card's
    /// "Vazgeç" and the way back again take the card up, its "Çık" asks for the game to close, and a touch on the
    /// ready page darkens it and leaves it. A touch that does not lead where it should fails the run.
    /// </summary>
    internal static class EntryShots
    {
        /// <summary>The bar is pictured this far filled while the game starts.</summary>
        private const float LoadingShare = 0.45f;

        /// <summary>The line to touch breathes in this long; it is pictured at the height of its breath.</summary>
        private const float BreathSeconds = 1.8f;
        private const float FullBreath = 0.99f;

        /// <returns>How many pictures were taken.</returns>
        /// <exception cref="InvalidOperationException">A touch or the way back did not do what it should.</exception>
        public static int Shoot(ScreenShape screen, Studio studio)
        {
            int pictures = 0;
            using (EntryStage entry = Open(screen, studio))
            {
                entry.Tell(LoadingShare);
                StageSteps.Advance(entry, LoadingBar.ShortestFillSeconds);
                Require(!entry.IsReady && Mathf.Approximately(entry.BarShown, LoadingShare), $"the bar shows {entry.BarShown:0.00} of a start-up {LoadingShare:0.00} done");
                Touch(entry, entry.MiddlePixel);
                StageSteps.Advance(entry, 0.5f);
                Require(!entry.HasLeft, "a touch while the bar filled left the page");
                Write(entry, "loading", screen, studio);
                pictures++;

                entry.Tell(1f);
                StageSteps.Settle(entry);
                Require(entry.IsReady, "the full bar did not make the page ready");
                for (float waited = 0f; entry.TapLineStrength < FullBreath; waited += StoryStudio.FrameSeconds)
                {
                    Require(waited < BreathSeconds, "the line to touch never breathed in full");
                    entry.Advance(StoryStudio.FrameSeconds);
                }

                Write(entry, "ready", screen, studio);
                pictures++;

                entry.GoBack();
                StageSteps.Settle(entry);
                Require(entry.QuitQuestionShown != null, "the way back brought no card down");
                Write(entry, "quit", screen, studio);
                pictures++;

                Touch(entry, entry.StayInTheGamePixel ?? throw new InvalidOperationException("the card has no \"Vazgeç\" to press"));
                StageSteps.Settle(entry);
                Require(entry.QuitQuestionShown == null, "\"Vazgeç\" did not take the card up");

                entry.GoBack();
                StageSteps.Settle(entry);
                entry.GoBack();
                StageSteps.Settle(entry);
                Require(entry.QuitQuestionShown == null, "the way back pressed again did not take the card up");

                entry.GoBack();
                StageSteps.Settle(entry);
                Touch(entry, entry.QuitTheGamePixel ?? throw new InvalidOperationException("the card has no \"Çık\" to press"));
                Require(entry.TakeTheWishToQuit() && !entry.TakeTheWishToQuit(), "\"Çık\" did not ask once for the game to close");
                Touch(entry, entry.StayInTheGamePixel ?? throw new InvalidOperationException("the card has no \"Vazgeç\" to press"));
                StageSteps.Settle(entry);

                Touch(entry, entry.MiddlePixel);
                StageSteps.Advance(entry, 1f);
                Require(entry.HasLeft, "a touch on the ready page did not leave it");
            }

            pictures += ShootTheFailedStartUp(screen, studio);
            Debug.Log($"Screen shots: the entry page on {screen} was walked by touches and the way back, and with a start-up that failed.");
            return pictures;
        }

        /// <summary>
        /// The page when a step of the start-up threw, as the game's root tells it: the bar stays where the work stopped
        /// and its line says that the game cannot open, through a change of the screen too; a touch leads nowhere, and
        /// the way back brings down the card whose "Çık" asks for the game to close.
        /// </summary>
        private static int ShootTheFailedStartUp(ScreenShape screen, Studio studio)
        {
            using EntryStage entry = Open(screen, studio);
            entry.Tell(LoadingShare);
            StageSteps.Advance(entry, LoadingBar.ShortestFillSeconds);
            entry.TellTheStartUpFailed();
            StageSteps.Advance(entry, LoadingBar.ShortestFillSeconds);
            Require(entry.LineUnderTheBar == EntryWords.CouldNotStart, $"the failed start-up's line says \"{entry.LineUnderTheBar}\"");
            Require(!entry.IsReady && Mathf.Approximately(entry.BarShown, LoadingShare), $"the failed start-up's bar shows {entry.BarShown:0.00}, or the page is ready");

            entry.Fit(screen);
            StageSteps.Advance(entry, StoryStudio.FrameSeconds);
            Require(entry.LineUnderTheBar == EntryWords.CouldNotStart, "the line went back to the share once the page was laid out anew");
            Touch(entry, entry.MiddlePixel);
            StageSteps.Advance(entry, 1f);
            Require(!entry.HasLeft, "a touch on the page of a failed start-up left it");
            Write(entry, "failed", screen, studio);

            entry.GoBack();
            StageSteps.Settle(entry);
            Require(entry.QuitQuestionShown != null, "the way back brought no card down on the page of a failed start-up");
            Touch(entry, entry.QuitTheGamePixel ?? throw new InvalidOperationException("the card has no \"Çık\" to press"));
            Require(entry.TakeTheWishToQuit(), "\"Çık\" did not ask for the game to close after a failed start-up");
            return 1;
        }

        private static EntryStage Open(ScreenShape screen, Studio studio)
        {
            // A scene of its own that is never saved: nothing of an earlier picture is left in the next.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return EntryStage.Open(studio.Art, Application.version, screen);
        }

        private static void Touch(EntryStage entry, Vector2 pixel)
        {
            entry.PointerDown(pixel);
            entry.PointerUp();
        }

        private static void Write(EntryStage entry, string moment, ScreenShape screen, Studio studio) =>
            ScreenShots.WritePicture(entry, screen, $"screen-entry-{moment}-{screen}{studio.FileNameEnding}.png");

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException($"The entry page: {otherwise}.");
            }
        }
    }
}
