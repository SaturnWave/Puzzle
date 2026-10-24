using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;
using Puzzle.Museum;

namespace Puzzle.Economy.Simulating;

/// <summary>
/// Walks one player through their days: sessions at the habit's times, attempts while there is a life and the session
/// is short of its ceiling, each attempt read from a stored game. It plays no game itself and decides nothing about
/// difficulty: which stored game an attempt meets depends on the player's seed, the level and the attempt's number,
/// never on what the player owns or has spent, with coins or with money.
/// </summary>
public sealed class PlayerSimulator
{
    private readonly EconomySettings _settings;
    private readonly SimulatedLevels _levels;
    private readonly MeasuredHalf _half;
    private readonly StartBoosterRule _startBoosters;
    private readonly AttemptReader _reader;
    private readonly PurchaseHabit _purchases;

    /// <summary>The in-level boosters whose assumed worth is above 0, in the engine's order; none by default.</summary>
    private readonly BoosterKind[] _inLevelBoostersWithAnEffect;

    public PlayerSimulator(EconomySettings settings, SimulatedLevels levels, MeasuredHalf half = MeasuredHalf.All)
    {
        _settings = settings;
        _levels = levels;
        _half = half;
        _inLevelBoostersWithAnEffect = settings.Player.InLevelBoosters.AssumedMovesWorth
            .Where(assumed => assumed.Value > MoveCount.None)
            .Select(assumed => assumed.Key)
            .Order()
            .ToArray();
        _startBoosters = new StartBoosterRule(settings.Player.StartBoosters, settings.Prices.Boosters);
        _purchases = settings.Player.Purchases;
        var extraMoves = new ExtraMovesRule(settings.Player.ExtraMoves, settings.ExtraMovesOffer.Price, _purchases.PackWhenShortForExtraMoves);
        _reader = new AttemptReader(levels.Spec, settings.Player.InLevelBoosters, extraMoves);
    }

    /// <summary>Plays the player's whole journey: to the last measured level or the last day of the horizon.</summary>
    public void Play(SimulatedPlayer player, IJourneyLog log)
    {
        var journey = new Journey(player, new LifeMeter(_settings.Lives));
        PlayerTime firstSession = PlayerTime.At(new DayNumber(1), _settings.Habit.SessionStarts[0]);
        GiveTheWelcomeGift(journey, firstSession, log);
        ComeTo(new LevelId(1), journey, firstSession, log);
        for (int day = 1; day <= _settings.Players.HorizonDays && !IsOver(journey); day++)
        {
            PlayDay(journey, new DayNumber(day), log);
        }
    }

    private bool IsOver(Journey journey) => journey.LevelsWon >= _levels.Count;

    private void PlayDay(Journey journey, DayNumber day, IJourneyLog log)
    {
        IReadOnlyList<TimeOnly> starts = _settings.Habit.SessionStarts;
        PlayerTime dayBegins = PlayerTime.At(day, starts[0]);
        journey.FreeAdsLeft = _settings.ExtraMovesOffer.FreeAdsPerDay;
        GiveDailyReward(journey, day, dayBegins, log);
        if (_purchases.OnASchedule is ScheduledPurchase scheduled && scheduled.IsDueOn(day))
        {
            Buy(journey, scheduled.Pack, PurchaseOccasion.Scheduled, dayBegins, log);
        }

        foreach (TimeOnly start in starts)
        {
            if (!IsOver(journey))
            {
                PlaySession(journey, PlayerTime.At(day, start), log);
            }
        }

        log.DayEnded(new DayEnded(day, journey.Standing));
    }

