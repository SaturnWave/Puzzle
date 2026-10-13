using System.Globalization;

namespace Puzzle.Economy.Units;

/// <summary>A number of moves of a game: played, allowed, left over, or the move something happened on.</summary>
public readonly record struct MoveCount : IComparable<MoveCount>
{
    /// <exception cref="ArgumentOutOfRangeException">The number is negative.</exception>
    public MoveCount(int value) => Value = value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "A number of moves is never negative.");

    public static MoveCount None => default;

    public int Value { get; }

    public int CompareTo(MoveCount other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static MoveCount operator +(MoveCount left, MoveCount right) => new(left.Value + right.Value);

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="right"/> is the larger number.</exception>
    public static MoveCount operator -(MoveCount left, MoveCount right) => new(left.Value - right.Value);

    public static bool operator <(MoveCount left, MoveCount right) => left.Value < right.Value;

    public static bool operator >(MoveCount left, MoveCount right) => left.Value > right.Value;

    public static bool operator <=(MoveCount left, MoveCount right) => left.Value <= right.Value;

    public static bool operator >=(MoveCount left, MoveCount right) => left.Value >= right.Value;
}

/// <summary>A number of lives.</summary>
public readonly record struct LifeCount : IComparable<LifeCount>
{
    /// <exception cref="ArgumentOutOfRangeException">The number is negative.</exception>
    public LifeCount(int value) => Value = value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "A number of lives is never negative.");

    public int Value { get; }

    public bool IsNone => Value == 0;

    public LifeCount OneMore => new(Value + 1);

    /// <exception cref="ArgumentOutOfRangeException">There is no life to take.</exception>
    public LifeCount OneFewer => new(Value - 1);

    public int CompareTo(LifeCount other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static bool operator <(LifeCount left, LifeCount right) => left.Value < right.Value;

    public static bool operator >(LifeCount left, LifeCount right) => left.Value > right.Value;

    public static bool operator <=(LifeCount left, LifeCount right) => left.Value <= right.Value;

    public static bool operator >=(LifeCount left, LifeCount right) => left.Value >= right.Value;
}

/// <summary>A whole percentage from 0 to 100: a share of the players, or a share of a level's goals that is done.</summary>
public readonly record struct Percent : IComparable<Percent>
{
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 0..100.</exception>
    public Percent(int value) => Value = value is >= 0 and <= 100
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "A percentage lies between 0 and 100.");

    public int Value { get; }

    public int CompareTo(Percent other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static bool operator <(Percent left, Percent right) => left.Value < right.Value;

    public static bool operator >(Percent left, Percent right) => left.Value > right.Value;

    public static bool operator <=(Percent left, Percent right) => left.Value <= right.Value;

    public static bool operator >=(Percent left, Percent right) => left.Value >= right.Value;
}

/// <summary>Which attempt of a level this is for a player, counted from 0; it is 0 again on the next level.</summary>
public readonly record struct AttemptNumber
{
    /// <exception cref="ArgumentOutOfRangeException">The number is negative.</exception>
    public AttemptNumber(int value) => Value = value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Attempts are counted from 0.");

    public static AttemptNumber First => default;

    public int Value { get; }

    public AttemptNumber Next => new(Value + 1);

    /// <summary>Every attempt before this one was lost, or the level would be behind the player.</summary>
    public int LossesBefore => Value;

    /// <summary>The seed of the level's pool this attempt meets: the pool in turn, as the engine gives it (AttemptPolicy.SeedsFor).</summary>
    public SeedPosition PositionIn(int poolSize) => new(Value % poolSize);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>The place of a play seed in a level's seed pool, from 0.</summary>
public readonly record struct SeedPosition
{
    /// <exception cref="ArgumentOutOfRangeException">The position is negative.</exception>
    public SeedPosition(int value) => Value = value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Pool positions are counted from 0.");

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>What makes one simulated player's luck their own: which of a seed's stored games they meet on each attempt.</summary>
public readonly record struct PlayerSeed(ulong Value);
