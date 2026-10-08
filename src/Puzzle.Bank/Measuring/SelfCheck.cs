using Puzzle.Bank.Candidates;
using Puzzle.Bank.Storage;
using Puzzle.Core.Randomness;

namespace Puzzle.Bank.Measuring;

/// <summary>What replaying stored games showed.</summary>
/// <param name="FirstDifferent">The candidate of the first batch that did not replay to its stored bytes; absent when all did.</param>
/// <param name="MeanFinishStored">Mean finishing move of the replayed games as stored (a game that did not finish counts as one move past the cap).</param>
/// <param name="MeanFinishReplayed">The same of the games as played now. Far from the stored mean: the rules changed. Close: the games differ in detail only.</param>
public sealed record ReplayReport(int Batches, int Games, int DifferentGames, CandidateId? FirstDifferent, double MeanFinishStored, double MeanFinishReplayed)
{
    public bool Agrees => DifferentGames == 0;
}

/// <summary>
/// The bank checking itself against its own content. A stored game depends on its level, player, seed and repeat
/// alone, so the running build must play it again to the very same bytes. A probe of hand-made levels
/// (<see cref="MeasurementFingerprint"/>) can miss a rule it does not exercise; the games the bank really holds
/// cannot miss a rule that matters to them.
/// </summary>
public static class SelfCheck
{
    /// <summary>Replays the first <paramref name="seedsPerBatch"/> seeds of every batch and compares every stored byte.</summary>
    /// <exception cref="BankFormatException">A batch is no longer where the catalog found it.</exception>
    /// <exception cref="BankReadException">A shard cannot be read.</exception>
    public static ReplayReport Replay(BankStore store, IEnumerable<(BankCandidate Candidate, StoredGames Games)> batches, int seedsPerBatch)
    {
        int batchCount = 0;
        int games = 0;
        int different = 0;
        long finishStored = 0;
        long finishReplayed = 0;
        CandidateId? firstDifferent = null;
        foreach ((BankCandidate candidate, StoredGames batch) in batches)
        {
            if (batch.Location is not RecordLocation location)
            {
                continue;
            }

            RungGames stored = store.ReadGames(location);
            RungGames replayed = ReplayStart(candidate, stored.Header, seedsPerBatch);
            int stride = RungGames.BytesPerGame(stored.Header.ProgressMarks.Count);
            for (int game = 0; game < replayed.Header.GameCount; game++)
            {
                ReadOnlySpan<byte> was = stored.Bytes.Slice(game * stride, stride);
                ReadOnlySpan<byte> now = replayed.Bytes.Slice(game * stride, stride);
                if (!was.SequenceEqual(now))
                {
                    different++;
                    firstDifferent ??= candidate.Content.Id;
                }

                finishStored += FinishOrPastCap(was[0], stored.Header.MoveCap);
                finishReplayed += FinishOrPastCap(now[0], stored.Header.MoveCap);
            }

            batchCount++;
            games += replayed.Header.GameCount;
        }

        return new ReplayReport(batchCount, games, different, firstDifferent, Mean(finishStored, games), Mean(finishReplayed, games));
    }

    /// <summary>
    /// Batches to replay: every valid batch of up to <paramref name="candidates"/> candidates picked by
    /// <paramref name="dice"/>, so a check covers every rung and, over the runs, the whole bank.
    /// </summary>
    public static IEnumerable<(BankCandidate Candidate, StoredGames Games)> Sample(BankCatalog catalog, EpochId epoch, int candidates, Rng dice)
    {
        BankCandidate[] measured = catalog.Candidates
            .Where(candidate => candidate.Games.Any(batch => batch.Header.Epoch == epoch))
            .OrderBy(candidate => candidate.Content.Id.Value)
            .ToArray();
        for (int pick = 0; pick < Math.Min(candidates, measured.Length); pick++)
        {
            int at = pick + dice.NextInt(measured.Length - pick);
            (measured[pick], measured[at]) = (measured[at], measured[pick]);
            foreach (StoredGames batch in measured[pick].Games.Where(batch => batch.Header.Epoch == epoch))
            {
                yield return (measured[pick], batch);
            }
        }
    }

    private static RungGames ReplayStart(BankCandidate candidate, GamesHeader header, int seeds) => LadderMeasurer.Measure(new MeasurementRequest
    {
        Candidate = candidate.Content,
        Seeds = candidate.Seeds,
        SeedIndices = header.SeedIndices.Take(seeds).ToArray(),
        Players = [header.Player],
        Purpose = header.Purpose,
        RepeatFrom = header.RepeatFrom,
        RepeatCount = header.RepeatCount,
        Settings = new MeasurementSettings { MoveCap = header.MoveCap, ProgressMarks = header.ProgressMarks },
        Epoch = header.Epoch,
    })[0];

    private static int FinishOrPastCap(byte finish, int moveCap) =>
        finish is RungGames.StillPlaying or RungGames.Stuck ? moveCap + 1 : finish;

    private static double Mean(long sum, int count) => count == 0 ? 0 : sum / (double)count;
}
