#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Meta;
using Puzzle.Museum;

namespace Puzzle.Client.Flow
{
    /// <summary>Why the rules gave nothing for a request of the wallet's. A closed set (private constructor).</summary>
    public abstract class WalletRefusal
    {
        private WalletRefusal()
        {
        }

        /// <summary>The coins do not cover the price; this is what the player lacks.</summary>
        public sealed class CoinsShort : WalletRefusal
        {
            public CoinsShort(Coins missing)
            {
                Missing = missing;
            }

            public Coins Missing { get; }

            public override string ToString() => $"{Missing} coins short";
        }

        /// <summary>The day's ads for coins are used up.</summary>
        public sealed class NoAdLeftToday : WalletRefusal
        {
            public override string ToString() => "no ad for coins left today";
        }

        /// <summary>The daily reward of this turn was taken; the next is due on this day.</summary>
        public sealed class GiftNotDueYet : WalletRefusal
        {
            public GiftNotDueYet(DayStamp dueOn)
            {
                DueOn = dueOn;
            }

            public DayStamp DueOn { get; }

            public override string ToString() => $"the next gift is due on {DueOn}";
        }
    }

    /// <summary>What the rules said to a request of the wallet's. A closed set (private constructor).</summary>
    public abstract class WalletAnswer
    {
        private WalletAnswer()
        {
        }

        /// <summary>Done: the wallet after it, and what the player received (coins and boosters).</summary>
        public sealed class Received : WalletAnswer
        {
            public Received(Wallet wallet, Reward what)
            {
                Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
                What = what ?? throw new ArgumentNullException(nameof(what));
            }

            public Wallet Wallet { get; }

            public Reward What { get; }

            public override string ToString() => $"received {What}";
        }

        /// <summary>Refused: nothing changed, for this reason.</summary>
        public sealed class Refused : WalletAnswer
        {
            public Refused(WalletRefusal why)
            {
                Why = why ?? throw new ArgumentNullException(nameof(why));
            }

            public WalletRefusal Why { get; }

            public override string ToString() => $"refused: {Why}";
        }
    }

    /// <summary>What delivering a payment of the store came to. A closed set (private constructor).</summary>
    public abstract class Delivery
    {
        private Delivery()
        {
        }

        /// <summary>Given: the progress with the product in the wallet and the payment counted, and what the player received.</summary>
        public sealed class Given : Delivery
        {
            public Given(PlayerProgress progress, Reward what)
            {
                Progress = progress ?? throw new ArgumentNullException(nameof(progress));
                What = what ?? throw new ArgumentNullException(nameof(what));
            }

            public PlayerProgress Progress { get; }

            public Reward What { get; }

            public override string ToString() => $"given {What}";
        }

        /// <summary>The payment was given (or seen) before: nothing changes. The store told of it again.</summary>
        public sealed class AlreadyGiven : Delivery
        {
            public override string ToString() => "given already";
        }

        /// <summary>
        /// The product is sold out to this player (the starter pack bought as often as it is sold): nothing is given,
        /// and the progress counts the payment as seen, so it is answered so once.
        /// </summary>
        public sealed class SoldOut : Delivery
        {
            public SoldOut(PlayerProgress progress)
            {
                Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            }

            public PlayerProgress Progress { get; }

            public override string ToString() => "sold out to this player";
        }
    }

