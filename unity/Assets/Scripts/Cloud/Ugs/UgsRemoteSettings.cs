#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.RemoteConfig;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// What Unity's Remote Config 4.2.5 (com.unity.remote-config, pinned) says of the sync: the four keys of the owner's
    /// decision 6 (<see cref="RemoteConfigKeys"/>), each that it gives read within the sync's bounds
    /// (<see cref="SyncSettings.FromRemote"/>), each it does not give at its default. Fetched once a run, after the
    /// sign-in, with no attributes of the player's; what it fetched on an earlier run counts when this run's fetch did not
    /// get through. A fetch that fails, or comes back with nothing of the project (none is set up yet), leaves the
    /// defaults. Remote Config keeps what it fetched in a file of its own on the phone. What failed goes to the log.
    /// </summary>
    public sealed class UgsRemoteSettings : IRemoteSettings
    {
        private readonly Action<string> _log;

        public UgsRemoteSettings(Action<string> log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<SyncSettings?> FetchAsync(CancellationToken cancel)
        {
            RuntimeConfig config;
            try
            {
                config = await RemoteConfigService.Instance.FetchConfigsAsync(new NoAttributes(), new NoAttributes());
            }
            catch (JsonException failure)
            {
                // Remote Config answers a network failure itself, with what it fetched before; only an answer that does not read throws.
                _log($"Remote Config's answer did not read: {failure.Message}");
                return null;
            }

            if (config == null || config.origin == ConfigOrigin.Default)
            {
                _log($"Remote Config gave nothing new ({config?.origin.ToString() ?? "no answer"}); the sync keeps its settings.");
                return null;
            }

            return SyncSettings.FromRemote(
                config.HasKey(RemoteConfigKeys.CloudSyncEnabled) ? config.GetBool(RemoteConfigKeys.CloudSyncEnabled) : (bool?)null,
                config.HasKey(RemoteConfigKeys.UploadMinMinutes) ? config.GetLong(RemoteConfigKeys.UploadMinMinutes) : (long?)null,
                config.HasKey(RemoteConfigKeys.ReadMinHours) ? config.GetLong(RemoteConfigKeys.ReadMinHours) : (long?)null,
                config.HasKey(RemoteConfigKeys.PurchaseValidationEnabled) ? config.GetBool(RemoteConfigKeys.PurchaseValidationEnabled) : (bool?)null);
        }

        /// <summary>The attributes the fetch sends of the player and of the app: none.</summary>
        private struct NoAttributes
        {
        }
    }
}
