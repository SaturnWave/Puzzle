#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Museum;

namespace Puzzle.Client.Flow
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the file's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// The saved progress as the file writes it:
    /// <c>{"version":2,"levelsWon":39,"attemptsOnLevel":2,"placed":[{"painting":4,"pieces":[0,1,2]}]}</c>.
    /// The fields are public and named as the document names them, because that is how Unity's JSON reader maps a
    /// document. Layout 1, written before the game had a museum, is the same document without <c>placed</c>: it
    /// reads as progress with no jigsaw piece in place yet, so the pieces of every level won before then wait on
    /// their trays. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    [Serializable]
    public sealed class ProgressDocument
    {
        /// <summary>The layout this code writes. A file of a later layout is not guessed at.</summary>
        public const int CurrentVersion = 2;

        /// <summary>The oldest layout a player can have on a device; this code reads it as it reads its own.</summary>
        private const int FirstVersion = 1;

        public int version;
        public int levelsWon;
        public int attemptsOnLevel;

        /// <summary>The jigsaw pieces in place, one entry for every painting that has any; in the file since layout 2.</summary>
        public PlacedDocument[] placed = Array.Empty<PlacedDocument>();

        /// <summary>The document that keeps a progress, in the current layout.</summary>
        public static ProgressDocument Of(PlayerProgress progress)
        {
            var placed = new List<PlacedDocument>();
            foreach (PaintingNumber painting in PaintingNumber.All)
            {
                IReadOnlyList<int> pieces = progress.Museum.Placed.In(painting);
                if (pieces.Count > 0)
                {
                    placed.Add(new PlacedDocument { painting = painting.Value, pieces = pieces.ToArray() });
                }
            }

            return new ProgressDocument
            {
                version = CurrentVersion,
                levelsWon = progress.LevelsWon,
                attemptsOnLevel = progress.AttemptsOnLevel,
                placed = placed.ToArray(),
            };
        }

        /// <summary>What a document that a JSON reader filled is, as far as this version of the game can tell.</summary>
        /// <param name="document">Nothing when the file's text held no document.</param>
        public static ProgressReading Read(ProgressDocument? document)
        {
            if (document == null || document.version < FirstVersion)
            {
                return new ProgressReading.Damaged();
            }

            bool hasCounts = document.levelsWon >= 0 && document.attemptsOnLevel >= 0;
            PlacedPieces? placed = hasCounts ? PossiblePlacedPiecesOf(document) : null;
            if (document.version > CurrentVersion)
            {
                // Later layouts are expected to add to this one, so what this game knows is looked for in them too:
                // the pieces if they still read as pieces, else at least where the player stands.
                PlayerProgress? understood = !hasCounts ? null
                    : new PlayerProgress(document.levelsWon, document.attemptsOnLevel, placed ?? PlacedPieces.None);
                return new ProgressReading.OfALaterGame(document.version, understood);
            }

            return placed != null
                ? new ProgressReading.Sound(new PlayerProgress(document.levelsWon, document.attemptsOnLevel, placed))
                : (ProgressReading)new ProgressReading.Damaged();
        }

        /// <summary>The pieces the document says are in place; nothing when it names a painting or a piece the museum does not have, a piece twice, or one the levels won have not given.</summary>
        private static PlacedPieces? PossiblePlacedPiecesOf(ProgressDocument document)
        {
            PlacedPieces placed = PlacedPieces.None;
            foreach (PlacedDocument entry in ListOrNone(document.placed))
            {
                if (entry.painting < 1 || entry.painting > MuseumPlan.PaintingCount)
                {
                    return null;
                }

                var painting = new PaintingNumber(entry.painting);
                foreach (int piece in ListOrNone(entry.pieces))
                {
                    bool isAPieceNotYetNamed = piece >= 0 && piece < painting.Size.PieceCount && !placed.IsPlaced(painting, piece);
                    if (!isAPieceNotYetNamed)
                    {
                        return null;
                    }

                    placed = placed.With(painting, piece);
                }
            }

            return MuseumProgress.IsPossible(document.levelsWon, placed) ? placed : null;
        }

        /// <summary>A JSON reader may hand over nothing for a list the document does not have, as layout 1 has no placed pieces.</summary>
        private static T[] ListOrNone<T>(T[]? list) => list ?? Array.Empty<T>();
    }

    /// <summary>The pieces of one painting that are in place, as the file writes them.</summary>
    [Serializable]
    public sealed class PlacedDocument
    {
        /// <summary>The painting's number in the museum, from 1.</summary>
        public int painting;

        /// <summary>The numbers of its pieces that are in place, from 0.</summary>
        public int[] pieces = Array.Empty<int>();
    }

    /// <summary>What reading a progress document came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class ProgressReading
    {
        private ProgressReading()
        {
        }

        /// <summary>A progress document of a layout this game reads as its own.</summary>
        public sealed class Sound : ProgressReading
        {
            public Sound(PlayerProgress progress)
            {
                Progress = progress;
            }

            public PlayerProgress Progress { get; }
        }

        /// <summary>A progress document of a later layout, with what this game can read of it, if anything.</summary>
        public sealed class OfALaterGame : ProgressReading
        {
            public OfALaterGame(int version, PlayerProgress? understood)
            {
                Version = version;
                Understood = understood;
            }

            public int Version { get; }

            public PlayerProgress? Understood { get; }
        }

        /// <summary>Not a progress document: no JSON, half a save, numbers no progress can have.</summary>
        public sealed class Damaged : ProgressReading
        {
        }
    }
}
