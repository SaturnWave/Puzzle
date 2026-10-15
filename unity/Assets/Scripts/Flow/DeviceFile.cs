#nullable enable
using System;
using System.IO;
using Puzzle.Core.Recording;
using UnityEngine;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// The phone's own small file, device.json, beside the progress in the game's data folder: it keeps the random id
    /// that the phone's attempt records carry, and the player's settings (<see cref="DeviceDocument"/>). The game's
    /// first start makes the id and writes the file; every later start reads it, so the id stays the same until the
    /// game is uninstalled. A setting the player changes is written at once (<see cref="Keep"/>).
    /// </summary>
    /// <remarks>
    /// A save writes a second file beside the first and then puts it in the first one's place, as the progress file
    /// does, so a game killed in the middle of it leaves the old file or the new one. A file that cannot be read as a
    /// device document is moved aside under another name and a new id is made and written with the settings of a
    /// phone that never set anything: the records after it carry another id and the settings are as on a first
    /// start, which is all such a file can cost; the progress and the wallet are in another file and are not touched.
    /// A file of layout 1 reads with those settings too, and is written in this game's layout at the first change. A
    /// file that a later version of the game wrote is left exactly as it is: its id and its settings are used as far
    /// as this game reads them (otherwise this run uses a new id), and a setting changed in this run is held for the
    /// run and not written.
    /// </remarks>
    public sealed class DeviceFile : ISettingsStore
    {
        public const string FileName = "device.json";

        private const string NewFileSuffix = ".new";
        private const string DamagedFileSuffix = ".damaged";

        private readonly string _path;

        /// <summary>True unless the file is a later game's, which this game never writes over.</summary>
        private readonly bool _isThisGamesToWrite;

        private DeviceFile(string path, DeviceId device, DeviceSettings settings, bool isThisGamesToWrite)
        {
            _path = path;
            Device = device;
            Settings = settings;
            _isThisGamesToWrite = isThisGamesToWrite;
        }

        /// <summary>The phone's id, which every attempt record of this phone carries.</summary>
        public DeviceId Device { get; }

        public DeviceSettings Settings { get; private set; }

        /// <summary>The phone as its file has it: the id and the settings on file, or a new id written at once where the game may keep it.</summary>
        /// <param name="folder">The folder the file lies in; on a device, <c>Application.persistentDataPath</c>.</param>
        /// <exception cref="IOException">The file could not be read, or a new id could not be written.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read or write the file.</exception>
        public static DeviceFile LoadOrMake(string folder)
        {
            string path = Path.Combine(folder, FileName);
            if (!File.Exists(path))
            {
                return Made(path);
            }

            switch (DeviceDocument.Read(Read(File.ReadAllText(path))))
            {
                case DeviceReading.Sound sound:
                    return new DeviceFile(path, sound.Device, sound.Settings, isThisGamesToWrite: true);
                case DeviceReading.OfALaterGame later:
                    return new DeviceFile(path, later.Device ?? NotWritten(later.Version), later.Settings, isThisGamesToWrite: false);
                default:
                    return MadeInPlaceOfTheDamagedFile(path);
            }
        }

        public void Keep(DeviceSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!_isThisGamesToWrite)
            {
                Debug.LogWarning($"{FileName} is a later version's of the game, so the settings changed in this run ({settings}) are not written over it.");
                return;
            }

            Write(_path, DeviceDocument.Of(Device, settings));
        }

        /// <summary>A new id, written as the phone's with the settings of a phone that never set anything.</summary>
        private static DeviceFile Made(string path)
        {
            DeviceId device = DeviceDocument.NewId();
            Write(path, DeviceDocument.Of(device, DeviceSettings.Default));
            return new DeviceFile(path, device, DeviceSettings.Default, isThisGamesToWrite: true);
        }

        private static void Write(string path, DeviceDocument document)
        {
            string newFile = path + NewFileSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(newFile, JsonUtility.ToJson(document));
            if (File.Exists(path))
            {
                File.Replace(newFile, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(newFile, path);
            }
        }

        /// <summary>A later game's file without an id this game reads: this run has an id of its own, and the file stays the later game's.</summary>
        private static DeviceId NotWritten(int version)
        {
            Debug.LogWarning($"device.json is in layout {version}, which a later version of the game wrote, and its id does not read here; this run's attempt records carry an id of their own.");
            return DeviceDocument.NewId();
        }

        /// <summary>The file is not a device document: it is kept under another name, and a new id is written in its place.</summary>
        private static DeviceFile MadeInPlaceOfTheDamagedFile(string path)
        {
            string aside = path + DamagedFileSuffix;
            File.Delete(aside);
            File.Move(path, aside);
            Debug.LogWarning($"The device file could not be read; it was kept as {aside}, and the phone has a new id and the settings of a first start.");
            return Made(path);
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
