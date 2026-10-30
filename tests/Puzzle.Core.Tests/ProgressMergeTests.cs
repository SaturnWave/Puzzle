using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Museum;
using static Puzzle.Core.Tests.CloudSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The three-way merge of a player's progress (docs/research/server-v1.md §3.5), field by field and in the plan's
/// cases: two phones of one player, a reinstall, a new phone; and the properties it must hold whatever the two sides
/// did: no coin is made, a ledger number is never lost, a merge done again changes nothing.
/// </summary>
public class ProgressMergeTests
{
    private static readonly ProgressMerge Merge = new(Rules);

    private static readonly PaintingNumber FirstPainting = new(1);

    /// <summary>A player on level 41: 40 levels won, the first painting whole, 1 000 coins, a few boosters.</summary>
    private static PlayerProgress OnLevelFortyOne()
    {
        PlayerProgress won = NewPlayer().Won(40).WithTrayPlaced(FirstPainting);
        return won.WithWallet(won.Wallet.WithCoins(new Coins(1000)).WithShelf(BoosterShelf.Empty.With(BoosterKind.HitRow, 2).With(BoosterKind.Shuffle, 3)));
    }

    private static PlayerProgress Merged(PlayerProgress baseProgress, PlayerProgress local, PlayerProgress cloud, PurchaseLedger? ledger = null) =>
        Assert.IsType<MergeOutcome.Merged>(Merge.Merge(baseProgress, local, cloud, ledger ?? PurchaseLedger.None)).Progress;

    private static PlayerProgress WithBoosters(PlayerProgress progress, BoosterKind kind, int count) =>
        progress.WithWallet(progress.Wallet.WithShelf(BoosterShelf.Empty.With(kind, count)));

    [Fact]
    public void When_only_one_side_moved_on_that_side_stands_as_it_is()
    {
        PlayerProgress synced = OnLevelFortyOne();
        PlayerProgress played = synced.Won(2);

        Assert.Same(played, Merged(synced, played, synced)); // the cloud is still the base: the phone's progress is uploaded
        Assert.Same(played, Merged(synced, synced, played)); // the phone is still the base: the cloud's is taken
    }

    /// <summary>A write whose answer was lost, read back with the old base: the cloud holds the phone's very progress, and nothing is counted twice.</summary>
    [Fact]
    public void Both_sides_the_same_progress_over_an_old_base_is_that_progress_not_twice_its_gains()
    {
        PlayerProgress synced = OnLevelFortyOne();
        PlayerProgress played = synced.Won(3).WithCoins(1600);

        Assert.Same(played, Merged(synced, played, played));
    }

    /// <summary>The plan's example: base 100, phone A +50 (150), phone B +30 and uploaded (130). A merges to 180.</summary>
    [Fact]
    public void Coins_are_the_clouds_plus_what_the_phone_gained_since_the_base()
    {
        PlayerProgress synced = OnLevelFortyOne().WithCoins(100);
        PlayerProgress phoneA = synced.Won(1).WithCoins(150);
        PlayerProgress phoneB = synced.WithTutorialStepSeen(TutorialStep.Chain).WithCoins(130);

        Assert.Equal(180, Merged(synced, phoneA, phoneB).Wallet.Coins.Amount);
    }

    [Theory]
    [InlineData(1000, 700, 1050, 750)] // the phone spent 300, the other phone earned 50
    [InlineData(100, 0, 0, 0)] // both spent everything: never below none
    [InlineData(100, 50, 0, 0)] // the phone spent 50, the other spent 100: none left, not -50
    [InlineData(0, 25, 80, 105)]
    public void Spending_is_taken_off_and_coins_never_go_below_none(long synced, long local, long cloud, long merged)
    {
        PlayerProgress start = OnLevelFortyOne().WithCoins(synced);

        Assert.Equal(merged, Merged(start, start.Won(1).WithCoins(local), start.WithTutorialStepSeen(TutorialStep.Chain).WithCoins(cloud)).Wallet.Coins.Amount);
    }

