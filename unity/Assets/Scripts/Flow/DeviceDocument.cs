#nullable enable
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Puzzle.Core.Recording;

namespace Puzzle.Client.Flow
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the file's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// What the game keeps about the phone itself, as device.json writes it: <c>{"version":1,"id":"6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a"}</c>.
    /// The id is 128 random bits, written as 32 lowercase hexadecimal digits, made on the game's first start; it is the
    /// only identity the phone's attempt records carry (Puzzle.Core's DeviceId, docs/GAME_DESIGN.md §20), and nothing
    /// about the player or the phone goes into it. The settings join this file in a later layout. The fields are public
    /// and named as the document names them, because that is how Unity's JSON reader maps a document; it makes an empty
    /// text of an id a document does not have, so a document is read by its layout's number and its id's spelling.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    [Serializable]
    public sealed class DeviceDocument
    {
        /// <summary>The layout this code writes. A file of a later layout is not guessed at.</summary>
        public const int CurrentVersion = 1;

        private const int FirstVersion = 1;
        private const int IdBytes = 16;

        public int version;
        public string id = string.Empty;

        /// <summary>The document that keeps a device's id, in the current layout.</summary>
        public static DeviceDocument Of(DeviceId device) => new DeviceDocument { version = CurrentVersion, id = device.Value };

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
            if (document.version > CurrentVersion)
            {
                // Later layouts are expected to add to this one: the id is looked for in them too.
                return new DeviceReading.OfALaterGame(document.version, device);
            }

            return device != null ? new DeviceReading.Sound(device) : (DeviceReading)new DeviceReading.Damaged();
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

    /// <summary>What reading a device document came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class DeviceReading
    {
        private DeviceReading()
        {
        }

        /// <summary>A device document of a layout this game reads as its own.</summary>
        public sealed class Sound : DeviceReading
        {
            public Sound(DeviceId device)
            {
                Device = device;
            }

            public DeviceId Device { get; }
        }

        /// <summary>A device document of a later layout, with its id if this game can read it.</summary>
        public sealed class OfALaterGame : DeviceReading
        {
            public OfALaterGame(int version, DeviceId? device)
            {
                Version = version;
                Device = device;
            }

            public int Version { get; }

            /// <summary>The id the later game kept; nothing when this game cannot read it as an id it makes.</summary>
            public DeviceId? Device { get; }
        }

        /// <summary>Not a device document: no JSON, half a save, no layout, an id the game never makes.</summary>
        public sealed class Damaged : DeviceReading
        {
        }
    }
}
