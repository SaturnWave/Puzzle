#nullable enable
using System;
using Puzzle.Client.Purchases;
using Puzzle.Client.Words;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>What a card about a purchase for money says: its nameplate and its two lines.</summary>
    public sealed class PurchaseCardWords
    {
        public PurchaseCardWords(string title, string firstLine, string secondLine)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            FirstLine = firstLine ?? throw new ArgumentNullException(nameof(firstLine));
            SecondLine = secondLine ?? throw new ArgumentNullException(nameof(secondLine));
        }

        public string Title { get; }

        public string FirstLine { get; }

        public string SecondLine { get; }

        public override string ToString() => $"{Title}: {FirstLine} / {SecondLine}";
    }

    /// <summary>
    /// What the shop's store and the cards about a purchase for money say, in the language the game speaks
    /// (<see cref="Speech"/>). The design canvas draws the shop's packs with a "[fiyat]" on their buttons, which is the
    /// store's price as the store words it; it draws none of the cards, whose words are written in the manner of the
    /// cards it does draw. A price is never written here: it is the store's words, put into a line as they come. No
    /// Unity dependency.
    /// </summary>
    public static class PurchaseWords
    {
        /// <summary>What the starter pack's frame in the shop says on its top edge: it is sold once.</summary>
        public static string StarterOnce => Speech.Say(WordId.ShopStarterOnce);

        /// <summary>A pack's button while the store has not said its price yet.</summary>
        public static string PriceComing => Speech.Say(WordId.ShopPriceComing);

        /// <summary>A pack's button while the store does not sell it.</summary>
        public static string PackNotOnSale => Speech.Say(WordId.ShopPackNotOnSale);

        /// <summary>The coins' panel instead of its buttons while the store cannot be reached.</summary>
        public static string StoreUnreachable => Speech.Say(WordId.ShopStoreUnreachable);

        /// <summary>The shop's row that asks the App Store for the purchases back.</summary>
        public static string Restore => Speech.Say(WordId.ShopRestore);

        /// <summary>The answers of the card that asks before paying.</summary>
        public static string Buy => Speech.Say(WordId.PurchaseBuy);
        public static string Decline => Speech.Say(WordId.PurchaseDecline);

        /// <summary>What a product of the store is called, by the settings' name for it.</summary>
        /// <exception cref="ArgumentException">The settings name a product the game has no words for: a new product needs its words first.</exception>
        public static string NameOf(StoreProduct product) => product.Name switch
        {
            "pouch" => Speech.Say(WordId.PackPouch),
            "bag" => Speech.Say(WordId.PackBag),
            "chest" => Speech.Say(WordId.PackChest),
            "treasure" => Speech.Say(WordId.PackTreasure),
            "starter-pack" => Speech.Say(WordId.StarterPackName),
            _ => throw new ArgumentException($"The store's product \"{product.Name}\" has no name in the player's language.", nameof(product)),
        };

        /// <summary>The card that asks before paying: the product's name, whether to buy it for the store's price, and that the store takes the payment.</summary>
        public static PurchaseCardWords Asking(StoreProduct product, StorePrice price) => new PurchaseCardWords(
            NameOf(product), Speech.Say(WordId.PurchaseAskQuestion, ("price", price.Words)), Speech.Say(WordId.PurchaseAskStoreLine));

        /// <summary>
        /// The card that tells where a purchase stands once its green button was pressed; nothing for a course that
        /// shows no such card (idle, asking, or cancelled by the player on the store's own sheet, which said so).
        /// </summary>
        public static PurchaseCardWords? Of(PurchaseState state) => state switch
        {
            PurchaseState.Pending _ => new PurchaseCardWords(
                Speech.Say(WordId.PurchasePendingTitle), Speech.Say(WordId.PurchasePendingLine), Speech.Say(WordId.PurchaseComesWhenPaid)),
            PurchaseState.Deferred _ => new PurchaseCardWords(
                Speech.Say(WordId.PurchaseDeferredTitle), Speech.Say(WordId.PurchaseDeferredLine), Speech.Say(WordId.PurchaseComesWhenPaid)),
            PurchaseState.Delivered delivered => Delivered(delivered),
            PurchaseState.Failed failed => new PurchaseCardWords(
                Speech.Say(WordId.PurchaseFailedTitle), WhyNot(failed), failed.Why == PurchaseFailure.AlreadyWaiting ? Speech.Say(WordId.PurchaseComesWhenPaid) : Speech.Say(WordId.PurchaseNothingPaid)),
            PurchaseState.Nothing _ => null,
            PurchaseState.Asking _ => null,
            PurchaseState.Cancelled _ => null,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "A purchase stands where the cards have no words for."),
        };

        /// <summary>The card that tells what asking for the purchases back came to; nothing while it has not ended.</summary>
        public static PurchaseCardWords? Of(RestoreState state, StoreProduct starterPack)
        {
            if (!(state is RestoreState.Ended ended))
            {
                return null;
            }

            string title = Speech.Say(WordId.RestoreTitle);
            if (!ended.Succeeded)
            {
                return new PurchaseCardWords(title, Speech.Say(WordId.RestoreFailed), Speech.Say(WordId.RestoreTryLater));
            }

            string onlyTheStarter = Speech.Say(WordId.RestoreOnlyTheStarter, ("name", NameOf(starterPack)));
            return ended.BroughtBack.Count == 0
                ? new PurchaseCardWords(title, Speech.Say(WordId.RestoreNothing), onlyTheStarter)
                : new PurchaseCardWords(title, Speech.Say(WordId.RestoreBroughtBack, ("name", NameOf(ended.BroughtBack[0]))), onlyTheStarter);
        }

        /// <summary>The card of a purchase given: how many coins arrived, then thanks, or for boosters that came with them, where they are used.</summary>
        private static PurchaseCardWords Delivered(PurchaseState.Delivered delivered)
        {
            Reward what = delivered.What;
            string coins = Speech.SayCounted(WordId.PurchaseCoinsArrived, what.Coins.Amount, ("coins", NumberWords.Amount(what.Coins.Amount)));
            return new PurchaseCardWords(
                Speech.Say(WordId.NoticeBoughtTitle), coins, what.Boosters.Count == 0 ? Speech.Say(WordId.PurchaseThanks) : ShopWords.HowToUse(what));
        }

        private static string WhyNot(PurchaseState.Failed failed) => failed.Why switch
        {
            PurchaseFailure.StoreClosed => Speech.Say(WordId.PurchaseStoreClosed),
            PurchaseFailure.NotOnSale => Speech.Say(WordId.PurchaseNotOnSale),
            PurchaseFailure.SoldOut => Speech.Say(WordId.PurchaseSoldOut, ("name", NameOf(failed.Bought))),
            PurchaseFailure.NotPaid => Speech.Say(WordId.PurchaseNotPaid),
            PurchaseFailure.AlreadyWaiting => Speech.Say(WordId.PurchaseAlreadyWaiting),
            _ => throw new ArgumentOutOfRangeException(nameof(failed), failed.Why, "A purchase failed in a way the cards have no words for."),
        };
    }
}