    private void PlaySession(Journey journey, PlayerTime start, IJourneyLog log)
    {
        LifeMeter lives = journey.Lives;
        lives.BringUpTo(start);
        bool fullAtStart = lives.IsFull;
        int livesBackBefore = lives.LivesBack;
        PlayerTime now = start;
        int attempts = 0;
        while (true)
        {
            lives.BringUpTo(now);
            TimeSpan length = now - start;
            if (EndBeforeNextAttempt(journey, length, attempts) is SessionEnd end)
            {
                var ofTheTimer = new SessionLives(fullAtStart, lives.LivesBack - livesBackBefore, EndedForLackOfALife(end) ? lives.UntilNextLife(now) : null);
                log.SessionEnded(new SessionEnded(start, length, attempts, end, PlayDenied(end, length), ofTheTimer, journey.Standing));
                return;
            }

            now += PlayAttempt(journey, now, log);
            attempts++;
        }
    }

    /// <summary>Why no further attempt starts; absent when one does. An attempt that has started is always played to its end.</summary>
    private SessionEnd? EndBeforeNextAttempt(Journey journey, TimeSpan length, int attempts)
    {
        if (IsOver(journey))
        {
            return SessionEnd.NothingLeftToPlay;
        }

        if (length >= _settings.Habit.SessionCeiling)
        {
            return SessionEnd.CeilingReached;
        }

        if (!journey.Lives.HasOne)
        {
            return attempts == 0 ? SessionEnd.NoLifeAtStart : SessionEnd.LivesGone;
        }

        return null;
    }

    /// <summary>In this habit nobody sits waiting for a life: what the lives cost is the rest of the session they ended.</summary>
    private TimeSpan PlayDenied(SessionEnd end, TimeSpan length) =>
        EndedForLackOfALife(end) ? _settings.Habit.SessionCeiling - length : TimeSpan.Zero;

    private static bool EndedForLackOfALife(SessionEnd end) => end switch
    {
        SessionEnd.NoLifeAtStart or SessionEnd.LivesGone => true,
        SessionEnd.CeilingReached or SessionEnd.NothingLeftToPlay => false,
        _ => throw new ArgumentOutOfRangeException(nameof(end), end, "Unknown session end."),
    };

    /// <summary>Plays the next attempt of the player's level and settles what it costs and pays.</summary>
    /// <returns>How long the attempt took, the pause before the next one included.</returns>
    private TimeSpan PlayAttempt(Journey journey, PlayerTime startedAt, IJourneyLog log)
    {
        var level = new LevelId(journey.LevelsWon + 1);
        AttemptNumber attempt = journey.Attempt;
        StartBoosterDecision startBooster = _startBoosters.Decide(new BeforeAttemptView(attempt.LossesBefore, journey.Shelf, journey.Balance));
        Settle(journey, startBooster);

        BoosterKind? taken = startBooster.Taken;
        MeasuredLine line = _levels.Line(level, journey.Player.Rung, taken?.BoardStart() ?? BoardStart.Plain);
        GameMet met = GameMet.By(journey.Player.Seed, level, attempt, line, _half);
        AttemptReading reading = _reader.Read(
            met.In(line),
            new AttemptTerms(line.MoveLimit, taken == BoosterKind.StartExtraMoves, InLevelBoosterAtHand(journey.Shelf, level), attempt.LossesBefore, journey.Balance, journey.FreeAdsLeft));

        TimeSpan playTime = _settings.Habit.PerMove * reading.MovesPlayed.Value;
        TimeSpan untilDecided = playTime + (reading.ExtraMoves is ExtraMovesDecision.TakenForAd ? _settings.Habit.PerAd : TimeSpan.Zero);
        TimeSpan duration = untilDecided + _settings.Habit.BetweenAttempts;
        PlayerTime decidedAt = startedAt + untilDecided;
        Settle(journey, reading, decidedAt, log);
        WinCoins coins = SettleEnding(journey, reading.Ending, decidedAt);
        log.AttemptPlayed(new AttemptPlayed(
            level, attempt, met, startedAt, decidedAt, playTime, duration, startBooster, reading, coins.Finale, coins.Level, journey.Lives.Lives, journey.Standing));
        if (reading.Ending is AttemptEnding.Won && level.Value % MuseumPlan.LevelsPerPainting == 0)
        {
            Give(journey, CoinSource.PaintingCompleted, _settings.Rewards.PaintingCompleted, journey.PaintingRewards++, decidedAt, log);
        }

        if (reading.Ending is AttemptEnding.Won)
        {
            ComeTo(new LevelId(journey.LevelsWon + 1), journey, decidedAt, log);
        }

        return duration;
    }

