#nullable enable
using System;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Flow;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// A walk through the game for <see cref="ScreenShots"/>, on the game's own session and by touches alone, with
    /// a picture at every stop: the hall, the collection and the map (a page turning is pictured too), the play
    /// button and the hall going dark, the board, the round button that asks whether to leave (answered first
    /// with "no" by the device's way back, then with "yes", which leads to the hall), the board again, a whole
    /// level played by the expert bot, its card; from a won level's card the blue button to the painting's jigsaw,
    /// where the piece just won is carried to its place by a finger and what else waits is placed by the blue
    /// button, then back to the card and on to the hall (through the restored room's celebration when the jigsaw
    /// completed a room), to the collection once more, and from there to the hall by the device's way back. The pictures are named <c>walk-&lt;state&gt;-&lt;size&gt;-NN-&lt;what&gt;.png</c>,
    /// with a sheet of all of them. The walk fails when a touch does not lead where it should, or when what was
    /// placed is not in the player's progress.
    /// </summary>
    internal static class GameWalk
    {
        /// <summary>The bot's choices are random where several moves are equally good; the walk always makes the same ones.</summary>
        private const ulong BotSeed = 20261007;
        private const ulong BotStream = 13;

        /// <summary>The moments of a page turn that are pictured, in seconds from the press.</summary>
        private static readonly float[] TurnMoments = { 0.1f, 0.2f };

        /// <summary>A held button is pictured this long after the finger came down; a room that goes dark or light, this long into it.</summary>
        private const float PressedMoment = 2f * StoryStudio.FrameSeconds;
        private const float DarkMoment = 0.1f;

        /// <summary>A piece that dropped in is pictured this long after; flying pieces this long after the button; a celebration this far into it.</summary>
        private const float DroppedMoment = 0.05f;
        private const float FlyingMoment = 0.32f;
        private const float CelebrationMoment = 0.28f;

        /// <summary>A restored room is pictured this long after the hall opened on it, while its light runs, and this much later, at rest.</summary>
        private const float RestoredRoomMoment = 0.85f;
        private const float RestoredRoomRests = 1.6f;

        /// <summary>No longer than this passes between a press that leaves a stage and the next stage.</summary>
        private const float LongestLeave = 2f;

        public static void Run(PlayerState state, ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, new ProgressInMemory(state.Progress), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-{state.Label}-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            int levelToPlay = studio.Levels[state.Progress.LevelIndexIn(studio.Levels.Count)].Id.Value;

            reel.Settle();
            reel.Shoot("home");
            TurnThePage(reel, Destination.Collection, picturesTheTurn: false);
            reel.Shoot("collection");
            TurnThePage(reel, Destination.Map, picturesTheTurn: true);
            reel.Shoot("map");
            TurnThePage(reel, Destination.Home, picturesTheTurn: false);

            OpenTheLevel(reel, levelToPlay, pictures: true);
            LeaveTheLevelAndComeBack(reel, levelToPlay);
            GameStatus ending = PlayTheLevel(reel);
            bool hadPiecesToPlace = session.Stage.PlaceButtonPixel != null;
            string atTheJigsaw = hadPiecesToPlace ? PlaceWhatWasWon(reel) : "no piece to place";
            if (session.Museum == null)
            {
                // A card that came back from the jigsaw is left by the shelf's round button, any other by its own teal one: both ways are walked.
                Vector2? wayOut = hadPiecesToPlace ? session.Stage.LeaveButtonPixel : session.Stage.MuseumButtonPixel;
                Press(session, wayOut, hadPiecesToPlace ? "the shelf's round button under the card" : "the card's museum button", reel, "museum-pressed");
                WaitFor(session, () => session.Museum != null, "the way from the card to the museum opened no museum");
                reel.Advance(DarkMoment);
                reel.Shoot("hall-comes");
            }

            reel.Settle();
            reel.Shoot("home-again");
            if (MuseumOf(session).Showing != Destination.Home)
            {
                throw new InvalidOperationException($"From a result's card the museum opened on {MuseumOf(session).Showing}, not on its home.");
            }

            TurnThePage(reel, Destination.Collection, picturesTheTurn: false);
            reel.Shoot("collection-again");
            session.GoBack();
            reel.Settle();
            if (MuseumOf(session).Showing != Destination.Home)
            {
                throw new InvalidOperationException($"The device's way back left the museum on {MuseumOf(session).Showing}, not on its home.");
            }

            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: hall, collection, map, hall, level {levelToPlay} ({ending}), {atTheJigsaw}, hall, collection; the player now stands at \"{session.Progress}\".");
        }

        /// <summary>From the hall into the level the player stands on, by the play button.</summary>
        private static void OpenTheLevel(Reel reel, int levelToPlay, bool pictures)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).PlayButtonPixel, "the play button", reel, pictures ? "play-pressed" : null);
            if (pictures)
            {
                reel.Advance(DarkMoment);
                reel.Shoot("hall-goes-dark");
            }

            WaitFor(session, () => session.Board != null, "the play button opened no board");
            if (session.Stage.Game.Level.Id.Value != levelToPlay)
            {
                throw new InvalidOperationException($"The play button opened level {session.Stage.Game.Level.Id.Value}, not level {levelToPlay}, where the player stands.");
            }

            if (pictures)
            {
                reel.Advance(DarkMoment);
                reel.Shoot("board-comes");
            }

            reel.Settle();
        }

        /// <summary>
        /// The shelf's round button asks whether to leave; the device's way back says no and the game goes on; asked
        /// again, the card's teal button leads to the hall, with the player's progress as it was (no move was
        /// played, so the attempt does not count); the play button then opens the same level again.
        /// </summary>
        private static void LeaveTheLevelAndComeBack(Reel reel, int levelToPlay)
        {
            GameSession session = reel.Session;
            PlayerProgress before = session.Progress;
            reel.Shoot("board");
            Press(session, session.Stage.LeaveButtonPixel, "the shelf's round button", reel, "leave-pressed");
            reel.Settle();
            reel.Shoot("leave-asked");
            if (!session.Stage.IsAskingWhetherToLeave)
            {
                throw new InvalidOperationException("The shelf's round button did not ask whether to leave the level.");
            }

            session.GoBack();
            reel.Settle();
            if (session.Board == null || session.Stage.IsAskingWhetherToLeave || session.Stage.LeaveButtonPixel == null)
            {
                throw new InvalidOperationException("The device's way back did not take the leaving card away and give the board back.");
            }

            session.GoBack();
            reel.Settle();
            Press(session, session.Stage.MuseumButtonPixel, "the leaving card's museum button", reel, pressedShot: null);
            WaitFor(session, () => session.Museum != null, "the leaving card's museum button opened no museum");
            reel.Settle();
            reel.Shoot("left-for-the-hall");
            if (!session.Progress.Equals(before))
            {
                throw new InvalidOperationException($"Leaving a level in which no move was played changed the progress from \"{before}\" to \"{session.Progress}\".");
            }

            OpenTheLevel(reel, levelToPlay, pictures: false);
        }

        /// <summary>Through the open level to its card.</summary>
        private static GameStatus PlayTheLevel(Reel reel)
        {
            GameSession session = reel.Session;
            FlowStory.PlayTheGame(session, Expert(), new Rng(BotSeed, BotStream));
            GameStatus ending = session.Stage.Game.Status;
            reel.Settle();
            reel.Shoot($"card-{ending.ToString().ToLowerInvariant()}");
            return ending;
        }

        /// <summary>
        /// From a won level's card to the jigsaw and back: the first piece of the tray is carried to its place by a
        /// finger, the blue button places what else waits, and the round button leads back to the card, which then
        /// has its button to the museum.
        /// </summary>
        /// <returns>What happened at the jigsaw, for the walk's line in the log.</returns>
        private static string PlaceWhatWasWon(Reel reel)
        {
            GameSession session = reel.Session;
            Press(session, session.Stage.PlaceButtonPixel, "the card's jigsaw button", reel, "place-pressed");
            WaitFor(session, () => session.Jigsaw != null, "the card's jigsaw button opened no jigsaw");
            reel.Advance(DarkMoment);
            reel.Shoot("jigsaw-comes");
            reel.Settle();
            reel.Shoot("jigsaw");

            PaintingNumber painting = JigsawOf(session).Table.Painting;
            int waited = JigsawOf(session).Table.Waiting.Count;
            CarryThePieceHome(reel);
            if (JigsawOf(session).Table.Waiting.Count > 0)
            {
                reel.Settle();
                reel.Shoot("piece-placed");
                Press(session, JigsawOf(session).TrayButtonPixel, "the button that places every piece", reel, pressedShot: null);
                reel.Advance(FlyingMoment);
                reel.Shoot("pieces-fly");
            }

            PictureTheEndOfThePlacing(reel);
            if (session.Progress.Museum.WaitingIn(painting).Count != 0 || session.HasUnsavedProgress)
            {
                throw new InvalidOperationException($"After the jigsaw of painting {painting} the progress is \"{session.Progress}\": pieces still wait, or what was placed is not saved.");
            }

            bool restoredTheRoom = RoomWall.Of(painting.Room, session.Progress.Museum, reel.Catalogue).IsRestored;
            Press(session, JigsawOf(session).BackButtonPixel, "the jigsaw's back button", reel, "back-pressed");
            string restored = session.Progress.Museum.IsComplete(painting) ? ", which restored it" : string.Empty;
            if (restoredTheRoom)
            {
                WalkThroughTheRestoredRoom(reel);
                return $"{waited} piece(s) of painting {painting} placed{restored} and room {painting.Room} with it";
            }

            WaitFor(session, () => session.Board != null, "the jigsaw's back button did not lead back to the card");
            reel.Advance(Tempo.CardAfterDark + Tempo.CardComesDown / 2f);
            reel.Shoot("card-comes-back");
            reel.Settle();
            reel.Shoot("card-again");
            return $"{waited} piece(s) of painting {painting} placed{restored}";
        }

        /// <summary>
        /// The jigsaw that completed a room leads to the hall, which shows the room restored: pictured in its
        /// celebration and at rest; its green button then leads on to the hall as the player's progress has it.
        /// </summary>
        private static void WalkThroughTheRestoredRoom(Reel reel)
        {
            GameSession session = reel.Session;
            WaitFor(session, () => session.Museum != null, "the jigsaw that restored a room did not lead to the hall");
            reel.Advance(RestoredRoomMoment);
            reel.Shoot("room-restored");
            reel.Advance(RestoredRoomRests);
            reel.Shoot("room-restored-rest");
            Press(session, MuseumOf(session).GoOnButtonPixel, "the restored room's button that leads on", reel, pressedShot: null);
            reel.Settle();
            if (MuseumOf(session).GoOnButtonPixel != null || MuseumOf(session).PlayButtonPixel == null)
            {
                throw new InvalidOperationException("The restored room's button did not lead on to the hall with its play button.");
            }
        }

        /// <summary>A finger takes the tray's first piece, carries it over the painting and lets it go on its place.</summary>
        private static void CarryThePieceHome(Reel reel)
        {
            GameSession session = reel.Session;
            JigsawStage jigsaw = JigsawOf(session);
            int piece = jigsaw.PiecesInTheTray.Count > 0
                ? jigsaw.PiecesInTheTray[0]
                : throw new InvalidOperationException("A card led to a jigsaw whose tray shows no piece.");
            Vector2 inTheTray = PixelOrFail(jigsaw.PixelOfTheTrayPiece(piece), "the piece in the tray");
            Vector2 onItsPlace = PixelOrFail(jigsaw.PixelToLetGoAt(piece, Vector2.zero), "the piece's place");

            session.PointerDown(inTheTray);
            reel.Settle();
            session.PointerMoved(Vector2.Lerp(inTheTray, onItsPlace, 0.55f));
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Shoot("piece-carried");
            session.PointerMoved(onItsPlace);
            session.PointerUp();
            int? next = jigsaw.PiecesInTheTray.Where(shown => shown != piece).Select(shown => (int?)shown).FirstOrDefault();
            if (next is int taken)
            {
                // The finger does not wait for the piece it let go: it comes down on the next one in the same moment.
                session.PointerDown(PixelOrFail(jigsaw.PixelOfTheTrayPiece(taken), "the next piece in the tray"));
            }

            reel.Advance(Tempo.PieceDropsIn + DroppedMoment);
            reel.Shoot("piece-dropped");
            if (!jigsaw.Table.InPlace.Contains(piece))
            {
                throw new InvalidOperationException($"Piece {piece} was let go on its place and did not drop in.");
            }

            if (next is int carried)
            {
                CarryHomeThePieceTakenAtOnce(reel, jigsaw, carried);
            }
        }

        /// <summary>The piece a finger took while the one before it was still dropping in must be in the hand; it is carried to its place like the first.</summary>
        private static void CarryHomeThePieceTakenAtOnce(Reel reel, JigsawStage jigsaw, int piece)
        {
            GameSession session = reel.Session;
            if (jigsaw.PieceInTheHand != piece)
            {
                throw new InvalidOperationException($"A finger that came down on piece {piece} while the piece before it dropped in did not pick it up.");
            }

            session.PointerMoved(PixelOrFail(jigsaw.PixelToLetGoAt(piece, Vector2.zero), "the next piece's place"));
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Shoot("next-piece-carried-at-once");
            session.PointerUp();
            reel.Advance(Tempo.PieceDropsIn + DroppedMoment);
            if (!jigsaw.Table.InPlace.Contains(piece))
            {
                throw new InvalidOperationException($"Piece {piece}, taken while the piece before it dropped in, was let go on its place and did not drop in.");
            }
        }

        /// <summary>Every piece is in place: a painting that is whole now is pictured in its celebration, and then, like any other, at rest.</summary>
        private static void PictureTheEndOfThePlacing(Reel reel)
        {
            JigsawStage jigsaw = JigsawOf(reel.Session);
            WaitFor(reel.Session, () => jigsaw.Table.Waiting.Count == 0, "the jigsaw's pieces were not all placed");
            if (jigsaw.Table.IsWhole && !jigsaw.IsSettled)
            {
                reel.Advance(Tempo.PauseBeforeRestored + CelebrationMoment);
                reel.Shoot("painting-restored");
            }

            reel.Settle();
            reel.Shoot("jigsaw-done");
        }

        /// <summary>A finger presses a place on the bar and lifts; the page turns, with pictures of the turn if asked.</summary>
        private static void TurnThePage(Reel reel, Destination to, bool picturesTheTurn)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).PixelOf(to), $"the bar's place for {to}", reel, pressedShot: null);
            if (picturesTheTurn)
            {
                float clock = 0f;
                foreach (float moment in TurnMoments)
                {
                    clock = reel.AdvanceTo(moment, clock);
                    reel.Shoot($"turn-t{StoryStudio.Seconds(moment)}");
                }
            }

            reel.Settle();
            if (MuseumOf(session).Showing != to)
            {
                throw new InvalidOperationException($"A press on the bar's place for {to} left the museum on {MuseumOf(session).Showing}.");
            }
        }

        /// <summary>A finger comes down on a control and lifts on it; the held control is pictured if a name for that picture is given.</summary>
        /// <exception cref="InvalidOperationException">The screen shows no such control to press.</exception>
        private static void Press(GameSession session, Vector2? pixel, string what, Reel reel, string? pressedShot)
        {
            session.PointerDown(PixelOrFail(pixel, what));
            if (pressedShot != null)
            {
                reel.Advance(PressedMoment);
                reel.Shoot(pressedShot);
            }

            session.PointerUp();
        }

        /// <summary>Moves the clock until something has happened that a press leads to.</summary>
        /// <exception cref="InvalidOperationException">It did not happen in time: the press led nowhere.</exception>
        private static void WaitFor(GameSession session, Func<bool> hasHappened, string otherwise)
        {
            for (float waited = 0f; !hasHappened(); waited += StoryStudio.FrameSeconds)
            {
                if (waited > LongestLeave)
                {
                    throw new InvalidOperationException($"After {LongestLeave} s {otherwise}.");
                }

                session.Advance(StoryStudio.FrameSeconds);
            }
        }

        private static Vector2 PixelOrFail(Vector2? pixel, string what) =>
            pixel ?? throw new InvalidOperationException($"The screen does not show {what}, which the walk was to touch.");

        private static MuseumStage MuseumOf(GameSession session) =>
            session.Museum ?? throw new InvalidOperationException("The session is not in the museum, where the walk expected it.");

        private static JigsawStage JigsawOf(GameSession session) =>
            session.Jigsaw ?? throw new InvalidOperationException("The session is not at a jigsaw, where the walk expected it.");

        private static IBotPolicy Expert() => BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot)
            ? bot
            : throw new InvalidOperationException("The bots' list has no expert.");

        /// <summary>The pictures of one walk: numbered in the order they are taken, and gathered on one sheet.</summary>
        private sealed class Reel
        {
            private readonly ScreenShape _screen;
            private readonly ContactSheet _sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
            private int _shots;

            public Reel(GameSession session, ScreenShape screen, string name, MuseumCatalogue catalogue)
            {
                Session = session;
                _screen = screen;
                Name = name;
                Catalogue = catalogue;
                TakeAwayTheReelBefore(name);
            }

            public GameSession Session { get; }

            /// <summary>What hangs where in the museum the walk goes through.</summary>
            public MuseumCatalogue Catalogue { get; }

            public string Name { get; }

            /// <summary>
            /// The pictures of an earlier walk of the same name are this walk's to replace: a walk that has other
            /// stops numbers its pictures differently, and the old ones would be left standing among the new.
            /// </summary>
            private static void TakeAwayTheReelBefore(string name)
            {
                if (!Directory.Exists(StoryStudio.ShotsFolder))
                {
                    return;
                }

                foreach (string earlier in Directory.GetFiles(StoryStudio.ShotsFolder, $"{name}-??-*.png"))
                {
                    File.Delete(earlier);
                }
            }

            public void Advance(float seconds) => StoryStudio.AdvanceBy(seconds, Session);

            public float AdvanceTo(float seconds, float clock) => StoryStudio.AdvanceTo(seconds, Session, clock);

            public void Settle() => StoryStudio.AdvanceUntilSettled(Session);

            public void Shoot(string what)
            {
                _shots++;
                StoryStudio.Shoot(Session, _screen, _sheet, $"{Name}-{_shots:D2}-{what}.png");
            }

            public void WriteSheet() => StoryStudio.WriteSheet(_sheet, $"{Name}-sheet.png");
        }
    }
}
