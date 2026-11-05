using System;
using System.Threading;
using System.Threading.Tasks;

namespace MuseumPuzzle.Server.Platform
{
    /// <summary>
    /// The secrets the module reads, each by its name in Unity's Secret Manager (docs/research/server-v1.md §4.7). A
    /// closed set. The owner enters their values in the Unity Dashboard, giving Cloud Code access; they are in no file
    /// of the repository and no build of the game.
    /// </summary>
    public enum SecretName
    {
        /// <summary>GOOGLE_PLAY_SERVICE_ACCOUNT_JSON: the whole JSON key file of the Google Cloud service account that may read the game's orders.</summary>
        GooglePlayServiceAccountJson,

        /// <summary>APPLE_IAP_PRIVATE_KEY: the text of the In-App Purchase key's .p8 file from App Store Connect.</summary>
        AppleIapPrivateKey,

        /// <summary>APPLE_IAP_KEY_ID: that key's id.</summary>
        AppleIapKeyId,

        /// <summary>APPLE_ISSUER_ID: the App Store Connect issuer id shown above the keys.</summary>
        AppleIssuerId,
    }

    /// <summary>Unity's Secret Manager could not be reached; the message says which secret, the cause is the SDK's exception.</summary>
    public sealed class SecretsUnavailableException : Exception
    {
        public SecretsUnavailableException(string message)
            : base(message)
        {
        }

        public SecretsUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>The module's view of Unity's Secret Manager.</summary>
    public interface ISecrets
    {
        /// <summary>The secret's value; nothing when the owner has not entered it (yet), or entered it empty.</summary>
        /// <exception cref="SecretsUnavailableException">The Secret Manager could not be reached.</exception>
        Task<string?> ReadAsync(SecretName name, CancellationToken cancellation);
    }

    public static class SecretNames
    {
        /// <summary>The secret's name in the Secret Manager: what the owner types in the Dashboard.</summary>
        public static string KeyOf(SecretName name) => name switch
        {
            SecretName.GooglePlayServiceAccountJson => "GOOGLE_PLAY_SERVICE_ACCOUNT_JSON",
            SecretName.AppleIapPrivateKey => "APPLE_IAP_PRIVATE_KEY",
            SecretName.AppleIapKeyId => "APPLE_IAP_KEY_ID",
            SecretName.AppleIssuerId => "APPLE_ISSUER_ID",
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "A secret the module does not read."),
        };
    }
}