    /// <summary>
    /// A win pays for every move left (bought and booster moves left over are paid too) and moves the player on; a loss
    /// costs a life and the next attempt meets the next seed.
    /// </summary>
    private WinCoins SettleEnding(Journey journey, AttemptEnding ending, PlayerTime decidedAt)
    {
        if (ending is not AttemptEnding.Won won)
        {
            journey.Lives.LoseOne(decidedAt);
            journey.Attempt = journey.Attempt.Next;
            return new WinCoins(Coins.None, Coins.None);
        }

        var coins = new WinCoins(_settings.Coins.PerMoveLeftInFinale * won.MovesLeft.Value, _settings.Coins.PerLevelWon);
        journey.Balance += coins.Finale + coins.Level;
        journey.LevelsWon++;
        journey.Attempt = AttemptNumber.First;
        return coins;
    }

    /// <summary>A start booster is spent when the attempt starts, whether it is then won or lost.</summary>
    private static void Settle(Journey journey, StartBoosterDecision decision)
    {
        switch (decision)
        {
            case StartBoosterDecision.UseOwned owned:
                journey.Shelf = journey.Shelf.WithOneFewer(owned.Booster);
                break;
            case StartBoosterDecision.Buy bought:
                journey.Balance = journey.Balance.Less(bought.Price);
                break;
            case StartBoosterDecision.None or StartBoosterDecision.WantedUnpaid:
                break;
            default:
                throw new InvalidOperationException($"Unhandled start booster decision {decision.GetType().Name}.");
        }
    }

    /// <summary>
    /// What the attempt used: its in-level booster, and the extra moves with what paid for them. A coin pack bought
    /// for them is told as bought when the attempt was decided, before the moves are paid with its coins.
    /// </summary>
    private static void Settle(Journey journey, AttemptReading reading, PlayerTime decidedAt, IJourneyLog log)
    {
        if (reading.InLevelBoosterUsed is BoosterKind used)
        {
            journey.Shelf = journey.Shelf.WithOneFewer(used);
        }

        switch (reading.ExtraMoves)
        {
            case ExtraMovesDecision.TakenForAd:
                journey.FreeAdsLeft--;
                break;
            case ExtraMovesDecision.TakenForCoins bought:
                if (bought.PackBoughtFirst is StoreProduct pack)
                {
                    Buy(journey, pack, PurchaseOccasion.ShortForExtraMoves, decidedAt, log);
                }

                journey.Balance = journey.Balance.Less(bought.Price);
                break;
            case ExtraMovesDecision.NotOffered or ExtraMovesDecision.NotWanted or ExtraMovesDecision.WantedUnpaid:
                break;
            default:
                throw new InvalidOperationException($"Unhandled extra-moves decision {reading.ExtraMoves.GetType().Name}.");
        }
    }

    /// <summary>The first in-level booster, in the engine's order, that is open on the level, the player holds, and the settings assume an effect for.</summary>
    private BoosterKind? InLevelBoosterAtHand(BoosterShelf shelf, LevelId level)
    {
        foreach (BoosterKind booster in _inLevelBoostersWithAnEffect)
        {
            if (shelf.Has(booster) && _settings.Unlocks.IsOpenOn(booster, level))
            {
                return booster;
            }
        }

        return null;
    }

    private void GiveDailyReward(Journey journey, DayNumber day, PlayerTime at, IJourneyLog log)
    {
        if ((day.Value - 1) % _settings.Rewards.DailyEveryDays == 0)
        {
            Give(journey, CoinSource.Daily, _settings.Rewards.Daily, journey.DailyRewards++, at, log);
        }
    }

