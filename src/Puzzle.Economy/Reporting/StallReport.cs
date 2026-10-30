using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Reporting;

/// <summary>What a population did on one level.</summary>
/// <param name="Players">Players of the population who played the level: an attempt of theirs was made on it.</param>
/// <param name="StillOnIt">Of those, the players whose journey ended on it, unwon: above 0 on a level somebody never passes.</param>
/// <param name="FirstTryWinShare">The share of the stored games of the pool's first seed that are won at the limit: what the level was tuned to.</param>
/// <param name="FirstSeedGames">The stored games that share is counted on.</param>
/// <param name="FirstTryBand">The band that holds the share with 90 % confidence, given the number of games.</param>
/// <param name="AttemptsMean">The attempts a player spent on the level, the unwon ones of players still on it included.</param>
/// <param name="AttemptsMeanOnEvenGames">The same, the players meeting only the even stored games; with the odd one, the noise of the measurement.</param>
/// <param name="ExtraMovesTakenShare">The share of the attempts in which the extra moves were taken.</param>
/// <param name="ExtraMovesWantableShare">
/// Of the stored games that run out of moves at the limit with a board that can go on, the share in which the player
/// sees enough of the goals done to want the extra moves; unknown when no stored game runs out of moves.
/// </param>
/// <param name="RankOnEvenGames">The level's place among the population's levels by attempts on the even games, 1 for the most.</param>
/// <param name="RankOnOddGames">The same place on the odd games: a level that is high on one half only owes it to the luck of that half.</param>
public sealed record StallRow(
    Population Population,
    LevelId Level,
    DifficultyTier Tier,
    MoveCount MoveLimit,
    int PoolSize,
    int Players,
    int StillOnIt,
    double FirstTryWinShare,
    long FirstSeedGames,
    (double Low, double High)? FirstTryBand,
    double? AttemptsMean,
    double? AttemptsMeanOnEvenGames,
    double? AttemptsMeanOnOddGames,
    int? AttemptsP50,
    int? AttemptsP90,
    double? PlayMinutesMean,
    double? ShareTakingLongerThanADay,
    double? ExtraMovesTakenShare,
    double? ExtraMovesWantableShare,
    double? StartBoosterUsedShare,
    double? ShareMeetingASeedTwice,
    int RankOnEvenGames,
    int RankOnOddGames)
{
    /// <summary>
    /// Games run out of moves here and not one of them shows the player enough of the goals to want the extra moves:
    /// the rule never takes them on this level. It happens where the goals move in a few big steps (three items to
    /// drop), and the attempts counted on such a level are the rule's blind spot as much as the level's difficulty.
    /// </summary>
    public bool ExtraMovesNeverWanted => ExtraMovesWantableShare == 0;
}

/// <summary>What a population spent on the levels of one group: the part of "where do they stall" that both halves of the stored games agree on.</summary>
/// <param name="AttemptsMean">The mean over the group's levels of the attempts a player spent on a level.</param>
/// <param name="NeverWantingExtraMoves">Levels of the group on which the player's rule never takes the extra moves (<see cref="StallRow.ExtraMovesNeverWanted"/>).</param>
/// <param name="AttemptsMeanThere">The attempts on those levels; unknown when the group has none.</param>
public sealed record StallGroupRow(
    Population Population,
    LevelGroup Group,
    int Levels,
    double? AttemptsMean,
    double? AttemptsMeanOnEvenGames,
    double? AttemptsMeanOnOddGames,
    int NeverWantingExtraMoves,
    double? AttemptsMeanThere,
    double? AttemptsMeanOnTheOthers);

/// <summary>
/// Where players stall. By group of levels the answer is firm. A list of single levels is not: a level's attempts
/// rest on few stored games, so the list is ranked on the even stored games and read from the odd ones, and each
/// level carries its place on both halves. It names levels to look at; a level should be measured deeper before
/// anyone changes it, and a change of a level is for every player alike.
/// </summary>
public static class StallReport
{
    public static IReadOnlyList<StallRow> Rows(PopulationResult run)
    {
        var rows = new List<StallRow>();
        Percent wantedFrom = run.Settings.Player.ExtraMoves.TakeWhenGoalsDone;
        foreach (Population population in Population.Of(run.Settings.Players))
        {
            GroupTallies all = population.TalliesIn(run.All);
            GroupTallies onEvenGames = population.TalliesIn(run.EvenGames);
            GroupTallies onOddGames = population.TalliesIn(run.OddGames);
            IReadOnlyDictionary<SkillRung, double> shares = population.SharesIn(run.Settings.Players);
            Dictionary<LevelId, int> rankOnEvenGames = RankByAttempts(run.Levels.Levels, onEvenGames);
            Dictionary<LevelId, int> rankOnOddGames = RankByAttempts(run.Levels.Levels, onOddGames);
            foreach (LevelId level in run.Levels.Levels)
            {
                var stored = new StoredOf(level, run.Levels, shares);
                var tallies = new TalliesOf(all.Of(level), onEvenGames.Of(level), onOddGames.Of(level), rankOnEvenGames[level], rankOnOddGames[level]);
                rows.Add(RowOf(population, stored, tallies, wantedFrom));
            }
        }

        return rows;
    }

