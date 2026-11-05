using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Bank.Growing;

/// <summary>What became of a level handed to the importer. Closed set.</summary>
public enum ImportOutcome
{
    /// <summary>Measured on the whole ladder and stored.</summary>
    Stored,

    /// <summary>The bank already holds this very candidate with its games of this epoch; nothing was played.</summary>
    AlreadyHeld,

    /// <summary>The bank held the candidate without games of this epoch (the rules changed since): its ladder games were played again.</summary>
    Remeasured,

    /// <summary>The level cannot be a candidate (a fixed start board, a palette out of colour order).</summary>
    NotACandidate,

    /// <summary>The level cannot produce a starting board.</summary>
    Unplayable,
}

/// <param name="Candidate">The candidate behind the level; absent when the level cannot be one.</param>
/// <param name="Detail">Why, for the outcomes that stored nothing new.</param>
public sealed record ImportResult(LevelId Level, ImportOutcome Outcome, CandidateId? Candidate, string? Detail);

/// <summary>
/// Takes existing levels into the bank: the candidate behind a level file, measured on the whole ladder like a drawn
/// one and stored with where it came from. A level's own seed pool leads its candidate's play seeds, so the seeds
/// people really play are among the measured ones and an assembly can keep them. With its board in the bank, a level
/// can stay the same board under another policy: its new limit is a look-up in these games. A level the bank holds
/// from before a rule change is measured again in the build's epoch, so importing the levels in use is also how they
/// are brought over to new rules.
/// </summary>
public sealed class Importer(BankCatalog catalog, BankWriter writer, EpochId epoch, GrowthSettings settings)
{
    private readonly Remeasurer _remeasurer = new(writer, epoch, settings);

    /// <summary>
    /// The play seeds of an imported candidate: the level's pool first, then the seeds its identity gives
    /// (<see cref="Grower.PlaySeedsOf"/>), none twice, up to the number every candidate gets. A pool larger than
    /// that is taken whole.
    /// </summary>
    public static uint[] PlaySeedsOf(LevelDefinition level, CandidateId id, int count)
    {
        var seeds = new List<uint>(level.Attempts.SeedPool);
        foreach (uint seed in Grower.PlaySeedsOf(id, count))
        {
            if (seeds.Count >= count)
            {
                break;
            }

            if (!seeds.Contains(seed))
            {
                seeds.Add(seed);
            }
        }

        return [.. seeds];
    }

    /// <param name="source">Where the level came from, as the candidate's origin will say (a path).</param>
    /// <exception cref="BankWriteException">The bank's disk failed; everything stored before is in the bank.</exception>
    public ImportResult Import(LevelDefinition level, string source)
    {
        CandidateContent content;
        try
        {
            content = CandidateContent.Of(level);
        }
        catch (CandidateFormatException ex)
        {
            return new ImportResult(level.Id, ImportOutcome.NotACandidate, Candidate: null, ex.Message);
        }

        if (catalog.Find(content.Id) is BankCandidate held)
        {
            return new ImportResult(level.Id, OutcomeOf(_remeasurer.Remeasure(held)), content.Id, Detail: null);
        }

        uint[] seeds = PlaySeedsOf(level, content.Id, settings.Seeds);
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
        catch (LevelInitializationException ex)
        {
            return new ImportResult(level.Id, ImportOutcome.Unplayable, content.Id, ex.Message);
        }

        var candidate = new BankCandidate(content, seeds, AmountGrid.BaseOf(content), amountStep: 0, new CandidateOrigin.Imported(source), DateTime.UtcNow);
        writer.Adopt(candidate, games);
        return new ImportResult(level.Id, ImportOutcome.Stored, content.Id, Detail: null);
    }

    private static ImportOutcome OutcomeOf(RemeasureOutcome outcome) => outcome switch
    {
        RemeasureOutcome.AlreadyMeasured => ImportOutcome.AlreadyHeld,
        RemeasureOutcome.Measured => ImportOutcome.Remeasured,
        RemeasureOutcome.Unplayable => ImportOutcome.Unplayable,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
