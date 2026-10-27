using System.IO;
using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using static Puzzle.Core.Tests.CloudSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// When the sync may use the cloud (docs/research/server-v1.md §2.4, §3.4, §3.8) and what the phone keeps of it
/// (cloud.json, §3.3): the budget's defaults and Remote Config's bounds, the gates, the backing off, and a state
/// file that reads back, starts with the switch on and never turns an "off" into an "on".
/// </summary>
public sealed class SyncPolicyTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cloud-state-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private CloudStateFile StateFile() => new(_folder, Json, Rules.StartingWallet, () => "fresh-writer");

    [Fact]
    public void By_default_the_game_syncs_and_verifies_uploads_at_most_hourly_and_reads_every_12_hours()
    {
        Assert.Equal((true, TimeSpan.FromMinutes(60), TimeSpan.FromHours(12), true),
            (SyncSettings.Default.Syncs, SyncSettings.Default.UploadEvery, SyncSettings.Default.ReadEvery, SyncSettings.Default.ValidatesPurchases));
        Assert.Equal(SyncSettings.Default.UploadEvery, SyncSettings.FromRemote(null, null, null, null).UploadEvery);
        Assert.Equal(
            new[] { "cloud_sync_enabled", "cloud_upload_min_minutes", "cloud_read_min_hours", "purchase_validation_enabled" },
            new[] { RemoteConfigKeys.CloudSyncEnabled, RemoteConfigKeys.UploadMinMinutes, RemoteConfigKeys.ReadMinHours, RemoteConfigKeys.PurchaseValidationEnabled });
    }

    [Theory]
    [InlineData(15L, 6L, 15, 6)]
    [InlineData(0L, 0L, 5, 1)] // a mistake towards nothing: the least
    [InlineData(-30L, -1L, 5, 1)]
    [InlineData(100_000L, 100_000L, 1440, 168)] // a mistake towards never: the most
    [InlineData(long.MaxValue, long.MaxValue, 1440, 168)]
    public void Remote_config_sets_the_budget_within_bounds(long uploadMinutes, long readHours, int minutes, int hours)
    {
        SyncSettings settings = SyncSettings.FromRemote(syncs: false, uploadMinutes, readHours, validatesPurchases: false);

        Assert.Equal((false, TimeSpan.FromMinutes(minutes), TimeSpan.FromHours(hours), false), (settings.Syncs, settings.UploadEvery, settings.ReadEvery, settings.ValidatesPurchases));
    }

    [Fact]
    public void The_services_start_only_with_the_switch_on_and_a_cloud_project()
    {
        Assert.True(SyncPolicy.MayStartServices(isSwitchedOn: true, hasACloudProject: true));
        Assert.False(SyncPolicy.MayStartServices(isSwitchedOn: false, hasACloudProject: true));
        Assert.False(SyncPolicy.MayStartServices(isSwitchedOn: true, hasACloudProject: false));
    }

    [Fact]
    public void The_gates_say_why_the_sync_does_not_run_the_players_switch_first()
    {
        SyncSettings off = SyncSettings.FromRemote(syncs: false, null, null, null);

        Assert.Null(SyncPolicy.Gate(true, SyncSettings.Default, ProgressKeeping.AfterClosing, testerMode: false));
        Assert.Equal(CloudOff.SwitchedOff, SyncPolicy.Gate(false, off, ProgressKeeping.ForThisRunOnly, testerMode: true));
        Assert.Equal(CloudOff.ThisRunOnly, SyncPolicy.Gate(true, off, ProgressKeeping.ForThisRunOnly, testerMode: true));
        Assert.Equal(CloudOff.TesterMode, SyncPolicy.Gate(true, off, ProgressKeeping.AfterClosing, testerMode: true));
        Assert.Equal(CloudOff.TurnedOffRemotely, SyncPolicy.Gate(true, off, ProgressKeeping.AfterClosing, testerMode: false));
    }

    [Theory]
    [InlineData(SyncTrigger.PurchaseDelivered, true)]
    [InlineData(SyncTrigger.PaintingOrRoomCompleted, true)]
    [InlineData(SyncTrigger.AccountChanged, true)]
    [InlineData(SyncTrigger.ColdStart, false)]
    [InlineData(SyncTrigger.Background, false)]
    [InlineData(SyncTrigger.LongSession, false)]
    public void A_payment_a_painting_and_a_sign_in_upload_outside_the_budget(SyncTrigger trigger, bool outside)
    {
        Assert.Equal(outside, trigger.IsOutsideTheBudget());
    }

    [Fact]
    public void Retries_back_off_except_when_no_network_was_there_to_reach_the_cloud()
    {
        Assert.Null(SyncPolicy.RetryAfter(CloudFailure.NoNetwork, failuresBefore: 3, Noon));
        TimeSpan[] steps = Enumerable.Range(0, 8).Select(before => SyncPolicy.RetryAfter(CloudFailure.Blocked, before, Noon)!.Value - Noon).ToArray();

        Assert.Equal(new[] { 1, 5, 30, 120, 360, 1440, 1440, 1440 }, steps.Select(step => (int)step.TotalMinutes));
    }

    [Fact]
    public void A_clock_set_back_before_the_last_read_or_write_does_not_stop_the_sync()
    {
        CloudState state = CloudState.Restored(true, "w", Player, CloudBase.NoSave(Rules.StartingWallet), Noon, Noon, null, 0, null, null);
        DateTimeOffset dayBefore = Noon.AddDays(-1);

        Assert.True(SyncPolicy.ReadsFirst(SyncTrigger.ColdStart, state, SyncSettings.Default, dayBefore));
        Assert.Null(SyncPolicy.UploadWaitsUntil(SyncTrigger.Background, state, SyncSettings.Default, dayBefore));
        Assert.False(SyncPolicy.ReadsFirst(SyncTrigger.Background, state, SyncSettings.Default, Noon.AddDays(3))); // only the start reads by the clock
    }

    [Fact]
    public void A_new_installation_has_the_switch_on_and_a_new_writer()
    {
        CloudStateLoad load = StateFile().Load();

        Assert.False(load.WasDamaged);
        Assert.True(load.State.IsSwitchedOn);
        Assert.Equal("fresh-writer", load.State.Writer);
        Assert.Null(load.State.Player);
    }

    [Fact]
    public void The_state_reads_back_with_its_base_its_unanswered_write_and_why_it_stopped()
    {
        PlayerProgress synced = NewPlayer().Won(12).Bought(Small, "GPA.1", number: 1);
        PlayerProgress sent = synced.Won(1);
        CloudState state = CloudState.Restored(
            false, "writer-1", Player, new CloudBase(synced, new CloudWriteLock("lock-7"), 41, new[] { "w-40", "w-41" }), Noon, Noon.AddMinutes(5),
            new PendingWrite("w-42", sent, 42, new[] { "w-40", "w-41", "w-42" }), 2, Noon.AddMinutes(30), new CloudHalt.LaterGame(9));

        StateFile().Save(state);
        CloudState read = StateFile().Load().State;

        Assert.Equal((false, "writer-1", (CloudPlayerId?)Player), (read.IsSwitchedOn, read.Writer, read.Player));
        Assert.Equal((synced, (CloudWriteLock?)new CloudWriteLock("lock-7"), 41L), (read.Base!.Progress, read.Base.WriteLock, read.Base.Revision));
        Assert.Equal(new[] { "w-40", "w-41" }, read.Base.Writes);
        Assert.Equal(("w-42", sent, 42L), (read.Pending!.Id, read.Pending.Progress, read.Pending.Revision));
        Assert.Equal(((DateTimeOffset?)Noon, (DateTimeOffset?)Noon.AddMinutes(5), 2, (DateTimeOffset?)Noon.AddMinutes(30)), (read.LastRead, read.LastWrite, read.Failures, read.RetryAt));
        Assert.Equal(9, Assert.IsType<CloudHalt.LaterGame>(read.Halt).ProgressLayout);
    }

    [Fact]
    public void A_cloud_with_no_save_reads_back_as_a_new_players_base()
    {
        StateFile().Save(CloudState.New("w").ForPlayer(Player).AfterRead(CloudBase.NoSave(Rules.StartingWallet), Noon));

        CloudBase read = StateFile().Load().State.Base!;

        Assert.False(read.HasSave);
        Assert.Equal(NewPlayer(), read.Progress);
    }

    /// <summary>A damaged cloud.json is set aside and the state starts over with the switch off: an "off" must never turn into an "on" by accident.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"version\":0,\"writer\":\"w\"}")]
    [InlineData("{\"version\":1,\"writer\":\"\",\"switchedOn\":false}")]
    public void A_damaged_state_is_set_aside_and_starts_over_with_the_switch_off(string text)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, CloudStateFile.FileName), text);

        CloudStateLoad load = StateFile().Load();

        Assert.True(load.WasDamaged);
        Assert.False(load.State.IsSwitchedOn);
        Assert.Equal(text, File.ReadAllText(Path.Combine(_folder, CloudStateFile.FileName + ".damaged")));
        Assert.False(File.Exists(Path.Combine(_folder, CloudStateFile.FileName)));
    }

    [Fact]
    public void A_base_whose_progress_does_not_read_is_dropped_and_the_next_sync_reads_first()
    {
        PlayerProgress synced = NewPlayer().Won(12);
        StateFile().Save(CloudState.New("w").ForPlayer(Player).AfterRead(new CloudBase(synced, new CloudWriteLock("lock-1"), 1, new[] { "w-1" }), Noon));
        string path = Path.Combine(_folder, CloudStateFile.FileName);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"levelsWon\":12", "\"levelsWon\":-12", StringComparison.Ordinal));

        CloudState read = StateFile().Load().State;

        Assert.Null(read.Base);
        Assert.True(read.IsSwitchedOn);
        Assert.True(SyncPolicy.ReadsFirst(SyncTrigger.Background, read, SyncSettings.Default, Noon));
    }

    [Fact]
    public void The_offer_to_link_is_kept_across_accounts_and_runs_and_a_file_from_before_it_reads_as_not_made()
    {
        StateFile().Save(CloudState.New("w").WithTheLinkOffered().ForPlayer(Player).ForPlayer(new CloudPlayerId("player-2")));
        bool keptAcrossAccounts = StateFile().Load().State.LinkOffered;
        string path = Path.Combine(_folder, CloudStateFile.FileName);
        File.WriteAllText(path, File.ReadAllText(path).Replace(",\"linkOffered\":true", string.Empty, StringComparison.Ordinal));

        CloudState before = StateFile().Load().State;

        Assert.True(keptAcrossAccounts);
        Assert.False(before.LinkOffered);
        Assert.False(CloudState.Forgotten("w").LinkOffered);
    }

    [Fact]
    public void The_ledger_is_read_only_when_it_can_have_changed()
    {
        var fresh = new LedgerCopy("{}", Noon, isStale: false);

        Assert.True(SyncPolicy.ReadsTheLedger(copy: null, namesAPaymentTheCopyLacks: false, copyHasPayments: false, Noon)); // never read for the account
        Assert.True(SyncPolicy.ReadsTheLedger(fresh.Stale(), namesAPaymentTheCopyLacks: false, copyHasPayments: false, Noon)); // a payment verified since
        Assert.True(SyncPolicy.ReadsTheLedger(fresh, namesAPaymentTheCopyLacks: true, copyHasPayments: false, Noon)); // one verified on another phone
        Assert.False(SyncPolicy.ReadsTheLedger(fresh, namesAPaymentTheCopyLacks: false, copyHasPayments: false, Noon.AddDays(30))); // no payment: never again
        Assert.False(SyncPolicy.ReadsTheLedger(fresh, namesAPaymentTheCopyLacks: false, copyHasPayments: true, Noon.AddHours(23)));
        Assert.True(SyncPolicy.ReadsTheLedger(fresh, namesAPaymentTheCopyLacks: false, copyHasPayments: true, Noon.AddDays(1))); // a day: refunds may be marked
        Assert.True(SyncPolicy.ReadsTheLedger(fresh, namesAPaymentTheCopyLacks: false, copyHasPayments: true, Noon.AddHours(-1))); // the clock went back
    }

    [Fact]
    public void The_ledgers_copy_reads_back_goes_with_its_account_and_a_file_from_before_it_reads_as_never_read()
    {
        var copy = new LedgerCopy(LedgerText((1, Small, "GPA.1")), Noon, isStale: true);
        StateFile().Save(CloudState.New("w").ForPlayer(Player).WithLedger(copy));
        LedgerCopy? read = StateFile().Load().State.Ledger;
        string path = Path.Combine(_folder, CloudStateFile.FileName);
        string text = File.ReadAllText(path);
        File.WriteAllText(path, text.Substring(0, text.IndexOf(",\"ledger\"", StringComparison.Ordinal)) + "}");

        Assert.Equal((copy.Text, copy.ReadAt, true), (read?.Text, read?.ReadAt, read?.IsStale ?? false));
        Assert.Null(StateFile().Load().State.Ledger);
        Assert.Null(CloudState.New("w").ForPlayer(Player).WithLedger(copy).ForPlayer(new CloudPlayerId("player-2")).Ledger);
        Assert.Same(copy, CloudState.New("w").ForPlayer(Player).WithLedger(copy).AfterRead(CloudBase.NoSave(Rules.StartingWallet), Noon).Ledger);
    }
}