    /// <summary>The levels of a population that top the list on the even games, worst first.</summary>
    public static IReadOnlyList<StallRow> Worst(IEnumerable<StallRow> rows, Population population, int count) =>
        rows.Where(row => row.Population.Name == population.Name).OrderBy(row => row.RankOnEvenGames).Take(count).ToArray();

    /// <summary>A population's attempts by group of levels, for every group it has levels of.</summary>
    public static IReadOnlyList<StallGroupRow> ByGroup(IEnumerable<StallRow> rows, Population population)
    {
        StallRow[] ofPopulation = rows.Where(row => row.Population.Name == population.Name).ToArray();
        var groups = new List<StallGroupRow>();
        foreach (LevelGroup group in LevelGroup.All)
        {
            StallRow[] levels = ofPopulation.Where(row => group.Has(row.Level, row.Tier)).ToArray();
            if (levels.Length == 0)
            {
                continue;
            }

            StallRow[] neverWanting = levels.Where(row => row.ExtraMovesNeverWanted).ToArray();
            groups.Add(new StallGroupRow(
                population,
                group,
                levels.Length,
                MeanOver(levels, row => row.AttemptsMean),
                MeanOver(levels, row => row.AttemptsMeanOnEvenGames),
                MeanOver(levels, row => row.AttemptsMeanOnOddGames),
                neverWanting.Length,
                MeanOver(neverWanting, row => row.AttemptsMean),
                MeanOver(levels.Except(neverWanting).ToArray(), row => row.AttemptsMean)));
        }

        return groups;
    }

    public static CsvTable Table(IEnumerable<StallRow> rows)
    {
        var table = new CsvTable(
            "level", "tier", "moveLimit", "poolSize", "population", "players", "stillOnIt", "firstTryWinShare", "firstSeedGames", "firstTryLow90", "firstTryHigh90",
            "attemptsMean", "attemptsMeanEvenGames", "attemptsMeanOddGames", "attemptsP50", "attemptsP90", "playMinutesMean", "shareTakingLongerThanADay",
            "extraMovesTakenShare", "extraMovesWantableShare", "startBoosterUsedShare", "shareMeetingASeedTwice", "rankOnEvenGames", "rankOnOddGames");
        foreach (StallRow row in rows)
        {
            table.Add(
                Cell.Whole(row.Level.Value),
                row.Tier.ToString(),
                Cell.Whole(row.MoveLimit.Value),
                Cell.Whole(row.PoolSize),
                row.Population.Name,
                Cell.Whole(row.Players),
                Cell.Whole(row.StillOnIt),
                Cell.Share(row.FirstTryWinShare),
                Cell.Whole(row.FirstSeedGames),
                Cell.Share(row.FirstTryBand?.Low),
                Cell.Share(row.FirstTryBand?.High),
                Cell.Fine(row.AttemptsMean),
                Cell.Fine(row.AttemptsMeanOnEvenGames),
                Cell.Fine(row.AttemptsMeanOnOddGames),
                Cell.Whole(row.AttemptsP50),
                Cell.Whole(row.AttemptsP90),
                Cell.Number(row.PlayMinutesMean),
                Cell.Share(row.ShareTakingLongerThanADay),
                Cell.Share(row.ExtraMovesTakenShare),
                Cell.Share(row.ExtraMovesWantableShare),
                Cell.Share(row.StartBoosterUsedShare),
                Cell.Share(row.ShareMeetingASeedTwice),
                Cell.Whole(row.RankOnEvenGames),
                Cell.Whole(row.RankOnOddGames));
        }

        return table;
    }

    /// <summary>Each level's place by the attempts a player spent on it, most first; a level nobody played comes last, and the earlier level goes first among equals.</summary>
    private static Dictionary<LevelId, int> RankByAttempts(IEnumerable<LevelId> levels, GroupTallies tallies) => levels
        .OrderByDescending(level => tallies.Of(level).AttemptsPerPlayer ?? 0)
        .ThenBy(level => level.Value)
        .Select((level, index) => (level, Rank: index + 1))
        .ToDictionary(ranked => ranked.level, ranked => ranked.Rank);

