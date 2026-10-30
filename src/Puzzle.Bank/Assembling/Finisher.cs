using System.Globalization;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Bot;
using Puzzle.Generator.Profiles;

namespace Puzzle.Bank.Assembling;

/// <summary>The seed pool a level gets: how many seeds, how few at least, and how near the level's own win share a seed's must be.</summary>
/// <param name="Tolerance">As a share: 0.07 lets a seed in that plays within seven points of the level.</param>
public sealed record PoolRules(int Size, int MinSize, double Tolerance)
{
    public static PoolRules Of(GenerationProfile plan) => new(plan.SeedPool.PoolSize, plan.MinPoolSize, plan.SeedPool.TolerancePoints / 100.0);
}

/// <summary>A level whose limit, seed pool and audit are done: everything its file and its report row are written from.</summary>
public sealed record FinishedLevel
{
    /// <summary>The limit the confirm games gave before any audit; <see cref="CalibratedLimit"/> differs when a failed audit moved it.</summary>
    public required int ConfirmLimit { get; init; }

    /// <summary>The smallest limit at which the player reaches the wanted share on the games the limit was read from.</summary>
    public required int CalibratedLimit { get; init; }

    /// <summary>The limit the level file carries: the calibrated one plus the move margin.</summary>
    public required int Limit { get; init; }

    /// <summary>The pool, as positions in the candidate's seed list; the first is the seed of every first attempt.</summary>
    public required IReadOnlyList<int> PoolSeeds { get; init; }

    /// <summary>The share the audit was held against: the policy's share, or with a move margin what the extra moves make of it.</summary>
    public required double AuditTarget { get; init; }

    /// <summary>Audits run, the passed one included.</summary>
    public required int Audits { get; init; }

    /// <summary>Games of the passed audit: none of them was read by any choice.</summary>
    public required int AuditGames { get; init; }

    /// <summary>The player's win share at <see cref="Limit"/> in the passed audit, every pool seed counting alike.</summary>
    public required double Win { get; init; }

    /// <summary>The same with the "+5 moves" offer; absent past the move cap.</summary>
    public required double? WinPlus5 { get; init; }

    public required int? MovesP50 { get; init; }

    public required int? MovesP90 { get; init; }

    public required double NearMiss { get; init; }

    public required double PoolMinWin { get; init; }

    public required double PoolMaxWin { get; init; }

    /// <summary>The win share on the first pool seed alone, in <see cref="FirstTryGames"/> audit games.</summary>
    public required double FirstTryWin { get; init; }

    public required int FirstTryGames { get; init; }

    /// <summary>
    /// The win share at <see cref="Limit"/> in the games played after the audit had passed, every pool seed counting
    /// alike; absent when the policy asks for no such games. They took no part in accepting the level.
    /// </summary>
    public required double? CheckWin { get; init; }

    public required int CheckGames { get; init; }

    /// <summary>The win share a report states: from the games after the audit where there are any, otherwise from the passed audit.</summary>
    public double ReportedWin => CheckWin ?? Win;
}

/// <summary>What the fresh games made of a chosen candidate. Closed set (private constructor).</summary>
public abstract record FinishOutcome
{
    private FinishOutcome()
    {
    }

    public sealed record Accepted(FinishedLevel Level) : FinishOutcome;

    /// <summary>The candidate does not become this level; <paramref name="Reason"/> says what the games showed.</summary>
    public sealed record Rejected(string Reason) : FinishOutcome;
}

