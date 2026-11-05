using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Catalogue;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>
    /// "Is this payment real?" (the endpoint ValidatePurchase; docs/research/server-v1.md §4.1, §4.2). In order:
    /// <list type="number">
    /// <item>the product must be one the game sells;</item>
    /// <item>a payment the player's ledger holds already is answered from the ledger, without asking the store again;</item>
    /// <item>a pack of coins whose receipt the registry gives another player, or kept as the trace of a deleted
    /// player, is that player's; a receipt the store voided is never given;</item>
    /// <item>the store is asked; only a payment it confirms enters the ledger, under the next number and the ledger's
    /// write lock, so two calls for one payment give one entry;</item>
    /// <item>the registry records who was given the receipt.</item>
    /// </list>
    /// A store or a Cloud Save that cannot be reached gives "unavailable": the game delivers as it does today and asks
    /// again. A payment the game delivered before it could ask, which the store then refuses, enters the ledger as
    /// invalid, for the record; its coins are not taken back in v1 (owner's decision 3).
    /// </summary>
    public sealed class PurchaseValidation
    {
        private readonly ProductCatalogue _catalogue;
        private readonly Dictionary<PurchaseStore, IStoreVerifier> _verifiers;
        private readonly IClock _clock;
        private readonly ILogger _log;

        public PurchaseValidation(ProductCatalogue catalogue, IEnumerable<IStoreVerifier> verifiers, IClock clock, ILogger log)
        {
            _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            _verifiers = (verifiers ?? throw new ArgumentNullException(nameof(verifiers))).ToDictionary(verifier => verifier.Store);
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<PurchaseVerdict> ValidateAsync(PlayerId player, PaymentClaim claim, IServerStore store, ISecrets secrets, CancellationToken cancellation)
        {
            if (!(_catalogue.FindBySuffix(claim.ProductIdSuffix) is CatalogueProduct product))
            {
                return new PurchaseVerdict.Invalid(InvalidReason.UnknownProduct);
            }

            if (claim.Token.Length == 0)
            {
                return new PurchaseVerdict.Invalid(InvalidReason.NotFromTheStore);
            }

            ReceiptId receipt = ReceiptId.Of(claim.Store, claim.Token);
            try
            {
                LedgerRead read = await Keeping.ReadLedgerAsync(store, player, cancellation).ConfigureAwait(false);
                if (read.Ledger.FindByReceipt(receipt) is LedgerEntry known)
                {
                    await RecordClaimAsync(store, receipt, known, player, cancellation).ConfigureAwait(false);
                    return VerdictOf(known);
                }

                ReceiptRecord? record = await Keeping.ReadReceiptAsync(store, receipt, cancellation).ConfigureAwait(false);
                if (RegistryRefusal(record, product, player) is InvalidReason refusal)
                {
                    return await RefusedAsync(store, player, read, claim, receipt, refusal, cancellation).ConfigureAwait(false);
                }

                StoreCheck check = await _verifiers[claim.Store].CheckAsync(claim, product, player, secrets, cancellation).ConfigureAwait(false);
                switch (check)
                {
                    case StoreCheck.Confirmed confirmed:
                        return await EnteredAsync(store, player, read, claim, receipt, confirmed, cancellation).ConfigureAwait(false);
                    case StoreCheck.Rejected rejected:
                        _log.LogWarning("The store refused a payment of {Product} for {Player}: {Reason}", claim.ProductIdSuffix, player, rejected.Reason);
                        return await RefusedAsync(store, player, read, claim, receipt, rejected.Reason, cancellation).ConfigureAwait(false);
                    case StoreCheck.Pending:
                        return new PurchaseVerdict.Pending();
                    case StoreCheck.Unreachable unreachable:
                        return new PurchaseVerdict.Unavailable(unreachable.Reason);
                    default:
                        throw new InvalidOperationException($"A store check of a kind the validation does not know: {check}.");
                }
            }
            catch (ServerStoreUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "Cloud Save could not be reached while a payment of {Player} was verified.", player);
                return new PurchaseVerdict.Unavailable(UnavailableReason.CloudSaveUnreachable);
            }
            catch (SecretsUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "The Secret Manager could not be reached while a payment of {Player} was verified.", player);
                return new PurchaseVerdict.Unavailable(UnavailableReason.CloudSaveUnreachable);
            }
            catch (ServerDocumentException unreadable)
            {
                _log.LogError(unreadable, "A document of {Player}'s payments is not one the module writes; nothing was written.", player);
                return new PurchaseVerdict.Unavailable(UnavailableReason.LedgerUnreadable);
            }
        }

        private static PurchaseVerdict VerdictOf(LedgerEntry known) => known.State switch
        {
            LedgerState.Valid => new PurchaseVerdict.AlreadyValidated(known.Number, known.IsTest),
            LedgerState.Voided or LedgerState.Refunded => new PurchaseVerdict.Invalid(InvalidReason.Refunded),
            LedgerState.Invalid => new PurchaseVerdict.Invalid(InvalidReason.AlreadyRefused),
            _ => throw new InvalidOperationException($"A ledger state the validation does not know: {known.State}."),
        };

        /// <summary>
        /// What the registry holds against giving the receipt to this player: a receipt the store voided or refunded,
        /// and a pack of coins someone else was given (a player, or the trace of one who deleted their data). Nothing
        /// against a receipt not recorded, one of this player's, or a starter pack.
        /// </summary>
        private static InvalidReason? RegistryRefusal(ReceiptRecord? record, CatalogueProduct product, PlayerId player)
        {
            if (record == null)
            {
                return null;
            }

            if (record.State is LedgerState.Voided or LedgerState.Refunded)
            {
                return InvalidReason.Refunded;
            }

            bool someoneElsesPack = product.IsGoodForOnePlayerOnly && record.ClaimOf(player) == null;
            return someoneElsesPack ? InvalidReason.AnotherPlayers : null;
        }

        /// <summary>Enters the confirmed payment (or finds that another call entered it first) and records its claim.</summary>
        private async Task<PurchaseVerdict> EnteredAsync(IServerStore store, PlayerId player, LedgerRead read, PaymentClaim claim, ReceiptId receipt, StoreCheck.Confirmed confirmed, CancellationToken cancellation)
        {
            DateTimeOffset now = _clock.UtcNow;
            (LedgerEntry entry, bool isNew) = await Keeping.ChangeLedgerAsync(
                store,
                player,
                read,
                ledger => ledger.FindByReceipt(receipt) is LedgerEntry already
                    ? LedgerChange.Nothing((already, false))
                    : NewPayment(ledger.WithPayment(claim.Store, claim.ProductIdSuffix, confirmed.Order, now, LedgerState.Valid, confirmed.IsTest, receipt)),
                cancellation).ConfigureAwait(false);
            await RecordClaimAsync(store, receipt, entry, player, cancellation).ConfigureAwait(false);
            return isNew ? new PurchaseVerdict.Valid(entry.Number, entry.IsTest) : VerdictOf(entry);
        }

        /// <summary>The refusal; a payment the game delivered unverified enters the ledger as invalid first.</summary>
        private async Task<PurchaseVerdict> RefusedAsync(IServerStore store, PlayerId player, LedgerRead read, PaymentClaim claim, ReceiptId receipt, InvalidReason reason, CancellationToken cancellation)
        {
            if (claim.DeliveredUnverified)
            {
                DateTimeOffset now = _clock.UtcNow;
                await Keeping.ChangeLedgerAsync(
                    store,
                    player,
                    read,
                    ledger => ledger.FindByReceipt(receipt) != null
                        ? LedgerChange.Nothing(false)
                        : LedgerChange.To(ledger.WithPayment(claim.Store, claim.ProductIdSuffix, string.Empty, now, LedgerState.Invalid, isTest: false, receipt), true),
                    cancellation).ConfigureAwait(false);
            }

            return new PurchaseVerdict.Invalid(reason);
        }

        private static LedgerChange<(LedgerEntry, bool)> NewPayment(Ledger changed) =>
            LedgerChange.To(changed, (changed.Entries[changed.Entries.Count - 1], true));

        /// <summary>
        /// Makes the registry say that this player holds the receipt under the entry's number. A record that already
        /// says so is not written again. Only a payment the store confirmed is recorded: an invalid entry stays in the
        /// player's ledger alone, and never touches the record of the player whose receipt it was. The payment is the
        /// player's whatever comes of this: a registry that cannot be written now is written at the next question
        /// about the payment.
        /// </summary>
        private async Task RecordClaimAsync(IServerStore store, ReceiptId receipt, LedgerEntry entry, PlayerId player, CancellationToken cancellation)
        {
            if (entry.State == LedgerState.Invalid)
            {
                return;
            }

            try
            {
                await Keeping.ChangeReceiptAsync(store, receipt, record => WithClaim(record, entry, player), cancellation).ConfigureAwait(false);
            }
            catch (ServerStoreUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "The registry could not record {Receipt} for {Player}; the next question about it will.", receipt, player);
            }
        }

        /// <summary>The record with the player's claim on the entry; nothing when it says so already.</summary>
        private static ReceiptRecord? WithClaim(ReceiptRecord? record, LedgerEntry entry, PlayerId player)
        {
            ReceiptRecord wanted = (record ?? new ReceiptRecord(entry.Store, entry.Product, entry.At, entry.State, entry.IsTest, Array.Empty<ReceiptClaim>(), forgottenOn: null))
                .WithClaim(new ReceiptClaim(player, entry.Number))
                .WithState(MoreFinal(record?.State ?? entry.State, entry.State));
            bool saysSoAlready = record != null && record.ClaimOf(player)?.Number == entry.Number && record.State == wanted.State;
            return saysSoAlready ? null : wanted;
        }

        /// <summary>A refund or a voiding, once known on either side, stands: the registry never goes back to valid.</summary>
        private static LedgerState MoreFinal(LedgerState recorded, LedgerState entered) =>
            recorded is LedgerState.Voided or LedgerState.Refunded ? recorded : entered;
    }
}
