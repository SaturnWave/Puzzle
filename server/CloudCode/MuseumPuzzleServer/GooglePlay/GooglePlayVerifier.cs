using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace MuseumPuzzle.Server.GooglePlay
{
    /// <summary>A payment Google voided: the purchase token that names its receipt, and when Google voided it.</summary>
    public sealed class VoidedPurchase
    {
        public VoidedPurchase(string purchaseToken, DateTimeOffset voidedAt)
        {
            PurchaseToken = purchaseToken;
            VoidedAt = voidedAt;
        }

        public string PurchaseToken { get; }

        public DateTimeOffset VoidedAt { get; }
    }

    /// <summary>A page of Google's list of voided purchases. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class VoidedPage
    {
        private VoidedPage()
        {
        }

        /// <summary>The page; the token of the next one, or nothing on the last page.</summary>
        public sealed class Read : VoidedPage
        {
            public Read(IReadOnlyList<VoidedPurchase> purchases, string? nextPage)
            {
                Purchases = purchases;
                NextPage = nextPage;
            }

            public IReadOnlyList<VoidedPurchase> Purchases { get; }

            public string? NextPage { get; }
        }

        public sealed class Failed : VoidedPage
        {
            public Failed(UnavailableReason reason)
            {
                Reason = reason;
            }

            public UnavailableReason Reason { get; }
        }
    }

    /// <summary>
    /// Google Play's side of the server (docs/research/server-v1.md §4.3, §4.6): a payment is asked of the Play
    /// Developer API's <c>purchases.productsv2</c> by its purchase token, and the daily job reads the list of voided
    /// purchases. Google is asked with the service account of the secret GOOGLE_PLAY_SERVICE_ACCOUNT_JSON. The game
    /// confirms (acknowledges or consumes) a payment itself once it has delivered it; the server never does.
    /// </summary>
    public sealed class GooglePlayVerifier : IStoreVerifier
    {
        public const string ApiRoot = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/";

        /// <summary>The most a page of voided purchases holds (Google's own bound).</summary>
        public const int VoidedPageSize = 1000;

        private readonly HttpClient _http;
        private readonly GoogleAccessTokens _tokens;
        private readonly ILogger _log;

        public GooglePlayVerifier(HttpClient http, GoogleAccessTokens tokens, ILogger log)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public PurchaseStore Store => PurchaseStore.GooglePlay;

        private static string AppRoot => $"{ApiRoot}{ProductCatalogue.ApplicationId}";

        public async Task<StoreCheck> CheckAsync(PaymentClaim claim, CatalogueProduct product, PlayerId player, ISecrets secrets, CancellationToken cancellation)
        {
            try
            {
                using GoogleServiceAccount account = await AccountAsync(secrets, cancellation).ConfigureAwait(false);
                OutsideAnswer answer = await AskAsync(account, $"{AppRoot}/purchases/productsv2/tokens/{Uri.EscapeDataString(claim.Token)}", cancellation).ConfigureAwait(false);
                return Judged(answer, product, player);
            }
            catch (StoreCredentialsException credentials)
            {
                _log.LogError(credentials, "Google Play could not be asked about a payment: {Problem}", credentials.Problem);
                return new StoreCheck.Unreachable(credentials.Problem == CredentialProblem.Refused ? UnavailableReason.CredentialsRefused : UnavailableReason.CredentialsMissing);
            }
            catch (OutsideUnreachableException unreachable)
            {
                _log.LogWarning(unreachable, "Google Play did not answer about a payment.");
                return new StoreCheck.Unreachable(UnavailableReason.StoreUnreachable);
            }
            catch (StoreAnswerUnreadableException unreadable)
            {
                _log.LogError(unreadable, "Google Play's answer about a payment could not be read.");
                return new StoreCheck.Unreachable(UnavailableReason.StoreAnswerUnreadable);
            }
        }

        /// <summary>A page of the payments Google voided between the two times (at most 30 days back); with a page token, the page it names.</summary>
        /// <exception cref="SecretsUnavailableException">The Secret Manager could not be reached.</exception>
        public async Task<VoidedPage> VoidedPageAsync(DateTimeOffset from, DateTimeOffset until, string? pageToken, ISecrets secrets, CancellationToken cancellation)
        {
            string query = pageToken is string token
                ? $"maxResults={VoidedPageSize}&token={Uri.EscapeDataString(token)}"
                : string.Create(CultureInfo.InvariantCulture, $"startTime={from.ToUnixTimeMilliseconds()}&endTime={until.ToUnixTimeMilliseconds()}&maxResults={VoidedPageSize}");
            try
            {
                using GoogleServiceAccount account = await AccountAsync(secrets, cancellation).ConfigureAwait(false);
                OutsideAnswer answer = await AskAsync(account, $"{AppRoot}/purchases/voidedpurchases?{query}", cancellation).ConfigureAwait(false);
                if (!answer.IsSuccess)
                {
                    _log.LogWarning("Google's list of voided purchases answered {Status}.", (int)answer.Status);
                    return new VoidedPage.Failed(answer.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? UnavailableReason.CredentialsRefused : UnavailableReason.StoreUnreachable);
                }

                VoidedList list = StoreJson.Read<VoidedList>(answer.Body, "Google's list of voided purchases");
                return new VoidedPage.Read(
                    (list.VoidedPurchases ?? new List<VoidedLine>())
                        .Where(line => !string.IsNullOrEmpty(line.PurchaseToken))
                        .Select(line => new VoidedPurchase(line.PurchaseToken!, VoidedAtOf(line)))
                        .ToArray(),
                    string.IsNullOrEmpty(list.TokenPagination?.NextPageToken) ? null : list.TokenPagination.NextPageToken);
            }
            catch (StoreCredentialsException credentials)
            {
                _log.LogError(credentials, "Google's list of voided purchases could not be asked for: {Problem}", credentials.Problem);
                return new VoidedPage.Failed(credentials.Problem == CredentialProblem.Refused ? UnavailableReason.CredentialsRefused : UnavailableReason.CredentialsMissing);
            }
            catch (OutsideUnreachableException unreachable)
            {
                _log.LogWarning(unreachable, "Google's list of voided purchases did not answer.");
                return new VoidedPage.Failed(UnavailableReason.StoreUnreachable);
            }
            catch (StoreAnswerUnreadableException unreadable)
            {
                _log.LogError(unreadable, "Google's list of voided purchases could not be read.");
                return new VoidedPage.Failed(UnavailableReason.StoreAnswerUnreadable);
            }
        }

        /// <summary>
        /// What Google's answer says of the payment. The receipt must be for the product the game named and only for it;
        /// a pending payment is not given yet; a cancelled or wholly refunded one is never given; a pack of coins whose
        /// receipt names another player as its buyer (the account id the game attached, <see cref="StoreAccount"/>)
        /// is another player's. The starter pack comes back to every Unity account its store account plays on, so the
        /// buyer it names is not held against it.
        /// </summary>
        internal StoreCheck Judged(OutsideAnswer answer, CatalogueProduct product, PlayerId player)
        {
            if (answer.Status is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return new StoreCheck.Rejected(InvalidReason.NotFromTheStore);
            }

            if (answer.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new StoreCredentialsException(CredentialProblem.Refused, $"Google Play refused the service account ({(int)answer.Status}).");
            }

            if (!answer.IsSuccess)
            {
                _log.LogWarning("Google Play answered {Status} about a payment.", (int)answer.Status);
                return new StoreCheck.Unreachable(UnavailableReason.StoreUnreachable);
            }

            ProductPurchase purchase = StoreJson.Read<ProductPurchase>(answer.Body, "Google Play's purchase");
            List<LineItem> lines = purchase.ProductLineItem ?? new List<LineItem>();
            if (lines.Count == 0 || lines.Any(line => line.ProductId != product.StoreId))
            {
                return new StoreCheck.Rejected(InvalidReason.ProductMismatch);
            }

            switch (purchase.PurchaseStateContext?.PurchaseState)
            {
                case "PURCHASED":
                    break;
                case "PENDING":
                    return new StoreCheck.Pending();
                case "CANCELLED":
                    return new StoreCheck.Rejected(InvalidReason.Cancelled);
                default:
                    throw new StoreAnswerUnreadableException($"Google Play's purchase is in the state '{purchase.PurchaseStateContext?.PurchaseState}'.");
            }

            if (lines.Any(line => line.ProductOfferDetails is OfferDetails offer && offer.Quantity > 0 && offer.RefundableQuantity == 0))
            {
                return new StoreCheck.Rejected(InvalidReason.Refunded);
            }

            string? buyer = purchase.ObfuscatedExternalAccountId;
            if (product.IsGoodForOnePlayerOnly && !string.IsNullOrEmpty(buyer) && buyer != StoreAccount.GooglePlayAccountOf(player.Value))
            {
                return new StoreCheck.Rejected(InvalidReason.AnotherPlayers);
            }

            return new StoreCheck.Confirmed(purchase.OrderId ?? string.Empty, isTest: purchase.TestPurchaseContext?.FopType == "TEST");
        }

        private static async Task<GoogleServiceAccount> AccountAsync(ISecrets secrets, CancellationToken cancellation)
        {
            string keyFile = await secrets.ReadAsync(SecretName.GooglePlayServiceAccountJson, cancellation).ConfigureAwait(false)
                ?? throw new StoreCredentialsException(CredentialProblem.Missing, "The secret GOOGLE_PLAY_SERVICE_ACCOUNT_JSON is not set.");
            return GoogleServiceAccount.Parse(keyFile);
        }

        /// <summary>A GET with the service account's access token; once more with a fresh token when Google refuses a kept one.</summary>
        private async Task<OutsideAnswer> AskAsync(GoogleServiceAccount account, string url, CancellationToken cancellation)
        {
            OutsideAnswer answer = await GetAsync(url, await _tokens.AccessTokenAsync(account, cancellation).ConfigureAwait(false), cancellation).ConfigureAwait(false);
            if (answer.Status != HttpStatusCode.Unauthorized)
            {
                return answer;
            }

            _tokens.Forget();
            return await GetAsync(url, await _tokens.AccessTokenAsync(account, cancellation).ConfigureAwait(false), cancellation).ConfigureAwait(false);
        }

        private async Task<OutsideAnswer> GetAsync(string url, string accessToken, CancellationToken cancellation)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await OutsideCalls.SendAsync(_http, request, "Google Play", cancellation).ConfigureAwait(false);
        }

        private static DateTimeOffset VoidedAtOf(VoidedLine line) =>
            long.TryParse(line.VoidedTimeMillis, NumberStyles.None, CultureInfo.InvariantCulture, out long millis)
                ? DateTimeOffset.FromUnixTimeMilliseconds(millis)
                : throw new StoreAnswerUnreadableException($"A voided purchase's voidedTimeMillis is '{line.VoidedTimeMillis}'.");

        // Google's layouts (developers.google.com/android-publisher/api-ref/rest/v3/purchases.productsv2 and
        // purchases.voidedpurchases). Google writes 64-bit numbers as strings.
        private sealed class ProductPurchase
        {
            [JsonPropertyName("productLineItem")]
            public List<LineItem>? ProductLineItem { get; set; }

            [JsonPropertyName("purchaseStateContext")]
            public StateContext? PurchaseStateContext { get; set; }

            [JsonPropertyName("testPurchaseContext")]
            public TestContext? TestPurchaseContext { get; set; }

            [JsonPropertyName("orderId")]
            public string? OrderId { get; set; }

            [JsonPropertyName("obfuscatedExternalAccountId")]
            public string? ObfuscatedExternalAccountId { get; set; }
        }

        private sealed class LineItem
        {
            [JsonPropertyName("productId")]
            public string? ProductId { get; set; }

            [JsonPropertyName("productOfferDetails")]
            public OfferDetails? ProductOfferDetails { get; set; }
        }

        private sealed class OfferDetails
        {
            [JsonPropertyName("quantity")]
            public int? Quantity { get; set; }

            [JsonPropertyName("refundableQuantity")]
            public int? RefundableQuantity { get; set; }
        }

        private sealed class StateContext
        {
            [JsonPropertyName("purchaseState")]
            public string? PurchaseState { get; set; }
        }

        private sealed class TestContext
        {
            [JsonPropertyName("fopType")]
            public string? FopType { get; set; }
        }

        private sealed class VoidedList
        {
            [JsonPropertyName("tokenPagination")]
            public Pagination? TokenPagination { get; set; }

            [JsonPropertyName("voidedPurchases")]
            public List<VoidedLine>? VoidedPurchases { get; set; }
        }

        private sealed class Pagination
        {
            [JsonPropertyName("nextPageToken")]
            public string? NextPageToken { get; set; }
        }

        private sealed class VoidedLine
        {
            [JsonPropertyName("purchaseToken")]
            public string? PurchaseToken { get; set; }

            [JsonPropertyName("voidedTimeMillis")]
            public string? VoidedTimeMillis { get; set; }
        }
    }
}
