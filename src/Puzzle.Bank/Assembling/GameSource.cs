using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;

namespace Puzzle.Bank.Assembling;

/// <summary>
/// Games of one rung on some seeds of a candidate, the same number on each: for every game the move it finished on
/// and the move on which it first had the near-miss share of its goals done, as <see cref="RungGames"/> codes them.
/// </summary>
public sealed class SeedGames
{
    private readonly byte[] _finishes;
    private readonly byte[] _nearMarks;

    /// <param name="seedIndices">Positions in the candidate's seed list, in the order the games are given.</param>
    /// <param name="finishes">Seed by seed, <paramref name="repeatCount"/> games each.</param>
    /// <param name="nearMarks">The move on which each game first reached <see cref="NearMissPercent"/> of its goals, or <see cref="RungGames.NeverReached"/>.</param>
    public SeedGames(IReadOnlyList<int> seedIndices, int repeatCount, byte[] finishes, byte[] nearMarks)
    {
        if (finishes.Length != seedIndices.Count * repeatCount || nearMarks.Length != finishes.Length)
        {
            throw new ArgumentException($"{finishes.Length} finishes and {nearMarks.Length} marks do not hold {seedIndices.Count} seeds of {repeatCount} games.", nameof(finishes));
        }

        SeedIndices = seedIndices;
        RepeatCount = repeatCount;
        _finishes = finishes;
        _nearMarks = nearMarks;
    }

    /// <summary>The share of a level's goals a lost game must have reached to count as a near miss, as the reports define it.</summary>
    public const int NearMissPercent = 80;

    public IReadOnlyList<int> SeedIndices { get; }

    public int RepeatCount { get; }

    /// <summary>The finishing moves of the games on the seed at <paramref name="position"/> of <see cref="SeedIndices"/>.</summary>
    public ReadOnlySpan<byte> FinishesAt(int position) => _finishes.AsSpan(position * RepeatCount, RepeatCount);

    public ReadOnlySpan<byte> NearMarksAt(int position) => _nearMarks.AsSpan(position * RepeatCount, RepeatCount);
}

/// <summary>
/// Where an assembly gets the games that confirm and audit a level. Asking twice for the same games gives the same
/// games: a game is fixed by its candidate, player, seed and repeat.
/// </summary>
public interface IGameSource
{
    /// <summary>The games of <paramref name="rung"/> on the named seeds of <paramref name="candidate"/>, repeats <paramref name="repeatFrom"/> onward.</summary>
    /// <exception cref="BankWriteException">Games were played and could not be stored.</exception>
    SeedGames Games(BankCandidate candidate, PlayerModel rung, GamePurpose purpose, IReadOnlyList<int> seedIndices, int repeatFrom, int repeatCount);
}

/// <summary>
/// The bank as a source of games: what it holds is read, what it lacks is played and stored, so the next assembly
/// that asks reads it. That is what makes a second assembly a matter of seconds.
/// </summary>
public sealed class BankGames(BankStore store, BankWriter writer, EpochId epoch, MeasurementSettings settings) : IGameSource
{
    /// <summary>A stored batch counts its repeats in one byte; longer runs go out as several batches.</summary>
    private const int LargestBatchRepeats = 128;

    public long GamesRead { get; private set; }

    public long GamesPlayed { get; private set; }

    public SeedGames Games(BankCandidate candidate, PlayerModel rung, GamePurpose purpose, IReadOnlyList<int> seedIndices, int repeatFrom, int repeatCount)
    {
        Dictionary<(int Seed, int Repeat), (byte Finish, byte NearMark)> held = Held(candidate, rung, purpose);
        int[] lacking = seedIndices
            .Where(seed => Enumerable.Range(repeatFrom, repeatCount).Any(repeat => !held.ContainsKey((seed, repeat))))
            .ToArray();
        GamesRead += (long)(seedIndices.Count - lacking.Length) * repeatCount;
        for (int from = repeatFrom; lacking.Length > 0 && from < repeatFrom + repeatCount; from += LargestBatchRepeats)
        {
            RungGames played = Play(candidate, rung, purpose, lacking, from, Math.Min(LargestBatchRepeats, repeatFrom + repeatCount - from));
            writer.Add([played]);
            Note(played, held);
            GamesPlayed += played.Header.GameCount;
        }

        var finishes = new byte[seedIndices.Count * repeatCount];
        var nearMarks = new byte[finishes.Length];
        for (int game = 0; game < finishes.Length; game++)
        {
            (finishes[game], nearMarks[game]) = held[(seedIndices[game / repeatCount], repeatFrom + (game % repeatCount))];
        }

        return new SeedGames(seedIndices, repeatCount, finishes, nearMarks);
    }

    /// <summary>Every game of that purpose the bank holds of the rung on the candidate in this epoch, by seed and repeat.</summary>
    private Dictionary<(int Seed, int Repeat), (byte Finish, byte NearMark)> Held(BankCandidate candidate, PlayerModel rung, GamePurpose purpose)
    {
        var held = new Dictionary<(int, int), (byte, byte)>();
        foreach (StoredGames batch in candidate.Games)
        {
            if (batch.Header.Epoch == epoch && batch.Header.Purpose == purpose && batch.Header.Player == rung && batch.Location is RecordLocation location)
            {
                Note(store.ReadGames(location), held);
            }
        }

        return held;
    }

    private static void Note(RungGames batch, Dictionary<(int Seed, int Repeat), (byte Finish, byte NearMark)> held)
    {
        GamesHeader header = batch.Header;
        int nearMark = Array.IndexOf(header.ProgressMarks.ToArray(), SeedGames.NearMissPercent);
        byte[] finishes = batch.Finishes();
        for (int game = 0; game < finishes.Length; game++)
        {
            (int seed, int repeat) = (header.SeedIndices[game / header.RepeatCount], header.RepeatFrom + (game % header.RepeatCount));
            held[(seed, repeat)] = (finishes[game], nearMark >= 0 ? batch.ReachedAt(game, nearMark) : RungGames.NeverReached);
        }
    }

    private RungGames Play(BankCandidate candidate, PlayerModel rung, GamePurpose purpose, int[] seedIndices, int repeatFrom, int repeatCount) =>
        LadderMeasurer.Measure(new MeasurementRequest
        {
            Candidate = candidate.Content,
            Seeds = candidate.Seeds,
            SeedIndices = seedIndices,
            Players = [rung],
            Purpose = purpose,
            RepeatFrom = repeatFrom,
            RepeatCount = repeatCount,
            Settings = settings,
            Epoch = epoch,
        })[0];
}
