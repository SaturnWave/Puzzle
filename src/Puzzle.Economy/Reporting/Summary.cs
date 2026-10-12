using System.Globalization;
using System.Text;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Reporting;

/// <summary>Text in columns: every column as wide as its widest cell, the first aligned left and the others right.</summary>
internal sealed class TextTable(params string[] headers)
{
    private readonly List<string[]> _rows = [headers];

    public void Add(params string[] cells) => _rows.Add(cells);

    public void AppendTo(StringBuilder text)
    {
        int[] widths = Enumerable.Range(0, headers.Length).Select(column => _rows.Max(row => row[column].Length)).ToArray();
        foreach (string[] row in _rows)
        {
            IEnumerable<string> cells = row.Select((cell, column) => column == 0 ? cell.PadRight(widths[column]) : cell.PadLeft(widths[column]));
            text.Append("  ").AppendLine(string.Join("  ", cells).TrimEnd());
        }
    }
}

/// <summary>
/// The short text a product owner reads first: what the run was read from, when players reach the milestones, what
/// ends their sessions, what lives cost, where coins come from and go, what a free player affords, where players
/// stall, and every assumption the numbers rest on. Each block says beside its figures what they do not show.
/// </summary>
public static class Summary
{
    private const int StallsListed = 10;
    private const int WallsListed = 12;
    private const string Unknown = "-";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Of(EconomyReport report)
    {
        var text = new StringBuilder();
        AppendIdentity(text, report);
        text.Append(MeasurementSummary.Text(report.Measurement.Spec, report.Measured)).AppendLine();
        AppendPace(text, report);
        AppendSessions(text, report);
        AppendLives(text, report);
        AppendCoins(text, report);
        AppendSpending(text, report);
        AppendStalls(text, report);
        AppendAssumptions(text, report);
        AppendLimits(text, report);
        return ReportFiles.WithLineFeeds(text);
    }

    private static void AppendIdentity(StringBuilder text, EconomyReport report)
    {
        EconomySettings settings = report.Settings;
        MeasurementIdentity measured = report.Measurement;
        MeasurementSpec spec = measured.Spec;
        string mix = string.Join(", ", settings.Players.SkillMix.Select(part => $"{part.Rung} {part.Share} %"));
        string check = measured.Checked is { } guard
            ? $"checked against this build and the level files ({guard.GamesReplayed} stored games replayed)"
            : "NOT CHECKED against this build or the level files (--trust-measurement)";
        text.AppendLine(Invariant, $"ECONOMY SIMULATION '{settings.Name}'  ({report.SettingsPath})");
        text.AppendLine(Line($"measurement: {measured.Path}, engine {MeasurementSpec.Abbreviated(spec.Engine)}, {measured.Games} stored games, levels 1..{measured.LastLevel}; {check}"));
        text.AppendLine(Line($"  its games: followed to move {spec.MoveCap}, bot repeats from {spec.FirstRepeat}, goals marked at {string.Join(", ", spec.ProgressMarks)} %, {string.Join(", ", spec.PlainGamesBySeedPosition)} plain games by pool position, {spec.BoostedGamesPerSeed} on a board that starts with a booster"));
        if (report.SettingsAskForAnotherMeasurement.Count > 0)
        {
            text.AppendLine(Line($"  NOTE: the measurement block of the settings asks for another measurement than the one that was read ({string.Join("; ", report.SettingsAskForAnotherMeasurement)}). The block says how `economy measure` would measure; this run read the file as it is."));
        }

        text.AppendLine(Line($"players: {settings.Players.Count} free players ({mix}), seed {settings.Players.Seed}, followed for {settings.Players.HorizonDays} days at most; the average player is {settings.Players.AverageRung}"));
        text.AppendLine();
    }

