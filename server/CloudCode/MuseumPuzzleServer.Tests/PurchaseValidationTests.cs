using Microsoft.Extensions.Logging.Abstractions;
using MuseumPuzzle.Server.Catalogue;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;
using static MuseumPuzzle.Server.Tests.TestWorld;

namespace MuseumPuzzle.Server.Tests;

/// <summary>A store that answers what the test says, and counts how often it was asked.</summary>
public sealed class ScriptedVerifier : IStoreVerifier
{
    public ScriptedVerifier(PurchaseStore store)
    {
        Store = store;
    }

    public PurchaseStore Store { get; }

    public StoreCheck Answer { get; set; } = new StoreCheck.Confirmed("GPA.1111-2222-3333-44444", isTest: false);

    public int Asked { get; private set; }

    public Task<StoreCheck> CheckAsync(PaymentClaim claim, CatalogueProduct product, PlayerId player, ISecrets secrets, CancellationToken cancellation)
    {
        Asked++;
        return Task.FromResult(Answer);
    }
}

/// <summary>"Is this payment real?": the ledger, the registry and the store, in the order the plan gives (docs/research/server-v1.md §4.1).</summary>
public sealed class PurchaseValidationTests
{
    private readonly ScriptedVerifier _google = new(PurchaseStore.GooglePlay);
    private readonly ScriptedVerifier _apple = new(PurchaseStore.AppleAppStore);
    private readonly FakeServerStore _cloud = new();
    private readonly HandClock _clock = new(Day);

    [Fact]
    public async Task A_confirmed_payment_enters_the_ledger_under_the_next_number_and_the_registry_under_its_player()
    {
        PurchaseVerdict first = await ValidateAsync(Ada, Pouch, "token-1");
        PurchaseVerdict second = await ValidateAsync(Ada, Bag, "token-2");

        Assert.Equal(1, Assert.IsType<PurchaseVerdict.Valid>(first).Number.Value);
        Assert.Equal(2, Assert.IsType<PurchaseVerdict.Valid>(second).Number.Value);
        Ledger ledger = _cloud.LedgerOf(Ada);
        Assert.Equal(new[] { Pouch, Bag }, ledger.Entries.Select(entry => entry.Product));
        Assert.Equal(3, ledger.Next.Value);
        Assert.All(ledger.Entries, entry => Assert.Equal(LedgerState.Valid, entry.State));
        Assert.Equal("GPA.1111-2222-3333-44444", ledger.Entries[0].Order);
        ReceiptRecord record = _cloud.RecordOf(Receipt("token-1"))!;
        Assert.Equal(new LedgerNumber(1), record.ClaimOf(Ada)!.Number);
        Assert.Equal(Day, record.At);
    }

    [Fact]
    public async Task A_payment_asked_about_again_is_answered_from_the_ledger_without_asking_the_store()
    {
        await ValidateAsync(Ada, Pouch, "token-1");
        int writes = _cloud.Writes;

        PurchaseVerdict again = await ValidateAsync(Ada, Pouch, "token-1");

        Assert.Equal(1, Assert.IsType<PurchaseVerdict.AlreadyValidated>(again).Number.Value);
        Assert.Equal(1, _google.Asked);
        Assert.Equal(writes, _cloud.Writes);
    }

    [Theory]
    [InlineData("coins.mountain")]
    [InlineData("")]
    public async Task A_product_the_game_does_not_sell_is_never_valid(string product)
    {
        Assert.Equal(InvalidReason.UnknownProduct, Assert.IsType<PurchaseVerdict.Invalid>(await ValidateAsync(Ada, product, "token-1")).Reason);
        Assert.Equal(0, _google.Asked);
        Assert.Equal(0, _cloud.Writes);
    }

    [Fact]
    public async Task A_pending_or_undecided_payment_writes_nothing()
    {
        _google.Answer = new StoreCheck.Pending();
        Assert.IsType<PurchaseVerdict.Pending>(await ValidateAsync(Ada, Pouch, "token-1"));

        _google.Answer = new StoreCheck.Unreachable(UnavailableReason.StoreUnreachable);
        Assert.Equal(UnavailableReason.StoreUnreachable, Assert.IsType<PurchaseVerdict.Unavailable>(await ValidateAsync(Ada, Pouch, "token-1")).Reason);

        Assert.Equal(0, _cloud.Writes);
    }

