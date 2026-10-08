using Puzzle.Core.Levels;
using Puzzle.Generator.Composition;

namespace Puzzle.Bank.Candidates;

/// <summary>
/// The amounts of a board's scalable goals at step 0 of the grid: each collect goal's target, in goal order, and the
/// items that spawn during play. Stored with every candidate of the board, so a sibling at any step is computed from
/// the same numbers and two tools never round their way to two different grids.
/// </summary>
public sealed record AmountBase(IReadOnlyList<int> CollectTargets, int SpawnedItems);

/// <summary>
/// The amounts a board can ask for. Goals that measure an amount (collect N gems, drop N items) scale; a bot plays
/// toward a goal's share that is done, so the same board at another amount is another level to measure, a sibling,
/// and cannot be read from one long game. The grid keeps the siblings few: step s asks for 1.25^s times the base.
/// A board whose goals are all obstacles has no siblings: its amount is its layout.
/// </summary>
public static class AmountGrid
{
    public const double Ratio = 1.25;

    public const int SmallestStep = -6;

    public const int LargestStep = 6;

    /// <summary>The candidate's own amounts, to serve as step 0 of its board.</summary>
    public static AmountBase BaseOf(CandidateContent content) => new(
        content.Level.Goals.OfType<GoalSpec.CollectColor>().Select(goal => goal.Target).ToArray(),
        content.Level.Spawn.Items.Total);

    /// <summary>
    /// The sibling of <paramref name="board"/> at <paramref name="step"/>: the same layout and goal kinds with the
    /// amounts of the grid, sized by the generator's own rules (a collect goal never under its floor, at least one
    /// item spawning). Neighbouring steps can give the same candidate where a rule holds an amount in place.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The step is off the grid.</exception>
    public static CandidateContent AtStep(CandidateContent board, AmountBase amounts, int step)
    {
        if (step < SmallestStep || step > LargestStep)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, $"Amount steps run from {SmallestStep} to {LargestStep}.");
        }

        double scale = Math.Pow(Ratio, step);
        LevelDefinition level = board.Level;
        bool dropsItems = level.Goals.Any(goal => goal is GoalSpec.DropItems);
        int spawnedItems = dropsItems ? AmountRules.SpawnedItems(amounts.SpawnedItems, scale) : level.Spawn.Items.Total;
        var collectBases = new Queue<int>(amounts.CollectTargets); // in goal order, as BaseOf read them
        var goals = new List<GoalSpec>();
        foreach (GoalSpec goal in level.Goals)
        {
            goals.Add(goal switch
            {
                GoalSpec.CollectColor collect => new GoalSpec.CollectColor(collect.Color, AmountRules.CollectTarget(collectBases.Dequeue(), scale)),
                GoalSpec.DropItems _ => new GoalSpec.DropItems(level.Layout.PresetItemCount + spawnedItems),
                _ => goal,
            });
        }

        ItemSpawnRules items = dropsItems
            ? new ItemSpawnRules { Total = spawnedItems, MaxOnBoard = AmountRules.MaxItemsOnBoard(spawnedItems), ChancePercent = level.Spawn.Items.ChancePercent }
            : level.Spawn.Items;
        return board.With(goals, items);
    }

    /// <summary>
    /// How many steps to move a candidate whose move limit came out at <paramref name="limit"/> toward
    /// <paramref name="wantedLimit"/>, taking the limit as proportional to the amount: negative for less, never zero
    /// (the caller asks because the limit missed), and no further than a factor 2.5 at once, because the proportion
    /// is rough far from where it was measured.
    /// </summary>
    public static int StepsToward(int limit, int wantedLimit)
    {
        double factor = Math.Clamp(wantedLimit / (double)Math.Max(1, limit), 0.4, 2.5);
        int steps = (int)Math.Round(Math.Log(factor) / Math.Log(Ratio));
        return steps != 0 ? steps : wantedLimit < limit ? -1 : 1;
    }
}
