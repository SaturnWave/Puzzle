using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>The boosters a player holds, by kind. A shelf never changes: taking or adding one gives another shelf, so a snapshot of a player's standing stays what it was.</summary>
public sealed class BoosterShelf
{
    private readonly int[] _counts;

    private BoosterShelf(int[] counts) => _counts = counts;

    public static BoosterShelf Empty { get; } = new(new int[BoosterKinds.All.Count]);

    public int Total => _counts.Sum();

    public int Count(BoosterKind kind) => _counts[(int)kind];

    public bool Has(BoosterKind kind) => Count(kind) > 0;

    public BoosterShelf WithOneMore(BoosterKind kind) => Changed(kind, +1);

    /// <exception cref="InvalidOperationException">The shelf holds none of the kind.</exception>
    public BoosterShelf WithOneFewer(BoosterKind kind) => Has(kind)
        ? Changed(kind, -1)
        : throw new InvalidOperationException($"No {kind.Name()} is held.");

    private BoosterShelf Changed(BoosterKind kind, int by)
    {
        int[] counts = [.. _counts];
        counts[(int)kind] += by;
        return new BoosterShelf(counts);
    }
}