/// <summary>
/// Turns a chosen candidate into a level, on games that are deeper and fresher than the ones it was chosen by.
/// <list type="number">
/// <item>Confirm: the level's player plays the candidate's seeds nearest the wanted share many times. The limit is
/// the smallest at which those games reach the share; a limit outside the move range ends it here.</item>
/// <item>Pool: the seeds whose own share at that limit is nearest the level's, within the pool's tolerance.</item>
/// <item>Audit: the player plays the pool on repeats nothing has read. The level stands when the pool's share is
/// within the tolerance of the wanted one and the first seed's (every player's first attempt) within its own.
/// A failed audit is no longer a check: its games join the ones the limit is read from, a first seed that is off
/// leaves the pool (the nearest confirmed seed takes a place the pool cannot spare), and the next audit takes new
/// repeats. After the last failed audit the candidate is turned down.</item>
/// <item>Check: a passed audit is the one that passed, so its share leans toward the target. The pool is played once
/// more on repeats no audit uses, and the win share a report states is read from those games alone.</item>
/// </list>
/// Choosing the best-looking of many candidates favours lucky measurements; the audit is where that luck ends.
/// Seeds people have recorded attempts on (<see cref="LevelSlot.RecordedSeeds"/>) are confirmed whatever the stored
/// games say of them and stay in the pool, first in it, while they play within the pool's tolerance of the level.
/// </summary>
public sealed class Finisher(IGameSource source, CheckSettings checks, PoolRules pool, double auditTolerance, EpochId epoch, int moveCap)
{
    public FinishOutcome Finish(LevelSlot slot, BankCandidate candidate)
    {
        SlotDifficulty ask = (slot.Difficulty ?? throw new InvalidOperationException($"Level {slot.Number} is pinned; it has nothing to finish.")).For(candidate.Content.Features);
        HashSet<int> recorded = RecordedSeedsOf(slot, candidate);
        int[] shortlist = Shortlist(candidate, ask.Share, recorded);
        if (shortlist.Length < pool.MinSize)
        {
            return new FinishOutcome.Rejected($"its stored games rank only {shortlist.Length} seeds, fewer than a pool of {pool.MinSize}");
        }

        PlayerGames fit = Play(candidate, ask.Player, GamePurpose.Confirm, shortlist, GamePurpose.Confirm.FirstRepeat(), checks.ConfirmGamesPerSeed);
        if (Reading.Of(fit, shortlist, ask, moveCap) is not Reading confirmed)
        {
            return new FinishOutcome.Rejected(Reading.WhyNone(fit, shortlist, ask, "confirm"));
        }

        (List<int> poolSeeds, Queue<int> reserve) = PoolAround(fit, shortlist, confirmed, recorded);
        Reading reading = confirmed;
        for (int round = 0; round < checks.AuditRounds; round++)
        {
            Audit audit = RunAudit(candidate, ask.Player, poolSeeds, round);
            double win = audit.Games.ShareAt(poolSeeds, reading.Limit);
            double firstTry = audit.FirstSeed.ShareAt([poolSeeds[0]], reading.Limit);
            bool firstSeedHolds = Math.Abs(firstTry - reading.AuditTarget) <= checks.FirstTryTolerance + ShareEpsilon;
            if (firstSeedHolds && Math.Abs(win - reading.AuditTarget) <= auditTolerance + ShareEpsilon)
            {
                return new FinishOutcome.Accepted(Finished(confirmed, reading, poolSeeds, audit, round + 1, CheckOf(candidate, ask.Player, poolSeeds)));
            }

            fit.Add(audit.Games);
            if (!firstSeedHolds && !Replace(poolSeeds, reserve))
            {
                return new FinishOutcome.Rejected($"audit {round + 1} left fewer than {pool.MinSize} seeds for the pool");
            }

            if (Reading.Of(fit, poolSeeds, ask, moveCap) is not Reading again)
            {
                return new FinishOutcome.Rejected(Reading.WhyNone(fit, poolSeeds, ask, $"audit {round + 1}"));
            }

            reading = again;
        }

        return new FinishOutcome.Rejected(string.Create(CultureInfo.InvariantCulture, $"its audits missed the share {reading.AuditTarget:0.###} {checks.AuditRounds} times"));
    }

    private const double ShareEpsilon = 1e-9;

    /// <summary>Where the seeds people recorded attempts on lie in the candidate's seed list; a seed the candidate was not measured on is not among them.</summary>
    private static HashSet<int> RecordedSeedsOf(LevelSlot slot, BankCandidate candidate)
    {
        var positions = new HashSet<int>();
        for (int position = 0; position < candidate.Seeds.Count; position++)
        {
            if (slot.RecordedSeeds?.Contains(candidate.Seeds[position]) == true)
            {
                positions.Add(position);
            }
        }

        return positions;
    }

