using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;

namespace MuseumPuzzle.Server.Endpoints
{
    /// <summary>
    /// The endpoints' work between Cloud Code's arguments and the use cases: who called (a player, or the scheduler
    /// with no player), what the game sent, and the call's time budget. The endpoints (<see cref="PurchaseModule"/>)
    /// only give it Cloud Save and the Secret Manager of the call; the tests give it fakes.
    /// </summary>
    public sealed class ModuleCalls
    {
        /// <summary>The time a call may take before it answers "unavailable": inside Cloud Code's fifteen seconds, with room for the answer.</summary>
        public static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(13);

        private readonly ModuleParts _parts;
        private readonly ILogger _log;

        public ModuleCalls(ModuleParts parts, ILogger log)
        {
            _parts = parts ?? throw new ArgumentNullException(nameof(parts));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <param name="callerId">The caller's player id (<c>IExecutionContext.PlayerId</c>); empty for the scheduler or another service.</param>
        /// <param name="store">"GooglePlay" or "AppleAppStore".</param>
        /// <param name="transactionId">Apple's transaction id; for Google Play the game sends what IAP gives and the purchase token is the payload.</param>
        /// <param name="payload">Google's purchase token, or Apple's signed transaction.</param>
        public async Task<PurchaseVerdict> ValidatePurchaseAsync(
            string? callerId, string? store, string? productIdSuffix, string? transactionId, string? payload, bool deliveredUnverified, IServerStore cloud, ISecrets secrets)
        {
            if (!(PlayerOf(callerId) is PlayerId player))
            {
                return new PurchaseVerdict.Unavailable(UnavailableReason.NotAPlayer);
            }

            if (!(PurchaseStoreWords.StoreNamed(store) is PurchaseStore named))
            {
                return new PurchaseVerdict.Invalid(InvalidReason.UnknownStore);
            }

            string token = (named == PurchaseStore.GooglePlay ? payload : transactionId) ?? string.Empty;
            var claim = new PaymentClaim(named, productIdSuffix ?? string.Empty, token, payload ?? string.Empty, deliveredUnverified);
            using var budget = new CancellationTokenSource(CallBudget);
            try
            {
                return await _parts.Validation(_log).ValidateAsync(player, claim, cloud, secrets, budget.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException outOfTime) when (budget.IsCancellationRequested)
            {
                _log.LogWarning(outOfTime, "A payment of {Player} was not verified within {Seconds} seconds.", player, CallBudget.TotalSeconds);
                return new PurchaseVerdict.Unavailable(UnavailableReason.OutOfTime);
            }
        }

        public async Task<RefundOutcome> CheckRefundsAsync(string? callerId, IServerStore cloud, ISecrets secrets)
        {
            if (!(PlayerOf(callerId) is PlayerId player))
            {
                return new RefundOutcome.Unavailable(UnavailableReason.NotAPlayer);
            }

            using var budget = new CancellationTokenSource(CallBudget);
            try
            {
                return await _parts.Refunds(_log).CheckAsync(player, cloud, secrets, budget.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException outOfTime) when (budget.IsCancellationRequested)
            {
                _log.LogWarning(outOfTime, "The refunds of {Player} were not checked within {Seconds} seconds.", player, CallBudget.TotalSeconds);
                return new RefundOutcome.Unavailable(UnavailableReason.OutOfTime);
            }
        }

        public async Task<DeletionOutcome> DeleteMyDataAsync(string? callerId, IServerStore cloud)
        {
            if (!(PlayerOf(callerId) is PlayerId player))
            {
                return new DeletionOutcome.Unavailable(UnavailableReason.NotAPlayer);
            }

            using var budget = new CancellationTokenSource(CallBudget);
            try
            {
                return await _parts.Deletion(_log).DeleteAsync(player, cloud, budget.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException outOfTime) when (budget.IsCancellationRequested)
            {
                _log.LogWarning(outOfTime, "The data of {Player} was not wholly deleted within {Seconds} seconds; the next call finishes it.", player, CallBudget.TotalSeconds);
                return new DeletionOutcome.Unavailable(UnavailableReason.OutOfTime);
            }
        }

        /// <summary>The daily look at Google's voided purchases; refused to a player, who could spend Google's daily quota with it.</summary>
        public async Task<ChoreOutcome> PollGoogleVoidedAsync(string? callerId, IServerStore cloud, ISecrets secrets)
        {
            if (PlayerOf(callerId) is PlayerId player)
            {
                _log.LogWarning("The player {Player} called PollGoogleVoided, which only the scheduler runs.", player);
                return new ChoreOutcome.Refused();
            }

            ChoreOutcome outcome = await _parts.VoidedPoll(_log).RunAsync(cloud, secrets, CancellationToken.None).ConfigureAwait(false);
            _log.LogInformation("Google's voided purchases: {Outcome}.", ModuleReplies.Of(outcome).Outcome);
            return outcome;
        }

        /// <summary>The daily end of the 36-month traces; refused to a player.</summary>
        public async Task<ChoreOutcome> ForgetOldTracesAsync(string? callerId, IServerStore cloud)
        {
            if (PlayerOf(callerId) is PlayerId player)
            {
                _log.LogWarning("The player {Player} called ForgetOldTraces, which only the scheduler runs.", player);
                return new ChoreOutcome.Refused();
            }

            ChoreOutcome outcome = await _parts.Retention(_log).RunAsync(cloud, CancellationToken.None).ConfigureAwait(false);
            _log.LogInformation("Old traces: {Outcome}.", ModuleReplies.Of(outcome).Outcome);
            return outcome;
        }

        private static PlayerId? PlayerOf(string? callerId) => string.IsNullOrEmpty(callerId) ? null : new PlayerId(callerId);
    }
}