    [Fact]
    public async Task A_cloud_save_out_of_reach_leaves_the_payment_undecided()
    {
        _cloud.IsUnreachable = true;

        Assert.Equal(UnavailableReason.CloudSaveUnreachable, Assert.IsType<PurchaseVerdict.Unavailable>(await ValidateAsync(Ada, Pouch, "token-1")).Reason);
        Assert.Equal(0, _google.Asked);
    }

    [Fact]
    public async Task A_pack_of_coins_another_player_was_given_is_theirs_and_the_store_is_not_asked()
    {
        await ValidateAsync(Ada, Pouch, "token-1");

        PurchaseVerdict verdict = await ValidateAsync(Bea, Pouch, "token-1");

        Assert.Equal(InvalidReason.AnotherPlayers, Assert.IsType<PurchaseVerdict.Invalid>(verdict).Reason);
        Assert.Equal(1, _google.Asked);
        Assert.Empty(_cloud.LedgerOf(Bea).Entries);
    }

    [Fact]
    public async Task Another_players_pack_the_game_had_delivered_unverified_enters_the_ledger_as_invalid_and_leaves_their_record()
    {
        await ValidateAsync(Ada, Pouch, "token-1");
        string adasRecord = _cloud.TextOf(Receipt("token-1").Value)!;

        PurchaseVerdict verdict = await ValidateAsync(Bea, Pouch, "token-1", deliveredUnverified: true);
        PurchaseVerdict again = await ValidateAsync(Bea, Pouch, "token-1", deliveredUnverified: true);

        Assert.Equal(InvalidReason.AnotherPlayers, Assert.IsType<PurchaseVerdict.Invalid>(verdict).Reason);
        Assert.Equal(InvalidReason.AlreadyRefused, Assert.IsType<PurchaseVerdict.Invalid>(again).Reason);
        LedgerEntry entry = Assert.Single(_cloud.LedgerOf(Bea).Entries);
        Assert.Equal(LedgerState.Invalid, entry.State);
        Assert.Equal(adasRecord, _cloud.TextOf(Receipt("token-1").Value));
    }

    [Fact]
    public async Task A_payment_the_store_refuses_after_the_game_delivered_it_is_kept_as_invalid_and_never_asked_again()
    {
        _google.Answer = new StoreCheck.Rejected(InvalidReason.NotFromTheStore);

        Assert.Equal(InvalidReason.NotFromTheStore, Assert.IsType<PurchaseVerdict.Invalid>(await ValidateAsync(Ada, Pouch, "forged", deliveredUnverified: true)).Reason);
        Assert.Equal(InvalidReason.AlreadyRefused, Assert.IsType<PurchaseVerdict.Invalid>(await ValidateAsync(Ada, Pouch, "forged")).Reason);

        Assert.Equal(1, _google.Asked);
        Assert.Equal(LedgerState.Invalid, Assert.Single(_cloud.LedgerOf(Ada).Entries).State);
        Assert.Null(_cloud.RecordOf(Receipt("forged")));
    }

    [Fact]
    public async Task A_refused_payment_the_game_did_not_deliver_leaves_no_trace()
    {
        _google.Answer = new StoreCheck.Rejected(InvalidReason.Cancelled);

        Assert.Equal(InvalidReason.Cancelled, Assert.IsType<PurchaseVerdict.Invalid>(await ValidateAsync(Ada, Pouch, "token-1")).Reason);
        Assert.Equal(0, _cloud.Writes);
    }

    [Fact]
    public async Task The_starter_pack_comes_back_on_a_second_game_account_of_the_same_store_account()
    {
        await ValidateAsync(Ada, StarterPack, "starter");

        PurchaseVerdict verdict = await ValidateAsync(Bea, StarterPack, "starter");

        Assert.Equal(1, Assert.IsType<PurchaseVerdict.Valid>(verdict).Number.Value);
        Assert.Equal(new[] { Ada, Bea }, _cloud.RecordOf(Receipt("starter"))!.Claims.Select(claim => claim.Player));
    }

