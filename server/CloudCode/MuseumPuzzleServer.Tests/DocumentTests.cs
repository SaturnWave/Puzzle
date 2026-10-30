using System.Text.Json.Nodes;
using MuseumPuzzle.Server.Catalogue;
using MuseumPuzzle.Server.Purchases;
using Puzzle.Client.Purchases;
using StoreProductKind = Puzzle.Meta.StoreProductKind;
using static MuseumPuzzle.Server.Tests.TestWorld;

namespace MuseumPuzzle.Server.Tests;

/// <summary>The names the server gives players and receipts to the stores and in Cloud Save, and the documents it keeps there.</summary>
public sealed class DocumentTests
{
    [Fact]
    public void A_name_based_uuid_is_rfc_4122s_version_5()
    {
        // RFC 4122's own namespace for domain names and the example every implementation is checked with.
        Assert.Equal("2ed6657d-e927-568b-95e1-2665a8aea6a2", StoreAccount.NameBasedUuid("6ba7b810-9dad-11d1-80b4-00c04fd430c8", "www.example.com"));
    }

    [Fact]
    public void The_store_accounts_of_a_player_never_change()
    {
        // Pinned, with the values Python's hashlib and uuid give: a change of either rule makes every earlier payment
        // another player's.
        Assert.Matches("^[0-9a-f]{64}$", StoreAccount.GooglePlayAccountOf(Ada.Value));
        Assert.Equal(PinnedGoogleAccountOfAda, StoreAccount.GooglePlayAccountOf(Ada.Value));
        Assert.Equal(PinnedAppStoreTokenOfAda, StoreAccount.AppStoreAccountTokenOf(Ada.Value));
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-5[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", StoreAccount.AppStoreAccountTokenOf(Ada.Value));
        Assert.NotEqual(StoreAccount.AppStoreAccountTokenOf(Ada.Value), StoreAccount.AppStoreAccountTokenOf(Bea.Value));
        Assert.Throws<ArgumentException>(() => StoreAccount.GooglePlayAccountOf(string.Empty));
    }

    [Fact]
    public void A_receipt_is_named_by_a_digest_of_its_store_and_token_and_never_by_the_token()
    {
        ReceiptId receipt = ReceiptId.Of(PurchaseStore.GooglePlay, "opaque-token-of-google");

        Assert.Matches("^rcpt-[0-9a-f]{40}$", receipt.Value);
        Assert.DoesNotContain("opaque", receipt.Value, StringComparison.Ordinal);
        Assert.Equal(receipt, ReceiptId.Of(PurchaseStore.GooglePlay, "opaque-token-of-google"));
        Assert.NotEqual(receipt, ReceiptId.Of(PurchaseStore.AppleAppStore, "opaque-token-of-google"));
        Assert.Equal(receipt, ReceiptId.Parse(receipt.Value));
        Assert.Null(ReceiptId.Parse("rcpt-ABC"));
        Assert.Null(ReceiptId.Parse("receipt-0123456789012345678901234567890123456789"));
    }

    [Fact]
    public void The_module_writes_the_ledger_the_game_reads()
    {
        // tests/fixtures/cloud-code/ledger.json is the game's side of this contract: tests/Puzzle.Core.Tests reads it
        // with the game's own reader (Purchases/PurchaseLedger).
        Ledger ledger = Ledger.Empty
            .WithPayment(PurchaseStore.GooglePlay, Pouch, "GPA.3312-4405-6102-77801", Day, LedgerState.Valid, isTest: false, ReceiptId.Of(PurchaseStore.GooglePlay, "token-1"))
            .WithPayment(PurchaseStore.AppleAppStore, StarterPack, "2000000912345678", Day.AddMinutes(5), LedgerState.Valid, isTest: true, ReceiptId.Of(PurchaseStore.AppleAppStore, "2000000912345678"))
            .WithPayment(PurchaseStore.GooglePlay, Bag, string.Empty, Day.AddMinutes(9), LedgerState.Valid, isTest: true, ReceiptId.Of(PurchaseStore.GooglePlay, "token-3"));
        ledger = ledger.WithStates(new Dictionary<LedgerNumber, LedgerState> { [new LedgerNumber(2)] = LedgerState.Refunded, [new LedgerNumber(3)] = LedgerState.Voided })
            .WithRefundsCheckedAt(Day.AddHours(1));

        string written = ServerDocuments.TextOf(ledger);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(RepoFiles.Read("tests", "fixtures", "cloud-code", "ledger.json")), JsonNode.Parse(written)), written);
        Assert.Equal(written, ServerDocuments.TextOf(ServerDocuments.LedgerFrom(written)));
    }

    [Fact]
    public void A_receipt_record_reads_back_as_it_was_written()
    {
        var record = new ReceiptRecord(PurchaseStore.GooglePlay, Pouch, Day, LedgerState.Valid, isTest: false, new[] { new ReceiptClaim(Ada, new LedgerNumber(4)) }, forgottenOn: null);

        string text = ServerDocuments.TextOf(record);
        ReceiptRecord read = ServerDocuments.ReceiptFrom(text);

        Assert.Equal(text, ServerDocuments.TextOf(read));
        Assert.Equal(new LedgerNumber(4), read.ClaimOf(Ada)!.Number);
        ReceiptRecord trace = read.WithoutPlayer(Ada, new DateOnly(2026, 12, 1));
        Assert.Empty(trace.Claims);
        Assert.Equal(new DateOnly(2026, 12, 1), trace.ForgottenOn);
        Assert.DoesNotContain(Ada.Value, ServerDocuments.TextOf(trace), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"next\":2,\"items\":[{\"seq\":1,\"store\":\"Steam\",\"product\":\"coins.pouch\",\"order\":\"\",\"at\":\"2026-11-01T10:02:11Z\",\"state\":\"valid\",\"test\":false,\"receipt\":\"rcpt-0123456789abcdef0123456789abcdef01234567\"}]}")]
    [InlineData("{\"next\":1,\"items\":[{\"seq\":1,\"store\":\"GooglePlay\",\"product\":\"coins.pouch\",\"order\":\"\",\"at\":\"2026-11-01T10:02:11Z\",\"state\":\"valid\",\"test\":false,\"receipt\":\"rcpt-0123456789abcdef0123456789abcdef01234567\"}]}")]
    [InlineData("{\"next\":2,\"items\":[{\"seq\":1,\"store\":\"GooglePlay\",\"product\":\"coins.pouch\",\"order\":\"\",\"at\":\"yesterday\",\"state\":\"valid\",\"test\":false,\"receipt\":\"rcpt-0123456789abcdef0123456789abcdef01234567\"}]}")]
    [InlineData("{\"next\":2,\"items\":[{\"seq\":1,\"store\":\"GooglePlay\",\"product\":\"coins.pouch\",\"order\":\"\",\"at\":\"2026-11-01T10:02:11Z\",\"state\":\"lost\",\"test\":false,\"receipt\":\"rcpt-0123456789abcdef0123456789abcdef01234567\"}]}")]
    [InlineData("{\"next\":2,\"items\":[{\"seq\":1,\"store\":\"GooglePlay\",\"product\":\"coins.pouch\",\"order\":\"\",\"at\":\"2026-11-01T10:02:11Z\",\"state\":\"valid\",\"test\":false}]}")]
    public void A_ledger_the_module_does_not_write_is_refused_not_guessed(string text)
    {
        Assert.Throws<ServerDocumentException>(() => ServerDocuments.LedgerFrom(text));
    }

    [Fact]
    public void The_module_knows_what_the_game_sells_by_the_games_own_settings()
    {
        ProductCatalogue catalogue = ProductCatalogue.Shipped();

        Assert.Equal(
            new[] { "com.saturnwave.museumpuzzle.coins.pouch", "com.saturnwave.museumpuzzle.coins.bag", "com.saturnwave.museumpuzzle.coins.chest", "com.saturnwave.museumpuzzle.coins.treasure", "com.saturnwave.museumpuzzle.starter.apprentice_kit" },
            catalogue.Products.Select(product => product.StoreId));
        Assert.All(catalogue.Products.Where(product => product.Kind == StoreProductKind.CoinPack), product => Assert.True(product.IsGoodForOnePlayerOnly));
        Assert.False(catalogue.FindBySuffix(StarterPack)!.IsGoodForOnePlayerOnly);
        Assert.Null(catalogue.FindBySuffix("coins.mountain"));
    }

    private const string PinnedGoogleAccountOfAda = "bfa168365fcd3647243cf34982dfde80715439e88043046162a48ea88b7e5be0";

    private const string PinnedAppStoreTokenOfAda = "96d6d3d1-7598-5588-a80b-a863f640b0b5";
}