    /// <summary>
    /// What a new player is given before the first level; it comes first, before the first day's reward. A gift of
    /// nothing is not given, so a journey without one tells of no gift.
    /// </summary>
    private void GiveTheWelcomeGift(Journey journey, PlayerTime at, IJourneyLog log)
    {
        BoosterKind[] boosters = _settings.Start.BoostersOneByOne.ToArray();
        if (_settings.Start.Coins != Coins.None || boosters.Length > 0)
        {
            Receive(journey, CoinSource.WelcomeGift, _settings.Start.Coins, boosters, at, log);
        }
    }

    /// <summary>
    /// The player has come to a level: the boosters used during play that open on it bring their gift, the welcome
    /// gift's part for them, and from the starter pack's level on the shop offers that pack. A level on which nothing
    /// opens, or whose openings bring nothing, gives nothing.
    /// </summary>
    private void ComeTo(LevelId level, Journey journey, PlayerTime at, IJourneyLog log)
    {
        BoosterKind[] gift = _settings.Unlocks.GiftOpeningOn(level).ToArray();
        if (gift.Length > 0)
        {
            Receive(journey, CoinSource.UnlockGift, Coins.None, gift, at, log);
        }

        StarterPackSettings starter = _settings.Store.StarterPack;
        if (_purchases.BuysTheStarterPack && !journey.BoughtTheStarterPack && level.Value >= starter.FromLevel.Value && !IsOver(journey))
        {
            Buy(journey, starter.Product, PurchaseOccasion.StarterPackOffered, at, log);
            journey.BoughtTheStarterPack = true;
        }
    }

    /// <summary>The product's coins and boosters go to the player, who pays its price in money; nothing of it reaches a game.</summary>
    private static void Buy(Journey journey, StoreProduct product, PurchaseOccasion occasion, PlayerTime at, IJourneyLog log)
    {
        journey.Balance += product.Coins;
        foreach (BoosterKind booster in product.BoostersOneByOne)
        {
            journey.Shelf = journey.Shelf.WithOneMore(booster);
        }

        log.PurchaseMade(new PurchaseMade(product, occasion, at, journey.Standing));
    }

    private static void Give(Journey journey, CoinSource source, RewardRule rule, int rewardNumber, PlayerTime at, IJourneyLog log) =>
        Receive(journey, source, rule.Coins, rule.BoostersOf(rewardNumber).ToArray(), at, log);

    private static void Receive(Journey journey, CoinSource source, Coins coins, BoosterKind[] boosters, PlayerTime at, IJourneyLog log)
    {
        journey.Balance += coins;
        foreach (BoosterKind booster in boosters)
        {
            journey.Shelf = journey.Shelf.WithOneMore(booster);
        }

        log.RewardGiven(new RewardGiven(source, at, coins, boosters, journey.Standing));
    }

    /// <summary>What a win paid: for the moves left, and the flat reward of the level.</summary>
    private readonly record struct WinCoins(Coins Finale, Coins Level);

    /// <summary>Everything about one player that changes while they play.</summary>
    private sealed class Journey(SimulatedPlayer player, LifeMeter lives)
    {
        public SimulatedPlayer Player { get; } = player;

        public LifeMeter Lives { get; } = lives;

        /// <summary>The level being played is the one after these.</summary>
        public int LevelsWon { get; set; }

        /// <summary>The next attempt of the level being played.</summary>
        public AttemptNumber Attempt { get; set; }

        public Coins Balance { get; set; }

        public BoosterShelf Shelf { get; set; } = BoosterShelf.Empty;

        public int FreeAdsLeft { get; set; }

        public int PaintingRewards { get; set; }

        public int DailyRewards { get; set; }

        /// <summary>A simulated player buys the starter pack once, the first time the shop offers it.</summary>
        public bool BoughtTheStarterPack { get; set; }

        public Standing Standing => new(LevelsWon, Balance, Shelf);
    }
}
