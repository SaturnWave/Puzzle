#nullable enable
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Puzzle.Client.Words;
using Puzzle.Core.Recording;

namespace Puzzle.Client.Flow
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the file's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// What the game keeps about the phone itself, as device.json writes it:
    /// <c>{"version":4,"id":"6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a","settings":{"sound":true,"vibration":true,"testerMode":false,"language":"en"}}</c>.
    /// The id is 128 random bits, written as 32 lowercase hexadecimal digits, made on the game's first start; it is the
    /// only identity the phone's attempt records carry (Puzzle.Core's DeviceId, docs/GAME_DESIGN.md §20), and nothing
    /// about the player or the phone goes into it. Beside it are the player's settings (<see cref="DeviceSettings"/>),
    /// since layout 2; a file of layout 1 has none and reads with the settings of a phone that never set anything.
    /// The tester mode joined the settings in layout 3; a file of an earlier layout reads with it off, whatever it holds.
    /// The language the player chose joined them in layout 4, as its code ("en"; empty for the phone's language); a
    /// file of an earlier layout reads with no language chosen, so the game speaks the phone's.
    /// The fields are public and named as the document names them, because that is how Unity's JSON reader maps a
    /// document; it makes an empty text of an id a document does not have, so a document is read by its layout's
    /// number and its id's spelling, and whether it holds settings by its layout's number, never by a false.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    [Serializable]
    public sealed class DeviceDocument
    {
        /// <summary>The layout this code writes. A file of a later layout is not guessed at.</summary>
        public const int CurrentVersion = 4;

        private const int FirstVersion = 1;

        /// <summary>The first layout that keeps the player's settings.</summary>
        private const int FirstVersionWithSettings = 2;

        /// <summary>The first layout that keeps the tester mode among the settings.</summary>
        private const int FirstVersionWithTheTesterMode = 3;

        /// <summary>The first layout that keeps the language the player chose.</summary>
        private const int FirstVersionWithALanguage = 4;

        private const int IdBytes = 16;

        public int version;
        public string id = string.Empty;

        /// <summary>The player's settings; in the file since layout 2.</summary>
        public SettingsDocument settings = new SettingsDocument();

        /// <summary>The document that keeps a device's id and its settings, in the current layout.</summary>
        public static DeviceDocument Of(DeviceId device, DeviceSettings settings) =>
            new DeviceDocument { version = CurrentVersion, id = device.Value, settings = SettingsDocument.Of(settings) };

        /// <summary>A new random id, as the game makes one on its first start: 128 bits from the system's source of secure randomness.</summary>
        public static DeviceId NewId()
        {
            var bits = new byte[IdBytes];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bits);
            }

            var digits = new StringBuilder(2 * IdBytes);
            foreach (byte eight in bits)
            {
                digits.Append(eight.ToString("x2", CultureInfo.InvariantCulture));
            }

            return new DeviceId(digits.ToString());
        }

        /// <summary>What a document that a JSON reader filled is, as far as this version of the game can tell.</summary>
        /// <param name="document">Nothing when the file's text held no document.</param>
        public static DeviceReading Read(DeviceDocument? document)
        {
            if (document == null || document.version < FirstVersion)
            {
                return new DeviceReading.Damaged();
            }

            DeviceId? device = IsAnIdTheGameMakes(document.id) ? new DeviceId(document.id) : null;
            DeviceSettings settings = document.version < FirstVersionWithSettings || document.settings == null
                ? DeviceSettings.Default
                : document.settings.ToSettings();
            if (document.version < FirstVersionWithTheTesterMode)
            {
                // The reader makes a false of a field a file does not have; the layout decides, so a hand's edit to an older file turns nothing on.
                settings = settings.With(DeviceSetting.TesterMode, isOn: false);
            }

            if (document.version < FirstVersionWithALanguage)
            {
                // Likewise an older file has chosen no language, whatever a hand wrote into it: the game speaks the phone's.
                settings = new DeviceSettings(settings.Sound, settings.Vibration, settings.TesterMode, language: null);
            }

            if (document.version > CurrentVersion)
            {
                // Later layouts are expected to add to this one: the id and the settings are looked for in them too.
                return new DeviceReading.OfALaterGame(document.version, device, settings);
            }

            return device != null ? new DeviceReading.Sound(device, settings) : (DeviceReading)new DeviceReading.Damaged();
        }

        /// <summary>Whether the text is spelled as <see cref="NewId"/> spells an id: 32 lowercase hexadecimal digits.</summary>
        private static bool IsAnIdTheGameMakes(string? text)
        {
            if (text == null || text.Length != 2 * IdBytes)
            {
                return false;
            }

            foreach (char c in text)
            {
                bool isADigit = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isADigit)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>The player's settings as the device file writes them: <c>{"sound":true,"vibration":true,"testerMode":false,"language":"en"}</c>.</summary>
    [Serializable]
    public sealed class SettingsDocument
    {
        public bool sound = DeviceSettings.Default.Sound;
        public bool vibration = DeviceSettings.Default.Vibration;

        /// <summary>The tester mode; in the file since layout 3.</summary>
        public bool testerMode = DeviceSettings.Default.TesterMode;

        /// <summary>
        /// The code of the language the player chose (<see cref="Languages.CodeOf"/>); in the file since layout 4. Empty
        /// for none: the game speaks the phone's. A code this game does not know (a later game's language) reads as none.
        /// </summary>
        public string language = string.Empty;

        /// <summary>The settings this block holds; a method, so that no JSON writer takes it for a field of the file.</summary>
        public DeviceSettings ToSettings() => new DeviceSettings(sound, vibration, testerMode, Languages.FromCode(language));

        public static SettingsDocument Of(DeviceSettings settings) => new SettingsDocument
        {
            sound = settings.Sound,
            vibration = settings.Vibration,
            testerMode = settings.TesterMode,
            language = settings.Language is Language chosen ? Languages.CodeOf(chosen) : string.Empty,
        };
    }

    /// <summary>What reading a device document came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class DeviceReading
    {
        private DeviceReading()
        {
        }

        /// <summary>A device document of a layout this game reads as its own.</summary>
        public sealed class Sound : DeviceReading
        {
            public Sound(DeviceId device, DeviceSettings settings)
            {
                Device = device;
                Settings = settings;
            }

            public DeviceId Device { get; }

            /// <summary>The settings on file; those of a phone that never set anything for a file from before the settings.</summary>
            public DeviceSettings Settings { get; }
        }

        /// <summary>A device document of a later layout, with its id if this game can read it, and its settings as far as they read.</summary>
        public sealed class OfALaterGame : DeviceReading
        {
            public OfALaterGame(int version, DeviceId? device, DeviceSettings settings)
            {
                Version = version;
                Device = device;
                Settings = settings;
            }

            public int Version { get; }

            /// <summary>The id the later game kept; nothing when this game cannot read it as an id it makes.</summary>
            public DeviceId? Device { get; }

            /// <summary>The settings the later game kept, as this game reads its block of them.</summary>
            public DeviceSettings Settings { get; }
        }

        /// <summary>Not a device document: no JSON, half a save, no layout, an id the game never makes.</summary>
        public sealed class Damaged : DeviceReading
        {
        }
    }
}
