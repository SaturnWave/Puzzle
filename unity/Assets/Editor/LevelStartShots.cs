#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Meta;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The way into a level by touches, as a player goes it, for the Editor's walks, stories and checks: the hall's
    /// play button brings the level's card down, the discs of the start boosters to take are pressed, and the card's
    /// green button opens the board. Each step requires that it led where it should.
    /// </summary>
    internal static class ThroughTheCard
    {
        /// <summary>No longer than this passes between a press and what it leads to.</summary>
        private const float LongestWait = 3f;

        /// <summary>
        /// From the hall, or the hall with the level's card coming down, into the level the player stands on with
        /// these start boosters: the play button where the card does not hang yet, a press on each booster's disc,
        /// and the card's green button. The session is left at the board, at rest.
        /// </summary>
        /// <exception cref="InvalidOperationException">A press did not lead where it should, or the board did not take what was chosen.</exception>
        public static void IntoTheLevel(GameSession session, IReadOnlyList<BoosterKind> boosters)
        {
            BringDownTheCard(session);
            foreach (BoosterKind booster in boosters)
            {
                Choose(session, booster);
            }

            PressPlay(session);
            StoryStudio.AdvanceUntilSettled(session);
        }

        /// <summary>
        /// The hall's play button, unless the level's card hangs already; the session is left with the card at rest in
        /// its place. Where the tutorial shows the start boosters on it (a level tried before), a touch goes on through
        /// the three, as a player's would (GameWalk.BoosterIntro walks the step itself).
        /// </summary>
        /// <exception cref="InvalidOperationException">The play button is not there, or brought no card down.</exception>
        public static void BringDownTheCard(GameSession session)
        {
            StoryStudio.AdvanceUntilSettled(session);
            MuseumStage museum = MuseumOf(session);
            if (!museum.ShowsTheStartCard)
            {
                Press(session, museum.PlayButtonPixel, "the hall's play button");
                StoryStudio.AdvanceUntilSettled(session);
            }

            GoThroughTheStartBoosters(session);
            Require(MuseumOf(session).StartCardPlayPixel != null, "the play button brought no card before the level down, or it is not at rest in front");
        }

        /// <summary>A touch on the card for each start booster the tutorial lights, until the card is the player's.</summary>
        /// <exception cref="InvalidOperationException">The step did not end after a touch for each start booster.</exception>
        private static void GoThroughTheStartBoosters(GameSession session)
        {
            for (int touches = 0; MuseumOf(session).StartBoosterLit != null; touches++)
            {
                Require(touches < LevelBrief.StartBoosters.Count, "the start boosters' step did not end after a touch for each of them");
                Press(session, MuseumOf(session).StartCardPlayPixel, "the card the start boosters are lit on");
                StoryStudio.AdvanceBy(StoryStudio.FrameSeconds, session);
                StoryStudio.AdvanceUntilSettled(session);
            }
        }

        /// <summary>A press on a start booster's disc on the card, which must choose it.</summary>
        /// <exception cref="InvalidOperationException">The disc is not there, or the press did not choose the booster.</exception>
        public static void Choose(GameSession session, BoosterKind booster)
        {
            MuseumStage museum = MuseumOf(session);
            Press(session, museum.StartCardDiscPixel(booster), $"the disc of {booster.Name()}");
            StoryStudio.AdvanceUntilSettled(session);
            Require(MuseumOf(session).StartChoice.Has(booster), $"a press on the disc of {booster.Name()} did not choose it (the choice is {MuseumOf(session).StartChoice})");
        }

        /// <summary>The card's green button: the card goes up and the level's board comes out of the dark.</summary>
        /// <exception cref="InvalidOperationException">The button is not there, or no board opened.</exception>
        public static void PressPlay(GameSession session)
        {
            Press(session, MuseumOf(session).StartCardPlayPixel, "the card's Oyna");
            WaitFor(session, () => session.Board != null, "the card's Oyna opened no board");
        }

        /// <summary>A finger comes down on a control and lifts on it.</summary>
        /// <exception cref="InvalidOperationException">The screen shows no such control to press.</exception>
        public static void Press(GameSession session, Vector2? pixel, string what)
        {
            session.PointerDown(pixel ?? throw new InvalidOperationException($"The screen does not show {what}, which was to be pressed."));
            session.PointerUp();
        }

        /// <summary>Moves the clock until something has happened that a press leads to.</summary>
        /// <exception cref="InvalidOperationException">It did not happen in time.</exception>
        public static void WaitFor(GameSession session, Func<bool> hasHappened, string otherwise)
        {
            for (float waited = 0f; !hasHappened(); waited += StoryStudio.FrameSeconds)
            {
                Require(waited <= LongestWait, $"after {LongestWait} s {otherwise}");
                session.Advance(StoryStudio.FrameSeconds);
            }
        }

        public static MuseumStage MuseumOf(GameSession session) =>
            session.Museum ?? throw new InvalidOperationException("The session is not in the museum, where the way into a level begins.");

        /// <exception cref="InvalidOperationException">What the game relies on did not hold; the message says what happened instead.</exception>
        public static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(char.ToUpperInvariant(otherwise[0]) + otherwise.Substring(1) + ".");
            }
        }
    }

    /// <summary>What the picture command shows of the card before a level. A closed set.</summary>
    internal enum StartCardMoment
    {
        /// <summary>The card as the play button brings it down, nothing chosen.</summary>
        AsItComes,

        /// <summary>Every start booster the player holds chosen: rings, ticks, and the moves the extra moves add.</summary>
        Chosen,

        /// <summary>The card that offers the first start booster the player holds none of, in front of the level's card.</summary>
        GetMore,
    }

    /// <summary>
    /// Pictures of the card before a level and of the board it opens, for <see cref="ScreenShots"/>, each reached on
    /// the game's own session by the touches a player makes: the card as it comes, with boosters chosen, the card
    /// that offers a booster held none of, and the board's entrance with each start booster and with all three.
    /// </summary>
    internal static class LevelStartShots
    {
        /// <summary>The board's entrance is pictured at these moments after it opened, and at rest: the gems' wave, the first special, the next ones.</summary>
        private static readonly float[] EntranceMoments = { 0.3f, 0.46f, 0.64f, 0.82f };

        /// <summary>The start boosters each board's entrance is pictured with: each alone, and all three.</summary>
        private static readonly BoosterKind[][] EntranceChoices =
        {
            new[] { BoosterKind.StartRocketAndBomb },
            new[] { BoosterKind.StartColorBomb },
            new[] { BoosterKind.StartExtraMoves },
            LevelBrief.StartBoosters.ToArray(),
        };

        /// <returns>How many pictures were taken: none where the state has nothing to show at the moment (no booster to choose, or none to buy).</returns>
        public static int ShootTheCard(StartCardMoment moment, PlayerState state, ScreenShape screen, Studio studio)
        {
            using GameSession session = InTheHall(state, screen, studio);
            if (!state.Wallet.HasALife)
            {
                Debug.Log($"Screen shots: the player {state.Label} has no life; the play button brings the lives card, not the level's card.");
                return 0;
            }

            ThroughTheCard.BringDownTheCard(session);
            BoosterShelf held = state.Progress.Wallet.Shelf;
            switch (moment)
            {
                case StartCardMoment.AsItComes:
                    ScreenShots.WritePicture(session.Front, screen, studio.FileName("levelstart", state, screen));
                    return 1;
                case StartCardMoment.Chosen:
                    BoosterKind[] owned = LevelBrief.StartBoosters.Where(held.Has).ToArray();
                    if (owned.Length == 0)
                    {
                        Debug.Log($"Screen shots: the player {state.Label} holds no start booster to choose.");
                        return 0;
                    }

                    foreach (BoosterKind booster in owned)
                    {
                        ThroughTheCard.Choose(session, booster);
                    }

                    ScreenShots.WritePicture(session.Front, screen, studio.FileName("levelstart-chosen", state, screen));
                    return 1;
                case StartCardMoment.GetMore:
                    BoosterKind[] notHeld = LevelBrief.StartBoosters.Where(booster => !held.Has(booster)).ToArray();
                    if (notHeld.Length == 0)
                    {
                        Debug.Log($"Screen shots: the player {state.Label} holds every start booster; no card offers one.");
                        return 0;
                    }

                    BoosterKind missing = notHeld[0];
                    ThroughTheCard.Press(session, ThroughTheCard.MuseumOf(session).StartCardDiscPixel(missing), $"the plus of {missing.Name()}");
                    StoryStudio.AdvanceUntilSettled(session);
                    ThroughTheCard.Require(ThroughTheCard.MuseumOf(session).OffersABooster == missing, $"the plus of {missing.Name()} brought no card that offers it");
                    ScreenShots.WritePicture(session.Front, screen, studio.FileName("getmore", state, screen));
                    return 1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(moment), moment, "A moment of the card the picture command does not know.");
            }
        }

        /// <summary>
        /// The board of the level the player stands on as it comes in with each start booster and with all three,
        /// gone into by touches: a few moments of its entrance, and at rest; one sheet of them all.
        /// </summary>
        /// <returns>How many pictures were taken; a choice the player's shelf cannot pay for is left out.</returns>
        public static int ShootTheEntrances(PlayerState state, ScreenShape screen, Studio studio)
        {
            if (!state.Wallet.HasALife)
            {
                Debug.Log($"Screen shots: the player {state.Label} has no life to open a level with.");
                return 0;
            }

            var sheet = new ContactSheet(StoryStudio.SheetShrinksBy, mostInARow: EntranceMoments.Length + 1);
            int pictures = 0;
            foreach (BoosterKind[] choice in EntranceChoices)
            {
                if (!choice.All(state.Progress.Wallet.Shelf.Has))
                {
                    Debug.Log($"Screen shots: the player {state.Label} does not hold {string.Join(" and ", choice.Select(BoosterKinds.Name))}.");
                    continue;
                }

                pictures += ShootTheEntrance(choice, state, screen, studio, sheet);
            }

            if (!sheet.IsEmpty)
            {
                StoryStudio.WriteSheet(sheet, studio.FileName("entrance-sheet", state, screen));
            }

            return pictures;
        }

        private static int ShootTheEntrance(BoosterKind[] choice, PlayerState state, ScreenShape screen, Studio studio, ContactSheet sheet)
        {
            using GameSession session = InTheHall(state, screen, studio);
            ThroughTheCard.BringDownTheCard(session);
            foreach (BoosterKind booster in choice)
            {
                ThroughTheCard.Choose(session, booster);
            }

            ThroughTheCard.PressPlay(session);
            string[] taken = session.Stage.Game.BoostersUsed.ToArray();
            ThroughTheCard.Require(
                taken.SequenceEqual(choice.Select(BoosterKinds.Name)),
                $"the board took [{string.Join(", ", taken)}] of the boosters chosen, [{string.Join(", ", choice.Select(BoosterKinds.Name))}]");

            string name = choice.Length == LevelBrief.StartBoosters.Count ? "all" : choice[0].Name();
            float clock = 0f;
            foreach (float moment in EntranceMoments)
            {
                clock = StoryStudio.AdvanceTo(moment, session, clock);
                StoryStudio.Shoot(session, screen, sheet, studio.FileName($"entrance-{name}-t{StoryStudio.Seconds(moment)}", state, screen));
            }

            StoryStudio.AdvanceUntilSettled(session);
            StoryStudio.Shoot(session, screen, sheet, studio.FileName($"entrance-{name}", state, screen));
            Debug.Log($"Screen shots: level {session.Stage.Game.Level.Id} opened with [{string.Join(", ", taken)}], {session.Stage.Game.MovesLeft} moves.");
            return EntranceMoments.Length + 1;
        }

        /// <summary>The game's own session in the hall, for a player as the picture command is told of one, in a scene of its own.</summary>
        private static GameSession InTheHall(PlayerState state, ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(state.Progress),
                StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
        }
    }
}