    [Fact]
    public void Each_booster_is_the_clouds_plus_the_phones_since_the_base_never_below_none()
    {
        PlayerProgress synced = WithBoosters(OnLevelFortyOne(), BoosterKind.HitRow, 2);
        PlayerProgress local = synced.Won(1).WithWallet(synced.Wallet.WithShelf(BoosterShelf.Empty.With(BoosterKind.HitRow, 1).With(BoosterKind.Shuffle, 4)));
        PlayerProgress cloud = synced.WithTutorialStepSeen(TutorialStep.Chain).WithWallet(synced.Wallet.WithShelf(BoosterShelf.Empty.With(BoosterKind.HitRow, 0).With(BoosterKind.HitCell, 1)));

        BoosterShelf shelf = Merged(synced, local, cloud).Wallet.Shelf;

        Assert.Equal(0, shelf.Count(BoosterKind.HitRow)); // 0 + (1 - 2) is under none
        Assert.Equal(4, shelf.Count(BoosterKind.Shuffle));
        Assert.Equal(1, shelf.Count(BoosterKind.HitCell));
    }

    [Theory]
    [InlineData(45, 2, 43, 5, 45, 2)] // the phone is further on: its attempts
    [InlineData(43, 5, 45, 2, 45, 2)] // the cloud is further on: its attempts
    [InlineData(44, 1, 44, 6, 44, 6)] // the same level: the more attempts, so no seed comes again
    public void Levels_won_are_the_more_and_attempts_follow_the_side_further_on(int localWon, int localTries, int cloudWon, int cloudTries, int won, int tries)
    {
        PlayerProgress synced = OnLevelFortyOne();
        PlayerProgress local = new PlayerProgress(localWon, localTries, synced.Museum.Placed, synced.Wallet, synced.Tutorial);
        PlayerProgress cloud = new PlayerProgress(cloudWon, cloudTries, synced.Museum.Placed, synced.Wallet, synced.Tutorial.With(TutorialStep.Chain));

        PlayerProgress merged = Merged(synced, local, cloud);

        Assert.Equal((won, tries), (merged.LevelsWon, merged.AttemptsOnLevel));
    }

    [Fact]
    public void Pieces_in_place_steps_seen_and_opening_gifts_are_every_one_either_side_has()
    {
        PlayerProgress synced = NewPlayer().Won(14);
        PlayerProgress local = synced.WithPiecePlaced(FirstPainting, 0).WithPiecePlaced(FirstPainting, 3).WithTutorialStepSeen(TutorialStep.Goal);
        PlayerProgress cloud = synced.WithPiecePlaced(FirstPainting, 3).WithPiecePlaced(FirstPainting, 7).WithTutorialStepSeen(TutorialStep.Box);

        PlayerProgress merged = Merged(synced, local, cloud);

        Assert.Equal(new[] { 0, 3, 7 }, merged.Museum.Placed.In(FirstPainting));
        Assert.True(merged.Tutorial.Has(TutorialStep.Goal) && merged.Tutorial.Has(TutorialStep.Box));
    }

    [Fact]
    public void Lives_are_the_phones_own()
    {
        PlayerProgress synced = OnLevelFortyOne();
        var short2 = new LifeMeter(shortOfFull: 2, Noon.AddMinutes(20));
        PlayerProgress local = synced.WithWallet(synced.Wallet.WithLives(short2));
        PlayerProgress cloud = synced.Won(1).WithWallet(synced.Wallet.WithLives(LifeMeter.Full));

        Assert.Equal(short2, Merged(synced, local, cloud).Wallet.Lives);
    }

    [Fact]
    public void The_days_ads_are_the_more_on_the_same_day_and_the_later_days_otherwise()
    {
        PlayerProgress synced = OnLevelFortyOne();
        DayStamp today = DayStamp.Of(Noon);
        PlayerProgress local = synced.WithWallet(synced.Wallet.WithAds(new AdsWatched(today, forExtraMoves: 2, forCoins: 0)));
        PlayerProgress cloud = synced.Won(1).WithWallet(synced.Wallet.WithAds(new AdsWatched(today, forExtraMoves: 1, forCoins: 1)));
        PlayerProgress cloudTomorrow = synced.Won(1).WithWallet(synced.Wallet.WithAds(new AdsWatched(today.Plus(1), forExtraMoves: 0, forCoins: 1)));

        Assert.Equal(new AdsWatched(today, forExtraMoves: 2, forCoins: 1), Merged(synced, local, cloud).Wallet.Ads);
        Assert.Equal(new AdsWatched(today.Plus(1), forExtraMoves: 0, forCoins: 1), Merged(synced, local, cloudTomorrow).Wallet.Ads);
    }

