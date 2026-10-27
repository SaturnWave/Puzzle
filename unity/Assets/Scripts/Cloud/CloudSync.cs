#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// Whoever holds the player's progress while the game runs (the game's session): the sync reads the progress from
    /// it and hands back the merged one, which it takes as the game's own and saves to the progress file at once.
    /// </summary>
    public interface IProgressHolder
    {
        /// <summary>The progress the game holds now.</summary>
        PlayerProgress Current { get; }

        /// <summary>Whether the progress store keeps what it saves after the game is closed; a run that keeps it for itself only does not sync.</summary>
        ProgressKeeping Keeping { get; }

        /// <summary>True in the tester mode, whose unlimited coins are never the cloud's.</summary>
        bool IsTesterMode { get; }

        /// <summary>The game's progress is this one from now on: keep it, and save it to the progress file at once.</summary>
        void Adopt(PlayerProgress progress);
    }

    /// <summary>What an upload came to in a sync that ran. A closed set.</summary>
    public enum UploadResult
    {
        /// <summary>The phone's progress is the cloud's: nothing to write.</summary>
        NotNeeded,

        /// <summary>No level is won yet: no save is written for such a player (an installation tried and left costs the cloud nothing).</summary>
        NoLevelWonYet,

        /// <summary>The budget's time has not passed since the last upload; a later trigger uploads.</summary>
        WaitsForTheBudget,

        /// <summary>Written.</summary>
        Written,

        /// <summary>The cloud refused the write twice for another device's write in between: the next trigger tries again.</summary>
        Contended,
    }

    /// <summary>What a sync came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class SyncOutcome
    {
        private SyncOutcome()
        {
        }

        /// <summary>The sync does not run at all, and nothing reached the cloud.</summary>
        public sealed class Off : SyncOutcome
        {
            public Off(CloudOff why)
            {
                Why = why;
            }

            public CloudOff Why { get; }

            public override string ToString() => $"off: {Why}";
        }

        /// <summary>Another sync was running: this one runs after it.</summary>
        public sealed class Queued : SyncOutcome
        {
            public override string ToString() => "queued behind the sync that runs";
        }

        /// <summary>The cloud failed lately: nothing is tried before <see cref="Until"/>.</summary>
        public sealed class Postponed : SyncOutcome
        {
            public Postponed(DateTimeOffset until)
            {
                Until = until;
            }

            public DateTimeOffset Until { get; }

            public override string ToString() => $"postponed until {Until:u}";
        }

        /// <summary>The cloud could not be reached; the phone's progress stands as it is.</summary>
        public sealed class Unreachable : SyncOutcome
        {
            public Unreachable(CloudFailure failure, DateTimeOffset? retryAt)
            {
                Failure = failure;
                RetryAt = retryAt;
            }

            public CloudFailure Failure { get; }

            /// <summary>When the next try may go; nothing when the next trigger may (no network).</summary>
            public DateTimeOffset? RetryAt { get; }

            public override string ToString() => $"unreachable: {Failure}";
        }

        /// <summary>The sync stopped for the account: nothing is written over the cloud's save.</summary>
        public sealed class Halted : SyncOutcome
        {
            public Halted(CloudHalt halt, PlayerProgress? understood)
            {
                Halt = halt ?? throw new ArgumentNullException(nameof(halt));
                Understood = understood;
            }

            public CloudHalt Halt { get; }

            /// <summary>Where the player stands in a later game's save, as far as this game reads it, for the settings to show; nothing otherwise.</summary>
            public PlayerProgress? Understood { get; }

            public override string ToString() => $"halted: {Halt}";
        }

        /// <summary>Payments both sides delivered could not be named, so nothing was merged or written (Cloud/ProgressMerge).</summary>
        public sealed class Unaccounted : SyncOutcome
        {
            public Unaccounted(IReadOnlyList<LedgerNumber> payments)
            {
                Payments = payments ?? throw new ArgumentNullException(nameof(payments));
            }

            public IReadOnlyList<LedgerNumber> Payments { get; }

            public override string ToString() => $"unaccounted payments: {string.Join(", ", Payments)}";
        }

        /// <summary>The sync ran.</summary>
        public sealed class Done : SyncOutcome
        {
            public Done(bool read, bool adopted, UploadResult upload, DateTimeOffset? uploadWaitsUntil, IReadOnlyList<string> notes)
            {
                Read = read;
                Adopted = adopted;
                Upload = upload;
                UploadWaitsUntil = uploadWaitsUntil;
                Notes = notes ?? throw new ArgumentNullException(nameof(notes));
            }

            /// <summary>Whether the cloud was read.</summary>
            public bool Read { get; }

            /// <summary>Whether the game's progress changed by the cloud's (the "your save came back" card shows on a new phone).</summary>
            public bool Adopted { get; }

            public UploadResult Upload { get; }

            /// <summary>When the budget lets the waiting upload go; nothing unless it waits.</summary>
            public DateTimeOffset? UploadWaitsUntil { get; }

            /// <summary>For the log: what was counted once in a merge, lines of the save or the ledger set aside.</summary>
            public IReadOnlyList<string> Notes { get; }

            public override string ToString() => $"done: read {Read}, adopted {Adopted}, upload {Upload}";
        }
    }

    /// <summary>
    /// The sync of the player's progress with the cloud (docs/research/server-v1.md §3): offline first, the phone's
    /// progress file the truth. A trigger (<see cref="SyncTrigger"/>) asks for a sync; the sync reads the save and the
    /// ledger when the budget or the state asks for it, merges the cloud's progress into the phone's
    /// (<see cref="ProgressMerge"/>), hands the merged progress to the game, and writes the save when the phone has
    /// something new and the budget lets it. A write gives back the lock of the save last read; when another device wrote
    /// in between, the cloud refuses, and the sync reads, merges and writes once more, never more (the next trigger
    /// tries again). Each write carries a random id and is kept in the state before it is sent, so a write whose answer
    /// was lost is found again at the next read and never counted twice.
    /// <para>
    /// When the cloud cannot be reached (no network, down, closed for the free tier) the game plays on exactly as
    /// without it: the progress file stands, nothing waits for the cloud, the next tries back off (1, 5, 30 minutes,
    /// 2, 6, 24 hours). A save the game cannot read, or a later game's, stops the writes for the account; reads go on
    /// within the budget, and stop the halt once the save reads again. No save is written for a player who has won no
    /// level. The switch, Remote Config, a run that cannot keep its progress and the tester mode keep the sync off.
    /// </para>
    /// Nothing here is Unity's: the cloud, the state's file, the progress and the clock come in through seams, and the
    /// tests run every case with <see cref="FakeCloudSave"/>.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class CloudSync
    {
        private readonly ICloudSave _cloud;
        private readonly ICloudStateStore _store;
        private readonly IProgressHolder _holder;
        private readonly ProgressMerge _merge;
        private readonly Wallet _startingWallet;
        private readonly IDocumentJson _json;
        private readonly IWallClock _clock;
        private readonly string _appVersion;
        private readonly Func<string> _newId;

        private bool _isRunning;
        private SyncTrigger? _runNext;
        private CloudPlayerId _runNextFor;

        /// <param name="appVersion">The game's version ("1.1.0"), written into each save for support.</param>
        /// <param name="newId">Where the random ids of the writes come from (<see cref="RandomIds.New"/>).</param>
        /// <exception cref="System.IO.IOException">The state's file is there but cannot be read.</exception>
        public CloudSync(
            ICloudSave cloud, ICloudStateStore store, IProgressHolder holder, EconomyRules rules, IDocumentJson json, IWallClock clock, string appVersion, Func<string> newId)
        {
            _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _holder = holder ?? throw new ArgumentNullException(nameof(holder));
            _merge = new ProgressMerge(rules ?? throw new ArgumentNullException(nameof(rules)));
            _startingWallet = rules.StartingWallet;
            _json = json ?? throw new ArgumentNullException(nameof(json));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _appVersion = string.IsNullOrEmpty(appVersion) ? throw new ArgumentException("The game has a version.", nameof(appVersion)) : appVersion;
            _newId = newId ?? throw new ArgumentNullException(nameof(newId));
            CloudStateLoad load = _store.Load();
            State = load.State;
            StateWasDamaged = load.WasDamaged;
        }

        /// <summary>The sync's state as it stands: the switch, the account, the last read and write, the backing off.</summary>
        public CloudState State { get; private set; }

        /// <summary>True when cloud.json did not read at the start: it was set aside, and the switch is off until the player turns it on.</summary>
        public bool StateWasDamaged { get; }

        /// <summary>The time on the sync's clock now: what the settings tell the last sync by.</summary>
        public DateTimeOffset Now => _clock.Now;

        /// <summary>What Remote Config says, or the defaults until it has said anything.</summary>
        public SyncSettings Settings { get; set; } = SyncSettings.Default;

        /// <summary>The player turned the settings' "Cloud save" switch; it is kept at once.</summary>
        /// <exception cref="System.IO.IOException">The state's file could not be written; the switch is turned for this run.</exception>
        public void SwitchTo(bool isOn) => Keep(State.SwitchedTo(isOn));

        /// <summary>The offer to link the account to Google Play Games or Game Center was made: it is kept, and not made again.</summary>
        /// <exception cref="System.IO.IOException">The state's file could not be written; the offer is not made again this run.</exception>
        public void KeepTheLinkOffered() => Keep(State.WithTheLinkOffered());

        /// <summary>The game's server verified a payment on this phone, so it wrote the ledger: the next merge reads the ledger anew.</summary>
        /// <exception cref="System.IO.IOException">The state's file could not be written; the copy is known stale for this run.</exception>
        public void KnowTheLedgerChanged() => Keep(State.WithTheLedgerStale());

        /// <summary>
        /// The player's cloud data was deleted ("Delete my data", §5.6): nothing of the account is kept, the switch is
        /// off, and a new writer id stands for the installation if the player turns it on again (a new account).
        /// </summary>
        /// <exception cref="System.IO.IOException">The state's file could not be written.</exception>
        public void Forget() => Keep(CloudState.Forgotten(_newId()));

        /// <summary>
        /// Syncs for the account signed in, as the trigger asks. While another sync runs, this one waits behind it
        /// (one waits at most: the one that asks for more takes the place).
        /// </summary>
        /// <exception cref="System.IO.IOException">The state's file could not be written. A write of the save is never sent before its id is kept.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not write the state's file.</exception>
        public async Task<SyncOutcome> SyncAsync(CloudPlayerId player, SyncTrigger trigger, CancellationToken cancel)
        {
            if (_isRunning)
            {
                _runNext = _runNext is SyncTrigger waiting && Weight(waiting) >= Weight(trigger) ? waiting : trigger;
                _runNextFor = player;
                return new SyncOutcome.Queued();
            }

            _isRunning = true;
            try
            {
                SyncOutcome outcome = await RunAsync(player, trigger, cancel);
                while (_runNext is SyncTrigger next)
                {
                    _runNext = null;
                    outcome = await RunAsync(_runNextFor, next, cancel);
                }

                return outcome;
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Task<SyncOutcome> RunAsync(CloudPlayerId player, SyncTrigger trigger, CancellationToken cancel)
        {
            if (SyncPolicy.Gate(State.IsSwitchedOn, Settings, _holder.Keeping, _holder.IsTesterMode) is CloudOff off)
            {
                return new SyncOutcome.Off(off);
            }

            if (State.Player != player)
            {
                Keep(State.ForPlayer(player));
            }

            DateTimeOffset now = _clock.Now;
            if (SyncPolicy.WaitsForTheBackoff(trigger, State, now))
            {
                return new SyncOutcome.Postponed(State.RetryAt ?? now);
            }

            bool theGameWasUpdated = State.Halt is CloudHalt.LaterGame later && later.ProgressLayout <= ProgressDocument.CurrentVersion;
            bool reads = SyncPolicy.ReadsFirst(trigger, State, Settings, now) || theGameWasUpdated;
            if (State.Halt is CloudHalt halt && !reads)
            {
                return new SyncOutcome.Halted(halt, understood: null);
            }

            var notes = new List<string>();
            bool adopted = false;
            if (reads)
            {
                ReadStep step = await ReadAndMergeAsync(now, notes, cancel);
                if (step.Ended is SyncOutcome ended)
                {
                    return ended;
                }

                adopted = step.Adopted;
            }

            return await UploadAsync(trigger, now, reads, adopted, notes, cancel);
        }

        /// <summary>Reads the save, and the ledger when it can have changed, and merges the cloud's progress into the game's.</summary>
        private async Task<ReadStep> ReadAndMergeAsync(DateTimeOffset now, List<string> notes, CancellationToken cancel)
        {
            CloudRead read = await _cloud.ReadSaveAsync(cancel);
            if (read is CloudRead.Failed failed)
            {
                return ReadStep.Ending(Failure(failed.Failure, now));
            }

            if (!(((CloudRead.Found)read).Item is CloudItem saved))
            {
                // Never written, or deleted: the phone's progress stands, and the next write starts the save anew.
                Keep(State.AfterRead(CloudBase.NoSave(_startingWallet), now));
                return ReadStep.Going(adopted: false);
            }

            switch (CloudEnvelope.Read(saved.Text, _json, _startingWallet))
            {
                case CloudSaveReading.Sound sound:
                    notes.AddRange(sound.SetAside.Select(line => $"set aside in the cloud's save: {line}"));
                    LedgerStep ledger = await LedgerForTheMergeAsync(sound.Save.Progress, now, notes, cancel);
                    return ledger.Ended is SyncOutcome ended ? ReadStep.Ending(ended) : Merged(sound.Save, saved.WriteLock, ledger.Ledger, now, notes);
                case CloudSaveReading.OfALaterGame later:
                    CloudHalt halt = new CloudHalt.LaterGame(Math.Max(later.ProgressLayout, ProgressDocument.CurrentVersion + 1));
                    Keep(State.Halted(halt, now));
                    return ReadStep.Ending(new SyncOutcome.Halted(halt, later.Understood));
                case CloudSaveReading.Damaged damaged:
                    CloudHalt damage = new CloudHalt.DamagedSave(damaged.Why);
                    Keep(State.Halted(damage, now));
                    return ReadStep.Ending(new SyncOutcome.Halted(damage, understood: null));
                default:
                    throw new InvalidOperationException("Reading the cloud's save ended in a way the sync does not know.");
            }
        }

        /// <summary>
        /// The cloud's save merged into the game's progress. The base is the save as last synced; when a write whose
        /// answer was lost is among the save's writes, it landed, and what it wrote is the base instead.
        /// </summary>
        private ReadStep Merged(CloudSave cloud, CloudWriteLock writeLock, PurchaseLedger ledger, DateTimeOffset now, List<string> notes)
        {
            PlayerProgress mergeBase = State.Pending is PendingWrite pending && cloud.Writes.Contains(pending.Id, StringComparer.Ordinal)
                ? pending.Progress
                : (State.Base ?? CloudBase.NoSave(_startingWallet)).Progress;
            PlayerProgress local = _holder.Current;
            switch (_merge.Merge(mergeBase, local, cloud.Progress, ledger))
            {
                case MergeOutcome.Merged merged:
                    notes.AddRange(merged.CountedOnce.Select(line => $"counted once: {line}"));
                    bool adopts = !merged.Progress.Equals(local);
                    if (adopts)
                    {
                        _holder.Adopt(merged.Progress);
                    }

                    Keep(State.AfterRead(CloudBase.Of(cloud, writeLock), now));
                    return ReadStep.Going(adopts);
                case MergeOutcome.Unaccounted unaccounted:
                    return ReadStep.Ending(new SyncOutcome.Unaccounted(unaccounted.Payments));
                default:
                    throw new InvalidOperationException("A merge ended in a way the sync does not know.");
            }
        }

        /// <summary>Writes the game's progress when it differs from the cloud's and the budget lets it; a write the cloud refuses for another's is read, merged and tried once more.</summary>
        private async Task<SyncOutcome> UploadAsync(SyncTrigger trigger, DateTimeOffset now, bool read, bool adopted, List<string> notes, CancellationToken cancel)
        {
            const int Tries = 2;
            for (int attempt = 1; ; attempt++)
            {
                CloudBase synced = State.Base ?? throw new InvalidOperationException("The sync uploads only after the account's cloud was read.");
                PlayerProgress current = _holder.Current;
                if (current.Equals(synced.Progress))
                {
                    return new SyncOutcome.Done(read, adopted, UploadResult.NotNeeded, null, notes);
                }

                if (current.LevelsWon < 1)
                {
                    return new SyncOutcome.Done(read, adopted, UploadResult.NoLevelWonYet, null, notes);
                }

                if (attempt == 1 && SyncPolicy.UploadWaitsUntil(trigger, State, Settings, now) is DateTimeOffset until)
                {
                    return new SyncOutcome.Done(read, adopted, UploadResult.WaitsForTheBudget, until, notes);
                }

                string id = _newId();
                var save = new CloudSave(current, _appVersion, synced.Revision + 1, now, State.Writer, synced.Writes.Append(id));
                Keep(State.Sending(new PendingWrite(id, current, save.Revision, save.Writes)));
                CloudWrite answer = await _cloud.WriteSaveAsync(CloudEnvelope.Write(save, _json), synced.WriteLock, cancel);
                switch (answer)
                {
                    case CloudWrite.Written written:
                        Keep(State.AfterWrite(new CloudBase(current, written.WriteLock, save.Revision, save.Writes), now));
                        return new SyncOutcome.Done(read, adopted, UploadResult.Written, null, notes);
                    case CloudWrite.Failed failed:
                        return Failure(failed.Failure, now);
                    case CloudWrite.Conflict _ when attempt == Tries:
                        Keep(State.NotWritten());
                        return new SyncOutcome.Done(read, adopted, UploadResult.Contended, null, notes);
                    case CloudWrite.Conflict _:
                        Keep(State.NotWritten());
                        ReadStep step = await ReadAndMergeAsync(now, notes, cancel);
                        if (step.Ended is SyncOutcome ended)
                        {
                            return ended;
                        }

                        read = true;
                        adopted |= step.Adopted;
                        break;
                    default:
                        throw new InvalidOperationException("A write ended in a way the sync does not know.");
                }
            }
        }

        /// <summary>
        /// The ledger the merge names payments by: the copy this phone keeps, or the cloud's, read when it can have changed
        /// (<see cref="SyncPolicy.ReadsTheLedger"/>) and kept as the copy from then on.
        /// </summary>
        private async Task<LedgerStep> LedgerForTheMergeAsync(PlayerProgress cloud, DateTimeOffset now, List<string> notes, CancellationToken cancel)
        {
            if (State.Ledger is LedgerCopy kept)
            {
                PurchaseLedger copy = LedgerOf(kept.Text, notes: null);
                LedgerNumbers named = _holder.Current.Purchases.LedgerDelivered.Union(cloud.Purchases.LedgerDelivered);
                bool lacksOne = named.Count > copy.Entries.Count || named.Numbers.Any(number => copy.Find(number) == null);
                if (!SyncPolicy.ReadsTheLedger(kept, lacksOne, copyHasPayments: copy.Entries.Count > 0, now))
                {
                    return LedgerStep.Going(LedgerOf(kept.Text, notes));
                }
            }

            CloudRead read = await _cloud.ReadLedgerAsync(cancel);
            if (read is CloudRead.Failed failed)
            {
                return LedgerStep.Ending(Failure(failed.Failure, now));
            }

            string? text = ((CloudRead.Found)read).Item?.Text;
            Keep(State.WithLedger(new LedgerCopy(text, now, isStale: false)));
            return LedgerStep.Going(LedgerOf(text, notes));
        }

        /// <summary>A ledger the server wrote, its lines that do not read noted (when notes are kept) and set aside; none when the player has no ledger.</summary>
        private PurchaseLedger LedgerOf(string? text, List<string>? notes)
        {
            if (text == null)
            {
                return PurchaseLedger.None;
            }

            PurchaseLedgerReading reading = PurchaseLedger.Read(_json.Read<PurchaseLedgerDocument>(text));
            notes?.AddRange(reading.SetAside.Select(line => $"set aside in the ledger: {line}"));
            return reading.Ledger;
        }

        /// <summary>The cloud did not answer: the failure is counted, and the next try backs off.</summary>
        private SyncOutcome.Unreachable Failure(CloudFailure failure, DateTimeOffset now)
        {
            DateTimeOffset? retryAt = SyncPolicy.RetryAfter(failure, State.Failures, now);
            Keep(State.AfterFailure(retryAt));
            return new SyncOutcome.Unreachable(failure, retryAt);
        }

        /// <summary>The state is this from now on, in memory and in its file.</summary>
        private void Keep(CloudState state)
        {
            State = state;
            _store.Save(state);
        }

        /// <summary>
        /// How much a trigger asks of a sync, so that of two waiting the one that asks more runs: a new sign-in most, then
        /// an upload outside the budget (a payment must not wait for the budget behind the start's read, which the sync
        /// that ran has as good as done), then the start, then the budget's uploads.
        /// </summary>
        private static int Weight(SyncTrigger trigger) =>
            trigger == SyncTrigger.AccountChanged ? 3 : trigger.IsOutsideTheBudget() ? 2 : trigger == SyncTrigger.ColdStart ? 1 : 0;

        /// <summary>How finding the ledger for a merge went: the ledger, or ended with an outcome (the cloud did not answer).</summary>
        private sealed class LedgerStep
        {
            private LedgerStep(SyncOutcome? ended, PurchaseLedger ledger)
            {
                Ended = ended;
                Ledger = ledger;
            }

            public SyncOutcome? Ended { get; }

            public PurchaseLedger Ledger { get; }

            public static LedgerStep Ending(SyncOutcome outcome) => new LedgerStep(outcome, PurchaseLedger.None);

            public static LedgerStep Going(PurchaseLedger ledger) => new LedgerStep(null, ledger);
        }

        /// <summary>How a read went: on to the upload (and whether the game's progress changed), or ended with an outcome.</summary>
        private sealed class ReadStep
        {
            private ReadStep(SyncOutcome? ended, bool adopted)
            {
                Ended = ended;
                Adopted = adopted;
            }

            public SyncOutcome? Ended { get; }

            public bool Adopted { get; }

            public static ReadStep Ending(SyncOutcome outcome) => new ReadStep(outcome, adopted: false);

            public static ReadStep Going(bool adopted) => new ReadStep(null, adopted);
        }
    }
}