    /// <summary>
    /// What the requests of the museum's pages and of the board do to the player's wallet, by the rules of the
    /// economy and at the time of the player's clock: a booster or a set bought for its price, the coins of an ad that
    /// was watched, the daily reward; the reward of a painting, given in the same change as the piece that completes
    /// it; and what the store was paid for, given once per payment (<see cref="Deliver"/>). A request the rules refuse
    /// changes nothing and says why. In the tester mode (<see cref="WalletHandling.Unlimited"/>) a booster or a set is
    /// bought by the same rules and its price never leaves the coins; an ad and the daily reward are as ever. Whoever
    /// holds the progress keeps what comes back and saves it at once.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class WalletLedger
    {
        private readonly EconomyRules _rules;

        public WalletLedger(EconomyRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>The rules' answer to a request made at <paramref name="now"/> on the player's clock, for a wallet held as the rules have it.</summary>
        /// <exception cref="ArgumentException">A set is asked for that is not the shop's: a price comes from the settings, never from a caller.</exception>
        public WalletAnswer Answer(WalletRequest request, Wallet wallet, DateTimeOffset now) => Answer(request, wallet, now, WalletHandling.Normal);

        /// <summary>The rules' answer to a request made at <paramref name="now"/> on the player's clock, for a wallet held as the client holds it.</summary>
        /// <exception cref="ArgumentException">A set is asked for that is not the shop's: a price comes from the settings, never from a caller.</exception>
        public WalletAnswer Answer(WalletRequest request, Wallet wallet, DateTimeOffset now, WalletHandling handling)
        {
            DayStamp today = DayStamp.Of(now);
            switch (request)
            {
                case WalletRequest.BuyABooster buy:
                    Purchase one = handling.Buy(_rules.Shop.PriceOf(buy.Booster), wallet, paying => _rules.Shop.BuyOne(buy.Booster, paying));
                    return AnswerTo(one, new Reward(Coins.None, new[] { buy.Booster }));
                case WalletRequest.BuyASet buy:
                    Purchase set = handling.Buy(buy.Set.Price, wallet, paying => _rules.Shop.Buy(buy.Set, paying));
                    return AnswerTo(set, new Reward(Coins.None, EachOf(buy.Set.Boosters)));
                case WalletRequest.CoinsForAnAd _:
                    return _rules.Shop.CoinsForAnAd(wallet, today) switch
                    {
                        AdOutcome.Rewarded rewarded => new WalletAnswer.Received(rewarded.Wallet, new Reward(_rules.Shop.AdForCoins.Coins, Array.Empty<BoosterKind>())),
                        AdOutcome.NoneLeftToday _ => new WalletAnswer.Refused(new WalletRefusal.NoAdLeftToday()),
                        _ => throw new InvalidOperationException("An ad for coins ended in a way the ledger does not know."),
                    };
                case WalletRequest.TakeTheDailyGift _:
                    return _rules.Daily.TakenOn(wallet, today) switch
                    {
                        DailyGift.Given given => new WalletAnswer.Received(given.Wallet, given.Reward),
                        DailyGift.NotDueYet notYet => new WalletAnswer.Refused(new WalletRefusal.GiftNotDueYet(notYet.DueOn)),
                        _ => throw new InvalidOperationException("Taking the daily reward ended in a way the ledger does not know."),
                    };
                default:
                    throw new InvalidOperationException($"The ledger does not know the request \"{request}\".");
            }
        }

        /// <summary>
        /// The progress with the pieces that lie in place on a painting's jigsaw: each is the player's, and when they
        /// complete a painting that was not complete, its reward is in the same progress, counted, so it is given once.
        /// Pieces that were placed already change nothing, and neither does a painting opened again once it is whole.
        /// </summary>
        public PlayerProgress WithPiecesInPlace(PlayerProgress progress, PaintingNumber painting, IEnumerable<int> inPlace)
        {
            bool wasComplete = progress.Museum.IsComplete(painting);
            PlayerProgress placed = progress;
            foreach (int piece in inPlace)
            {
                if (!placed.Museum.Placed.IsPlaced(painting, piece))
                {
                    placed = placed.WithPiecePlaced(painting, piece);
                }
            }

            return !wasComplete && placed.Museum.IsComplete(painting)
                ? placed.WithWallet(_rules.PaintingCompleted.GivenTo(placed.Wallet))
                : placed;
        }

        /// <summary>
        /// A payment the store took for a product, put into the progress once: the product's coins and boosters into
        /// the wallet and the payment into the purchases, in one change, so one save holds both and a payment the
        /// store tells of again finds itself given (<see cref="Delivery.AlreadyGiven"/>). The starter pack bought as
        /// often as it is sold gives nothing more (<see cref="Delivery.SoldOut"/>). Nothing here takes a life or
        /// touches a level: what is bought for money never changes difficulty. The tester mode plays no part: a
        /// build that sells for money has none.
        /// </summary>
        /// <exception cref="ArgumentException">The product is not the store's: what a payment gives comes from the settings, never from a caller.</exception>
        public Delivery Deliver(PlayerProgress progress, StoreProduct product, string transactionId)
        {
            StoreCatalogue store = _rules.Shop.Store;
            if (!store.Products.Contains(product))
            {
                throw new ArgumentException($"The store does not sell the {product}.", nameof(product));
            }

            PurchasesMade made = progress.Purchases;
            if (made.HasSeen(transactionId))
            {
                return new Delivery.AlreadyGiven();
            }

            bool isSoldOut = product.Kind == StoreProductKind.StarterPack && made.TimesBought(product.IdSuffix) >= store.StarterPack.TimesPerPlayer;
            if (isSoldOut)
            {
                return new Delivery.SoldOut(progress.WithPurchases(made.WithSeen(transactionId)));
            }

            Reward what = product.Contents;
            return new Delivery.Given(progress.WithWallet(what.AddedTo(progress.Wallet)).WithPurchases(made.WithDelivered(product.IdSuffix, transactionId)), what);
        }

        /// <summary>What completing a painting will give the player, for its jigsaw to show; nothing for a painting that is whole already.</summary>
        public Reward? RewardForCompleting(PlayerProgress progress, PaintingNumber painting) =>
            progress.Museum.IsComplete(painting) ? null : _rules.PaintingCompleted.NextFor(progress.Wallet);

        private static WalletAnswer AnswerTo(Purchase purchase, Reward bought) => purchase switch
        {
            Purchase.Bought done => new WalletAnswer.Received(done.Wallet, bought),
            Purchase.NotEnoughCoins notEnough => new WalletAnswer.Refused(new WalletRefusal.CoinsShort(notEnough.Missing)),
            _ => throw new InvalidOperationException("A purchase ended in a way the ledger does not know."),
        };

        /// <summary>Every booster of a shelf, one by one, in the engine's order.</summary>
        private static BoosterKind[] EachOf(BoosterShelf shelf) =>
            BoosterKinds.All.SelectMany(kind => Enumerable.Repeat(kind, shelf.Count(kind))).ToArray();
    }
}
