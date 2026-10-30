using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;

namespace Puzzle.Tooling.Ladder;

/// <summary>How one bot of the skill ladder did on one level, read from games that were followed to the move cap.</summary>
/// <param name="Tier">The level's tier; the summary groups by it, the report file does not hold it.</param>
/// <param name="Bot">The bot that played; its skill is the share of moves it notices.</param>
/// <param name="WinAtLimit">Share of the games won within <paramref name="MoveLimit"/>.</param>
/// <param name="MovesNeeded">
/// The moves within which the bot finished each of <see cref="MovePercents"/> of its games, in that order; absent where
/// that share of the games did not finish within the cap.
/// </param>
/// <param name="LimitFor">
/// The smallest move limit at which the bot wins at least each of <see cref="WinPercents"/> of its games, in that order;
/// absent where no limit within the cap does.
/// </param>
public sealed record LadderRow(
    LevelId Level,
    DifficultyTier Tier,
    int MoveLimit,
    SkillProfile Bot,
    int Runs,
    double WinAtLimit,
    IReadOnlyList<int?> MovesNeeded,
    IReadOnlyList<int?> LimitFor)
{
    /// <summary>The shares of games <see cref="MovesNeeded"/> is read at, in percent.</summary>
    public static IReadOnlyList<int> MovePercents { get; } = [50, 80, 90, 95];

    /// <summary>The win rates <see cref="LimitFor"/> is read at, in percent.</summary>
    public static IReadOnlyList<int> WinPercents { get; } = [70, 80, 90, 95];

    /// <summary>The row of <paramref name="bot"/> on <paramref name="level"/>, read from the report of its games there.</summary>
    /// <exception cref="ArgumentException">
    /// The games stopped before the level's move limit: what they would have won by the limit is unknown.
    /// </exception>
    public static LadderRow From(LevelDefinition level, SkillProfile bot, LevelReport report)
    {
        if (level.MoveLimit > report.MoveCap)
        {
            throw new ArgumentException(
                $"Level {level.Id} has a move limit of {level.MoveLimit}, past the move cap of {report.MoveCap} its games were followed to.",
                nameof(report));
        }

        return new LadderRow(
            level.Id,
            level.Difficulty,
            level.MoveLimit,
            bot,
            report.Runs,
            report.WinRateAt(level.MoveLimit),
            MovePercents.Select(percent => report.MovesNeeded(percent / 100.0)).ToArray(),
            WinPercents.Select(percent => report.MoveLimitFor(percent / 100.0)).ToArray());
    }
}
