using System.Globalization;

namespace Puzzle.Economy.Units;

/// <summary>A day of a player's life with the game; the day of the first session is day 1.</summary>
public readonly record struct DayNumber : IComparable<DayNumber>
{
    /// <exception cref="ArgumentOutOfRangeException">The number is below 1.</exception>
    public DayNumber(int value) => Value = value >= 1
        ? value
        : throw new ArgumentOutOfRangeException(nameof(value), value, "Days are counted from 1.");

    public int Value { get; }

    public int CompareTo(DayNumber other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static bool operator <(DayNumber left, DayNumber right) => left.Value < right.Value;

    public static bool operator >(DayNumber left, DayNumber right) => left.Value > right.Value;

    public static bool operator <=(DayNumber left, DayNumber right) => left.Value <= right.Value;

    public static bool operator >=(DayNumber left, DayNumber right) => left.Value >= right.Value;
}

/// <summary>
/// A moment of a player's life with the game: the time since the midnight before their first session. Players have no
/// calendar; two players at the same <see cref="PlayerTime"/> are equally far into their own journeys.
/// </summary>
public readonly record struct PlayerTime : IComparable<PlayerTime>
{
    /// <exception cref="ArgumentOutOfRangeException">The time is before the first midnight.</exception>
    public PlayerTime(TimeSpan sinceFirstMidnight) => SinceFirstMidnight = sinceFirstMidnight >= TimeSpan.Zero
        ? sinceFirstMidnight
        : throw new ArgumentOutOfRangeException(nameof(sinceFirstMidnight), sinceFirstMidnight, "A player's time starts at their first midnight.");

    public TimeSpan SinceFirstMidnight { get; }

    public DayNumber Day => new((int)(SinceFirstMidnight.Ticks / TimeSpan.TicksPerDay) + 1);

    public static PlayerTime At(DayNumber day, TimeOnly timeOfDay) =>
        new(TimeSpan.FromDays(day.Value - 1) + timeOfDay.ToTimeSpan());

    /// <summary>The midnight that ends <paramref name="day"/>.</summary>
    public static PlayerTime EndOf(DayNumber day) => new(TimeSpan.FromDays(day.Value));

    public int CompareTo(PlayerTime other) => SinceFirstMidnight.CompareTo(other.SinceFirstMidnight);

    public static PlayerTime operator +(PlayerTime time, TimeSpan duration) => new(time.SinceFirstMidnight + duration);

    public static TimeSpan operator -(PlayerTime later, PlayerTime earlier) => later.SinceFirstMidnight - earlier.SinceFirstMidnight;

    public static bool operator <(PlayerTime left, PlayerTime right) => left.SinceFirstMidnight < right.SinceFirstMidnight;

    public static bool operator >(PlayerTime left, PlayerTime right) => left.SinceFirstMidnight > right.SinceFirstMidnight;

    public static bool operator <=(PlayerTime left, PlayerTime right) => left.SinceFirstMidnight <= right.SinceFirstMidnight;

    public static bool operator >=(PlayerTime left, PlayerTime right) => left.SinceFirstMidnight >= right.SinceFirstMidnight;
}
