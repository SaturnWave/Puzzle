using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;

namespace Puzzle.Bank.Storage;

/// <summary>Where a candidate came from. Closed set (private constructor).</summary>
public abstract record CandidateOrigin
{
    private CandidateOrigin()
    {
    }

    /// <summary>Drawn by the grower: draw <paramref name="Draw"/> of the bank, its try <paramref name="Variant"/>, with the drawing code of that version.</summary>
    public sealed record Drawn(long Draw, int Variant, ulong Seed, DrawSpec Spec, int DrawCode) : CandidateOrigin;

    /// <summary>Read from an existing level file.</summary>
    public sealed record Imported(string From) : CandidateOrigin;
}

/// <summary>A candidate as the bank holds it: its content, its play seeds, its place on its board's amount grid, and every batch of games stored for it.</summary>
public sealed class BankCandidate(CandidateContent content, IReadOnlyList<uint> seeds, AmountBase amounts, int amountStep, CandidateOrigin origin, DateTime createdUtc)
{
    private readonly List<StoredGames> _games = [];
    private readonly Dictionary<EpochId, LadderReading> _readings = [];

    public CandidateContent Content { get; } = content;

    /// <summary>The play seeds its games are played on; a batch of games names them by position in this list.</summary>
    public IReadOnlyList<uint> Seeds { get; } = seeds;

    public AmountBase Amounts { get; } = amounts;

    public int AmountStep { get; } = amountStep;

    public CandidateOrigin Origin { get; } = origin;

    public DateTime CreatedUtc { get; } = createdUtc;

    public IReadOnlyList<StoredGames> Games => _games;

    /// <summary>
    /// What the breadth games of <paramref name="epoch"/> say about this candidate. Only games of rungs that are on
    /// the ladder today with the settings they were played with take part.
    /// </summary>
    public LadderReading ReadingIn(EpochId epoch)
    {
        if (!_readings.TryGetValue(epoch, out LadderReading? reading))
        {
            reading = new LadderReading(_games.Where(batch =>
                batch.Header.Epoch == epoch && batch.Header.Purpose == GamePurpose.Breadth && batch.Header.Player.IsOnLadder));
            _readings[epoch] = reading;
        }

        return reading;
    }

    internal void Add(StoredGames games)
    {
        _games.Add(games);
        _readings.Remove(games.Header.Epoch);
    }
}

/// <summary>What the grower's draws came to so far, summed for the status tables.</summary>
public sealed record DrawTotals(long Draws, IReadOnlyDictionary<DrawOutcome, int> Variants, double MeasuredSeconds);

/// <summary>
/// Everything the bank's shards hold, in memory: candidates with their games' finishing moves, the epochs, and the
/// trail of draws. Built by reading the shards and kept current by the process that appends to them.
/// </summary>
public sealed class BankCatalog
{
    private readonly Dictionary<CandidateId, BankCandidate> _candidates = [];
    private readonly HashSet<CandidateId> _retracted = [];
    private readonly List<EpochRecord> _epochs = [];
    private readonly List<DrawRecord> _draws = [];
    private readonly List<StoredGames> _orphanGames = [];

    public IReadOnlyCollection<BankCandidate> Candidates => _candidates.Values;

    public IReadOnlyList<EpochRecord> Epochs => _epochs;

    /// <summary>Every variant of every draw, in the order read.</summary>
    public IReadOnlyList<DrawRecord> Draws => _draws;

    /// <summary>The number the bank's next draw gets: one past the largest stored, so no draw is made twice after a restart.</summary>
    public long NextDrawIndex => _draws.Count == 0 ? 0 : _draws.Max(draw => draw.Draw) + 1;

    public int RetractedCount => _retracted.Count;

    public BankCandidate? Find(CandidateId id) => _candidates.GetValueOrDefault(id);

    public bool Contains(CandidateId id) => _candidates.ContainsKey(id);

    /// <summary>
    /// The epoch of the build whose behaviour fingerprint is <paramref name="fingerprint"/>: the one opened last
    /// under it (its highest generation). Found by value, so two builds can work in one bank, each in its own epoch,
    /// in any order.
    /// </summary>
    public EpochRecord? EpochOf(string fingerprint) =>
        _epochs.Where(epoch => epoch.Fingerprint == fingerprint).MaxBy(epoch => epoch.Generation);

    public DrawTotals DrawTotals => new(
        _draws.Select(draw => draw.Draw).Distinct().LongCount(),
        _draws.GroupBy(draw => draw.Outcome).ToDictionary(outcome => outcome.Key, outcome => outcome.Count()),
        _draws.Where(draw => draw.Outcome == DrawOutcome.Stored).Sum(draw => draw.Seconds));

    public void Add(EpochRecord epoch) => _epochs.Add(epoch);

    public void Add(DrawRecord draw) => _draws.Add(draw);

    /// <summary>Adds a candidate; a second record of one already held (two writers drew the same) changes nothing.</summary>
    public void Add(BankCandidate candidate)
    {
        if (!_retracted.Contains(candidate.Content.Id))
        {
            _candidates.TryAdd(candidate.Content.Id, candidate);
        }
    }

    /// <summary>Adds a batch of games to its candidate. Games whose candidate is not known yet wait for it (<see cref="AdoptWaitingGames"/>).</summary>
    public void Add(StoredGames games)
    {
        if (_candidates.TryGetValue(games.Header.Candidate, out BankCandidate? candidate))
        {
            candidate.Add(games);
        }
        else if (!_retracted.Contains(games.Header.Candidate))
        {
            _orphanGames.Add(games);
        }
    }

    public void Retract(CandidateId id)
    {
        _retracted.Add(id);
        _candidates.Remove(id);
    }

    /// <summary>
    /// Hands the games that were read before their candidate to it: shards are read file by file, and a later tool
    /// may add games for a candidate that lies in another file. Returns how many batches have their candidate in no file.
    /// </summary>
    public int AdoptWaitingGames()
    {
        StoredGames[] waiting = [.. _orphanGames];
        _orphanGames.Clear();
        foreach (StoredGames games in waiting)
        {
            Add(games);
        }

        return _orphanGames.Count;
    }
}
