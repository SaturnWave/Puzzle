#nullable enable
using System;

namespace Puzzle.Client.Screens
{
    /// <summary>What the player has done with a booster where its disc stands. A closed set.</summary>
    public enum BoosterChoice
    {
        /// <summary>Nothing: the booster waits on its disc.</summary>
        Waiting,

        /// <summary>Chosen before a level, to be taken into it (LevelStartTR: a green ring and a tick).</summary>
        Chosen,

        /// <summary>Armed in a level: the next touch on the board uses it.</summary>
        Armed,

        /// <summary>Set aside for now: another booster is armed, or the shelf does not listen.</summary>
        SetAside,

        /// <summary>Not open yet: the booster opens on a later level, which the disc names (the owner's decision of 2026-10-15).</summary>
        Locked,
    }

    /// <summary>The badge at the lower right of a booster's disc. A closed set.</summary>
    public enum BoosterBadge
    {
        /// <summary>The teal badge with how many the player owns.</summary>
        Count,

        /// <summary>The green badge with a plus: the player owns none, and the disc offers more.</summary>
        GetMore,

        /// <summary>The teal badge across the disc's foot that names the level the booster opens on: "Bölüm 7".</summary>
        OpensOn,

        /// <summary>None: the disc shows a booster that is sold or given, not the player's stock (ShopTR's sets, a reward).</summary>
        None,
    }

    /// <summary>
    /// What a booster's disc shows, as the design canvas draws its faces (BoardTR, LevelStartTR): how many the
    /// player owns or that more can be had, the green ring of one that is chosen or armed, the tick of a chosen
    /// one, and whether the disc is drawn faint. It is the one rule that turns a count and a choice into a face,
    /// so every disc of the game shows the same thing for the same stock. No Unity dependency.
    /// </summary>
    public readonly struct BoosterFace : IEquatable<BoosterFace>
    {
        private BoosterFace(int owned, BoosterChoice choice, int? opensOn = null, bool isGoods = false)
        {
            Owned = owned;
            Choice = choice;
            OpensOn = opensOn;
            IsGoods = isGoods;
        }

        /// <summary>How many of the booster the player owns.</summary>
        public int Owned { get; }

        public BoosterChoice Choice { get; }

        /// <summary>The level a locked booster opens on; nothing for a booster that is open.</summary>
        public int? OpensOn { get; }

        /// <summary>True for a booster shown as what a set holds or a reward gives (<see cref="Goods"/>): its disc is bare.</summary>
        public bool IsGoods { get; }

        /// <summary>
        /// The face of a booster shown as goods rather than as the player's stock: a bare disc, no badge, no ring, as
        /// the canvas draws a set's boosters over the pills that count them (ShopTR), and as a reward shows what it gives.
        /// </summary>
        public static BoosterFace Goods => new BoosterFace(0, BoosterChoice.Waiting, isGoods: true);

        /// <summary>No badge on goods; the level a locked booster opens on; the count while the player owns any; the plus of "get more" once none is left.</summary>
        public BoosterBadge Badge => IsGoods ? BoosterBadge.None
            : Choice == BoosterChoice.Locked ? BoosterBadge.OpensOn
            : Owned > 0 ? BoosterBadge.Count
            : BoosterBadge.GetMore;

        /// <summary>True for a disc in the green ring: a booster that is chosen for the level, or armed in it.</summary>
        public bool IsRinged => Choice == BoosterChoice.Chosen || Choice == BoosterChoice.Armed;

        /// <summary>True for a disc with the tick on its upper right: a booster chosen for the level.</summary>
        public bool IsTicked => Choice == BoosterChoice.Chosen;

        /// <summary>True for a disc that is drawn faint and takes no finger: one that is set aside, or not open yet.</summary>
        public bool IsFaint => Choice == BoosterChoice.SetAside || Choice == BoosterChoice.Locked;

        /// <summary>The face of a booster of which the player owns so many and has done this with.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The count is less than nothing.</exception>
        /// <exception cref="ArgumentException">
        /// A booster the player owns none of is said to be chosen or armed: only what is owned can be; or a booster
        /// is said to be locked without the level it opens on (<see cref="LockedUntil"/>).
        /// </exception>
        public static BoosterFace Of(int owned, BoosterChoice choice)
        {
            if (owned < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(owned), owned, "A player owns none of a booster at the least.");
            }

            if (choice == BoosterChoice.Locked)
            {
                throw new ArgumentException("A locked booster's face names the level it opens on: BoosterFace.LockedUntil.", nameof(choice));
            }

            bool isInUse = choice == BoosterChoice.Chosen || choice == BoosterChoice.Armed;
            if (owned == 0 && isInUse)
            {
                throw new ArgumentException($"A booster the player owns none of cannot be {choice}.", nameof(choice));
            }

            return new BoosterFace(owned, choice);
        }

        /// <summary>The face of a booster that is not open yet: dimmed, under no finger, the level it opens on across its foot. What is owned of it is not shown.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The level is below 2: a booster that opens on the first level is never locked.</exception>
        public static BoosterFace LockedUntil(int level) => level >= 2
            ? new BoosterFace(0, BoosterChoice.Locked, level)
            : throw new ArgumentOutOfRangeException(nameof(level), level, "A booster is locked only until a later level than the first.");

        public bool Equals(BoosterFace other) => Owned == other.Owned && Choice == other.Choice && OpensOn == other.OpensOn && IsGoods == other.IsGoods;

        public override bool Equals(object? obj) => obj is BoosterFace other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Owned, Choice, OpensOn, IsGoods);

        public override string ToString() => IsGoods ? "goods" : OpensOn is int level ? $"locked until level {level}" : $"{Owned} owned, {Choice}";

        public static bool operator ==(BoosterFace left, BoosterFace right) => left.Equals(right);

        public static bool operator !=(BoosterFace left, BoosterFace right) => !left.Equals(right);
    }
}
