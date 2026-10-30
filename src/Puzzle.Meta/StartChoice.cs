using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;

namespace Puzzle.Meta
{
    /// <summary>
    /// The start boosters a player has picked for one attempt, each kind at most once. Picking spends nothing: the
    /// boosters leave the shelf when the attempt counts (<see cref="TakenFrom"/>), and nothing is ever picked for
    /// the player.
    /// </summary>
    public sealed class StartChoice : IEquatable<StartChoice>
    {
        /// <summary>The picked kinds, in the engine's order and without a repeat.</summary>
        private readonly BoosterKind[] _boosters;

        private StartChoice(BoosterKind[] boosters)
        {
            _boosters = boosters;
        }

        /// <summary>Nothing picked: how every attempt starts.</summary>
        public static StartChoice None { get; } = new StartChoice(Array.Empty<BoosterKind>());

        /// <summary>The picked boosters, in the engine's order.</summary>
        public IReadOnlyList<BoosterKind> Boosters => _boosters;

        public int Count => _boosters.Length;

        public bool Has(BoosterKind booster) => Array.IndexOf(_boosters, booster) >= 0;

        /// <summary>The choice as the engine's game options take it.</summary>
        public IReadOnlyList<StartBooster> ForTheEngine() => _boosters.Select(BoosterKinds.AsStartBooster).ToArray();

        /// <summary>The shelf once the attempt counts: every picked booster has left it.</summary>
        /// <exception cref="InvalidOperationException">A picked booster is not on the shelf.</exception>
        public BoosterShelf TakenFrom(BoosterShelf owned)
        {
            BoosterShelf left = owned;
            foreach (BoosterKind booster in _boosters)
            {
                left = left.WithOneFewer(booster);
            }

            return left;
        }

        internal StartChoice With(BoosterKind booster) => new StartChoice(_boosters.Append(booster).OrderBy(picked => picked).ToArray());

        internal StartChoice Without(BoosterKind booster) => new StartChoice(_boosters.Where(picked => picked != booster).ToArray());

        public bool Equals(StartChoice? other) => other is not null && _boosters.SequenceEqual(other._boosters);

        public override bool Equals(object? obj) => Equals(obj as StartChoice);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (BoosterKind booster in _boosters)
            {
                hash.Add(booster);
            }

            return hash.ToHashCode();
        }

        public override string ToString() => Count == 0 ? "no start booster" : string.Join(", ", _boosters.Select(BoosterKinds.Name));
    }

    /// <summary>What a press on a start booster did to the choice. Closed set.</summary>
    public abstract record StartToggle
    {
        private StartToggle()
        {
        }

        /// <summary>The booster was picked, or put back if it was picked before: the choice after the press.</summary>
        public sealed record Changed(StartChoice Choice) : StartToggle;

        /// <summary>The player holds none of the booster; the choice stays.</summary>
        public sealed record NoneOwned(BoosterKind Booster) : StartToggle;

        /// <summary>The attempt has its <paramref name="PerAttempt"/> start boosters already; the choice stays.</summary>
        public sealed record NoMoreThisAttempt(int PerAttempt) : StartToggle;
    }

    /// <summary>How many start boosters one attempt can take (the settings' startBoosters).</summary>
    public sealed class StartBoosterRules
    {
        /// <exception cref="ArgumentOutOfRangeException">Fewer than one, or more than there are kinds of start booster.</exception>
        public StartBoosterRules(int perAttempt)
        {
            if (perAttempt < 1 || perAttempt > KindsOfStartBooster)
            {
                throw new ArgumentOutOfRangeException(nameof(perAttempt), perAttempt, $"An attempt takes 1 to {KindsOfStartBooster} start boosters, each kind once.");
            }

            PerAttempt = perAttempt;
        }

        /// <summary>How many kinds of start booster the game has: no attempt can take more, since each is taken once.</summary>
        public static int KindsOfStartBooster { get; } = BoosterKinds.All.Count(BoosterKinds.IsStart);

        public int PerAttempt { get; }

        /// <summary>
        /// A press on <paramref name="booster"/>: a picked booster is put back; one that is not picked is picked
        /// when the player holds it and the attempt has room for it.
        /// </summary>
        /// <exception cref="ArgumentException">The booster is used during play, not taken before a level starts.</exception>
        public StartToggle Toggled(StartChoice choice, BoosterKind booster, BoosterShelf owned)
        {
            if (!booster.IsStart())
            {
                throw new ArgumentException($"{booster.Name()} is used during play, not taken before a level starts.", nameof(booster));
            }

            if (choice.Has(booster))
            {
                return new StartToggle.Changed(choice.Without(booster));
            }

            if (!owned.Has(booster))
            {
                return new StartToggle.NoneOwned(booster);
            }

            if (choice.Count >= PerAttempt)
            {
                return new StartToggle.NoMoreThisAttempt(PerAttempt);
            }

            return new StartToggle.Changed(choice.With(booster));
        }
    }
}
