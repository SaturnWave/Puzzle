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
        /// <param name="testerMode">
        /// True for an attempt the released game played in its tester mode, with coins and lives that have no end: such
        /// an attempt is the owner's testing, not a player's, and is left out of what records are counted for. False
        /// for every other attempt, and for every record written before the mark existed.
        /// </param>
        /// <param name="hintsShown">
        /// Hints the released game showed by itself during the attempt (a board at rest left untouched long enough): 0
        /// for an attempt that was shown none, for every record of the web lab, whose hints <see cref="LabDetails.HintsShown"/>
        /// counts, and for every record written before the count existed.
        /// </param>
        /// <param name="guidedMoves">
        /// Moves of the attempt that the released game's tutorial showed by hand before the player played them: 0 for an
        /// attempt with none, for every record of the web lab, and for every record written before the count existed.
        /// </param>
        /// <exception cref="ArgumentException">A part is missing, the attempt ends before it starts, or the hints or the guided moves are fewer than none.</exception>
        public AttemptRecord(
            AttemptStart start, AttemptOutcome outcome, LabDetails? lab, IReadOnlyList<AttemptAction>? actions = null, bool testerMode = false, int hintsShown = 0, int guidedMoves = 0)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
            Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            if (outcome.EndedUtc < start.StartedUtc)
            {
                throw new ArgumentException($"The attempt ends ({outcome.EndedUtc:O}) before it starts ({start.StartedUtc:O}).", nameof(outcome));
            }

            Lab = lab;
            Actions = actions is null ? null : Array.AsReadOnly(CheckedActions(actions));
            TesterMode = testerMode;
            HintsShown = hintsShown >= 0 ? hintsShown : throw new ArgumentOutOfRangeException(nameof(hintsShown), hintsShown, "An attempt shows none or more hints.");
            GuidedMoves = guidedMoves >= 0 ? guidedMoves : throw new ArgumentOutOfRangeException(nameof(guidedMoves), guidedMoves, "An attempt has none or more guided moves.");
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

        /// <summary>True when the released game played the attempt in its tester mode, with coins and lives that have no end.</summary>
        public bool TesterMode { get; }

        /// <summary>Hints the released game showed by itself during the attempt; 0 for none, and in every record of the web lab.</summary>
        public int HintsShown { get; }

        /// <summary>Moves the released game's tutorial showed by hand and the player then played; 0 for none, and in every record of the web lab.</summary>
        public int GuidedMoves { get; }

        private static AttemptAction[] CheckedActions(IReadOnlyList<AttemptAction> actions) => actions.All(action => action is not null)
            ? actions.ToArray()
            : throw new ArgumentException("An action cannot be null.", nameof(actions));
    }
}
