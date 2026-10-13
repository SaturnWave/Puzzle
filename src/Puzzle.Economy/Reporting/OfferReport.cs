using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Reporting;

/// <summary>What a row of the offer report is about: the extra moves offered when the moves run out, or a start booster. Closed set.</summary>
public abstract record Offered
{
    /// <summary>The name of the extra-moves offer in the report; a start booster goes by its record name.</summary>
    public const string ExtraMovesName = "extra-moves-offer";

    private Offered()
    {
    }

    public sealed record ExtraMoves : Offered;

    public sealed record StartBooster(BoosterKind Booster) : Offered;

    public string Name => this switch
    {
        ExtraMoves => ExtraMovesName,
        StartBooster start => start.Booster.Name(),
        _ => throw new InvalidOperationException($"Unhandled offer {GetType().Name}."),
    };
}

/// <summary>The attempts a row of the offer report counts. Closed set.</summary>
public abstract record OfferCondition
{
    private OfferCondition()
    {
    }

    /// <summary>Every attempt.</summary>
    public sealed record Any : OfferCondition;

    /// <summary>
    /// The attempts that ran out of moves, with a board that could go on, having done a share of the goals between two
    /// neighbouring measured marks: what the player sees when the offer is made.
    /// </summary>
    /// <param name="From">The last mark that was reached; absent below the first mark.</param>
    /// <param name="Below">The next mark, which was not reached; absent at or above the last mark.</param>
    public sealed record OutOfMovesWithGoals(Percent? From, Percent? Below) : OfferCondition;

    public string Name => this switch
    {
        Any => "any",
        OutOfMovesWithGoals { From: null, Below: Percent below } => $"goals below {below} %",
        OutOfMovesWithGoals { From: Percent from, Below: null } => $"goals {from} % or more",
        OutOfMovesWithGoals { From: Percent from, Below: Percent below } => $"goals {from} to {below} %",
        OutOfMovesWithGoals => "out of moves",
        _ => throw new InvalidOperationException($"Unhandled condition {GetType().Name}."),
    };

    /// <summary>The band of progress of a game that has reached <paramref name="marksReached"/> of the measured <paramref name="marks"/>.</summary>
    public static OutOfMovesWithGoals Band(IReadOnlyList<Percent> marks, int marksReached) => new(
        marksReached == 0 ? null : marks[marksReached - 1],
        marksReached == marks.Count ? null : marks[marksReached]);
}

/// <summary>
/// What an offer or a start booster is worth to one rung on one group of levels, read from the stored games alone,
/// with its price beside it. This is the table a price is read from.
/// </summary>
/// <param name="Games">The stored games the share with the offer is counted on.</param>
/// <param name="WinWithout">The share of the attempts won without it. Under a progress condition every attempt is one that ran out of moves, so it is 0.</param>
/// <param name="MovesLeftWithout">The moves a winner has left without it: what the finale pays; unknown when nothing is won.</param>
public sealed record OfferRow(
    SkillRung Rung,
    LevelGroup Levels,
    Offered Offer,
    OfferCondition Condition,
    long Games,
    double? WinWithout,
    double? WinWith,
    double? MovesLeftWithout,
    double? MovesLeftWith,
    Coins Price)
{
    /// <summary>The percentage points of win chance it adds.</summary>
    public double? GainPoints => (WinWith - WinWithout) * 100;

    /// <summary>What one percentage point of win chance costs at the price, in whole coins; unknown when it adds none.</summary>
    public Coins? CoinsPerGainPoint => GainPoints is > 0 ? new Coins((long)Math.Round(Price.Amount / GainPoints.Value)) : null;
}

public static class OfferReport
{
    private static readonly BoosterKind[] BoardBoosters = [BoosterKind.StartRocketAndBomb, BoosterKind.StartColorBomb];

    /// <summary>The rows for every rung of the settings' mix on levels 1 to <paramref name="lastLevel"/>; a board booster has rows where its start was measured for every level of a group.</summary>
    public static IReadOnlyList<OfferRow> Rows(EconomySettings settings, Measurement measurement, int lastLevel)
    {
        var rows = new List<OfferRow>();
        foreach (SkillRung rung in settings.Players.Rungs)
        {
            MeasuredLine[] plain = LinesOf(measurement, rung, BoardStart.Plain, lastLevel);
            foreach (LevelGroup group in LevelGroup.All)
            {
                var games = new GamesOf(rung, group, plain.Where(group.Holds).ToArray());
                if (games.Plain.Length == 0)
                {
                    continue;
                }

                rows.AddRange(ExtraMovesOfferRows(games, measurement.Spec, settings.ExtraMovesOffer.Price));
                rows.Add(ExtraMovesBoosterRow(games, measurement.Spec, settings.Prices.Boosters[BoosterKind.StartExtraMoves]));
                foreach (BoosterKind booster in BoardBoosters)
                {
                    MeasuredLine[] boosted = LinesOf(measurement, rung, booster.BoardStart(), lastLevel).Where(group.Holds).ToArray();
                    if (boosted.Length == games.Plain.Length)
                    {
                        rows.Add(BoardBoosterRow(games, booster, boosted, settings.Prices.Boosters[booster]));
                    }
                }
            }
        }

        return rows;
    }

