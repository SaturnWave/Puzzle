using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>A player's ledger as read, with the write lock to write it back under; no lock for a player who has none yet.</summary>
    public sealed class LedgerRead
    {
        public LedgerRead(Ledger ledger, WriteLock? writeLock)
        {
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            WriteLock = writeLock;
        }

        public Ledger Ledger { get; }

        public WriteLock? WriteLock { get; }
    }

    /// <summary>What a change of a ledger came to: the ledger to write (nothing when there is nothing to write) and what the change found.</summary>
    public sealed class LedgerChange<T>
    {
        internal LedgerChange(Ledger? changed, T result)
        {
            Changed = changed;
            Result = result;
        }

        public Ledger? Changed { get; }

        public T Result { get; }
    }

    /// <summary>The two kinds of <see cref="LedgerChange{T}"/>.</summary>
    public static class LedgerChange
    {
        public static LedgerChange<T> To<T>(Ledger changed, T result) => new LedgerChange<T>(changed ?? throw new ArgumentNullException(nameof(changed)), result);

        public static LedgerChange<T> Nothing<T>(T result) => new LedgerChange<T>(null, result);
    }

    /// <summary>
    /// How the module changes the documents it keeps: read, change, write back under the write lock of the read, and
    /// when another write came between (another call of the same player, the daily job), read again and change that.
    /// A few tries, never a loop: a document that keeps changing under the module is Cloud Save's fault for now.
    /// </summary>
    public static class Keeping
    {
        public const int Tries = 3;

        /// <exception cref="ServerStoreUnavailableException">Cloud Save could not be reached.</exception>
        /// <exception cref="ServerDocumentException">The ledger is not one the module writes.</exception>
        public static async Task<LedgerRead> ReadLedgerAsync(IServerStore store, PlayerId player, CancellationToken cancellation)
        {
            StoredText? stored = await store.ReadLedgerAsync(player, cancellation).ConfigureAwait(false);
            return stored == null ? new LedgerRead(Ledger.Empty, null) : new LedgerRead(ServerDocuments.LedgerFrom(stored.Text), stored.WriteLock);
        }

        /// <summary>Changes the ledger from the read given, and from a new read after every conflict.</summary>
        /// <exception cref="ServerStoreUnavailableException">Cloud Save could not be reached, or the ledger changed under every try.</exception>
        /// <exception cref="ServerDocumentException">The ledger is not one the module writes.</exception>
        public static async Task<T> ChangeLedgerAsync<T>(IServerStore store, PlayerId player, LedgerRead first, Func<Ledger, LedgerChange<T>> change, CancellationToken cancellation)
        {
            LedgerRead read = first;
            for (int attempt = 1; ; attempt++)
            {
                LedgerChange<T> changed = change(read.Ledger);
                if (changed.Changed == null)
                {
                    return changed.Result;
                }

                LockedWrite written = await store.WriteLedgerAsync(player, ServerDocuments.TextOf(changed.Changed), read.WriteLock, cancellation).ConfigureAwait(false);
                if (written is LockedWrite.Written)
                {
                    return changed.Result;
                }

                if (attempt == Tries)
                {
                    throw new ServerStoreUnavailableException($"The ledger of {player} changed under each of {Tries} writes.");
                }

                read = await ReadLedgerAsync(store, player, cancellation).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Changes a receipt's record: <paramref name="change"/> takes the record (nothing for a receipt not recorded)
        /// and gives the record to write, or nothing to leave it. Gives the record as it stands afterwards.
        /// </summary>
        /// <exception cref="ServerStoreUnavailableException">Cloud Save could not be reached, or the record changed under every try.</exception>
        /// <exception cref="ServerDocumentException">The record is not one the module writes.</exception>
        public static async Task<ReceiptRecord?> ChangeReceiptAsync(IServerStore store, ReceiptId receipt, Func<ReceiptRecord?, ReceiptRecord?> change, CancellationToken cancellation)
        {
            for (int attempt = 1; ; attempt++)
            {
                StoredText? stored = await store.ReadReceiptAsync(receipt, cancellation).ConfigureAwait(false);
                ReceiptRecord? record = stored == null ? null : ServerDocuments.ReceiptFrom(stored.Text);
                if (!(change(record) is ReceiptRecord changed))
                {
                    return record;
                }

                LockedWrite written = await store.WriteReceiptAsync(receipt, ServerDocuments.TextOf(changed), stored?.WriteLock, cancellation).ConfigureAwait(false);
                if (written is LockedWrite.Written)
                {
                    return changed;
                }

                if (attempt == Tries)
                {
                    throw new ServerStoreUnavailableException($"The record of {receipt} changed under each of {Tries} writes.");
                }
            }
        }

        /// <summary>The receipt's record; nothing for a receipt the server never recorded.</summary>
        /// <exception cref="ServerStoreUnavailableException">Cloud Save could not be reached.</exception>
        /// <exception cref="ServerDocumentException">The record is not one the module writes.</exception>
        public static async Task<ReceiptRecord?> ReadReceiptAsync(IServerStore store, ReceiptId receipt, CancellationToken cancellation)
        {
            StoredText? stored = await store.ReadReceiptAsync(receipt, cancellation).ConfigureAwait(false);
            return stored == null ? null : ServerDocuments.ReceiptFrom(stored.Text);
        }
    }

    /// <summary>The registry's changes that follow a ledger's: the ledger is the player's truth, the registry its index.</summary>
    public static class Registry
    {
        /// <summary>
        /// Marks the receipt's record with the state its ledger entry has now (a refund, a voiding). A record not there,
        /// or one that says so already, is left. A Cloud Save that cannot be reached is logged and passed over: the
        /// ledger is marked already, and a later look marks the record.
        /// </summary>
        /// <exception cref="ServerDocumentException">The record is not one the module writes.</exception>
        public static async Task MarkAsync(IServerStore store, ReceiptId receipt, LedgerState state, ILogger log, CancellationToken cancellation)
        {
            try
            {
                await Keeping.ChangeReceiptAsync(store, receipt, record => record == null || record.State == state ? null : record.WithState(state), cancellation).ConfigureAwait(false);
            }
            catch (ServerStoreUnavailableException unavailable)
            {
                log.LogWarning(unavailable, "The registry's record {Receipt} could not be marked {State}.", receipt, state);
            }
        }
    }
}
