using Puzzle.Core.Levels;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Reporting;

/// <summary>
/// What lives cost a population up to a milestone, over the players who reached it. In this habit nobody sits
/// waiting for a life: the cost is the play a session lost because the lives ran out, and a refill moment is a
/// session that ended, or could not start, for lack of a life. Three figures say what the timer itself did: the
/// sessions that began below a full meter, the lives that came back while a session ran, and how long the next life
/// was away at a refill moment. Where the first two are 0, the timer changed nothing else in the row.
/// </summary>
/// <param name="ReachedShare">The share of the population the row is about: the players who won the level within the horizon.</param>
/// <param name="StartedBelowFullShare">Of the sessions, the share that began with fewer lives than the meter holds.</param>
/// <param name="LivesBackInSessionsMean">Lives the timer gave back while a session was running, per player.</param>
/// <param name="MinutesToNextLifeMean">At a refill moment, the minutes until the next life; unknown when there was no such moment.</param>
public sealed record LivesRow(
    Population Population,
    LevelId Level,
    int Players,
    double? ReachedShare,
    double? SessionsMean,
    double? EndedByLivesShare,
    double? EndedAtCeilingShare,
    double? StartedWithoutALifeShare,
    double? LivesLostMean,
    double? RefillMomentsMean,
    double? HoursDeniedMean,
    double? HoursDeniedP50,
    double? HoursDeniedP90,
    double? StartedBelowFullShare,
    double? LivesBackInSessionsMean,
    double? MinutesToNextLifeMean);

/// <summary>When a population's coins are looked at. Closed set.</summary>
public enum BalanceMoment
{
    EndOfDay,
    Milestone,
}

public static class BalanceMoments
{
    /// <summary>The moment's name in the coins report.</summary>
    public static string Name(this BalanceMoment moment) => moment switch
    {
        BalanceMoment.EndOfDay => "day",
        BalanceMoment.Milestone => "level",
        _ => throw new ArgumentOutOfRangeException(nameof(moment), moment, "Unknown moment."),
    };
}

/// <summary>A population's coins at the end of a day or at a milestone. Sums are means per player and whole coins.</summary>
/// <param name="At">Whether <paramref name="Value"/> is a day at whose end the players stand, or a level they have just won.</param>
/// <param name="ReachedShare">The share of the population the row is about: everyone at the end of a day, the players who won the level at a milestone.</param>
/// <param name="PlayersStillPlaying">Players who have not won the last measured level by then.</param>
/// <param name="GapsAtUnpaidWants">The coins missing at each unpaid want, summed (<see cref="Unpaid.Gaps"/>): less than taking them all would have cost.</param>
/// <param name="PriceOfUnpaidWants">The prices of everything gone without, summed (<see cref="Unpaid.Price"/>).</param>
/// <param name="BalanceBuysExtraMoves">How many times the median balance pays for the extra moves.</param>
public sealed record CoinsRow(
    Population Population,
    BalanceMoment At,
    int Value,
    int Players,
    double? ReachedShare,
    int PlayersStillPlaying,
    int? LevelP50,
    Coins? BalanceP10,
    Coins? BalanceP50,
    Coins? BalanceP90,
    Coins? EarnedFinale,
    Coins? EarnedLevelWon,
    Coins? EarnedPainting,
    Coins? EarnedDaily,
    Coins? SpentExtraMoves,
    Coins? SpentStartBoosters,
    Coins? GapsAtUnpaidWants,
    Coins? PriceOfUnpaidWants,
    long? BalanceBuysExtraMoves,
    long? BalanceBuysSidePieces);

/// <summary>What a population did about the extra-moves offer and start boosters up to a milestone: means per player who reached it.</summary>
/// <param name="ReachedShare">The share of the population the row is about.</param>
/// <param name="ExtraMovesWanted">Times the offer was made and the player saw enough of the goals done to want it.</param>
/// <param name="ExtraMovesWantedADay">The same per day of the journey so far: what stands against the free ads of a day.</param>
/// <param name="GapsAtUnpaidExtraMoves">The coins missing each time the extra moves went unpaid, summed: less than paying for them all would have cost.</param>
/// <param name="PriceOfUnpaidExtraMoves">The price of the extra moves that went unpaid, summed.</param>
/// <param name="AdsWatched">Every ad of this version is one for extra moves.</param>
public sealed record SpendingRow(
    Population Population,
    LevelId Level,
    int Players,
    double? ReachedShare,
    double? TimesOutOfMoves,
    double? ExtraMovesWanted,
    double? ExtraMovesWantedADay,
    double? ExtraMovesByAd,
    double? ExtraMovesBought,
    double? ExtraMovesWantedUnpaid,
    Coins? GapsAtUnpaidExtraMoves,
    Coins? PriceOfUnpaidExtraMoves,
    double? ShareOfTakenExtraMovesThatWon,
    double? AdsWatched,
    double? StartBoostersWantedUnpaid,
    Coins? GapsAtUnpaidStartBoosters,
    Coins? PriceOfUnpaidStartBoosters);

