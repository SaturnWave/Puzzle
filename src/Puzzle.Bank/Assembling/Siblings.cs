using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Engine;

namespace Puzzle.Bank.Assembling;

/// <summary>
/// Where an assembly gets a level's own board at another amount. A board whose goals measure an amount (collect N,
/// drop N) is a family of candidates; when a policy moves a level's limit out of its range, the same layout with
/// smaller or larger goals can bring it back, and the player keeps the level they know.
/// </summary>
public interface ISiblingSource
{
    /// <summary>
    /// The candidate on <paramref name="candidate"/>'s board that lies <paramref name="steps"/> steps of the amount
    /// grid away, with its ladder games. Absent when the board has no amount to move, the grid ends before that
    /// step, the generator's rules hold the amount in place, or the sibling cannot be played.
    /// </summary>
    /// <exception cref="BankWriteException">The sibling was measured and could not be stored.</exception>
    BankCandidate? Sibling(BankCandidate candidate, int steps);
}

/// <summary>
/// Siblings from the bank: one it holds is handed out, one it lacks is measured on the whole ladder like any
/// candidate and stored, under the origin of the candidate it was made from (the same draw or the same level file;
/// its amount step says which member of the family it is).
/// </summary>
public sealed class BankSiblings(BankCatalog catalog, BankWriter writer, EpochId epoch, GrowthSettings settings) : ISiblingSource
{
    public int Measured { get; private set; }

    public BankCandidate? Sibling(BankCandidate candidate, int steps)
    {
        int step = candidate.AmountStep + steps;
        if (!candidate.Content.Features.IsScalable || step < AmountGrid.SmallestStep || step > AmountGrid.LargestStep)
        {
            return null;
        }

        CandidateContent content = AmountGrid.AtStep(candidate.Content, candidate.Amounts, step);
        if (content.Id == candidate.Content.Id)
        {
            return null;
        }

        return catalog.Find(content.Id) ?? Measure(candidate, content, step);
    }

    private BankCandidate? Measure(BankCandidate from, CandidateContent content, int step)
    {
        uint[] seeds = Grower.PlaySeedsOf(content.Id, settings.Seeds);
        RungGames[] games;
        try
        {
            games = LadderMeasurer.Measure(new MeasurementRequest
            {
                Candidate = content,
                Seeds = seeds,
                SeedIndices = Enumerable.Range(0, seeds.Length).ToArray(),
                Players = settings.Rungs,
                Purpose = GamePurpose.Breadth,
                RepeatFrom = GamePurpose.Breadth.FirstRepeat(),
                RepeatCount = settings.Repeats,
                Settings = settings.Measurement,
                Epoch = epoch,
            });
        }
        catch (LevelInitializationException)
        {
            // An amount at which the board cannot start is no sibling; the level then looks for another board.
            return null;
        }

        var sibling = new BankCandidate(content, seeds, from.Amounts, step, from.Origin, DateTime.UtcNow);
        writer.Adopt(sibling, games);
        Measured++;
        return sibling;
    }
}
