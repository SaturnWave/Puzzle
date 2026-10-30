using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Engine;

namespace Puzzle.Bank.Growing;

/// <summary>What became of a candidate handed to the remeasurer. Closed set.</summary>
public enum RemeasureOutcome
{
    /// <summary>The ladder games it lacked in the epoch were played and stored.</summary>
    Measured,

    /// <summary>Every rung has its games in the epoch already; nothing was played.</summary>
    AlreadyMeasured,

    /// <summary>The candidate cannot produce a starting board under the build's rules; it stays without games and counts for nothing.</summary>
    Unplayable,
}

/// <summary>
/// Plays the ladder games a held candidate lacks in an epoch. After a change of the engine or the bot a new epoch
/// starts empty: the boards are all still there, and their games of the old rules say nothing of the new ones. This
/// gives a board its games again, on the same seeds and repeats as before, so the bank outlives a rule change with
/// its boards, its amounts and its origins; only the playing is done again. It also fills in a single rung whose
/// settings changed, because what is lacking is counted rung by rung. A candidate is written whole or not at all,
/// so a stopped run is resumed by starting it again.
/// </summary>
public sealed class Remeasurer(BankWriter writer, EpochId epoch, GrowthSettings settings)
{
    /// <summary>The rungs of the ladder that have no breadth games of the candidate in the epoch.</summary>
    public IReadOnlyList<PlayerModel> RungsLacking(BankCandidate candidate)
    {
        HashSet<PlayerModel> measured = candidate.Games
            .Where(batch => batch.Header.Epoch == epoch && batch.Header.Purpose == GamePurpose.Breadth)
            .Select(batch => batch.Header.Player)
            .ToHashSet();
        return settings.Rungs.Where(rung => !measured.Contains(rung)).ToArray();
    }

    /// <exception cref="BankWriteException">The games were played and could not be stored.</exception>
    public RemeasureOutcome Remeasure(BankCandidate candidate)
    {
        IReadOnlyList<PlayerModel> lacking = RungsLacking(candidate);
        if (lacking.Count == 0)
        {
            return RemeasureOutcome.AlreadyMeasured;
        }

        try
        {
            writer.Add(LadderMeasurer.Measure(new MeasurementRequest
            {
                Candidate = candidate.Content,
                Seeds = candidate.Seeds,
                SeedIndices = Enumerable.Range(0, candidate.Seeds.Count).ToArray(),
                Players = lacking,
                Purpose = GamePurpose.Breadth,
                RepeatFrom = GamePurpose.Breadth.FirstRepeat(),
                RepeatCount = settings.Repeats,
                Settings = settings.Measurement,
                Epoch = epoch,
            }));
        }
        catch (LevelInitializationException)
        {
            // A board the new rules cannot start is no candidate under them; without games it fits no level.
            return RemeasureOutcome.Unplayable;
        }

        return RemeasureOutcome.Measured;
    }

    /// <summary>
    /// The order in which a bank is measured again: first the candidates behind levels that are in use
    /// (<paramref name="inUse"/>), because an assembly can only keep a level's board once that board has games;
    /// then the other imported levels, which people may have played; then the rest. Within each, by id.
    /// </summary>
    public static IReadOnlyList<BankCandidate> InOrder(BankCatalog catalog, IReadOnlySet<CandidateId> inUse) => catalog.Candidates
        .OrderBy(candidate => inUse.Contains(candidate.Content.Id) ? 0 : candidate.Origin is CandidateOrigin.Imported ? 1 : 2)
        .ThenBy(candidate => candidate.Content.Id.Value)
        .ToArray();
}