    [Fact]
    public async Task A_pack_of_coins_kept_as_a_deleted_players_trace_is_not_given_again()
    {
        _cloud.PutRecord(Receipt("token-1"), new ReceiptRecord(PurchaseStore.GooglePlay, Pouch, Day, LedgerState.Valid, false, Array.Empty<ReceiptClaim>(), new DateOnly(2026, 11, 1)));

        Assert.Equal(InvalidReason.AnotherPlayers, Assert.IsType<PurchaseVerdict.Invalid>(await ValidateAsync(Bea, Pouch, "token-1")).Reason);
        Assert.Equal(0, _google.Asked);
    }

    [Fact]
    public async Task A_receipt_google_voided_is_never_given()
    {
        _cloud.PutRecord(Receipt("starter"), new ReceiptRecord(PurchaseStore.GooglePlay, StarterPack, Day, LedgerState.Voided, false, new[] { new ReceiptClaim(Ada, new LedgerNumber(1)) }, null));

        Assert.Equal(InvalidReason.Refunded, Assert.IsType<PurchaseVerdict.Invalid>(await ValidateAsync(Bea, StarterPack, "starter")).Reason);
    }

    [Fact]
    public async Task A_payment_the_ledger_holds_as_refunded_is_refused()
    {
        await ValidateAsync(Ada, Pouch, "token-1");
        Ledger ledger = _cloud.LedgerOf(Ada);
        _cloud.PutLedger(Ada, ledger.WithStates(new Dictionary<LedgerNumber, LedgerState> { [LedgerNumber.First] = LedgerState.Voided }));

        Assert.Equal(InvalidReason.Refunded, Assert.IsType<PurchaseVerdict.Invalid>(await ValidateAsync(Ada, Pouch, "token-1")).Reason);
    }

    [Fact]
    public async Task Two_calls_for_one_payment_give_one_entry()
    {
        // The other call enters the payment between this call's read of the ledger and its write.
        Ledger before = Ledger.Empty.WithPayment(PurchaseStore.GooglePlay, Bag, "GPA.9", Day, LedgerState.Valid, false, Receipt("token-0"));
        _cloud.PutLedger(Ada, before);
        _cloud.BeforeNextWrite[FakeServerStore.LedgerItem(Ada)] = store => store.PutLedger(
            Ada, before.WithPayment(PurchaseStore.GooglePlay, Pouch, "GPA.1111-2222-3333-44444", Day, LedgerState.Valid, false, Receipt("token-1")));

        PurchaseVerdict verdict = await ValidateAsync(Ada, Pouch, "token-1");

        Assert.Equal(2, Assert.IsType<PurchaseVerdict.AlreadyValidated>(verdict).Number.Value);
        Assert.Equal(2, _cloud.LedgerOf(Ada).Entries.Count);
        Assert.NotNull(_cloud.RecordOf(Receipt("token-1"))!.ClaimOf(Ada));
    }

    [Fact]
    public async Task A_payment_entered_meanwhile_by_another_call_moves_this_one_to_the_next_number()
    {
        Ledger before = Ledger.Empty.WithPayment(PurchaseStore.GooglePlay, Bag, "GPA.9", Day, LedgerState.Valid, false, Receipt("token-0"));
        _cloud.PutLedger(Ada, before);
        _cloud.BeforeNextWrite[FakeServerStore.LedgerItem(Ada)] = store => store.PutLedger(
            Ada, before.WithPayment(PurchaseStore.GooglePlay, Bag, "GPA.8", Day, LedgerState.Valid, false, Receipt("token-other")));

        PurchaseVerdict verdict = await ValidateAsync(Ada, Pouch, "token-1");

        Assert.Equal(3, Assert.IsType<PurchaseVerdict.Valid>(verdict).Number.Value);
        Assert.Equal(new[] { Bag, Bag, Pouch }, _cloud.LedgerOf(Ada).Entries.Select(entry => entry.Product));
    }

