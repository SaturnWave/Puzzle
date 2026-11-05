using System.Globalization;

namespace Puzzle.Economy.Units;

/// <summary>
/// An amount of US money in whole cents: a price as the US store lists it ($1.99 is 199 cents), or what a player has
/// paid in all. Never negative and never a fraction of a cent, so no sum of prices is rounded on the way. A player
/// sees a price as their own country's store words it; this is the reference price a product is set up with in the
/// stores, and the money the simulator counts in.
/// </summary>
public readonly record struct UsDollars : IComparable<UsDollars>
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public UsDollars(long cents) => Cents = cents >= 0
        ? cents
        : throw new ArgumentOutOfRangeException(nameof(cents), cents, "An amount of money is never negative.");

    public static UsDollars None => default;

    public long Cents { get; }

    /// <summary>The amount in dollars, exactly: 199 cents are 1.99.</summary>
    public decimal Dollars => Cents / 100m;

    /// <summary>The cents after the dollars: 99 for $1.99, the ending every store price of the game has.</summary>
    public int CentsAfterTheDollars => (int)(Cents % 100);

    public int CompareTo(UsDollars other) => Cents.CompareTo(other.Cents);

    /// <summary>The amount as the US store writes it: "$1.99".</summary>
    public override string ToString() => "$" + Dollars.ToString("0.00", Invariant);

    public static UsDollars operator +(UsDollars left, UsDollars right) => new(checked(left.Cents + right.Cents));

    public static bool operator <(UsDollars left, UsDollars right) => left.Cents < right.Cents;

    public static bool operator >(UsDollars left, UsDollars right) => left.Cents > right.Cents;

    public static bool operator <=(UsDollars left, UsDollars right) => left.Cents <= right.Cents;

    public static bool operator >=(UsDollars left, UsDollars right) => left.Cents >= right.Cents;
}
