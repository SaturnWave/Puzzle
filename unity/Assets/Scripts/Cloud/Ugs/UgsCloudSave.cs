#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Internal;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// The player's items in Unity's Cloud Save 3.4.2 (com.unity.services.cloudsave, pinned), as the sync reads and writes
    /// them (docs/research/server-v1.md §3.2): the Player Data item "save" in the default access class, which the game
    /// writes with the write lock of its last read, and "purchases" in the protected class, which only the server writes
    /// and the game reads. Cloud Save keeps them under the account signed in; nothing is asked before the sign-in.
    /// <para>
    /// A read is two of the free tier's reads: Cloud Save reads one access class a call (§2.4 counted one; the 12-hour
    /// budget of the start's read, Stage 1, leaves room for it). Each item is a string: the save's envelope as
    /// Cloud/CloudEnvelope writes it, the ledger as the server writes it.
    /// </para>
    /// <para>
    /// Every exception of the service ends here as a <see cref="CloudFailure"/>: a write lock that is old (409) as the
    /// write's own answer, no internet as no network, a project or player refused as blocked, too many requests as rate
    /// limited, a key or a size refused as refused, anything else as unavailable. What Cloud Save said goes to the log.
    /// </para>
    /// </summary>
    public sealed class UgsCloudSave : ICloudSave
    {
        /// <summary>The Player Data key of the save (default access class).</summary>
        public const string SaveKey = "save";

        /// <summary>The Player Data key of the server's ledger (protected access class).</summary>
        public const string PurchasesKey = "purchases";

        private readonly Action<string> _log;

        public UgsCloudSave(Action<string> log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<CloudRead> ReadAsync(CancellationToken cancel)
        {
            try
            {
                IPlayerDataService player = CloudSaveService.Instance.Data.Player;
                Dictionary<string, Item> saved = await player.LoadAsync(new HashSet<string> { SaveKey }, new LoadOptions(new DefaultReadAccessClassOptions()));
                Dictionary<string, Item> ledger = await player.LoadAsync(new HashSet<string> { PurchasesKey }, new LoadOptions(new ProtectedReadAccessClassOptions()));
                return new CloudRead.Found(ItemOf(saved, SaveKey), ItemOf(ledger, PurchasesKey));
            }
            catch (CloudSaveException failure)
            {
                _log($"Cloud Save did not read: {failure.Reason} {failure.ErrorCode} {failure.Message}");
                return new CloudRead.Failed(FailureOf(failure));
            }
            catch (RequestFailedException failure)
            {
                _log($"Cloud Save did not read: {failure.ErrorCode} {failure.Message}");
                return new CloudRead.Failed(CloudFailure.Unavailable);
            }
        }

        public async Task<CloudWrite> WriteSaveAsync(string text, CloudWriteLock? expected, CancellationToken cancel)
        {
            try
            {
                var item = new SaveItem(text, expected?.Value);
                Dictionary<string, string> locks = await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, SaveItem> { { SaveKey, item } }, new PlayerSaveOptions());
                return locks.TryGetValue(SaveKey, out string? written) && !string.IsNullOrEmpty(written)
                    ? new CloudWrite.Written(new CloudWriteLock(written))
                    : (CloudWrite)new CloudWrite.Failed(CloudFailure.Unavailable);
            }
            catch (CloudSaveConflictException)
            {
                return new CloudWrite.Conflict();
            }
            catch (CloudSaveException failure) when (failure.Reason == CloudSaveExceptionReason.Conflict)
            {
                return new CloudWrite.Conflict();
            }
            catch (CloudSaveException failure)
            {
                _log($"Cloud Save did not write: {failure.Reason} {failure.ErrorCode} {failure.Message}");
                return new CloudWrite.Failed(FailureOf(failure));
            }
            catch (RequestFailedException failure)
            {
                _log($"Cloud Save did not write: {failure.ErrorCode} {failure.Message}");
                return new CloudWrite.Failed(CloudFailure.Unavailable);
            }
        }

        /// <summary>The item the answer holds under the key, with its write lock; nothing when the player has none, or an empty one.</summary>
        private static CloudItem? ItemOf(Dictionary<string, Item> answer, string key)
        {
            if (!answer.TryGetValue(key, out Item? item) || item?.Value == null)
            {
                return null;
            }

            string text = item.Value.GetAs<string>();
            return string.IsNullOrEmpty(text) || string.IsNullOrEmpty(item.WriteLock) ? null : new CloudItem(text, new CloudWriteLock(item.WriteLock));
        }

        private static CloudFailure FailureOf(CloudSaveException failure) => failure.Reason switch
        {
            CloudSaveExceptionReason.NoInternetConnection => CloudFailure.NoNetwork,
            CloudSaveExceptionReason.TooManyRequests => CloudFailure.RateLimited,
            CloudSaveExceptionReason.ProjectIdMissing => CloudFailure.Blocked,
            CloudSaveExceptionReason.PlayerIdMissing => CloudFailure.Blocked,
            CloudSaveExceptionReason.AccessTokenMissing => CloudFailure.Blocked,
            CloudSaveExceptionReason.Unauthorized => CloudFailure.Blocked,
            CloudSaveExceptionReason.InvalidArgument => CloudFailure.Refused,
            CloudSaveExceptionReason.KeyLimitExceeded => CloudFailure.Refused,
            CloudSaveExceptionReason.NotFound => CloudFailure.Unavailable,
            CloudSaveExceptionReason.ServiceUnavailable => CloudFailure.Unavailable,
            CloudSaveExceptionReason.Conflict => CloudFailure.Unavailable,
            CloudSaveExceptionReason.Unknown => CloudFailure.Unavailable,
            _ => CloudFailure.Unavailable,
        };
    }
}