    [Fact]
    public async Task A_players_first_ledger_is_written_without_a_lock_and_one_payment_still_gives_one_entry()
    {
        // Cloud Save has no "write only if absent" (docs/research/server-v1.md §3.5, limit 3): the first ledger is
        // written over whatever another call wrote just before it. For one payment, both write the same entry.
        _cloud.BeforeNextWrite[FakeServerStore.LedgerItem(Ada)] = store => store.PutLedger(
            Ada, Ledger.Empty.WithPayment(PurchaseStore.GooglePlay, Pouch, "GPA.1111-2222-3333-44444", Day, LedgerState.Valid, false, Receipt("token-1")));

        PurchaseVerdict verdict = await ValidateAsync(Ada, Pouch, "token-1");

        Assert.Equal(1, Assert.IsType<PurchaseVerdict.Valid>(verdict).Number.Value);
        Assert.Single(_cloud.LedgerOf(Ada).Entries);
    }

    [Fact]
    public async Task A_registry_that_cannot_be_written_now_is_written_at_the_next_question()
    {
        _cloud.RefusedWrites.Add(Receipt("token-1").Value);
        Assert.IsType<PurchaseVerdict.Valid>(await ValidateAsync(Ada, Pouch, "token-1"));
        Assert.Null(_cloud.RecordOf(Receipt("token-1")));

        _cloud.RefusedWrites.Clear();
        Assert.IsType<PurchaseVerdict.AlreadyValidated>(await ValidateAsync(Ada, Pouch, "token-1"));

        Assert.Equal(LedgerNumber.First, _cloud.RecordOf(Receipt("token-1"))!.ClaimOf(Ada)!.Number);
    }

    [Fact]
    public async Task A_ledger_the_module_does_not_write_is_never_written_over()
    {
        _cloud.Put(FakeServerStore.LedgerItem(Ada), "{\"next\":\"many\"}");

        Assert.Equal(UnavailableReason.LedgerUnreadable, Assert.IsType<PurchaseVerdict.Unavailable>(await ValidateAsync(Ada, Pouch, "token-1")).Reason);
        Assert.Equal("{\"next\":\"many\"}", _cloud.TextOf(FakeServerStore.LedgerItem(Ada)));
        Assert.Equal(0, _google.Asked);
    }

    [Fact]
    public async Task An_app_store_payment_goes_to_apple_and_keeps_its_transaction_as_the_order()
    {
        _apple.Answer = new StoreCheck.Confirmed("2000000912345678", isTest: true);

        PurchaseVerdict verdict = await new PurchaseValidation(Shipped, new IStoreVerifier[] { _google, _apple }, _clock, NullLogger.Instance)
            .ValidateAsync(Ada, new PaymentClaim(PurchaseStore.AppleAppStore, Pouch, "2000000912345678", "jws", false), _cloud, new FakeSecrets(), CancellationToken.None);

        Assert.True(Assert.IsType<PurchaseVerdict.Valid>(verdict).IsTest);
        Assert.Equal(1, _apple.Asked);
        Assert.Equal(0, _google.Asked);
        LedgerEntry entry = Assert.Single(_cloud.LedgerOf(Ada).Entries);
        Assert.Equal(PurchaseStore.AppleAppStore, entry.Store);
        Assert.Equal("2000000912345678", entry.Order);
        Assert.Equal(ReceiptId.Of(PurchaseStore.AppleAppStore, "2000000912345678"), entry.Receipt);
    }

    private static ReceiptId Receipt(string token) => ReceiptId.Of(PurchaseStore.GooglePlay, token);

    private Task<PurchaseVerdict> ValidateAsync(PlayerId player, string product, string token, bool deliveredUnverified = false) =>
        new PurchaseValidation(Shipped, new IStoreVerifier[] { _google, _apple }, _clock, NullLogger.Instance)
            .ValidateAsync(player, new PaymentClaim(PurchaseStore.GooglePlay, product, token, token, deliveredUnverified), _cloud, new FakeSecrets(), CancellationToken.None);
}
