using Puzzle.Bot;
using Puzzle.Core.Levels;

namespace Puzzle.Tooling.Ladder;

/// <summary>The mean share of games a bot won at the move limit over some levels, every level counting alike.</summary>
/// <param name="Levels">How many levels the mean is over; at least one.</param>
public readonly record struct MeanWinAtLimit(int Levels, double WinRate);

/// <summary>How one bot of the ladder did over the levels measured.</summary>
/// <param name="ByTier">The same over the levels of each tier; a tier without a measured level has no entry.</param>
public sealed record LadderBotSummary(SkillProfile Bot, MeanWinAtLimit Overall, IReadOnlyDictionary<DifficultyTier, MeanWinAtLimit> ByTier);

/// <summary>Sums a ladder measurement up: each bot's mean win rate at the move limit, overall and by tier.</summary>
public static class LadderSummary
{
    /// <summary>One summary per bot of <paramref name="rows"/>, weakest first; bots are told apart by their skill, as in the report.</summary>
    public static IReadOnlyList<LadderBotSummary> Of(IEnumerable<LadderRow> rows) => rows
        .GroupBy(row => row.Bot.NoticePercent)
        .OrderBy(rowsOfBot => rowsOfBot.Key)
        .Select(rowsOfBot => new LadderBotSummary(
            rowsOfBot.First().Bot,
            MeanOf(rowsOfBot),
            rowsOfBot.GroupBy(row => row.Tier).ToDictionary(rowsOfTier => rowsOfTier.Key, MeanOf)))
        .ToArray();

    /// <summary>The mean over <paramref name="rows"/>, which are not empty: a group always has a row.</summary>
    private static MeanWinAtLimit MeanOf(IEnumerable<LadderRow> rows)
    {
        double[] winRates = rows.Select(row => row.WinAtLimit).ToArray();
        return new MeanWinAtLimit(winRates.Length, winRates.Average());
    }
}
