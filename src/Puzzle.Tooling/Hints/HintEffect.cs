using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;

namespace Puzzle.Tooling.Hints;

/// <summary>How the effect of hints on a player model is measured.</summary>
public sealed record HintEffectSettings
{
    /// <summary>The player model whose attempts are played: the average player is the rung of docs/GAME_DESIGN.md §8 item 9.</summary>
    public required string Player { get; init; }

    /// <summary>The player models whose moves the hints show, each measured on the same attempts.</summary>
    public required IReadOnlyList<string> HintBots { get; init; }

    public required HintMoment Moment { get; init; }

    /// <summary>The game's cap: no attempt shows more hints than this.</summary>
    public int HintsPerAttempt { get; init; } = 3;

    /// <summary>The attempts of each level played, from the first: each level's own seeds, as a player meets them.</summary>
    public int AttemptsPerLevel { get; init; } = 8;

    /// <summary>The bot choices of attempt i of level n come from <c>Rng.Derive(Rng.Derive(Seed, n), i)</c>, the same with and without hints.</summary>
    public ulong Seed { get; init; } = 7;
}

/// <summary>One attempt played without hints and once with each hint bot's hints, on the same seeds and the same bot choices.</summary>
/// <param name="Hinted">By hint bot, in the settings' order: whether it was won at the level's limit, and how many hints were followed.</param>
public sealed record HintedAttempt(LevelDefinition Level, int Attempt, bool WonPlain, IReadOnlyList<(bool Won, int Hints)> Hinted);

/// <summary>What hints from one bot did to the attempts of a group of levels.</summary>
/// <param name="Change">The hinted win rate less the plain one, in points of 0..1.</param>
/// <param name="StandardError">Of <paramref name="Change"/>, read from the attempt pairs: a 95 % interval is about two of it either side.</param>
public sealed record HintEffectSummary(
    string HintBot, int Attempts, double PlainWinRate, double HintedWinRate, double Change, double StandardError, double MeanHints, double ShareWithAHint);

/// <summary>
/// Measures what the game's hints do to a player model's win rate: every attempt is played twice and more, once as it
/// is and once for each hint bot with that bot's hints followed whenever one is shown (<see cref="HintedPlayer"/>).
/// Win rates are read at each level's own move limit, on the seeds the level gives its attempts (as the game starts
/// them, LevelStart.SeedsOf). The attempts are paired, so the change is read from the attempts that ended otherwise
/// with hints, which makes it far surer than two separate win rates would be. No level is changed.
/// </summary>
public static class HintEffect
{
    /// <exception cref="ArgumentException">A bot name is no player model, or no attempt is asked for.</exception>
    /// <exception cref="LevelInitializationException">A level cannot produce a starting board.</exception>
    public static IReadOnlyList<HintedAttempt> Measure(IReadOnlyList<LevelDefinition> levels, HintEffectSettings settings)
    {
        RequirePlayerModel(settings.Player);
        foreach (string hintBot in settings.HintBots)
        {
            RequirePlayerModel(hintBot);
        }

        if (settings.AttemptsPerLevel < 1 || settings.HintBots.Count == 0)
        {
            throw new ArgumentException("At least one attempt per level and one hint bot are needed.", nameof(settings));
        }

        (LevelDefinition Level, int Attempt)[] jobs = levels
            .SelectMany(level => Enumerable.Range(0, settings.AttemptsPerLevel).Select(attempt => (level, attempt)))
            .ToArray();
        return jobs.AsParallel().AsOrdered().Select(job => Play(job.Level, job.Attempt, settings)).ToArray();
    }

    /// <summary>Each hint bot's effect on the attempts given, in the order of <paramref name="hintBots"/>.</summary>
    /// <exception cref="ArgumentException">No attempt is given.</exception>
    public static IReadOnlyList<HintEffectSummary> Summarize(IReadOnlyList<HintedAttempt> attempts, IReadOnlyList<string> hintBots)
    {
        if (attempts.Count == 0)
        {
            throw new ArgumentException("No attempt to summarize.", nameof(attempts));
        }

        double plain = attempts.Average(attempt => attempt.WonPlain ? 1.0 : 0.0);
        return hintBots.Select((hintBot, index) =>
        {
            double[] differences = attempts.Select(attempt => (attempt.Hinted[index].Won ? 1.0 : 0.0) - (attempt.WonPlain ? 1.0 : 0.0)).ToArray();
            double change = differences.Average();
            double variance = differences.Sum(difference => (difference - change) * (difference - change)) / Math.Max(1, differences.Length - 1);
            return new HintEffectSummary(
                hintBot,
                attempts.Count,
                plain,
                attempts.Average(attempt => attempt.Hinted[index].Won ? 1.0 : 0.0),
                change,
                Math.Sqrt(variance / differences.Length),
                attempts.Average(attempt => (double)attempt.Hinted[index].Hints),
                attempts.Average(attempt => attempt.Hinted[index].Hints > 0 ? 1.0 : 0.0));
        }).ToArray();
    }

    private static HintedAttempt Play(LevelDefinition level, int attempt, HintEffectSettings settings)
    {
        AttemptSeeds seeds = level.Attempts.SeedsFor(attempt, unpooledSeed: (uint)(level.Id.Value + attempt));
        ulong botSeed = Rng.Derive(Rng.Derive(settings.Seed, (ulong)level.Id.Value), (ulong)attempt);
        bool wonPlain = Won(SimulationRunner.Play(level, PlayerOf(settings.Player), seeds, botSeed, level.MoveLimit));
        var hinted = new List<(bool Won, int Hints)>(settings.HintBots.Count);
        foreach (string hintBot in settings.HintBots)
        {
            var player = new HintedPlayer(PlayerOf(settings.Player), AdvisorOf(hintBot), settings.Moment, settings.HintsPerAttempt);
            hinted.Add((Won(SimulationRunner.Play(level, player, seeds, botSeed, level.MoveLimit)), player.HintsFollowed));
        }

        return new HintedAttempt(level, attempt, wonPlain, hinted);
    }

    private static bool Won(RunResult run) => run.FinalStatus == GameStatus.Won;

    private static IBotPolicy PlayerOf(string name) =>
        BotPolicies.TryCreate(name, out IBotPolicy policy) ? policy : throw new ArgumentException($"'{name}' is no player model.", nameof(name));

    private static HintAdvisor AdvisorOf(string name) =>
        HintAdvisor.TryCreate(name, out HintAdvisor? advisor) ? advisor : throw new ArgumentException($"'{name}' gives no hints.", nameof(name));

    private static void RequirePlayerModel(string name)
    {
        if (!BotPolicies.IsPlayerModel(name))
        {
            throw new ArgumentException($"'{name}' is no player model: random, novice, casual, average, expert or {SkillLadder.NamePattern}.", nameof(name));
        }
    }
}
