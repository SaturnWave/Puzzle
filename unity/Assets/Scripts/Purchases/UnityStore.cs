#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Puzzle.Client.Purchases
{
    /// <summary>
    /// The platform's store over Unity In-App Purchasing 5.4.4 (com.unity.purchasing, pinned): Google Play on Android,
    /// the App Store on iOS, Unity's fake store in the Editor. It connects without Unity Gaming Services and without a
    /// cloud project (docs/research/iap-and-analytics.md §3.3): no UnityServices.InitializeAsync is called anywhere in
    /// the game. The products are defined here, in code, by their store ids (a coin pack consumable, the starter pack
    /// non-consumable); prices come from the store in the player's currency. What the store answers, on Unity's main
    /// thread, is gathered as news for the game's next step.
    /// <para>
    /// A payment is confirmed (ConfirmPurchase: Google's acknowledgement and consumption, StoreKit's finish) only when
    /// the game asks, once its progress holds the payment: until then the store keeps it as pending and tells of it
    /// again when the game opens. Pending orders the store hands back on opening come through OnPurchasePending
    /// (IAP's own reroute of fetched pending orders, on by default); confirmed orders of the starter pack come through
    /// OnPurchasesFetched as what the player owns, and so does an App Store restore.
    /// </para>
    /// <para>
    /// IAP 5.4.4 itself reports every purchase step (receipt, product, price, device model, language, and the IDFV on
    /// iOS) to Unity's analytics endpoint, with no switch to turn it off; the game does not patch that out
    /// (docs/research/iap-and-analytics.md §3.4). On Android the package asks for com.android.vending.BILLING and
    /// ACCESS_NETWORK_STATE; INTERNET stays withdrawn by AndroidBuild until the analytics stage brings it back.
    /// </para>
    /// </summary>
    public sealed class UnityStore : IStore
    {
        /// <summary>What a payment of the starter pack that the store hands back without an id of its own is called (Unity's fake store does so).</summary>
        private const string OwnedWithoutAnId = "#owned";

        private readonly object _gate = new object();
        private readonly List<StoreNews> _news = new List<StoreNews>();
        private readonly Dictionary<string, PendingOrder> _waiting = new Dictionary<string, PendingOrder>(StringComparer.Ordinal);
        private IReadOnlyList<StoreListing> _listings = Array.Empty<StoreListing>();
        private StoreController? _controller;
        private bool _hasOpened;

        /// <summary>How many times the store has said what the player owns (or could not): a restore ends with the telling after it.</summary>
        private int _ownedTold;

        /// <summary>The telling a restore that succeeded waits for: what the player owns, which IAP fetches once the restore is done; nothing while none waits.</summary>
        private int? _restoreEndsAfterTelling;

        /// <param name="offersRestore">True on the App Store, whose players ask for their purchases back by a button.</param>
        public UnityStore(bool offersRestore)
        {
            OffersRestore = offersRestore;
        }

        /// <summary>The store of the platform the game runs on: the App Store's restore button on iPhone, iPad and Mac.</summary>
        public static UnityStore OfThisPlatform() =>
            new UnityStore(Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer);

        public bool OffersRestore { get; }

        public void Open(IReadOnlyList<StoreListing> listings)
        {
            _listings = listings.ToArray();
            _hasOpened = false;
            StoreController controller;
            try
            {
                controller = _controller ??= Listened(UnityIAPServices.StoreController());
            }
            catch (StoreException noStore)
            {
                // IAP registers its stores only while a game runs: the Editor's edit mode has none.
                Tell(new StoreNews.Unreachable(noStore.Message));
                return;
            }

            if (controller.GetConnectionState() == ConnectionState.Connected)
            {
                FetchTheProducts();
                return;
            }

            // The connection's own outcome comes as OnStoreConnected or OnStoreDisconnected; the task adds nothing.
            _ = controller.Connect();
        }

        public void Buy(StoreListing listing)
        {
            if (_controller == null)
            {
                Tell(new StoreNews.Failed(listing.StoreId, StoreRefusal.NotPaid, "the store was never opened"));
                return;
            }

            _controller.PurchaseProduct(listing.StoreId);
        }

        public void Confirm(StorePayment payment)
        {
            PendingOrder? order;
            lock (_gate)
            {
                if (!_waiting.TryGetValue(payment.TransactionId, out order))
                {
                    return; // confirmed already, or handed back as owned: nothing waits
                }

                _waiting.Remove(payment.TransactionId);
            }

            _controller?.ConfirmPurchase(order);
        }

        public void Restore()
        {
            if (_controller == null)
            {
                Tell(new StoreNews.RestoreEnded(succeeded: false));
                return;
            }

            int toldBefore = _ownedTold;
            _controller.RestoreTransactions((succeeded, error) =>
            {
                if (!succeeded)
                {
                    Debug.LogWarning($"The store could not restore the purchases: {error}");
                    Tell(new StoreNews.RestoreEnded(succeeded: false));
                    return;
                }

                // IAP fetches what the player owns once the restore is done, and tells it after this or before: the
                // restore has ended when that telling is in, so what it brought back comes before its end.
                if (_ownedTold > toldBefore)
                {
                    Tell(new StoreNews.RestoreEnded(succeeded: true));
                }
                else
                {
                    _restoreEndsAfterTelling = toldBefore + 1;
                }
            });
        }

        public IReadOnlyList<StoreNews> TakeNews()
        {
            lock (_gate)
            {
                StoreNews[] taken = _news.ToArray();
                _news.Clear();
                return taken;
            }
        }

        private StoreController Listened(StoreController controller)
        {
            controller.OnStoreConnected += FetchTheProducts;
            controller.OnStoreDisconnected += failure => Tell(new StoreNews.Unreachable(failure.Message));
            controller.OnProductsFetched += Opened;
            controller.OnProductsFetchFailed += failure =>
            {
                if (!_hasOpened)
                {
                    Tell(new StoreNews.Unreachable($"no product was fetched: {failure.FailureReason}"));
                }
            };
            controller.OnPurchasesFetched += Owned;
            controller.OnPurchasesFetchFailed += failure =>
            {
                Debug.LogWarning($"The store could not say what the player owns: {failure.Message}");
                EndTheRestoreAfterTheTelling(succeeded: false);
            };
            controller.OnPurchasePending += Paid;
            controller.OnPurchaseConfirmed += order =>
            {
                if (order is FailedOrder failed)
                {
                    Debug.LogWarning($"The store did not take the confirmation of {StoreIdOf(order)}: {failed.FailureReason} ({failed.Details})");
                }
            };
            controller.OnPurchaseFailed += Failed;
            controller.OnPurchaseDeferred += order => Tell(new StoreNews.Deferred(StoreIdOf(order)));
            return controller;
        }

        private void FetchTheProducts() =>
            _controller?.FetchProducts(_listings
                .Select(listing => new ProductDefinition(listing.StoreId, listing.IsKeptByTheStore ? ProductType.NonConsumable : ProductType.Consumable))
                .ToList());

        /// <summary>The store's prices, of the products it sells now; a store that sells none of them is unreachable to the game.</summary>
        private void Opened(List<Product> products)
        {
            StorePrice[] prices = products
                .Where(product => product.availableToPurchase && product.metadata != null && !string.IsNullOrWhiteSpace(product.metadata.localizedPriceString))
                .Select(product => new StorePrice(product.definition.id, product.metadata.localizedPriceString, product.metadata.localizedPrice, product.metadata.isoCurrencyCode))
                .ToArray();
            if (prices.Length == 0)
            {
                Tell(new StoreNews.Unreachable("the store sells none of the game's products"));
                return;
            }

            _hasOpened = true;
            Tell(new StoreNews.Opened(prices));
            _controller?.FetchPurchases();
        }

        /// <summary>The starter pack the player owns (confirmed before, on this device or another), as the store hands it back.</summary>
        private void Owned(Orders orders)
        {
            foreach (ConfirmedOrder order in orders.ConfirmedOrders)
            {
                string storeId = StoreIdOf(order);
                if (_listings.Any(listing => listing.StoreId == storeId && listing.IsKeptByTheStore))
                {
                    Tell(new StoreNews.Owned(new StorePayment(storeId, TransactionIdOf(order, storeId))));
                }
            }

            EndTheRestoreAfterTheTelling(succeeded: true);
        }

        /// <summary>A telling of what the player owns is in: a restore that waited for it has ended.</summary>
        private void EndTheRestoreAfterTheTelling(bool succeeded)
        {
            _ownedTold++;
            if (_restoreEndsAfterTelling is int waitsFor && _ownedTold >= waitsFor)
            {
                _restoreEndsAfterTelling = null;
                Tell(new StoreNews.RestoreEnded(succeeded));
            }
        }

        private void Paid(PendingOrder order)
        {
            string storeId = StoreIdOf(order);
            var payment = new StorePayment(storeId, TransactionIdOf(order, storeId));
            lock (_gate)
            {
                _waiting[payment.TransactionId] = order;
            }

            Tell(new StoreNews.Paid(payment));
        }

        private void Failed(FailedOrder order)
        {
            string storeId = StoreIdOf(order);
            Tell(order.FailureReason switch
            {
                PurchaseFailureReason.UserCancelled => new StoreNews.Cancelled(storeId),
                PurchaseFailureReason.ExistingPurchasePending => new StoreNews.Failed(storeId, StoreRefusal.AlreadyWaiting, order.Details),
                PurchaseFailureReason.ProductUnavailable => new StoreNews.Failed(storeId, StoreRefusal.NotForSale, order.Details),
                PurchaseFailureReason.DuplicateTransaction => new StoreNews.Failed(storeId, StoreRefusal.NotForSale, order.Details),
                _ => new StoreNews.Failed(storeId, StoreRefusal.NotPaid, $"{order.FailureReason}: {order.Details}"),
            });
        }

        private void Tell(StoreNews news)
        {
            Debug.Log($"The store said: {news}.");
            lock (_gate)
            {
                _news.Add(news);
            }
        }

        /// <summary>The store id of what an order is for; the game buys one product at a time, so an order holds one.</summary>
        private static string StoreIdOf(Order order) => order.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id ?? string.Empty;

        /// <summary>The store's id of the payment; one the store gives none is named after the product, which is then one the store keeps.</summary>
        private static string TransactionIdOf(Order order, string storeId) =>
            string.IsNullOrEmpty(order.Info?.TransactionID) ? storeId + OwnedWithoutAnId : order.Info!.TransactionID;
    }
}
