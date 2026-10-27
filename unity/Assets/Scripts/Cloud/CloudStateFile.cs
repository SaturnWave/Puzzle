#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Client.Flow;
using Puzzle.Meta;

namespace Puzzle.Client.Cloud
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the file's layout: Unity's JSON reader maps fields, not properties

    /// <summary>Where the phone keeps its sync's state between two runs of the game (cloud.json).</summary>
    public interface ICloudStateStore
    {
        /// <summary>The state that was saved last; a new installation's when none ever was.</summary>
        /// <exception cref="IOException">The store is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read the store.</exception>
        CloudStateLoad Load();

        /// <exception cref="IOException">The store could not be written; what it held before is still there.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not write the store; what it held before is still there.</exception>
        void Save(CloudState state);
    }

    /// <summary>What loading the sync's state came to: the state, and whether the file was damaged (moved aside; the state is then a forgotten one, the switch off).</summary>
    public sealed class CloudStateLoad
    {
        public CloudStateLoad(CloudState state, bool wasDamaged)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            WasDamaged = wasDamaged;
        }

        public CloudState State { get; }

        /// <summary>True when the file did not read: it was kept under another name for support, and the state starts over with the switch off.</summary>
        public bool WasDamaged { get; }
    }

    /// <summary>
    /// The sync's state as a small JSON file beside the progress file (docs/research/server-v1.md §3.3). Like the
    /// progress file it is written beside itself and then put in its own place, so a game killed in a save leaves the
    /// old state or the new one. A file that does not read is moved aside, and the state starts over <b>with the
    /// switch off</b>: the player's "off" must never turn into an "on" by accident, and an "on" lost so is turned on
    /// again with one touch (§6.2). The progress the state keeps (the base, a write not answered) is read as the
    /// progress file reads it; one that does not read is dropped, and the next sync reads the cloud before it writes.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class CloudStateFile : ICloudStateStore
    {
        public const string FileName = "cloud.json";

        private const string NewFileSuffix = ".new";
        private const string DamagedFileSuffix = ".damaged";

        private readonly string _path;
        private readonly IDocumentJson _json;
        private readonly Wallet _startingWallet;
        private readonly Func<string> _newId;

        /// <param name="folder">The folder the file lies in: the progress file's.</param>
        /// <param name="startingWallet">What the rules give a new player: the wallet of a kept progress of a layout before the wallet.</param>
        /// <param name="newId">Where a new installation's writer id comes from (<see cref="RandomIds.New"/>).</param>
        public CloudStateFile(string folder, IDocumentJson json, Wallet startingWallet, Func<string> newId)
        {
            _path = Path.Combine(folder ?? throw new ArgumentNullException(nameof(folder)), FileName);
            _json = json ?? throw new ArgumentNullException(nameof(json));
            _startingWallet = startingWallet ?? throw new ArgumentNullException(nameof(startingWallet));
            _newId = newId ?? throw new ArgumentNullException(nameof(newId));
        }

        public CloudStateLoad Load()
        {
            if (!File.Exists(_path))
            {
                return new CloudStateLoad(CloudState.New(_newId()), wasDamaged: false);
            }

            CloudState? state = CloudStateDocument.Read(_json.Read<CloudStateDocument>(File.ReadAllText(_path)), _json, _startingWallet);
            if (state != null)
            {
                return new CloudStateLoad(state, wasDamaged: false);
            }

            string aside = _path + DamagedFileSuffix;
            File.Delete(aside);
            File.Move(_path, aside);
            return new CloudStateLoad(CloudState.Forgotten(_newId()), wasDamaged: true);
        }

        public void Save(CloudState state)
        {
            string newFile = _path + NewFileSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            File.WriteAllText(newFile, _json.Write(CloudStateDocument.Of(state)));
            if (File.Exists(_path))
            {
                File.Replace(newFile, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(newFile, _path);
            }
        }
    }

    /// <summary>A sync's state in memory: for the tests and the Editor's checks, which must not touch a player's own files.</summary>
    public sealed class CloudStateInMemory : ICloudStateStore
    {
        private CloudState _kept;

        public CloudStateInMemory(CloudState start)
        {
            _kept = start ?? throw new ArgumentNullException(nameof(start));
        }

        public int TimesSaved { get; private set; }

        public CloudStateLoad Load() => new CloudStateLoad(_kept, wasDamaged: false);

        public void Save(CloudState state)
        {
            _kept = state ?? throw new ArgumentNullException(nameof(state));
            TimesSaved++;
        }
    }

    /// <summary>
    /// The sync's state as cloud.json writes it:
    /// <c>{"version":1,"switchedOn":true,"writer":"…","player":"…","synced":{"known":true,"saved":true,"progress":{…},"writeLock":"…","revision":42,"writes":["…"]},
    /// "lastReadUnixSeconds":…,"lastWriteUnixSeconds":0,"pending":{"id":"","progress":{…},"revision":0,"writes":[]},"failures":0,"retryAtUnixSeconds":0,"halt":"","haltWhy":"","haltLayout":0,
    /// "linkOffered":false,"ledger":{"known":true,"text":"{…}","readAtUnixSeconds":…,"stale":false}}</c>. A file written before
    /// <c>linkOffered</c> (Stage 2 of the server plan, 2026-10-26) reads it as false, and one written before <c>ledger</c>
    /// (Stage 4, 2026-10-27) as a ledger never read: the next merge reads it.
    /// An instant is whole seconds since 1970 (UTC), 0 for none; an empty text is none. Unity's reader writes every
    /// block whether it holds anything or not, so a block says so itself (<c>known</c>, <c>saved</c>, an empty id).
    /// </summary>
    [Serializable]
    public sealed class CloudStateDocument
    {
        public const int CurrentVersion = 1;

        private const string DamagedSaveWord = "damaged-save";
        private const string LaterGameWord = "later-game";

        public int version;
        public bool switchedOn;
        public string writer = string.Empty;
        public string player = string.Empty;
        public SyncedDocument synced = new SyncedDocument();
        public long lastReadUnixSeconds;
        public long lastWriteUnixSeconds;
        public PendingDocument pending = new PendingDocument();
        public int failures;
        public long retryAtUnixSeconds;
        public string halt = string.Empty;
        public string haltWhy = string.Empty;
        public int haltLayout;
        public bool linkOffered;
        public LedgerDocument ledger = new LedgerDocument();

        public static CloudStateDocument Of(CloudState state) => new CloudStateDocument
        {
            version = CurrentVersion,
            switchedOn = state.IsSwitchedOn,
            writer = state.Writer,
            player = state.Player?.Value ?? string.Empty,
            synced = SyncedDocument.Of(state.Base),
            lastReadUnixSeconds = SecondsOf(state.LastRead),
            lastWriteUnixSeconds = SecondsOf(state.LastWrite),
            pending = PendingDocument.Of(state.Pending),
            failures = state.Failures,
            retryAtUnixSeconds = SecondsOf(state.RetryAt),
            halt = state.Halt switch
            {
                null => string.Empty,
                CloudHalt.DamagedSave _ => DamagedSaveWord,
                CloudHalt.LaterGame _ => LaterGameWord,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state.Halt, "A halt the file cannot write."),
            },
            haltWhy = state.Halt is CloudHalt.DamagedSave damaged ? damaged.Why : string.Empty,
            haltLayout = state.Halt is CloudHalt.LaterGame later ? later.ProgressLayout : 0,
            linkOffered = state.LinkOffered,
            ledger = LedgerDocument.Of(state.Ledger),
        };

        /// <summary>
        /// The state a document holds; nothing for one no game wrote (no layout, no writer id). A later layout is read
        /// for what this game knows of it, later layouts adding to this one. What the state keeps of an account that
        /// does not read (a base or a pending write whose progress no game has) is dropped, the rest kept: the next
        /// sync reads the cloud first.
        /// </summary>
        public static CloudState? Read(CloudStateDocument? document, IDocumentJson json, Wallet startingWallet)
        {
            if (document == null || document.version < 1 || string.IsNullOrEmpty(document.writer))
            {
                return null;
            }

            CloudPlayerId? player = string.IsNullOrEmpty(document.player) ? (CloudPlayerId?)null : new CloudPlayerId(document.player);
            CloudBase? synced = document.synced?.Base(startingWallet);
            PendingWrite? pending = document.pending?.Write(startingWallet);
            CloudHalt? halt = document.halt switch
            {
                DamagedSaveWord => new CloudHalt.DamagedSave(document.haltWhy ?? string.Empty),
                LaterGameWord => new CloudHalt.LaterGame(document.haltLayout),
                _ => null,
            };
            return CloudState.Restored(
                document.switchedOn, document.writer, player, synced, InstantOf(document.lastReadUnixSeconds), InstantOf(document.lastWriteUnixSeconds),
                pending, document.failures, InstantOf(document.retryAtUnixSeconds), halt, document.linkOffered, document.ledger?.Copy());
        }

        internal static long SecondsOf(DateTimeOffset? instant) => instant?.ToUnixTimeSeconds() ?? 0;

        internal static DateTimeOffset? InstantOf(long seconds)
        {
            bool isOnTheCalendar = seconds > 0 && seconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds();
            return isOnTheCalendar ? DateTimeOffset.FromUnixTimeSeconds(seconds) : (DateTimeOffset?)null;
        }

        internal static PlayerProgress? ProgressOf(ProgressDocument? document, Wallet startingWallet) =>
            ProgressDocument.Read(document, startingWallet) is ProgressReading.Sound sound ? sound.Progress : null;

        internal static string[] IdsOf(string[]? ids) => (ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).ToArray();
    }

    /// <summary>The base of the next merge, as cloud.json writes it.</summary>
    [Serializable]
    public sealed class SyncedDocument
    {
        /// <summary>Whether the account's cloud was read at all; false, and the rest says nothing.</summary>
        public bool known;

        /// <summary>Whether the cloud held a save then; false, and the base is a new player.</summary>
        public bool saved;

        public ProgressDocument progress = new ProgressDocument();
        public string writeLock = string.Empty;
        public long revision;
        public string[] writes = Array.Empty<string>();

        public static SyncedDocument Of(CloudBase? synced) => synced == null ? new SyncedDocument() : new SyncedDocument
        {
            known = true,
            saved = synced.HasSave,
            progress = ProgressDocument.Of(synced.Progress),
            writeLock = synced.WriteLock?.Value ?? string.Empty,
            revision = synced.Revision,
            writes = synced.Writes.ToArray(),
        };

        /// <summary>The base the block holds; nothing when none is known, or when its progress or lock does not read.</summary>
        public CloudBase? Base(Wallet startingWallet)
        {
            if (!known)
            {
                return null;
            }

            if (!saved)
            {
                return CloudBase.NoSave(startingWallet);
            }

            PlayerProgress? read = CloudStateDocument.ProgressOf(progress, startingWallet);
            return read != null && !string.IsNullOrEmpty(writeLock)
                ? new CloudBase(read, new CloudWriteLock(writeLock), Math.Max(0, revision), CloudStateDocument.IdsOf(writes))
                : null;
        }
    }

    /// <summary>The ledger as last read, as cloud.json writes it; not <c>known</c> for one never read, an empty text for a player who had none.</summary>
    [Serializable]
    public sealed class LedgerDocument
    {
        public bool known;
        public string text = string.Empty;
        public long readAtUnixSeconds;
        public bool stale;

        public static LedgerDocument Of(LedgerCopy? copy) => copy == null ? new LedgerDocument() : new LedgerDocument
        {
            known = true,
            text = copy.Text ?? string.Empty,
            readAtUnixSeconds = CloudStateDocument.SecondsOf(copy.ReadAt),
            stale = copy.IsStale,
        };

        /// <summary>The copy the block holds; nothing for one never read, or one without the time it was read.</summary>
        public LedgerCopy? Copy() =>
            known && CloudStateDocument.InstantOf(readAtUnixSeconds) is DateTimeOffset readAt ? new LedgerCopy(text, readAt, stale) : null;
    }

    /// <summary>A write not answered, as cloud.json writes it; an empty id for none.</summary>
    [Serializable]
    public sealed class PendingDocument
    {
        public string id = string.Empty;
        public ProgressDocument progress = new ProgressDocument();
        public long revision;
        public string[] writes = Array.Empty<string>();

        public static PendingDocument Of(PendingWrite? pending) => pending == null ? new PendingDocument() : new PendingDocument
        {
            id = pending.Id,
            progress = ProgressDocument.Of(pending.Progress),
            revision = pending.Revision,
            writes = pending.Writes.ToArray(),
        };

        /// <summary>The write the block holds; nothing for none, or for one whose progress does not read.</summary>
        public PendingWrite? Write(Wallet startingWallet) =>
            !string.IsNullOrEmpty(id) && CloudStateDocument.ProgressOf(progress, startingWallet) is PlayerProgress read
                ? new PendingWrite(id, read, Math.Max(0, revision), CloudStateDocument.IdsOf(writes))
                : null;
    }
}
