using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Meta
{
    /// <summary>A set of booster kinds; two sets that hold the same kinds are equal. It never changes: adding a kind gives another set.</summary>
    public readonly struct BoosterKindSet : IEquatable<BoosterKindSet>
    {
        /// <summary>One bit for each kind, by its place in <see cref="BoosterKind"/>.</summary>
        private readonly int _bits;

        private BoosterKindSet(int bits)
        {
            _bits = bits;
        }

        /// <summary>No kind at all.</summary>
        public static BoosterKindSet None => default;

        /// <summary>The kinds the set holds, in the engine's order.</summary>
        public IEnumerable<BoosterKind> Kinds
        {
            get
            {
                BoosterKindSet set = this;
                return BoosterKinds.All.Where(set.Contains);
            }
        }

        public static BoosterKindSet Of(IEnumerable<BoosterKind> kinds) => kinds.Aggregate(None, (set, kind) => set.With(kind));

        public bool Contains(BoosterKind kind) => (_bits & BitOf(kind)) != 0;

        public BoosterKindSet With(BoosterKind kind) => new BoosterKindSet(_bits | BitOf(kind));

        public bool Equals(BoosterKindSet other) => _bits == other._bits;

        public override bool Equals(object? obj) => obj is BoosterKindSet other && Equals(other);

        public override int GetHashCode() => _bits;

        public override string ToString() => _bits == 0 ? "none" : string.Join(", ", Kinds.Select(BoosterKinds.Name));

        public static bool operator ==(BoosterKindSet left, BoosterKindSet right) => left.Equals(right);

        public static bool operator !=(BoosterKindSet left, BoosterKindSet right) => !left.Equals(right);

        /// <exception cref="ArgumentOutOfRangeException">The kind is not one of the closed set.</exception>
        private static int BitOf(BoosterKind kind) => Enum.IsDefined(typeof(BoosterKind), kind)
            ? 1 << (int)kind
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown booster.");
    }

    /// <summary>
    /// When the boosters used during play open to a player, and what each brings as it opens (the owner's decision of
    /// 2026-10-15): one at a time, each on a level of its own. Before its level a booster cannot be used; on its level
    /// it is shown once, and its gift is given, once for good. The wallet keeps which gifts were given
    /// (<see cref="RewardsGiven.UnlockGifts"/>), so no gift comes twice, also not to a player whose save is past the
    /// level already: that player is given each gift the first time the rule is asked. The gifts are the welcome
    /// gift's part for the boosters used during play, given as each opens instead of before the first level. The start
    /// boosters are chosen on the card before a level and are open from the first level on.
    /// </summary>
    public sealed class BoosterUnlocks
    {
        private readonly Dictionary<BoosterKind, int> _levels;

        /// <param name="levels">The level each booster used during play opens on, from 1; every one of the four, and no start booster.</param>
        /// <param name="gift">What each of them brings as it opens: none or more of its own kind, and nothing of any start booster.</param>
        /// <exception cref="ArgumentException">A booster used during play has no level, a start booster has one or a gift, or a level is below 1.</exception>
        public BoosterUnlocks(IReadOnlyDictionary<BoosterKind, int> levels, BoosterShelf gift)
        {
            if (levels is null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            Gift = gift ?? throw new ArgumentNullException(nameof(gift));
            if (levels.Keys.Any(BoosterKinds.IsStart) || BoosterKinds.All.Any(kind => kind.IsStart() && gift.Has(kind)))
            {
                throw new ArgumentException("A start booster is open from the first level on: it has no level of its own and no gift.", nameof(levels));
            }

            if (BoosterKinds.InLevel.Any(kind => !levels.ContainsKey(kind)) || levels.Values.Any(level => level < 1))
            {
                throw new ArgumentException("Every booster used during play opens on a level of its own, from level 1.", nameof(levels));
            }

            _levels = levels.ToDictionary(opening => opening.Key, opening => opening.Value);
        }

        /// <summary>What the boosters used during play bring as they open, each of its own kind.</summary>
        public BoosterShelf Gift { get; }

        /// <summary>The level a booster used during play opens on.</summary>
        /// <exception cref="ArgumentException">The booster is a start booster, which is open from the first level on.</exception>
        public int LevelOf(BoosterKind kind) => kind.IsInLevel()
            ? _levels[kind]
            : throw new ArgumentException($"{kind.Name()} is a start booster, open from the first level on.", nameof(kind));

        /// <summary>True when the booster can be used on a level: a start booster always, one used during play from its own level on.</summary>
        public bool IsOpenOn(BoosterKind kind, int level) => kind.IsStart() || level >= _levels[kind];

        /// <summary>The boosters used during play that open on exactly this level, in the engine's order: the ones it shows for the first time.</summary>
        public IReadOnlyList<BoosterKind> OpeningOn(int level) => BoosterKinds.InLevel.Where(kind => _levels[kind] == level).ToArray();

        /// <summary>The boosters open on a level whose gift the wallet was not given yet, in the engine's order.</summary>
        public IReadOnlyList<BoosterKind> GiftsDueOn(Wallet wallet, int level) =>
            BoosterKinds.InLevel.Where(kind => IsOpenOn(kind, level) && !wallet.Rewards.UnlockGifts.Contains(kind)).ToArray();

        /// <summary>
        /// The wallet with the gift of every booster that is open on <paramref name="level"/> and was not given its
        /// gift yet, each kept as given; the very wallet when none is due.
        /// </summary>
        public Wallet GivenTo(Wallet wallet, int level)
        {
            Wallet given = wallet;
            foreach (BoosterKind kind in GiftsDueOn(wallet, level))
            {
                given = given
                    .WithShelf(given.Shelf.With(kind, Gift.Count(kind)))
                    .WithRewards(given.Rewards.WithTheUnlockGiftOf(kind));
            }

            return given;
        }
    }
}