/// <summary>One booster in a population up to a milestone: means per player who reached it.</summary>
public sealed record BoosterRow(Population Population, LevelId Level, BoosterKind Booster, int Players, double? ReachedShare, double? Received, double? Used, double? Bought, double? Held);

/// <summary>How a population's sessions ran over the whole journey, to set beside what recordings of people show.</summary>
/// <param name="EndedWithTheLastLevelShare">Sessions that ended because the last measured level was won: a player's last session.</param>
/// <param name="AttemptsPerSessionP50">Of the sessions in which something was played.</param>
/// <param name="PlaySecondsPerAttemptP50">The time the moves of an attempt took, without pauses and ads: what a recording measures.</param>
/// <param name="SessionMinutesMean">The length of a session in which something was played: an attempt under way at the ceiling is finished, so this lies above the ceiling.</param>
/// <param name="MinutesADayMean">The minutes a player spends in sessions on a day of their journey.</param>
public sealed record HabitRow(
    Population Population,
    int Players,
    double? EndedByLivesShare,
    double? EndedAtCeilingShare,
    double? StartedWithoutALifeShare,
    double? EndedWithTheLastLevelShare,
    int? AttemptsPerSessionP50,
    int? PlaySecondsPerAttemptP50,
    double? SessionMinutesMean,
    double? MinutesADayMean);

/// <summary>The reports that follow players through their journeys: lives, coins, spending, boosters and the session habit.</summary>
public static class JourneyReports
{
    public static IReadOnlyList<LivesRow> Lives(PopulationResult run) => AtMilestones(run, LivesOf);

    public static IReadOnlyList<SpendingRow> Spending(PopulationResult run) => AtMilestones(run, SpendingOf);

    public static IReadOnlyList<BoosterRow> Boosters(PopulationResult run) =>
        AtMilestones(run, (population, level, arrived) => BoosterKinds.All.Select(booster => BoosterOf(population, level, booster, arrived)))
            .SelectMany(ofMilestone => ofMilestone)
            .ToArray();

    /// <summary>A row for the end of every listed day, then one for every milestone the measurement reaches.</summary>
    public static IReadOnlyList<CoinsRow> Coins(PopulationResult run)
    {
        ReportSettings report = run.Settings.Report;
        var rows = new List<CoinsRow>();
        foreach (Population population in Population.Of(run.Settings.Players))
        {
            IReadOnlyList<PlayerOutcome> players = population.PlayersIn(run.All);
            for (int day = 0; day < report.BalanceAtDays.Count; day++)
            {
                Checkpoint[] dayEnds = players.Select(player => player.AtDayEnds[day]).ToArray();
                rows.Add(CoinsOf(population, BalanceMoment.EndOfDay, report.BalanceAtDays[day].Value, new Arrived(dayEnds, players.Count), run));
            }

            for (int milestone = 0; milestone < report.Milestones.Count; milestone++)
            {
                if (report.Milestones[milestone].Value <= run.Levels.Count)
                {
                    rows.Add(CoinsOf(population, BalanceMoment.Milestone, report.Milestones[milestone].Value, Arrived.At(players, milestone), run));
                }
            }
        }

        return rows;
    }

