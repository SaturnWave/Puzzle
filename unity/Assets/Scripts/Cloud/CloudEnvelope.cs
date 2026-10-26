#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Puzzle.Client.Flow;
using Puzzle.Meta;

namespace Puzzle.Client.Cloud
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the document's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// A player's save as the cloud keeps it: their progress, and what the cloud's code needs to know of it besides
    /// (docs/research/server-v1.md §3.2). The progress is the progress file's (Flow/ProgressDocument), so a save holds
    /// exactly what the phone keeps; the settings and the attempt records stay on the phone.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class CloudSave
    {
        /// <summary>How many of the latest writes a save names (<see cref="Writes"/>).</summary>
        public const int WritesKept = 16;

        /// <param name="writes">The ids of the latest writes this save comes from, the oldest first, its own last; only the latest <see cref="WritesKept"/> are kept.</param>
        /// <exception cref="ArgumentException">A revision below 0, no app version, or an empty id among the writes.</exception>
        public CloudSave(PlayerProgress progress, string appVersion, long revision, DateTimeOffset? writtenAt, string writer, IEnumerable<string> writes)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            AppVersion = string.IsNullOrEmpty(appVersion) ? throw new ArgumentException("A save names the version of the game that wrote it.", nameof(appVersion)) : appVersion;
            Revision = revision >= 0 ? revision : throw new ArgumentException($"A save's revision is never below 0, not {revision}.", nameof(revision));
            WrittenAt = writtenAt;
            Writer = writer ?? string.Empty;
            string[] ids = (writes ?? throw new ArgumentNullException(nameof(writes))).ToArray();
            if (ids.Any(string.IsNullOrEmpty))
            {
                throw new ArgumentException("A write's id is not empty.", nameof(writes));
            }

            Writes = ids.Skip(Math.Max(0, ids.Length - WritesKept)).ToArray();
        }

        public PlayerProgress Progress { get; }

        /// <summary>The version of the game that wrote the save ("1.1.0"), for support.</summary>
        public string AppVersion { get; }

        /// <summary>One more with every write, for the log and support; the cloud's write lock, not this, finds two writes that cross.</summary>
        public long Revision { get; }

        /// <summary>When the save was written, in UTC; nothing for a save whose time does not read (it is only for support).</summary>
        public DateTimeOffset? WrittenAt { get; }

        /// <summary>The random id of the installation that wrote the save (Cloud/CloudState.Writer), for support; never the attempt records' device id.</summary>
        public string Writer { get; }

        /// <summary>
        /// The random ids of the latest writes this save comes from, the oldest first and its own last: a device whose
        /// write had no answer finds out from them whether it was written, and whether a later save was merged with it
        /// (Cloud/CloudSync), so it never counts its own coins twice.
        /// </summary>
        public IReadOnlyList<string> Writes { get; }

        /// <summary>The id of the write that made this save; nothing for a save that names none.</summary>
        public string? LastWrite => Writes.Count > 0 ? Writes[Writes.Count - 1] : null;
    }

    /// <summary>
    /// The save's text in the cloud (the Cloud Save item "save" of the Player Data's default class):
    /// <c>{"envelope":1,"progressLayout":7,"appVersion":"1.1.0","revision":42,"writtenAtUtc":"2026-11-03T18:22:05Z","writer":"c1f0…","writes":["…","…"],"progress":{…}}</c>.
    /// A progress of <see cref="PackFromBytes"/> or more of JSON is packed: gzip, then base64, in
    /// <c>"progressGz"</c>, which then is the progress whatever <c>"progress"</c> holds. A reader that finds an
    /// envelope or a progress layout later than its own does not write over it (docs/research/server-v1.md §3.2).
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class CloudEnvelope
    {
        /// <summary>The envelope's layout this code writes.</summary>
        public const int CurrentEnvelope = 1;

        /// <summary>The size of the progress's JSON, in bytes of UTF-8, from which it is packed.</summary>
        public const int PackFromBytes = 16 * 1024;

        /// <summary>The most a packed progress may unpack to: the Cloud Save's own limit for one player's data. A packed text that unpacks to more is damaged, never read on.</summary>
        private const int UnpackedBytesAtMost = 5 * 1024 * 1024;

        private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        /// <summary>The save's text, its progress packed from <see cref="PackFromBytes"/> on.</summary>
        public static string Write(CloudSave save, IDocumentJson json)
        {
            ProgressDocument progress = ProgressDocument.Of(save.Progress);
            string progressText = json.Write(progress);
            bool packs = Encoding.UTF8.GetByteCount(progressText) >= PackFromBytes;
            return json.Write(new CloudSaveDocument
            {
                envelope = CurrentEnvelope,
                progressLayout = ProgressDocument.CurrentVersion,
                appVersion = save.AppVersion,
                revision = save.Revision,
                writtenAtUtc = save.WrittenAt is DateTimeOffset at ? at.UtcDateTime.ToString(UtcFormat, CultureInfo.InvariantCulture) : string.Empty,
                writer = save.Writer,
                writes = save.Writes.ToArray(),
                progress = packs ? null : progress,
                progressGz = packs ? Packed(progressText) : string.Empty,
            });
        }

        /// <summary>What a save's text is, as far as this version of the game can tell.</summary>
        /// <param name="startingWallet">What the rules give a new player: the wallet of a progress of a layout before the wallet.</param>
        public static CloudSaveReading Read(string text, IDocumentJson json, Wallet startingWallet)
        {
            CloudSaveDocument? document = json.Read<CloudSaveDocument>(text ?? string.Empty);
            if (document == null || document.envelope < 1)
            {
                return new CloudSaveReading.Damaged("the save is not an envelope this game writes");
            }

            if (document.envelope > CurrentEnvelope)
            {
                return new CloudSaveReading.OfALaterGame(document.envelope, document.progressLayout, understood: null);
            }

            ProgressDocument? progress = document.progress;
            if (!string.IsNullOrEmpty(document.progressGz))
            {
                string? unpacked = Unpacked(document.progressGz);
                progress = unpacked == null ? null : json.Read<ProgressDocument>(unpacked);
                if (progress == null)
                {
                    return new CloudSaveReading.Damaged("the packed progress does not unpack to a progress document");
                }
            }

            switch (ProgressDocument.Read(progress, startingWallet))
            {
                case ProgressReading.Sound sound when document.revision >= 0:
                    var save = new CloudSave(sound.Progress, NonEmpty(document.appVersion), document.revision, InstantOf(document.writtenAtUtc), document.writer ?? string.Empty, IdsOf(document.writes));
                    return new CloudSaveReading.Sound(save, sound.SetAside);
                case ProgressReading.OfALaterGame later:
                    return new CloudSaveReading.OfALaterGame(document.envelope, later.Version, later.Understood);
                default:
                    return new CloudSaveReading.Damaged("the progress in the save is not one this game can have written");
            }
        }

        /// <summary>The text as gzip, in base64.</summary>
        private static string Packed(string text)
        {
            using (var packed = new MemoryStream())
            {
                using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(text);
                    gzip.Write(bytes, 0, bytes.Length);
                }

                return Convert.ToBase64String(packed.ToArray());
            }
        }

        /// <summary>The text a packed progress holds; nothing when it is not base64, not gzip, or unpacks to more than a player's data can be.</summary>
        private static string? Unpacked(string base64)
        {
            byte[] packed;
            try
            {
                packed = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }

            try
            {
                using (var gzip = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress))
                using (var unpacked = new MemoryStream())
                {
                    var buffer = new byte[16 * 1024];
                    int read;
                    while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (unpacked.Length + read > UnpackedBytesAtMost)
                        {
                            return null;
                        }

                        unpacked.Write(buffer, 0, read);
                    }

                    return Encoding.UTF8.GetString(unpacked.ToArray());
                }
            }
            catch (InvalidDataException)
            {
                return null; // not gzip, or cut short
            }
        }

        private static DateTimeOffset? InstantOf(string? utc) =>
            DateTimeOffset.TryParseExact(utc, UtcFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset at)
                ? at
                : (DateTimeOffset?)null;

        /// <summary>The ids that are there, in their order; an empty one is no id.</summary>
        private static IEnumerable<string> IdsOf(string[]? writes) => (writes ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id));

        /// <summary>The version a save names, or a word for none: a save is not thrown away for a word written only for support.</summary>
        private static string NonEmpty(string? appVersion) => string.IsNullOrEmpty(appVersion) ? "unknown" : appVersion;
    }

    /// <summary>The save's text as Unity's JSON reader maps it (<see cref="CloudEnvelope"/> says what each field is).</summary>
    [Serializable]
    public sealed class CloudSaveDocument
    {
        public int envelope;
        public int progressLayout;
        public string appVersion = string.Empty;
        public long revision;
        public string writtenAtUtc = string.Empty;
        public string writer = string.Empty;
        public string[] writes = Array.Empty<string>();

        /// <summary>The progress, unless it is packed in <see cref="progressGz"/>.</summary>
        public ProgressDocument? progress;

        /// <summary>The progress's JSON as gzip in base64; empty when it is not packed.</summary>
        public string progressGz = string.Empty;
    }

    /// <summary>What reading a save's text came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class CloudSaveReading
    {
        private CloudSaveReading()
        {
        }

        /// <summary>A save this game reads as its own.</summary>
        public sealed class Sound : CloudSaveReading
        {
            /// <param name="setAside">The lines of its purchases' block that were set aside, each with a word on why; none for a save a game wrote.</param>
            public Sound(CloudSave save, IReadOnlyList<string> setAside)
            {
                Save = save ?? throw new ArgumentNullException(nameof(save));
                SetAside = setAside ?? throw new ArgumentNullException(nameof(setAside));
            }

            public CloudSave Save { get; }

            public IReadOnlyList<string> SetAside { get; }
        }

        /// <summary>A save a later version of the game wrote: this game does not write over it, and shows what it can read of it.</summary>
        public sealed class OfALaterGame : CloudSaveReading
        {
            public OfALaterGame(int envelope, int progressLayout, PlayerProgress? understood)
            {
                Envelope = envelope;
                ProgressLayout = progressLayout;
                Understood = understood;
            }

            public int Envelope { get; }

            public int ProgressLayout { get; }

            /// <summary>Where the player stands in it, as far as this game reads it; nothing when it reads not even that (a later envelope is not opened).</summary>
            public PlayerProgress? Understood { get; }
        }

        /// <summary>Not a save any game writes: no JSON, no envelope, a packed progress that does not unpack, a progress no game has.</summary>
        public sealed class Damaged : CloudSaveReading
        {
            public Damaged(string why)
            {
                Why = why ?? string.Empty;
            }

            /// <summary>Why, for the log; never shown to a player.</summary>
            public string Why { get; }
        }
    }
}