    /// <summary>
    /// The seeds worth playing in depth: the recorded ones, then those the stored games of the whole ladder put
    /// nearest the wanted share (every rung judged at its own limit), which is where a pool of equal difficulty
    /// will come from.
    /// </summary>
    private int[] Shortlist(BankCandidate candidate, double share, HashSet<int> recorded) => candidate.ReadingIn(epoch).SeedShares(share)
        .OrderBy(seed => recorded.Contains(seed.SeedIndex) ? 0 : 1)
        .ThenBy(seed => Math.Abs(seed.Share - share))
        .ThenBy(seed => seed.SeedIndex)
        .Take(checks.ConfirmSeeds)
        .Select(seed => seed.SeedIndex)
        .Order()
        .ToArray();

    /// <summary>
    /// The pool: the seeds nearest the level's share at its limit, nearest first, within the tolerance. Retries must
    /// not run out of patterns, so a pool that would be smaller than the minimum takes the nearest seeds regardless.
    /// The seeds left over wait in reserve, nearest first, for a place a pool seed gives up. A recorded seed within
    /// the tolerance comes before every other: the first attempts people played stay first attempts.
    /// </summary>
    private (List<int> Pool, Queue<int> Reserve) PoolAround(PlayerGames fit, int[] seeds, Reading reading, HashSet<int> recorded)
    {
        (int Seed, double Off)[] nearestFirst = seeds
            .Select(seed => (Seed: seed, Off: Math.Abs(fit.ShareAt([seed], reading.Limit) - reading.PoolTarget)))
            .OrderBy(seed => recorded.Contains(seed.Seed) && seed.Off <= pool.Tolerance + ShareEpsilon ? 0 : 1)
            .ThenBy(seed => seed.Off)
            .ThenBy(seed => seed.Seed)
            .ToArray();
        int size = Math.Clamp(nearestFirst.Count(seed => seed.Off <= pool.Tolerance + ShareEpsilon), pool.MinSize, pool.Size);
        return (nearestFirst.Take(size).Select(seed => seed.Seed).ToList(), new Queue<int>(nearestFirst.Skip(size).Select(seed => seed.Seed)));
    }

    /// <summary>
    /// Takes the first seed out of the pool: its own audit games showed it plays unlike the level. A pool that is
    /// then smaller than the minimum takes the nearest seed in reserve; false when none is left to take.
    /// </summary>
    private bool Replace(List<int> poolSeeds, Queue<int> reserve)
    {
        poolSeeds.RemoveAt(0);
        if (poolSeeds.Count < pool.MinSize && reserve.TryDequeue(out int next))
        {
            poolSeeds.Add(next);
        }

        return poolSeeds.Count >= pool.MinSize;
    }

    /// <summary>One audit: every pool seed on repeats of its own round, the first seed on many more of them.</summary>
    private Audit RunAudit(BankCandidate candidate, PlayerMix player, List<int> poolSeeds, int round)
    {
        int repeatFrom = GamePurpose.Audit.FirstRepeat() + (round * checks.FirstSeedGames);
        PlayerGames firstSeed = Play(candidate, player, GamePurpose.Audit, [poolSeeds[0]], repeatFrom, checks.FirstSeedGames);
        PlayerGames games = Play(candidate, player, GamePurpose.Audit, poolSeeds.Skip(1).ToArray(), repeatFrom, checks.AuditGamesPerSeed);
        games.Add(firstSeed);
        return new Audit(games, firstSeed);
    }

    /// <summary>The games after a passed audit, on repeats past every audit round; absent when the policy asks for none.</summary>
    private PlayerGames? CheckOf(BankCandidate candidate, PlayerMix player, List<int> poolSeeds)
    {
        if (checks.CheckGamesPerSeed == 0)
        {
            return null;
        }

        int repeatFrom = GamePurpose.Audit.FirstRepeat() + (checks.AuditRounds * checks.FirstSeedGames);
        return Play(candidate, player, GamePurpose.Audit, poolSeeds.ToArray(), repeatFrom, checks.CheckGamesPerSeed);
    }