    private static void AppendPace(StringBuilder text, EconomyReport report)
    {
        text.AppendLine("PACE: the day a level is won (day 1 is a player's first day)");
        text.AppendLine("  (the band p10 to p90 is luck alone: every player has the same habit, so it is no range of what real players will do. The session");
        text.AppendLine("   settings, habit.sessionStarts and habit.sessionCeilingMinutes, move these days far more than the band is wide: compare a variant.)");
        var table = new TextTable("population", "level", "reached", "day p10", "p50", "p90", "p50 on even / odd games", "play hours p50", "attempts p50");
        foreach (PaceRow row in report.Pace)
        {
            if (!row.IsMeasured)
            {
                table.Add(row.Population.Name, Whole(row.Level.Value), "not measured", Unknown, Unknown, Unknown, Unknown, Unknown, Unknown);
                continue;
            }

            table.Add(
                Named(row.Population),
                Whole(row.Level.Value),
                Percent(row.ReachedShare),
                Whole(row.DaysP10),
                Whole(row.DaysP50),
                Whole(row.DaysP90),
                $"{Whole(row.DaysP50OnEvenGames)} / {Whole(row.DaysP50OnOddGames)}",
                Number(row.PlayHoursP50),
                Whole(row.AttemptsP50));
        }

        table.AppendTo(text);
        PaceTarget target = report.Settings.Report.PaceTarget;
        foreach (PaceRow row in report.Pace.Where(row => row.Population.IsAverage && row.Verdict is not null))
        {
            text.AppendLine(Line($"pace target: level {target.Level} between day {target.From} and day {target.To} for the average player ({row.Population.Name}). The median lands on day {Whole(row.DaysP50)}: {Described(row.Verdict!.Value)}. The target is reported against; nothing is adjusted to meet it."));
        }

        text.AppendLine();
    }

    private static void AppendSessions(StringBuilder text, EconomyReport report)
    {
        HabitSettings habit = report.Settings.Habit;
        ObservedFigures observed = report.Settings.Observed;
        text.AppendLine(Line($"SESSIONS over the whole journey: {habit.SessionStarts.Count} a day; no attempt starts once the lives are gone or the session has lasted {habit.SessionCeiling.TotalMinutes:0} minutes"));
        text.AppendLine("  (an attempt under way at that moment is finished, so a session runs past the ceiling: the mean length says by how much)");
        var table = new TextTable(
            "population", "ended by lives", "at the ceiling", "no life at the start", "last level won", "attempts a session p50", "play seconds an attempt p50", "session minutes mean", "minutes a day");
        foreach (HabitRow row in report.Habit)
        {
            table.Add(
                Named(row.Population),
                Percent(row.EndedByLivesShare),
                Percent(row.EndedAtCeilingShare),
                Percent(row.StartedWithoutALifeShare),
                Percent(row.EndedWithTheLastLevelShare),
                Whole(row.AttemptsPerSessionP50),
                Whole(row.PlaySecondsPerAttemptP50),
                Number(row.SessionMinutesMean),
                Number(row.MinutesADayMean));
        }

        table.AppendTo(text);
        text.AppendLine(Line($"recorded on the test server ({observed.Players} people): {observed.MedianAttemptsPerSession} attempts a session, {observed.MedianAttemptSeconds} seconds an attempt (medians; never used by the simulation)"));
        text.AppendLine();
    }

    private static void AppendLives(StringBuilder text, EconomyReport report)
    {
        LivesSettings lives = report.Settings.Lives;
        text.AppendLine(Line($"LIVES up to each milestone, of the players who reached it: {lives.Max} lives, one back every {lives.PerLife.TotalMinutes:0} minutes"));
        text.AppendLine("  (nobody sits waiting in this habit: 'hours denied' is the play that sessions lost because the lives ran out, to their ceiling; a refill moment is a");
        text.AppendLine("   session lives ended or kept from starting. The last three columns are what the timer itself did.)");
        var table = new TextTable(
            "population", "level", "reached", "sessions", "lives lost", "refill moments", "hours denied mean", "p50", "p90",
            "sessions started below full", "lives back during sessions", "minutes to the next life at a refill moment");
        foreach (LivesRow row in report.Lives)
        {
            table.Add(
                Named(row.Population),
                Whole(row.Level.Value),
                Percent(row.ReachedShare),
                Number(row.SessionsMean),
                Number(row.LivesLostMean),
                Number(row.RefillMomentsMean),
                Number(row.HoursDeniedMean),
                Number(row.HoursDeniedP50),
                Number(row.HoursDeniedP90),
                Percent(row.StartedBelowFullShare),
                Fine(row.LivesBackInSessionsMean),
                Number(row.MinutesToNextLifeMean));
        }

        table.AppendTo(text);
        AppendTimerNote(text, report.Lives, lives);
        AppendSurvivorNote(text, report.Lives.Select(row => row.ReachedShare));
        text.AppendLine();
    }

