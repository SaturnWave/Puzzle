using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MuseumPuzzle.Server.AppStore;
using MuseumPuzzle.Server.Catalogue;
using MuseumPuzzle.Server.GooglePlay;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;

namespace MuseumPuzzle.Server.Tests;

/// <summary>What the tests share: the shipped catalogue, two players, a day, and the products' ids.</summary>
public static class TestWorld
{
    public const string App = ProductCatalogue.ApplicationId;

    public const string Pouch = "coins.pouch";

    public const string Bag = "coins.bag";

    public const string StarterPack = "starter.apprentice_kit";

    public static readonly PlayerId Ada = new("ada-7Hq2");

    public static readonly PlayerId Bea = new("bea-9Kx4");

    public static readonly DateTimeOffset Day = new(2026, 11, 3, 18, 22, 5, TimeSpan.Zero);

    public static ProductCatalogue Shipped { get; } = ProductCatalogue.Shipped();

    public static CatalogueProduct Product(string suffix) => Shipped.FindBySuffix(suffix) ?? throw new ArgumentException($"The game sells no {suffix}.");

    public static string StoreIdOf(string suffix) => $"{App}.{suffix}";
}

/// <summary>Google's side as the tests play it: a service account of their own, and Google's answers in Google's layouts.</summary>
public sealed class TestGoogle : IDisposable
{
    public const string TokenUrl = "https://oauth2.googleapis.com/token";

    public const string AccessToken = "ya29.test-access-token";

    private TestGoogle(RSA key)
    {
        Key = key;
        KeyFile = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "museum-puzzle-test",
            ["private_key_id"] = "0123456789abcdef",
            ["private_key"] = key.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "play-orders@museum-puzzle-test.iam.gserviceaccount.com",
            ["token_uri"] = TokenUrl,
        });
    }

    public RSA Key { get; }

    /// <summary>A service account's JSON key file, as Google Cloud gives it, with a key the tests made.</summary>
    public string KeyFile { get; }

    public static string AppRoot => $"{GooglePlayVerifier.ApiRoot}{TestWorld.App}";

    public static string PurchaseUrl(string token) => $"{AppRoot}/purchases/productsv2/tokens/{token}";

    public static string VoidedUrl => $"{AppRoot}/purchases/voidedpurchases";

    public static TestGoogle Create() => new(RSA.Create(2048));

    public FakeSecrets Secrets() => new FakeSecrets().With(SecretName.GooglePlayServiceAccountJson, KeyFile);

    /// <summary>The network with Google's token address answering a good token.</summary>
    public static ScriptedNetwork Network() => new ScriptedNetwork().On(TokenUrl, HttpStatusCode.OK, $"{{\"access_token\":\"{AccessToken}\",\"expires_in\":3599,\"token_type\":\"Bearer\"}}");

    /// <summary>A ProductPurchaseV2 in Google's layout.</summary>
    public static string Purchase(
        string productId, string state = "PURCHASED", string? buyer = null, bool test = false, string? order = "GPA.3312-4405-6102-77801", int quantity = 1, int refundable = 1)
    {
        var purchase = new Dictionary<string, object>
        {
            ["kind"] = "androidpublisher#productPurchaseV2",
            ["productLineItem"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["productId"] = productId,
                    ["productOfferDetails"] = new Dictionary<string, object>
                    {
                        ["purchaseOptionId"] = "legacy-base",
                        ["quantity"] = quantity,
                        ["refundableQuantity"] = refundable,
                        ["consumptionState"] = "CONSUMPTION_STATE_YET_TO_BE_CONSUMED",
                    },
                },
            },
            ["purchaseStateContext"] = new Dictionary<string, object> { ["purchaseState"] = state },
            ["regionCode"] = "TR",
            ["purchaseCompletionTime"] = "2026-11-03T18:21:58.123Z",
            ["acknowledgementState"] = "ACKNOWLEDGEMENT_STATE_PENDING",
        };
        if (order != null)
        {
            purchase["orderId"] = order;
        }

        if (buyer != null)
        {
            purchase["obfuscatedExternalAccountId"] = buyer;
        }

        if (test)
        {
            purchase["testPurchaseContext"] = new Dictionary<string, object> { ["fopType"] = "TEST" };
        }

        return JsonSerializer.Serialize(purchase);
    }

    /// <summary>A page of the list of voided purchases in Google's layout; Google writes the times as strings.</summary>
    public static string Voided(IEnumerable<(string Token, DateTimeOffset At)> voided, string? nextPage = null)
    {
        var page = new Dictionary<string, object>
        {
            ["voidedPurchases"] = voided.Select(item => new Dictionary<string, object>
            {
                ["kind"] = "androidpublisher#voidedPurchase",
                ["purchaseToken"] = item.Token,
                ["purchaseTimeMillis"] = (item.At - TimeSpan.FromDays(2)).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["voidedTimeMillis"] = item.At.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["orderId"] = "GPA.0000-0000-0000-00000",
                ["voidedSource"] = 0,
                ["voidedReason"] = 2,
            }).ToArray(),
        };
        if (nextPage != null)
        {
            page["tokenPagination"] = new Dictionary<string, string> { ["nextPageToken"] = nextPage };
        }

        return JsonSerializer.Serialize(page);
    }

    public static GooglePlayVerifier Verifier(ScriptedNetwork network, IClock clock) => new(network.Client(), new GoogleAccessTokens(network.Client(), clock), NullLogger.Instance);

    public void Dispose() => Key.Dispose();
}

