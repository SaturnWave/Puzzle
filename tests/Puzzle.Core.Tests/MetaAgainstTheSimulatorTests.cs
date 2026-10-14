using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using Puzzle.Meta.Settings;

namespace Puzzle.Core.Tests;

/// <summary>
/// The game's meta rules (Puzzle.Meta) and the economy simulator's (Puzzle.Economy) are two implementations of one
/// economy: the simulator is not built on the game's rules yet. These tests hold them to the same answers on the
/// same sequences, so that what the simulator's reports say of lives, coins and rewards is true of the game.
/// What the two knowingly do not share is said where it shows: a simulated player starts with an empty wallet,
/// takes one start booster per attempt and uses a bought one at once.
/// </summary>
/// <remarks>The simulator's names are the plain ones in this file; the game's are written with <c>Meta.</c> before them.</remarks>
public class MetaAgainstTheSimulatorTests
{
    /// <summary>The first midnight of a simulated player, as a time on the player's own clock.</summary>
    private static readonly DateTimeOffset FirstMidnight = new(2026, 10, 13, 0, 0, 0, TimeSpan.FromHours(3));

    /// <summary>
    /// Settings under which the small world's players meet everything the replay settles. The fixture's own, with a
    /// flat reward for a win and coins in the daily reward: three free ads a day, coins that come slowly, a reward
    /// booster used after one loss. Much coin, one ad a day and a start booster in every attempt: the extra moves
    /// and the boosters are bought. No offer taken and one life that takes ten hours: every loss ends a session, and
    /// the next one cannot start.
    /// </summary>
    private static readonly string[][] JourneySettings =
    [
        [
            """{ "coins": { "perLevelWon": 7 }, "rewards": { "daily": { "coins": 3 } } }""",
            """{ "player": { "startBoosters": { "useAfterLossesOnLevel": 1, "buyAfterLossesOnLevel": null } } }""",
        ],
        [
            """{ "coins": { "perMoveLeftInFinale": 100 }, "extraMovesOffer": { "freeAdsPerDay": 1 }, "rewards": { "paintingCompleted": { "coins": 80 } } }""",
            """{ "player": { "startBoosters": { "useAfterLossesOnLevel": 0, "buyAfterLossesOnLevel": 0 } } }""",
        ],
        ["""{ "player": { "extraMoves": { "watchesAds": false, "paysCoins": false } }, "lives": { "max": 1, "minutesPerLife": 600 } }"""],
        [
            """{ "start": { "coins": 450, "boosters": { "start-rocket-and-bomb": 2, "start-color-bomb": 1, "start-extra-moves": 0, "hit-cell": 1, "hit-row": 0, "hit-column": 0, "shuffle": 3 } } }""",
            """{ "player": { "startBoosters": { "useAfterLossesOnLevel": 1, "buyAfterLossesOnLevel": 2 } } }""",
        ],
    ];

    private static DateTimeOffset ClockAt(PlayerTime time) => FirstMidnight + time.SinceFirstMidnight;

    private static Meta.DayStamp DayOf(DayNumber day) => Meta.DayStamp.Of(ClockAt(PlayerTime.At(day, new TimeOnly(12, 0))));

    private static Meta.BoosterKind GameKind(BoosterKind simulated) =>
        Meta.BoosterKinds.Find(simulated.Name()) ?? throw new InvalidOperationException($"The game has no booster '{simulated.Name()}'.");

