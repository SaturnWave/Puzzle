using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MuseumPuzzle.Server.AppStore;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;
using static MuseumPuzzle.Server.Tests.TestWorld;

namespace MuseumPuzzle.Server.Tests;

/// <summary>Refunds, voided purchases, deletion and the 36-month traces (docs/research/server-v1.md §4.6, §5.6).</summary>
public sealed class ChoreTests : IDisposable
{
    private readonly FakeServerStore _cloud = new();
    private readonly HandClock _clock = new(Day);
    private readonly TestApple _apple = TestApple.Create();
    private readonly TestGoogle _google = TestGoogle.Create();

    public void Dispose()
    {
        _apple.Dispose();
        _google.Dispose();
    }

    [Fact]
    public async Task Apple_is_asked_for_refunds_once_a_day_and_a_refunded_payment_is_marked_not_taken_back()
    {
        GiveApple(Ada, ("2000000900000001", Pouch, false), ("2000000900000002", Bag, false));
        ScriptedNetwork network = new ScriptedNetwork().On(
            TestApple.RefundsUrl(AppleEnvironment.Production, "2000000900000002"),
            HttpStatusCode.OK,
            TestApple.RefundPage(new[] { _apple.Transaction("2000000900000001", StoreIdOf(Pouch), revokedAt: Day.AddDays(-1)) }));
        AppleRefundCheck check = new(_apple.Verifier(network, _clock), _clock, NullLogger.Instance);

        RefundOutcome first = await check.CheckAsync(Ada, _cloud, _apple.Secrets(), CancellationToken.None);
        _clock.Now += TimeSpan.FromHours(19);
        RefundOutcome soon = await check.CheckAsync(Ada, _cloud, _apple.Secrets(), CancellationToken.None);
        _clock.Now += TimeSpan.FromHours(2);
        RefundOutcome nextDay = await check.CheckAsync(Ada, _cloud, _apple.Secrets(), CancellationToken.None);

        Assert.Equal(new[] { LedgerNumber.First }, Assert.IsType<RefundOutcome.Checked>(first).Refunded);
        Assert.IsType<RefundOutcome.NotDue>(soon);
        Assert.Empty(Assert.IsType<RefundOutcome.Checked>(nextDay).Refunded);
        Assert.Equal(2, network.Seen.Count);
        Ledger ledger = _cloud.LedgerOf(Ada);
        Assert.Equal(new[] { LedgerState.Refunded, LedgerState.Valid }, ledger.Entries.Select(entry => entry.State));
        Assert.Equal(LedgerState.Refunded, _cloud.RecordOf(ledger.Entries[0].Receipt)!.State);
    }

    [Fact]
    public async Task A_player_with_no_standing_app_store_payment_is_not_asked_about()
    {
        GivePaymentTo(Ada, PurchaseStore.GooglePlay, "token-1", Pouch);
        AppleRefundCheck check = new(_apple.Verifier(new ScriptedNetwork(), _clock), _clock, NullLogger.Instance);

        Assert.IsType<RefundOutcome.NothingToCheck>(await check.CheckAsync(Ada, _cloud, _apple.Secrets(), CancellationToken.None));
        Assert.IsType<RefundOutcome.NothingToCheck>(await check.CheckAsync(Bea, _cloud, _apple.Secrets(), CancellationToken.None));
    }

    [Fact]
    public async Task Sandbox_payments_are_asked_of_the_sandbox_and_a_failure_leaves_the_day_unchecked()
    {
        GiveApple(Ada, ("2000000900000001", Pouch, true));
        var network = new ScriptedNetwork().On(TestApple.RefundsUrl(AppleEnvironment.Sandbox, "2000000900000001"), HttpStatusCode.InternalServerError, "{\"errorCode\":5000000}");
        AppleRefundCheck check = new(_apple.Verifier(network, _clock), _clock, NullLogger.Instance);

        RefundOutcome outcome = await check.CheckAsync(Ada, _cloud, _apple.Secrets(), CancellationToken.None);

        Assert.Equal(UnavailableReason.StoreUnreachable, Assert.IsType<RefundOutcome.Unavailable>(outcome).Reason);
        Assert.Null(_cloud.LedgerOf(Ada).RefundsCheckedAt);
    }

