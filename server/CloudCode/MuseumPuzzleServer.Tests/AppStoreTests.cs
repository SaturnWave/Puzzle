using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using MuseumPuzzle.Server.AppStore;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Purchases;
using Puzzle.Client.Purchases;
using static MuseumPuzzle.Server.Tests.TestWorld;

namespace MuseumPuzzle.Server.Tests;

/// <summary>The App Store's side: Apple's signatures, the judgement of a payment, Apple's server, the JWT and the refund history.</summary>
public sealed class AppStoreTests : IDisposable
{
    private const string TransactionId = "2000000912345678";

    private readonly TestApple _apple = TestApple.Create();
    private readonly HandClock _clock = new(Day);

    public void Dispose() => _apple.Dispose();

    [Fact]
    public void The_module_carries_apple_root_ca_g3_and_it_is_the_root_apples_own_library_trusts()
    {
        using Stream root = typeof(SignedTransactions).Assembly.GetManifestResourceStream("AppleRootCA-G3.cer")!;
        using var bytes = new MemoryStream();
        root.CopyTo(bytes);

        // The fingerprint apple.com/certificateauthority gives for Apple Root CA - G3.
        Assert.Equal("63343abfb89a6a03ebb57e9b3f5fa7be7c4f5c756f3017b3a8c488c3653e9179", Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant());
        Assert.Equal(Convert.FromBase64String(AppleRealChain.Root), bytes.ToArray());
    }

