using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>What a player went without for lack of coins, summed two ways that must not be read as one.</summary>
/// <param name="Wants">Times something was wanted and not paid for.</param>
/// <param name="Gaps">
/// The coins missing each time, summed. The coins a player holds are missing from none of these gaps, though they
/// would pay for one want only: this is less than what taking every want would have cost.
/// </param>
/// <param name="Price">The prices of everything gone without, summed: what taking every want would have cost a player with no coins of their own.</param>
public readonly record struct Unpaid(int Wants, Coins Gaps, Coins Price)
{
    public static Unpaid operator +(Unpaid left, Unpaid right) => new(left.Wants + right.Wants, left.Gaps + right.Gaps, left.Price + right.Price);

    public Unpaid With(Shortfall another) => new(Wants + 1, Gaps + another.Missing, Price + another.Price);
}

/// <summary>
/// Everything counted about one player from their first day on; every figure only grows. The totals count the events
/// themselves, so what an attempt, a session or a reward adds is written in one place.
/// </summary>
public sealed class JourneyTotals
{
    private Coins[] _earned = new Coins[Enum.GetValues<CoinSource>().Length];
    private Coins[] _spent = new Coins[Enum.GetValues<CoinSink>().Length];
    private int[] _boostersReceived = new int[BoosterKinds.All.Count];
    private int[] _boostersUsed = new int[BoosterKinds.All.Count];
    private int[] _boostersBought = new int[BoosterKinds.All.Count];

    public int Attempts { get; private set; }

    public int Wins { get; private set; }

    public int LivesLost { get; private set; }

    /// <summary>The time the moves took, without pauses and ads.</summary>
    public TimeSpan PlayTime { get; private set; }

    public int Sessions { get; private set; }

    /// <summary>Sessions in which at least one attempt was played.</summary>
    public int SessionsWithPlay { get; private set; }

    /// <summary>The length of every session, summed: moves, ads and the pauses between attempts.</summary>
    public TimeSpan TimeInSessions { get; private set; }

    /// <summary>Sessions in which the last life was lost.</summary>
    public int SessionsEndedByLives { get; private set; }

    /// <summary>Sessions the player opened without a life and played nothing in.</summary>
    public int SessionsWithoutALife { get; private set; }

    public int SessionsEndedAtCeiling { get; private set; }

    /// <summary>Sessions that ended because the last measured level was won: every session ends one of the four ways.</summary>
    public int SessionsEndedWithTheLastLevel => Sessions - SessionsEndedByLives - SessionsWithoutALife - SessionsEndedAtCeiling;

    /// <summary>The play the lives cost: what was left of every session they ended, to its ceiling.</summary>
    public TimeSpan PlayDeniedByLives { get; private set; }

    /// <summary>Sessions that began with fewer lives than the meter holds: the timer had not yet given back what earlier sessions lost.</summary>
    public int SessionsStartedBelowFull { get; private set; }

    /// <summary>Lives the timer gave back while a session was running.</summary>
    public int LivesBackDuringSessions { get; private set; }

    /// <summary>At every session that ended, or could not start, for lack of a life: the time until the next life, summed.</summary>
    public TimeSpan WaitForTheNextLife { get; private set; }

    public int TimesOutOfMoves { get; private set; }

    /// <summary>Times the moves ran out with enough of the goals done for the player to want the offer.</summary>
    public int TimesExtraMovesWanted { get; private set; }

    public int ExtraMovesByAd { get; private set; }

    public int ExtraMovesBought { get; private set; }

    /// <summary>Times the extra moves were wanted and not taken, whether for lack of coins or because the player never pays coins for them.</summary>
    public int ExtraMovesWantedUnpaid { get; private set; }

    /// <summary>Attempts won after the extra moves were taken: wins the offer made.</summary>
    public int ExtraMovesThatWon { get; private set; }

    /// <summary>The extra moves a player who pays coins wanted and could not pay for.</summary>
    public Unpaid UnpaidExtraMoves { get; private set; }

    /// <summary>The start boosters a player wanted to buy and could not pay for.</summary>
    public Unpaid UnpaidStartBoosters { get; private set; }

    public Unpaid UnpaidInAll => UnpaidExtraMoves + UnpaidStartBoosters;

    public int ExtraMovesTaken => ExtraMovesByAd + ExtraMovesBought;

    /// <summary>Things bought for real money: coin packs and the starter pack.</summary>
    public int Purchases { get; private set; }

    /// <summary>What the purchases cost, at the US store's prices.</summary>
    public UsDollars MoneySpent { get; private set; }

    /// <summary>Sessions that ended for lack of a life: the moments a player would refill.</summary>
    public int RefillMoments => SessionsEndedByLives + SessionsWithoutALife;

    public Coins Earned(CoinSource source) => _earned[(int)source];

    public Coins Spent(CoinSink sink) => _spent[(int)sink];

    public int Received(BoosterKind booster) => _boostersReceived[(int)booster];

    public int Used(BoosterKind booster) => _boostersUsed[(int)booster];

    public int Bought(BoosterKind booster) => _boostersBought[(int)booster];

