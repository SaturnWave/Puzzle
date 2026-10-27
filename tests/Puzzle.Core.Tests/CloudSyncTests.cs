using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using static Puzzle.Core.Tests.CloudSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The sync with the cloud (docs/research/server-v1.md §3.3–§3.8), against a cloud in memory: two phones of one
/// player, a reinstall, a new phone, an older version of the game, a cloud out of reach or closed, the budget, a write
/// whose answer was lost, the switch and the other gates. In every case the phone's progress file stays the truth:
/// what the cloud cannot do never stops or changes the game.
/// </summary>
public class CloudSyncTests
{
    private readonly FakeCloudSave _cloud = new();
    private readonly MovableClock _clock = new(Noon);

    private Device Phone(PlayerProgress start, string writer = "phone") => new(_cloud, _clock, start, writer);

    private static UploadResult UploadOf(SyncOutcome outcome) => Assert.IsType<SyncOutcome.Done>(outcome).Upload;

    [Fact]
    public async Task No_save_is_written_for_a_player_who_has_won_no_level_and_the_first_win_writes_one()
    {
        Device phone = Phone(NewPlayer());
        phone.Progress.Play(progress => progress.WithWallet(Assert.IsType<DailyGift.Given>(Rules.Daily.TakenOn(progress.Wallet, DayStamp.Of(Noon))).Wallet));

        Assert.Equal(UploadResult.NoLevelWonYet, UploadOf(await phone.SyncsAsync(SyncTrigger.ColdStart)));
        Assert.Null(_cloud.Save);

        phone.Progress.Play(progress => progress.Won(1));
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.Background)));
        Assert.Equal(phone.Progress.Current, ProgressIn(_cloud.Save));
    }

    /// <summary>Two phones of one player play in turn and at once: what each earned adds up, both end with the same progress, and the cloud holds it.</summary>
    [Fact]
    public async Task Two_phones_of_one_player_add_up_what_each_earned()
    {
        Device phoneA = Phone(NewPlayer().Won(10), "phone-a");
        Assert.Equal(UploadResult.Written, UploadOf(await phoneA.SyncsAsync(SyncTrigger.ColdStart)));
        PlayerProgress first = phoneA.Progress.Current;

        Device phoneB = Phone(NewPlayer(), "phone-b");
        var cameBack = Assert.IsType<SyncOutcome.Done>(await phoneB.SyncsAsync(SyncTrigger.ColdStart));
        Assert.True(cameBack.Adopted); // the "your save came back" card
        Assert.Equal(first, phoneB.Progress.Current);

        // Both play from the same base; A uploads first, so B's upload crosses it, is refused, merges and goes again.
        _clock.Passes(TimeSpan.FromMinutes(61));
        phoneA.Progress.Play(progress => progress.Won(2));
        phoneB.Progress.Play(progress => progress.Won(1).WithTutorialStepSeen(TutorialStep.Chain));
        Assert.Equal(UploadResult.Written, UploadOf(await phoneA.SyncsAsync(SyncTrigger.Background)));
        var crossed = Assert.IsType<SyncOutcome.Done>(await phoneB.SyncsAsync(SyncTrigger.Background));
        Assert.Equal((UploadResult.Written, true), (crossed.Upload, crossed.Adopted));

        _clock.Passes(TimeSpan.FromHours(12));
        await phoneA.SyncsAsync(SyncTrigger.ColdStart);

        PlayerProgress merged = ProgressIn(_cloud.Save);
        Assert.Equal(first.Wallet.Coins.Amount + (3 * 25), merged.Wallet.Coins.Amount);
        Assert.Equal((12, true), (merged.LevelsWon, merged.Tutorial.Has(TutorialStep.Chain)));
        Assert.Equal(merged, phoneA.Progress.Current);
        Assert.Equal(merged, phoneB.Progress.Current);
    }

    /// <summary>The game deleted and installed again on a phone linked to Google Play Games: the same account, no cloud.json. The cloud's save comes back with what was played before the sign-in on top.</summary>
    [Fact]
    public async Task After_a_reinstall_the_save_comes_back_with_what_was_played_since_on_top()
    {
        PlayerProgress before = NewPlayer().Won(312).WithCoins(4200);
        _cloud.SaveWrittenOutsideTheGame(SaveText(before));
        Device reinstalled = Phone(NewPlayer().Won(2), "reinstalled");

        var outcome = Assert.IsType<SyncOutcome.Done>(await reinstalled.SyncsAsync(SyncTrigger.ColdStart));

        Assert.True(outcome.Adopted);
        Assert.Equal(UploadResult.Written, outcome.Upload);
        Assert.Equal((312, 4200L + 50), (reinstalled.Progress.Current.LevelsWon, reinstalled.Progress.Current.Wallet.Coins.Amount));
        Assert.Equal(reinstalled.Progress.Current, ProgressIn(_cloud.Save));
    }

    /// <summary>A new phone that was never linked signs in to a new account: its cloud has nothing, the old account's save stays where it was, and the game starts as a new player.</summary>
    [Fact]
    public async Task A_new_phone_never_linked_starts_as_a_new_player_and_leaves_the_old_save_alone()
    {
        _cloud.SaveWrittenOutsideTheGame(SaveText(NewPlayer().Won(312)));
        var newAccountsCloud = new FakeCloudSave();
        var newPhone = new Device(newAccountsCloud, _clock, NewPlayer(), "new-phone");

        var outcome = Assert.IsType<SyncOutcome.Done>(await newPhone.SyncsAsync(new CloudPlayerId("player-2"), SyncTrigger.ColdStart));

        Assert.False(outcome.Adopted);
        Assert.Equal(NewPlayer(), newPhone.Progress.Current);
        Assert.Null(newAccountsCloud.Save);
        Assert.Equal(312, ProgressIn(_cloud.Save).LevelsWon);
    }

    /// <summary>An older version of the game meets a save a later version wrote: it writes nothing over it, shows how far the player is there, and plays on with its own file.</summary>
    [Fact]
    public async Task An_older_game_never_writes_over_a_later_games_save_and_plays_on()
    {
        string later = SaveText(NewPlayer().Won(312)).Replace("\"progress\":{\"version\":7", "\"progress\":{\"version\":8", StringComparison.Ordinal);
        _cloud.SaveWrittenOutsideTheGame(later);
        PlayerProgress own = NewPlayer().Won(40);
        Device older = Phone(own, "older");

        var halted = Assert.IsType<SyncOutcome.Halted>(await older.SyncsAsync(SyncTrigger.ColdStart));
        older.Progress.Play(progress => progress.Won(1));
        SyncOutcome purchase = await older.SyncsAsync(SyncTrigger.PurchaseDelivered);
        _clock.Passes(TimeSpan.FromMinutes(5));
        SyncOutcome background = await older.SyncsAsync(SyncTrigger.Background);

        Assert.Equal(8, Assert.IsType<CloudHalt.LaterGame>(halted.Halt).ProgressLayout);
        Assert.Equal(312, halted.Understood!.LevelsWon);
        Assert.IsType<SyncOutcome.Halted>(purchase);
        Assert.IsType<SyncOutcome.Halted>(background);
        Assert.Equal((later, 0, 1), (_cloud.Save!.Text, _cloud.Writes, _cloud.Reads)); // nothing written; no read on every trigger
        Assert.Equal(own.Won(1), older.Progress.Current);
    }

    /// <summary>Once the game is updated to the save's layout the halt no longer holds: the next start reads at once, the budget notwithstanding.</summary>
    [Fact]
    public async Task An_updated_game_reads_the_save_it_stopped_at_and_syncs_again()
    {
        _cloud.SaveWrittenOutsideTheGame(SaveText(NewPlayer().Won(312)));
        CloudState stoppedByAnOlderVersion = CloudState.Restored(
            true, "updated", Player, null, Noon, null, null, 0, null, new CloudHalt.LaterGame(ProgressDocument.CurrentVersion));
        var updated = new Device(_cloud, _clock, NewPlayer().Won(3), "updated", stoppedByAnOlderVersion);

        var outcome = Assert.IsType<SyncOutcome.Done>(await updated.SyncsAsync(SyncTrigger.ColdStart));

        Assert.True(outcome.Read && outcome.Adopted);
        Assert.Null(updated.Sync.State.Halt);
    }

    [Fact]
    public async Task A_damaged_save_is_never_written_over()
    {
        _cloud.SaveWrittenOutsideTheGame("{\"envelope\":1,\"revision\":3,\"progressGz\":\"broken\"}");
        Device phone = Phone(NewPlayer().Won(5));

        var halted = Assert.IsType<SyncOutcome.Halted>(await phone.SyncsAsync(SyncTrigger.ColdStart));
        phone.Progress.Play(progress => progress.Won(1));
        await phone.SyncsAsync(SyncTrigger.PaintingOrRoomCompleted);

        Assert.IsType<CloudHalt.DamagedSave>(halted.Halt);
        Assert.Equal("{\"envelope\":1,\"revision\":3,\"progressGz\":\"broken\"}", _cloud.Save!.Text);
        Assert.Equal(0, _cloud.Writes);
        Assert.Equal(6, phone.Progress.Current.LevelsWon);
    }

    /// <summary>No network: the game plays on as it does today, nothing waits, and the next trigger simply tries again (nothing reached the cloud to back off from).</summary>
    [Fact]
    public async Task With_no_network_the_game_plays_on_and_the_next_trigger_tries_again()
    {
        Device phone = Phone(NewPlayer().Won(3));
        _cloud.GoesDown(CloudFailure.NoNetwork);

        var unreachable = Assert.IsType<SyncOutcome.Unreachable>(await phone.SyncsAsync(SyncTrigger.ColdStart));
        phone.Progress.Play(progress => progress.Won(1));
        _cloud.ComesBack();
        SyncOutcome back = await phone.SyncsAsync(SyncTrigger.Background);

        Assert.Equal((CloudFailure.NoNetwork, (DateTimeOffset?)null), (unreachable.Failure, unreachable.RetryAt));
        Assert.Equal(UploadResult.Written, UploadOf(back));
        Assert.Equal(0, phone.Progress.TimesAdopted);
        Assert.Equal(4, ProgressIn(_cloud.Save).LevelsWon);
    }

    /// <summary>The free tier's limit passed and Unity closed the services: the same as a cloud that does not answer. The tries back off, the game never waits or changes.</summary>
    [Fact]
    public async Task A_closed_cloud_is_tried_after_1_5_30_minutes_then_2_6_and_24_hours()
    {
        PlayerProgress start = NewPlayer().Won(3);
        Device phone = Phone(start);
        _cloud.GoesDown(CloudFailure.Blocked);
        TimeSpan[] waits = { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6), TimeSpan.FromHours(24), TimeSpan.FromHours(24) };

        foreach (TimeSpan wait in waits)
        {
            var unreachable = Assert.IsType<SyncOutcome.Unreachable>(await phone.SyncsAsync(SyncTrigger.ColdStart));
            Assert.Equal(_clock.Now + wait, unreachable.RetryAt);

            _clock.Passes(wait - TimeSpan.FromSeconds(1));
            Assert.IsType<SyncOutcome.Postponed>(await phone.SyncsAsync(SyncTrigger.PurchaseDelivered));
            _clock.Passes(TimeSpan.FromSeconds(1));
        }

        Assert.Equal((start, 0), (phone.Progress.Current, phone.Progress.TimesAdopted));

        _cloud.ComesBack();
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.Background)));
        Assert.Equal((0, (DateTimeOffset?)null), (phone.Sync.State.Failures, phone.Sync.State.RetryAt));
    }

    [Fact]
    public async Task A_new_sign_in_does_not_wait_for_the_backing_off()
    {
        Device phone = Phone(NewPlayer().Won(3));
        _cloud.GoesDown(CloudFailure.Unavailable);
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        _cloud.ComesBack();

        Assert.IsType<SyncOutcome.Postponed>(await phone.SyncsAsync(SyncTrigger.Background));
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.AccountChanged)));
    }

    /// <summary>
    /// At most an upload an hour for the background and a read every 12 hours at the start; a payment, a painting and a
    /// sign-in upload at once. What the background waits with here is no won level, which would go at once (T530).
    /// </summary>
    [Fact]
    public async Task The_budget_spaces_uploads_an_hour_and_reads_12_hours_apart_but_a_payment_goes_at_once()
    {
        Device phone = Phone(NewPlayer().Won(3));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        int reads = _cloud.Reads;

        _clock.Passes(TimeSpan.FromMinutes(30));
        phone.Progress.Play(progress => progress.WithTutorialStepSeen(TutorialStep.Chain));
        var waits = Assert.IsType<SyncOutcome.Done>(await phone.SyncsAsync(SyncTrigger.Background));
        Assert.Equal((UploadResult.WaitsForTheBudget, Noon + TimeSpan.FromHours(1)), (waits.Upload, waits.UploadWaitsUntil!.Value));

        phone.Progress.Play(progress => progress.Bought(Small, "GPA.1", number: 1));
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.PurchaseDelivered)));

        _clock.Passes(TimeSpan.FromHours(11));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        Assert.Equal(reads, _cloud.Reads); // 11.5 hours after the last read: no read

        _clock.Passes(TimeSpan.FromHours(1));
        Assert.True(Assert.IsType<SyncOutcome.Done>(await phone.SyncsAsync(SyncTrigger.ColdStart)).Read);
    }

    [Fact]
    public async Task Remote_config_can_open_the_upload_budget_to_15_minutes()
    {
        Device phone = Phone(NewPlayer().Won(3));
        phone.Sync.Settings = SyncSettings.FromRemote(syncs: null, uploadMinMinutes: 15, readMinHours: null, validatesPurchases: null);
        await phone.SyncsAsync(SyncTrigger.ColdStart);

        _clock.Passes(TimeSpan.FromMinutes(14));
        phone.Progress.Play(progress => progress.WithTutorialStepSeen(TutorialStep.Chain));
        Assert.Equal(UploadResult.WaitsForTheBudget, UploadOf(await phone.SyncsAsync(SyncTrigger.Background)));
        _clock.Passes(TimeSpan.FromMinutes(2));

        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.Background)));
    }

    /// <summary>
    /// T530 (2026-10-27): Android stops a game it puts away within seconds, so a won level must not wait for the hourly
    /// budget. The session's first win goes at the win; a later one waits for the way out, which sends it; after the
    /// way out the next win is the next session's first and goes at once again; a way out with no win to send keeps to
    /// the budget.
    /// </summary>
    [Fact]
    public async Task A_won_level_goes_at_once_the_sessions_first_at_the_win_and_the_others_on_the_way_out()
    {
        Device phone = Phone(NewPlayer().Won(3));
        await phone.SyncsAsync(SyncTrigger.ColdStart);

        _clock.Passes(TimeSpan.FromMinutes(5));
        phone.Progress.Play(progress => progress.Won(1));
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.LevelWon)));
        Assert.Equal(4, ProgressIn(_cloud.Save).LevelsWon);

        _clock.Passes(TimeSpan.FromMinutes(3));
        phone.Progress.Play(progress => progress.Won(1));
        Assert.Equal(UploadResult.WaitsForTheBudget, UploadOf(await phone.SyncsAsync(SyncTrigger.LevelWon)));
        Assert.Equal(4, ProgressIn(_cloud.Save).LevelsWon);

        _clock.Passes(TimeSpan.FromMinutes(1));
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.Background)));
        Assert.Equal(5, ProgressIn(_cloud.Save).LevelsWon);

        _clock.Passes(TimeSpan.FromMinutes(10));
        phone.Progress.Play(progress => progress.Won(1));
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.LevelWon)));
        Assert.Equal(6, ProgressIn(_cloud.Save).LevelsWon);

        phone.Progress.Play(progress => progress.WithTutorialStepSeen(TutorialStep.Chain));
        Assert.Equal(UploadResult.WaitsForTheBudget, UploadOf(await phone.SyncsAsync(SyncTrigger.Background)));
        Assert.Equal(4, _cloud.Writes); // the start's, the two first wins and a way out; none for the tutorial's step
    }

    [Fact]
    public async Task Remote_config_can_let_the_won_levels_wait_for_the_budget_as_before()
    {
        Device phone = Phone(NewPlayer().Won(3));
        phone.Sync.Settings = SyncSettings.FromRemote(syncs: null, uploadMinMinutes: null, readMinHours: null, validatesPurchases: null, winsGoAtOnce: false);
        await phone.SyncsAsync(SyncTrigger.ColdStart);

        _clock.Passes(TimeSpan.FromMinutes(5));
        phone.Progress.Play(progress => progress.Won(1));

        Assert.Equal(UploadResult.WaitsForTheBudget, UploadOf(await phone.SyncsAsync(SyncTrigger.LevelWon)));
        Assert.Equal(UploadResult.WaitsForTheBudget, UploadOf(await phone.SyncsAsync(SyncTrigger.Background)));
        Assert.Equal(3, ProgressIn(_cloud.Save).LevelsWon);
    }

    /// <summary>The way out had no network: the game was closed with a win the cloud lacks, and the next start sends it at once, within the hour.</summary>
    [Fact]
    public async Task A_win_the_way_out_could_not_send_goes_at_the_next_start()
    {
        Device phone = Phone(NewPlayer().Won(3));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        _clock.Passes(TimeSpan.FromMinutes(5));
        phone.Progress.Play(progress => progress.Won(1));
        await phone.SyncsAsync(SyncTrigger.LevelWon);
        phone.Progress.Play(progress => progress.Won(1));
        _cloud.GoesDown(CloudFailure.NoNetwork);
        Assert.IsType<SyncOutcome.Unreachable>(await phone.SyncsAsync(SyncTrigger.Background));

        _clock.Passes(TimeSpan.FromMinutes(10));
        _cloud.ComesBack();
        var openedAgain = new Device(_cloud, _clock, phone.Progress.Current, "phone-again", phone.Sync.State);

        Assert.Equal(UploadResult.Written, UploadOf(await openedAgain.SyncsAsync(SyncTrigger.ColdStart)));
        Assert.Equal(5, ProgressIn(_cloud.Save).LevelsWon);
    }

    /// <summary>T562 (2026-10-27): what was played while the switch was off goes at once when it is turned on again, the hour's budget notwithstanding.</summary>
    [Fact]
    public async Task The_switch_turned_on_again_uploads_what_was_played_while_it_was_off_at_once()
    {
        Device phone = Phone(NewPlayer().Won(3));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        phone.Sync.SwitchTo(isOn: false);
        _clock.Passes(TimeSpan.FromMinutes(10));
        phone.Progress.Play(progress => progress.WithTutorialStepSeen(TutorialStep.Chain).WithCoins(900));
        Assert.IsType<SyncOutcome.Off>(await phone.SyncsAsync(SyncTrigger.Background));

        phone.Sync.SwitchTo(isOn: true);
        var on = Assert.IsType<SyncOutcome.Done>(await phone.SyncsAsync(SyncTrigger.SwitchedOn));

        Assert.Equal((true, UploadResult.Written), (on.Read, on.Upload));
        Assert.Equal(phone.Progress.Current, ProgressIn(_cloud.Save));
    }

    /// <summary>
    /// Android's backup brought the phone's files back as they were a day before, while the installation went on and
    /// uploaded what it won after them: the save's last write is the installation's own, so the phone's progress is the
    /// merge's base, and the two wins the file held, which the save holds too, are not counted twice.
    /// </summary>
    [Fact]
    public async Task A_state_the_backup_brought_back_counts_nothing_twice()
    {
        Device phone = Phone(NewPlayer().Won(3).WithCoins(1000));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        _clock.Passes(TimeSpan.FromHours(1));
        phone.Progress.Play(progress => progress.Won(2));
        CloudState backedUpState = phone.Sync.State;
        PlayerProgress backedUpProgress = phone.Progress.Current;
        await phone.SyncsAsync(SyncTrigger.Background);
        _clock.Passes(TimeSpan.FromHours(1));
        phone.Progress.Play(progress => progress.Won(1));
        await phone.SyncsAsync(SyncTrigger.LevelWon);

        _clock.Passes(TimeSpan.FromHours(13));
        var restored = new Device(_cloud, _clock, backedUpProgress, "restored", backedUpState, CloudStateOrigin.MayBeFromABackup);
        var outcome = Assert.IsType<SyncOutcome.Done>(await restored.SyncsAsync(SyncTrigger.ColdStart));

        Assert.True(outcome.Read && outcome.Adopted);
        Assert.Equal((6, 1000L + (3 * 25)), (restored.Progress.Current.LevelsWon, restored.Progress.Current.Wallet.Coins.Amount));
        Assert.Equal(1000L + (3 * 25), ProgressIn(_cloud.Save).Wallet.Coins.Amount);
    }

    /// <summary>
    /// A second phone set up from the first one's backup shares its writer id, and the first plays on: once the second
    /// has synced, the first phone's later write is another phone's, and what the second earned meanwhile is kept.
    /// </summary>
    [Fact]
    public async Task A_phone_set_up_from_a_backup_keeps_its_wins_when_the_old_phone_plays_on()
    {
        Device oldPhone = Phone(NewPlayer().Won(3).WithCoins(1000));
        await oldPhone.SyncsAsync(SyncTrigger.ColdStart);
        var newPhone = new Device(_cloud, _clock, oldPhone.Progress.Current, "new-phone", oldPhone.Sync.State, CloudStateOrigin.MayBeFromABackup);
        _clock.Passes(TimeSpan.FromHours(13));
        await newPhone.SyncsAsync(SyncTrigger.ColdStart);

        newPhone.Progress.Play(progress => progress.Won(1)); // +25 on the new phone, not yet in the cloud
        oldPhone.Progress.Play(progress => progress.Won(2)); // +50 on the old one, which writes first
        await oldPhone.SyncsAsync(SyncTrigger.PaintingOrRoomCompleted);
        _clock.Passes(TimeSpan.FromMinutes(2));
        Assert.Equal(UploadResult.Written, UploadOf(await newPhone.SyncsAsync(SyncTrigger.PaintingOrRoomCompleted)));

        Assert.Equal(1000L + (3 * 25), newPhone.Progress.Current.Wallet.Coins.Amount);
        Assert.Equal(1000L + (3 * 25), ProgressIn(_cloud.Save).Wallet.Coins.Amount);
    }

    /// <summary>A write lands and its answer is lost: the phone plays on, and the next sync finds its own write in the save and counts nothing twice.</summary>
    [Fact]
    public async Task A_write_whose_answer_was_lost_is_found_again_and_nothing_is_counted_twice()
    {
        Device phone = Phone(NewPlayer().Won(3).WithCoins(1000));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        _clock.Passes(TimeSpan.FromHours(2));
        phone.Progress.Play(progress => progress.Won(2));
        _cloud.LosesTheNextAnswer();

        Assert.IsType<SyncOutcome.Unreachable>(await phone.SyncsAsync(SyncTrigger.Background));
        Assert.Equal(5, ProgressIn(_cloud.Save).LevelsWon); // it landed

        phone.Progress.Play(progress => progress.Won(1));
        _clock.Passes(TimeSpan.FromMinutes(2));
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.PaintingOrRoomCompleted)));

        Assert.Equal(1000 + (3 * 25), ProgressIn(_cloud.Save).Wallet.Coins.Amount);
        Assert.Equal(1000 + (3 * 25), phone.Progress.Current.Wallet.Coins.Amount);
    }

    /// <summary>The same, with another phone merging the lost write and writing on top before this phone comes back: still once.</summary>
    [Fact]
    public async Task A_lost_write_merged_by_another_phone_is_still_counted_once()
    {
        Device phoneA = Phone(NewPlayer().Won(3).WithCoins(1000), "phone-a");
        await phoneA.SyncsAsync(SyncTrigger.ColdStart);
        Device phoneB = Phone(NewPlayer(), "phone-b");
        await phoneB.SyncsAsync(SyncTrigger.ColdStart);

        _clock.Passes(TimeSpan.FromHours(2));
        phoneA.Progress.Play(progress => progress.Won(2)); // +50
        _cloud.LosesTheNextAnswer();
        await phoneA.SyncsAsync(SyncTrigger.Background);

        _clock.Passes(TimeSpan.FromHours(13));
        await phoneB.SyncsAsync(SyncTrigger.ColdStart); // B takes A's lost write
        phoneB.Progress.Play(progress => progress.Won(1).WithTutorialStepSeen(TutorialStep.Chain)); // +25
        await phoneB.SyncsAsync(SyncTrigger.PaintingOrRoomCompleted);

        phoneA.Progress.Play(progress => progress.WithTutorialStepSeen(TutorialStep.Box));
        await phoneA.SyncsAsync(SyncTrigger.Background);

        Assert.Equal(1000 + 50 + 25, ProgressIn(_cloud.Save).Wallet.Coins.Amount); // A's two wins after the 1 000 and B's one, each once
        Assert.Equal(1000 + 50 + 25, phoneA.Progress.Current.Wallet.Coins.Amount);
        Assert.True(phoneA.Progress.Current.Tutorial.Has(TutorialStep.Chain) && phoneA.Progress.Current.Tutorial.Has(TutorialStep.Box));
    }

    [Theory]
    [InlineData(CloudOff.SwitchedOff)]
    [InlineData(CloudOff.TurnedOffRemotely)]
    [InlineData(CloudOff.ThisRunOnly)]
    [InlineData(CloudOff.TesterMode)]
    public async Task While_a_gate_is_closed_nothing_reaches_the_cloud(CloudOff gate)
    {
        Device phone = Phone(NewPlayer().Won(3));
        switch (gate)
        {
            case CloudOff.SwitchedOff:
                phone.Sync.SwitchTo(isOn: false);
                break;
            case CloudOff.TurnedOffRemotely:
                phone.Sync.Settings = SyncSettings.FromRemote(syncs: false, null, null, null);
                break;
            case CloudOff.ThisRunOnly:
                phone.Progress.Keeping = ProgressKeeping.ForThisRunOnly;
                break;
            case CloudOff.TesterMode:
                phone.Progress.IsTesterMode = true;
                break;
        }

        Assert.Equal(gate, Assert.IsType<SyncOutcome.Off>(await phone.SyncsAsync(SyncTrigger.AccountChanged)).Why);
        Assert.Equal((0, 0), (_cloud.Reads, _cloud.Writes));
    }

    [Fact]
    public async Task Another_account_starts_from_nothing_of_the_one_before()
    {
        Device phone = Phone(NewPlayer().Won(3));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        var otherCloud = new FakeCloudSave();
        otherCloud.SaveWrittenOutsideTheGame(SaveText(NewPlayer().Won(50)));
        var sameDeviceOtherAccount = new Device(otherCloud, _clock, phone.Progress.Current, "phone", phone.Sync.State);

        var outcome = Assert.IsType<SyncOutcome.Done>(await sameDeviceOtherAccount.SyncsAsync(new CloudPlayerId("player-2"), SyncTrigger.AccountChanged));

        Assert.Equal(new CloudPlayerId("player-2"), sameDeviceOtherAccount.Sync.State.Player);
        Assert.True(outcome.Adopted); // merged over a new player's base: the two progresses add up (§3.6)
        Assert.Equal(50, sameDeviceOtherAccount.Progress.Current.LevelsWon);
    }

    [Fact]
    public async Task A_save_deleted_in_the_cloud_is_written_anew_from_the_phone()
    {
        Device phone = Phone(NewPlayer().Won(3));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        _cloud.Deleted();

        _clock.Passes(TimeSpan.FromHours(13));
        phone.Progress.Play(progress => progress.Won(1));
        Assert.Equal(UploadResult.Written, UploadOf(await phone.SyncsAsync(SyncTrigger.ColdStart)));
        Assert.Equal(4, ProgressIn(_cloud.Save).LevelsWon);
    }

    [Fact]
    public async Task A_sync_asked_for_while_one_runs_runs_after_it()
    {
        var gated = new GatedCloud(_cloud);
        var progress = new HeldProgress(NewPlayer().Won(3));
        var sync = new CloudSync(gated, new CloudStateInMemory(CloudState.New("phone")), progress, Rules, Json, _clock, "1.1.0", RandomIds.New);

        Task<SyncOutcome> first = sync.SyncAsync(Player, SyncTrigger.ColdStart, CancellationToken.None);
        progress.Play(held => held.Bought(Small, "GPA.1", number: 1));
        SyncOutcome second = await sync.SyncAsync(Player, SyncTrigger.PurchaseDelivered, CancellationToken.None);
        gated.Opens();

        Assert.IsType<SyncOutcome.Queued>(second);
        Assert.Equal(UploadResult.NotNeeded, UploadOf(await first)); // the start's upload took the payment; the queued sync found nothing more
        Assert.Equal(progress.Current, ProgressIn(_cloud.Save));
    }

    [Fact]
    public async Task Forgetting_the_account_turns_the_switch_off_and_keeps_nothing_of_it()
    {
        Device phone = Phone(NewPlayer().Won(3));
        await phone.SyncsAsync(SyncTrigger.ColdStart);
        string writer = phone.Sync.State.Writer;

        phone.Sync.Forget();

        Assert.Equal((false, (CloudPlayerId?)null, (CloudBase?)null), (phone.Sync.State.IsSwitchedOn, phone.Sync.State.Player, phone.Sync.State.Base));
        Assert.NotEqual(writer, phone.Sync.State.Writer);
        Assert.IsType<SyncOutcome.Off>(await phone.SyncsAsync(SyncTrigger.ColdStart));
    }

    /// <summary>
    /// Stage 4's budget of reads (docs/research/server-v1.md §2.4): the ledger is a read of its own, made when it can have
    /// changed. A phone that merges a save naming a payment verified on another phone reads it then; a payer's phone
    /// reads it once a day at most; a phone without payments reads it once.
    /// </summary>
    [Fact]
    public async Task The_ledger_is_read_when_another_phone_was_paid_on_and_once_a_day_for_a_payer()
    {
        Device phoneA = Phone(NewPlayer().Won(3), "phone-a");
        Device phoneB = Phone(NewPlayer().Won(3), "phone-b");
        await phoneA.SyncsAsync(SyncTrigger.ColdStart);
        await phoneB.SyncsAsync(SyncTrigger.ColdStart);
        _clock.Passes(TimeSpan.FromHours(13));
        await phoneB.SyncsAsync(SyncTrigger.ColdStart);
        int ledgerReads = _cloud.LedgerReads;

        // Phone A is paid on: the server enters #1, phone A gives it and uploads.
        _cloud.LedgerWrittenByTheServer(LedgerText((1, Small, "GPA.1")));
        phoneA.Progress.Play(progress => progress.Bought(Small, "GPA.1", number: 1));
        phoneA.Sync.KnowTheLedgerChanged();
        await phoneA.SyncsAsync(SyncTrigger.PurchaseDelivered);

        // Phone B opens: the save names #1, which its copy lacks, so the merge reads the ledger.
        _clock.Passes(TimeSpan.FromHours(13));
        await phoneB.SyncsAsync(SyncTrigger.ColdStart);
        Assert.Equal(ledgerReads + 1, _cloud.LedgerReads);
        Assert.Contains("coins.small", phoneB.Sync.State.Ledger?.Text ?? string.Empty, StringComparison.Ordinal);

        // Twelve hours on, a payer's copy is still fresh; a day on, it is read again.
        _clock.Passes(TimeSpan.FromHours(13));
        await phoneB.SyncsAsync(SyncTrigger.ColdStart);
        Assert.Equal(ledgerReads + 1, _cloud.LedgerReads);
        _clock.Passes(TimeSpan.FromHours(13));
        await phoneB.SyncsAsync(SyncTrigger.ColdStart);
        Assert.Equal(ledgerReads + 2, _cloud.LedgerReads);
    }
}
