#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Meta;

namespace Puzzle.Client.Purchases
{
    /// <summary>
    /// A product as the platform's store knows it: the game's product, and its id in both stores, which is the
    /// game's application id, a dot and the product's id suffix (com.saturnwave.museumpuzzle.coins.pouch). Putting the
    /// two together here keeps the ids with the application id the build carries.
    /// </summary>
    public sealed class StoreListing
    {
        /// <exception cref="ArgumentException">An application id that is empty, or that begins or ends with a dot.</exception>
        public StoreListing(StoreProduct product, string applicationId)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            if (string.IsNullOrEmpty(applicationId) || applicationId.StartsWith(".", StringComparison.Ordinal) || applicationId.EndsWith(".", StringComparison.Ordinal))
            {
                throw new ArgumentException($"'{applicationId}' is no application id.", nameof(applicationId));
            }

            StoreId = $"{applicationId}.{product.IdSuffix}";
        }

        public StoreProduct Product { get; }

        /// <summary>The product's id in both stores.</summary>
        public string StoreId { get; }

        /// <summary>True for a product the store keeps as the player's once bought (the starter pack); false for one it forgets once confirmed (a coin pack).</summary>
        public bool IsKeptByTheStore => Product.Kind == StoreProductKind.StarterPack;

        /// <summary>Every product of the catalogue as the store knows it, the coin packs first.</summary>
        public static IReadOnlyList<StoreListing> AllOf(StoreCatalogue catalogue, string applicationId) =>
            catalogue.Products.Select(product => new StoreListing(product, applicationId)).ToArray();