    public int StartBoostersUsed => BoosterKinds.All.Where(BoosterKinds.IsStart).Sum(Used);

    public int StartBoostersBought => BoosterKinds.All.Where(BoosterKinds.IsStart).Sum(Bought);

    /// <summary>The totals as they stand now; later events do not reach the copy.</summary>
    public JourneyTotals Snapshot()
    {
        var copy = (JourneyTotals)MemberwiseClone();
        copy._earned = [.. _earned];
        copy._spent = [.. _spent];
        copy._boostersReceived = [.. _boostersReceived];
        copy._boostersUsed = [.. _boostersUsed];
        copy._boostersBought = [.. _boostersBought];
        return copy;
    }

    internal void Count(AttemptPlayed attempt)
    {
        Attempts++;
        PlayTime += attempt.PlayTime;
        if (attempt.IsWon)
        {
            Wins++;
        }
        else
        {
            LivesLost++;
        }

        _earned[(int)CoinSource.Finale] += attempt.FinaleCoins;
        _earned[(int)CoinSource.LevelWon] += attempt.LevelCoins;
        Count(attempt.StartBooster);
        Count(attempt.Reading, attempt.IsWon);
    }

    internal void Count(SessionEnded session)
    {
        Sessions++;
        TimeInSessions += session.Length;
        PlayDeniedByLives += session.PlayDenied;
        LivesBackDuringSessions += session.Lives.BackDuringIt;
        WaitForTheNextLife += session.Lives.UntilNextLife ?? TimeSpan.Zero;
        if (session.Attempts > 0)
        {
            SessionsWithPlay++;
        }

        if (!session.Lives.FullAtStart)
        {
            SessionsStartedBelowFull++;
        }

        switch (session.End)
        {
            case SessionEnd.LivesGone:
                SessionsEndedByLives++;
                break;
            case SessionEnd.NoLifeAtStart:
                SessionsWithoutALife++;
                break;
            case SessionEnd.CeilingReached:
                SessionsEndedAtCeiling++;
                break;
            case SessionEnd.NothingLeftToPlay:
                break;
            default:
                throw new InvalidOperationException($"Unhandled session end {session.End}.");
        }
    }

    internal void Count(RewardGiven reward)
    {
        _earned[(int)reward.Source] += reward.Coins;
        foreach (BoosterKind booster in reward.Boosters)
        {
            _boostersReceived[(int)booster]++;
        }
    }

    /// <summary>A product's coins count as bought, its boosters as received like a reward's.</summary>
    internal void Count(PurchaseMade purchase)
    {
        Purchases++;
        MoneySpent += purchase.Product.Price;
        _earned[(int)CoinSource.BoughtForMoney] += purchase.Product.Coins;
        foreach (BoosterKind booster in purchase.Product.BoostersOneByOne)
        {
            _boostersReceived[(int)booster]++;
        }
    }

    private void Count(StartBoosterDecision decision)
    {
        switch (decision)
        {
            case StartBoosterDecision.UseOwned owned:
                _boostersUsed[(int)owned.Booster]++;
                break;
            case StartBoosterDecision.Buy bought:
                _boostersBought[(int)bought.Booster]++;
                _boostersUsed[(int)bought.Booster]++;
                _spent[(int)CoinSink.StartBooster] += bought.Price;
                break;
            case StartBoosterDecision.WantedUnpaid unpaid:
                UnpaidStartBoosters = UnpaidStartBoosters.With(unpaid.Shortfall);
                break;
            case StartBoosterDecision.None:
                break;
            default:
                throw new InvalidOperationException($"Unhandled start booster decision {decision.GetType().Name}.");
        }
    }

    private void Count(AttemptReading reading, bool won)
    {
        if (reading.RanOutOfMoves)
        {
            TimesOutOfMoves++;
        }

        if (reading.InLevelBoosterUsed is BoosterKind used)
        {
            _boostersUsed[(int)used]++;
        }

        switch (reading.ExtraMoves)
        {
            case ExtraMovesDecision.TakenForAd:
                ExtraMovesByAd++;
                break;
            case ExtraMovesDecision.TakenForCoins bought:
                ExtraMovesBought++;
                _spent[(int)CoinSink.ExtraMoves] += bought.Price;
                break;
            case ExtraMovesDecision.WantedUnpaid unpaid:
                ExtraMovesWantedUnpaid++;
                if (unpaid.Shortfall is Shortfall shortfall)
                {
                    UnpaidExtraMoves = UnpaidExtraMoves.With(shortfall);
                }

                break;
            case ExtraMovesDecision.NotOffered or ExtraMovesDecision.NotWanted:
                break;
            default:
                throw new InvalidOperationException($"Unhandled extra-moves decision {reading.ExtraMoves.GetType().Name}.");
        }

        if (reading.ExtraMoves is not (ExtraMovesDecision.NotOffered or ExtraMovesDecision.NotWanted))
        {
            TimesExtraMovesWanted++;
        }

        if (reading.ExtraMoves.IsTaken && won)
        {
            ExtraMovesThatWon++;
        }
    }
}