    private PlayerGames Play(BankCandidate candidate, PlayerMix player, GamePurpose purpose, int[] seeds, int repeatFrom, int repeatCount)
    {
        var games = new PlayerGames(player);
        for (int part = 0; part < player.Parts.Count && seeds.Length > 0; part++)
        {
            games.Add(part, source.Games(candidate, RungOf(player.Parts[part]), purpose, seeds, repeatFrom, repeatCount));
        }

        return games;
    }

    private static PlayerModel RungOf(MixPart part) => SkillLadder.TryFind(part.RungName, out SkillProfile? rung)
        ? PlayerModel.Of(rung)
        : throw new InvalidOperationException($"{part.RungName} is not on the ladder (a player mix only holds rungs).");

    private FinishedLevel Finished(Reading confirmed, Reading reading, List<int> poolSeeds, Audit audit, int audits, PlayerGames? check)
    {
        double[] seedWins = poolSeeds.Select(seed => audit.Games.ShareAt([seed], reading.Limit)).ToArray();
        return new FinishedLevel
        {
            ConfirmLimit = confirmed.Calibrated,
            CalibratedLimit = reading.Calibrated,
            Limit = reading.Limit,
            PoolSeeds = poolSeeds.ToArray(),
            AuditTarget = reading.AuditTarget,
            Audits = audits,
            AuditGames = audit.Games.GameCount,
            Win = seedWins.Average(),
            WinPlus5 = reading.Limit + Core.Levels.ExtraMovesOffer.Moves <= moveCap ? audit.Games.ShareAt(poolSeeds, reading.Limit + Core.Levels.ExtraMovesOffer.Moves) : null,
            MovesP50 = audit.Games.LimitFor(poolSeeds, 0.5, moveCap),
            MovesP90 = audit.Games.LimitFor(poolSeeds, 0.9, moveCap),
            NearMiss = audit.Games.NearMissAt(poolSeeds, reading.Limit),
            PoolMinWin = seedWins.Min(),
            PoolMaxWin = seedWins.Max(),
            FirstTryWin = audit.FirstSeed.ShareAt([poolSeeds[0]], reading.Limit),
            FirstTryGames = audit.FirstSeed.GameCount,
            CheckWin = check?.ShareAt(poolSeeds, reading.Limit),
            CheckGames = check?.GameCount ?? 0,
        };
    }

    private sealed record Audit(PlayerGames Games, PlayerGames FirstSeed);

    /// <summary>A limit read from games, and the shares that go with it.</summary>
    /// <param name="Calibrated">The smallest limit at which the games reach the wanted share.</param>
    /// <param name="Limit">The final limit: calibrated plus the move margin, no further than the games were followed.</param>
    /// <param name="PoolTarget">The share the games show at the final limit: what a pool seed should play like.</param>
    /// <param name="AuditTarget">What an audit must find: the wanted share itself without a margin, the pool target with one.</param>
    private sealed record Reading(int Calibrated, int Limit, double PoolTarget, double AuditTarget)
    {
        /// <summary>Absent when the games never reach the share within the move cap, or reach it outside the move range.</summary>
        public static Reading? Of(PlayerGames games, IReadOnlyList<int> seeds, SlotDifficulty ask, int moveCap)
        {
            if (games.LimitFor(seeds, ask.Share, moveCap) is not int calibrated || !ask.Moves.Contains(calibrated))
            {
                return null;
            }

            int limit = Math.Min(ask.Margin.Soften(calibrated), moveCap);
            double poolTarget = games.ShareAt(seeds, limit);
            return new Reading(calibrated, limit, poolTarget, ask.Margin.IsNone ? ask.Share : poolTarget);
        }

        public static string WhyNone(PlayerGames games, IReadOnlyList<int> seeds, SlotDifficulty ask, string stage) =>
            games.LimitFor(seeds, ask.Share, int.MaxValue) is int limit
                ? string.Create(CultureInfo.InvariantCulture, $"the {stage} games put its limit at {limit}, outside {ask.Moves}")
                : string.Create(CultureInfo.InvariantCulture, $"in the {stage} games {ask.Player.Label} never wins {ask.Share:0.###} of them within the move cap");
    }
}