/// <summary>
/// Apple's side as the tests play it: a certificate chain of their own shaped like Apple's (a root, an intermediate
/// with Apple's marker 1.2.840.113635.100.6.2.1, a signing certificate with 1.2.840.113635.100.6.11.1), transactions
/// signed with it in Apple's layout, and an In-App Purchase key of their own.
/// </summary>
public sealed class TestApple : IDisposable
{
    public const string KeyId = "2X9R4HXF34";

    public const string IssuerId = "57246542-96fe-1a63-e053-0824d011072a";

    private readonly ECDsa _rootKey;
    private readonly ECDsa _intermediateKey;
    private readonly ECDsa _signingKey;

    private TestApple(bool intermediateMarker, bool signingMarker, DateTimeOffset signingNotAfter)
    {
        DateTimeOffset from = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset until = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        _intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        IapKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var rootRequest = new CertificateRequest("CN=Test Root CA - G3, O=Museum Puzzle Tests", _rootKey, HashAlgorithmName.SHA384);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        Root = rootRequest.CreateSelfSigned(from, until);

        var intermediateRequest = new CertificateRequest("CN=Test Worldwide Developer Relations, O=Museum Puzzle Tests", _intermediateKey, HashAlgorithmName.SHA384);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        if (intermediateMarker)
        {
            intermediateRequest.CertificateExtensions.Add(new X509Extension(new Oid(SignedTransactions.IntermediateMarker), new byte[] { 0x05, 0x00 }, false));
        }

        using X509Certificate2 intermediate = intermediateRequest.Create(Root, from, until, new byte[] { 1, 2, 3, 4 });
        Intermediate = intermediate.CopyWithPrivateKey(_intermediateKey);

        var signingRequest = new CertificateRequest("CN=Test Store Receipt Signing, O=Museum Puzzle Tests", _signingKey, HashAlgorithmName.SHA384);
        signingRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        signingRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        if (signingMarker)
        {
            signingRequest.CertificateExtensions.Add(new X509Extension(new Oid(SignedTransactions.SigningMarker), new byte[] { 0x05, 0x00 }, false));
        }

        Signing = signingRequest.Create(Intermediate, from, signingNotAfter, new byte[] { 5, 6, 7, 8 });
    }

    public X509Certificate2 Root { get; }

    public X509Certificate2 Intermediate { get; }

    public X509Certificate2 Signing { get; }

    /// <summary>The tests' In-App Purchase key, the one APPLE_IAP_PRIVATE_KEY holds.</summary>
    public ECDsa IapKey { get; }

    public SignedTransactions Signatures => new(Root.RawData);

