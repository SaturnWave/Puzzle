using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Platform;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace MuseumPuzzle.Server.Endpoints
{
    /// <summary>Cloud Code's setup of the module: the game API client (Cloud Save, Secret Manager) and the parts the module shares between calls.</summary>
    public sealed class ModuleSetup : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            config.AddGameApiClient();
            config.Dependencies.AddSingleton(ModuleParts.ForCloudCode());
        }
    }

    /// <summary>
    /// The module's endpoints (docs/research/server-v1.md §4.2), as the game calls them:
    /// <c>CloudCodeService.Instance.CallModuleEndpointAsync("MuseumPuzzleServer", "ValidatePurchase", args)</c>. The
    /// first three are a player's; the last two only the scheduler's (server/CloudCode/schedules), and a player who
    /// calls them is refused. Each endpoint gives the call's Cloud Save and Secret Manager to
    /// <see cref="ModuleCalls"/>, which does the work.
    /// </summary>
    public sealed class PurchaseModule
    {
        public const string ModuleName = "MuseumPuzzleServer";

        private readonly ModuleParts _parts;
        private readonly ILogger<PurchaseModule> _log;

        public PurchaseModule(ModuleParts parts, ILogger<PurchaseModule> log)
        {
            _parts = parts ?? throw new ArgumentNullException(nameof(parts));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Verifies a payment with its store and enters it in the player's ledger. The game sends every argument:
        /// <c>{"store":"GooglePlay","productIdSuffix":"coins.pouch","transactionId":"…","payload":"…","deliveredUnverified":false}</c>.
        /// </summary>
        [CloudCodeFunction("ValidatePurchase")]
        public async Task<PurchaseReply> ValidatePurchase(
            IExecutionContext context, IGameApiClient gameApiClient, string store, string productIdSuffix, string transactionId, string payload, bool deliveredUnverified) =>
            ModuleReplies.Of(await Calls.ValidatePurchaseAsync(
                context.PlayerId, store, productIdSuffix, transactionId, payload, deliveredUnverified, CloudSaveOf(context, gameApiClient), SecretsOf(context, gameApiClient)).ConfigureAwait(false));

        /// <summary>Asks Apple for the player's refunds, at most once in 20 hours; the game calls it at start for a player with an App Store payment.</summary>
        [CloudCodeFunction("CheckRefunds")]
        public async Task<RefundReply> CheckRefunds(IExecutionContext context, IGameApiClient gameApiClient) =>
            ModuleReplies.Of(await Calls.CheckRefundsAsync(context.PlayerId, CloudSaveOf(context, gameApiClient), SecretsOf(context, gameApiClient)).ConfigureAwait(false));

        /// <summary>Deletes the player's data in Cloud Save and their name in the registry, keeping the 36-month trace; the game deletes the account after "deleted".</summary>
        [CloudCodeFunction("DeleteMyData")]
        public async Task<DeletionReply> DeleteMyData(IExecutionContext context, IGameApiClient gameApiClient) =>
            ModuleReplies.Of(await Calls.DeleteMyDataAsync(context.PlayerId, CloudSaveOf(context, gameApiClient)).ConfigureAwait(false));

        /// <summary>The scheduler's daily look at Google's voided purchases (03:00 UTC).</summary>
        [CloudCodeFunction("PollGoogleVoided")]
        public async Task<ChoreReply> PollGoogleVoided(IExecutionContext context, IGameApiClient gameApiClient) =>
            ModuleReplies.Of(await Calls.PollGoogleVoidedAsync(context.PlayerId, CloudSaveOf(context, gameApiClient), SecretsOf(context, gameApiClient)).ConfigureAwait(false));

        /// <summary>The scheduler's daily end of the traces older than 36 months (04:00 UTC).</summary>
        [CloudCodeFunction("ForgetOldTraces")]
        public async Task<ChoreReply> ForgetOldTraces(IExecutionContext context, IGameApiClient gameApiClient) =>
            ModuleReplies.Of(await Calls.ForgetOldTracesAsync(context.PlayerId, CloudSaveOf(context, gameApiClient)).ConfigureAwait(false));

        private ModuleCalls Calls => new ModuleCalls(_parts, _log);

        private static UgsServerStore CloudSaveOf(IExecutionContext context, IGameApiClient gameApiClient) => new UgsServerStore(gameApiClient.CloudSaveData, context);

        private static UgsSecrets SecretsOf(IExecutionContext context, IGameApiClient gameApiClient) => new UgsSecrets(gameApiClient.SecretManager, context);
    }
}
