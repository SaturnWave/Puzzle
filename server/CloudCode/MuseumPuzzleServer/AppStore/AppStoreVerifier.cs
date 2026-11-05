using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Catalogue;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;
using Puzzle.Client.Purchases;

namespace MuseumPuzzle.Server.AppStore
{
    /// <summary>Apple's two worlds of payments. A closed set: each has a server of its own.</summary>
    public enum AppleEnvironment
    {
        /// <summary>Real payments: api.storekit.apple.com.</summary>
        Production,

        /// <summary>Test payments (sandbox testers, App Review): api.storekit-sandbox.apple.com. The ledger marks them as tests.</summary>
        Sandbox,
    }

    /// <summary>What Apple's refund history came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class RefundHistory
    {
        private RefundHistory()
        {
        }

        /// <summary>The ids of the refunded transactions.</summary>
        public sealed class Read : RefundHistory
        {
            public Read(IReadOnlyCollection<string> refundedTransactions)
            {
                RefundedTransactions = refundedTransactions;
            }

            public IReadOnlyCollection<string> RefundedTransactions { get; }
        }

        public sealed class Failed : RefundHistory
        {
            public Failed(UnavailableReason reason)
            {
                Reason = reason;
            }

            public UnavailableReason Reason { get; }
        }
    }

    /// <summary>
    /// The App Store's side of the server (docs/research/server-v1.md §4.4, §4.6): the signed transaction the game
    /// sends must be Apple's (<see cref="SignedTransactions"/>) and say this app, this product, this transaction and,
    /// for a pack of coins, this player; then Apple's server is asked for the transaction as it is now (Get
    /// Transaction Info), and once a day for a player's refunds (Get Refund History). Apple is asked with a JWT
    /// signed by the In-App Purchase key of the secrets APPLE_IAP_PRIVATE_KEY, APPLE_IAP_KEY_ID and APPLE_ISSUER_ID.
    /// </summary>
    public sealed class AppStoreVerifier : IStoreVerifier
    {
        private readonly HttpClient _http;
        private readonly AppStoreTokens _tokens;
        private readonly SignedTransactions _signatures;
        private readonly ILogger _log;

        public AppStoreVerifier(HttpClient http, AppStoreTokens tokens, SignedTransactions signatures, ILogger log)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public PurchaseStore Store => PurchaseStore.AppleAppStore;

        public static string ServerOf(AppleEnvironment environment) => environment switch
        {
            AppleEnvironment.Production => "https://api.storekit.apple.com",
            AppleEnvironment.Sandbox => "https://api.storekit-sandbox.apple.com",
            _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "An environment Apple does not have."),
        };

        public async Task<StoreCheck> CheckAsync(PaymentClaim claim, CatalogueProduct product, PlayerId player, ISecrets secrets, CancellationToken cancellation)
        {
            if (claim.Proof.Length == 0)
            {
                return new StoreCheck.Unreachable(UnavailableReason.NoSignedTransaction);
            }

            AppleTransaction sent;
            try
            {
                sent = _signatures.Verify(claim.Proof);
            }
            catch (AppleSignatureException notApple)
            {
                _log.LogWarning(notApple, "A signed transaction the game sent is not Apple's.");
                return new StoreCheck.Rejected(InvalidReason.NotFromTheStore);
            }

            if (EnvironmentOf(sent) is not AppleEnvironment environment)
            {
                return new StoreCheck.Rejected(InvalidReason.NotFromTheStore);
            }

            if (Judged(sent, claim.Token, product, player) is StoreCheck.Rejected refused)
            {
                return refused;
            }

            try
            {
                OutsideAnswer answer = await AskAsync(secrets, $"{ServerOf(environment)}/inApps/v1/transactions/{Uri.EscapeDataString(claim.Token)}", cancellation).ConfigureAwait(false);
                return ConfirmedByApple(answer, claim.Token, product, player, environment);
            }
            catch (StoreCredentialsException credentials)
            {
                _log.LogError(credentials, "The App Store could not be asked about a payment: {Problem}", credentials.Problem);
                return new StoreCheck.Unreachable(credentials.Problem == CredentialProblem.Refused ? UnavailableReason.CredentialsRefused : UnavailableReason.CredentialsMissing);
            }
            catch (OutsideUnreachableException unreachable)
            {
                _log.LogWarning(unreachable, "The App Store did not answer about a payment.");
                return new StoreCheck.Unreachable(UnavailableReason.StoreUnreachable);
            }
            catch (StoreAnswerUnreadableException unreadable)
            {
                _log.LogError(unreadable, "The App Store's answer about a payment could not be read.");
                return new StoreCheck.Unreachable(UnavailableReason.StoreAnswerUnreadable);
            }
        }