    /// <summary>What the table above says about the life timer: nothing, where no session began below full and no life came back in one.</summary>
    private static void AppendTimerNote(StringBuilder text, IReadOnlyList<LivesRow> rows, LivesSettings lives)
    {
        string minutes = lives.PerLife.TotalMinutes.ToString("0", Invariant);
        if (rows.Any(row => row.StartedBelowFullShare > 0 || row.LivesBackInSessionsMean > 0))
        {
            text.AppendLine(Line($"  The {minutes}-minute timer acted in this run: sessions began below {lives.Max} lives or lives came back while one ran, where the columns say so."));
            return;
        }

        text.AppendLine(Line($"  THE TIMER DID NOTHING HERE: every session began with {lives.Max} lives and no life came back while one ran. Every figure but the last column"));
        text.AppendLine(Line($"   would be the same with any other number of minutes a life that keeps both so: this run cannot say whether {minutes} minutes are generous or tight."));
        text.AppendLine("   It takes a habit in which lives end sessions early and the player is back before they are full again (longer or more frequent sessions) to see the timer.");
    }

    private static void AppendCoins(StringBuilder text, EconomyReport report)
    {
        EconomySettings settings = report.Settings;
        string offer = OfferName(report);
        text.AppendLine(Line($"COINS at each milestone (means per player; balances as p10 / p50 / p90). The {offer} moves cost {settings.ExtraMovesOffer.Price}, a side-collection piece {settings.Prices.SideCollectionPiece}"));
        var table = new TextTable(
            "population", "level", "reached", "balance", "from the finale", "level wins", "paintings", "daily", $"spent on {offer}", "on boosters",
            "unpaid wants: gaps", "their full price", $"p50 buys {offer}", "side pieces");
        foreach (CoinsRow row in report.Coins.Where(row => row.At == BalanceMoment.Milestone))
        {
            table.Add(
                Named(row.Population),
                Whole(row.Value),
                Percent(row.ReachedShare),
                $"{Whole(row.BalanceP10)} / {Whole(row.BalanceP50)} / {Whole(row.BalanceP90)}",
                Whole(row.EarnedFinale),
                Whole(row.EarnedLevelWon),
                Whole(row.EarnedPainting),
                Whole(row.EarnedDaily),
                Whole(row.SpentExtraMoves),
                Whole(row.SpentStartBoosters),
                Whole(row.GapsAtUnpaidWants),
                Whole(row.PriceOfUnpaidWants),
                Whole(row.BalanceBuysExtraMoves),
                Whole(row.BalanceBuysSidePieces));
        }

        table.AppendTo(text);
        text.AppendLine("  (the balance day by day is in coins.csv. 'gaps' adds the coins missing at each unpaid want: the coins a player holds are missing from none of them, though");
        text.AppendLine("   they would pay for one want only, so it understates what taking every want would have cost. 'full price' adds the prices of what was gone without.");
        text.AppendLine("   A player who paid would also have played on differently, so neither is the size of a purchase: that takes a simulated payer, which this version has not.)");
        AppendWhatDecidesTheCoins(text, report);
        AppendSurvivorNote(text, report.Coins.Where(row => row.At == BalanceMoment.Milestone).Select(row => row.ReachedShare));
        text.AppendLine();
    }