    public static CsvTable Table(IEnumerable<OfferRow> rows)
    {
        var table = new CsvTable(
            "rung", "levels", "offer", "condition", "games", "winWithoutShare", "winWithShare", "gainPoints",
            "movesLeftWithoutMean", "movesLeftWithMean", "priceCoins", "coinsPerGainPoint");
        foreach (OfferRow row in rows)
        {
            table.Add(
                row.Rung.Name,
                row.Levels.Name,
                row.Offer.Name,
                row.Condition.Name,
                Cell.Whole(row.Games),
                Cell.Share(row.WinWithout),
                Cell.Share(row.WinWith),
                Cell.Number(row.GainPoints),
                Cell.Number(row.MovesLeftWithout),
                Cell.Number(row.MovesLeftWith),
                Cell.Whole(row.Price),
                Cell.Whole(row.CoinsPerGainPoint));
        }

        return table;
    }

    private static MeasuredLine[] LinesOf(Measurement measurement, SkillRung rung, BoardStart start, int lastLevel) => Enumerable.Range(1, lastLevel)
        .Select(level => measurement.Find(new LevelId(level), rung, start))
        .OfType<MeasuredLine>()
        .ToArray();

    /// <summary>
    /// The offer for every attempt, were it always taken when it is made; then for the attempts that ran out of moves
    /// with a board that could go on, by the progress the player saw.
    /// </summary>
    private static IEnumerable<OfferRow> ExtraMovesOfferRows(GamesOf games, MeasurementSpec spec, Coins price)
    {
        var any = new Tally();
        Tally[] byProgress = Enumerable.Range(0, spec.ProgressMarks.Count + 1).Select(_ => new Tally()).ToArray();
        foreach (WeightedGame stored in StoredGames.Weighted(games.Plain))
        {
            MoveCount limit = stored.Line.MoveLimit;
            MoveCount? leftAtTheLimit = MovesLeft(stored.Game, limit);
            MoveCount? leftWithTheOffer = leftAtTheLimit ?? MovesLeft(stored.Game, limit + spec.ExtraMovesOfferMoves);
            any.Add(stored.Weight, leftAtTheLimit, leftWithTheOffer);
            if (leftAtTheLimit is null && !stored.Game.StuckWithin(limit))
            {
                int marksReached = spec.ProgressMarks.Count(mark => stored.Game.HasReached(mark, limit));
                byProgress[marksReached].Add(stored.Weight, without: null, leftWithTheOffer);
            }
        }

        var offer = new Offered.ExtraMoves();
        yield return any.Row(games, offer, new OfferCondition.Any(), price);
        for (int marksReached = 0; marksReached < byProgress.Length; marksReached++)
        {
            yield return byProgress[marksReached].Row(games, offer, OfferCondition.Band(spec.ProgressMarks, marksReached), price);
        }
    }

    private static OfferRow ExtraMovesBoosterRow(GamesOf games, MeasurementSpec spec, Coins price)
    {
        var tally = new Tally();
        foreach (WeightedGame stored in StoredGames.Weighted(games.Plain))
        {
            MoveCount limit = stored.Line.MoveLimit;
            tally.Add(stored.Weight, MovesLeft(stored.Game, limit), MovesLeft(stored.Game, limit + spec.StartBoosterExtraMoves));
        }

        return tally.Row(games, new Offered.StartBooster(BoosterKind.StartExtraMoves), new OfferCondition.Any(), price);
    }

    /// <summary>A board booster's games are games of their own: the plain games of the same levels and seeds stand beside them.</summary>
    private static OfferRow BoardBoosterRow(GamesOf games, BoosterKind booster, MeasuredLine[] boosted, Coins price)
    {
        var tally = new Tally();
        foreach (WeightedGame stored in StoredGames.Weighted(games.Plain))
        {
            tally.AddWithout(stored.Weight, MovesLeft(stored.Game, stored.Line.MoveLimit));
        }

        foreach (WeightedGame stored in StoredGames.Weighted(boosted))
        {
            tally.AddWith(stored.Weight, MovesLeft(stored.Game, stored.Line.MoveLimit));
        }

        return tally.Row(games, new Offered.StartBooster(booster), new OfferCondition.Any(), price);
    }

    /// <summary>The moves a winner has left with <paramref name="budget"/> moves; absent when the game is not won with them.</summary>
    private static MoveCount? MovesLeft(MeasuredGame game, MoveCount budget) => game.WonWithin(budget) ? game.MovesLeftAt(budget) : null;

    /// <summary>The plain games of one rung on one group of levels: what every row of the group is counted against.</summary>
    private readonly record struct GamesOf(SkillRung Rung, LevelGroup Levels, MeasuredLine[] Plain);

    /// <summary>Attempts with and without an offer, each as the moves a winner has left or nothing for a loss.</summary>
    private sealed class Tally
    {
        private readonly WeightedMean _wonWithout = new();
        private readonly WeightedMean _wonWith = new();
        private readonly WeightedMean _leftWithout = new();
        private readonly WeightedMean _leftWith = new();

        public void Add(double weight, MoveCount? without, MoveCount? with)
        {
            AddWithout(weight, without);
            AddWith(weight, with);
        }

        public void AddWithout(double weight, MoveCount? left) => Add(_wonWithout, _leftWithout, weight, left);

        public void AddWith(double weight, MoveCount? left) => Add(_wonWith, _leftWith, weight, left);

        public OfferRow Row(GamesOf games, Offered offer, OfferCondition condition, Coins price) => new(
            games.Rung, games.Levels, offer, condition, _wonWith.Count, _wonWithout.Value, _wonWith.Value, _leftWithout.Value, _leftWith.Value, price);

        private static void Add(WeightedMean won, WeightedMean left, double weight, MoveCount? movesLeft)
        {
            won.Add(weight, movesLeft is not null);
            if (movesLeft is MoveCount moves)
            {
                left.Add(weight, moves.Value);
            }
        }
    }
}
