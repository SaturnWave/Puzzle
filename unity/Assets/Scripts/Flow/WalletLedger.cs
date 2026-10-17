#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>The thing is sold for real money, and this build sells nothing for money.</summary>
        public sealed class NotOnSale : WalletRefusal
        {
            public override string ToString() => "not on sale in this build";
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

    /// <summary>
    /// What the requests of the museum's pages and of the board do to the player's wallet, by the rules of the
    /// economy and at the time of the player's clock: a booster or a set bought for its price, a pack of coins that a
    /// test build does not sell, the coins of an ad that was watched, the daily reward; and the reward of a painting,
    /// given in the same change as the piece that completes it. A request the rules refuse changes nothing and says
    /// why. In the tester mode (<see cref="WalletHandling.Unlimited"/>) a booster or a set is bought by the same rules
    /// and its price never leaves the coins; a pack, an ad and the daily reward are as ever. Whoever holds the progress
    /// keeps what comes back and saves it at once.
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
        /// <exception cref="ArgumentException">A set or a pack is asked for that is not the shop's: a price comes from the settings, never from a caller.</exception>
        public WalletAnswer Answer(WalletRequest request, Wallet wallet, DateTimeOffset now) => Answer(request, wallet, now, WalletHandling.Normal);

        /// <summary>The rules' answer to a request made at <paramref name="now"/> on the player's clock, for a wallet held as the client holds it.</summary>
        /// <exception cref="ArgumentException">A set or a pack is asked for that is not the shop's: a price comes from the settings, never from a caller.</exception>
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
                case WalletRequest.BuyACoinPack buy:
                    if (!_rules.Shop.CoinPacks.Contains(buy.Pack))
                    {
                        throw new ArgumentException($"The shop does not show the {buy.Pack}.", nameof(request));
                    }

                    return AnswerTo(_rules.Shop.Buy(buy.Pack), new Reward(buy.Pack.Coins, Array.Empty<BoosterKind>()));
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

        /// <summary>What completing a painting will give the player, for its jigsaw to show; nothing for a painting that is whole already.</summary>
        public Reward? RewardForCompleting(PlayerProgress progress, PaintingNumber painting) =>
            progress.Museum.IsComplete(painting) ? null : _rules.PaintingCompleted.NextFor(progress.Wallet);

        private static WalletAnswer AnswerTo(Purchase purchase, Reward bought) => purchase switch
        {
            Purchase.Bought done => new WalletAnswer.Received(done.Wallet, bought),
            Purchase.NotEnoughCoins notEnough => new WalletAnswer.Refused(new WalletRefusal.CoinsShort(notEnough.Missing)),
            Purchase.NotOnSale _ => new WalletAnswer.Refused(new WalletRefusal.NotOnSale()),
            _ => throw new InvalidOperationException("A purchase ended in a way the ledger does not know."),
        };

        /// <summary>Every booster of a shelf, one by one, in the engine's order.</summary>
        private static BoosterKind[] EachOf(BoosterShelf shelf) =>
            BoosterKinds.All.SelectMany(kind => Enumerable.Repeat(kind, shelf.Count(kind))).ToArray();
    }
}