    /// <summary>
    /// The free ads are per day and the attempts of a day follow the session habit: how often the average player wants
    /// the extra moves in a day, against the ads of a day, decides whether coins are scarce or pile up.
    /// </summary>
    private static void AppendWhatDecidesTheCoins(StringBuilder text, EconomyReport report)
    {
        SpendingRow? toTheLastMilestone = report.Spending.LastOrDefault(row => row.Population.IsAverage);
        if (toTheLastMilestone is null)
        {
            return;
        }

        string offer = OfferName(report);
        int freeAds = report.Settings.ExtraMovesOffer.FreeAdsPerDay;
        text.AppendLine(Line($"  WHAT DECIDES THIS TABLE: up to level {toTheLastMilestone.Level} the average player wants the {offer} {Fine(toTheLastMilestone.ExtraMovesWantedADay)} times a day, and {freeAds} a day cost an ad. What is above that is bought or gone without;"));
        text.AppendLine("   below it coins have nothing to go to. How long a session is (habit.sessionCeilingMinutes, an assumption) and when the player takes the offer");
        text.AppendLine("   (player.extraMoves.takeWhenGoalsDonePercent, a proposal) move that number, and with it every balance above, as much as a price does.");
        text.AppendLine("   Read no price from one settings file alone: set a variant beside it (puzzle economy compare).");
    }

    private static void AppendSpending(StringBuilder text, EconomyReport report)
    {
        string offer = OfferName(report);
        text.AppendLine("EXTRA MOVES AND BOOSTERS a free player gets, up to each milestone (means per player who reached it)");
        var table = new TextTable(
            "population", "level", "reached", "out of moves", $"{offer} wanted", "a day", "by ad", "for coins", "wanted, unpaid", "gaps", "full price", $"taken {offer} that won",
            "start boosters received", "used", "bought", "held", "wanted, unpaid", "in-level received", "held");
        foreach (SpendingRow row in report.Spending)
        {
            BoosterRow[] boosters = report.Boosters.Where(booster => booster.Population.Name == row.Population.Name && booster.Level == row.Level).ToArray();
            BoosterRow[] start = boosters.Where(booster => booster.Booster.IsStart()).ToArray();
            BoosterRow[] inLevel = boosters.Where(booster => booster.Booster.IsInLevel()).ToArray();
            table.Add(
                Named(row.Population),
                Whole(row.Level.Value),
                Percent(row.ReachedShare),
                Number(row.TimesOutOfMoves),
                Number(row.ExtraMovesWanted),
                Fine(row.ExtraMovesWantedADay),
                Number(row.ExtraMovesByAd),
                Number(row.ExtraMovesBought),
                Number(row.ExtraMovesWantedUnpaid),
                Whole(row.GapsAtUnpaidExtraMoves),
                Whole(row.PriceOfUnpaidExtraMoves),
                Percent(row.ShareOfTakenExtraMovesThatWon),
                Number(Sum(start, booster => booster.Received)),
                Number(Sum(start, booster => booster.Used)),
                Number(Sum(start, booster => booster.Bought)),
                Number(Sum(start, booster => booster.Held)),
                Number(row.StartBoostersWantedUnpaid),
                Number(Sum(inLevel, booster => booster.Received)),
                Number(Sum(inLevel, booster => booster.Held)));
        }

        table.AppendTo(text);
        text.AppendLine("  (every ad of this version is one for extra moves; 'gaps' and 'full price' are of the unpaid extra moves, as in the table above;");
        text.AppendLine("   what an offer or a booster is worth, with its price beside it, is in offers.csv)");
        AppendSurvivorNote(text, report.Spending.Select(row => row.ReachedShare));
        text.AppendLine();
    }

    private static void AppendStalls(StringBuilder text, EconomyReport report)
    {
        foreach (Population average in report.Populations.Where(population => population.IsAverage))
        {
            AppendStallsByGroup(text, report, average);
            AppendWorstStalls(text, report, average);
        }
    }

