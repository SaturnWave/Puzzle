#nullable enable
using System;
using System.IO;
using Puzzle.Client.Flow;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line that the progress file does what the game relies on, with Unity's own JSON reader
    /// and file calls: nothing saved reads as a new player, what is saved is read back (also when a file is already
    /// there to be replaced), a file that is not a progress document is moved aside and never written over, and a
    /// file that a later version of the game wrote is left exactly as it is while the game is told where the player
    /// stands. It works in a folder of its own (out/unity/progress-check), not in a player's.
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
        };

        /// <summary>The document of a later game: this game's two numbers, and something it does not know.</summary>
        private const string LaterDocument = "{\"version\":2,\"levelsWon\":3,\"attemptsOnLevel\":1,\"coins\":250}";

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
                foreach (string damaged in DamagedDocuments)
                {
                    File.WriteAllText(path, damaged);
                    Expect(PlayerProgress.None, file.Load(), $"with the file holding \"{damaged}\"");
                    ExpectMovedAside(path, damaged);
                }

                File.WriteAllText(path, LaterDocument);
                ExpectLeftAlone(file, path, new PlayerProgress(levelsWon: 3, attemptsOnLevel: 1));

                Debug.Log($"Progress file check passed: saved progress is read back, {DamagedDocuments.Length} damaged files were moved aside, and a later game's file was left as it was.");
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is InvalidOperationException)
            {
                Debug.LogError($"Progress file check failed: {failure.Message}");
                EditorApplication.Exit(1);
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
                if (!isLeft || later.Version != 2 || !understood.Equals(later.Understood))
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
