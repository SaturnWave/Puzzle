using Puzzle.Bank.Candidates;
using Puzzle.Core.Levels;

namespace Puzzle.Bank.Assembling;

/// <summary>How a level number differs between two folders. Closed set.</summary>
public enum LevelChange
{
    /// <summary>The two files are the same bytes.</summary>
    Identical,

    /// <summary>The same candidate (board and amounts) under another limit, seed pool, tier or palette.</summary>
    SameCandidate,

    /// <summary>The same board with other goal amounts.</summary>
    SameBoard,

    /// <summary>Another board: a player would not recognise the level.</summary>
    OtherBoard,

    OnlyInFirst,

    OnlyInSecond,
}

/// <param name="MovesFirst">The move limit in the first folder; absent when the level is not there.</param>
/// <param name="PoolChanged">Both folders have the level and its seed pools differ.</param>
public sealed record LevelDifference(int Number, DifficultyTier Tier, LevelChange Change, int? MovesFirst, int? MovesSecond, bool PoolChanged);

/// <summary>One tier of one folder: how many levels, how long, and what the report says the player wins.</summary>
/// <param name="MeanTarget">The mean of the report's target win share; absent without report rows.</param>
/// <param name="MeanWin">The mean of the report's measured win share at the limit; absent without report rows.</param>
public sealed record TierFigures(DifficultyTier Tier, int Levels, double MeanMoves, double? MeanTarget, double? MeanWin);

/// <summary>A folder of levels in figures: per tier, and the number of levels of each kind (a level of two families counts for both).</summary>
public sealed record LevelSetFigures(IReadOnlyList<TierFigures> Tiers, IReadOnlyDictionary<LevelKind, int> Kinds, int Levels)
{
    public static LevelSetFigures Of(IReadOnlyList<FolderLevel> levels)
    {
        TierFigures[] tiers = levels
            .GroupBy(level => level.Level.Difficulty)
            .OrderBy(tier => tier.Key)
            .Select(tier => new TierFigures(
                tier.Key,
                tier.Count(),
                tier.Average(level => level.Level.MoveLimit),
                MeanOf(tier, "targetWin"),
                MeanOf(tier, "win")))
            .ToArray();
        Dictionary<LevelKind, int> kinds = levels
            .SelectMany(level => LevelKinds.Of(CandidateFeatures.Of(level.Level)))
            .GroupBy(kind => kind)
            .ToDictionary(kind => kind.Key, kind => kind.Count());
        return new LevelSetFigures(tiers, kinds, levels.Count);
    }

    private static double? MeanOf(IEnumerable<FolderLevel> levels, string column)
    {
        double[] values = levels.Select(level => level.Row?.Number(column)).OfType<double>().ToArray();
        return values.Length == 0 ? null : values.Average();
    }
}

/// <summary>
/// Two folders of levels side by side, over the level numbers named: an assembly against the live levels, or against
/// another assembly. It answers what a change of policy did: per tier the limits and shares, the mix of level kinds,
/// and how many numbers changed their limit, their amounts or their board.
/// </summary>
public sealed record LevelSetComparison(LevelSetFigures First, LevelSetFigures Second, IReadOnlyList<LevelDifference> Levels)
{
    public int Count(LevelChange change) => Levels.Count(level => level.Change == change);

    /// <exception cref="LevelFormatException">A level file of either folder is not a valid level.</exception>
    public static LevelSetComparison Of(LevelFolder first, LevelFolder second, IEnumerable<int> numbers)
    {
        var inFirst = new List<FolderLevel>();
        var inSecond = new List<FolderLevel>();
        var differences = new List<LevelDifference>();
        foreach (int number in numbers)
        {
            (FolderLevel? one, FolderLevel? other) = (first.Find(number), second.Find(number));
            if (one is null && other is null)
            {
                continue;
            }

            AddIfThere(inFirst, one);
            AddIfThere(inSecond, other);
            differences.Add(new LevelDifference(
                number,
                (one ?? other)!.Level.Difficulty,
                ChangeOf(one, other),
                one?.Level.MoveLimit,
                other?.Level.MoveLimit,
                PoolChanged: one is not null && other is not null && !one.Level.Attempts.SeedPool.SequenceEqual(other.Level.Attempts.SeedPool)));
        }

        return new LevelSetComparison(LevelSetFigures.Of(inFirst), LevelSetFigures.Of(inSecond), differences);
    }

    private static void AddIfThere(List<FolderLevel> levels, FolderLevel? level)
    {
        if (level is not null)
        {
            levels.Add(level);
        }
    }

    private static LevelChange ChangeOf(FolderLevel? one, FolderLevel? other)
    {
        if (one is null || other is null)
        {
            return one is null ? LevelChange.OnlyInSecond : LevelChange.OnlyInFirst;
        }

        if (File.ReadAllBytes(one.Path).AsSpan().SequenceEqual(File.ReadAllBytes(other.Path)))
        {
            return LevelChange.Identical;
        }

        // A level that is no bank candidate (a fixed start board) has no board identity to compare: it counts as another board.
        if (one.Content is null || other.Content is null || one.Content.Board != other.Content.Board)
        {
            return LevelChange.OtherBoard;
        }

        return one.Content.Id == other.Content.Id ? LevelChange.SameCandidate : LevelChange.SameBoard;
    }
}