    /// <summary>The plan's purchase case: A buys a pack (number 5) and cannot upload; B finds 5 in the ledger, delivers and uploads; A's merge counts it once.</summary>
    [Fact]
    public void A_payment_both_phones_delivered_is_counted_once()
    {
        PlayerProgress synced = OnLevelFortyOne();
        PlayerProgress phoneA = synced.Bought(Small, "GPA.5", number: 5).Won(1);
        PlayerProgress phoneB = synced.Bought(Small, "GPA.5", number: 5);

        PlayerProgress merged = Merged(synced, phoneA, phoneB, LedgerOf((5, Small, "GPA.5")));

        Assert.Equal(synced.Wallet.Coins.Amount + 500 + 25, merged.Wallet.Coins.Amount); // one pack and A's win
        Assert.Equal(1, merged.Purchases.TimesBought(Small.IdSuffix));
        Assert.Equal("5", merged.Purchases.LedgerDelivered.Text);
    }

    /// <summary>A paid the pack before the server answered and delivered it then (no number); B restored number 5 from the ledger; A numbered its delivery when the server answered. One pack.</summary>
    [Fact]
    public void A_payment_numbered_after_its_delivery_and_restored_on_the_other_phone_is_counted_once()
    {
        PlayerProgress delivered = Assert.IsType<Delivery.Given>(new WalletLedger(Rules).Deliver(OnLevelFortyOne(), Small, "GPA.5")).Progress;
        PlayerProgress synced = delivered; // A uploaded the pack before its number came
        PlayerProgress phoneA = synced.WithPurchases(synced.Purchases.WithLedgerNumber(new LedgerNumber(5)));
        PlayerProgress phoneB = synced.Bought(Small, "GPA.5-restored", number: 5);

        PlayerProgress merged = Merged(synced, phoneA, phoneB, LedgerOf((5, Small, "GPA.5")));

        Assert.Equal(synced.Wallet.Coins.Amount, merged.Wallet.Coins.Amount);
    }

    [Fact]
    public void Payments_delivered_on_one_side_only_all_count_and_their_numbers_are_kept_whole()
    {
        PlayerProgress synced = OnLevelFortyOne().Bought(Small, "GPA.1", number: 1);
        PlayerProgress local = synced.Bought(Large, "GPA.2", number: 2);
        PlayerProgress cloud = synced.Bought(Small, "GPA.3", number: 3);

        PlayerProgress merged = Merged(synced, local, cloud, LedgerOf((1, Small, "GPA.1"), (2, Large, "GPA.2"), (3, Small, "GPA.3")));

        Assert.Equal(synced.Wallet.Coins.Amount + 2500 + 500, merged.Wallet.Coins.Amount);
        Assert.Equal((2, 1), (merged.Purchases.TimesBought(Small.IdSuffix), merged.Purchases.TimesBought(Large.IdSuffix)));
        Assert.Equal("1-3", merged.Purchases.LedgerDelivered.Text);
        Assert.Equal(new[] { "GPA.1", "GPA.3", "GPA.2" }, merged.Purchases.Transactions);
    }

    [Fact]
    public void A_payment_both_sides_delivered_that_the_ledger_cannot_name_stops_the_merge()
    {
        PlayerProgress synced = OnLevelFortyOne();
        PlayerProgress local = synced.Bought(Small, "GPA.5", number: 5);
        PlayerProgress cloud = synced.Bought(Small, "GPA.5", number: 5).Won(1);

        var unaccounted = Assert.IsType<MergeOutcome.Unaccounted>(Merge.Merge(synced, local, cloud, PurchaseLedger.None));

        Assert.Equal(new[] { new LedgerNumber(5) }, unaccounted.Payments);
    }

    [Fact]
    public void The_payments_ids_of_both_are_kept_the_latest_64()
    {
        PlayerProgress synced = OnLevelFortyOne();
        PlayerProgress local = synced;
        PlayerProgress cloud = synced;
        for (int payment = 1; payment <= 40; payment++)
        {
            local = local.WithPurchases(local.Purchases.WithSeen($"A.{payment}"));
            cloud = cloud.WithPurchases(cloud.Purchases.WithSeen($"B.{payment}"));
        }

        IReadOnlyList<string> kept = Merged(synced, local, cloud).Purchases.Transactions;

        Assert.Equal(PurchasesMade.TransactionsKept, kept.Count);
        Assert.Equal("B.17", kept[0]); // the cloud's first, then the phone's; the oldest give way
        Assert.Equal("A.40", kept[^1]);
    }

