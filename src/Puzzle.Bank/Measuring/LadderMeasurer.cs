using System.Runtime.ExceptionServices;
using Puzzle.Bank.Candidates;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;

namespace Puzzle.Bank.Measuring;

/// <summary>Which games of a candidate to play: the players, the seeds (by position in the candidate's seed list) and the bot repeats.</summary>
public sealed record MeasurementRequest
{
    public required CandidateContent Candidate { get; init; }

    /// <summary>The candidate's whole seed list; <see cref="SeedIndices"/> point into it.</summary>
    public required IReadOnlyList<uint> Seeds { get; init; }

    public required IReadOnlyList<int> SeedIndices { get; init; }

    public required IReadOnlyList<PlayerModel> Players { get; init; }

    public required GamePurpose Purpose { get; init; }

    public required int RepeatFrom { get; init; }

    public required int RepeatCount { get; init; }

    public required MeasurementSettings Settings { get; init; }

    public required EpochId Epoch { get; init; }
}

/// <summary>How far games are followed and what is kept of them.</summary>
public sealed record MeasurementSettings
{
    public static MeasurementSettings Default { get; } = new();

    /// <summary>Twice the longest limit a move range allows, so a rung far below a level's player still shows where it would finish.</summary>
    public int MoveCap { get; init; } = 150;

    /// <summary>Half way, the near-miss line the reports use (80 %), and nearly there.</summary>
    public IReadOnlyList<int> ProgressMarks { get; init; } = [50, 80, 90];
}

/// <summary>
/// Plays a candidate's games across the CPU cores and hands back one batch per player. The game of a seed and repeat
/// is <c>SimulationRunner.Play(level, GreedyPolicy(player), AttemptSeeds.From(seed), Rng.Derive(seed, repeat), cap)</c>:
/// the start board and falling tiles a pool seed gives a player, and the bot randomness of the seed-pool calibrator,
/// the same for every player. A game depends on nothing else, so it can be played again and compared.
/// </summary>
public static class LadderMeasurer
{
    /// <exception cref="ArgumentOutOfRangeException">The move cap does not fit a stored finishing move, or the repeats leave their purpose's range.</exception>
    /// <exception cref="LevelInitializationException">The candidate cannot produce a starting board.</exception>
    public static RungGames[] Measure(MeasurementRequest request)
    {
        Validate(request);
        int marks = request.Settings.ProgressMarks.Count;
        int stride = RungGames.BytesPerGame(marks);
        int gamesPerPlayer = request.SeedIndices.Count * request.RepeatCount;
        byte[][] games = request.Players.Select(_ => new byte[gamesPerPlayer * stride]).ToArray();
        SkillProfile[] profiles = request.Players.Select(player => player.ToProfile()).ToArray();
        LevelDefinition level = request.Candidate.Level;

        try
        {
            Parallel.For(
                0,
                profiles.Length * gamesPerPlayer,
                () => profiles.Select(profile => new GreedyPolicy(profile)).ToArray(), // policies keep scratch buffers: one set per thread
                (job, _, policies) =>
                {
                    int player = job / gamesPerPlayer;
                    int game = job % gamesPerPlayer;
                    uint seed = request.Seeds[request.SeedIndices[game / request.RepeatCount]];
                    int repeat = request.RepeatFrom + (game % request.RepeatCount);
                    RunResult run = Play(level, policies[player], seed, repeat, request.Settings.MoveCap);
                    RungGames.Record(run, request.Settings.ProgressMarks, games[player].AsSpan(game * stride, stride));
                    return policies;
                },
                _ => { });
        }
        catch (AggregateException ex) when (ex.InnerExceptions.OfType<LevelInitializationException>().FirstOrDefault() is { } unplayable)
        {
            // Every worker fails alike on a board that cannot start; the caller wants that one reason, not the bundle.
            ExceptionDispatchInfo.Capture(unplayable).Throw();
        }

        return request.Players.Select((player, index) => new RungGames(HeaderFor(request, player), games[index])).ToArray();
    }

    /// <summary>One game of the convention above.</summary>
    public static RunResult Play(LevelDefinition level, IBotPolicy policy, uint seed, int repeat, int moveCap) =>
        SimulationRunner.Play(level, policy, AttemptSeeds.From(seed), Rng.Derive(seed, (ulong)repeat), moveCap);

    private static void Validate(MeasurementRequest request)
    {
        if (request.Settings.MoveCap is < 1 or > RungGames.LargestMoveCap)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Settings.MoveCap, $"The move cap must be 1..{RungGames.LargestMoveCap}.");
        }

        int first = request.Purpose.FirstRepeat();
        if (request.RepeatCount < 1 || request.RepeatFrom < first || request.RepeatFrom + request.RepeatCount > first + GamePurposes.RepeatsPerPurpose)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Repeats {request.RepeatFrom}..{request.RepeatFrom + request.RepeatCount - 1} are not {request.Purpose} repeats ({first}..{first + GamePurposes.RepeatsPerPurpose - 1}).");
        }
    }

    private static GamesHeader HeaderFor(MeasurementRequest request, PlayerModel player) => new()
    {
        Candidate = request.Candidate.Id,
        Epoch = request.Epoch,
        Player = player,
        Purpose = request.Purpose,
        SeedIndices = request.SeedIndices,
        RepeatFrom = request.RepeatFrom,
        RepeatCount = request.RepeatCount,
        MoveCap = request.Settings.MoveCap,
        ProgressMarks = request.Settings.ProgressMarks,
    };
}