    private static void AppendStallsByGroup(StringBuilder text, EconomyReport report, Population average)
    {
        string offer = OfferName(report);
        text.AppendLine(Line($"WHERE THE AVERAGE PLAYER ({average.Name}) STALLS, by group of levels: attempts a level, as a mean over the group's levels"));
        text.AppendLine("  (this is the firm answer: the two halves of the stored games agree on it. The last three columns set apart the levels on which the player's");
        text.AppendLine(Line($"   rule never takes the {offer}, because no stored game that runs out of moves shows {report.Settings.Player.ExtraMoves.TakeWhenGoalsDone} % of the goals done: levels whose goals move in a few big steps.)"));
        var table = new TextTable("levels", "count", "attempts", "on even / odd games", $"levels where the {offer} is never wanted", "attempts there", "on the others");
        foreach (StallGroupRow row in StallReport.ByGroup(report.Stalls, average))
        {
            table.Add(
                row.Group.Name,
                Whole(row.Levels),
                Fine(row.AttemptsMean),
                $"{Fine(row.AttemptsMeanOnEvenGames)} / {Fine(row.AttemptsMeanOnOddGames)}",
                Whole(row.NeverWantingExtraMoves),
                Fine(row.AttemptsMeanThere),
                Fine(row.AttemptsMeanOnTheOthers));
        }

        table.AppendTo(text);
        text.AppendLine();
    }

    private static void AppendWorstStalls(StringBuilder text, EconomyReport report, Population average)
    {
        string offer = OfferName(report);
        IReadOnlyList<StallRow> worst = StallReport.Worst(report.Stalls, average, StallsListed);
        int alsoOnOddGames = worst.Count(row => row.RankOnOddGames <= StallsListed);
        text.AppendLine(Line($"THE {StallsListed} LEVELS WHERE THE AVERAGE PLAYER ({average.Name}) STALLS LONGEST: ranked on the even stored games, attempts read from the odd ones"));
        var table = new TextTable(
            "level", "tier", "moves", "pool", "first try won (90 % band)", "attempts on odd games", "on even", "rank on odd", "p90", "over a day", "met a seed twice", "still on it",
            $"{offer} taken", $"{offer} can be wanted");
        foreach (StallRow row in worst)
        {
            table.Add(
                Whole(row.Level.Value),
                row.Tier.ToString(),
                row.MoveLimit.ToString(),
                Whole(row.PoolSize),
                $"{Percent(row.FirstTryWinShare)} ({Percent(row.FirstTryBand?.Low)} to {Percent(row.FirstTryBand?.High)})",
                Fine(row.AttemptsMeanOnOddGames),
                Fine(row.AttemptsMeanOnEvenGames),
                Whole(row.RankOnOddGames),
                Whole(row.AttemptsP90),
                Percent(row.ShareTakingLongerThanADay),
                Percent(row.ShareMeetingASeedTwice),
                Whole(row.StillOnIt),
                Percent(row.ExtraMovesTakenShare),
                row.ExtraMovesNeverWanted ? "NEVER" : "yes");
        }

        table.AppendTo(text);
        text.AppendLine(Line($"  ({alsoOnOddGames} of these {worst.Count} are also among the {StallsListed} worst on the odd games ('rank on odd'). A level that is not owes its place to the luck of a few stored games"));
        text.AppendLine(Line($"   and would be another level on other games. A level marked NEVER is high on the list partly because this player never takes the {offer} there."));
        text.AppendLine("   A nomination, not a verdict: measure a level deeper before changing it, and a change of a level is for every player alike.)");
        text.AppendLine();
    }

    private static void AppendAssumptions(StringBuilder text, EconomyReport report)
    {
        text.AppendLine("WHAT THE NUMBERS REST ON: every setting that is not a decision or a matter of the tool");
        foreach (Provenance provenance in new[] { Provenance.Assumed, Provenance.Proposed, Provenance.Placeholder, Provenance.Recorded })
        {
            text.AppendLine(Invariant, $"  {Explained(provenance)}");
            foreach (SettingRow row in report.SettingRows.Where(row => row.Provenance == provenance))
            {
                text.AppendLine(Invariant, $"    {row.Field} = {row.Value}  ({row.Unit})");
            }
        }

        InLevelBoosterHabit inLevel = report.Settings.Player.InLevelBoosters;
        if (inLevel.AssumesAnEffect)
        {
            string worths = string.Join(", ", inLevel.AssumedMovesWorth.Where(worth => worth.Value > MoveCount.None).Select(worth => $"{worth.Key.Name()} as {worth.Value} moves"));
            text.AppendLine(Line($"  ASSUMED, NOT MEASURED: an in-level booster is read as more moves of the stored game ({worths}), used when the moves run out with {inLevel.UseWhenGoalsDone} % of the goals done. No bot played one."));
        }

        text.AppendLine();
    }

