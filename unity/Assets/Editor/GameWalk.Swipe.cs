#nullable enable
using System;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Museum;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the swipes, once on each screen, on the game's own session and by touches alone, for a player who
    /// stands in the third room with every piece won in place, so the two rooms before it hang restored: in the hall
    /// a slow swipe to the left drags the hall out and the map in beside it (pictured with the finger down), and
    /// settles on the map; there, past the bar's last page, the map only stretches (pictured) and springs back; a
    /// short slow drag springs back too, and a short flick to the right turns to the hall. In the hall a drag that
    /// goes up first and then as far sideways as a swipe, and a swipe that comes down at the screen's left edge, turn
    /// nothing; a swipe to the right turns to the collection. There a swipe to the right on the plaque turns to the
    /// room before (pictured with the finger down) and leaves the page where it is, and a swipe to the right anywhere
    /// else, beginning on a restored painting, turns to the shop and brings no painting forward. In the shop, with
    /// the settings card down, a swipe from the bar under its dark moves nothing and presses nothing (pictured);
    /// once the card is closed, the bar's place for the hall still leads there (<c>walk-swipe-...</c>).
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>The level the swipes' player stands on: the first of the third room, with the two rooms before it open.</summary>
        private const int SwipedLevel = 2 * MuseumPlan.LevelsPerRoom + 1;

        /// <summary>A slow swipe and a slow drag take this long; a flick this long.</summary>
        private const float SlowSwipe = 0.5f;
        private const float SlowDrag = 0.6f;
        private const float FlickSeconds = 0.06f;

        /// <summary>How far a swipe, a short drag and a flick go, as shares of the screen's width.</summary>
        private const float SwipeShare = 0.55f;
        private const float ShortDragShare = 0.15f;
        private const float FlickShare = 0.12f;

        public static void RunTheSwipeWalk(ScreenShape screen, Studio studio)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, SwipedLevel), studio.Levels);
            PlayerProgress placed = standing.WithWaitingPiecesPlaced(standing.Museum.PiecesWaiting);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(placed), StoryStudio.AttemptsKeptInMemory(),
                screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-swipe-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            var finger = new SwipingFinger(reel, ScreenView.Of(screen));
            reel.Settle();
            RequireShowing(session, Destination.Home, "the walk begins");
            reel.Shoot("hall");

            SwipeThroughThePages(finger);
            RoomsTurned room = SwipeTheRoomsAndThePage(finger);
            NothingMovesUnderACard(finger);
            reel.WriteSheet();
            Debug.Log(
                $"Screen shots: walk {reel.Name}: hall to map by a swipe, the map stretched past the bar's end and sprung back, a short drag sprung back, "
                + $"a flick to the hall, an upright drag and one from the edge turned nothing, the collection's plaque turned room {room.Before} to {room.After}, "
                + "a swipe on a painting turned to the shop, a swipe under the settings card moved nothing, the bar still leads to the hall.");
        }

        /// <summary>Hall to map by a slow swipe; past the map the page stretches and springs back; a short drag springs back; a flick turns to the hall; an upright drag and one from the edge turn nothing.</summary>
        private static void SwipeThroughThePages(SwipingFinger finger)
        {
            GameSession session = finger.Reel.Session;
            finger.Swipe(0.3f, 0f, -SwipeShare, 0f, SlowSwipe, heldShot: "hall-to-map-held");
            RequireShowing(session, Destination.Map, "a slow swipe to the left from the hall");
            ThroughTheCard.Require(MuseumOf(session).PlayerLeftFor == null && !MuseumOf(session).ShowsTheStartCard, "a swipe that began on the hall pressed what it began on");
            finger.Reel.Shoot("map");

            finger.Swipe(0.3f, 0f, -SwipeShare, 0f, SlowSwipe, heldShot: "map-stretched-held");
            RequireShowing(session, Destination.Map, "a swipe past the bar's last page");
            finger.Reel.Shoot("map-sprung-back");

            finger.Swipe(-0.2f, 0f, ShortDragShare, 0f, SlowDrag, heldShot: null);
            RequireShowing(session, Destination.Map, "a short slow drag to the right");

            finger.Swipe(-0.1f, 0f, FlickShare, 0f, FlickSeconds, heldShot: null);
            RequireShowing(session, Destination.Home, "a short flick to the right from the map");

            // Up first, then as far to the left as the swipe that turned the hall to the map.
            finger.SwipeAlong(finger.PixelAt(0.3f, 0f), heldShot: null, (0f, 0.15f, 0.2f), (-SwipeShare, 0f, SlowSwipe));
            RequireShowing(session, Destination.Home, "a drag that went up first");
            ThroughTheCard.Require(MuseumOf(session).PlayerLeftFor == null && !MuseumOf(session).ShowsTheStartCard, "a drag that went up pressed what it began on");

            float atTheLeftEdge = -0.5f + (PageSwipe.BackGestureEdge / 2f) / finger.View.Width;
            finger.Swipe(atTheLeftEdge, 0f, SwipeShare, 0f, SlowSwipe, heldShot: null);
            RequireShowing(session, Destination.Home, "a swipe that came down at the screen's left edge, where the back gesture begins");
        }

        /// <summary>To the collection; a swipe on its plaque turns the room and leaves the page; one that begins on a painting turns to the shop and brings nothing forward.</summary>
        private static RoomsTurned SwipeTheRoomsAndThePage(SwipingFinger finger)
        {
            GameSession session = finger.Reel.Session;
            finger.Swipe(-0.3f, 0f, SwipeShare, 0f, SlowSwipe, heldShot: null);
            RequireShowing(session, Destination.Collection, "a slow swipe to the right from the hall");
            finger.Reel.Shoot("collection");

            RoomNumber before = MuseumOf(session).Door.CollectionRoom ?? throw new InvalidOperationException("The collection names no room.");
            Vector2 plaque = PixelOrFail(MuseumOf(session).PlaquePixel, "the collection's plaque");
            finger.SwipeFromPixel(plaque, 0.45f, 0f, SlowSwipe, heldShot: "plaque-held");
            RequireShowing(session, Destination.Collection, "a swipe on the collection's plaque");
            RoomNumber after = MuseumOf(session).Door.CollectionRoom ?? throw new InvalidOperationException("The collection names no room.");
            ThroughTheCard.Require(after.Value == before.Value - 1, $"a swipe to the right on the plaque turned room {before} to {after}, not to the room before");
            finger.Reel.Shoot("room-before");

            PaintingNumber painting = after.Paintings.First();
            Vector2 onAPainting = PixelOrFail(MuseumOf(session).PixelOfExhibit(painting), $"painting {painting} on the wall");
            finger.SwipeFromPixel(onAPainting, SwipeShare, 0f, SlowSwipe, heldShot: null);
            RequireShowing(session, Destination.Shop, "a swipe to the right that began on a painting of the collection");
            ThroughTheCard.Require(
                MuseumOf(session).PaintingViewed == null && MuseumOf(session).PlayerLeftFor == null,
                "a swipe that began on a painting brought it forward or led to its jigsaw");
            finger.Reel.Shoot("shop");
            return new RoomsTurned(before, after);
        }

        /// <summary>With the settings card down over the shop, a swipe moves nothing; closed, the bar's place for the hall leads there.</summary>
        private static void NothingMovesUnderACard(SwipingFinger finger)
        {
            GameSession session = finger.Reel.Session;
            Press(session, MuseumOf(session).GearPixel, "the shop's gear", finger.Reel, pressedShot: null);
            finger.Reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).ShowsTheSettings, "the gear did not bring the settings card down");

            // From the bar under the card's dark, where a finger on the bare shop would swipe to the collection.
            finger.SwipeFromPixel(MuseumOf(session).PixelOf(Destination.Home), -SwipeShare, 0f, SlowSwipe, heldShot: "card-swipe-held");
            RequireShowing(session, Destination.Shop, "a swipe while the settings card hung");
            ThroughTheCard.Require(
                MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings,
                $"a swipe under the settings card left {MuseumOf(session).SettingsCardInFront?.ToString() ?? "no card"} in front");

            Press(session, MuseumOf(session).CardClosePixel, "the settings card's round button", finger.Reel, pressedShot: null);
            finger.Reel.Settle();
            TurnThePage(finger.Reel, Destination.Home, picturesTheTurn: false);
            finger.Reel.Shoot("hall-by-the-bar");
        }

        private static void RequireShowing(GameSession session, Destination expected, string after) =>
            ThroughTheCard.Require(MuseumOf(session).Showing == expected && MuseumOf(session).IsSettled, $"after {after} the museum shows {MuseumOf(session).Showing}, not {expected}, or has not settled");

        /// <summary>The room the collection showed before the plaque's swipe, and after it.</summary>
        private readonly struct RoomsTurned
        {
            public RoomsTurned(RoomNumber before, RoomNumber after)
            {
                Before = before;
                After = after;
            }

            public RoomNumber Before { get; }

            public RoomNumber After { get; }
        }

        /// <summary>
        /// A finger that swipes on the screen: it comes down, slides a step a frame as the clock runs, and lifts; a
        /// picture may be taken while it is still down at the end of its way. Places are told as shares of the
        /// screen's width and height from its middle, distances as shares of its width.
        /// </summary>
        private sealed class SwipingFinger
        {
            public SwipingFinger(Reel reel, ScreenView view)
            {
                Reel = reel;
                View = view;
            }

            public Reel Reel { get; }

            public ScreenView View { get; }

            public void Swipe(float fromAcross, float fromUp, float acrossShare, float upShare, float seconds, string? heldShot) =>
                SwipeAlong(PixelAt(fromAcross, fromUp), heldShot, (acrossShare, upShare, seconds));

            public void SwipeFromPixel(Vector2 from, float acrossShare, float upShare, float seconds, string? heldShot) =>
                SwipeAlong(from, heldShot, (acrossShare, upShare, seconds));

            /// <summary>The pixel of a place told as shares of the screen's width and height from its middle.</summary>
            public Vector2 PixelAt(float across, float up)
            {
                (float x, float y) = View.PixelOfPoint(across * View.Width, up * View.Height);
                return new Vector2(x, y);
            }

            /// <summary>The finger comes down at a pixel and goes one stretch after the other, each in a straight line and in its own time, without lifting between them.</summary>
            public void SwipeAlong(Vector2 from, string? heldShot, params (float Across, float Up, float Seconds)[] stretches)
            {
                GameSession session = Reel.Session;
                session.PointerDown(from);
                Vector2 at = from;
                foreach ((float across, float up, float seconds) in stretches)
                {
                    Vector2 way = new Vector2(across * View.Width, up * View.Width) * View.PixelsPerPoint;
                    int frames = Math.Max(1, Mathf.RoundToInt(seconds / StoryStudio.FrameSeconds));
                    for (int frame = 1; frame <= frames; frame++)
                    {
                        Reel.Advance(StoryStudio.FrameSeconds);
                        session.PointerMoved(at + way * ((float)frame / frames));
                    }

                    at += way;
                }

                if (heldShot != null)
                {
                    Reel.Shoot(heldShot);
                }

                session.PointerUp();
                Reel.Settle();
            }
        }
    }
}