        /// <summary>
        /// The ids of the refunded transactions of the Apple account that made <paramref name="transactionId"/> (Get
        /// Refund History: every page, newest first, Apple's signature checked on each).
        /// </summary>
        /// <exception cref="SecretsUnavailableException">The Secret Manager could not be reached.</exception>
        public async Task<RefundHistory> RefundsAsync(string transactionId, AppleEnvironment environment, ISecrets secrets, CancellationToken cancellation)
        {
            var refunded = new HashSet<string>(StringComparer.Ordinal);
            string? revision = null;
            try
            {
                do
                {
                    string url = $"{ServerOf(environment)}/inApps/v2/refund/lookup/{Uri.EscapeDataString(transactionId)}" + (revision is string at ? $"?revision={Uri.EscapeDataString(at)}" : string.Empty);
                    OutsideAnswer answer = await AskAsync(secrets, url, cancellation).ConfigureAwait(false);
                    if (!answer.IsSuccess)
                    {
                        _log.LogWarning("The App Store's refund history answered {Status}.", (int)answer.Status);
                        return new RefundHistory.Failed(answer.Status == HttpStatusCode.Unauthorized ? UnavailableReason.CredentialsRefused : UnavailableReason.StoreUnreachable);
                    }

                    RefundPage page = StoreJson.Read<RefundPage>(answer.Body, "The App Store's refund history");
                    foreach (string jws in page.SignedTransactions ?? new List<string>())
                    {
                        refunded.Add(_signatures.Verify(jws).TransactionId ?? throw new StoreAnswerUnreadableException("A refunded transaction has no transactionId."));
                    }

                    revision = page.HasMore && !string.IsNullOrEmpty(page.Revision) ? page.Revision : null;
                }
                while (revision != null);
                return new RefundHistory.Read(refunded);
            }
            catch (StoreCredentialsException credentials)
            {
                _log.LogError(credentials, "The App Store's refund history could not be asked for: {Problem}", credentials.Problem);
                return new RefundHistory.Failed(credentials.Problem == CredentialProblem.Refused ? UnavailableReason.CredentialsRefused : UnavailableReason.CredentialsMissing);
            }
            catch (OutsideUnreachableException unreachable)
            {
                _log.LogWarning(unreachable, "The App Store's refund history did not answer.");
                return new RefundHistory.Failed(UnavailableReason.StoreUnreachable);
            }
            catch (StoreAnswerUnreadableException unreadable)
            {
                _log.LogError(unreadable, "The App Store's refund history could not be read.");
                return new RefundHistory.Failed(UnavailableReason.StoreAnswerUnreadable);
            }
            catch (AppleSignatureException notApple)
            {
                _log.LogError(notApple, "The App Store's refund history holds a transaction whose signature is not Apple's.");
                return new RefundHistory.Failed(UnavailableReason.StoreAnswerUnreadable);
            }
        }

        private static AppleEnvironment? EnvironmentOf(AppleTransaction transaction) => transaction.Environment switch
        {
            "Production" => AppleEnvironment.Production,
            "Sandbox" => AppleEnvironment.Sandbox,
            _ => null,
        };

