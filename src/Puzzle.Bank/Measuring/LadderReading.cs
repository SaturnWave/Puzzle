using Puzzle.Bot;

namespace Puzzle.Bank.Measuring;

/// <summary>Stored games as the bank keeps them in memory: who played what, and the move each game finished on.</summary>
/// <param name="Finishes">One byte per game, in stored order (seed by seed): 1..cap, <see cref="RungGames.StillPlaying"/> or <see cref="RungGames.Stuck"/>.</param>
/// <param name="Location">Where the full record lies, for a reader that wants more than the finishing moves; absent for games that are not in the bank (a probe).</param>
public sealed record StoredGames(GamesHeader Header, byte[] Finishes, RecordLocation? Location);

/// <summary>A record's place in the bank: the shard file's name and the offset of the record's frame.</summary>
public sealed record RecordLocation(string File, long Offset);

/// <summary>How hard one seed of a candidate is, pooled over the rungs: the share of its games won, each rung judged at its own limit.</summary>
public readonly record struct SeedEstimate(int SeedIndex, double Share, int Games);

/// <summary>
/// What the ladder's breadth games say about one candidate in one epoch: each rung's win curve, and from them the
/// readings a draft needs without playing a game: the limit that gives a player a win share, the share a limit
/// gives, and how the candidate's seeds compare.
/// </summary>
public sealed class LadderReading
{
    /// <summary>Limits of the rungs up to this many skill points on either side of the player are averaged into a draft limit.</summary>
    private const int SmoothingReach = 10;

    private const double ShareTolerance = 1e-9;

    private readonly Dictionary<string, List<StoredGames>> _batchesByRung;
    private readonly Dictionary<string, WinCurve> _curves;

    /// <param name="breadth">A candidate's breadth games of one epoch, every batch played by a rung of today's ladder.</param>
    public LadderReading(IEnumerable<StoredGames> breadth)
    {
        _batchesByRung = breadth.GroupBy(batch => batch.Header.Player.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(rung => rung.Key, rung => rung.ToList(), StringComparer.OrdinalIgnoreCase);
        _curves = _batchesByRung.ToDictionary(
            rung => rung.Key,
            rung => WinCurve.Of(rung.Value.SelectMany(batch => batch.Finishes).ToArray(), rung.Value.Min(batch => batch.Header.MoveCap)),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Rungs => _curves.Keys;

    public int Games => _curves.Values.Sum(curve => curve.Games);

    public int Stuck => _curves.Values.Sum(curve => curve.Stuck);

    /// <summary>Measured, and not stuck more often than <see cref="StuckRule"/> tolerates.</summary>
    public bool IsUsable => Games > 0 && StuckRule.Tolerates(Stuck, Games);

    public WinCurve? CurveOf(string rung) => _curves.GetValueOrDefault(rung);

    /// <summary>The share of its games <paramref name="player"/> wins within <paramref name="limit"/>; absent when a rung of the mix was not measured.</summary>
    public double? ShareAt(PlayerMix player, int limit)
    {
        double share = 0;
        foreach (MixPart part in player.Parts)
        {
            if (CurveOf(part.RungName) is not WinCurve curve)
            {
                return null;
            }

            share += part.Weight * curve.ShareAt(limit);
        }

        return share;
    }

    /// <summary>
    /// The smallest limit at which <paramref name="player"/> wins <paramref name="share"/> of its games, read from
    /// the player's own curve; absent when a rung was not measured or the share is out of reach within the move cap.
    /// </summary>
    public int? LimitFor(PlayerMix player, double share)
    {
        if (player.IsSingleRung)
        {
            return CurveOf(player.Parts[0].RungName)?.LimitFor(share);
        }

        if (player.Parts.Any(part => CurveOf(part.RungName) is null))
        {
            return null;
        }

        int moveCap = player.Parts.Min(part => _curves[part.RungName].MoveCap);
        for (int limit = 1; limit <= moveCap; limit++)
        {
            if (ShareAt(player, limit) >= share - ShareTolerance)
            {
                return limit;
            }
        }

        return null;
    }

    /// <summary>
    /// The limit to expect for <paramref name="player"/> before any confirm game: the mean of
    /// <see cref="LimitFor"/> over the player and the same player moved 5 and 10 points down and up the ladder, as
    /// far as both sides were measured and reach the share. One rung's 128 games put a limit one to two moves off;
    /// the neighbours' limits lie nearly on a line through it and halve that. A draft only: it runs about half a
    /// move low, and a level's limit is always read from its own confirm games.
    /// </summary>
    public int? DraftLimit(PlayerMix player, double share)
    {
        if (LimitFor(player, share) is not int own)
        {
            return null;
        }

        var limits = new List<int> { own };
        for (int reach = SkillLadder.Step; reach <= SmoothingReach; reach += SkillLadder.Step)
        {
            if (NeighbourLimit(player, -reach, share) is not int weaker || NeighbourLimit(player, reach, share) is not int stronger)
            {
                break;
            }

            limits.Add(weaker);
            limits.Add(stronger);
        }

        return (int)Math.Round(limits.Average(), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Every measured seed's difficulty for a player who should win <paramref name="share"/>, pooled over the rungs:
    /// the share of the seed's games that each rung finished within that rung's own limit for the share. The rungs
    /// rank a candidate's seeds alike, so thirty pooled games tell more of a seed than its two games by one rung.
    /// A rung that never reaches the share within the cap has no limit and takes no part.
    /// </summary>
    public IReadOnlyList<SeedEstimate> SeedShares(double share)
    {
        var won = new SortedDictionary<int, (int Won, int Games)>();
        foreach ((string rung, List<StoredGames> batches) in _batchesByRung)
        {
            if (_curves[rung].LimitFor(share) is not int limit)
            {
                continue;
            }

            foreach (StoredGames batch in batches)
            {
                for (int game = 0; game < batch.Finishes.Length; game++)
                {
                    int seedIndex = batch.Header.SeedIndices[game / batch.Header.RepeatCount];
                    byte finish = batch.Finishes[game];
                    bool wonWithin = finish != RungGames.StillPlaying && finish != RungGames.Stuck && finish <= limit;
                    (int wins, int games) = won.GetValueOrDefault(seedIndex);
                    won[seedIndex] = (wins + (wonWithin ? 1 : 0), games + 1);
                }
            }
        }

        return won.Select(seed => new SeedEstimate(seed.Key, seed.Value.Won / (double)seed.Value.Games, seed.Value.Games)).ToArray();
    }

    private int? NeighbourLimit(PlayerMix player, int points, double share) =>
        player.Shifted(points) is PlayerMix neighbour ? LimitFor(neighbour, share) : null;
}
