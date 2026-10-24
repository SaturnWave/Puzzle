using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Museum;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// Purchases for real money (<c>PurchasesMade</c>, <c>WalletLedger.Deliver</c>, <c>Checkout</c> and the scripted
/// store, the Unity project's source files, compiled into these tests): the closed course of a purchase from the card
/// that asks to the store's answer; each payment given once, also when the store tells of it again after the game was
/// closed between the payment and its delivery, or between the delivery and the confirmation; a confirmation only once
/// the progress that holds the payment is saved; the starter pack offered from its level and sold once; what a restore
/// brings back; and a test build that never asks the store.
/// </summary>
public class PurchaseTests
{
    private static readonly EconomyRules StoreRules = Rules(InAStoreBuild);
    private static readonly StoreCatalogue Catalogue = StoreRules.Shop.Store;
    private static readonly StoreProduct Small = Catalogue.CoinPacks[0];
    private static readonly StoreProduct Large = Catalogue.CoinPacks[1];
    private static readonly StoreProduct Starter = Catalogue.StarterPack.Product;

    /// <summary>A player on level 25 (past the starter pack's level 20) with 100 coins and nothing bought.</summary>
    private static readonly PlayerProgress OnLevelTwentyFive = new(levelsWon: 24, attemptsOnLevel: 0, PlacedPieces.None, WalletWith(100));

    [Fact]
    public void The_record_of_purchases_counts_each_product_and_keeps_each_payment_once()
    {
        PurchasesMade made = PurchasesMade.None.WithDelivered("coins.small", "GPA.1").WithDelivered("coins.small", "GPA.2").WithSeen("GPA.3");

        Assert.Equal(2, made.TimesBought("coins.small"));
        Assert.Equal(0, made.TimesBought("starter.kit"));
        Assert.Equal(["GPA.1", "GPA.2", "GPA.3"], made.Transactions);
        Assert.True(made.HasSeen("GPA.3"));
        Assert.False(made.HasSeen("GPA.4"));
        Assert.Equal(made, new PurchasesMade([("coins.small", 2)], ["GPA.1", "GPA.2", "GPA.3"]));
        Assert.Throws<ArgumentException>(() => made.WithDelivered("coins.large", "GPA.2"));
        Assert.Throws<ArgumentException>(() => made.WithSeen(string.Empty));
    }

    [Fact]
    public void Only_the_latest_payments_are_kept_so_the_record_stays_small_however_much_is_bought()
    {
        PurchasesMade made = PurchasesMade.None;
        for (int payment = 1; payment <= PurchasesMade.TransactionsKept + 10; payment++)
        {
            made = made.WithDelivered("coins.small", $"GPA.{payment}");
        }

        Assert.Equal(PurchasesMade.TransactionsKept, made.Transactions.Count);
        Assert.Equal("GPA.11", made.Transactions[0]);
        Assert.Equal(PurchasesMade.TransactionsKept + 10, made.TimesBought("coins.small"));
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("coins.small", 0)]
    public void A_record_no_game_could_have_written_is_refused(string product, int times) =>
        Assert.Throws<ArgumentException>(() => new PurchasesMade([(product, times)], []));