        /// <summary>What a transaction Apple signed says of the payment, before or after Apple's server is asked; nothing to hold against it when it is this player's payment of this product in this app.</summary>
        private static StoreCheck.Rejected? Judged(AppleTransaction transaction, string transactionId, CatalogueProduct product, PlayerId player)
        {
            if (transaction.BundleId != ProductCatalogue.ApplicationId)
            {
                return new StoreCheck.Rejected(InvalidReason.WrongApp);
            }

            if (transaction.ProductId != product.StoreId)
            {
                return new StoreCheck.Rejected(InvalidReason.ProductMismatch);
            }

            if (transaction.TransactionId != transactionId)
            {
                return new StoreCheck.Rejected(InvalidReason.NotFromTheStore);
            }

            if (transaction.RevocationDate != null)
            {
                return new StoreCheck.Rejected(InvalidReason.Refunded);
            }

            bool namesAnotherPlayer = !string.IsNullOrEmpty(transaction.AppAccountToken)
                && !string.Equals(transaction.AppAccountToken, StoreAccount.AppStoreAccountTokenOf(player.Value), StringComparison.OrdinalIgnoreCase);
            return product.IsGoodForOnePlayerOnly && namesAnotherPlayer ? new StoreCheck.Rejected(InvalidReason.AnotherPlayers) : null;
        }

        private StoreCheck ConfirmedByApple(OutsideAnswer answer, string transactionId, CatalogueProduct product, PlayerId player, AppleEnvironment environment)
        {
            if (answer.Status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                return new StoreCheck.Rejected(InvalidReason.NotFromTheStore);
            }

            if (answer.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new StoreCredentialsException(CredentialProblem.Refused, $"The App Store refused the In-App Purchase key ({(int)answer.Status}).");
            }

            if (!answer.IsSuccess)
            {
                _log.LogWarning("The App Store answered {Status} about a payment.", (int)answer.Status);
                return new StoreCheck.Unreachable(UnavailableReason.StoreUnreachable);
            }

            TransactionInfo info = StoreJson.Read<TransactionInfo>(answer.Body, "The App Store's transaction");
            AppleTransaction now;
            try
            {
                now = _signatures.Verify(info.SignedTransactionInfo ?? string.Empty);
            }
            catch (AppleSignatureException notApple)
            {
                throw new StoreAnswerUnreadableException("The App Store's transaction is not signed by Apple.", notApple);
            }

            if (EnvironmentOf(now) != environment)
            {
                return new StoreCheck.Rejected(InvalidReason.NotFromTheStore);
            }

            return (StoreCheck?)Judged(now, transactionId, product, player) ?? new StoreCheck.Confirmed(transactionId, isTest: environment == AppleEnvironment.Sandbox);
        }

        /// <summary>A GET with a JWT of the In-App Purchase key; once more with a fresh JWT when Apple refuses a kept one.</summary>
        private async Task<OutsideAnswer> AskAsync(ISecrets secrets, string url, CancellationToken cancellation)
        {
            OutsideAnswer answer = await GetAsync(url, await _tokens.TokenAsync(secrets, cancellation).ConfigureAwait(false), cancellation).ConfigureAwait(false);
            if (answer.Status != HttpStatusCode.Unauthorized)
            {
                return answer;
            }

            _tokens.Forget();
            return await GetAsync(url, await _tokens.TokenAsync(secrets, cancellation).ConfigureAwait(false), cancellation).ConfigureAwait(false);
        }

        private async Task<OutsideAnswer> GetAsync(string url, string token, CancellationToken cancellation)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await OutsideCalls.SendAsync(_http, request, "The App Store", cancellation).ConfigureAwait(false);
        }

        // Apple's layouts (developer.apple.com/documentation/appstoreserverapi: TransactionInfoResponse, RefundHistoryResponse).
        private sealed class TransactionInfo
        {
            [JsonPropertyName("signedTransactionInfo")]
            public string? SignedTransactionInfo { get; set; }
        }

        private sealed class RefundPage
        {
            [JsonPropertyName("signedTransactions")]
            public List<string>? SignedTransactions { get; set; }

            [JsonPropertyName("revision")]
            public string? Revision { get; set; }

            [JsonPropertyName("hasMore")]
            public bool HasMore { get; set; }
        }
    }
}
