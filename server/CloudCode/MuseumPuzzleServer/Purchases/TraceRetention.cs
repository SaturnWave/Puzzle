using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.Platform;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>
    /// The end of the traces (the scheduled endpoint ForgetOldTraces, 04:00 UTC daily). A receipt whose last player
    /// deleted their data keeps its digest, product, date and state for 36 months (owner's decision 5, 2026-10-26;
    /// docs/research/server-v1.md §5.6), and then goes: each day the job reads the note of the receipts whose trace
    /// began 36 months before (and of the month before that, so a day the job did not run is caught up) and deletes
    /// every one that is still a trace. A receipt someone was given again since (the starter pack) is no trace and
    /// stays. The note of a day goes once its traces are gone; a run stopped at its time budget leaves it for the
    /// next day.
    /// </summary>
    public sealed class TraceRetention
    {
        public const int MonthsKept = 36;

        /// <summary>How many days before the last day due the job looks too: a month of days it may not have run.</summary>
        public const int DaysCaughtUp = 31;

        public static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

        private readonly IClock _clock;
        private readonly ILogger _log;

        public TraceRetention(IClock clock, ILogger log)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>The last day whose traces are due on <paramref name="today"/>: 36 months before it.</summary>
        public static DateOnly LastDueOn(DateOnly today) => today.AddMonths(-MonthsKept);

        public async Task<ChoreOutcome> RunAsync(IServerStore store, CancellationToken cancellation)
        {
            DateTimeOffset deadline = _clock.UtcNow + Budget;
            DateOnly lastDue = LastDueOn(DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime));
            int looked = 0;
            int deleted = 0;
            try
            {
                for (DateOnly day = lastDue.AddDays(-DaysCaughtUp); day <= lastDue; day = day.AddDays(1))
                {
                    IReadOnlyList<ReceiptId> noted = await store.TracesOfAsync(day, cancellation).ConfigureAwait(false);
                    foreach (ReceiptId receipt in noted)
                    {
                        looked++;
                        if (await DeleteIfStillATraceAsync(store, receipt, lastDue, cancellation).ConfigureAwait(false))
                        {
                            deleted++;
                        }

                        if (_clock.UtcNow >= deadline)
                        {
                            return new ChoreOutcome.Unfinished(looked, deleted);
                        }
                    }

                    if (noted.Count > 0)
                    {
                        await store.DeleteTracesOfAsync(day, cancellation).ConfigureAwait(false);
                    }
                }

                return new ChoreOutcome.Done(looked, deleted);
            }
            catch (ServerStoreUnavailableException unavailable)
            {
                _log.LogWarning(unavailable, "Cloud Save could not be reached while old traces were deleted ({Deleted} deleted).", deleted);
                return new ChoreOutcome.Unavailable(UnavailableReason.CloudSaveUnreachable);
            }
        }

        private async Task<bool> DeleteIfStillATraceAsync(IServerStore store, ReceiptId receipt, DateOnly lastDue, CancellationToken cancellation)
        {
            try
            {
                ReceiptRecord? record = await Keeping.ReadReceiptAsync(store, receipt, cancellation).ConfigureAwait(false);
                if (record == null || record.Claims.Count > 0 || !(record.ForgottenOn is DateOnly since) || since > lastDue)
                {
                    return false;
                }

                await store.DeleteReceiptAsync(receipt, cancellation).ConfigureAwait(false);
                return true;
            }
            catch (ServerDocumentException unreadable)
            {
                _log.LogError(unreadable, "The registry's record {Receipt} cannot be read; it was left.", receipt);
                return false;
            }
        }
    }
}
