using Puzzle.Bot;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Tooling.Simulation;

namespace Puzzle.Tooling.Ladder;

/// <summary>How the bots of the skill ladder are measured on a level.</summary>
public sealed record LadderSettings
{
    private readonly int _runs = 60;
    private readonly int _moveCap = 150;

    /// <summary>Games each bot plays on each level. At least 1.</summary>
    public int Runs
    {
        get => _runs;
        init => _runs = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Runs), value, "At least one run is needed.");
    }

    /// <summary>Every game is followed to this many moves, whatever the level's own limit. At least 1.</summary>
    public int MoveCap
    {
        get => _moveCap;
        init => _moveCap = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MoveCap), value, "The move cap must be positive.");
    }

    /// <summary>The seed every level's games are derived from (<see cref="LadderMeasurement.Measure"/>).</summary>
    public ulong Seed { get; init; } = 7;

    /// <summary>
    /// Which games the bots play: fresh seeds (the level as it is on boards nobody picked), or the attempts a player
    /// meets, which run through the level's retry pool.
    /// </summary>
    public BatchGames Games { get; init; } = BatchGames.FreshSeeds;

    /// <summary>Games played at the same time; absent for one per core. The rows do not depend on it.</summary>
    public int? Workers { get; init; }
}

/// <summary>Measures existing levels against the bots of the skill ladder (<see cref="SkillLadder"/>).</summary>
public static class LadderMeasurement
{
    /// <summary>
    /// Plays every level with every bot and reads one row from each pairing. Run i of a level plays the same game for
    /// every bot — the same starting board and the same falling tiles, from <c>Rng.Derive(Rng.Derive(seed, level), i)</c> —
    /// so the bots of a level are compared on the same games, and only their own choices differ. By default these are fresh
    /// 64-bit seeds: the level's retry pool, whose play seeds were picked for their difficulty, plays no part. With
    /// <see cref="BatchGames.PlayerAttempts"/> run i is the level's attempt i instead: the pool's seeds in turn, each
    /// played with bot choices of its own.
    /// </summary>
    /// <returns>
    /// The rows level by level in the order of <paramref name="levels"/>, and within a level in the order of
    /// <paramref name="bots"/>, each handed back as soon as it and every row before it are done. They depend on the
    /// levels, the bots, the runs, the cap, the seed and the kind of games alone.
    /// </returns>
    /// <exception cref="ArgumentException">A level's move limit is past the move cap, so its games would stop before the limit.</exception>
    /// <exception cref="LevelInitializationException">While the rows are read: a level cannot produce a starting board.</exception>
    /// <exception cref="InvalidOperationException">While the rows are read: a bot or the engine failed in a game.</exception>
    public static IEnumerable<LadderRow> Measure(IReadOnlyList<LevelDefinition> levels, IReadOnlyList<SkillProfile> bots, LadderSettings settings)
    {
        if (levels.FirstOrDefault(level => level.MoveLimit > settings.MoveCap) is { } cutShort)
        {
            throw new ArgumentException(
                $"Level {cutShort.Id} has a move limit of {cutShort.MoveLimit}, past the move cap of {settings.MoveCap}.",
                nameof(levels));
        }

        Pairing[] pairings = (from level in levels from bot in bots select new Pairing(level, bot)).ToArray();
        IEnumerable<SimulationBatch> batches = pairings.Select(pairing => pairing.GamesFrom(settings.Seed, settings.Games));
        return pairings.Zip(
            MultiBatchSimulator.Run(batches, settings.Runs, settings.MoveCap, settings.Workers),
            (pairing, report) => LadderRow.From(pairing.Level, pairing.Bot, report));
    }

    /// <summary>One bot on one level.</summary>
    private readonly record struct Pairing(LevelDefinition Level, SkillProfile Bot)
    {
        /// <summary>The bot's games on the level: the seed depends on the level alone, so every bot plays the same games there.</summary>
        public SimulationBatch GamesFrom(ulong seed, BatchGames games)
        {
            SkillProfile bot = Bot;
            return new SimulationBatch(Level, () => new GreedyPolicy(bot), Rng.Derive(seed, (ulong)Level.Id.Value), games);
        }
    }
}
