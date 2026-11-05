using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Purchases;

namespace MuseumPuzzle.Server.GooglePlay
{
    /// <summary>
    /// The daily look at Google's list of voided purchases (the scheduled endpoint PollGoogleVoided, 03:00 UTC;
    /// docs/research/server-v1.md §4.6). Google's instant notices need Pub/Sub, which needs a billing account, so the
    /// list is read instead: from where the last complete look came to, an hour over for safety, but never further
    /// back than Google keeps (30 days), page by page. A receipt the registry knows is marked "voided" in the ledger
    /// of everyone who holds it, then in the registry; its coins are not taken back in v1 (owner's decision 3). Every
    /// step can be done twice without harm, so a run stopped at its time budget is simply taken up the next day.
    /// </summary>
    public sealed class GoogleVoidedPoll
    {
        /// <summary>How far back Google keeps voided purchases, less an hour so that a slow call never asks for more.</summary>
        public static readonly TimeSpan Reach = TimeSpan.FromDays(30) - TimeSpan.FromHours(1);

        public static readonly TimeSpan Overlap = TimeSpan.FromHours(1);

        /// <summary>The job's time, well inside Cloud Code's fifteen seconds a call.</summary>
        public static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

        private readonly GooglePlayVerifier _google;
        private readonly IClock _clock;
        private readonly ILogger _log;

        public GoogleVoidedPoll(GooglePlayVerifier google, IClock clock, ILogger log)
        {
            _google = google ?? throw new ArgumentNullException(nameof(google));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<ChoreOutcome> RunAsync(IServerStore store, ISecrets secrets, CancellationToken cancellation)
        {
            DateTimeOffset now = _clock.UtcNow;
            DateTimeOffset deadline = now + Budget;
            int looked = 0;
            int marked = 0;
            try
            {
                DateTimeOffset oldest = now - Reach;
                DateTimeOffset? seenUntil = await store.ReadVoidedSeenUntilAsync(cancellation).ConfigureAwait(false);
                DateTimeOffset from = seenUntil is DateTimeOffset until && until - Overlap > oldest ? until - Overlap : oldest;
                string? pageToken = null;
                do
                {
                    VoidedPage page = await _google.VoidedPageAsync(from, now, pageToken, secrets, cancellation).ConfigureAwait(false);
                    if (page is VoidedPage.Failed failed)
                    {
                        return new ChoreOutcome.Unavailable(failed.Reason);
                    }

                    if (!(page is VoidedPage.Read read))
                    {
                        throw new InvalidOperationException($"A page of a kind the poll does not know: {page}.");
                    }

                    foreach (VoidedPurchase voided in read.Purchases)
                    {
                        looked++;
                        if (await MarkAsync(store, ReceiptId.Of(PurchaseStore.GooglePlay, voided.PurchaseToken), cancellation).ConfigureAwait(false))
                        {
                            marked++;
                        }

                        if (_clock.UtcNow >= deadline)
                        {
                            return new ChoreOutcome.Unfinished(looked, marked);
                        }
                    }

                    pageToken = read.NextPage;
                }
                while (pageToken != null);

                await store.WriteVoidedSeenUntilAsync(now, cancellation).ConfigureAwait(false);
                return new ChoreOutcome.Done(looked, marked);
            }
            catch (ServerStoreUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "Cloud Save could not be reached while Google's voided purchases were marked ({Looked} looked at).", looked);
                return new ChoreOutcome.Unavailable(UnavailableReason.CloudSaveUnreachable);
            }
            catch (SecretsUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "The Secret Manager could not be reached for Google's voided purchases.");
                return new ChoreOutcome.Unavailable(UnavailableReason.CloudSaveUnreachable);
            }
        }

        /// <summary>
        /// Marks a voided receipt: in the ledger of each player who holds it first, then in the registry. A receipt the
        /// registry does not know (a payment never verified, or a subscription's) or knows as voided already is left.
        /// A ledger or a record the module cannot read is logged and left, so one fault never stops the others.
        /// </summary>
        private async Task<bool> MarkAsync(IServerStore store, ReceiptId receipt, CancellationToken cancellation)
        {
            try
            {
                ReceiptRecord? record = await Keeping.ReadReceiptAsync(store, receipt, cancellation).ConfigureAwait(false);
                if (record == null || record.State == LedgerState.Voided)
                {
                    return false;
                }

                foreach (ReceiptClaim claim in record.Claims)
                {
                    LedgerRead read = await Keeping.ReadLedgerAsync(store, claim.Player, cancellation).ConfigureAwait(false);
                    await Keeping.ChangeLedgerAsync(store, claim.Player, read, ledger => Voided(ledger, receipt), cancellation).ConfigureAwait(false);
                }

                await Registry.MarkAsync(store, receipt, LedgerState.Voided, _log, cancellation).ConfigureAwait(false);
                _log.LogInformation("Google voided {Receipt} ({Product}); marked for {Players} player(s), its coins are not taken back.", receipt, record.Product, record.Claims.Count);
                return true;
            }
            catch (ServerDocumentException unreadable)
            {
                _log.LogError(unreadable, "A document of the voided receipt {Receipt} is not one the module writes; it was left as it is.", receipt);
                return false;
            }
        }

        private static LedgerChange<bool> Voided(Ledger ledger, ReceiptId receipt) =>
            ledger.FindByReceipt(receipt) is LedgerEntry entry && entry.State == LedgerState.Valid
                ? LedgerChange.To(ledger.WithStates(new System.Collections.Generic.Dictionary<LedgerNumber, LedgerState> { [entry.Number] = LedgerState.Voided }), true)
                : LedgerChange.Nothing(false);
    }
}
