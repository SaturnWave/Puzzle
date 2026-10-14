#nullable enable
using System;
using System.IO;
using Puzzle.Core.Recording;
using UnityEngine;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// The phone's own small file, device.json, beside the progress in the game's data folder: it keeps the random id
    /// that the phone's attempt records carry (<see cref="DeviceDocument"/>). The game's first start makes the id and
    /// writes the file; every later start reads it, so the id stays the same until the game is uninstalled.
    /// </summary>
    /// <remarks>
    /// A save writes a second file beside the first and then puts it in the first one's place, as the progress file
    /// does, so a game killed in the middle of it leaves the old file or the new one. A file that cannot be read as a
    /// device document is moved aside under another name and a new id is made and written: the records after it carry
    /// another id, which is all such a file can cost. A file that a later version of the game wrote is left exactly as
    /// it is: its id is used if this game can read it, and otherwise this run uses a new id that is never written.
    /// </remarks>
    public sealed class DeviceFile
    {
        public const string FileName = "device.json";

        private const string NewFileSuffix = ".new";
        private const string DamagedFileSuffix = ".damaged";

        private readonly string _path;

        /// <param name="folder">The folder the file lies in; on a device, <c>Application.persistentDataPath</c>.</param>
        public DeviceFile(string folder)
        {
            _path = Path.Combine(folder, FileName);
        }

        /// <summary>The phone's id: the one on file, or a new one, written at once when the game may keep it.</summary>
        /// <exception cref="IOException">The file could not be read, or a new id could not be written.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read or write the file.</exception>
        public DeviceId LoadOrMake()
        {
            if (!File.Exists(_path))
            {
                return Made();
            }

            switch (DeviceDocument.Read(Read(File.ReadAllText(_path))))
            {
                case DeviceReading.Sound sound:
                    return sound.Device;
                case DeviceReading.OfALaterGame later:
                    return later.Device ?? NotWritten(later.Version);
                default:
                    return MadeInPlaceOfTheDamagedFile();
            }
        }

        /// <summary>A new id, written as the phone's.</summary>
        private DeviceId Made()
        {
            DeviceId device = DeviceDocument.NewId();
            string newFile = _path + NewFileSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(newFile, JsonUtility.ToJson(DeviceDocument.Of(device)));
            if (File.Exists(_path))
            {
                File.Replace(newFile, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(newFile, _path);
            }

            return device;
        }

        /// <summary>A later game's file without an id this game reads: this run has an id of its own, and the file stays the later game's.</summary>
        private static DeviceId NotWritten(int version)
        {
            Debug.LogWarning($"device.json is in layout {version}, which a later version of the game wrote, and its id does not read here; this run's attempt records carry an id of their own.");
            return DeviceDocument.NewId();
        }

        /// <summary>The file is not a device document: it is kept under another name, and a new id is written in its place.</summary>
        private DeviceId MadeInPlaceOfTheDamagedFile()
        {
            string aside = _path + DamagedFileSuffix;
            File.Delete(aside);
            File.Move(_path, aside);
            Debug.LogWarning($"The device file could not be read; it was kept as {aside} and the phone has a new id.");
            return Made();
        }

        /// <summary>The document a file's text holds; nothing when it is not JSON at all.</summary>
        private static DeviceDocument? Read(string json)
        {
            try
            {
                return JsonUtility.FromJson<DeviceDocument>(json);
            }
            catch (ArgumentException)
            {
                return null; // Unity's reader says so when the text is not JSON at all
            }
        }
    }
}
