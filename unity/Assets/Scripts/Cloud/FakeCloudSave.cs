#nullable enable
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// A cloud in memory, as Unity's Cloud Save behaves for one player (docs/research/server-v1.md §3.2): a save with a
    /// write lock that changes with every write and refuses a write that gives an old one, and the server's ledger,
    /// which only <see cref="LedgerWrittenByTheServer"/> writes. It can be out of reach (no network, down, closed for
    /// the free tier, slowing the player down), and it can lose the answer to a write that it did make. Several
    /// devices of one player share one. For the tests and the Editor's checks: nothing here talks to a network.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class FakeCloudSave : ICloudSave
    {
        private int _locksHandedOut;
        private bool _losesTheNextAnswer;

        /// <summary>The save as the cloud holds it; nothing before the first write.</summary>
        public CloudItem? Save { get; private set; }

        /// <summary>The ledger as the server wrote it; nothing before the first verified payment.</summary>
        public CloudItem? Purchases { get; private set; }

        /// <summary>Why the cloud is out of reach; nothing while it answers.</summary>
        public CloudFailure? Down { get; private set; }

        /// <summary>The reads that reached the cloud, of the save and of the ledger: what the free tier counts.</summary>
        public int Reads => SaveReads + LedgerReads;

        /// <summary>The reads of the save that reached the cloud.</summary>
        public int SaveReads { get; private set; }

        /// <summary>The reads of the ledger that reached the cloud.</summary>
        public int LedgerReads { get; private set; }

        /// <summary>The writes that reached the cloud, written or refused: what the free tier counts.</summary>
        public int Writes { get; private set; }

        /// <summary>The cloud is out of reach for this reason until <see cref="ComesBack"/>.</summary>
        public void GoesDown(CloudFailure failure) => Down = failure;

        public void ComesBack() => Down = null;

        /// <summary>The next write lands, and its answer is lost on the way (a timeout): the device hears a failure.</summary>
        public void LosesTheNextAnswer() => _losesTheNextAnswer = true;

        /// <summary>The game's server wrote the player's ledger (the Protected class: the player cannot write it).</summary>
        public void LedgerWrittenByTheServer(string text) => Purchases = new CloudItem(text, NextLock());

        /// <summary>Someone else's write of the save, outside every lock: a save of an earlier test, or one a test makes damaged.</summary>
        public void SaveWrittenOutsideTheGame(string text) => Save = new CloudItem(text, NextLock());

        /// <summary>The player's data was deleted (DeleteMyData): the save and the ledger are gone.</summary>
        public void Deleted()
        {
            Save = null;
            Purchases = null;
        }

        public Task<CloudRead> ReadSaveAsync(CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if (Down is CloudFailure failure)
            {
                return Task.FromResult<CloudRead>(new CloudRead.Failed(failure));
            }

            SaveReads++;
            return Task.FromResult<CloudRead>(new CloudRead.Found(Save));
        }

        public Task<CloudRead> ReadLedgerAsync(CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if (Down is CloudFailure failure)
            {
                return Task.FromResult<CloudRead>(new CloudRead.Failed(failure));
            }

            LedgerReads++;
            return Task.FromResult<CloudRead>(new CloudRead.Found(Purchases));
        }

        public Task<CloudWrite> WriteSaveAsync(string text, CloudWriteLock? expected, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if (Down is CloudFailure failure)
            {
                return Task.FromResult<CloudWrite>(new CloudWrite.Failed(failure));
            }

            Writes++;
            bool isTheLockOfTheSave = expected is CloudWriteLock given ? Save != null && Save.WriteLock == given : true;
            if (!isTheLockOfTheSave)
            {
                return Task.FromResult<CloudWrite>(new CloudWrite.Conflict());
            }

            Save = new CloudItem(text, NextLock());
            if (_losesTheNextAnswer)
            {
                _losesTheNextAnswer = false;
                return Task.FromResult<CloudWrite>(new CloudWrite.Failed(CloudFailure.Unavailable));
            }

            return Task.FromResult<CloudWrite>(new CloudWrite.Written(Save.WriteLock));
        }

        private CloudWriteLock NextLock() => new CloudWriteLock("lock-" + (++_locksHandedOut).ToString(CultureInfo.InvariantCulture));
    }
}
