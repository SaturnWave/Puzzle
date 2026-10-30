using System.Globalization;
using System.Text;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Reporting;

/// <summary>What a measurement says about one rung on one group of levels, before any player is simulated.</summary>
/// <param name="WinOnFirstSeed">The share of first attempts won at the level's limit: the figure the levels were tuned to.</param>
/// <param name="WinOverPool">The same over every seed of the pools.</param>
/// <param name="WinByStart">For each board start that was measured for every level of the group, the share won at the limit.</param>
/// <param name="Stuck">Games whose board ran out of moves for good, at any move.</param>
/// <param name="Unfinished">Games still running at the move cap.</param>
public sealed record MeasuredGroup(
    SkillRung Rung,
    string Levels,
    int LevelCount,
    long Games,
    double? WinOnFirstSeed,
    double? WinOverPool,
    double? WinWithBoosterMoves,
    double? WinWithOfferMoves,
    double? WinWithBoth,
    IReadOnlyDictionary<BoardStart, double?> WinByStart,
    long Stuck,
    long Unfinished);

/// <summary>
/// The first look at a measurement: per rung and group of levels, how often the stored games are won at the limit, with
/// the booster's moves, with the offer's and with both, and with each board start. It is how a person checks the
/// measurement against what the levels were tuned to before reading any simulated day.
/// </summary>
public static class MeasurementSummary
{
    public static IReadOnlyList<MeasuredGroup> Of(MeasurementSpec spec, IReadOnlyCollection<MeasuredLine> lines)
    {
        var groups = new List<MeasuredGroup>();
        foreach (IGrouping<int, MeasuredLine> ofRung in lines.GroupBy(line => line.Player.NoticePercent).OrderBy(rung => rung.Key))
        {
            SkillRung rung = SkillRung.Parse(ofRung.First().Player.Name);
            foreach (LevelGroup group in LevelGroup.All)
            {
                MeasuredLine[] ofGroup = ofRung.Where(group.Holds).ToArray();
                if (ofGroup.Length > 0)
                {
                    groups.Add(GroupOf(rung, group, ofGroup, spec));
                }
            }
        }

        return groups;
    }

    /// <param name="spec">The measurement the groups were read from: its header says how many moves the booster and the offer add.</param>
    public static string Text(MeasurementSpec spec, IEnumerable<MeasuredGroup> groups)
    {
        string booster = $"+{spec.StartBoosterExtraMoves}";
        string offer = $"+{spec.ExtraMovesOfferMoves}";
        var text = new StringBuilder();
        text.AppendLine("WHAT THE STORED GAMES SAY, before any player is simulated: the share won, each level and each seed of its pool weighing the same");
        text.AppendLine(CultureInfo.InvariantCulture, $"  (the first seed at the limit is everyone's first attempt, the figure the levels were tuned to; {booster} is the start booster's moves, {offer} the offer's)");
        var table = new TextTable("rung", "levels", "count", "games", "first seed", "whole pool", $"with {booster}", $"with {offer}", "with both", "rocket and bomb", "color bomb", "stuck", "at the cap");
        foreach (MeasuredGroup group in groups)
        {
            table.Add(
                group.Rung.Name,
                group.Levels,
                group.LevelCount.ToString(CultureInfo.InvariantCulture),
                group.Games.ToString(CultureInfo.InvariantCulture),
                Percent(group.WinOnFirstSeed),
                Percent(group.WinOverPool),
                Percent(group.WinWithBoosterMoves),
                Percent(group.WinWithOfferMoves),
                Percent(group.WinWithBoth),
                Percent(group.WinByStart[BoardStart.RocketAndBomb]),
                Percent(group.WinByStart[BoardStart.ColorBomb]),
                group.Stuck.ToString(CultureInfo.InvariantCulture),
                group.Unfinished.ToString(CultureInfo.InvariantCulture));
        }

        table.AppendTo(text);
        return ReportFiles.WithLineFeeds(text);
    }

    private static MeasuredGroup GroupOf(SkillRung rung, LevelGroup group, MeasuredLine[] lines, MeasurementSpec spec)
    {
        MeasuredLine[] plain = lines.Where(line => line.Start == BoardStart.Plain).ToArray();
        MeasuredGame[] games = lines.SelectMany(line => line.Seeds).SelectMany(seed => seed.Games).ToArray();
        return new MeasuredGroup(
            rung,
            group.Name,
            plain.Length,
            games.LongLength,
            WinShare(StoredGames.Weighted(plain, firstSeedOnly: true), MoveCount.None),
            WinShare(StoredGames.Weighted(plain), MoveCount.None),
            WinShare(StoredGames.Weighted(plain), spec.StartBoosterExtraMoves),
            WinShare(StoredGames.Weighted(plain), spec.ExtraMovesOfferMoves),
            WinShare(StoredGames.Weighted(plain), spec.StartBoosterExtraMoves + spec.ExtraMovesOfferMoves),
            BoardStarts.All.ToDictionary(start => start, start => WinShareOf(start, lines, plain.Length)),
            games.LongCount(game => game.End is GameEnd.Stuck),
            games.LongCount(game => game.End is GameEnd.Unfinished));
    }

    /// <summary>The share won at the limit on boards of <paramref name="start"/>; unknown unless the start was measured for every level of the group.</summary>
    private static double? WinShareOf(BoardStart start, MeasuredLine[] lines, int levels)
    {
        MeasuredLine[] ofStart = lines.Where(line => line.Start == start).ToArray();
        return ofStart.Length == levels ? WinShare(StoredGames.Weighted(ofStart), MoveCount.None) : null;
    }

    /// <summary>The share of the games won with the level's limit and <paramref name="added"/> moves more.</summary>
    private static double? WinShare(IEnumerable<WeightedGame> games, MoveCount added)
    {
        var won = new WeightedMean();
        foreach (WeightedGame stored in games)
        {
            won.Add(stored.Weight, stored.Game.WonWithin(stored.Line.MoveLimit + added));
        }

        return won.Value;
    }

    private static string Percent(double? share) => share is double known ? (known * 100).ToString("0.0", CultureInfo.InvariantCulture) + " %" : "-";
}