    /// <summary>The product's count is never fewer than the ledger's numbers of it that the merged progress holds (the starter kit's "once").</summary>
    [Fact]
    public void A_products_count_is_never_fewer_than_the_ledger_has_of_it()
    {
        PlayerProgress synced = OnLevelFortyOne();
        PlayerProgress local = synced.Won(1);
        PlayerProgress cloud = synced.WithPurchases(new PurchasesMade(Array.Empty<(string, int)>(), Array.Empty<string>(), LedgerNumbers.Of(new[] { new LedgerNumber(1), new LedgerNumber(2) })));

        PlayerProgress merged = Merged(synced, local, cloud, LedgerOf((1, Small, "GPA.1"), (2, Small, "GPA.2")));

        Assert.Equal(2, merged.Purchases.TimesBought(Small.IdSuffix));
    }

    [Fact]
    public void The_daily_reward_both_phones_took_on_the_same_day_is_counted_once()
    {
        PlayerProgress synced = OnLevelFortyOne();
        DayStamp today = DayStamp.Of(Noon);
        PlayerProgress local = TookTheDaily(synced.Won(1), today);
        PlayerProgress cloud = TookTheDaily(synced, today);

        PlayerProgress merged = Merged(synced, local, cloud);

        Assert.Equal(synced.Wallet.Coins.Amount + 25 + 5, merged.Wallet.Coins.Amount); // A's win, and one daily reward of 5 coins
        Assert.Equal(1, merged.Wallet.Shelf.Total - synced.Wallet.Shelf.Total);
        Assert.Equal((1, today), (merged.Wallet.Rewards.Daily, merged.Wallet.Rewards.LastDailyOn!.Value));
    }

    [Fact]
    public void Daily_rewards_taken_on_two_days_both_count()
    {
        PlayerProgress synced = OnLevelFortyOne();
        DayStamp today = DayStamp.Of(Noon);
        PlayerProgress local = TookTheDaily(synced, today);
        PlayerProgress cloud = TookTheDaily(synced, today.Plus(1));

        PlayerProgress merged = Merged(synced, local, cloud);

        Assert.Equal(synced.Wallet.Coins.Amount + 10, merged.Wallet.Coins.Amount);
        Assert.Equal((2, today.Plus(1)), (merged.Wallet.Rewards.Daily, merged.Wallet.Rewards.LastDailyOn!.Value));
    }

    [Fact]
    public void A_painting_both_phones_completed_is_rewarded_once_and_two_paintings_twice()
    {
        PlayerProgress synced = NewPlayer().Won(20).WithCoins(1000);
        var ledger = new WalletLedger(Rules);
        PlayerProgress local = ledger.WithPiecesInPlace(synced, FirstPainting, Enumerable.Range(0, 10));
        PlayerProgress cloud = ledger.WithPiecesInPlace(synced, FirstPainting, Enumerable.Range(0, 10)).Won(1);
        PlayerProgress cloudSecond = ledger.WithPiecesInPlace(synced, new PaintingNumber(2), Enumerable.Range(0, 10));

        PlayerProgress same = Merged(synced, local, cloud);
        PlayerProgress two = Merged(synced, local, cloudSecond);

        Assert.Equal((1, 1000 + 80 + 25), (same.Wallet.Rewards.ForPaintings, same.Wallet.Coins.Amount));
        Assert.Equal((2, 1000 + 80 + 80), (two.Wallet.Rewards.ForPaintings, two.Wallet.Coins.Amount));
    }

    /// <summary>A reinstall's base is a new player, who holds the welcome gift: playing levels 1 to 12 again on it opens the spatula's and the row brush's gifts once more, and they are not counted twice.</summary>
    [Fact]
    public void After_a_reinstall_the_cloud_is_taken_and_what_was_played_since_is_added_the_welcome_gifts_once()
    {
        PlayerProgress newPlayer = NewPlayer();
        PlayerProgress cloud = OnLevelFortyOne();
        cloud = cloud.WithWallet(Rules.Unlocks.GivenTo(cloud.Wallet, level: 41));
        PlayerProgress replayed = newPlayer.Won(12);
        replayed = replayed.WithWallet(Rules.Unlocks.GivenTo(replayed.Wallet, level: 13));

        PlayerProgress merged = Merged(newPlayer, replayed, cloud);

        Assert.Equal(40, merged.LevelsWon);
        Assert.Equal(cloud.Wallet.Coins.Amount + (12 * 25), merged.Wallet.Coins.Amount); // the welcome gift's 100 coins are the base's
        Assert.Equal(cloud.Wallet.Shelf, merged.Wallet.Shelf); // the spatula's and the brush's gifts once; the base's boosters cancel out
        Assert.Equal(cloud.Wallet.Rewards.UnlockGifts, merged.Wallet.Rewards.UnlockGifts);
        Assert.Equal(cloud.Museum.Placed, merged.Museum.Placed);
    }

