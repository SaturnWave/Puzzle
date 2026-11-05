#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Maps;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Museum;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the world map (#4, MapWorldTR), once on each screen, on the game's own session and by touches
    /// alone, for a player on level 287 with every piece won before it in place: the map opens on the whole world
    /// (pictured) and glides to the sixth era, where the player is (pictured at rest); a finger drags the strip of eras
    /// to the left and presses the twelfth (pictured), where a pin's card shows a painting still ahead, veiled
    /// (pictured), and its cross closes it; the strip is dragged on to the fourteenth (pictured); the globe shows the
    /// whole world again (pictured); the target goes back to the sixth era with the card of the painting being
    /// restored (pictured), whose "Oyna" brings the card before the level (pictured) and the way back takes it up
    /// again; its arrow up steps to the restored painting before it (pictured), and its
    /// "Koleksiyonda gör" opens the collection at the sixth room (pictured). On every view each button shown says
    /// what it does and is at least 44 points square, and the plaque and the card say what the view and the
    /// painting are (<c>walk-worldmap-...</c>).
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>The design canvas' own player: level 287, the fourth painting of the sixth era.</summary>
        private const int MapLevel = 287;

        /// <summary>Long enough for the map to open, glide and fade its pins in; for a press on the map to take effect.</summary>
        private const float MapComesToRest = 2f;
        private const float MapTakesThePress = 0.4f;

        /// <summary>A drag of the strip: this share of the screen's width to the left, in this long, as often as it takes.</summary>
        private const float StripDragShare = -0.25f;
        private const float StripDragSeconds = 0.5f;
        private const int MostDrags = 8;

        public static int RunTheWorldMapWalk(ScreenShape screen, Studio studio)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, MapLevel), studio.Levels);
            PlayerProgress placed = standing.WithWaitingPiecesPlaced(standing.Museum.PiecesWaiting);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(placed), StoryStudio.AttemptsKeptInMemory(),
                screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-worldmap-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            var finger = new SwipingFinger(reel, ScreenView.Of(screen));
            reel.Settle();
            TurnThePage(reel, Destination.Map, picturesTheTurn: false);
            MapPage map = MapOf(session);
            ThroughTheCard.Require(map.View.IsWorld, $"the map opened on {map.View}, not on the whole world");
            RequireTheButtons(map, "the whole world as the map opens");
            reel.Shoot("world");

            reel.Advance(MapComesToRest);
            ThroughTheCard.Require(map.View == MapView.Of(new RoomNumber(6)), $"the map glided to {map.View}, not to the player's sixth era");
            ThroughTheCard.Require(map.PlaqueSays.StartsWith(MapWords.EraLabel(studio.Paintings.Catalogue.Room(new RoomNumber(6))), StringComparison.Ordinal), $"the plaque says \"{map.PlaqueSays}\" in the sixth era");
            RequireTheButtons(map, "the sixth era");
            reel.Shoot("era-6");

            GoToTheEra(reel, finger, new RoomNumber(12));
            reel.Shoot("era-12");
            var ahead = new PaintingNumber(56);
            Press(session, MuseumOf(session).PixelOnTheMap(page => page.MiddleOfPin(ahead)), "the pin of the twelfth era's first painting", reel, pressedShot: null);
            reel.Advance(MapTakesThePress);
            ThroughTheCard.Require(map.CardOf == ahead && map.CardSays == MapWords.StatusOpensAt(ahead), $"the pin of painting {ahead} opened the card of {map.CardOf}, which says \"{map.CardSays}\"");
            RequireTheButtons(map, "the card of a painting ahead");
            reel.Shoot("card-ahead");
            Press(session, MuseumOf(session).PixelOnTheMap(page => page.MiddleOnTheCard(MapCardPress.Close)), "the card's cross", reel, pressedShot: null);
            reel.Advance(MapTakesThePress);
            ThroughTheCard.Require(map.CardOf == null, "the card's cross did not close it");

            GoToTheEra(reel, finger, new RoomNumber(14));
            reel.Shoot("era-14");

            Press(session, MuseumOf(session).PixelOnTheMap(page => page.MiddleOfTheGlobe), "the globe", reel, pressedShot: null);
            reel.Advance(MapComesToRest);
            ThroughTheCard.Require(map.View.IsWorld && map.PlaqueSays.Contains(MapWords.WorldTitle), $"the globe left the map on {map.View}, its plaque saying \"{map.PlaqueSays}\"");
            reel.Shoot("world-again");

            Press(session, MuseumOf(session).PixelOnTheMap(page => page.MiddleOfTheTarget), "the target", reel, pressedShot: null);
            reel.Advance(MapComesToRest);
            var here = new PaintingNumber(29);
            ThroughTheCard.Require(map.View == MapView.Of(new RoomNumber(6)) && map.CardOf == here, $"the target left the map on {map.View} with the card of {map.CardOf}");
            ThroughTheCard.Require(map.CardSays == MapWords.StatusPieces(14, 20, MapLevel), $"the card of the painting being restored says \"{map.CardSays}\"");
            RequireTheButtons(map, "the card of the painting being restored");
            reel.Shoot("card-here");

            Press(session, MuseumOf(session).PixelOnTheMap(page => page.MiddleOnTheCard(MapCardPress.Play)), "the card's Oyna", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).ShowsTheStartCard, "the card's Oyna did not bring the card before the level");
            reel.Shoot("level-card");
            session.GoBack();
            reel.Settle();
            ThroughTheCard.Require(!MuseumOf(session).ShowsTheStartCard && map.CardOf == here, "the way back from the card before the level did not leave the map with the card of the painting being restored");

            Press(session, MuseumOf(session).PixelOnTheMap(page => page.MiddleOnTheCard(MapCardPress.Previous)), "the card's arrow up", reel, pressedShot: null);
            reel.Advance(MapTakesThePress);
            ThroughTheCard.Require(map.CardOf == new PaintingNumber(28) && map.CardSays == MapWords.StatusRestored, $"the arrow up led to the card of {map.CardOf}, which says \"{map.CardSays}\"");
            RequireTheButtons(map, "the card of a restored painting");
            reel.Shoot("card-restored");

            Press(session, MuseumOf(session).PixelOnTheMap(page => page.MiddleOnTheCard(MapCardPress.SeeInTheCollection)), "the card's Koleksiyonda gör", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).Showing == Destination.Collection && MuseumOf(session).Door.CollectionRoom == new RoomNumber(6),
                $"Koleksiyonda gör left the museum on {MuseumOf(session).Showing} at room {MuseumOf(session).Door.CollectionRoom}");
            reel.Shoot("collection");
            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: the world, eras 6, 12 and 14, a card ahead, the card being restored and a restored one, and the collection; {reel.Taken} pictures.");
            return reel.Taken;
        }

        /// <summary>The finger drags the strip to the left until the era's medallion is in full view, then presses it, and the camera comes to rest there.</summary>
        private static void GoToTheEra(Reel reel, SwipingFinger finger, RoomNumber era)
        {
            GameSession session = reel.Session;
            for (int drag = 0; drag < MostDrags && MuseumOf(session).PixelOnTheMap(page => page.MiddleOfEra(era)) == null; drag++)
            {
                // From the first medallion in view, towards the era: to the left for an era further on, to the right for one before.
                (RoomNumber from, Vector2 pixel) = FirstMedallionInView(session);
                float towards = era.Value > from.Value ? StripDragShare : -StripDragShare;
                finger.SwipeFromPixel(pixel, towards, 0f, StripDragSeconds, heldShot: null);
                reel.Advance(Tempo.MapStripScrolls + MapTakesThePress);
                ThroughTheCard.Require(MuseumOf(session).Showing == Destination.Map, "a drag on the strip of eras turned the page");
            }

            Press(session, MuseumOf(session).PixelOnTheMap(page => page.MiddleOfEra(era)), $"the medallion of era {era}", reel, pressedShot: null);
            reel.Advance(MapComesToRest);
            MapPage map = MapOf(session);
            ThroughTheCard.Require(map.View == MapView.Of(era), $"the medallion of era {era} left the map on {map.View}");
            RequireTheButtons(map, $"era {era}");
        }

        /// <summary>The first era whose medallion the strip shows in full, with its pixel.</summary>
        private static (RoomNumber Era, Vector2 Pixel) FirstMedallionInView(GameSession session)
        {
            foreach (RoomNumber era in RoomNumber.All)
            {
                if (MuseumOf(session).PixelOnTheMap(page => page.MiddleOfEra(era)) is Vector2 pixel)
                {
                    return (era, pixel);
                }
            }

            throw new InvalidOperationException("The strip of eras shows no medallion to drag it from.");
        }

        /// <summary>Every button the map shows says what it does and is at least 44 points square.</summary>
        private static void RequireTheButtons(MapPage map, string where)
        {
            foreach (MapButton button in map.ButtonsShown)
            {
                ThroughTheCard.Require(!string.IsNullOrWhiteSpace(button.Label), $"a button of the map in {where} says nothing to a screen reader");
                ThroughTheCard.Require(
                    button.Target.Width >= MapButton.LeastTarget - 0.01f && button.Target.Height >= MapButton.LeastTarget - 0.01f,
                    $"the button \"{button.Label}\" in {where} is pressed in {button.Target.Width:0.#} by {button.Target.Height:0.#} points");
            }
        }

        private static MapPage MapOf(GameSession session) =>
            MuseumOf(session).Map ?? throw new InvalidOperationException($"The museum shows {MuseumOf(session).Showing}, not the map the walk expected.");
    }
}
