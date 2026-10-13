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
    }

    /// <summary>The badge at the lower right of a booster's disc. A closed set.</summary>
    public enum BoosterBadge
    {
        /// <summary>The teal badge with how many the player owns.</summary>
        Count,

        /// <summary>The green badge with a plus: the player owns none, and the disc offers more.</summary>
        GetMore,
    }

    /// <summary>
    /// What a booster's disc shows, as the design canvas draws its faces (BoardTR, LevelStartTR): how many the
    /// player owns or that more can be had, the green ring of one that is chosen or armed, the tick of a chosen
    /// one, and whether the disc is drawn faint. It is the one rule that turns a count and a choice into a face,
    /// so every disc of the game shows the same thing for the same stock. No Unity dependency.
    /// </summary>
    public readonly struct BoosterFace : IEquatable<BoosterFace>
    {
        private BoosterFace(int owned, BoosterChoice choice)
        {
            Owned = owned;
            Choice = choice;
        }

        /// <summary>How many of the booster the player owns.</summary>
        public int Owned { get; }

        public BoosterChoice Choice { get; }

        /// <summary>The count while the player owns any; the plus of "get more" once none is left.</summary>
        public BoosterBadge Badge => Owned > 0 ? BoosterBadge.Count : BoosterBadge.GetMore;

        /// <summary>True for a disc in the green ring: a booster that is chosen for the level, or armed in it.</summary>
        public bool IsRinged => Choice == BoosterChoice.Chosen || Choice == BoosterChoice.Armed;

        /// <summary>True for a disc with the tick on its upper right: a booster chosen for the level.</summary>
        public bool IsTicked => Choice == BoosterChoice.Chosen;

        /// <summary>True for a disc that is drawn faint and takes no finger: one that is set aside.</summary>
        public bool IsFaint => Choice == BoosterChoice.SetAside;

        /// <summary>The face of a booster of which the player owns so many and has done this with.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The count is less than nothing.</exception>
        /// <exception cref="ArgumentException">A booster the player owns none of is said to be chosen or armed: only what is owned can be.</exception>
        public static BoosterFace Of(int owned, BoosterChoice choice)
        {
            if (owned < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(owned), owned, "A player owns none of a booster at the least.");
            }

            bool isInUse = choice == BoosterChoice.Chosen || choice == BoosterChoice.Armed;
            if (owned == 0 && isInUse)
            {
                throw new ArgumentException($"A booster the player owns none of cannot be {choice}.", nameof(choice));
            }

            return new BoosterFace(owned, choice);
        }

        public bool Equals(BoosterFace other) => Owned == other.Owned && Choice == other.Choice;

        public override bool Equals(object? obj) => obj is BoosterFace other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Owned, Choice);

        public override string ToString() => $"{Owned} owned, {Choice}";

        public static bool operator ==(BoosterFace left, BoosterFace right) => left.Equals(right);

        public static bool operator !=(BoosterFace left, BoosterFace right) => !left.Equals(right);
    }
}
