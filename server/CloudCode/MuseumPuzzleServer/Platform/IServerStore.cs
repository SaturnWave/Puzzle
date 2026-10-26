using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;

namespace MuseumPuzzle.Server.Platform
{
    /// <summary>Cloud Save's version token of an item ("write lock"): handed out with every read and write, given back with the next write, which Cloud Save refuses if anyone wrote in between.</summary>
    public readonly struct WriteLock : IEquatable<WriteLock>
    {
        /// <exception cref="ArgumentException">An empty token: Cloud Save never hands one out.</exception>
        public WriteLock(string value)
        {
            Value = string.IsNullOrEmpty(value) ? throw new ArgumentException("A write lock is not empty.", nameof(value)) : value;
        }

        public string Value { get; }

        public bool Equals(WriteLock other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is WriteLock other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value;

        public static bool operator ==(WriteLock left, WriteLock right) => left.Equals(right);

        public static bool operator !=(WriteLock left, WriteLock right) => !left.Equals(right);
    }

    /// <summary>A document Cloud Save holds: its text, and its write lock as of the read.</summary>
    public sealed class StoredText
    {
        public StoredText(string text, WriteLock writeLock)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            WriteLock = writeLock;
        }

        public string Text { get; }

        public WriteLock WriteLock { get; }
    }

    /// <summary>What a write under a write lock came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class LockedWrite
    {
        private LockedWrite()
        {
        }

        /// <summary>Written; the document's new write lock.</summary>
        public sealed class Written : LockedWrite
        {
            public Written(WriteLock writeLock)
            {
                WriteLock = writeLock;
            }

            public WriteLock WriteLock { get; }
        }

        /// <summary>Not written: someone wrote the document since the lock that was given. Nothing changed.</summary>
        public sealed class Conflict : LockedWrite
        {
        }
    }

    /// <summary>
    /// Cloud Save could not be reached, or refused this module's request. The message says which call; the cause is
    /// the SDK's own exception. The use cases answer such a call with "unavailable" and the game tries again later;
    /// the raw error goes to Cloud Code's log, never to the player.
    /// </summary>
    public sealed class ServerStoreUnavailableException : Exception
    {
        public ServerStoreUnavailableException(string message)
            : base(message)
        {
        }

        public ServerStoreUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// The documents this module keeps in Cloud Save (docs/research/server-v1.md §3.1, §4.5), always written with the
    /// module's service token:
    /// <list type="bullet">
    /// <item>a player's ledger, the Player Data item "purchases" of the Protected class (the player reads it, only the server writes it);</item>
    /// <item>a receipt's record, the custom item "rcpt-…" of the Private class (only the server reads or writes it);</item>
    /// <item>the receipts whose trace began on a day, the Private custom item "forgotten-YYYY-MM-DD", which the daily retention job empties 36 months later;</item>
    /// <item>where the daily look at Google's voided purchases has come to, in the Private custom item "server-state".</item>
    /// </list>
    /// Every method throws <see cref="ServerStoreUnavailableException"/> when Cloud Save cannot be reached or refuses.
    /// A write with no write lock creates the document, or overwrites it: Cloud Save has no "write only if absent".
    /// </summary>
    public interface IServerStore
    {
        /// <summary>The player's ledger; nothing for a player with no ledger yet.</summary>
        Task<StoredText?> ReadLedgerAsync(PlayerId player, CancellationToken cancellation);

        Task<LockedWrite> WriteLedgerAsync(PlayerId player, string text, WriteLock? expected, CancellationToken cancellation);

        /// <summary>The receipt's record; nothing for a receipt the server never recorded.</summary>
        Task<StoredText?> ReadReceiptAsync(ReceiptId receipt, CancellationToken cancellation);

        Task<LockedWrite> WriteReceiptAsync(ReceiptId receipt, string text, WriteLock? expected, CancellationToken cancellation);

        /// <summary>Deletes the receipt's record; nothing happens for one that is not there.</summary>
        Task DeleteReceiptAsync(ReceiptId receipt, CancellationToken cancellation);

        /// <summary>Deletes every item the player has in every access class of Player Data ("save", "purchases" and whatever else); nothing happens for a player who has none.</summary>
        Task DeletePlayerDataAsync(PlayerId player, CancellationToken cancellation);

        /// <summary>Notes that the receipt's trace began on this day.</summary>
        Task NoteTraceAsync(DateOnly day, ReceiptId receipt, CancellationToken cancellation);

        /// <summary>The receipts whose trace began on this day; none for a day with none.</summary>
        Task<IReadOnlyList<ReceiptId>> TracesOfAsync(DateOnly day, CancellationToken cancellation);

        /// <summary>Deletes the day's note of traces; nothing happens for a day with none.</summary>
        Task DeleteTracesOfAsync(DateOnly day, CancellationToken cancellation);

        /// <summary>Up to when Google's list of voided purchases was read through; nothing before the first complete look.</summary>
        Task<DateTimeOffset?> ReadVoidedSeenUntilAsync(CancellationToken cancellation);

        Task WriteVoidedSeenUntilAsync(DateTimeOffset until, CancellationToken cancellation);
    }
}
