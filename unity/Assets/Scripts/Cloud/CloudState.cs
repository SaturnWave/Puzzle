#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Puzzle.Client.Flow;
using Puzzle.Meta;

namespace Puzzle.Client.Cloud
{
    /// <summary>The id Unity's Authentication gives the player's account: what the cloud keeps the player's items under.</summary>
    public readonly struct CloudPlayerId : IEquatable<CloudPlayerId>
    {
        /// <exception cref="ArgumentException">An empty id.</exception>
        public CloudPlayerId(string value)
        {
            Value = string.IsNullOrEmpty(value) ? throw new ArgumentException("A player's id is not empty.", nameof(value)) : value;
        }

        public string Value { get; }

        public bool Equals(CloudPlayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is CloudPlayerId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value;

        public static bool operator ==(CloudPlayerId left, CloudPlayerId right) => left.Equals(right);

        public static bool operator !=(CloudPlayerId left, CloudPlayerId right) => !left.Equals(right);
    }

    /// <summary>
    /// The cloud's save as this phone last synced it: the base of the next merge (Cloud/ProgressMerge), with the write
    /// lock the next write gives back and the ids of the writes it comes from.
    /// </summary>
    public sealed class CloudBase
    {
        /// <param name="writeLock">The save's write lock; nothing when the cloud had no save.</param>
        public CloudBase(PlayerProgress progress, CloudWriteLock? writeLock, long revision, IEnumerable<string> writes)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            WriteLock = writeLock;
            Revision = revision;
            Writes = (writes ?? throw new ArgumentNullException(nameof(writes))).ToArray();
        }

        /// <summary>The cloud had no save: the base is a new player, who holds what the rules give one.</summary>
        public static CloudBase NoSave(Wallet startingWallet) => new CloudBase(PlayerProgress.NewPlayer(startingWallet), writeLock: null, revision: 0, Array.Empty<string>());

        /// <summary>A save the cloud holds, as it was read or written.</summary>
        public static CloudBase Of(CloudSave save, CloudWriteLock writeLock) => new CloudBase(save.Progress, writeLock, save.Revision, save.Writes);

        public PlayerProgress Progress { get; }

        /// <summary>The save's write lock; nothing when the cloud had no save at the last read.</summary>
        public CloudWriteLock? WriteLock { get; }

        public bool HasSave => WriteLock.HasValue;

        public long Revision { get; }

        /// <summary>The ids of the latest writes the save comes from, its own last (Cloud/CloudEnvelope's <c>writes</c>).</summary>
        public IReadOnlyList<string> Writes { get; }
    }

    /// <summary>
    /// A write of the save that has not been answered: kept before the write is sent, so that a game killed or cut off
    /// before the answer still knows what it wrote. The next read looks for its id among the save's writes: there, the
    /// write landed, and what it wrote is the base the cloud's save comes from.
    /// </summary>
    public sealed class PendingWrite
    {
        public PendingWrite(string id, PlayerProgress progress, long revision, IEnumerable<string> writes)
        {
            Id = string.IsNullOrEmpty(id) ? throw new ArgumentException("A write has an id.", nameof(id)) : id;
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            Revision = revision;
            Writes = (writes ?? throw new ArgumentNullException(nameof(writes))).ToArray();
        }

        public string Id { get; }

        public PlayerProgress Progress { get; }

        public long Revision { get; }

        /// <summary>The writes the save that was sent names, its own last.</summary>
        public IReadOnlyList<string> Writes { get; }

        /// <summary>The base the sync stands on if the write landed and nobody else wrote since.</summary>
        public CloudBase AsWritten(CloudWriteLock writeLock) => new CloudBase(Progress, writeLock, Revision, Writes);
    }