    [Fact]
    public async Task A_voided_receipt_is_marked_in_the_ledger_of_everyone_who_holds_it_then_in_the_registry()
    {
        GivePaymentTo(Ada, PurchaseStore.GooglePlay, "starter", StarterPack);
        GivePaymentTo(Bea, PurchaseStore.GooglePlay, "starter", StarterPack);
        GivePaymentTo(Bea, PurchaseStore.GooglePlay, "token-2", Pouch);
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.VoidedUrl, HttpStatusCode.OK, TestGoogle.Voided(new[] { ("starter", Day.AddHours(-5)), ("never-verified", Day.AddHours(-4)) }));

        ChoreOutcome outcome = await new MuseumPuzzle.Server.GooglePlay.GoogleVoidedPoll(TestGoogle.Verifier(network, _clock), _clock, NullLogger.Instance).RunAsync(_cloud, _google.Secrets(), CancellationToken.None);

        var done = Assert.IsType<ChoreOutcome.Done>(outcome);
        Assert.Equal((2, 1), (done.Looked, done.Changed));
        Assert.Equal(LedgerState.Voided, _cloud.LedgerOf(Ada).Entries.Single().State);
        Assert.Equal(new[] { LedgerState.Voided, LedgerState.Valid }, _cloud.LedgerOf(Bea).Entries.Select(entry => entry.State));
        Assert.Equal(LedgerState.Voided, _cloud.RecordOf(ReceiptId.Of(PurchaseStore.GooglePlay, "starter"))!.State);
        Assert.Equal(Day, _cloud.VoidedSeenUntil);
    }

    [Fact]
    public async Task The_look_at_voided_purchases_starts_an_hour_before_the_last_one_ended_but_never_beyond_googles_30_days()
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.VoidedUrl, HttpStatusCode.OK, TestGoogle.Voided(Array.Empty<(string, DateTimeOffset)>()));
        var poll = new MuseumPuzzle.Server.GooglePlay.GoogleVoidedPoll(TestGoogle.Verifier(network, _clock), _clock, NullLogger.Instance);

        _cloud.VoidedSeenUntil = Day.AddDays(-1);
        await poll.RunAsync(_cloud, _google.Secrets(), CancellationToken.None);
        _clock.Now = Day.AddDays(60);
        await poll.RunAsync(_cloud, _google.Secrets(), CancellationToken.None);

        string[] asked = network.Seen.Where(request => request.Url.StartsWith(TestGoogle.VoidedUrl, StringComparison.Ordinal)).Select(request => request.Url).ToArray();
        Assert.Contains($"startTime={Day.AddDays(-1).AddHours(-1).ToUnixTimeMilliseconds()}", asked[0], StringComparison.Ordinal);
        Assert.Contains($"startTime={(Day.AddDays(60) - MuseumPuzzle.Server.GooglePlay.GoogleVoidedPoll.Reach).ToUnixTimeMilliseconds()}", asked[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_look_stopped_at_its_time_budget_does_not_move_its_mark_and_googles_failure_is_unavailable()
    {
        GivePaymentTo(Ada, PurchaseStore.GooglePlay, "token-1", Pouch);
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.VoidedUrl, HttpStatusCode.OK, TestGoogle.Voided(new[] { ("token-1", Day), ("token-x", Day) }));
        _clock.Step = TimeSpan.FromSeconds(6);

        ChoreOutcome stopped = await new MuseumPuzzle.Server.GooglePlay.GoogleVoidedPoll(TestGoogle.Verifier(network, _clock), _clock, NullLogger.Instance).RunAsync(_cloud, _google.Secrets(), CancellationToken.None);
        ChoreOutcome failed = await new MuseumPuzzle.Server.GooglePlay.GoogleVoidedPoll(TestGoogle.Verifier(network, _clock), _clock, NullLogger.Instance).RunAsync(_cloud, new FakeSecrets(), CancellationToken.None);

        Assert.IsType<ChoreOutcome.Unfinished>(stopped);
        Assert.Null(_cloud.VoidedSeenUntil);
        Assert.Equal(UnavailableReason.CredentialsMissing, Assert.IsType<ChoreOutcome.Unavailable>(failed).Reason);
    }

    [Fact]
    public async Task Deleting_a_players_data_deletes_it_all_and_keeps_only_a_trace_without_them()
    {
        GivePaymentTo(Ada, PurchaseStore.GooglePlay, "token-1", Pouch);
        GivePaymentTo(Ada, PurchaseStore.GooglePlay, "starter", StarterPack);
        GivePaymentTo(Bea, PurchaseStore.GooglePlay, "starter", StarterPack);
        _cloud.Put($"save:{Ada}", "{\"envelope\":1}");
        var deletion = new DataDeletion(_clock, NullLogger.Instance);

        DeletionOutcome outcome = await deletion.DeleteAsync(Ada, _cloud, CancellationToken.None);
        DeletionOutcome again = await deletion.DeleteAsync(Ada, _cloud, CancellationToken.None);

        Assert.IsType<DeletionOutcome.Deleted>(outcome);
        Assert.IsType<DeletionOutcome.Deleted>(again);
        Assert.False(_cloud.HasPlayerData(Ada));
        ReceiptRecord pouch = _cloud.RecordOf(ReceiptId.Of(PurchaseStore.GooglePlay, "token-1"))!;
        Assert.Empty(pouch.Claims);
        Assert.Equal(DateOnly.FromDateTime(Day.UtcDateTime), pouch.ForgottenOn);
        Assert.Equal((Pouch, LedgerState.Valid, Day), (pouch.Product, pouch.State, pouch.At));
        ReceiptRecord starter = _cloud.RecordOf(ReceiptId.Of(PurchaseStore.GooglePlay, "starter"))!;
        Assert.Equal(new[] { Bea }, starter.Claims.Select(claim => claim.Player));
        Assert.Null(starter.ForgottenOn);
        Assert.Equal(new[] { ReceiptId.Of(PurchaseStore.GooglePlay, "token-1").Value }, _cloud.TracesNotedOn(DateOnly.FromDateTime(Day.UtcDateTime)));
    }

    [Fact]
    public async Task A_deletion_cloud_save_cannot_finish_says_so_and_a_ledger_it_cannot_read_does_not_stop_it()
    {
        _cloud.IsUnreachable = true;
        Assert.Equal(UnavailableReason.CloudSaveUnreachable, Assert.IsType<DeletionOutcome.Unavailable>(await new DataDeletion(_clock, NullLogger.Instance).DeleteAsync(Ada, _cloud, CancellationToken.None)).Reason);

        _cloud.IsUnreachable = false;
        _cloud.Put(FakeServerStore.LedgerItem(Ada), "{\"broken\":");
        Assert.IsType<DeletionOutcome.Deleted>(await new DataDeletion(_clock, NullLogger.Instance).DeleteAsync(Ada, _cloud, CancellationToken.None));
        Assert.False(_cloud.HasPlayerData(Ada));
    }

    [Fact]
    public async Task A_trace_goes_36_months_after_its_last_player_left_unless_someone_holds_the_receipt_again()
    {
        var day = new DateOnly(2026, 10, 26);
        ReceiptId old = ReceiptId.Of(PurchaseStore.GooglePlay, "old");
        ReceiptId reclaimed = ReceiptId.Of(PurchaseStore.GooglePlay, "reclaimed");
        ReceiptId young = ReceiptId.Of(PurchaseStore.GooglePlay, "young");
        _cloud.PutRecord(old, new ReceiptRecord(PurchaseStore.GooglePlay, Pouch, Day, LedgerState.Valid, false, Array.Empty<ReceiptClaim>(), day));
        _cloud.PutRecord(reclaimed, new ReceiptRecord(PurchaseStore.GooglePlay, StarterPack, Day, LedgerState.Valid, false, new[] { new ReceiptClaim(Bea, LedgerNumber.First) }, null));
        _cloud.PutRecord(young, new ReceiptRecord(PurchaseStore.GooglePlay, Pouch, Day, LedgerState.Valid, false, Array.Empty<ReceiptClaim>(), day.AddDays(1)));
        await _cloud.NoteTraceAsync(day, old, CancellationToken.None);
        await _cloud.NoteTraceAsync(day, reclaimed, CancellationToken.None);
        await _cloud.NoteTraceAsync(day.AddDays(1), young, CancellationToken.None);

        _clock.Now = new DateTimeOffset(2029, 10, 25, 4, 0, 0, TimeSpan.Zero);
        ChoreOutcome early = await new TraceRetention(_clock, NullLogger.Instance).RunAsync(_cloud, CancellationToken.None);
        _clock.Now = new DateTimeOffset(2029, 10, 26, 4, 0, 0, TimeSpan.Zero);
        ChoreOutcome due = await new TraceRetention(_clock, NullLogger.Instance).RunAsync(_cloud, CancellationToken.None);

        Assert.Equal(0, Assert.IsType<ChoreOutcome.Done>(early).Changed);
        Assert.Equal(1, Assert.IsType<ChoreOutcome.Done>(due).Changed);
        Assert.Null(_cloud.RecordOf(old));
        Assert.NotNull(_cloud.RecordOf(reclaimed));
        Assert.NotNull(_cloud.RecordOf(young));
        Assert.Empty(_cloud.TracesNotedOn(day));
        Assert.Single(_cloud.TracesNotedOn(day.AddDays(1)));
        Assert.Equal(new DateOnly(2026, 10, 26), TraceRetention.LastDueOn(new DateOnly(2029, 10, 26)));
    }

    [Fact]
    public async Task A_day_the_retention_job_did_not_run_is_caught_up_within_a_month()
    {
        var day = new DateOnly(2026, 10, 1);
        ReceiptId old = ReceiptId.Of(PurchaseStore.GooglePlay, "old");
        _cloud.PutRecord(old, new ReceiptRecord(PurchaseStore.GooglePlay, Pouch, Day, LedgerState.Valid, false, Array.Empty<ReceiptClaim>(), day));
        await _cloud.NoteTraceAsync(day, old, CancellationToken.None);
        _clock.Now = new DateTimeOffset(2029, 10, 20, 4, 0, 0, TimeSpan.Zero);

        await new TraceRetention(_clock, NullLogger.Instance).RunAsync(_cloud, CancellationToken.None);

        Assert.Null(_cloud.RecordOf(old));
    }

    /// <summary>Gives the player a verified payment as the validation would: an entry under the next number, and the player's claim in the registry.</summary>
    private void GivePaymentTo(PlayerId player, PurchaseStore store, string token, string product, string order = "GPA.1", bool isTest = false)
    {
        ReceiptId receipt = ReceiptId.Of(store, token);
        Ledger ledger = _cloud.LedgerOf(player).WithPayment(store, product, order, Day, LedgerState.Valid, isTest, receipt);
        _cloud.PutLedger(player, ledger);
        ReceiptRecord record = _cloud.RecordOf(receipt) ?? new ReceiptRecord(store, product, Day, LedgerState.Valid, isTest, Array.Empty<ReceiptClaim>(), null);
        _cloud.PutRecord(receipt, record.WithClaim(new ReceiptClaim(player, ledger.Entries[ledger.Entries.Count - 1].Number)));
    }

    private void GiveApple(PlayerId player, params (string TransactionId, string Product, bool IsTest)[] payments)
    {
        foreach ((string transactionId, string product, bool isTest) in payments)
        {
            GivePaymentTo(player, PurchaseStore.AppleAppStore, transactionId, product, transactionId, isTest);
        }
    }
}
