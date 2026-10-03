using System;

namespace Puzzle.Core.Recording
{
    /// <summary>
    /// One played attempt of a level, in the form both the web test lab and the released game record it
    /// (docs/GAME_DESIGN.md §20). The only identity is a random device id. Serialization lives outside the engine:
    /// Puzzle.Tooling's <c>AttemptRecordJson</c> writes it as one JSON line.
    /// </summary>
    public sealed class AttemptRecord
    {
        /// <summary>Version of the record format. Readers accept every version up to this one; changes are additive.</summary>
        public const int SchemaVersion = 1;

        /// <param name="lab">Web lab details; absent in records of the released game.</param>
        /// <exception cref="ArgumentException">A part is missing, or the attempt ends before it starts.</exception>
        public AttemptRecord(AttemptStart start, AttemptOutcome outcome, LabDetails? lab)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
            Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            if (outcome.EndedUtc < start.StartedUtc)
            {
                throw new ArgumentException($"The attempt ends ({outcome.EndedUtc:O}) before it starts ({start.StartedUtc:O}).", nameof(outcome));
            }

            Lab = lab;
        }

        public AttemptStart Start { get; }

        public AttemptOutcome Outcome { get; }

        /// <summary>Web lab details; absent in records of the released game.</summary>
        public LabDetails? Lab { get; }
    }
}
