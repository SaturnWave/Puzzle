#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// The Cloud Save's version token of an item (its "write lock", docs/research/server-v1.md §3.2): handed out with
    /// every read and write, and given back with the next write, which the cloud refuses if anyone wrote in between.
    /// </summary>
    public readonly struct CloudWriteLock : IEquatable<CloudWriteLock>
    {
        /// <exception cref="ArgumentException">An empty token: the cloud never hands one out.</exception>
        public CloudWriteLock(string value)
        {
            Value = string.IsNullOrEmpty(value) ? throw new ArgumentException("A write lock is not empty.", nameof(value)) : value;
        }

        public string Value { get; }

        public bool Equals(CloudWriteLock other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is CloudWriteLock other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value;

        public static bool operator ==(CloudWriteLock left, CloudWriteLock right) => left.Equals(right);

        public static bool operator !=(CloudWriteLock left, CloudWriteLock right) => !left.Equals(right);
    }

    /// <summary>An item the cloud holds: its text, and its write lock as of the read.</summary>
    public sealed class CloudItem
    {
        /// <exception cref="ArgumentException">No text.</exception>
        public CloudItem(string text, CloudWriteLock writeLock)
        {
            Text = string.IsNullOrEmpty(text) ? throw new ArgumentException("An item the cloud holds has a text.", nameof(text)) : text;
            WriteLock = writeLock;
        }

        public string Text { get; }

        public CloudWriteLock WriteLock { get; }
    }

    /// <summary>
    /// Why the cloud could not be reached or would not answer. A closed set. Every kind leaves the phone's progress as
    /// it is and is never shown to the player as an error that stops the game (§3.8): only the settings' status line
    /// says the cloud is out of reach.
    /// </summary>
    public enum CloudFailure
    {
        /// <summary>The phone has no network: nothing reached the cloud, so trying again costs the cloud nothing.</summary>
        NoNetwork,

        /// <summary>The cloud did not answer in time, or answered with a fault of its own (5xx), or with something the adapter did not expect.</summary>
        Unavailable,

        /// <summary>
        /// The cloud refuses the project: its services closed (the free tier's limit passed, docs/research/server-v1.md
        /// §1.3), a project not linked, or a player not let in. Unity documents no code for the closed services, so
        /// whatever refuses the project lands here.
        /// </summary>
        Blocked,

        /// <summary>The cloud asks the player to slow down (429).</summary>
        RateLimited,

        /// <summary>The cloud refused the request itself: too large, or a key it does not take. A fault of this game, kept apart in the log.</summary>
        Refused,
    }

    /// <summary>What reading the player's items came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class CloudRead
    {
        private CloudRead()
        {
        }

        /// <summary>The cloud answered: the save and the ledger of verified payments, each nothing when the player has none yet.</summary>
        public sealed class Found : CloudRead
        {
            public Found(CloudItem? save, CloudItem? purchases)
            {
                Save = save;
                Purchases = purchases;
            }

            /// <summary>The Player Data item "save" (default class); nothing for a player who never saved.</summary>
            public CloudItem? Save { get; }

            /// <summary>The Player Data item "purchases" (protected class, written by the server only); nothing for a player with no verified payment.</summary>
            public CloudItem? Purchases { get; }
        }

        public sealed class Failed : CloudRead
        {
            public Failed(CloudFailure failure)
            {
                Failure = failure;
            }

            public CloudFailure Failure { get; }
        }
    }

    /// <summary>What writing the save came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class CloudWrite
    {
        private CloudWrite()
        {
        }

        /// <summary>Written; the item's new write lock.</summary>
        public sealed class Written : CloudWrite
        {
            public Written(CloudWriteLock writeLock)
            {
                WriteLock = writeLock;
            }

            public CloudWriteLock WriteLock { get; }
        }

        /// <summary>Not written: someone wrote the save since the lock that was given (409). Nothing was changed.</summary>
        public sealed class Conflict : CloudWrite
        {
        }

        /// <summary>
        /// No answer that says what happened: the save may or may not have been written (a write whose answer was lost
        /// in a timeout lands all the same). Cloud/CloudSync finds out at the next read.
        /// </summary>
        public sealed class Failed : CloudWrite
        {
            public Failed(CloudFailure failure)
            {
                Failure = failure;
            }

            public CloudFailure Failure { get; }
        }
    }

    /// <summary>
    /// The player's items in the cloud (Unity's Cloud Save, docs/research/server-v1.md §3.1–§3.2), as the sync
    /// needs them: one read of the save and the ledger, one write of the save. It is a seam: the game's adapter
    /// talks to Unity's Cloud Save once the player is signed in, the tests and the Editor's checks use a cloud in
    /// memory (<see cref="FakeCloudSave"/>). An adapter turns every exception of the service into a
    /// <see cref="CloudFailure"/> at this edge: nothing of the service's own errors goes further, and none reaches a
    /// player.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public interface ICloudSave
    {
        /// <summary>The save and the ledger of verified payments, with their write locks.</summary>
        Task<CloudRead> ReadAsync(CancellationToken cancel);

        /// <summary>
        /// Writes the save if the cloud's item still has <paramref name="expected"/> as its lock. With no lock (the cloud
        /// had no save at the last read) it writes whatever is there: the cloud offers no "only if absent", so the read
        /// just before is what keeps two first saves from crossing.
        /// </summary>
        Task<CloudWrite> WriteSaveAsync(string text, CloudWriteLock? expected, CancellationToken cancel);
    }
}