    public static TestApple Create(bool intermediateMarker = true, bool signingMarker = true, DateTimeOffset? signingNotAfter = null) =>
        new(intermediateMarker, signingMarker, signingNotAfter ?? new DateTimeOffset(2039, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public static string TransactionUrl(AppleEnvironment environment, string transactionId) => $"{AppStoreVerifier.ServerOf(environment)}/inApps/v1/transactions/{transactionId}";

    public static string RefundsUrl(AppleEnvironment environment, string transactionId) => $"{AppStoreVerifier.ServerOf(environment)}/inApps/v2/refund/lookup/{transactionId}";

    public FakeSecrets Secrets() => new FakeSecrets()
        .With(SecretName.AppleIapPrivateKey, IapKey.ExportPkcs8PrivateKeyPem())
        .With(SecretName.AppleIapKeyId, KeyId)
        .With(SecretName.AppleIssuerId, IssuerId);

    /// <summary>A transaction in Apple's layout (JWSTransactionDecodedPayload), signed with the tests' chain.</summary>
    public string Transaction(
        string transactionId,
        string productId,
        string environment = "Production",
        string bundleId = TestWorld.App,
        string? appAccountToken = null,
        DateTimeOffset? revokedAt = null,
        DateTimeOffset? signedAt = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["transactionId"] = transactionId,
            ["originalTransactionId"] = transactionId,
            ["bundleId"] = bundleId,
            ["productId"] = productId,
            ["purchaseDate"] = TestWorld.Day.ToUnixTimeMilliseconds(),
            ["originalPurchaseDate"] = TestWorld.Day.ToUnixTimeMilliseconds(),
            ["quantity"] = 1,
            ["type"] = productId.EndsWith(TestWorld.StarterPack, StringComparison.Ordinal) ? "Non-Consumable" : "Consumable",
            ["inAppOwnershipType"] = "PURCHASED",
            ["signedDate"] = (signedAt ?? TestWorld.Day).ToUnixTimeMilliseconds(),
            ["environment"] = environment,
            ["transactionReason"] = "PURCHASE",
            ["storefront"] = "TUR",
            ["storefrontId"] = "143480",
            ["price"] = 1990,
            ["currency"] = "USD",
        };
        if (appAccountToken != null)
        {
            payload["appAccountToken"] = appAccountToken;
        }

        if (revokedAt is DateTimeOffset revoked)
        {
            payload["revocationDate"] = revoked.ToUnixTimeMilliseconds();
            payload["revocationReason"] = 0;
        }

        return Sign(JsonSerializer.Serialize(payload));
    }

    /// <summary>A JWS of this payload: ES256 with the signing key, the three certificates in x5c.</summary>
    public string Sign(string payloadJson, ECDsa? signingKey = null)
    {
        string header = OutsideCalls.Base64Url(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["alg"] = "ES256",
            ["x5c"] = new[] { Signing, Intermediate, Root }.Select(certificate => Convert.ToBase64String(certificate.RawData)).ToArray(),
        }));
        string payload = OutsideCalls.Base64Url(payloadJson);
        byte[] signature = (signingKey ?? _signingKey).SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{header}.{payload}.{OutsideCalls.Base64Url(signature)}";
    }

    public static string TransactionInfo(string signedTransaction) => JsonSerializer.Serialize(new Dictionary<string, string> { ["signedTransactionInfo"] = signedTransaction });

    public static string RefundPage(IEnumerable<string> signedTransactions, bool hasMore = false, string revision = "rev-1") =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["signedTransactions"] = signedTransactions.ToArray(), ["revision"] = revision, ["hasMore"] = hasMore });

    public AppStoreVerifier Verifier(ScriptedNetwork network, IClock clock) => new(network.Client(), new AppStoreTokens(clock), Signatures, NullLogger.Instance);

    public void Dispose()
    {
        Signing.Dispose();
        Intermediate.Dispose();
        Root.Dispose();
        IapKey.Dispose();
        _signingKey.Dispose();
        _intermediateKey.Dispose();
        _rootKey.Dispose();
    }
}
