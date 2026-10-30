using System;
using System.Globalization;

namespace Puzzle.Meta
{
    /// <summary>
    /// An amount of the game's coins: a whole number that is never negative. Nobody owes coins; what a player lacks
    /// for a price is a shortfall, itself an amount (<see cref="ShortOf"/>).
    /// </summary>
    public readonly struct Coins : IEquatable<Coins>, IComparable<Coins>
    {
        /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
        public Coins(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "An amount of coins is never negative.");
            }

            Amount = amount;
        }

        public static Coins None => default;

        public long Amount { get; }

        public bool IsNone => Amount == 0;

        public bool Covers(Coins price) => Amount >= price.Amount;

        /// <summary>What is left after paying <paramref name="price"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The amount does not cover the price.</exception>
        public Coins Less(Coins price) => new Coins(Amount - price.Amount);

        /// <summary>How much is missing to pay <paramref name="price"/>; none when the amount covers it.</summary>
        public Coins ShortOf(Coins price) => Covers(price) ? None : new Coins(price.Amount - Amount);

        public int CompareTo(Coins other) => Amount.CompareTo(other.Amount);

        public bool Equals(Coins other) => Amount == other.Amount;

        public override bool Equals(object? obj) => obj is Coins other && Equals(other);

        public override int GetHashCode() => Amount.GetHashCode();

        public override string ToString() => Amount.ToString(CultureInfo.InvariantCulture);

        public static Coins operator +(Coins left, Coins right) => new Coins(checked(left.Amount + right.Amount));

        /// <summary>The same amount <paramref name="times"/> over: the coins of that many moves left.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="times"/> is negative.</exception>
        public static Coins operator *(Coins each, int times) => new Coins(checked(each.Amount * times));

        public static bool operator ==(Coins left, Coins right) => left.Equals(right);

        public static bool operator !=(Coins left, Coins right) => !left.Equals(right);

        public static bool operator <(Coins left, Coins right) => left.Amount < right.Amount;

        public static bool operator >(Coins left, Coins right) => left.Amount > right.Amount;

        public static bool operator <=(Coins left, Coins right) => left.Amount <= right.Amount;

        public static bool operator >=(Coins left, Coins right) => left.Amount >= right.Amount;
    }
}
