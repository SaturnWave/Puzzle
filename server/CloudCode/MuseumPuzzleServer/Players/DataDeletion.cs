using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Purchases;

namespace MuseumPuzzle.Server.Players
{
    /// <summary>
    /// "Delete my data" (the endpoint DeleteMyData; docs/research/server-v1.md §5.6). Unity's account deletion deletes
    /// only the account, so the module deletes the rest before the game deletes the account:
    /// <list type="number">
    /// <item>every receipt record of the player's ledger lets go of the player; a record the player was the last
    /// to hold keeps no player at all, only its digest, product, date and state, as the trace the owner decided to
    /// keep for 36 months against fraud (decision 5, 2026-10-26), noted under today for the retention job;</item>
    /// <item>every Player Data item of the player is deleted, in every access class: the save, the ledger and
    /// whatever else.</item>
    /// </list>
    /// Each step can be done again without harm, so a call that stops halfway is finished by the next. The order keeps
    /// it so: the day's note is written before the record that it names, and the player's items go last.
    /// </summary>
    public sealed class DataDeletion
    {
        private readonly IClock _clock;
        private readonly ILogger _log;

        public DataDeletion(IClock clock, ILogger log)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<DeletionOutcome> DeleteAsync(PlayerId player, IServerStore store, CancellationToken cancellation)
        {
            DateOnly today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
            try
            {
                foreach (ReceiptId receipt in await ReceiptsOfAsync(player, store, cancellation).ConfigureAwait(false))
                {
                    await ForgetAsync(player, receipt, today, store, cancellation).ConfigureAwait(false);
                }

                await store.DeletePlayerDataAsync(player, cancellation).ConfigureAwait(false);
                _log.LogInformation("The data of {Player} was deleted on request.", player);
                return new DeletionOutcome.Deleted();
            }
            catch (ServerStoreUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "Cloud Save could not be reached while the data of {Player} was deleted; the next call finishes it.", player);
                return new DeletionOutcome.Unavailable(UnavailableReason.CloudSaveUnreachable);
            }
        }

        /// <summary>
        /// The receipts of the player's ledger that the registry may name the player in (an invalid entry never
        /// reaches the registry). A ledger the module cannot read names none: its data is deleted all the same, and
        /// the log tells the owner to look for the player's id in the registry by hand.
        /// </summary>
        private async Task<ReceiptId[]> ReceiptsOfAsync(PlayerId player, IServerStore store, CancellationToken cancellation)
        {
            try
            {
                LedgerRead read = await Keeping.ReadLedgerAsync(store, player, cancellation).ConfigureAwait(false);
                return read.Ledger.Entries.Where(entry => entry.State != LedgerState.Invalid).Select(entry => entry.Receipt).ToArray();
            }
            catch (ServerDocumentException unreadable)
            {
                _log.LogError(unreadable, "The ledger of {Player} cannot be read; their data is deleted, but the registry's records may still name them: look for the id in the Private custom items rcpt-*.", player);
                return Array.Empty<ReceiptId>();
            }
        }

        private async Task ForgetAsync(PlayerId player, ReceiptId receipt, DateOnly today, IServerStore store, CancellationToken cancellation)
        {
            try
            {
                ReceiptRecord? record = await Keeping.ReadReceiptAsync(store, receipt, cancellation).ConfigureAwait(false);
                if (record?.ClaimOf(player) == null)
                {
                    return;
                }

                if (record.Claims.Count == 1)
                {
                    await store.NoteTraceAsync(today, receipt, cancellation).ConfigureAwait(false);
                }

                await Keeping.ChangeReceiptAsync(store, receipt, held => held?.ClaimOf(player) == null ? null : held.WithoutPlayer(player, today), cancellation).ConfigureAwait(false);
            }
            catch (ServerDocumentException unreadable)
            {
                _log.LogError(unreadable, "The registry's record {Receipt} cannot be read; it may still name {Player}.", receipt, player);
            }
        }
    }
}
