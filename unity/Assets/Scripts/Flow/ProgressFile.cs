#nullable enable
using System;
using System.IO;
using UnityEngine;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// The saved progress was written by a later version of the game than this one (a newer build was installed
    /// before, and this older one after it). The file is sound and is left as it is: writing this game's layout
    /// over it would lose what the later game kept there. Whoever opens the session decides how to go on without it.
    /// </summary>
    public sealed class ProgressOfALaterGameException : IOException
    {
        public ProgressOfALaterGameException(string path, int version, PlayerProgress? understood)
            : base($"{path} holds progress in layout {version}, which a later version of the game wrote; this version does not write over it.")
        {
            Version = version;
            Understood = understood;
        }

        /// <summary>The layout the file is in.</summary>
        public int Version { get; }

        /// <summary>What this game can read of the later game's progress: where the player stands. Nothing when it cannot read that either.</summary>
        public PlayerProgress? Understood { get; }
    }

    /// <summary>
    /// A player's progress as a small JSON file on the device (in the game's own data folder, which survives a
    /// restart and an update of the game, and goes when the game is uninstalled):
    /// <c>{"version":1,"levelsWon":39,"attemptsOnLevel":2}</c>.
    /// </summary>
    /// <remarks>
    /// A save writes a second file beside the first and then puts it in the first one's place, so a game that is
    /// killed in the middle of a save leaves the old progress or the new one, never half a file. A file that cannot
    /// be read as progress is not written over: it is moved aside under another name and reported, and the player
    /// starts at the first level. A file that a later version of the game wrote is something else: it is sound,
    /// only this game does not know all of it. It is left exactly where and as it is, and
    /// <see cref="Load"/> says so (<see cref="ProgressOfALaterGameException"/>) instead of answering.
    /// </remarks>
    public sealed class ProgressFile : IProgressStore
    {
        public const string FileName = "progress.json";

        /// <summary>The version of the document's layout this code writes; a file of a later game is not guessed at.</summary>
        private const int CurrentVersion = 1;

        private const string NewFileSuffix = ".new";
        private const string DamagedFileSuffix = ".damaged";

        private readonly string _path;

        /// <param name="folder">The folder the file lies in; on a device, <c>Application.persistentDataPath</c>.</param>
        public ProgressFile(string folder)
        {
            _path = Path.Combine(folder, FileName);
        }

        /// <exception cref="ProgressOfALaterGameException">The file was written by a later version of the game; it is left as it is.</exception>
        /// <exception cref="IOException">The file is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read the file.</exception>
        public PlayerProgress Load()
        {
            if (!File.Exists(_path))
            {
                return PlayerProgress.None;
            }

            switch (Read(File.ReadAllText(_path)))
            {
                case Reading.Sound sound:
                    return sound.Progress;
                case Reading.OfALaterGame later:
                    throw new ProgressOfALaterGameException(_path, later.Version, later.Understood);
                default:
                    return MoveTheDamagedFileAside();
            }
        }

        public void Save(PlayerProgress progress)
        {
            var document = new Document { version = CurrentVersion, levelsWon = progress.LevelsWon, attemptsOnLevel = progress.AttemptsOnLevel };
            string newFile = _path + NewFileSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(newFile, JsonUtility.ToJson(document));
            if (File.Exists(_path))
            {
                File.Replace(newFile, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(newFile, _path);
            }
        }

        /// <summary>The file is not a progress document: it is kept under another name, and the player starts at the first level.</summary>
        private PlayerProgress MoveTheDamagedFileAside()
        {
            string aside = _path + DamagedFileSuffix;
            File.Delete(aside);
            File.Move(_path, aside);
            Debug.LogWarning($"The saved progress could not be read; it was kept as {aside} and the game starts at the first level.");
            return PlayerProgress.None;
        }

        /// <summary>What a file's text is, as far as this version of the game can tell.</summary>
        private static Reading Read(string json)
        {
            Document? document;
            try
            {
                document = JsonUtility.FromJson<Document>(json);
            }
            catch (ArgumentException)
            {
                return new Reading.Damaged(); // Unity's reader says so when the text is not JSON at all
            }

            if (document == null || document.version < CurrentVersion)
            {
                return new Reading.Damaged();
            }

            // Later layouts are expected to add to this one, so the two numbers are looked for in them too.
            PlayerProgress? progress = document.levelsWon >= 0 && document.attemptsOnLevel >= 0
                ? new PlayerProgress(document.levelsWon, document.attemptsOnLevel)
                : null;
            if (document.version > CurrentVersion)
            {
                return new Reading.OfALaterGame(document.version, progress);
            }

            return progress != null ? new Reading.Sound(progress) : (Reading)new Reading.Damaged();
        }

        /// <summary>What reading the file's text came to. A closed set: the private constructor keeps every kind inside this class.</summary>
        private abstract class Reading
        {
            private Reading()
            {
            }

            /// <summary>A progress document of this game's own layout.</summary>
            public sealed class Sound : Reading
            {
                public Sound(PlayerProgress progress)
                {
                    Progress = progress;
                }

                public PlayerProgress Progress { get; }
            }

            /// <summary>A progress document of a later layout, with what this game can read of it, if anything.</summary>
            public sealed class OfALaterGame : Reading
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
            public sealed class Damaged : Reading
            {
            }
        }

        /// <summary>The file's layout, as Unity's JSON reader maps it: public fields named as the document names them.</summary>
        [Serializable]
        private sealed class Document
        {
            public int version;
            public int levelsWon;
            public int attemptsOnLevel;
        }
    }
}