    public static IReadOnlyList<HabitRow> Habit(PopulationResult run)
    {
        var rows = new List<HabitRow>();
        foreach (Population population in Population.Of(run.Settings.Players))
        {
            Checkpoint[] ends = population.PlayersIn(run.All).Select(player => player.AtEnd).ToArray();
            JourneyTotals[] totals = ends.Select(end => end.Totals).ToArray();
            GroupTallies tallies = population.TalliesIn(run.All);
            long sessions = totals.Sum(player => (long)player.Sessions);
            double minutesInSessions = totals.Sum(player => player.TimeInSessions.TotalMinutes);
            rows.Add(new HabitRow(
                population,
                totals.Length,
                Ratio(totals.Sum(player => (long)player.SessionsEndedByLives), sessions),
                Ratio(totals.Sum(player => (long)player.SessionsEndedAtCeiling), sessions),
                Ratio(totals.Sum(player => (long)player.SessionsWithoutALife), sessions),
                Ratio(totals.Sum(player => (long)player.SessionsEndedWithTheLastLevel), sessions),
                tallies.AttemptsPerSession.Quantile(Statistics.Median),
                tallies.PlaySecondsPerAttempt.Quantile(Statistics.Median),
                Ratio(minutesInSessions, totals.Sum(player => (long)player.SessionsWithPlay)),
                Ratio(minutesInSessions, ends.Sum(end => (long)end.At.Day.Value))));
        }

        return rows;
    }

    public static CsvTable LivesTable(IEnumerable<LivesRow> rows)
    {
        var table = new CsvTable(
            "population", "level", "players", "reachedShare", "sessionsMean", "endedByLivesShare", "endedAtCeilingShare", "startedWithoutALifeShare",
            "livesLostMean", "refillMomentsMean", "hoursDeniedByLivesMean", "hoursDeniedByLivesP50", "hoursDeniedByLivesP90",
            "startedBelowFullShare", "livesBackInSessionsMean", "minutesToNextLifeMean");
        foreach (LivesRow row in rows)
        {
            table.Add(
                row.Population.Name,
                Cell.Whole(row.Level.Value),
                Cell.Whole(row.Players),
                Cell.Share(row.ReachedShare),
                Cell.Number(row.SessionsMean),
                Cell.Share(row.EndedByLivesShare),
                Cell.Share(row.EndedAtCeilingShare),
                Cell.Share(row.StartedWithoutALifeShare),
                Cell.Number(row.LivesLostMean),
                Cell.Number(row.RefillMomentsMean),
                Cell.Number(row.HoursDeniedMean),
                Cell.Number(row.HoursDeniedP50),
                Cell.Number(row.HoursDeniedP90),
                Cell.Share(row.StartedBelowFullShare),
                Cell.Fine(row.LivesBackInSessionsMean),
                Cell.Number(row.MinutesToNextLifeMean));
        }

        return table;
    }

    public static CsvTable CoinsTable(IEnumerable<CoinsRow> rows)
    {
        var table = new CsvTable(
            "population", "at", "value", "players", "reachedShare", "playersStillPlaying", "levelP50", "balanceP10", "balanceP50", "balanceP90",
            "earnedFinale", "earnedLevelWon", "earnedPainting", "earnedDaily", "spentExtraMoves", "spentStartBoosters", "gapsAtUnpaidWants", "priceOfUnpaidWants",
            "balanceBuysExtraMoves", "balanceBuysSidePieces");
        foreach (CoinsRow row in rows)
        {
            table.Add(
                row.Population.Name,
                row.At.Name(),
                Cell.Whole(row.Value),
                Cell.Whole(row.Players),
                Cell.Share(row.ReachedShare),
                Cell.Whole(row.PlayersStillPlaying),
                Cell.Whole(row.LevelP50),
                Cell.Whole(row.BalanceP10),
                Cell.Whole(row.BalanceP50),
                Cell.Whole(row.BalanceP90),
                Cell.Whole(row.EarnedFinale),
                Cell.Whole(row.EarnedLevelWon),
                Cell.Whole(row.EarnedPainting),
                Cell.Whole(row.EarnedDaily),
                Cell.Whole(row.SpentExtraMoves),
                Cell.Whole(row.SpentStartBoosters),
                Cell.Whole(row.GapsAtUnpaidWants),
                Cell.Whole(row.PriceOfUnpaidWants),
                Cell.Whole(row.BalanceBuysExtraMoves),
                Cell.Whole(row.BalanceBuysSidePieces));
        }

        return table;
    }