    [Theory]
    [InlineData(5, 30, 1)]
    [InlineData(5, 30, 2)]
    [InlineData(1, 30, 3)]
    [InlineData(5, 960, 4)]
    [InlineData(3, 7, 5)]
    [InlineData(9, 1, 6)]
    public void The_two_life_meters_agree_after_every_step_of_a_long_run_of_losses_and_waits(int max, int minutesPerLife, int seed)
    {
        var settings = new LivesSettings { Max = new LifeCount(max), PerLife = TimeSpan.FromMinutes(minutesPerLife) };
        var rules = new Meta.LifeRules(max, TimeSpan.FromMinutes(minutesPerLife));
        var simulated = new LifeMeter(settings);
        Meta.LifeMeter game = Meta.LifeMeter.Full;
        var luck = new Random(seed);
        var now = new PlayerTime(TimeSpan.FromHours(8.5));
        (int losses, int timesEmpty, int timesFullAgain) = (0, 0, 0);

        for (int step = 0; step < 3000; step++)
        {
            now += Wait(luck, settings.PerLife);
            bool wasShort = !simulated.IsFull;
            simulated.BringUpTo(now);
            game = game.SettledAt(ClockAt(now), rules);
            AssertSameLives(simulated, game, rules, now, step);
            timesFullAgain += wasShort && simulated.IsFull ? 1 : 0;

            // Up to three attempts are lost in a row while there is a life: the run empties the meter and lets it fill up again.
            for (int attempt = luck.Next(4); attempt > 0 && simulated.HasOne; attempt--)
            {
                simulated.LoseOne(now);
                game = game.WithOneLost(ClockAt(now), rules);
                AssertSameLives(simulated, game, rules, now, step);
                losses++;
                timesEmpty += simulated.HasOne ? 0 : 1;
            }
        }

        // The run saw what it is for.
        Assert.True(losses > 500 && timesEmpty > 50 && timesFullAgain > 50, $"{losses} losses, {timesEmpty} times empty, {timesFullAgain} times full again");
    }