    private static StallRow RowOf(Population population, StoredOf stored, TalliesOf tallies, Percent wantedFrom)
    {
        MeasuredLine line = stored.Levels.Line(stored.Level, population.Rungs[0], BoardStart.Plain);
        FirstTry firstTry = FirstTryOf(stored);
        LevelTally all = tallies.All;
        return new StallRow(
            population,
            stored.Level,
            line.Tier,
            line.MoveLimit,
            line.Pool.Count,
            all.Players,
            all.PlayersLeftOnIt,
            firstTry.WinShare,
            firstTry.Games,
            Statistics.Wilson90(firstTry.WinShare, firstTry.GamesInEffect),
            all.AttemptsPerPlayer,
            tallies.OnEvenGames.AttemptsPerPlayer,
            tallies.OnOddGames.AttemptsPerPlayer,
            all.AttemptsNeeded.QuantileOrLater(all.Players, Statistics.Median),
            all.AttemptsNeeded.QuantileOrLater(all.Players, Statistics.BandHigh),
            Ratio(all.PlayTimeOfWinners.TotalMinutes, all.Winners),
            Ratio(all.WinnersWhoTookOverADay, all.Winners),
            Ratio(all.AttemptsWithExtraMoves, all.Attempts),
            WantableShareOf(stored, wantedFrom),
            Ratio(all.AttemptsWithAStartBooster, all.Attempts),
            Ratio(all.WinnersWhoMetASeedTwice, all.Winners),
            tallies.RankOnEvenGames,
            tallies.RankOnOddGames);
    }

    /// <summary>
    /// The first-try win share of a population: its rungs' shares on the pool's first seed, weighted by the rungs'
    /// shares of the players. The games "in effect" are how many games of one rung would pin the share down as well
    /// as the weighted games of several do (Kish); the confidence band is drawn from that number.
    /// </summary>
    private static FirstTry FirstTryOf(StoredOf stored)
    {
        double winShare = 0;
        double varianceWeight = 0;
        long games = 0;
        foreach ((SkillRung rung, double share) in stored.Shares)
        {
            MeasuredLine line = stored.Levels.Line(stored.Level, rung, BoardStart.Plain);
            SeedGames firstSeed = line.Seeds[0];
            winShare += share * firstSeed.Games.Count(game => game.WonWithin(line.MoveLimit)) / firstSeed.Count;
            varianceWeight += share * share / firstSeed.Count;
            games += firstSeed.Count;
        }

        return new FirstTry(winShare, games, 1 / varianceWeight);
    }

    /// <summary>
    /// Of the stored plain games that run out of moves at the limit with a board that can go on, the share in which
    /// <paramref name="wantedFrom"/> percent of the goals is done by then: each rung by its share of the players, in a
    /// rung each pool seed alike. Unknown when no game runs out of moves.
    /// </summary>
    private static double? WantableShareOf(StoredOf stored, Percent wantedFrom)
    {
        var wantable = new WeightedMean();
        foreach ((SkillRung rung, double share) in stored.Shares)
        {
            MeasuredLine line = stored.Levels.Line(stored.Level, rung, BoardStart.Plain);
            foreach (WeightedGame game in StoredGames.Weighted([line]))
            {
                if (!game.Game.WonWithin(line.MoveLimit) && !game.Game.StuckWithin(line.MoveLimit))
                {
                    wantable.Add(share * game.Weight, game.Game.HasReached(wantedFrom, line.MoveLimit));
                }
            }
        }

        return wantable.Value;
    }

    /// <summary>The mean of a figure over the levels that have it; unknown when none has.</summary>
    private static double? MeanOver(StallRow[] levels, Func<StallRow, double?> figure) =>
        Statistics.Mean(levels.Select(figure).OfType<double>().ToArray());

    private static double? Ratio(double part, long whole) => whole == 0 ? null : part / whole;

    /// <summary>A level as the stored games of a population's rungs show it.</summary>
    private readonly record struct StoredOf(LevelId Level, SimulatedLevels Levels, IReadOnlyDictionary<SkillRung, double> Shares);

    /// <summary>What a population did on a level: on every stored game, and on each half of them.</summary>
    private readonly record struct TalliesOf(LevelTally All, LevelTally OnEvenGames, LevelTally OnOddGames, int RankOnEvenGames, int RankOnOddGames);

    private readonly record struct FirstTry(double WinShare, long Games, double GamesInEffect);
}
