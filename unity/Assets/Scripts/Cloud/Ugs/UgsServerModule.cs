#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Purchases;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// The game's Cloud Code module, MuseumPuzzleServer (server/CloudCode), through Cloud Code 2.10.4
    /// (com.unity.services.cloudcode, pinned), as the player signed in calls it: ValidatePurchase with the store's name, the
    /// product, the payment's id and its proof (Google Play's purchase token, the App Store's signed transaction), and
    /// DeleteMyData. The module's answer is the JSON of Endpoints/ModuleReplies, read as tests/fixtures/cloud-code/replies.json
    /// pins it. Nothing is asked before the player signs in (Cloud/CloudSaving waits for it).
    /// <para>
    /// Every exception of the service ends here as an answer: a module the owner has not deployed yet (Cloud Code's
    /// NotFound), no network, a service down, too many requests, a refused token: no verdict
    /// (<see cref="ServerVerdict.Unavailable"/>), the payment waits; a deletion not done. What Cloud Code said goes to
    /// the log, never to the player.
    /// </para>
    /// </summary>
    public sealed class UgsServerModule : IServerModule
    {
        /// <summary>The module's name, which the Deployment window gives it from its project (MuseumPuzzleServer.csproj).</summary>
        public const string ModuleName = "MuseumPuzzleServer";

        public const string GooglePlayStore = "GooglePlay";
        public const string AppleAppStore = "AppleAppStore";

        private readonly string _store;
        private readonly Action<string> _log;

        /// <param name="store">The store this build's payments are made in, as the module names it: <see cref="GooglePlayStore"/> or <see cref="AppleAppStore"/>.</param>
        /// <param name="log">Where what Cloud Code said goes: the device's log.</param>
        public UgsServerModule(string store, Action<string> log)
        {
            _store = string.IsNullOrEmpty(store) ? throw new ArgumentException("A build's payments are made in a store.", nameof(store)) : store;
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>The store of the platform the game runs on, as the module names it: the App Store on iPhone and iPad, Google Play elsewhere.</summary>
        public static string StoreOfThisPlatform() => Application.platform == RuntimePlatform.IPhonePlayer ? AppleAppStore : GooglePlayStore;

        public async Task<ServerVerdict> ValidatePurchaseAsync(PaymentQuestion question, CancellationToken cancel)
        {
            var arguments = new Dictionary<string, object>
            {
                { "store", _store },
                { "productIdSuffix", question.Product.IdSuffix },
                { "transactionId", question.Payment.TransactionId },
                { "payload", question.Payment.Proof ?? string.Empty },
                { "deliveredUnverified", false },
            };
            try
            {
                string reply = await CloudCodeService.Instance.CallModuleEndpointAsync(ModuleName, "ValidatePurchase", arguments);
                return ServerVerdict.Of(DocumentOf<PurchaseReplyDocument>(reply));
            }
            catch (CloudCodeException failure)
            {
                _log($"Cloud Code did not verify the {question}: {failure.Reason} {failure.ErrorCode} {failure.Message}");
                return new ServerVerdict.Unavailable(failure.Reason == CloudCodeExceptionReason.NotFound ? "the module is not deployed (NotFound)" : $"Cloud Code: {failure.Reason}");
            }
            catch (RequestFailedException failure)
            {
                _log($"Cloud Code did not verify the {question}: {failure.ErrorCode} {failure.Message}");
                return new ServerVerdict.Unavailable($"Cloud Code: {failure.ErrorCode}");
            }
        }

        public async Task<DataDeletion> DeleteMyDataAsync(CancellationToken cancel)
        {
            try
            {
                string reply = await CloudCodeService.Instance.CallModuleEndpointAsync(ModuleName, "DeleteMyData", new Dictionary<string, object>());
                if (DocumentOf<DeletionReplyDocument>(reply)?.outcome == "deleted")
                {
                    return DataDeletion.Deleted;
                }

                _log($"The module did not delete the player's data: {reply}");
                return DataDeletion.Unavailable;
            }
            catch (CloudCodeException failure)
            {
                _log($"Cloud Code did not delete the player's data: {failure.Reason} {failure.ErrorCode} {failure.Message}");
                return failure.Reason == CloudCodeExceptionReason.NoInternetConnection ? DataDeletion.NoNetwork : DataDeletion.Unavailable;
            }
            catch (RequestFailedException failure)
            {
                _log($"Cloud Code did not delete the player's data: {failure.ErrorCode} {failure.Message}");
                return failure.ErrorCode == CommonErrorCodes.TransportError ? DataDeletion.NoNetwork : DataDeletion.Unavailable;
            }
        }

        /// <summary>The reply's document; nothing for a text that holds none (Unity's reader throws on text that is no JSON).</summary>
        private T? DocumentOf<T>(string? reply)
            where T : class
        {
            if (string.IsNullOrEmpty(reply))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(reply);
            }
            catch (ArgumentException unreadable)
            {
                _log($"The module's answer is no document: {unreadable.Message}");
                return null;
            }
        }

#pragma warning disable IDE1006, CA1051 // public camelCase fields are the reply's layout: Unity's JSON reader maps fields, not properties

        /// <summary>DeleteMyData's answer: <c>{"outcome":"deleted"}</c>, or "unavailable" with a reason.</summary>
        [Serializable]
        internal sealed class DeletionReplyDocument
        {
            public string outcome = string.Empty;
            public string reason = string.Empty;
        }
    }
}