    [Fact]
    public void A_loss_at_the_very_moment_a_life_is_due_is_settled_the_same_way()
    {
        var settings = new LivesSettings { Max = new LifeCount(5), PerLife = TimeSpan.FromMinutes(30) };
        var rules = new Meta.LifeRules(5, TimeSpan.FromMinutes(30));
        var simulated = new LifeMeter(settings);
        Meta.LifeMeter game = Meta.LifeMeter.Full;

        // Lost at 0, at 30 sharp (the first life is back that instant, and one is lost again), at 60 sharp, and at 61.
        foreach (int minute in new[] { 0, 30, 60, 61 })
        {
            var now = new PlayerTime(TimeSpan.FromMinutes(minute));
            simulated.LoseOne(now);
            game = game.WithOneLost(ClockAt(now), rules);
            AssertSameLives(simulated, game, rules, now, minute);
        }

        Assert.Equal(3, game.LivesUnder(rules));
        Assert.Equal(TimeSpan.FromMinutes(29), game.UntilNextLife(ClockAt(new PlayerTime(TimeSpan.FromMinutes(61)))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Reward_number_n_holds_the_same_boosters_in_both(int boostersEachTime)
    {
        BoosterKind[][] rotations =
        [
            [.. BoosterKinds.All],
            [BoosterKind.StartColorBomb, BoosterKind.StartRocketAndBomb, BoosterKind.StartExtraMoves],
            [BoosterKind.Shuffle],
            [BoosterKind.HitRow, BoosterKind.HitRow, BoosterKind.StartColorBomb, BoosterKind.HitColumn],
        ];

        foreach (BoosterKind[] rotation in rotations)
        {
            var simulated = new RewardRule { Coins = new Coins(80), BoostersEachTime = boostersEachTime, BoosterRotation = rotation };
            Meta.RewardRotation game = GameRotation(simulated);

            foreach (int number in Enumerable.Range(0, 60).Append(int.MaxValue))
            {
                Assert.Equal(simulated.BoostersOf(number).Select(BoosterKinds.Name), game.Of(number).Boosters.Select(Meta.BoosterKinds.Name));
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void The_daily_reward_falls_on_the_same_days_for_a_player_who_plays_every_day(int everyDays)
    {
        // The simulator, whose players skip no day: a reward on day 1 and then every so many days. The one level is never won, so the journey lasts its thirty days.
        EconomySettings settings = EconomySupport.OnePlayer($$"""{ "rewards": { "daily": { "everyDays": {{everyDays}}, "boostersEachTime": 1 } } }""");
        HandMadeLevels neverWon = new HandMadeLevels().Level(6, EconomySupport.Always(EconomySupport.Unfinished()));
        string[] simulated = EconomySupport.Journey(settings, neverWon).Rewards
            .Select(reward => $"day {reward.At.Day}: {string.Join(' ', reward.Boosters.Select(BoosterKinds.Name))}")
            .ToArray();

        // The game, asked once on each of the same days.
        var rule = new Meta.DailyReward(GameRotation(settings.Rewards.Daily), everyDays);
        Meta.Wallet wallet = Meta.Wallet.Empty;
        var given = new List<string>();
        for (int day = 1; day <= settings.Players.HorizonDays; day++)
        {
            if (rule.TakenOn(wallet, DayOf(new DayNumber(day))) is Meta.DailyGift.Given gift)
            {
                wallet = gift.Wallet;
                given.Add($"day {day}: {string.Join(' ', gift.Reward.Boosters.Select(Meta.BoosterKinds.Name))}");
            }
        }

        Assert.Equal(1 + ((settings.Players.HorizonDays - 1) / everyDays), simulated.Length);
        Assert.Equal(simulated, given);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(0, 0)]
    [InlineData(899, 0)]
    [InlineData(900, 0)]
    [InlineData(900, 1)]
    [InlineData(5000, 0)]
    public void The_terms_of_the_extra_moves_are_what_a_simulated_player_who_wants_them_gets(long balance, int freeAdsLeft)
    {
        // A simulated player who always wants the moves, watches ads and pays coins: an ad while one is left, coins otherwise.
        var habit = new ExtraMovesHabit { TakeWhenGoalsDone = new Percent(0), WatchesAds = true, PaysCoins = true };
        var view = new OutOfMovesView(new Percent(0), LossesOnLevel: 0, new Coins(balance), freeAdsLeft);
        ExtraMovesDecision simulated = new ExtraMovesRule(habit, new Coins(900)).Decide(view);

        var game = new Meta.ExtraMovesRules(freeAdsPerDay: 3, new Meta.Coins(900));
        Meta.DayStamp today = DayOf(new DayNumber(1));
        Meta.Wallet wallet = Meta.Wallet.Empty
            .WithCoins(new Meta.Coins(balance))
            .WithAds(new Meta.AdsWatched(today, forExtraMoves: 3 - freeAdsLeft, forCoins: 0));

        Assert.Equal(freeAdsLeft, game.TermsFor(wallet, today).FreeAdsLeftToday);
        Meta.Wallet after = JourneyReplay.AfterTheExtraMoves(game, habit, simulated, wallet, today);
        Assert.Equal(simulated is ExtraMovesDecision.WantedUnpaid, after.Equals(wallet));
        Assert.Equal(simulated is ExtraMovesDecision.TakenForCoins ? balance - 900 : balance, after.Coins.Amount);
    }

    [Fact]
    public void Simulated_journeys_replayed_through_the_games_rules_give_the_same_lives_coins_and_boosters_at_every_step()
    {
        var met = new List<object>();
        foreach (string[] changes in JourneySettings)
        {
            EconomySettings settings = SmallWorld.Settings(changes);
            var replay = new JourneyReplay(GameRules(settings), settings.Player.ExtraMoves);
            for (int player = 0; player < 6; player++)
            {
                JourneyList journey = EconomySupport.Journey(settings, SmallWorld.Levels(), EconomySupport.Player(index: player, seed: (ulong)(100 + player)));
                replay.AssertSameAtEveryStep(journey);
                met.AddRange(journey.Events);
            }
        }

        // The journeys had what the replay is for: wins and losses, lives that ran out, rewards of both kinds, the
        // extra moves paid both ways and gone without, start boosters used from the shelf and bought.
        AttemptPlayed[] attempts = met.OfType<AttemptPlayed>().ToArray();
        string[] neverMet = new (string What, bool Met)[]
        {
            ("a win", attempts.Any(attempt => attempt.IsWon)),
            ("a loss", attempts.Any(attempt => !attempt.IsWon)),
            ("extra moves for an ad", attempts.Any(attempt => attempt.Reading.ExtraMoves is ExtraMovesDecision.TakenForAd)),
            ("extra moves for coins", attempts.Any(attempt => attempt.Reading.ExtraMoves is ExtraMovesDecision.TakenForCoins)),
            ("extra moves wanted and not paid", attempts.Any(attempt => attempt.Reading.ExtraMoves is ExtraMovesDecision.WantedUnpaid)),
            ("a start booster from the shelf", attempts.Any(attempt => attempt.StartBooster is StartBoosterDecision.UseOwned)),
            ("a start booster bought", attempts.Any(attempt => attempt.StartBooster is StartBoosterDecision.Buy)),
            ("a session the lives ended", met.OfType<SessionEnded>().Any(session => session.End is SessionEnd.LivesGone)),
            ("a session without a life at its start", met.OfType<SessionEnded>().Any(session => session.End is SessionEnd.NoLifeAtStart)),
            ("a painting's reward", met.OfType<RewardGiven>().Any(reward => reward.Source == CoinSource.PaintingCompleted)),
            ("a daily reward", met.OfType<RewardGiven>().Any(reward => reward.Source == CoinSource.Daily)),
            ("a welcome gift", met.OfType<RewardGiven>().Any(reward => reward.Source == CoinSource.WelcomeGift)),
        }.Where(part => !part.Met).Select(part => part.What).ToArray();
        Assert.Empty(neverMet);
    }

    /// <summary>
    /// A wait before the next step: mostly part of a life's time, to the millisecond; sometimes a life's time to
    /// the tick; sometimes long enough for several lives; sometimes a night or a day away.
    /// </summary>
    private static TimeSpan Wait(Random luck, TimeSpan perLife) => luck.Next(10) switch
    {
        0 => perLife,
        1 => perLife * luck.Next(2, 12),
        2 => TimeSpan.FromHours(luck.Next(6, 30)),
        _ => TimeSpan.FromMilliseconds(luck.Next((int)(perLife.TotalMilliseconds * 2 / 3))),
    };

    private static Meta.RewardRotation GameRotation(RewardRule rule) =>
        new(new Meta.Coins(rule.Coins.Amount), rule.BoostersEachTime, rule.BoosterRotation.Select(GameKind).ToArray());

    /// <summary>The game's rules with the numbers of a simulation's settings in them. The numbers go the way the game's own take: through a pack.</summary>
    private static Meta.EconomyRules GameRules(EconomySettings settings)
    {
        EconomyFile pack = MetaSupport.Pack();
        pack.lives.max = settings.Lives.Max.Value;
        pack.lives.minutesPerLife = (int)settings.Lives.PerLife.TotalMinutes;
        pack.coins.perMoveLeftInFinale = settings.Coins.PerMoveLeftInFinale.Amount;
        pack.coins.perLevelWon = settings.Coins.PerLevelWon.Amount;
        pack.rewards.paintingCompleted.coins = settings.Rewards.PaintingCompleted.Coins.Amount;
        pack.rewards.paintingCompleted.boostersEachTime = settings.Rewards.PaintingCompleted.BoostersEachTime;
        pack.rewards.paintingCompleted.boosterRotation = settings.Rewards.PaintingCompleted.BoosterRotation.Select(BoosterKinds.Name).ToArray();
        pack.rewards.daily.coins = settings.Rewards.Daily.Coins.Amount;
        pack.rewards.daily.boostersEachTime = settings.Rewards.Daily.BoostersEachTime;
        pack.rewards.daily.boosterRotation = settings.Rewards.Daily.BoosterRotation.Select(BoosterKinds.Name).ToArray();
        pack.rewards.daily.everyDays = settings.Rewards.DailyEveryDays;
        pack.extraMovesOffer.freeAdsPerDay = settings.ExtraMovesOffer.FreeAdsPerDay;
        pack.extraMovesOffer.priceCoins = settings.ExtraMovesOffer.Price.Amount;
        pack.prices.boosters = settings.Prices.Boosters.Select(price => MetaSupport.Price(price.Key.Name(), price.Value.Amount)).ToArray();
        pack.start.coins = settings.Start.Coins.Amount;
        pack.start.boosters = settings.Start.Boosters.Select(held => new BoosterCountFile { booster = held.Key.Name(), count = held.Value }).ToArray();
        return EconomyFileMapper.ToRules(pack);
    }

    private static void AssertSameLives(LifeMeter simulated, Meta.LifeMeter game, Meta.LifeRules rules, PlayerTime now, int step)
    {
        Assert.True(simulated.Lives.Value == game.LivesUnder(rules), $"step {step}: the simulator holds {simulated.Lives} lives, the game {game.LivesUnder(rules)}");
        Assert.Equal(simulated.IsFull, game.IsFull);
        Assert.Equal(simulated.HasOne, game.HasOneUnder(rules));
        Assert.Equal(simulated.UntilNextLife(now), game.UntilNextLife(ClockAt(now)));
    }

    /// <summary>
    /// Settles everything a simulated journey tells of through the game's rules, on a wallet of the game's, and
    /// holds the wallet to the simulator's own account after each thing. A journey begins with the welcome gift, which
    /// must be the game's starting wallet.
    /// </summary>
    private sealed class JourneyReplay(Meta.EconomyRules game, ExtraMovesHabit habit)
    {
        public void AssertSameAtEveryStep(JourneyList journey)
        {
            Meta.Wallet wallet = Meta.Wallet.Empty;
            foreach (object happened in journey.Events)
            {
                switch (happened)
                {
                    case AttemptPlayed attempt:
                        wallet = After(attempt, wallet);
                        Assert.Equal(attempt.LivesLeft.Value, wallet.Lives.LivesUnder(game.Lives));
                        AssertSameStanding(attempt.After, wallet, $"an attempt on level {attempt.Level}");
                        break;
                    case RewardGiven reward:
                        wallet = After(reward, wallet);
                        AssertSameStanding(reward.After, wallet, $"the {reward.Source} reward of day {reward.At.Day}");
                        break;
                    case SessionEnded session:
                        AssertSameStanding(session.After, wallet, $"a session of day {session.StartedAt.Day}");
                        AssertSameWaitForALife(session, wallet);
                        break;
                    case DayEnded day:
                        AssertSameStanding(day.Standing, wallet, $"the end of day {day.Day}");
                        break;
                    default:
                        Assert.Fail($"The journey told of a {happened.GetType().Name}, which the replay does not know.");
                        break;
                }
            }
        }

        /// <summary>
        /// The wallet after the extra moves were taken or gone without, as the simulated player decided. The game
        /// must allow what was taken, and a player who watches ads pays coins or goes without only when the game
        /// has no free ad left for them either.
        /// </summary>
        public static Meta.Wallet AfterTheExtraMoves(Meta.ExtraMovesRules rules, ExtraMovesHabit habit, ExtraMovesDecision decision, Meta.Wallet wallet, Meta.DayStamp today)
        {
            Meta.ExtraMovesTerms terms = rules.TermsFor(wallet, today);
            bool anAdWasThere = habit.WatchesAds && terms.AnAdPaysToday;
            switch (decision)
            {
                case ExtraMovesDecision.TakenForAd:
                    return Assert.IsType<Meta.AdOutcome.Rewarded>(rules.PaidWithAnAd(wallet, today)).Wallet;
                case ExtraMovesDecision.TakenForCoins paid:
                    Assert.False(anAdWasThere, "The simulated player paid coins while the game still had a free ad for them.");
                    Assert.Equal(paid.Price.Amount, terms.Price.Amount);
                    return Assert.IsType<Meta.Purchase.Bought>(rules.PaidWithCoins(wallet)).Wallet;
                case ExtraMovesDecision.WantedUnpaid unpaid:
                    Assert.False(anAdWasThere, "The simulated player went without while the game still had a free ad for them.");
                    if (unpaid.Shortfall is Shortfall shortfall)
                    {
                        Assert.Equal(new Meta.Purchase.NotEnoughCoins(new Meta.Coins(shortfall.Missing.Amount)), rules.PaidWithCoins(wallet));
                    }

                    return wallet;
                case ExtraMovesDecision.NotOffered or ExtraMovesDecision.NotWanted:
                    return wallet;
                default:
                    throw new InvalidOperationException($"Unhandled extra-moves decision {decision.GetType().Name}.");
            }
        }

        /// <summary>The wallet after an attempt: its start booster, its extra moves, its in-level booster, and what its ending pays or costs.</summary>
        private Meta.Wallet After(AttemptPlayed attempt, Meta.Wallet wallet)
        {
            DateTimeOffset decidedAt = ClockAt(attempt.DecidedAt);
            wallet = wallet.WithLives(wallet.Lives.SettledAt(ClockAt(attempt.StartedAt), game.Lives));
            Assert.True(wallet.Lives.HasOneUnder(game.Lives), "The simulator started an attempt the game has no life for.");

            wallet = AfterTheStartBooster(attempt.StartBooster, wallet);
            wallet = AfterTheExtraMoves(game.ExtraMoves, habit, attempt.Reading.ExtraMoves, wallet, Meta.DayStamp.Of(decidedAt));
            if (attempt.Reading.InLevelBoosterUsed is BoosterKind used)
            {
                wallet = wallet.WithShelf(wallet.Shelf.WithOneFewer(GameKind(used)));
            }

            if (attempt.Reading.Ending is not AttemptEnding.Won won)
            {
                return wallet.WithLives(wallet.Lives.WithOneLost(decidedAt, game.Lives));
            }

            Meta.Coins paid = game.Win.Of(won.MovesLeft.Value);
            Assert.Equal(attempt.FinaleCoins.Amount + attempt.LevelCoins.Amount, paid.Amount);
            return wallet.WithCoins(wallet.Coins + paid);
        }

        private Meta.Wallet After(RewardGiven reward, Meta.Wallet wallet)
        {
            switch (reward.Source)
            {
                case CoinSource.PaintingCompleted:
                    AssertSame(reward, game.PaintingCompleted.NextFor(wallet));
                    return game.PaintingCompleted.GivenTo(wallet);
                case CoinSource.Daily:
                    var gift = Assert.IsType<Meta.DailyGift.Given>(game.Daily.TakenOn(wallet, Meta.DayStamp.Of(ClockAt(reward.At))));
                    AssertSame(reward, gift.Reward);
                    return gift.Wallet;
                case CoinSource.WelcomeGift:
                    Assert.Equal(Meta.Wallet.Empty, wallet);
                    return game.StartingWallet;
                case CoinSource.Finale or CoinSource.LevelWon:
                    throw new InvalidOperationException($"The simulator told of a reward for {reward.Source}, which is a win's pay and no reward.");
                default:
                    throw new InvalidOperationException($"Unhandled source of a reward {reward.Source}.");
            }
        }

        /// <summary>A simulated player who buys a start booster uses it at once: it is bought and taken, and never lies on the shelf.</summary>
        private Meta.Wallet AfterTheStartBooster(StartBoosterDecision decision, Meta.Wallet wallet)
        {
            switch (decision)
            {
                case StartBoosterDecision.UseOwned owned:
                    return wallet.WithShelf(wallet.Shelf.WithOneFewer(GameKind(owned.Booster)));
                case StartBoosterDecision.Buy bought:
                    Assert.Equal(bought.Price.Amount, game.Shop.PriceOf(GameKind(bought.Booster)).Amount);
                    Meta.Wallet paid = Assert.IsType<Meta.Purchase.Bought>(game.Shop.BuyOne(GameKind(bought.Booster), wallet)).Wallet;
                    return paid.WithShelf(paid.Shelf.WithOneFewer(GameKind(bought.Booster)));
                case StartBoosterDecision.WantedUnpaid unpaid:
                    IEnumerable<Meta.Coins> missing = Meta.BoosterKinds.All.Where(Meta.BoosterKinds.IsStart).Select(booster => wallet.Coins.ShortOf(game.Shop.PriceOf(booster)));
                    Assert.Equal(unpaid.Shortfall.Missing.Amount, missing.Min(coins => coins.Amount));
                    return wallet;
                case StartBoosterDecision.None:
                    return wallet;
                default:
                    throw new InvalidOperationException($"Unhandled start booster decision {decision.GetType().Name}.");
            }
        }

        /// <summary>A session that ended for lack of a life says how long the next one takes; the game's meter says the same at that moment.</summary>
        private void AssertSameWaitForALife(SessionEnded session, Meta.Wallet wallet)
        {
            if (session.Lives.UntilNextLife is TimeSpan wait)
            {
                DateTimeOffset ended = ClockAt(session.StartedAt + session.Length);
                Assert.Equal(wait, wallet.Lives.SettledAt(ended, game.Lives).UntilNextLife(ended));
            }
        }

        private static void AssertSame(RewardGiven simulated, Meta.Reward game)
        {
            Assert.Equal(simulated.Coins.Amount, game.Coins.Amount);
            Assert.Equal(simulated.Boosters.Select(BoosterKinds.Name), game.Boosters.Select(Meta.BoosterKinds.Name));
        }

        private static void AssertSameStanding(Standing simulated, Meta.Wallet game, string when)
        {
            Assert.True(simulated.Balance.Amount == game.Coins.Amount, $"after {when}: the simulator holds {simulated.Balance} coins, the game {game.Coins}");
            Assert.All(BoosterKinds.All, kind => Assert.True(
                simulated.Held.Count(kind) == game.Shelf.Count(GameKind(kind)),
                $"after {when}: the simulator holds {simulated.Held.Count(kind)} {kind.Name()}, the game {game.Shelf.Count(GameKind(kind))}"));
        }
    }
}