    /// <summary>
    /// Two phones played from one base, each a random mix of wins, spending, packs bought and the other phone's packs
    /// restored from the ledger. Whatever they did, the merge makes no coin (never more than the base and both gains),
    /// keeps every ledger number, counts each pack once, and merging the cloud again changes nothing.
    /// </summary>
    [Fact]
    public void Whatever_two_phones_did_no_coin_is_made_no_number_lost_and_a_second_merge_changes_nothing()
    {
        var random = new Random(20261026);
        for (int round = 0; round < 400; round++)
        {
            PlayerProgress synced = OnLevelFortyOne().WithCoins(random.Next(0, 3000));
            var payments = new List<(long Number, StoreProduct Product, string Order)>();
            var spent = new long[2];
            var earned = new long[2];
            PlayerProgress[] sides = { synced, synced };
            for (int step = 0; step < 12; step++)
            {
                int side = random.Next(2);
                switch (random.Next(4))
                {
                    case 0:
                        sides[side] = sides[side].Won(1);
                        earned[side] += 25;
                        break;
                    case 1:
                        long price = Math.Min(sides[side].Wallet.Coins.Amount, random.Next(0, 400));
                        sides[side] = sides[side].WithCoins(sides[side].Wallet.Coins.Amount - price);
                        spent[side] += price;
                        break;
                    case 2:
                        long number = payments.Count + 1;
                        StoreProduct product = random.Next(2) == 0 ? Small : Large;
                        payments.Add((number, product, $"GPA.{number}"));
                        sides[side] = sides[side].Bought(product, $"GPA.{number}", number);
                        break;
                    default:
                        // The other phone restores from the ledger a pack this one does not hold yet.
                        var owed = payments.Where(payment => !sides[side].Purchases.LedgerDelivered.Contains(new LedgerNumber(payment.Number))).ToArray();
                        if (owed.Length > 0)
                        {
                            var restore = owed[random.Next(owed.Length)];
                            sides[side] = sides[side].Bought(restore.Product, $"{restore.Order}-restored", restore.Number);
                        }

                        break;
                }
            }

            PurchaseLedger ledger = LedgerOf(payments.ToArray());
            PlayerProgress merged = Merged(synced, sides[0], sides[1], ledger);

            long gains = Math.Max(0, sides[0].Wallet.Coins.Amount - synced.Wallet.Coins.Amount) + Math.Max(0, sides[1].Wallet.Coins.Amount - synced.Wallet.Coins.Amount);
            Assert.InRange(merged.Wallet.Coins.Amount, 0, synced.Wallet.Coins.Amount + gains);
            Assert.True(merged.Purchases.LedgerDelivered.Equals(merged.Purchases.LedgerDelivered.Union(sides[0].Purchases.LedgerDelivered).Union(sides[1].Purchases.LedgerDelivered)));

            long packs = payments.Where(payment => merged.Purchases.LedgerDelivered.Contains(new LedgerNumber(payment.Number))).Sum(payment => payment.Product.Coins.Amount);
            long expected = synced.Wallet.Coins.Amount + earned[0] + earned[1] + packs - spent[0] - spent[1];
            Assert.Equal(Math.Max(0, expected), merged.Wallet.Coins.Amount);

            // The cloud now holds the merged progress: merging it again with itself as the base changes nothing.
            Assert.Equal(merged, Merged(merged, merged, merged, ledger));
            Assert.Equal(merged, Merged(sides[1], merged, sides[1], ledger));
        }
    }

    private static PlayerProgress TookTheDaily(PlayerProgress progress, DayStamp day) =>
        progress.WithWallet(Assert.IsType<DailyGift.Given>(Rules.Daily.TakenOn(progress.Wallet, day)).Wallet);
}
