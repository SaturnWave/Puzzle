#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Puzzle.Meta;
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

        /// <summary>What this game can read of the later game's progress: where the player stands, with the pieces and the wallet as far as they read. Nothing when it cannot read the player's place either.</summary>
        public PlayerProgress? Understood { get; }
    }

    /// <summary>
    /// A player's progress as a small JSON file on the device (in the game's own data folder, which survives a
    /// restart and an update of the game, and goes when the game is uninstalled). What the file holds, and how its
    /// layouts follow each other, is <see cref="ProgressDocument"/>'s to say; this class reads and writes it.
    /// </summary>
    /// <remarks>
    /// A save writes a second file beside the first and then puts it in the first one's place, so a game that is
    /// killed in the middle of a save leaves the old progress or the new one, never half a file. A file that cannot
    /// be read as progress is not written over: it is moved aside under another name and reported, and the player
    /// starts at the first level as a new player. A file that a later version of the game wrote is something else:
    /// it is sound, only this game does not know all of it. It is left exactly where and as it is, and
    /// <see cref="Load"/> says so (<see cref="ProgressOfALaterGameException"/>) instead of answering. A file whose
    /// purchases hold a line no game writes is read without that line (<see cref="PurchasesDocument.Read"/>), and is
    /// copied beside the progress as it was, for support, before the next save writes it anew. A file of an
    /// earlier layout is read as it stands (one from before the wallet gives its player a new player's wallet) and
    /// written in this game's layout at the next save.
    /// </remarks>
    public sealed class ProgressFile : IProgressStore
    {
        public const string FileName = "progress.json";

        private const string NewFileSuffix = ".new";
        private const string DamagedFileSuffix = ".damaged";

        /// <summary>The file as it was read when lines of its purchases were set aside, kept for support beside the progress.</summary>
        private const string SetAsideFileSuffix = ".purchases-set-aside";

        private readonly string _path;
        private readonly Wallet _startingWallet;

        /// <param name="folder">The folder the file lies in; on a device, <c>Application.persistentDataPath</c>.</param>
        /// <param name="startingWallet">
        /// What the rules give a new player (Puzzle.Meta.EconomyRules.StartingWallet): the wallet of a player with no
        /// file, with a damaged one, or with one of a layout before the wallet.
        /// </param>
        public ProgressFile(string folder, Wallet startingWallet)
        {
            _path = Path.Combine(folder, FileName);
            _startingWallet = startingWallet ?? throw new ArgumentNullException(nameof(startingWallet));
        }

        /// <summary>The file is in the game's own data folder, which survives a restart and an update.</summary>
        public ProgressKeeping Keeping => ProgressKeeping.AfterClosing;

        /// <exception cref="ProgressOfALaterGameException">The file was written by a later version of the game; it is left as it is.</exception>
        /// <exception cref="IOException">The file is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read the file.</exception>
        public PlayerProgress Load()
        {
            if (!File.Exists(_path))
            {
                return PlayerProgress.NewPlayer(_startingWallet);
            }

            string text = File.ReadAllText(_path);
            switch (Read(text))
            {
                case ProgressReading.Sound sound:
                    if (sound.SetAside.Count > 0)
                    {
                        KeepTheFileAsRead(text, sound.SetAside);
                    }

                    return sound.Progress;
                case ProgressReading.OfALaterGame later:
                    throw new ProgressOfALaterGameException(_path, later.Version, later.Understood);
                default:
                    return MoveTheDamagedFileAside();
            }
        }

        public void Save(PlayerProgress progress)
        {
            string newFile = _path + NewFileSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(newFile, JsonUtility.ToJson(ProgressDocument.Of(progress)));
            if (File.Exists(_path))
            {
                File.Replace(newFile, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(newFile, _path);
            }
        }

        /// <summary>
        /// Lines of the purchases were set aside and the rest of the progress read: the file as it was is copied beside
        /// it (the next save writes the progress without those lines), and the log says which lines and why.
        /// </summary>
        private void KeepTheFileAsRead(string text, IReadOnlyList<string> setAside)
        {
            string aside = _path + SetAsideFileSuffix;
            File.WriteAllText(aside, text);
            Debug.LogWarning($"The saved purchases had {setAside.Count} line(s) no game writes, set aside: {string.Join("; ", setAside)}. The rest of the progress was read; the file as it was is kept as {aside}.");
        }

        /// <summary>The file is not a progress document: it is kept under another name, and the player starts at the first level as a new player.</summary>
        private PlayerProgress MoveTheDamagedFileAside()
        {
            string aside = _path + DamagedFileSuffix;
            File.Delete(aside);
            File.Move(_path, aside);
            Debug.LogWarning($"The saved progress could not be read; it was kept as {aside} and the game starts at the first level.");
            return PlayerProgress.NewPlayer(_startingWallet);
        }

        /// <summary>What a file's text is, as far as this version of the game can tell.</summary>
        private ProgressReading Read(string json)
        {
            try
            {
                return ProgressDocument.Read(JsonUtility.FromJson<ProgressDocument>(json), _startingWallet);
            }
            catch (ArgumentException)
            {
                return new ProgressReading.Damaged(); // Unity's reader says so when the text is not JSON at all
            }
        }
    }
}
