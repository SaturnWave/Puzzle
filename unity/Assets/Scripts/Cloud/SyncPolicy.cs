#nullable enable
using System;
using Puzzle.Client.Flow;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// The keys of the game's Remote Config (the owner's decision 6, 2026-10-26): the four settings the owner can turn
    /// without an update of the game, when the free tier's limits come near or a merge goes wrong, and a fifth of
    /// 2026-10-27 for the won levels that upload at once. A key the project does not set keeps its default.
    /// </summary>
    public static class RemoteConfigKeys
    {
        /// <summary>A switch: false stops every read and write of the save (purchases are still verified).</summary>
        public const string CloudSyncEnabled = "cloud_sync_enabled";

        /// <summary>A number of minutes: the least time between two uploads that the budget pays for.</summary>
        public const string UploadMinMinutes = "cloud_upload_min_minutes";

        /// <summary>A number of hours: the least time between two reads at the game's start.</summary>
        public const string ReadMinHours = "cloud_read_min_hours";

        /// <summary>A switch: false sends no purchase to the server for verification (the game delivers on the phone, as before the server).</summary>
        public const string PurchaseValidationEnabled = "purchase_validation_enabled";

        /// <summary>
        /// A switch (2026-10-27, T530): false lets a won level wait for the budget as every other change does, the
        /// owner's lever when the free tier's writes come near (<see cref="SyncPolicy.LetsAWinGoAtOnce"/>).
        /// </summary>
        public const string WinUploadsAtOnce = "cloud_win_uploads_at_once";
    }

    /// <summary>
    /// How often the sync may use the cloud, so the free tier's monthly reads and writes last (docs/research/server-v1.md
    /// §2.4): at most one read every 12 hours at the game's start and one upload every 60 minutes by default (the
    /// tighter of the plan's two rules, 2026-10-26); a payment delivered, a painting or a room completed, a new sign-in
    /// and the switch turned on upload at once, outside the budget, and so do the won levels the cloud lacks, as
    /// <see cref="SyncPolicy.LetsAWinGoAtOnce"/> says (2026-10-27). Remote Config can change each within bounds that keep
    /// a typing mistake from spending the month's budget in a day, or from never syncing at all.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class SyncSettings
    {
        public static readonly TimeSpan DefaultUploadEvery = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan DefaultReadEvery = TimeSpan.FromHours(12);

        /// <summary>The bounds of the time between two uploads that Remote Config can set: 5 minutes to a day.</summary>
        public static readonly TimeSpan UploadEveryAtLeast = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan UploadEveryAtMost = TimeSpan.FromHours(24);

        /// <summary>The bounds of the time between two reads that Remote Config can set: an hour to a week.</summary>
        public static readonly TimeSpan ReadEveryAtLeast = TimeSpan.FromHours(1);
        public static readonly TimeSpan ReadEveryAtMost = TimeSpan.FromDays(7);

        /// <param name="winsGoAtOnce">Whether a won level the cloud lacks may upload outside the budget (<see cref="WinsGoAtOnce"/>).</param>
        /// <exception cref="ArgumentOutOfRangeException">A time outside its bounds.</exception>
        public SyncSettings(bool syncs, TimeSpan uploadEvery, TimeSpan readEvery, bool validatesPurchases, bool winsGoAtOnce)
        {
            if (uploadEvery < UploadEveryAtLeast || uploadEvery > UploadEveryAtMost)
            {
                throw new ArgumentOutOfRangeException(nameof(uploadEvery), uploadEvery, $"Uploads are between {UploadEveryAtLeast} and {UploadEveryAtMost} apart.");
            }

            if (readEvery < ReadEveryAtLeast || readEvery > ReadEveryAtMost)
            {
                throw new ArgumentOutOfRangeException(nameof(readEvery), readEvery, $"Reads are between {ReadEveryAtLeast} and {ReadEveryAtMost} apart.");
            }

            Syncs = syncs;
            UploadEvery = uploadEvery;
            ReadEvery = readEvery;
            ValidatesPurchases = validatesPurchases;
            WinsGoAtOnce = winsGoAtOnce;
        }

        /// <summary>
        /// The settings of a game that has not heard from Remote Config: it syncs and verifies, an upload an hour at most
        /// with the won levels at once, a read every 12 hours.
        /// </summary>
        public static SyncSettings Default { get; } = new SyncSettings(syncs: true, DefaultUploadEvery, DefaultReadEvery, validatesPurchases: true, winsGoAtOnce: true);

        /// <summary>Whether the save is read and written at all (<see cref="RemoteConfigKeys.CloudSyncEnabled"/>).</summary>
        public bool Syncs { get; }

        /// <summary>The least time between two uploads the budget pays for (<see cref="RemoteConfigKeys.UploadMinMinutes"/>).</summary>
        public TimeSpan UploadEvery { get; }

        /// <summary>The least time between two reads at the game's start (<see cref="RemoteConfigKeys.ReadMinHours"/>).</summary>
        public TimeSpan ReadEvery { get; }

        /// <summary>Whether purchases are sent to the server for verification (<see cref="RemoteConfigKeys.PurchaseValidationEnabled"/>).</summary>
        public bool ValidatesPurchases { get; }

        /// <summary>Whether a won level the cloud lacks may upload outside the budget (<see cref="RemoteConfigKeys.WinUploadsAtOnce"/>, <see cref="SyncPolicy.LetsAWinGoAtOnce"/>).</summary>
        public bool WinsGoAtOnce { get; }

        /// <summary>
        /// The settings Remote Config gave, each key that it did not give at its default, and a time outside its bounds
        /// at the bound nearest to it.
        /// </summary>
        public static SyncSettings FromRemote(bool? syncs, long? uploadMinMinutes, long? readMinHours, bool? validatesPurchases, bool? winsGoAtOnce = null) =>
            new SyncSettings(
                syncs ?? Default.Syncs,
                uploadMinMinutes is long minutes ? Within(TimeSpan.FromMinutes(Math.Min(minutes, (long)UploadEveryAtMost.TotalMinutes + 1)), UploadEveryAtLeast, UploadEveryAtMost) : Default.UploadEvery,
                readMinHours is long hours ? Within(TimeSpan.FromHours(Math.Min(hours, (long)ReadEveryAtMost.TotalHours + 1)), ReadEveryAtLeast, ReadEveryAtMost) : Default.ReadEvery,
                validatesPurchases ?? Default.ValidatesPurchases,
                winsGoAtOnce ?? Default.WinsGoAtOnce);

        public override string ToString() =>
            $"{(Syncs ? "syncs" : "does not sync")}, an upload every {UploadEvery.TotalMinutes:0} min at most{(WinsGoAtOnce ? " with the won levels at once" : ", the won levels too")}, "
            + $"a read every {ReadEvery.TotalHours:0} h, {(ValidatesPurchases ? "verifies" : "does not verify")} purchases";

        private static TimeSpan Within(TimeSpan time, TimeSpan least, TimeSpan most) => time < least ? least : time > most ? most : time;
    }

    /// <summary>What asks the sync to run (docs/research/server-v1.md §3.4). A closed set.</summary>
    public enum SyncTrigger
    {
        /// <summary>
        /// The game started and the player signed in: read (within the budget), merge, upload what the phone has new
        /// (within the budget; at once when the cloud lacks a won level the way out before could not send).
        /// </summary>
        ColdStart,

        /// <summary>
        /// The game went to the background or is closing, which ends the session: upload what changed, within the
        /// budget; at once when the cloud lacks a won level.
        /// </summary>
        Background,

        /// <summary>A long session's tick (every 30 minutes): upload what changed, within the budget.</summary>
        LongSession,

        /// <summary>A payment was delivered: upload at once, outside the budget.</summary>
        PurchaseDelivered,

        /// <summary>A painting or a room was completed: upload at once, outside the budget.</summary>
        PaintingOrRoomCompleted,

        /// <summary>An account was linked, or the game moved to another: read, merge and upload at once, outside the budget and the backing off.</summary>
        AccountChanged,

        /// <summary>
        /// A level was won, its coins and gifts given (2026-10-27, T530): the session's first win the cloud lacks
        /// uploads at once, outside the budget; the session's later wins wait for the way out (<see cref="Background"/>).
        /// </summary>
        LevelWon,

        /// <summary>
        /// The player turned the "Bulut kaydı" switch on again and signed in (2026-10-27, T562): read, merge and upload
        /// what was played while it was off at once, outside the budget and the backing off, as a new sign-in does.
        /// </summary>
        SwitchedOn,
    }

    /// <summary>
    /// Whether the cloud lacks a level the phone won, and whether a win of the session the game is in reached the
    /// cloud already (<see cref="SyncPolicy.LetsAWinGoAtOnce"/>). A session is the game in front: from its start, or
    /// from its return from the background, to the way out (<see cref="SyncTrigger.Background"/>). A closed set.
    /// </summary>
    public enum WinsInTheCloud
    {
        /// <summary>The cloud has every level the phone won.</summary>
        AllThere,

        /// <summary>The cloud lacks a won level, and no win of this session has reached it.</summary>
        LacksTheSessionsFirst,

        /// <summary>The cloud lacks a won level, though a win of this session reached it already.</summary>
        LacksALaterOne,
    }

    /// <summary>Why the sync does not run at all. A closed set.</summary>
    public enum CloudOff
    {
        /// <summary>The player's "Cloud save" switch is off.</summary>
        SwitchedOff,

        /// <summary>Remote Config turned the sync off (<see cref="RemoteConfigKeys.CloudSyncEnabled"/>).</summary>
        TurnedOffRemotely,

        /// <summary>The progress file cannot be kept this run (damaged, or a later game's): the run saves nowhere, so it syncs nothing either.</summary>
        ThisRunOnly,

        /// <summary>The tester mode: unlimited coins are never the cloud's (§3.1).</summary>
        TesterMode,
    }

    /// <summary>
    /// When the sync runs, reads and uploads (docs/research/server-v1.md §2.4, §3.4, §3.8): the gates that keep it off,
    /// the budget of reads and uploads, and the backing off after a failure (1, 5 and 30 minutes, then 2, 6 and 24
    /// hours, no loop of retries: each try waits for something that asks for a sync).
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class SyncPolicy
    {
        /// <summary>A player with payments has the ledger read again once this long has passed: the server's daily look at refunds may have marked one.</summary>
        public static readonly TimeSpan LedgerReadEvery = TimeSpan.FromDays(1);

        private static readonly TimeSpan[] Backoff =
        {
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6), TimeSpan.FromHours(24),
        };

        /// <summary>
        /// Whether the game may start Unity's services at all: only with the player's switch on, and only in a build
        /// that has a cloud project to talk to. While this is false nothing of the services runs, so nothing is sent.
        /// </summary>
        public static bool MayStartServices(bool isSwitchedOn, bool hasACloudProject) => isSwitchedOn && hasACloudProject;

        /// <summary>Why the sync does not run now; nothing when it may.</summary>
        public static CloudOff? Gate(bool isSwitchedOn, SyncSettings settings, ProgressKeeping keeping, bool testerMode)
        {
            if (!isSwitchedOn)
            {
                return CloudOff.SwitchedOff;
            }

            if (keeping == ProgressKeeping.ForThisRunOnly)
            {
                return CloudOff.ThisRunOnly;
            }

            if (testerMode)
            {
                return CloudOff.TesterMode;
            }

            return settings.Syncs ? (CloudOff?)null : CloudOff.TurnedOffRemotely;
        }

        /// <summary>
        /// Whether the trigger uploads at once, whatever the budget and whatever the cloud lacks: a payment, a painting or
        /// a room, a new sign-in, the switch turned on.
        /// </summary>
        public static bool IsOutsideTheBudget(this SyncTrigger trigger) => trigger switch
        {
            SyncTrigger.PurchaseDelivered => true,
            SyncTrigger.PaintingOrRoomCompleted => true,
            SyncTrigger.AccountChanged => true,
            SyncTrigger.SwitchedOn => true,
            SyncTrigger.ColdStart => false,
            SyncTrigger.Background => false,
            SyncTrigger.LongSession => false,
            SyncTrigger.LevelWon => false,
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "A trigger the sync does not know."),
        };

        /// <summary>
        /// Whether the trigger signs in anew, so that the cloud is read first and the backing off is not waited for: an
        /// account linked or gone over to, and the switch turned on again.
        /// </summary>
        public static bool IsANewSignIn(this SyncTrigger trigger) => trigger == SyncTrigger.AccountChanged || trigger == SyncTrigger.SwitchedOn;

        /// <summary>
        /// Whether a won level the cloud lacks lets the trigger upload at once, outside the budget (2026-10-27, T530): the
        /// session's first such win at the win itself; every win the cloud lacks on the way out, which ends the session;
        /// and at the next start what the way out could not send. Android cuts a game it puts away off the network within
        /// seconds and Unity stops its loop at once, so a won level left to the hourly budget reached the cloud only when
        /// the game came to the front again, or never. A session's later wins wait for its way out: a write a win would
        /// cost the free tier (docs/research/server-v1.md §2.4). Remote Config's switch lets every win wait for the budget
        /// again (<see cref="SyncSettings.WinsGoAtOnce"/>). The triggers outside the budget go at once anyway, and a long
        /// session's tick keeps to it.
        /// </summary>
        public static bool LetsAWinGoAtOnce(this SyncTrigger trigger, WinsInTheCloud wins, SyncSettings settings)
        {
            if (!settings.WinsGoAtOnce || wins == WinsInTheCloud.AllThere)
            {
                return false;
            }

            return trigger switch
            {
                SyncTrigger.LevelWon => wins == WinsInTheCloud.LacksTheSessionsFirst,
                SyncTrigger.Background => true,
                SyncTrigger.ColdStart => true,
                SyncTrigger.LongSession => false,
                SyncTrigger.PurchaseDelivered => false,
                SyncTrigger.PaintingOrRoomCompleted => false,
                SyncTrigger.AccountChanged => false,
                SyncTrigger.SwitchedOn => false,
                _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "A trigger the sync does not know."),
            };
        }

        /// <summary>
        /// Whether the sync reads the cloud before anything else: always for a new sign-in, an account never read, or a
        /// write whose answer was lost; at the game's start once the read budget's time has passed (or the clock went
        /// back before the last read); never otherwise, since a write that crosses another is refused and read then.
        /// An account whose sync stopped (<see cref="CloudState.Halt"/>) writes nothing, so it reads only at a new sign-in
        /// and at the game's start within the budget, to see whether the save reads again.
        /// </summary>
        public static bool ReadsFirst(SyncTrigger trigger, CloudState state, SyncSettings settings, DateTimeOffset now)
        {
            bool startsAndMayRead = trigger == SyncTrigger.ColdStart && IsDue(state.LastRead, settings.ReadEvery, now);
            if (trigger.IsANewSignIn() || state.Halt != null)
            {
                return trigger.IsANewSignIn() || startsAndMayRead;
            }

            return state.Base == null || state.Pending != null || startsAndMayRead;
        }

        /// <summary>
        /// Whether a merge reads the ledger of verified payments (the protected item "purchases", a read of the free tier's
        /// of its own) rather than the copy this phone keeps: only when it can have changed (Stage 4, docs/research/server-v1.md
        /// §2.4). That is when no copy was read for the account; when a payment was verified on this phone since (the copy is
        /// stale); when the progress names a payment the copy does not (one verified on another phone of the player); and once a
        /// day for a player with payments, whose refunds the server marks (or when the clock went back before the copy). A
        /// player with no payment, the most of them, reads it once.
        /// </summary>
        public static bool ReadsTheLedger(LedgerCopy? copy, bool namesAPaymentTheCopyLacks, bool copyHasPayments, DateTimeOffset now)
        {
            if (copy == null || copy.IsStale || namesAPaymentTheCopyLacks)
            {
                return true;
            }

            return copyHasPayments && IsDue(copy.ReadAt, LedgerReadEvery, now);
        }

        /// <summary>When an upload for this trigger, with these wins in the cloud, may go: nothing for now, else the instant the budget allows it.</summary>
        public static DateTimeOffset? UploadWaitsUntil(SyncTrigger trigger, WinsInTheCloud wins, CloudState state, SyncSettings settings, DateTimeOffset now) =>
            trigger.IsOutsideTheBudget() || trigger.LetsAWinGoAtOnce(wins, settings) || IsDue(state.LastWrite, settings.UploadEvery, now)
                ? (DateTimeOffset?)null
                : state.LastWrite + settings.UploadEvery;

        /// <summary>
        /// When to try again after the failures in a row so far: nothing for no network (nothing reached the cloud, and
        /// the next trigger finds the network back or not), else the next step of 1, 5, 30 minutes, 2, 6, 24 hours.
        /// </summary>
        public static DateTimeOffset? RetryAfter(CloudFailure failure, int failuresBefore, DateTimeOffset now) =>
            failure == CloudFailure.NoNetwork ? (DateTimeOffset?)null : now + Backoff[Math.Min(Math.Max(0, failuresBefore), Backoff.Length - 1)];

        /// <summary>Whether a sync waits for the backing off: every trigger but a new sign-in, which shows the cloud is there.</summary>
        public static bool WaitsForTheBackoff(SyncTrigger trigger, CloudState state, DateTimeOffset now) =>
            !trigger.IsANewSignIn() && state.RetryAt is DateTimeOffset retryAt && now < retryAt;

        /// <summary>True when as long as <paramref name="every"/> has passed since <paramref name="last"/>, it never was, or the clock went back before it.</summary>
        private static bool IsDue(DateTimeOffset? last, TimeSpan every, DateTimeOffset now) =>
            !(last is DateTimeOffset then) || now < then || now - then >= every;
    }
}
