using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;

namespace MuseumPuzzle.Server.AppStore
{
    /// <summary>
    /// The day's look at a player's App Store refunds (the endpoint CheckRefunds; docs/research/server-v1.md §4.6).
    /// Apple's refund notices need an address of our own, which Unity Gaming Services does not give, so the game asks
    /// when it opens, and the server asks Apple's refund history at most once in 20 hours, and only for a player with
    /// a valid App Store payment. A refunded payment is marked "refunded" in the ledger and the registry; its coins
    /// are not taken back in v1 (owner's decision 3, 2026-10-26).
    /// One question covers every payment of the Apple account that made the newest one; payments a player made from
    /// a second Apple account on the same game account are not covered (a known limit, noted in the plan).
    /// </summary>
    public sealed class AppleRefundCheck
    {
        /// <summary>Apple is asked again only this long after the last time: about once a day for a player who opens the game daily.</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromHours(20);

        private readonly AppStoreVerifier _apple;
        private readonly IClock _clock;
        private readonly ILogger _log;

        public AppleRefundCheck(AppStoreVerifier apple, IClock clock, ILogger log)
        {
            _apple = apple ?? throw new ArgumentNullException(nameof(apple));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<RefundOutcome> CheckAsync(PlayerId player, IServerStore store, ISecrets secrets, CancellationToken cancellation)
        {
            try
            {
                LedgerRead read = await Keeping.ReadLedgerAsync(store, player, cancellation).ConfigureAwait(false);
                LedgerEntry[] standing = StandingApplePayments(read.Ledger).ToArray();
                DateTimeOffset now = _clock.UtcNow;
                if (standing.Length == 0)
                {
                    return new RefundOutcome.NothingToCheck();
                }

                if (read.Ledger.RefundsCheckedAt is DateTimeOffset checkedAt && now - checkedAt < Interval)
                {
                    return new RefundOutcome.NotDue();
                }

                var refundedTransactions = new HashSet<string>(StringComparer.Ordinal);
                foreach (IGrouping<bool, LedgerEntry> world in standing.GroupBy(entry => entry.IsTest))
                {
                    LedgerEntry newest = world.MaxBy(entry => entry.Number)!;
                    RefundHistory history = await _apple.RefundsAsync(newest.Order, world.Key ? AppleEnvironment.Sandbox : AppleEnvironment.Production, secrets, cancellation).ConfigureAwait(false);
                    switch (history)
                    {
                        case RefundHistory.Failed failed:
                            return new RefundOutcome.Unavailable(failed.Reason);
                        case RefundHistory.Read answered:
                            refundedTransactions.UnionWith(answered.RefundedTransactions);
                            break;
                        default:
                            throw new InvalidOperationException($"A refund history of a kind the check does not know: {history}.");
                    }
                }

                LedgerEntry[] refunded = await Keeping.ChangeLedgerAsync(
                    store,
                    player,
                    read,
                    ledger =>
                    {
                        LedgerEntry[] found = StandingApplePayments(ledger).Where(entry => refundedTransactions.Contains(entry.Order)).ToArray();
                        return LedgerChange.To(ledger.WithStates(found.ToDictionary(entry => entry.Number, _ => LedgerState.Refunded)).WithRefundsCheckedAt(now), found);
                    },
                    cancellation).ConfigureAwait(false);
                foreach (LedgerEntry entry in refunded)
                {
                    _log.LogInformation("Apple refunded the payment #{Number} ({Product}) of {Player}; it is marked, its coins are not taken back.", entry.Number, entry.Product, player);
                    await Registry.MarkAsync(store, entry.Receipt, LedgerState.Refunded, _log, cancellation).ConfigureAwait(false);
                }

                return new RefundOutcome.Checked(refunded.Select(entry => entry.Number).ToArray());
            }
            catch (ServerStoreUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "Cloud Save could not be reached while {Player}'s refunds were checked.", player);
                return new RefundOutcome.Unavailable(UnavailableReason.CloudSaveUnreachable);
            }
            catch (SecretsUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "The Secret Manager could not be reached while {Player}'s refunds were checked.", player);
                return new RefundOutcome.Unavailable(UnavailableReason.CloudSaveUnreachable);
            }
            catch (ServerDocumentException unreadable)
            {
                _log.LogError(unreadable, "{Player}'s ledger is not one the module writes; refunds were not checked.", player);
                return new RefundOutcome.Unavailable(UnavailableReason.LedgerUnreadable);
            }
        }

        private static IEnumerable<LedgerEntry> StandingApplePayments(Ledger ledger) =>
            ledger.Entries.Where(entry => entry.Store == PurchaseStore.AppleAppStore && entry.State == LedgerState.Valid && entry.Order.Length > 0);
    }
}
