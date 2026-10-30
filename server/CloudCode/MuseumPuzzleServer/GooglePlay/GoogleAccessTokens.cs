using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MuseumPuzzle.Server.Platform;

namespace MuseumPuzzle.Server.GooglePlay
{
    /// <summary>
    /// The Google Cloud service account the module asks Google Play with: the JSON key file the owner made in Google
    /// Cloud (docs/research/server-v1.md §9 B6) and entered as the secret GOOGLE_PLAY_SERVICE_ACCOUNT_JSON. Only the
    /// three fields the token exchange needs are read; the private key never leaves the module, and is let go of
    /// with the account at the end of the call that read it.
    /// </summary>
    public sealed class GoogleServiceAccount : IDisposable
    {
        private const string DefaultTokenUri = "https://oauth2.googleapis.com/token";

        private GoogleServiceAccount(string clientEmail, RSA privateKey, string tokenUri, string fingerprint)
        {
            ClientEmail = clientEmail;
            PrivateKey = privateKey;
            TokenUri = tokenUri;
            Fingerprint = fingerprint;
        }

        public string ClientEmail { get; }

        public RSA PrivateKey { get; }

        public string TokenUri { get; }

        /// <summary>The SHA-256 of the whole key file: which key a kept token was made with.</summary>
        public string Fingerprint { get; }

        public void Dispose() => PrivateKey.Dispose();

        /// <exception cref="StoreCredentialsException">The text is not a service account's key file (<see cref="CredentialProblem.Malformed"/>).</exception>
        public static GoogleServiceAccount Parse(string keyFile)
        {
            KeyFile file;
            try
            {
                file = JsonSerializer.Deserialize<KeyFile>(keyFile) ?? throw Malformed("is empty");
            }
            catch (JsonException unreadable)
            {
                throw new StoreCredentialsException(CredentialProblem.Malformed, "GOOGLE_PLAY_SERVICE_ACCOUNT_JSON is not JSON.", unreadable);
            }

            if (file.Type != "service_account" || string.IsNullOrEmpty(file.ClientEmail) || string.IsNullOrEmpty(file.PrivateKey))
            {
                throw Malformed("is not a service account's key file (type, client_email and private_key)");
            }

            var key = RSA.Create();
            try
            {
                key.ImportFromPem(file.PrivateKey);
            }
            catch (Exception notAKey) when (notAKey is ArgumentException or CryptographicException)
            {
                key.Dispose();
                throw new StoreCredentialsException(CredentialProblem.Malformed, "GOOGLE_PLAY_SERVICE_ACCOUNT_JSON's private_key is not an RSA key in PEM.", notAKey);
            }

            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyFile)));
            return new GoogleServiceAccount(file.ClientEmail, key, string.IsNullOrEmpty(file.TokenUri) ? DefaultTokenUri : file.TokenUri, fingerprint);
        }

        private static StoreCredentialsException Malformed(string what) =>
            new StoreCredentialsException(CredentialProblem.Malformed, $"GOOGLE_PLAY_SERVICE_ACCOUNT_JSON {what}.");

        private sealed class KeyFile
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("client_email")]
            public string? ClientEmail { get; set; }

            [JsonPropertyName("private_key")]
            public string? PrivateKey { get; set; }

            [JsonPropertyName("token_uri")]
            public string? TokenUri { get; set; }
        }
    }

    /// <summary>
    /// Google's access tokens for the Play Developer API (docs/research/server-v1.md §4.3): a JWT the service account
    /// signs with RS256, for the scope androidpublisher, traded at Google's token address for an access token, which
    /// is kept for at most 55 minutes (Google gives an hour).
    /// </summary>
    public sealed class GoogleAccessTokens
    {
        public const string Scope = "https://www.googleapis.com/auth/androidpublisher";

        private static readonly TimeSpan LongestKept = TimeSpan.FromMinutes(55);

        private static readonly TimeSpan AssertionLife = TimeSpan.FromHours(1);

        private readonly HttpClient _http;
        private readonly IClock _clock;
        private readonly TokenSlot _slot = new TokenSlot();

        public GoogleAccessTokens(HttpClient http, IClock clock)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <exception cref="StoreCredentialsException">Google refuses the service account (<see cref="CredentialProblem.Refused"/>).</exception>
        /// <exception cref="OutsideUnreachableException">Google's token address did not answer.</exception>
        public async Task<string> AccessTokenAsync(GoogleServiceAccount account, CancellationToken cancellation)
        {
            DateTimeOffset now = _clock.UtcNow;
            if (_slot.Held(account.Fingerprint, now) is string held)
            {
                return held;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, account.TokenUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = AssertionOf(account, now),
                }),
            };
            OutsideAnswer answer = await OutsideCalls.SendAsync(_http, request, "Google's token address", cancellation).ConfigureAwait(false);
            if (answer.IsPassing)
            {
                throw new OutsideUnreachableException($"Google's token address answered {(int)answer.Status}.", new HttpRequestException(answer.Body));
            }

            if (!answer.IsSuccess)
            {
                throw new StoreCredentialsException(CredentialProblem.Refused, $"Google's token address refused the service account {account.ClientEmail} ({(int)answer.Status}).");
            }

            TokenAnswer token = StoreJson.Read<TokenAnswer>(answer.Body, "Google's token answer");
            if (string.IsNullOrEmpty(token.AccessToken))
            {
                throw new StoreAnswerUnreadableException("Google's token answer holds no access_token.");
            }

            string accessToken = token.AccessToken;
            TimeSpan life = TimeSpan.FromSeconds(Math.Max(0, token.ExpiresIn - 300));
            _slot.Keep(account.Fingerprint, accessToken, now + (life < LongestKept ? life : LongestKept));
            return accessToken;
        }

        /// <summary>Forgets the kept token: Google refused it before its time.</summary>
        public void Forget() => _slot.Drop();

        /// <summary>The signed JWT the service account trades for an access token (RFC 7523).</summary>
        internal static string AssertionOf(GoogleServiceAccount account, DateTimeOffset now)
        {
            string header = OutsideCalls.Base64Url("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
            string claims = OutsideCalls.Base64Url(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["iss"] = account.ClientEmail,
                ["scope"] = Scope,
                ["aud"] = account.TokenUri,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["exp"] = (now + AssertionLife).ToUnixTimeSeconds(),
            }));
            byte[] signature = account.PrivateKey.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{header}.{claims}.{OutsideCalls.Base64Url(signature)}";
        }

        private sealed class TokenAnswer
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public long ExpiresIn { get; set; }
        }
    }
}