    public static CsvTable SpendingTable(IEnumerable<SpendingRow> rows)
    {
        var table = new CsvTable(
            "population", "level", "players", "reachedShare", "timesOutOfMoves", "extraMovesWanted", "extraMovesWantedADay", "extraMovesByAd", "extraMovesBought",
            "extraMovesWantedUnpaid", "gapsAtUnpaidExtraMoves", "priceOfUnpaidExtraMoves", "shareOfTakenExtraMovesThatWon", "adsWatched",
            "startBoostersWantedUnpaid", "gapsAtUnpaidStartBoosters", "priceOfUnpaidStartBoosters");
        foreach (SpendingRow row in rows)
        {
            table.Add(
                row.Population.Name,
                Cell.Whole(row.Level.Value),
                Cell.Whole(row.Players),
                Cell.Share(row.ReachedShare),
                Cell.Fine(row.TimesOutOfMoves),
                Cell.Fine(row.ExtraMovesWanted),
                Cell.Fine(row.ExtraMovesWantedADay),
                Cell.Fine(row.ExtraMovesByAd),
                Cell.Fine(row.ExtraMovesBought),
                Cell.Fine(row.ExtraMovesWantedUnpaid),
                Cell.Whole(row.GapsAtUnpaidExtraMoves),
                Cell.Whole(row.PriceOfUnpaidExtraMoves),
                Cell.Share(row.ShareOfTakenExtraMovesThatWon),
                Cell.Fine(row.AdsWatched),
                Cell.Fine(row.StartBoostersWantedUnpaid),
                Cell.Whole(row.GapsAtUnpaidStartBoosters),
                Cell.Whole(row.PriceOfUnpaidStartBoosters));
        }

        return table;
    }

    public static CsvTable BoostersTable(IEnumerable<BoosterRow> rows)
    {
        var table = new CsvTable("population", "level", "booster", "players", "reachedShare", "received", "used", "bought", "heldAtMilestone");
        foreach (BoosterRow row in rows)
        {
            table.Add(
                row.Population.Name,
                Cell.Whole(row.Level.Value),
                row.Booster.Name(),
                Cell.Whole(row.Players),
                Cell.Share(row.ReachedShare),
                Cell.Fine(row.Received),
                Cell.Fine(row.Used),
                Cell.Fine(row.Bought),
                Cell.Fine(row.Held));
        }

        return table;
    }

    /// <summary>A row for every population and every milestone the measurement reaches, from the players who won that level.</summary>
    private static List<TRow> AtMilestones<TRow>(PopulationResult run, Func<Population, LevelId, Arrived, TRow> rowOf)
    {
        IReadOnlyList<LevelId> milestones = run.Settings.Report.Milestones;
        var rows = new List<TRow>();
        foreach (Population population in Population.Of(run.Settings.Players))
        {
            IReadOnlyList<PlayerOutcome> players = population.PlayersIn(run.All);
            for (int milestone = 0; milestone < milestones.Count; milestone++)
            {
                if (milestones[milestone].Value <= run.Levels.Count)
                {
                    rows.Add(rowOf(population, milestones[milestone], Arrived.At(players, milestone)));
                }
            }
        }

        return rows;
    }

    private static LivesRow LivesOf(Population population, LevelId level, Arrived arrived)
    {
        JourneyTotals[] totals = arrived.Totals;
        long sessions = totals.Sum(player => (long)player.Sessions);
        double[] hoursDenied = totals.Select(player => player.PlayDeniedByLives.TotalHours).ToArray();
        return new LivesRow(
            population,
            level,
            totals.Length,
            arrived.Share,
            Mean(totals, player => player.Sessions),
            Ratio(totals.Sum(player => (long)player.SessionsEndedByLives), sessions),
            Ratio(totals.Sum(player => (long)player.SessionsEndedAtCeiling), sessions),
            Ratio(totals.Sum(player => (long)player.SessionsWithoutALife), sessions),
            Mean(totals, player => player.LivesLost),
            Mean(totals, player => player.RefillMoments),
            Statistics.Mean(hoursDenied),
            Statistics.Quantile(hoursDenied, Statistics.Median),
            Statistics.Quantile(hoursDenied, Statistics.BandHigh),
            Ratio(totals.Sum(player => (long)player.SessionsStartedBelowFull), sessions),
            Mean(totals, player => player.LivesBackDuringSessions),
            Ratio(totals.Sum(player => player.WaitForTheNextLife.TotalMinutes), totals.Sum(player => (long)player.RefillMoments)));
    }