/// <summary>
/// The games of a level's player (a rung, or a mix of rungs) on seeds of a candidate, gathered batch by batch. Every
/// seed counts alike whatever its number of games, because a player meets the pool's seeds equally often; within a
/// seed, a mix wins the weighted share of its rungs' wins.
/// </summary>
internal sealed class PlayerGames(PlayerMix player)
{
    private readonly Dictionary<int, SeedTally[]> _bySeed = [];

    public int GameCount => _bySeed.Values.Sum(parts => parts[0].Finishes.Count);

    public void Add(int part, SeedGames games)
    {
        for (int position = 0; position < games.SeedIndices.Count; position++)
        {
            SeedTally tally = TalliesOf(games.SeedIndices[position])[part];
            tally.Finishes.AddRange(games.FinishesAt(position));
            tally.NearMarks.AddRange(games.NearMarksAt(position));
        }
    }

    public void Add(PlayerGames other)
    {
        foreach ((int seed, SeedTally[] parts) in other._bySeed)
        {
            SeedTally[] own = TalliesOf(seed);
            for (int part = 0; part < parts.Length; part++)
            {
                own[part].Finishes.AddRange(parts[part].Finishes);
                own[part].NearMarks.AddRange(parts[part].NearMarks);
            }
        }
    }

    /// <summary>The share of games won within <paramref name="limit"/>, averaged over <paramref name="seeds"/>.</summary>
    public double ShareAt(IReadOnlyCollection<int> seeds, int limit) => seeds.Average(seed => OverParts(seed, tally => tally.WonWithin(limit)));

    /// <summary>The share of games lost at <paramref name="limit"/> that had the near-miss share of their goals done by then.</summary>
    public double NearMissAt(IReadOnlyCollection<int> seeds, int limit) => seeds.Average(seed => OverParts(seed, tally => tally.NearMissesAt(limit)));

    /// <summary>The smallest limit up to <paramref name="moveCap"/> at which <see cref="ShareAt"/> reaches <paramref name="share"/>; absent when none does.</summary>
    public int? LimitFor(IReadOnlyCollection<int> seeds, double share, int moveCap)
    {
        for (int limit = 1; limit <= Math.Min(moveCap, RungGames.LargestMoveCap); limit++)
        {
            if (ShareAt(seeds, limit) >= share - 1e-9)
            {
                return limit;
            }
        }

        return null;
    }

    private double OverParts(int seed, Func<SeedTally, double> shareOf)
    {
        SeedTally[] parts = _bySeed[seed];
        double share = 0;
        for (int part = 0; part < parts.Length; part++)
        {
            share += player.Parts[part].Weight * shareOf(parts[part]);
        }

        return share;
    }

    private SeedTally[] TalliesOf(int seed)
    {
        if (!_bySeed.TryGetValue(seed, out SeedTally[]? parts))
        {
            _bySeed[seed] = parts = player.Parts.Select(_ => new SeedTally()).ToArray();
        }

        return parts;
    }

    /// <summary>One rung's games on one seed.</summary>
    private sealed class SeedTally
    {
        public List<byte> Finishes { get; } = [];

        public List<byte> NearMarks { get; } = [];

        public double WonWithin(int limit) => Finishes.Count(finish => Won(finish, limit)) / (double)Finishes.Count;

        public double NearMissesAt(int limit)
        {
            int nearMisses = 0;
            for (int game = 0; game < Finishes.Count; game++)
            {
                if (!Won(Finishes[game], limit) && NearMarks[game] != RungGames.NeverReached && NearMarks[game] <= limit)
                {
                    nearMisses++;
                }
            }

            return nearMisses / (double)Finishes.Count;
        }

        private static bool Won(byte finish, int limit) => finish != RungGames.StillPlaying && finish != RungGames.Stuck && finish <= limit;
    }
}