        public override string ToString() => StoreId;
    }

    /// <summary>
    /// A product's price as the store words it for the player: in their currency and their manner of writing it
    /// ("₺89,99", "1,99 €"), with the amount and the currency's code beside it. The game shows the words; it never
    /// writes a price of its own, nor works it out from the US price. The amount is a decimal, never a float.
    /// </summary>
    public sealed class StorePrice
    {
        /// <exception cref="ArgumentException">No store id or no words.</exception>
        public StorePrice(string storeId, string words, decimal amount, string currency)
        {
            StoreId = string.IsNullOrEmpty(storeId) ? throw new ArgumentException("A price is a product's.", nameof(storeId)) : storeId;
            Words = string.IsNullOrWhiteSpace(words) ? throw new ArgumentException("A price is said in words.", nameof(words)) : words.Trim();
            Amount = amount;
            Currency = currency ?? string.Empty;
        }

        public string StoreId { get; }

        /// <summary>The price as the store words it for the player: what a button says.</summary>
        public string Words { get; }

        public decimal Amount { get; }

        /// <summary>The currency's ISO 4217 code ("TRY"), as the store gives it; empty when it gives none.</summary>
        public string Currency { get; }

        public override string ToString() => $"{StoreId} at {Words}";
    }

    /// <summary>
    /// A payment the store has taken for a product, which the game delivers and, once its progress is saved with it,
    /// confirms to the store. Until then the store keeps it as waiting and tells of it again (Google refunds one left
    /// unconfirmed for three days). Its id is the store's for the payment; a store that gives none (Unity's fake store
    /// gives a product it keeps none) is given one by its adapter, so every payment has an id.
    /// </summary>
    public sealed class StorePayment
    {
        /// <exception cref="ArgumentException">No store id or no payment id.</exception>
        public StorePayment(string storeId, string transactionId)
        {
            StoreId = string.IsNullOrEmpty(storeId) ? throw new ArgumentException("A payment is for a product.", nameof(storeId)) : storeId;
            TransactionId = string.IsNullOrEmpty(transactionId) ? throw new ArgumentException("A payment has an id.", nameof(transactionId)) : transactionId;
        }

        public string StoreId { get; }

        public string TransactionId { get; }

        public override string ToString() => $"payment {TransactionId} for {StoreId}";
    }

    /// <summary>What the store said. A closed set (private constructor).</summary>
    public abstract class StoreNews
    {
        private StoreNews()
        {
        }

        /// <summary>The store is open, with the prices of the products it sells; one it does not know has none.</summary>
        public sealed class Opened : StoreNews
        {
            public Opened(IReadOnlyList<StorePrice> prices)
            {
                Prices = (prices ?? throw new ArgumentNullException(nameof(prices))).ToArray();
            }

            public IReadOnlyList<StorePrice> Prices { get; }

            public override string ToString() => $"opened with {Prices.Count} prices";
        }

        /// <summary>The store could not be reached, or knows none of the game's products: nothing can be bought now.</summary>
        public sealed class Unreachable : StoreNews
        {
            public Unreachable(string why)
            {
                Why = why ?? string.Empty;
            }

            /// <summary>What the store said, for the log; never shown to a player.</summary>
            public string Why { get; }

            public override string ToString() => $"unreachable: {Why}";
        }

        /// <summary>The store took a payment: a purchase just made, or one it tells of again because the game has not confirmed it.</summary>
        public sealed class Paid : StoreNews
        {
            public Paid(StorePayment payment)
            {
                Payment = payment ?? throw new ArgumentNullException(nameof(payment));
            }

            public StorePayment Payment { get; }

            public override string ToString() => $"paid: {Payment}";
        }

        /// <summary>
        /// The store says the player owns a product it keeps (the starter pack), confirmed before: when it opens, and
        /// when the player asks for their purchases back. There is nothing to confirm.
        /// </summary>
        public sealed class Owned : StoreNews
        {
            public Owned(StorePayment payment)
            {
                Payment = payment ?? throw new ArgumentNullException(nameof(payment));
            }

            public StorePayment Payment { get; }

            public override string ToString() => $"owned: {Payment}";
        }

        /// <summary>The player cancelled the store's payment sheet: nothing was paid.</summary>
        public sealed class Cancelled : StoreNews
        {
            public Cancelled(string storeId)
            {
                StoreId = storeId ?? string.Empty;
            }

            public string StoreId { get; }

            public override string ToString() => $"cancelled: {StoreId}";
        }

        /// <summary>The store did not take the payment, for a reason of its own; nothing was paid.</summary>
        public sealed class Failed : StoreNews
        {
            public Failed(string storeId, StoreRefusal why, string details)
            {
                StoreId = storeId ?? string.Empty;
                Why = why;
                Details = details ?? string.Empty;
            }

            public string StoreId { get; }

            public StoreRefusal Why { get; }

            /// <summary>What the store said, for the log; never shown to a player.</summary>
            public string Details { get; }

            public override string ToString() => $"failed: {StoreId}, {Why} ({Details})";
        }

        /// <summary>
        /// The payment waits for someone else: a parent's approval (Apple's Ask to Buy), or a payment in cash at a shop
        /// (Google's pending purchase). Nothing is paid yet; the store tells of the payment when it is made.
        /// </summary>
        public sealed class Deferred : StoreNews
        {
            public Deferred(string storeId)
            {
                StoreId = storeId ?? string.Empty;
            }

            public string StoreId { get; }

            public override string ToString() => $"deferred: {StoreId}";
        }

        /// <summary>
        /// The store did not take the confirmation of a payment (it was not connected, or failed otherwise): it still
        /// holds the payment as waiting, and the game confirms it again the next time the store opens.
        /// </summary>
        public sealed class NotConfirmed : StoreNews
        {
            public NotConfirmed(StorePayment payment, string why)
            {
                Payment = payment ?? throw new ArgumentNullException(nameof(payment));
                Why = why ?? string.Empty;
            }

            public StorePayment Payment { get; }

            /// <summary>What the store said, for the log; never shown to a player.</summary>
            public string Why { get; }

            public override string ToString() => $"not confirmed: {Payment} ({Why})";
        }

        /// <summary>A request for the player's purchases back has ended; what it brought came as <see cref="Owned"/> or <see cref="Paid"/> before this.</summary>
        public sealed class RestoreEnded : StoreNews
        {
            public RestoreEnded(bool succeeded)
            {
                Succeeded = succeeded;
            }

            public bool Succeeded { get; }

            public override string ToString() => $"restore {(Succeeded ? "ended" : "failed")}";
        }
    }

    /// <summary>Why the store did not take a payment, as far as the game tells reasons apart. A closed set.</summary>
    public enum StoreRefusal
    {
        /// <summary>The store's own reasons: a card declined, a store that cannot take payments now, an error.</summary>
        NotPaid,

        /// <summary>A payment for the product is waiting already; the store tells of it when it is made.</summary>
        AlreadyWaiting,

        /// <summary>The store does not sell the product, or the player owns it already (the starter pack).</summary>
        NotForSale,
    }

    /// <summary>
    /// The platform's store, as the game talks to it: Unity In-App Purchasing over Google Play or the App Store in the
    /// game (UnityStore), Unity's fake store in the Editor, a scripted store in the tests and the Editor's walks. What
    /// the store answers is gathered as news and taken by the game at its next step, in the order it came, so a
    /// store that answers at once and one that answers a minute later are handled alike.
    /// </summary>
    public interface IStore
    {
        /// <summary>True on a platform whose players ask for their purchases back by a button (the App Store); Google Play gives them back by itself.</summary>
        bool OffersRestore { get; }

        /// <summary>Connects and asks for the products and for what the player owns or has paid for unconfirmed; news follows.</summary>
        void Open(IReadOnlyList<StoreListing> listings);

        /// <summary>Opens the store's own payment sheet for the product; news follows.</summary>
        void Buy(StoreListing listing);

        /// <summary>
        /// Tells the store the payment is delivered and saved, so it stops telling of it (and Google does not refund it).
        /// A confirmation the store does not take comes back as <see cref="StoreNews.NotConfirmed"/>.
        /// </summary>
        void Confirm(StorePayment payment);

        /// <summary>Asks the store for what the player owns (the App Store's restore); news follows, and <see cref="StoreNews.RestoreEnded"/> last.</summary>
        void Restore();

        /// <summary>What the store said since this was asked last, in the order it came.</summary>
        IReadOnlyList<StoreNews> TakeNews();
    }
}
