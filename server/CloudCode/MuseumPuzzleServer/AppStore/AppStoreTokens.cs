using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MuseumPuzzle.Server.Catalogue;
using MuseumPuzzle.Server.Platform;

namespace MuseumPuzzle.Server.AppStore
{
    /// <summary>
    /// The JWTs the App Store Server API takes (developer.apple.com, "Generating JSON Web Tokens for API requests"):
    /// ES256 with the In-App Purchase key of the secret APPLE_IAP_PRIVATE_KEY; the header's kid is APPLE_IAP_KEY_ID,
    /// the payload's iss APPLE_ISSUER_ID, its aud "appstoreconnect-v1" and its bid the game's bundle id. Apple takes a
    /// token for at most an hour; this one lives 30 minutes and is used for 25.
    /// </summary>
    public sealed class AppStoreTokens
    {
        private static readonly TimeSpan TokenLife = TimeSpan.FromMinutes(30);

        private static readonly TimeSpan TokenKept = TimeSpan.FromMinutes(25);

        private readonly IClock _clock;
        private readonly TokenSlot _slot = new TokenSlot();

        public AppStoreTokens(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <exception cref="StoreCredentialsException">A secret is not set (<see cref="CredentialProblem.Missing"/>) or the key is not a .p8 key (<see cref="CredentialProblem.Malformed"/>).</exception>
        /// <exception cref="SecretsUnavailableException">The Secret Manager could not be reached.</exception>
        public async Task<string> TokenAsync(ISecrets secrets, CancellationToken cancellation)
        {
            string privateKey = await RequiredAsync(secrets, SecretName.AppleIapPrivateKey, cancellation).ConfigureAwait(false);
            string keyId = await RequiredAsync(secrets, SecretName.AppleIapKeyId, cancellation).ConfigureAwait(false);
            string issuerId = await RequiredAsync(secrets, SecretName.AppleIssuerId, cancellation).ConfigureAwait(false);
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{keyId}\n{issuerId}\n{privateKey}")));
            DateTimeOffset now = _clock.UtcNow;
            if (_slot.Held(fingerprint, now) is string held)
            {
                return held;
            }

            using ECDsa key = ECDsa.Create();
            try
            {
                key.ImportFromPem(privateKey);
            }
            catch (Exception notAKey) when (notAKey is ArgumentException or CryptographicException)
            {
                throw new StoreCredentialsException(CredentialProblem.Malformed, "APPLE_IAP_PRIVATE_KEY is not the text of a .p8 key.", notAKey);
            }

            string token = TokenOf(key, keyId, issuerId, now);
            _slot.Keep(fingerprint, token, now + TokenKept);
            return token;
        }

        /// <summary>Forgets the kept token: Apple refused it before its time.</summary>
        public void Forget() => _slot.Drop();

        internal static string TokenOf(ECDsa key, string keyId, string issuerId, DateTimeOffset now)
        {
            string header = OutsideCalls.Base64Url(JsonSerializer.Serialize(new Dictionary<string, string> { ["alg"] = "ES256", ["kid"] = keyId, ["typ"] = "JWT" }));
            string claims = OutsideCalls.Base64Url(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["iss"] = issuerId,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["exp"] = (now + TokenLife).ToUnixTimeSeconds(),
                ["aud"] = "appstoreconnect-v1",
                ["bid"] = ProductCatalogue.ApplicationId,
            }));
            byte[] signature = key.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return $"{header}.{claims}.{OutsideCalls.Base64Url(signature)}";
        }

        private static async Task<string> RequiredAsync(ISecrets secrets, SecretName name, CancellationToken cancellation) =>
            await secrets.ReadAsync(name, cancellation).ConfigureAwait(false)
                ?? throw new StoreCredentialsException(CredentialProblem.Missing, $"The secret {SecretNames.KeyOf(name)} is not set.");
    }
}
