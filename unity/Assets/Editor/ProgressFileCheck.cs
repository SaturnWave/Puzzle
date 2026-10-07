#nullable enable
using System;
using System.IO;
using Puzzle.Client.Flow;
using Puzzle.Museum;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line that the progress file does what the game relies on, with Unity's own JSON reader
    /// and file calls: nothing saved reads as a new player, what is saved is read back (also when a file is already
    /// there to be replaced, and with the jigsaw pieces the player has put in place), the file of the game before
    /// the museum still reads and gives its player the pieces of the levels they had won, a file that is not a
    /// progress document is moved aside and never written over, and a file that a later version of the game wrote
    /// is left exactly as it is while the game is told where the player stands. It works in a folder of its own
    /// (out/unity/progress-check), not in a player's.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.ProgressFileCheck.Run
    /// </summary>
    public static class ProgressFileCheck
    {
        private const string Folder = "../out/unity/progress-check";

        /// <summary>What a damaged file may hold: nothing, half a save, no JSON at all, a document without a layout, numbers no progress has.</summary>
        private static readonly string[] DamagedDocuments =
        {
            string.Empty,
            "{\"version\":1,\"levelsWon\":3",
            "not a progress file",
            "{\"levelsWon\":3,\"attemptsOnLevel\":1}",
            "{\"version\":1,\"levelsWon\":-4,\"attemptsOnLevel\":0}",
            "{\"version\":2,\"levelsWon\":3,\"attemptsOnLevel\":0,\"placed\":[{\"painting\":1,\"pieces\":[3]}]}", // three levels give the pieces 0 to 2
        };

        /// <summary>What the first Android build wrote: the layout before the museum.</summary>
        private const string DocumentBeforeTheMuseum = "{\"version\":1,\"levelsWon\":39,\"attemptsOnLevel\":2}";

        /// <summary>The same player after placing the first painting's pieces and two of the fourth's, as this game writes it.</summary>
        private const string DocumentWithPlacedPieces =
            "{\"version\":2,\"levelsWon\":39,\"attemptsOnLevel\":2,\"placed\":[{\"painting\":1,\"pieces\":[0,1,2,3,4,5,6,7,8,9]},{\"painting\":4,\"pieces\":[0,2]}]}";

        /// <summary>The document of a later game: what this game writes, and something it does not know.</summary>
        private const string LaterDocument = "{\"version\":3,\"levelsWon\":3,\"attemptsOnLevel\":1,\"placed\":[{\"painting\":1,\"pieces\":[1]}],\"coins\":250}";

        public static void Run()
        {
            try
            {
                StartWithAnEmptyFolder();
                var file = new ProgressFile(Folder);
                Expect(PlayerProgress.None, file.Load(), "with nothing saved");

                var first = new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2);
                file.Save(first);
                Expect(first, new ProgressFile(Folder).Load(), "after a first save");

                var second = first.WithLevelWon(levelCount: 1000);
                file.Save(second);
                Expect(second, new ProgressFile(Folder).Load(), "after a save over an older one");

                string path = Path.Combine(Folder, ProgressFile.FileName);
                ExpectThePiecesOfTheLevelsWonBeforeTheMuseum(file, path);

                foreach (string damaged in DamagedDocuments)
                {
                    File.WriteAllText(path, damaged);
                    Expect(PlayerProgress.None, file.Load(), $"with the file holding \"{damaged}\"");
                    ExpectMovedAside(path, damaged);
                }

                File.WriteAllText(path, LaterDocument);
                ExpectLeftAlone(file, path, new PlayerProgress(levelsWon: 3, attemptsOnLevel: 1).WithPiecePlaced(new PaintingNumber(1), 1));

                Debug.Log($"Progress file check passed: saved progress is read back with its placed pieces, the file of the game before the museum gives its "
                    + $"player their pieces, {DamagedDocuments.Length} damaged files were moved aside, and a later game's file was left as it was.");
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is InvalidOperationException)
            {
                Debug.LogError($"Progress file check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// The file of the game before the museum is read as it stands; every piece its levels gave waits on a tray.
        /// Once the player places pieces, the file is written in this game's layout, exactly as pinned, and reads back.
        /// </summary>
        /// <exception cref="InvalidOperationException">The old file was not read so, or the new one not written so.</exception>
        private static void ExpectThePiecesOfTheLevelsWonBeforeTheMuseum(ProgressFile file, string path)
        {
            File.WriteAllText(path, DocumentBeforeTheMuseum);
            PlayerProgress read = file.Load();
            Expect(new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2), read, "with the file of the game before the museum");
            if (read.Museum.PiecesWaiting != 39 || File.ReadAllText(path) != DocumentBeforeTheMuseum)
            {
                throw new InvalidOperationException($"The file of the game before the museum gave {read.Museum.PiecesWaiting} waiting pieces instead of 39, or was changed by reading it.");
            }

            PlayerProgress placed = read.WithTrayPlaced(new PaintingNumber(1)).WithPiecePlaced(new PaintingNumber(4), 2).WithPiecePlaced(new PaintingNumber(4), 0);
            file.Save(placed);
            Expect(placed, new ProgressFile(Folder).Load(), "after placing pieces");
            if (File.ReadAllText(path) != DocumentWithPlacedPieces)
            {
                throw new InvalidOperationException($"The progress with placed pieces was written as {File.ReadAllText(path)}; {DocumentWithPlacedPieces} was expected.");
            }
        }

        private static void StartWithAnEmptyFolder()
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }

        /// <exception cref="InvalidOperationException">The progress that was read is not the expected one.</exception>
        private static void Expect(PlayerProgress expected, PlayerProgress read, string when)
        {
            if (!read.Equals(expected))
            {
                throw new InvalidOperationException($"The file gave \"{read}\" {when}; \"{expected}\" was expected.");
            }
        }

        /// <exception cref="InvalidOperationException">The later game's file was read as this game's, moved or changed, or the player's place was not told.</exception>
        private static void ExpectLeftAlone(ProgressFile file, string path, PlayerProgress understood)
        {
            try
            {
                PlayerProgress read = file.Load();
                throw new InvalidOperationException($"A later game's file was read as \"{read}\" instead of being refused.");
            }
            catch (ProgressOfALaterGameException later)
            {
                bool isLeft = File.Exists(path) && File.ReadAllText(path) == LaterDocument;
                if (!isLeft || later.Version != 3 || !understood.Equals(later.Understood))
                {
                    throw new InvalidOperationException(
                        $"A later game's file was not left as it was (still there unchanged: {isLeft}), or the game was told layout {later.Version} and \"{later.Understood}\".");
                }
            }
        }

        /// <exception cref="InvalidOperationException">The damaged file is still in the progress file's place, or was not kept.</exception>
        private static void ExpectMovedAside(string path, string damaged)
        {
            string[] others = Directory.GetFiles(Folder);
            bool isKept = others.Length == 1 && others[0] != path && File.ReadAllText(others[0]) == damaged;
            if (File.Exists(path) || !isKept)
            {
                throw new InvalidOperationException($"A file holding \"{damaged}\" was not moved aside as it was; the folder holds {string.Join(", ", others)}.");
            }
        }
    }
}
