using System;
using System.Collections.Generic;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Museum
{
    /// <summary>
    /// A player's museum as lines of text, for the tools: what is open, what is whole, what waits on the trays, and
    /// what hangs where. The rooms that are open are listed painting by painting, the first closed one after them
    /// with the level that opens it.
    /// </summary>
    public static class MuseumSummary
    {
        public static IReadOnlyList<string> Of(MuseumProgress progress, MuseumCatalogue catalogue)
        {
            var lines = new List<string>
            {
                FormattableString.Invariant($"Museum after {progress.LevelsWon} of {MuseumPlan.LevelCount} levels won:"),
                FormattableString.Invariant($"  rooms: {progress.RoomsOpened} of {MuseumPlan.RoomCount} open, {progress.RoomsRestored} restored"),
                FormattableString.Invariant($"  paintings: {progress.PaintingsCompleted} of {MuseumPlan.PaintingCount} whole; the next level gives pieces of painting {progress.PaintingOfTheNextLevel}"),
                FormattableString.Invariant($"  pieces: {progress.PiecesWon} won, {progress.PiecesPlaced} placed, {progress.PiecesWaiting} waiting on the trays"),
            };

            foreach (MuseumRoom room in catalogue.Rooms)
            {
                lines.Add(RoomLine(room, progress));
                if (!progress.IsOpen(room.Number))
                {
                    break; // the first closed room says what comes next; the ones behind it say nothing new
                }

                foreach (PaintingNumber painting in room.Number.Paintings)
                {
                    lines.Add(PaintingLine(painting, catalogue.ArtworkAt(painting), progress));
                }
            }

            return lines;
        }

        private static string RoomLine(MuseumRoom room, MuseumProgress progress)
        {
            string name = room.IsNameProvisional ? $"{room.Movement} (provisional name)" : room.Movement;
            string levels = FormattableString.Invariant($"levels {room.Number.FirstLevel}-{room.Number.LastLevel}");
            string state = progress.IsRestored(room.Number) ? "restored"
                : progress.IsOpen(room.Number) ? "open"
                : FormattableString.Invariant($"closed, opens when level {room.Number.LevelsWonToOpen} is won");
            return FormattableString.Invariant($"Room {room.Number}: {name}, {levels}, {state}");
        }

        private static string PaintingLine(PaintingNumber number, Artwork? artwork, MuseumProgress progress)
        {
            JigsawSize size = number.Size;
            string work = artwork == null ? "not chosen yet" : $"{artwork} [{artwork.Id}, {artwork.Orientation}]";
            string state = progress.IsComplete(number) ? "whole"
                : FormattableString.Invariant($"{progress.PiecesPlacedIn(number)} placed, {progress.WaitingIn(number).Count} waiting");
            return FormattableString.Invariant(
                $"  painting {number}, levels {number.FirstLevel}-{number.LastLevel}, {size.PieceCount} pieces ({size.Name}): {progress.PiecesWonIn(number)} won, {state}; {work}");
        }
    }
}
