using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Museum;

namespace Puzzle.Economy.Reporting;

/// <summary>
/// A group of levels that what the measurement says is split by: the first room, which is set easier than the rest
/// of the game, the later rooms by tier, and every level together.
/// </summary>
public sealed record LevelGroup(string Name, Func<LevelId, DifficultyTier, bool> Has)
{
    /// <summary>The first room, the later rooms tier by tier, and every level together: the last holds all the others.</summary>
    public static IReadOnlyList<LevelGroup> All { get; } =
    [
        new("first room", (level, _) => IsInTheFirstRoom(level)),
        .. Enum.GetValues<DifficultyTier>().Select(tier => new LevelGroup($"later rooms {tier}", (level, ofLevel) => !IsInTheFirstRoom(level) && ofLevel == tier)),
        new("all levels", (_, _) => true),
    ];

    public bool Holds(MeasuredLine line) => Has(line.Level, line.Tier);

    private static bool IsInTheFirstRoom(LevelId level) => level.Value <= MuseumPlan.LevelsPerRoom;
}

/// <summary>A stored game with the weight it has in a share over levels.</summary>
public readonly record struct WeightedGame(MeasuredLine Line, MeasuredGame Game, double Weight);

/// <summary>A weighted mean that is built up one value at a time; unknown while nothing was added.</summary>
public sealed class WeightedMean
{
    private double _total;

    public double Weight { get; private set; }

    public long Count { get; private set; }

    public double? Value => Weight > 0 ? _total / Weight : null;

    public void Add(double weight, double value)
    {
        Weight += weight;
        _total += weight * value;
        Count++;
    }

    public void Add(double weight, bool counts) => Add(weight, counts ? 1 : 0);
}

public static class StoredGames
{
    /// <summary>
    /// Every stored game of the lines with its weight: each level weighs the same, in a level each pool seed, in a seed
    /// each game. A share counted with these weights is what a player meets who is as likely to be on any level of the
    /// group and on any seed of its pool; the deeper measurement of a pool's first seeds does not tilt it.
    /// </summary>
    /// <param name="firstSeedOnly">Only the games of each level's first pool seed: what a first attempt meets.</param>
    public static IEnumerable<WeightedGame> Weighted(IReadOnlyCollection<MeasuredLine> lines, bool firstSeedOnly = false)
    {
        foreach (MeasuredLine line in lines)
        {
            int seeds = firstSeedOnly ? 1 : line.Seeds.Count;
            for (int seed = 0; seed < seeds; seed++)
            {
                SeedGames games = line.Seeds[seed];
                double weight = 1.0 / lines.Count / seeds / games.Count;
                for (int game = 0; game < games.Count; game++)
                {
                    yield return new WeightedGame(line, games[game], weight);
                }
            }
        }
    }
}
