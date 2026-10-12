using Puzzle.Bank.Measuring;
using Puzzle.Core.Levels;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Measuring;

/// <summary>What a line of a measurement is for; lines are sorted by level, then skill, then board start.</summary>
/// <param name="Skill">
/// The share of the moves the player of the line notices. A number and not a rung: a file may hold the games of a player
/// the ladder no longer has, and such a line must still be read to be named as stale.
/// </param>
public readonly record struct LineKey(LevelId Level, int Skill, BoardStart Start) : IComparable<LineKey>
{
    public static LineKey Of(LevelId level, SkillRung rung, BoardStart start) => new(level, rung.Skill, start);

    public int CompareTo(LineKey other) => (Level.Value, Skill, Start).CompareTo((other.Level.Value, other.Skill, other.Start));

    public override string ToString() => $"level {Level}, skill-{Skill}, {Start.FileName()}";

    public static bool operator <(LineKey left, LineKey right) => left.CompareTo(right) < 0;

    public static bool operator >(LineKey left, LineKey right) => left.CompareTo(right) > 0;

    public static bool operator <=(LineKey left, LineKey right) => left.CompareTo(right) <= 0;

    public static bool operator >=(LineKey left, LineKey right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// One line of a measurement: the stored games of one level, player and board start, seed by seed in the order of
/// the level's own pool. It carries what a simulation needs of the level (tier, limit, pool size), so a run reads no
/// level file to simulate; the fingerprint says which content of the level number the games were played on.
/// </summary>
public sealed record MeasuredLine
{
    public required LevelId Level { get; init; }

    /// <summary>The level's content when it was measured (LevelJson.Fingerprint).</summary>
    public required string Fingerprint { get; init; }

    public required DifficultyTier Tier { get; init; }

    public required MoveCount MoveLimit { get; init; }

    public required IReadOnlyList<uint> Pool { get; init; }

    /// <summary>The rung with the settings it played with; games of a rung whose settings changed since are another player's.</summary>
    public required PlayerModel Player { get; init; }

    public required BoardStart Start { get; init; }

    /// <summary>The games of each pool seed, in pool order.</summary>
    public required IReadOnlyList<SeedGames> Seeds { get; init; }

    public LineKey Key => new(Level, Player.NoticePercent, Start);

    public int GameCount => Seeds.Sum(seed => seed.Count);
}