    private static SpendingRow SpendingOf(Population population, LevelId level, Arrived arrived)
    {
        JourneyTotals[] totals = arrived.Totals;
        double[] wantedADay = arrived.Checkpoints.Select(checkpoint => checkpoint.Totals.TimesExtraMovesWanted / (double)checkpoint.At.Day.Value).ToArray();
        return new SpendingRow(
            population,
            level,
            totals.Length,
            arrived.Share,
            Mean(totals, player => player.TimesOutOfMoves),
            Mean(totals, player => player.TimesExtraMovesWanted),
            Statistics.Mean(wantedADay),
            Mean(totals, player => player.ExtraMovesByAd),
            Mean(totals, player => player.ExtraMovesBought),
            Mean(totals, player => player.ExtraMovesWantedUnpaid),
            MeanCoins(totals, player => player.UnpaidExtraMoves.Gaps),
            MeanCoins(totals, player => player.UnpaidExtraMoves.Price),
            Ratio(totals.Sum(player => (long)player.ExtraMovesThatWon), totals.Sum(player => (long)player.ExtraMovesTaken)),
            Mean(totals, player => player.ExtraMovesByAd),
            Mean(totals, player => player.UnpaidStartBoosters.Wants),
            MeanCoins(totals, player => player.UnpaidStartBoosters.Gaps),
            MeanCoins(totals, player => player.UnpaidStartBoosters.Price));
    }

    private static BoosterRow BoosterOf(Population population, LevelId level, BoosterKind booster, Arrived arrived) => new(
        population,
        level,
        booster,
        arrived.Checkpoints.Count,
        arrived.Share,
        Mean(arrived.Totals, player => player.Received(booster)),
        Mean(arrived.Totals, player => player.Used(booster)),
        Mean(arrived.Totals, player => player.Bought(booster)),
        Statistics.Mean(arrived.Checkpoints.Select(checkpoint => (double)checkpoint.Standing.Held.Count(booster)).ToArray()));

    private static CoinsRow CoinsOf(Population population, BalanceMoment at, int value, Arrived arrived, PopulationResult run)
    {
        JourneyTotals[] totals = arrived.Totals;
        Coins[] balances = arrived.Checkpoints.Select(checkpoint => checkpoint.Standing.Balance).ToArray();
        Coins? median = Statistics.Quantile(balances, Statistics.Median);
        return new CoinsRow(
            population,
            at,
            value,
            totals.Length,
            arrived.Share,
            arrived.Checkpoints.Count(checkpoint => checkpoint.Standing.LevelsWon < run.Levels.Count),
            Statistics.Quantile(arrived.Checkpoints.Select(checkpoint => checkpoint.Standing.LevelsWon).ToArray(), Statistics.Median),
            Statistics.Quantile(balances, Statistics.BandLow),
            median,
            Statistics.Quantile(balances, Statistics.BandHigh),
            MeanCoins(totals, player => player.Earned(CoinSource.Finale)),
            MeanCoins(totals, player => player.Earned(CoinSource.LevelWon)),
            MeanCoins(totals, player => player.Earned(CoinSource.PaintingCompleted)),
            MeanCoins(totals, player => player.Earned(CoinSource.Daily)),
            MeanCoins(totals, player => player.Spent(CoinSink.ExtraMoves)),
            MeanCoins(totals, player => player.Spent(CoinSink.StartBooster)),
            MeanCoins(totals, player => player.UnpaidInAll.Gaps),
            MeanCoins(totals, player => player.UnpaidInAll.Price),
            median?.Amount / run.Settings.ExtraMovesOffer.Price.Amount,
            median?.Amount / run.Settings.Prices.SideCollectionPiece.Amount);
    }

    private static double? Mean(JourneyTotals[] totals, Func<JourneyTotals, int> count) =>
        Statistics.Mean(totals.Select(player => (double)count(player)).ToArray());

    private static Coins? MeanCoins(JourneyTotals[] totals, Func<JourneyTotals, Coins> amount) => Statistics.MeanCoins(totals.Select(amount).ToArray());

    private static double? Ratio(double part, long whole) => whole == 0 ? null : part / whole;

    /// <summary>
    /// The players of a population a row is about, as they stood at the row's moment, out of all the population's
    /// players. At a milestone they are the players who won its level within the horizon: when that is not everyone,
    /// the row describes the faster players only, and its share says so.
    /// </summary>
    private readonly record struct Arrived(IReadOnlyList<Checkpoint> Checkpoints, int OutOf)
    {
        public static Arrived At(IReadOnlyList<PlayerOutcome> players, int milestone) => new(PaceReport.Reached(players, milestone), players.Count);

        public double? Share => OutOf == 0 ? null : Checkpoints.Count / (double)OutOf;

        public JourneyTotals[] Totals => Checkpoints.Select(checkpoint => checkpoint.Totals).ToArray();
    }
}