    [Fact]
    public void A_record_with_a_payment_twice_or_too_many_payments_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new PurchasesMade([], ["GPA.1", "GPA.1"]));
        Assert.Throws<ArgumentException>(() => new PurchasesMade([], Enumerable.Range(0, PurchasesMade.TransactionsKept + 1).Select(n => $"GPA.{n}")));
        Assert.Throws<ArgumentException>(() => new PurchasesMade([("coins.small", 1), ("coins.small", 2)], []));
    }

    [Fact]
    public void A_payment_for_a_pack_puts_its_coins_into_the_wallet_and_counts_the_payment_once()
    {
        var ledger = new WalletLedger(StoreRules);

        Delivery.Given given = Assert.IsType<Delivery.Given>(ledger.Deliver(OnLevelTwentyFive, Large, "GPA.1"));

        Assert.Equal(new Coins(2600), given.Progress.Wallet.Coins);
        Assert.Equal(new Reward(new Coins(2500), []), given.What);
        Assert.Equal(1, given.Progress.Purchases.TimesBought("coins.large"));
        Assert.Equal((24, 0, OnLevelTwentyFive.Wallet.Lives), (given.Progress.LevelsWon, given.Progress.AttemptsOnLevel, given.Progress.Wallet.Lives));
        Assert.IsType<Delivery.AlreadyGiven>(ledger.Deliver(given.Progress, Large, "GPA.1"));
        Assert.Throws<ArgumentException>(() => ledger.Deliver(OnLevelTwentyFive, Rules().Shop.Store.CoinPacks[0], "GPA.2"));
    }

    [Fact]
    public void The_starter_pack_gives_its_coins_and_boosters_once_and_a_second_payment_gives_nothing()
    {
        var ledger = new WalletLedger(StoreRules);

        Delivery.Given given = Assert.IsType<Delivery.Given>(ledger.Deliver(OnLevelTwentyFive, Starter, "GPA.1"));
        Delivery.SoldOut soldOut = Assert.IsType<Delivery.SoldOut>(ledger.Deliver(given.Progress, Starter, "GPA.2"));

        Assert.Equal(new Coins(1600), given.Progress.Wallet.Coins);
        Assert.All(BoosterKinds.All.Where(BoosterKinds.IsStart), kind => Assert.Equal(OnLevelTwentyFive.Wallet.Shelf.Count(kind) + 1, given.Progress.Wallet.Shelf.Count(kind)));
        Assert.Equal(given.Progress.Wallet, soldOut.Progress.Wallet);
        Assert.Equal(1, soldOut.Progress.Purchases.TimesBought("starter.kit"));
        Assert.True(soldOut.Progress.Purchases.HasSeen("GPA.2"));
    }

    [Fact]
    public void A_test_build_never_asks_the_store_and_its_packs_cannot_be_bought()
    {
        var store = ScriptedStore.AtUsPrices(Catalogue, TestApplicationId, offersRestore: true);
        var checkout = new Checkout(Catalogue, RealMoneySales.NotAvailable, TestApplicationId, store);

        checkout.Open(TimeSpan.Zero);
        checkout.Step(new Keeper(OnLevelTwentyFive));

        Assert.Equal(0, store.TimesOpened);
        Assert.IsType<StoreStatus.NotSold>(checkout.Status);
        Assert.False(checkout.OffersRestore);
        Assert.False(checkout.Ask(Small, PurchasesMade.None, levelNumber: 25));
        StoreView view = checkout.ViewFor(PurchasesMade.None, levelNumber: 25);
        Assert.All(view.CoinPacks, pack => Assert.False(pack.CanBeBought));
        Assert.Null(view.StarterPack);
    }

    [Fact]
    public void The_store_opens_with_its_own_prices_and_a_product_it_does_not_know_cannot_be_bought()
    {
        var store = new ScriptedStore([new StorePrice($"{TestApplicationId}.coins.small", "₺44,99", 44.99m, "TRY")], offersRestore: false);
        var checkout = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, store);

        checkout.Open(TimeSpan.Zero);
        Assert.IsType<StoreStatus.Connecting>(checkout.Status);
        checkout.Step(new Keeper(OnLevelTwentyFive));

        StoreView view = checkout.ViewFor(PurchasesMade.None, levelNumber: 25);
        Assert.IsType<StoreStatus.Open>(view.Status);
        Assert.Equal("₺44,99", view.CoinPacks[0].Price!.Words);
        Assert.False(view.CoinPacks[1].CanBeBought);
        Assert.Null(view.StarterPack);
        Assert.True(checkout.Ask(Large, PurchasesMade.None, levelNumber: 25));
        Assert.Equal(PurchaseFailure.NotOnSale, Assert.IsType<PurchaseState.Failed>(checkout.State).Why);
    }

    [Fact]
    public void A_store_that_knows_none_of_the_products_or_cannot_be_reached_is_unreachable_and_is_asked_again_only_after_a_while()
    {
        var store = new ScriptedStore([new StorePrice("com.other.game.coins", "$0.99", 0.99m, "USD")], offersRestore: false);
        var checkout = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, store);
        var keeper = new Keeper(OnLevelTwentyFive);

        checkout.Open(TimeSpan.Zero);
        checkout.Step(keeper);
        Assert.IsType<StoreStatus.Unreachable>(checkout.Status);
        Assert.True(checkout.Ask(Small, PurchasesMade.None, levelNumber: 25));
        Assert.Equal(PurchaseFailure.StoreClosed, Assert.IsType<PurchaseState.Failed>(checkout.State).Why);
        Assert.True(checkout.Dismiss());

        store.OpensWith(null);
        checkout.OpenAgainIfUnreachable(TimeSpan.FromSeconds(5));
        Assert.Equal(1, store.TimesOpened);
        checkout.OpenAgainIfUnreachable(Checkout.PatienceBeforeAskingAgain);
        checkout.Step(keeper);
        Assert.Equal(2, store.TimesOpened);
        Assert.IsType<StoreStatus.Unreachable>(checkout.Status);

        store.OpensWith(ScriptedStore.UsPricesOf(Catalogue, TestApplicationId));
        checkout.OpenAgainIfUnreachable(Checkout.PatienceBeforeAskingAgain + Checkout.PatienceBeforeAskingAgain);
        checkout.Step(keeper);
        Assert.IsType<StoreStatus.Open>(checkout.Status);
    }

    [Fact]
    public void A_pack_is_paid_only_by_the_green_button_of_the_card_that_asks_and_is_given_and_then_confirmed()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);

        Assert.True(checkout.Ask(Large, keeper.Progress.Purchases, levelNumber: 25));
        PurchaseState.Asking asking = Assert.IsType<PurchaseState.Asking>(checkout.State);
        Assert.Equal((Large, "$4.99"), (asking.Bought, asking.Price.Words));
        Assert.Equal(0, store.SheetsOpened);

        Assert.True(checkout.Pay());
        Assert.IsType<PurchaseState.Pending>(checkout.State);
        Assert.Equal(1, store.SheetsOpened);
        checkout.Step(keeper);

        PurchaseState.Delivered delivered = Assert.IsType<PurchaseState.Delivered>(checkout.State);
        Assert.Equal(new Coins(2500), delivered.What.Coins);
        Assert.Equal(new Coins(2600), keeper.Saved.Wallet.Coins);
        Assert.Single(store.Confirmed);
        Assert.Empty(store.Unconfirmed);
        Assert.True(checkout.Dismiss());
        Assert.Same(PurchaseState.Idle, checkout.State);
    }

    [Fact]
    public void The_card_that_asks_closed_buys_nothing_and_a_request_that_crosses_the_course_is_let_go()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);

        Assert.False(checkout.Pay());
        Assert.False(checkout.Dismiss());
        Assert.True(checkout.Ask(Small, keeper.Progress.Purchases, levelNumber: 25));
        Assert.False(checkout.Ask(Large, keeper.Progress.Purchases, levelNumber: 25));
        Assert.True(checkout.Decline());
        checkout.Step(keeper);

        Assert.Same(PurchaseState.Idle, checkout.State);
        Assert.Equal(0, store.SheetsOpened);
        Assert.Equal(OnLevelTwentyFive, keeper.Progress);
    }

    [Theory]
    [InlineData(ScriptedAnswer.Cancel)]
    [InlineData(ScriptedAnswer.Fail)]
    public void A_sheet_cancelled_or_refused_gives_nothing_and_confirms_nothing(ScriptedAnswer answer)
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);
        store.NextAnswer = answer;

        checkout.Ask(Small, keeper.Progress.Purchases, levelNumber: 25);
        checkout.Pay();
        checkout.Step(keeper);

        if (answer == ScriptedAnswer.Cancel)
        {
            Assert.Equal(Small, Assert.IsType<PurchaseState.Cancelled>(checkout.State).Bought);
        }
        else
        {
            Assert.Equal(PurchaseFailure.NotPaid, Assert.IsType<PurchaseState.Failed>(checkout.State).Why);
        }

        Assert.Equal(OnLevelTwentyFive, keeper.Progress);
        Assert.Empty(store.Confirmed);
        Assert.True(checkout.Dismiss());
        Assert.Same(PurchaseState.Idle, checkout.State);
    }

    [Fact]
    public void A_deferred_payment_is_given_when_it_is_made_also_after_its_card_was_closed()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);
        store.NextAnswer = ScriptedAnswer.Defer;

        checkout.Ask(Small, keeper.Progress.Purchases, levelNumber: 25);
        checkout.Pay();
        checkout.Step(keeper);
        Assert.IsType<PurchaseState.Deferred>(checkout.State);
        Assert.Equal(OnLevelTwentyFive, keeper.Progress);

        checkout.Dismiss();
        store.MakeTheDeferredPayment();
        checkout.Step(keeper);

        Assert.Equal(new Coins(500), Assert.IsType<PurchaseState.Delivered>(checkout.State).What.Coins);
        Assert.Equal(new Coins(600), keeper.Saved.Wallet.Coins);
        Assert.Single(store.Confirmed);
    }

    [Fact]
    public void A_payment_given_while_the_save_fails_is_confirmed_only_once_the_progress_is_saved()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);
        keeper.SavesFail = true;

        checkout.Ask(Small, keeper.Progress.Purchases, levelNumber: 25);
        checkout.Pay();
        checkout.Step(keeper);
        Assert.IsType<PurchaseState.Delivered>(checkout.State);
        Assert.Equal(new Coins(600), keeper.Progress.Wallet.Coins);
        Assert.Empty(store.Confirmed);
        Assert.Single(checkout.WaitingToBeConfirmed);

        keeper.SavesFail = false;
        keeper.SaveAgain();
        checkout.Step(keeper);
        Assert.Single(store.Confirmed);
        Assert.Empty(checkout.WaitingToBeConfirmed);
    }

    [Fact]
    public void A_game_closed_between_the_payment_and_its_delivery_gives_it_once_when_it_opens_again()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);
        checkout.Ask(Large, keeper.Progress.Purchases, levelNumber: 25);
        checkout.Pay();

        // Closed before the store's news was taken: nothing was given or saved, the store holds the payment unconfirmed.
        var reopened = new Keeper(keeper.Saved);
        var afterTheRestart = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, store);
        afterTheRestart.Open(TimeSpan.Zero);
        afterTheRestart.Step(reopened);

        Assert.Equal(new Coins(2600), reopened.Saved.Wallet.Coins);
        Assert.IsType<PurchaseState.Delivered>(afterTheRestart.State);
        Assert.Single(store.Confirmed);
        Assert.Empty(store.Unconfirmed);
    }

    [Fact]
    public void A_game_closed_between_the_save_and_the_confirmation_finds_the_payment_given_and_confirms_it()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);
        keeper.ClosesBeforeTheConfirmation = true;
        checkout.Ask(Large, keeper.Progress.Purchases, levelNumber: 25);
        checkout.Pay();
        checkout.Step(keeper);
        Assert.Empty(store.Confirmed);
        Assert.Equal(new Coins(2600), keeper.Saved.Wallet.Coins);

        var reopened = new Keeper(keeper.Saved);
        var afterTheRestart = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, store);
        afterTheRestart.Open(TimeSpan.Zero);
        afterTheRestart.Step(reopened);

        Assert.Equal(new Coins(2600), reopened.Saved.Wallet.Coins);
        Assert.Same(PurchaseState.Idle, afterTheRestart.State);
        Assert.Single(store.Confirmed);
    }

    [Fact]
    public void The_starter_pack_is_offered_from_its_level_until_it_is_bought_and_is_then_refused()
    {
        PlayerProgress onLevelTen = new(levelsWon: 9, attemptsOnLevel: 0, PlacedPieces.None, WalletWith(100));
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);

        Assert.Null(checkout.ViewFor(onLevelTen.Purchases, levelNumber: 10).StarterPack);
        Assert.Equal("$1.99", checkout.ViewFor(keeper.Progress.Purchases, levelNumber: 20).StarterPack!.Price!.Words);
        Assert.True(checkout.Ask(Starter, onLevelTen.Purchases, levelNumber: 10));
        Assert.Equal(PurchaseFailure.NotOnSale, Assert.IsType<PurchaseState.Failed>(checkout.State).Why);
        checkout.Dismiss();

        checkout.Ask(Starter, keeper.Progress.Purchases, levelNumber: 25);
        checkout.Pay();
        checkout.Step(keeper);
        Assert.Equal(new Coins(1600), keeper.Saved.Wallet.Coins);
        checkout.Dismiss();

        Assert.Null(checkout.ViewFor(keeper.Progress.Purchases, levelNumber: 25).StarterPack);
        Assert.True(checkout.Ask(Starter, keeper.Progress.Purchases, levelNumber: 25));
        Assert.Equal(PurchaseFailure.SoldOut, Assert.IsType<PurchaseState.Failed>(checkout.State).Why);
        Assert.Equal(1, store.SheetsOpened);
        Assert.Equal(new Coins(1600), keeper.Saved.Wallet.Coins);
    }

    [Fact]
    public void A_restore_brings_back_the_starter_pack_to_a_new_install_once_and_never_a_coin_pack()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive, offersRestore: true);
        foreach (StoreProduct product in new[] { Starter, Small })
        {
            checkout.Ask(product, keeper.Progress.Purchases, levelNumber: 25);
            checkout.Pay();
            checkout.Step(keeper);
            checkout.Dismiss();
        }

        // The same phone: the progress holds the starter pack, so nothing comes back.
        Assert.True(checkout.RestorePurchases());
        checkout.Step(keeper);
        Assert.Empty(Assert.IsType<RestoreState.Ended>(checkout.Restore).BroughtBack);
        Assert.True(checkout.DismissTheRestore());
        Assert.Equal(new Coins(2100), keeper.Saved.Wallet.Coins);

        // A new install: a new player's progress, the same account. Opening gives the starter pack back (as Google Play does by
        // itself); a restore afterwards finds it given.
        var newInstall = new Keeper(OnLevelTwentyFive);
        var afterTheInstall = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, store);
        afterTheInstall.Open(TimeSpan.Zero);
        afterTheInstall.Step(newInstall);
        Assert.Equal(new Coins(1600), newInstall.Saved.Wallet.Coins);
        Assert.Equal(1, newInstall.Saved.Purchases.TimesBought("starter.kit"));
        Assert.Equal(0, newInstall.Saved.Purchases.TimesBought("coins.small"));
        afterTheInstall.Dismiss();

        Assert.True(afterTheInstall.RestorePurchases());
        afterTheInstall.Step(newInstall);
        Assert.Empty(Assert.IsType<RestoreState.Ended>(afterTheInstall.Restore).BroughtBack);
        Assert.Equal(new Coins(1600), newInstall.Saved.Wallet.Coins);
    }

    [Fact]
    public void A_restore_into_a_progress_without_the_starter_pack_brings_it_back_and_says_so()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive, offersRestore: true);
        checkout.Ask(Starter, keeper.Progress.Purchases, levelNumber: 25);
        checkout.Pay();
        checkout.Step(keeper);

        var newInstall = new Keeper(OnLevelTwentyFive);
        var afterTheInstall = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, store);
        afterTheInstall.RestorePurchases();
        afterTheInstall.Step(newInstall);

        Assert.Equal([Starter], Assert.IsType<RestoreState.Ended>(afterTheInstall.Restore).BroughtBack);
        Assert.Equal(new Coins(1600), newInstall.Saved.Wallet.Coins);
    }

    [Fact]
    public void A_payment_given_while_another_card_is_up_is_shown_after_it()
    {
        (Checkout checkout, ScriptedStore store, Keeper keeper) = OpenStore(OnLevelTwentyFive);
        store.NextAnswer = ScriptedAnswer.Defer;
        checkout.Ask(Small, keeper.Progress.Purchases, levelNumber: 25);
        checkout.Pay();
        checkout.Step(keeper);
        checkout.Dismiss();

        checkout.Ask(Large, keeper.Progress.Purchases, levelNumber: 25);
        store.MakeTheDeferredPayment();
        checkout.Step(keeper);

        Assert.IsType<PurchaseState.Asking>(checkout.State);
        Assert.Equal(new Coins(600), keeper.Saved.Wallet.Coins);
        Assert.True(checkout.Decline());
        Assert.Equal(new Coins(500), Assert.IsType<PurchaseState.Delivered>(checkout.State).What.Coins);
        Assert.True(checkout.Dismiss());
        Assert.Same(PurchaseState.Idle, checkout.State);
    }

    [Fact]
    public void A_payment_for_a_product_the_game_does_not_sell_is_neither_given_nor_confirmed()
    {
        // A payment the store took for a product this game no longer sells.
        var store = ScriptedStore.AtUsPrices(Catalogue, TestApplicationId, offersRestore: false);
        StoreProduct retired = Rules(pack => pack.store.coinPacks[0].productIdSuffix = "coins.retired").Shop.Store.CoinPacks[0];
        store.Buy(new StoreListing(retired, TestApplicationId));

        var keeper = new Keeper(OnLevelTwentyFive);
        var checkout = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, store);
        checkout.Open(TimeSpan.Zero);
        checkout.Step(keeper);

        Assert.Equal(OnLevelTwentyFive, keeper.Progress);
        Assert.Empty(store.Confirmed);
        Assert.Single(store.Unconfirmed);
    }

    private static (Checkout Checkout, ScriptedStore Store, Keeper Keeper) OpenStore(PlayerProgress progress, bool offersRestore = false)
    {
        var store = ScriptedStore.AtUsPrices(Catalogue, TestApplicationId, offersRestore);
        var checkout = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, store);
        var keeper = new Keeper(progress);
        checkout.Open(TimeSpan.Zero);
        checkout.Step(keeper);
        return (checkout, store, keeper);
    }

    /// <summary>The session's part: gives by the ledger, keeps the progress and saves it, or fails to; or is closed between its save and the next step.</summary>
    private sealed class Keeper(PlayerProgress start) : IPurchaseKeeper
    {
        private readonly WalletLedger _ledger = new(StoreRules);

        public PlayerProgress Progress { get; private set; } = start;

        public PlayerProgress Saved { get; private set; } = start;

        public bool SavesFail { get; set; }

        /// <summary>True to stand for a game closed right after its save: it never reaches the step that would confirm.</summary>
        public bool ClosesBeforeTheConfirmation { get; set; }

        public bool IsSaved => !ClosesBeforeTheConfirmation && Progress.Equals(Saved);

        public Delivery Deliver(StoreProduct product, StorePayment payment)
        {
            Delivery delivery = _ledger.Deliver(Progress, product, payment.TransactionId);
            Progress = delivery switch
            {
                Delivery.Given given => given.Progress,
                Delivery.SoldOut soldOut => soldOut.Progress,
                _ => Progress,
            };
            SaveAgain();
            return delivery;
        }

        public void SaveAgain()
        {
            if (!SavesFail)
            {
                Saved = Progress;
            }
        }
    }
}