    /// <summary>Why the sync stopped for an account until the game is updated or the save is looked at. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class CloudHalt
    {
        private CloudHalt()
        {
        }

        /// <summary>The cloud's save does not read (§3.2): nothing is written over it, the log says why, support can look.</summary>
        public sealed class DamagedSave : CloudHalt
        {
            public DamagedSave(string why)
            {
                Why = why ?? string.Empty;
            }

            public string Why { get; }

            public override string ToString() => $"the cloud's save is damaged: {Why}";
        }

        /// <summary>A later version of the game wrote the save: this one does not write over it, and asks the player to update.</summary>
        public sealed class LaterGame : CloudHalt
        {
            public LaterGame(int progressLayout)
            {
                ProgressLayout = progressLayout;
            }

            public int ProgressLayout { get; }

            public override string ToString() => $"the cloud's save is of a later game (layout {ProgressLayout})";
        }
    }

    /// <summary>
    /// What the phone keeps about its sync with the cloud (cloud.json, docs/research/server-v1.md §3.3): the player's
    /// "Cloud save" switch, the installation's random writer id, the account it syncs with, the base of the next merge,
    /// when it last read and wrote, a write not yet answered, how it backs off after failures, and why it stopped, if it
    /// did. A value that never changes; every step gives another.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class CloudState
    {
        private CloudState(
            bool isSwitchedOn,
            string writer,
            CloudPlayerId? player,
            CloudBase? syncedBase,
            DateTimeOffset? lastRead,
            DateTimeOffset? lastWrite,
            PendingWrite? pending,
            int failures,
            DateTimeOffset? retryAt,
            CloudHalt? halt,
            bool linkOffered)
        {
            IsSwitchedOn = isSwitchedOn;
            Writer = string.IsNullOrEmpty(writer) ? throw new ArgumentException("An installation has a writer id.", nameof(writer)) : writer;
            Player = player;
            Base = syncedBase;
            LastRead = lastRead;
            LastWrite = lastWrite;
            Pending = pending;
            Failures = failures;
            RetryAt = retryAt;
            Halt = halt;
            LinkOffered = linkOffered;
        }

        /// <summary>
        /// The player's "Cloud save" switch in the settings (on unless the player turned it off): while it is off the
        /// game starts none of Unity's services, so no account, no sync, no verification (the owner's decision 1,
        /// 2026-10-26).
        /// </summary>
        public bool IsSwitchedOn { get; }

        /// <summary>The installation's random id, written into each save it writes for support (never the attempt records' device id).</summary>
        public string Writer { get; }

        /// <summary>The account the rest of the state is of; nothing before the first sign-in.</summary>
        public CloudPlayerId? Player { get; }

        /// <summary>The cloud's save as last synced; nothing while the account's cloud was never read (the next sync reads before it writes).</summary>
        public CloudBase? Base { get; }

        public DateTimeOffset? LastRead { get; }

        public DateTimeOffset? LastWrite { get; }

        /// <summary>The write that was sent and not answered; nothing when every write was answered.</summary>
        public PendingWrite? Pending { get; }

        /// <summary>The failures in a row since the cloud last answered.</summary>
        public int Failures { get; }

        /// <summary>No sync before this instant, but for a fresh sign-in (Cloud/SyncPolicy); nothing when the cloud answered last.</summary>
        public DateTimeOffset? RetryAt { get; }

        /// <summary>Why the sync stopped for the account; nothing while it goes on.</summary>
        public CloudHalt? Halt { get; }

        /// <summary>
        /// True once the installation offered to link the account to Google Play Games or Game Center (the owner's
        /// decision 2, 2026-10-26: once, after the first won level). Kept whatever account the phone signs in to; a
        /// forgotten state ("Delete my data", a damaged cloud.json) offers again, to the new account.
        /// </summary>
        public bool LinkOffered { get; }

        /// <summary>A new installation's state: the switch on, a new writer id, no account yet.</summary>
        public static CloudState New(string writer) => new CloudState(true, writer, null, null, null, null, null, 0, null, null, linkOffered: false);

        /// <summary>
        /// The state after the player's cloud data was deleted ("Delete my data", §5.6), or after cloud.json was found
        /// damaged: the switch off, a new writer id, nothing of any account. The player turns the switch on to sync again.
        /// </summary>
        public static CloudState Forgotten(string writer) => new CloudState(false, writer, null, null, null, null, null, 0, null, null, linkOffered: false);

        /// <summary>The state as cloud.json keeps it, every part given.</summary>
        public static CloudState Restored(
            bool isSwitchedOn, string writer, CloudPlayerId? player, CloudBase? syncedBase, DateTimeOffset? lastRead, DateTimeOffset? lastWrite,
            PendingWrite? pending, int failures, DateTimeOffset? retryAt, CloudHalt? halt, bool linkOffered = false) =>
            new CloudState(
                isSwitchedOn, writer, player, player.HasValue ? syncedBase : null, lastRead, lastWrite, player.HasValue ? pending : null, Math.Max(0, failures), retryAt, halt, linkOffered);

        /// <summary>The offer to link the account was made: it is not made again.</summary>
        public CloudState WithTheLinkOffered() => new CloudState(IsSwitchedOn, Writer, Player, Base, LastRead, LastWrite, Pending, Failures, RetryAt, Halt, linkOffered: true);

        /// <summary>The player turned the switch.</summary>
        public CloudState SwitchedTo(bool isOn) => new CloudState(isOn, Writer, Player, Base, LastRead, LastWrite, Pending, Failures, RetryAt, Halt, LinkOffered);

        /// <summary>
        /// The state for the account signed in now: this one when it is the account already synced, else a state with
        /// nothing of the other account (its base is not this account's, so the next sync reads first and merges with a
        /// new player as the base, §3.6).
        /// </summary>
        public CloudState ForPlayer(CloudPlayerId player) =>
            Player == player ? this : new CloudState(IsSwitchedOn, Writer, player, null, null, null, null, 0, null, null, LinkOffered);

        /// <summary>The cloud was read: its save is the base now, and the cloud answered.</summary>
        public CloudState AfterRead(CloudBase read, DateTimeOffset at) => new CloudState(IsSwitchedOn, Writer, Player, read, at, LastWrite, null, 0, null, null, LinkOffered);

        /// <summary>The save was written: what was written is the base now.</summary>
        public CloudState AfterWrite(CloudBase written, DateTimeOffset at) => new CloudState(IsSwitchedOn, Writer, Player, written, LastRead, at, null, 0, null, Halt, LinkOffered);

        /// <summary>A write is about to be sent.</summary>
        public CloudState Sending(PendingWrite write) => new CloudState(IsSwitchedOn, Writer, Player, Base, LastRead, LastWrite, write, Failures, RetryAt, Halt, LinkOffered);

        /// <summary>The cloud refused the write for a lock that was old: it is known not to have landed.</summary>
        public CloudState NotWritten() => new CloudState(IsSwitchedOn, Writer, Player, Base, LastRead, LastWrite, null, 0, null, Halt, LinkOffered);

        /// <summary>The cloud could not be reached; the next try waits until <paramref name="retryAt"/>, or not at all for nothing (no network).</summary>
        public CloudState AfterFailure(DateTimeOffset? retryAt) =>
            new CloudState(IsSwitchedOn, Writer, Player, Base, LastRead, LastWrite, Pending, Failures + 1, retryAt, Halt, LinkOffered);

        /// <summary>The sync stops for this account: nothing is written over the cloud's save.</summary>
        public CloudState Halted(CloudHalt halt, DateTimeOffset at) => new CloudState(IsSwitchedOn, Writer, Player, Base, at, LastWrite, null, 0, null, halt ?? throw new ArgumentNullException(nameof(halt)), LinkOffered);
    }

    /// <summary>Random ids for the sync: 128 bits from the system's source of secure randomness, as 32 lowercase hexadecimal digits.</summary>
    public static class RandomIds
    {
        private const int Bytes = 16;

        public static string New()
        {
            var bits = new byte[Bytes];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bits);
            }

            var digits = new StringBuilder(2 * Bytes);
            foreach (byte eight in bits)
            {
                digits.Append(eight.ToString("x2", CultureInfo.InvariantCulture));
            }

            return digits.ToString();
        }
    }
}