    [Fact]
    public void Apples_real_chain_leads_to_the_embedded_root_with_apples_markers()
    {
        byte[][] chain = { Convert.FromBase64String(AppleRealChain.Signing), Convert.FromBase64String(AppleRealChain.Intermediate), Convert.FromBase64String(AppleRealChain.Root) };

        using X509Certificate2 signer = SignedTransactions.WithAppleRoot().VerifyChain(chain, AppleRealChain.EffectiveDate);

        Assert.Contains("Receipt Signing", signer.Subject, StringComparison.Ordinal);
        Assert.Throws<AppleSignatureException>(() => SignedTransactions.WithAppleRoot().VerifyChain(chain, new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero)).Dispose());
        Assert.Throws<AppleSignatureException>(() => SignedTransactions.WithAppleRoot().VerifyChain(chain.Take(2).ToArray(), AppleRealChain.EffectiveDate).Dispose());
        Assert.Throws<AppleSignatureException>(() => _apple.Signatures.VerifyChain(chain, AppleRealChain.EffectiveDate).Dispose());
    }

    [Fact]
    public void A_transaction_signed_through_the_trusted_chain_is_read()
    {
        AppleTransaction transaction = _apple.Signatures.Verify(_apple.Transaction(TransactionId, StoreIdOf(Pouch)));

        Assert.Equal(TransactionId, transaction.TransactionId);
        Assert.Equal(StoreIdOf(Pouch), transaction.ProductId);
        Assert.Equal("Production", transaction.Environment);
    }

    [Fact]
    public void A_transaction_whose_signature_or_chain_is_not_apples_is_refused()
    {
        string good = _apple.Transaction(TransactionId, StoreIdOf(Pouch));
        string[] parts = good.Split('.');
        string tampered = $"{parts[0]}.{OutsideCalls.Base64Url(Encoding.UTF8.GetString(OutsideCalls.FromBase64Url(parts[1])).Replace("coins.pouch", "coins.treasure", StringComparison.Ordinal))}.{parts[2]}";
        using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestApple noIntermediateMarker = TestApple.Create(intermediateMarker: false);
        using TestApple noSigningMarker = TestApple.Create(signingMarker: false);
        using TestApple expired = TestApple.Create(signingNotAfter: Day.AddDays(-1));

        Assert.Throws<AppleSignatureException>(() => _apple.Signatures.Verify(tampered));
        Assert.Throws<AppleSignatureException>(() => _apple.Signatures.Verify(_apple.Sign($"{{\"signedDate\":{Day.ToUnixTimeMilliseconds()}}}", stranger)));
        Assert.Throws<AppleSignatureException>(() => SignedTransactions.WithAppleRoot().Verify(good));
        Assert.Throws<AppleSignatureException>(() => noIntermediateMarker.Signatures.Verify(noIntermediateMarker.Transaction(TransactionId, StoreIdOf(Pouch))));
        Assert.Throws<AppleSignatureException>(() => noSigningMarker.Signatures.Verify(noSigningMarker.Transaction(TransactionId, StoreIdOf(Pouch))));
        Assert.Throws<AppleSignatureException>(() => expired.Signatures.Verify(expired.Transaction(TransactionId, StoreIdOf(Pouch))));
        Assert.Throws<AppleSignatureException>(() => _apple.Signatures.Verify("not.a jws"));
    }

    [Fact]
    public async Task A_payment_apple_signed_and_its_server_confirms_is_confirmed_by_its_transaction()
    {
        string signed = _apple.Transaction(TransactionId, StoreIdOf(Pouch), appAccountToken: StoreAccount.AppStoreAccountTokenOf(Ada.Value));
        ScriptedNetwork network = new ScriptedNetwork().On(TestApple.TransactionUrl(AppleEnvironment.Production, TransactionId), HttpStatusCode.OK, TestApple.TransactionInfo(signed));

        var confirmed = Assert.IsType<StoreCheck.Confirmed>(await CheckAsync(network, Pouch, signed));

        Assert.Equal(TransactionId, confirmed.Order);
        Assert.False(confirmed.IsTest);
    }

    [Fact]
    public async Task A_sandbox_payment_is_asked_of_the_sandbox_and_marked_a_test()
    {
        string signed = _apple.Transaction(TransactionId, StoreIdOf(Pouch), environment: "Sandbox");
        ScriptedNetwork network = new ScriptedNetwork().On(TestApple.TransactionUrl(AppleEnvironment.Sandbox, TransactionId), HttpStatusCode.OK, TestApple.TransactionInfo(signed));

        Assert.True(Assert.IsType<StoreCheck.Confirmed>(await CheckAsync(network, Pouch, signed)).IsTest);
        Assert.Equal(1, network.CountTo("https://api.storekit-sandbox.apple.com"));
    }

    [Fact]
    public async Task What_the_signed_transaction_says_is_held_against_the_payment_before_apple_is_asked()
    {
        var network = new ScriptedNetwork();
        string beasToken = StoreAccount.AppStoreAccountTokenOf(Bea.Value);

        Assert.Equal(InvalidReason.WrongApp, await RefusedAsync(network, Pouch, _apple.Transaction(TransactionId, StoreIdOf(Pouch), bundleId: "com.example.other")));
        Assert.Equal(InvalidReason.ProductMismatch, await RefusedAsync(network, Pouch, _apple.Transaction(TransactionId, StoreIdOf(Bag))));
        Assert.Equal(InvalidReason.NotFromTheStore, await RefusedAsync(network, Pouch, _apple.Transaction("2000000999999999", StoreIdOf(Pouch))));
        Assert.Equal(InvalidReason.NotFromTheStore, await RefusedAsync(network, Pouch, _apple.Transaction(TransactionId, StoreIdOf(Pouch), environment: "Xcode")));
        Assert.Equal(InvalidReason.Refunded, await RefusedAsync(network, Pouch, _apple.Transaction(TransactionId, StoreIdOf(Pouch), revokedAt: Day)));
        Assert.Equal(InvalidReason.AnotherPlayers, await RefusedAsync(network, Pouch, _apple.Transaction(TransactionId, StoreIdOf(Pouch), appAccountToken: beasToken)));
        Assert.Equal(InvalidReason.NotFromTheStore, await RefusedAsync(network, Pouch, "eyJhbGciOiJub25lIn0.e30."));
        Assert.Empty(network.Seen);
    }

    [Fact]
    public async Task The_starter_pack_bought_from_another_game_account_comes_back()
    {
        string signed = _apple.Transaction(TransactionId, StoreIdOf(StarterPack), appAccountToken: StoreAccount.AppStoreAccountTokenOf(Bea.Value));
        ScriptedNetwork network = new ScriptedNetwork().On(TestApple.TransactionUrl(AppleEnvironment.Production, TransactionId), HttpStatusCode.OK, TestApple.TransactionInfo(signed));

        Assert.IsType<StoreCheck.Confirmed>(await CheckAsync(network, StarterPack, signed));
    }

    [Fact]
    public async Task What_apples_server_says_now_decides()
    {
        string signed = _apple.Transaction(TransactionId, StoreIdOf(Pouch));
        string url = TestApple.TransactionUrl(AppleEnvironment.Production, TransactionId);

        Assert.Equal(InvalidReason.NotFromTheStore, await RefusedAsync(new ScriptedNetwork().On(url, HttpStatusCode.NotFound, "{\"errorCode\":4040010}"), Pouch, signed));
        Assert.Equal(InvalidReason.Refunded, await RefusedAsync(new ScriptedNetwork().On(url, HttpStatusCode.OK, TestApple.TransactionInfo(_apple.Transaction(TransactionId, StoreIdOf(Pouch), revokedAt: Day))), Pouch, signed));
        Assert.Equal(UnavailableReason.StoreUnreachable, Assert.IsType<StoreCheck.Unreachable>(await CheckAsync(new ScriptedNetwork().On(url, HttpStatusCode.TooManyRequests, "{\"errorCode\":4290000}"), Pouch, signed)).Reason);
        Assert.Equal(UnavailableReason.CredentialsRefused, Assert.IsType<StoreCheck.Unreachable>(await CheckAsync(new ScriptedNetwork().On(url, HttpStatusCode.Unauthorized, string.Empty), Pouch, signed)).Reason);
        Assert.Equal(UnavailableReason.StoreAnswerUnreadable, Assert.IsType<StoreCheck.Unreachable>(await CheckAsync(new ScriptedNetwork().On(url, HttpStatusCode.OK, "{\"signedTransactionInfo\":\"forged\"}"), Pouch, signed)).Reason);
    }

    [Fact]
    public async Task No_signed_transaction_and_no_key_leave_the_payment_undecided()
    {
        string signed = _apple.Transaction(TransactionId, StoreIdOf(Pouch));
        AppStoreVerifier verifier = _apple.Verifier(new ScriptedNetwork(), _clock);

        Assert.Equal(UnavailableReason.NoSignedTransaction, Assert.IsType<StoreCheck.Unreachable>(await CheckAsync(new ScriptedNetwork(), Pouch, string.Empty)).Reason);
        StoreCheck keyless = await verifier.CheckAsync(new PaymentClaim(PurchaseStore.AppleAppStore, Pouch, TransactionId, signed, false), Product(Pouch), Ada, new FakeSecrets(), CancellationToken.None);
        Assert.Equal(UnavailableReason.CredentialsMissing, Assert.IsType<StoreCheck.Unreachable>(keyless).Reason);
    }

    [Fact]
    public void The_jwt_for_apples_server_carries_what_apple_asks_and_is_signed_by_the_in_app_purchase_key()
    {
        string token = AppStoreTokens.TokenOf(_apple.IapKey, TestApple.KeyId, TestApple.IssuerId, Day);
        string[] parts = token.Split('.');
        using JsonDocument header = JsonDocument.Parse(OutsideCalls.FromBase64Url(parts[0]));
        using JsonDocument claims = JsonDocument.Parse(OutsideCalls.FromBase64Url(parts[1]));

        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal(TestApple.KeyId, header.RootElement.GetProperty("kid").GetString());
        Assert.Equal(TestApple.IssuerId, claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal("appstoreconnect-v1", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal(App, claims.RootElement.GetProperty("bid").GetString());
        long issued = claims.RootElement.GetProperty("iat").GetInt64();
        Assert.InRange(claims.RootElement.GetProperty("exp").GetInt64() - issued, 1, 3600);
        Assert.True(_apple.IapKey.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), OutsideCalls.FromBase64Url(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public async Task The_refund_history_is_read_through_every_page_with_apples_signature_on_each()
    {
        string url = TestApple.RefundsUrl(AppleEnvironment.Production, TransactionId);
        ScriptedNetwork network = new ScriptedNetwork().On(url, request => request.Url.Contains("revision=rev-1", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, TestApple.RefundPage(new[] { _apple.Transaction("2000000900000002", StoreIdOf(Bag), revokedAt: Day) }, hasMore: false, revision: "rev-2"))
            : (HttpStatusCode.OK, TestApple.RefundPage(new[] { _apple.Transaction("2000000900000001", StoreIdOf(Pouch), revokedAt: Day) }, hasMore: true, revision: "rev-1")));

        var read = Assert.IsType<RefundHistory.Read>(await _apple.Verifier(network, _clock).RefundsAsync(TransactionId, AppleEnvironment.Production, _apple.Secrets(), CancellationToken.None));

        Assert.Equal(new[] { "2000000900000001", "2000000900000002" }, read.RefundedTransactions.OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(2, network.Seen.Count);
        Assert.All(network.Seen, request => Assert.StartsWith("Bearer ", request.Authorization, StringComparison.Ordinal));
    }

    private Task<StoreCheck> CheckAsync(ScriptedNetwork network, string product, string signed) =>
        _apple.Verifier(network, _clock).CheckAsync(new PaymentClaim(PurchaseStore.AppleAppStore, product, TransactionId, signed, deliveredUnverified: false), Product(product), Ada, _apple.Secrets(), CancellationToken.None);

    private async Task<InvalidReason> RefusedAsync(ScriptedNetwork network, string product, string signed) =>
        Assert.IsType<StoreCheck.Rejected>(await CheckAsync(network, product, signed)).Reason;
}
