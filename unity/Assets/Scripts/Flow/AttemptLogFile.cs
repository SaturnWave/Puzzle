#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Puzzle.Core.Recording;

namespace Puzzle.Client.Flow
{
    /// <summary>Where the records of the attempts played on this phone are kept: one record for every attempt that ended for good.</summary>
    public interface IAttemptLog
    {
        /// <summary>The phone whose attempts these are: every record carries its id.</summary>
        DeviceId Device { get; }

        /// <summary>Keeps the record of an attempt that has ended for good, after every record still waiting to be written.</summary>
        /// <exception cref="ArgumentException">The record is of another device.</exception>
        /// <exception cref="IOException">The record could not be written now; it waits, and is written with the next record or by <see cref="WriteWaiting"/>.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not write the log now; the record waits as it does after an <see cref="IOException"/>.</exception>
        void Append(AttemptRecord record);

        /// <summary>Writes the records that could not be written before, if any.</summary>
        /// <exception cref="IOException">They could not be written now either; they go on waiting.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not write the log now; they go on waiting.</exception>
        void WriteWaiting();
    }

    /// <summary>
    /// The phone's attempt log: attempts.jsonl in the game's own data folder, one line per attempt as
    /// <see cref="AttemptLine"/> writes it, only ever appended, and never sent anywhere (docs/GAME_DESIGN.md §20). It
    /// goes with the game's data when the game is uninstalled.
    /// </summary>
    /// <remarks>
    /// Each line is written whole and the file closed after it, so a game killed in the middle of a write cuts at
    /// most that line short. The next line starts a line of its own (the file is looked at for its last line break),
    /// and the tools' reader reports the cut line as one it cannot read. A record that cannot be written does not stop
    /// the game: it waits here, in the order the attempts ended, and is written before the next record, or when the
    /// game is put away. At most <see cref="MaxWaiting"/> records wait; past that the one that has waited longest gives
    /// way and is counted in <see cref="RecordsLost"/>.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </remarks>
    public sealed class AttemptLogFile : IAttemptLog
    {
        public const string FileName = "attempts.jsonl";

        /// <summary>A few hours of play: enough for a full storage to be cleared before anything is lost.</summary>
        public const int MaxWaiting = 64;

        private const byte LineBreak = (byte)'\n';

        private readonly string _folder;
        private readonly Queue<string> _waiting = new Queue<string>();

        /// <param name="folder">The folder the file lies in; on a device, <c>Application.persistentDataPath</c>.</param>
        /// <param name="device">The phone's id (<see cref="DeviceDocument"/>), which every record of this log carries.</param>
        public AttemptLogFile(string folder, DeviceId device)
        {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Path = System.IO.Path.Combine(folder, FileName);
        }

        public DeviceId Device { get; }

        /// <summary>The full path of the file.</summary>
        public string Path { get; }

        /// <summary>How many records wait to be written.</summary>
        public int Waiting => _waiting.Count;

        /// <summary>How many records gave way while too many waited, since the game started.</summary>
        public int RecordsLost { get; private set; }

        public void Append(AttemptRecord record)
        {
            if (!record.Start.Device.Equals(Device))
            {
                throw new ArgumentException($"The record is of the device {record.Start.Device}, and this log keeps the attempts of {Device}.", nameof(record));
            }

            _waiting.Enqueue(AttemptLine.Of(record));
            if (_waiting.Count > MaxWaiting)
            {
                _waiting.Dequeue();
                RecordsLost++;
            }

            WriteWaiting();
        }

        public void WriteWaiting()
        {
            if (_waiting.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(_folder);
            while (_waiting.Count > 0)
            {
                WriteALine(_waiting.Peek());
                _waiting.Dequeue();
            }
        }

        /// <summary>Appends one line, after a line break of its own if the file does not end in one.</summary>
        private void WriteALine(string line)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");

            // Read access only to look at the last byte; nobody else writes while this does.
            using (var file = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                if (file.Length > 0)
                {
                    file.Seek(-1, SeekOrigin.End);
                    if (file.ReadByte() != LineBreak)
                    {
                        file.WriteByte(LineBreak);
                    }
                }

                file.Write(bytes, 0, bytes.Length);
            }
        }
    }

    /// <summary>A log that forgets when the game closes: for the Editor's tools and for tests, which must not write a player's log.</summary>
    public sealed class AttemptsInMemory : IAttemptLog
    {
        private readonly List<AttemptRecord> _records = new List<AttemptRecord>();

        public AttemptsInMemory(DeviceId device)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public DeviceId Device { get; }

        /// <summary>The records kept, in the order the attempts ended.</summary>
        public IReadOnlyList<AttemptRecord> Records => _records;

        public void Append(AttemptRecord record)
        {
            if (!record.Start.Device.Equals(Device))
            {
                throw new ArgumentException($"The record is of the device {record.Start.Device}, and this log keeps the attempts of {Device}.", nameof(record));
            }

            _records.Add(record);
        }

        public void WriteWaiting()
        {
            // Nothing waits: a record in memory is kept the moment it comes.
        }
    }
}
