using System.Collections.Generic;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>What the day's look at a player's Apple refunds came to (the endpoint CheckRefunds). A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class RefundOutcome
    {
        private RefundOutcome()
        {
        }

        /// <summary>Apple was asked; these payments were found refunded now and are marked so in the ledger (none, mostly).</summary>
        public sealed class Checked : RefundOutcome
        {
            public Checked(IReadOnlyList<LedgerNumber> refunded)
            {
                Refunded = refunded;
            }

            public IReadOnlyList<LedgerNumber> Refunded { get; }
        }

        /// <summary>Apple was asked less than a day ago: not asked again (the free tier's compute time, docs/research/server-v1.md §2.4).</summary>
        public sealed class NotDue : RefundOutcome
        {
        }

        /// <summary>The player has no valid App Store payment: nothing to ask.</summary>
        public sealed class NothingToCheck : RefundOutcome
        {
        }

        public sealed class Unavailable : RefundOutcome
        {
            public Unavailable(UnavailableReason reason)
            {
                Reason = reason;
            }

            public UnavailableReason Reason { get; }
        }
    }

    /// <summary>What deleting a player's data came to (the endpoint DeleteMyData). A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class DeletionOutcome
    {
        private DeletionOutcome()
        {
        }

        /// <summary>The player's Cloud Save data is gone and the registry no longer names the player: the game may delete the account now.</summary>
        public sealed class Deleted : DeletionOutcome
        {
        }

        /// <summary>Not done (or not wholly): the game says so and the player tries again; a second call finishes what the first began.</summary>
        public sealed class Unavailable : DeletionOutcome
        {
            public Unavailable(UnavailableReason reason)
            {
                Reason = reason;
            }

            public UnavailableReason Reason { get; }
        }
    }

    /// <summary>What a scheduled job came to, for Cloud Code's log. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class ChoreOutcome
    {
        private ChoreOutcome()
        {
        }

        /// <summary>Done: so many items looked at, so many changed.</summary>
        public sealed class Done : ChoreOutcome
        {
            public Done(int looked, int changed)
            {
                Looked = looked;
                Changed = changed;
            }

            public int Looked { get; }

            public int Changed { get; }
        }

        /// <summary>
        /// Stopped at the job's time budget (a Cloud Code call has fifteen seconds): what was done stays, and the next
        /// day's run takes the rest up again, as every step can be done twice without harm.
        /// </summary>
        public sealed class Unfinished : ChoreOutcome
        {
            public Unfinished(int looked, int changed)
            {
                Looked = looked;
                Changed = changed;
            }

            public int Looked { get; }

            public int Changed { get; }
        }

        public sealed class Unavailable : ChoreOutcome
        {
            public Unavailable(UnavailableReason reason)
            {
                Reason = reason;
            }

            public UnavailableReason Reason { get; }
        }

        /// <summary>A player called a job only the scheduler may run: nothing was done.</summary>
        public sealed class Refused : ChoreOutcome
        {
        }
    }
}
