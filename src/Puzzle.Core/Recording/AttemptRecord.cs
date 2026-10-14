using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <param name="actions">
        /// What the player did, in order (<see cref="Engine.Game.Actions"/>), for a replay (<see cref="AttemptReplay"/>);
        /// absent in the lab's records, whose moves replay them, and in records written before the list existed.
        /// </param>
        /// <exception cref="ArgumentException">A part is missing, or the attempt ends before it starts.</exception>
        public AttemptRecord(AttemptStart start, AttemptOutcome outcome, LabDetails? lab, IReadOnlyList<AttemptAction>? actions = null)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
            Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            if (outcome.EndedUtc < start.StartedUtc)
            {
                throw new ArgumentException($"The attempt ends ({outcome.EndedUtc:O}) before it starts ({start.StartedUtc:O}).", nameof(outcome));
            }

            Lab = lab;
            Actions = actions is null ? null : Array.AsReadOnly(CheckedActions(actions));
        }

        public AttemptStart Start { get; }

        public AttemptOutcome Outcome { get; }

        /// <summary>Web lab details; absent in records of the released game.</summary>
        public LabDetails? Lab { get; }

        /// <summary>
        /// What the player did, in order, for a replay; absent in the lab's records and in records written before the
        /// list existed, which then cannot be replayed with boosters or the extra moves.
        /// </summary>
        public IReadOnlyList<AttemptAction>? Actions { get; }

        private static AttemptAction[] CheckedActions(IReadOnlyList<AttemptAction> actions) => actions.All(action => action is not null)
            ? actions.ToArray()
            : throw new ArgumentException("An action cannot be null.", nameof(actions));
    }
}