    private static void AppendLimits(StringBuilder text, EconomyReport report)
    {
        if (report.Walls.Count > 0)
        {
            string walls = string.Join(", ", report.Walls.Take(WallsListed).Select(wall => $"level {wall.Level} for {wall.Rung}"));
            string more = report.Walls.Count > WallsListed ? $" and {report.Walls.Count - WallsListed} more" : string.Empty;
            text.AppendLine(Line($"WALLS: no stored game is won even with the booster's and the offer's moves on {walls}{more}. Players of that rung stay there."));
        }
        else
        {
            text.AppendLine("WALLS: none. Every level has stored games that each rung wins.");
        }

        text.AppendLine("NOT MEASURED: in-level boosters (received and held only, unless a worth is assumed above); two start boosters in one attempt.");
        text.AppendLine("NOT SIMULATED: purchases with real money, players who quit, days without play, learning a level over retries, a slower pace of weaker players,");
        text.AppendLine("  players whose habits differ, and a player who waits for a life or comes back early for one.");
        text.AppendLine("What a free player goes without here (unpaid wants, play denied by lives) is where a paying player would spend; how much they would spend is not simulated.");
        text.AppendLine("Nothing a player spends changes a game they meet.");
    }

    /// <summary>A milestone row is about the players who reached the level; when that is not everyone, the reader has to know it.</summary>
    private static void AppendSurvivorNote(StringBuilder text, IEnumerable<double?> reachedShares)
    {
        if (reachedShares.Any(share => share < 1))
        {
            text.AppendLine("  NOTE: where 'reached' is below 100 %, the row is about the players who got there within the horizon, the faster ones, and not about the population.");
        }
    }

    /// <summary>The extra-moves offer as players know it: a plus and the moves it adds.</summary>
    private static string OfferName(EconomyReport report) => $"+{report.Measurement.Spec.ExtraMovesOfferMoves}";

    private static string Named(Population population) => population.IsAverage ? $"{population.Name} (average)" : population.Name;

    private static string Described(PaceVerdict verdict) => verdict switch
    {
        PaceVerdict.Faster => "FASTER than the target",
        PaceVerdict.Within => "WITHIN the target",
        PaceVerdict.Slower => "SLOWER than the target",
        PaceVerdict.NotReached => "NOT REACHED by half the players within the horizon",
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Unknown verdict."),
    };

    private static string Explained(Provenance provenance) => provenance switch
    {
        Provenance.Assumed => "assumed (the owner's starting assumptions):",
        Provenance.Proposed => "proposed (added by the simulator's design; waiting for the owner's word):",
        Provenance.Placeholder => "placeholder (amounts and prices nobody has decided; read the reports, then change them):",
        Provenance.Recorded => "recorded (from the test server's recordings):",
        Provenance.Decided or Provenance.Tool => throw new ArgumentOutOfRangeException(nameof(provenance), provenance, "Not listed among the assumptions."),
        _ => throw new ArgumentOutOfRangeException(nameof(provenance), provenance, "Unknown provenance."),
    };

    private static double? Sum(BoosterRow[] rows, Func<BoosterRow, double?> of) => rows.Length == 0 ? null : rows.Sum(of);

    private static string Line(FormattableString text) => text.ToString(Invariant);

    private static string Whole(long? number) => number?.ToString(Invariant) ?? Unknown;

    private static string Whole(Coins? coins) => Whole(coins?.Amount);

    private static string Number(double? number) => number?.ToString("0.0", Invariant) ?? Unknown;

    private static string Fine(double? number) => number?.ToString("0.00", Invariant) ?? Unknown;

    private static string Percent(double? share) => share is double known ? (known * 100).ToString("0.0", Invariant) + " %" : Unknown;
}
