using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MuseumPuzzle.Server.GooglePlay;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Purchases;
using Puzzle.Client.Purchases;
using static MuseumPuzzle.Server.Tests.TestWorld;

namespace MuseumPuzzle.Server.Tests;

/// <summary>Google Play's side: the service account's token, the judgement of a payment, and the list of voided purchases, all against Google's layouts.</summary>
public sealed class GooglePlayTests : IDisposable
{
    private const string Token = "google-purchase-token-1";

    private readonly TestGoogle _google = TestGoogle.Create();
    private readonly HandClock _clock = new(Day);

    public void Dispose() => _google.Dispose();

    [Fact]
    public async Task A_purchase_google_took_the_money_for_is_confirmed_with_its_order()
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch), buyer: StoreAccount.GooglePlayAccountOf(Ada.Value)));

        StoreCheck check = await CheckAsync(network, Pouch);

        var confirmed = Assert.IsType<StoreCheck.Confirmed>(check);
        Assert.Equal("GPA.3312-4405-6102-77801", confirmed.Order);
        Assert.False(confirmed.IsTest);
        Assert.Equal($"Bearer {TestGoogle.AccessToken}", network.Seen.Last().Authorization);
    }

    [Fact]
    public async Task A_licence_testers_purchase_is_a_test_and_may_have_no_order()
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch), test: true, order: null));

        var confirmed = Assert.IsType<StoreCheck.Confirmed>(await CheckAsync(network, Pouch));

        Assert.True(confirmed.IsTest);
        Assert.Equal(string.Empty, confirmed.Order);
    }

    [Theory]
    [InlineData("PENDING", typeof(StoreCheck.Pending), null)]
    [InlineData("CANCELLED", typeof(StoreCheck.Rejected), InvalidReason.Cancelled)]
    [InlineData("PURCHASE_STATE_UNSPECIFIED", typeof(StoreCheck.Unreachable), null)]
    public async Task What_google_says_of_the_purchases_state_decides(string state, Type kind, InvalidReason? reason)
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch), state: state));

        StoreCheck check = await CheckAsync(network, Pouch);

        Assert.IsType(kind, check);
        if (reason != null)
        {
            Assert.Equal(reason, ((StoreCheck.Rejected)check).Reason);
        }
    }

    [Fact]
    public async Task A_receipt_for_another_product_is_refused()
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch)));

        Assert.Equal(InvalidReason.ProductMismatch, Assert.IsType<StoreCheck.Rejected>(await CheckAsync(network, "coins.treasure")).Reason);
    }

    [Fact]
    public async Task A_wholly_refunded_purchase_is_refused()
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch), refundable: 0));

        Assert.Equal(InvalidReason.Refunded, Assert.IsType<StoreCheck.Rejected>(await CheckAsync(network, Pouch)).Reason);
    }

    [Fact]
    public async Task A_pack_of_coins_bought_by_another_player_is_theirs_but_the_starter_pack_comes_back_to_every_account()
    {
        string beasAccount = StoreAccount.GooglePlayAccountOf(Bea.Value);
        ScriptedNetwork network = TestGoogle.Network()
            .On(TestGoogle.PurchaseUrl(Token), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch), buyer: beasAccount))
            .On(TestGoogle.PurchaseUrl("starter-token"), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(StarterPack), buyer: beasAccount));

        Assert.Equal(InvalidReason.AnotherPlayers, Assert.IsType<StoreCheck.Rejected>(await CheckAsync(network, Pouch)).Reason);
        Assert.IsType<StoreCheck.Confirmed>(await CheckAsync(network, StarterPack, "starter-token"));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task A_token_google_does_not_know_is_not_from_the_store(HttpStatusCode status)
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), status, "{\"error\":{\"code\":400,\"message\":\"Invalid Value\"}}");

        Assert.Equal(InvalidReason.NotFromTheStore, Assert.IsType<StoreCheck.Rejected>(await CheckAsync(network, Pouch)).Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, UnavailableReason.StoreUnreachable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, UnavailableReason.StoreUnreachable)]
    [InlineData(HttpStatusCode.Forbidden, UnavailableReason.CredentialsRefused)]
    [InlineData(HttpStatusCode.Unauthorized, UnavailableReason.CredentialsRefused)]
    public async Task A_google_that_cannot_answer_now_leaves_the_payment_undecided(HttpStatusCode status, UnavailableReason reason)
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), status, "{}");

        Assert.Equal(reason, Assert.IsType<StoreCheck.Unreachable>(await CheckAsync(network, Pouch)).Reason);
    }

    [Fact]
    public async Task A_kept_token_google_refuses_is_traded_for_a_fresh_one_once()
    {
        int asked = 0;
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), _ => ++asked == 1 ? (HttpStatusCode.Unauthorized, "{}") : (HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch))));

        Assert.IsType<StoreCheck.Confirmed>(await CheckAsync(network, Pouch));
        Assert.Equal(2, network.CountTo(TestGoogle.TokenUrl));
    }

    [Fact]
    public async Task No_network_and_no_key_leave_the_payment_undecided()
    {
        ScriptedNetwork unreachable = TestGoogle.Network();
        unreachable.Unreachable.Add(TestGoogle.PurchaseUrl(Token));
        GooglePlayVerifier verifier = TestGoogle.Verifier(TestGoogle.Network(), _clock);
        var claim = new PaymentClaim(PurchaseStore.GooglePlay, Pouch, Token, Token, deliveredUnverified: false);

        Assert.Equal(UnavailableReason.StoreUnreachable, Assert.IsType<StoreCheck.Unreachable>(await CheckAsync(unreachable, Pouch)).Reason);
        Assert.Equal(UnavailableReason.CredentialsMissing, Assert.IsType<StoreCheck.Unreachable>(await verifier.CheckAsync(claim, Product(Pouch), Ada, new FakeSecrets(), CancellationToken.None)).Reason);
        Assert.Equal(
            UnavailableReason.CredentialsMissing,
            Assert.IsType<StoreCheck.Unreachable>(await verifier.CheckAsync(claim, Product(Pouch), Ada, new FakeSecrets().With(SecretName.GooglePlayServiceAccountJson, "{\"type\":\"authorized_user\"}"), CancellationToken.None)).Reason);
    }

    [Fact]
    public async Task The_service_account_signs_its_assertion_for_the_androidpublisher_scope_and_the_token_is_kept()
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl(Token), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch)));
        GooglePlayVerifier verifier = TestGoogle.Verifier(network, _clock);
        var claim = new PaymentClaim(PurchaseStore.GooglePlay, Pouch, Token, Token, deliveredUnverified: false);

        await verifier.CheckAsync(claim, Product(Pouch), Ada, _google.Secrets(), CancellationToken.None);
        _clock.Now += TimeSpan.FromMinutes(50);
        await verifier.CheckAsync(claim, Product(Pouch), Ada, _google.Secrets(), CancellationToken.None);
        _clock.Now += TimeSpan.FromMinutes(10);
        await verifier.CheckAsync(claim, Product(Pouch), Ada, _google.Secrets(), CancellationToken.None);

        SeenRequest exchange = network.Seen.First(request => request.Url == TestGoogle.TokenUrl);
        string assertion = Uri.UnescapeDataString(exchange.Body.Split('&').Single(part => part.StartsWith("assertion=", StringComparison.Ordinal))["assertion=".Length..]);
        string[] parts = assertion.Split('.');
        Assert.True(_google.Key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), OutsideCalls.FromBase64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using JsonDocument claims = JsonDocument.Parse(OutsideCalls.FromBase64Url(parts[1]));
        Assert.Equal("play-orders@museum-puzzle-test.iam.gserviceaccount.com", claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal(GoogleAccessTokens.Scope, claims.RootElement.GetProperty("scope").GetString());
        Assert.Equal(TestGoogle.TokenUrl, claims.RootElement.GetProperty("aud").GetString());
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", exchange.Body, StringComparison.Ordinal);
        Assert.Equal(2, network.CountTo(TestGoogle.TokenUrl));
    }

    [Fact]
    public async Task The_list_of_voided_purchases_is_read_page_by_page()
    {
        DateTimeOffset voidedAt = Day.AddHours(-3);
        ScriptedNetwork network = TestGoogle.Network()
            .On(TestGoogle.VoidedUrl, request => request.Url.Contains("token=page-2", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, TestGoogle.Voided(new[] { ("second", voidedAt) }))
                : (HttpStatusCode.OK, TestGoogle.Voided(new[] { ("first", voidedAt) }, nextPage: "page-2")));
        GooglePlayVerifier verifier = TestGoogle.Verifier(network, _clock);

        var first = Assert.IsType<VoidedPage.Read>(await verifier.VoidedPageAsync(Day.AddDays(-1), Day, null, _google.Secrets(), CancellationToken.None));
        var second = Assert.IsType<VoidedPage.Read>(await verifier.VoidedPageAsync(Day.AddDays(-1), Day, first.NextPage, _google.Secrets(), CancellationToken.None));

        Assert.Equal("first", first.Purchases.Single().PurchaseToken);
        Assert.Equal(voidedAt, first.Purchases.Single().VoidedAt);
        Assert.Equal("page-2", first.NextPage);
        Assert.Equal("second", second.Purchases.Single().PurchaseToken);
        Assert.Null(second.NextPage);
        string firstUrl = network.Seen.First(request => request.Url.StartsWith(TestGoogle.VoidedUrl, StringComparison.Ordinal)).Url;
        Assert.Contains($"startTime={Day.AddDays(-1).ToUnixTimeMilliseconds()}&endTime={Day.ToUnixTimeMilliseconds()}&maxResults=1000", firstUrl, StringComparison.Ordinal);
    }

    private Task<StoreCheck> CheckAsync(ScriptedNetwork network, string product, string token = Token) =>
        TestGoogle.Verifier(network, _clock).CheckAsync(new PaymentClaim(PurchaseStore.GooglePlay, product, token, token, deliveredUnverified: false), Product(product), Ada, _google.Secrets(), CancellationToken.None);
}
