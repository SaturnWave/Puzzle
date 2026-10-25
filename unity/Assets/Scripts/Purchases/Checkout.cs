#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Flow;
using Puzzle.Meta;

namespace Puzzle.Client.Purchases
{
    /// <summary>Where the store stands for the game. A closed set (private constructor).</summary>
    public abstract class StoreStatus
    {
        private StoreStatus()
        {
        }

        /// <summary>A test build: nothing is sold for money (the settings' build.realMoneyPurchases), and the store is never asked.</summary>
        public sealed class NotSold : StoreStatus
        {
            public override string ToString() => "not sold in this build";
        }

        /// <summary>
        /// A build that sells, in a run that cannot keep what it gives: its progress could not be read, or a later
        /// version of the game wrote it, so nothing it saves is there when the game opens again
        /// (<see cref="ProgressKeeping.ForThisRunOnly"/>). Nothing is sold, and the store is never asked: a payment it
        /// holds from an earlier run stays unconfirmed with it, for a run that can save to give.
        /// </summary>
        public sealed class ClosedForThisRun : StoreStatus
        {
            public override string ToString() => "closed for this run, which keeps no progress";
        }

        /// <summary>The store has been asked and has not answered yet.</summary>
        public sealed class Connecting : StoreStatus
        {
            public override string ToString() => "connecting";
        }

        /// <summary>The store is open, with the prices of the game's products it knows.</summary>
        public sealed class Open : StoreStatus
        {
            private readonly IReadOnlyList<StorePrice> _prices;

            public Open(IReadOnlyList<StorePrice> prices)
            {
                _prices = (prices ?? throw new ArgumentNullException(nameof(prices))).ToArray();
            }

            /// <summary>The price the store gives the product, as it words it for the player; nothing when the store does not know it.</summary>
            public StorePrice? PriceOf(StoreListing listing) => _prices.FirstOrDefault(price => price.StoreId == listing.StoreId);

            public override string ToString() => $"open: {string.Join(", ", _prices)}";
        }

        /// <summary>
        /// The store cannot be reached now, or knows none of the game's products (a build whose products the store's
        /// console does not have yet): nothing can be bought. The game asks again when the player comes back to the
        /// shop, and the shop goes on saying the store cannot be reached until the store answers.
        /// </summary>
        public sealed class Unreachable : StoreStatus
        {
            public override string ToString() => "unreachable";
        }
    }

    /// <summary>Why a purchase came to nothing, as the player is told it. A closed set.</summary>
    public enum PurchaseFailure
    {
        /// <summary>The store cannot be reached now.</summary>
        StoreClosed,

        /// <summary>The store does not sell the product (it has no price for it).</summary>
        NotOnSale,

        /// <summary>The starter pack, bought as often as it is sold to a player.</summary>
        SoldOut,

        /// <summary>The store did not take the payment, for a reason of its own.</summary>
        NotPaid,

        /// <summary>A payment for the product waits already; it is delivered when the store says it is made.</summary>
        AlreadyWaiting,
    }

    /// <summary>Why the store hands the game a product to give. A closed set.</summary>
    public enum DeliveryReason
    {
        /// <summary>A payment: one just made, or one the store tells of again until the game confirms it.</summary>
        Payment,

        /// <summary>
        /// A product the store keeps as the player's (the starter pack), bought and confirmed before: given back by the
        /// store by itself when it opens after a new install (Google Play), or by the restore row (the App Store).
        /// Nothing is paid now.
        /// </summary>
        Restore,
    }

    /// <summary>
    /// Where a purchase for real money stands, as the cards over the museum show it. A closed set (private
    /// constructor). The course is: <see cref="Idle"/>; a product pressed, <see cref="Asking"/> (the card that asks
    /// before paying); its green button, <see cref="Pending"/> (the store's payment sheet); and the store's answer:
    /// <see cref="Delivered"/>, <see cref="Cancelled"/>, <see cref="Failed"/> or <see cref="Deferred"/>, each shown
    /// until the player closes its card, which leads back to <see cref="Idle"/>. A sheet the store does not answer (it
    /// was lost while the sheet was up, or has been silent a while) is <see cref="Unanswered"/>, whose card can be
    /// closed. A payment the store makes later (a deferred one approved, an unanswered one made, one told of again after
    /// the game was closed) is <see cref="Delivered"/> from wherever the course stands, once the player has closed the
    /// card in front.
    /// </summary>
    public abstract class PurchaseState
    {
        private PurchaseState()
        {
        }

        /// <summary>Nothing is being bought.</summary>
        public static PurchaseState Idle { get; } = new Nothing();

        /// <summary>The product the course is about; nothing while idle.</summary>
        public abstract StoreProduct? Product { get; }

        /// <summary>Nothing is being bought (<see cref="PurchaseState.Idle"/>).</summary>
        public sealed class Nothing : PurchaseState
        {
            internal Nothing()
            {
            }

            public override StoreProduct? Product => null;

            public override string ToString() => "idle";
        }

        /// <summary>The card asks the player whether to pay this price for the product; nothing is paid yet.</summary>
        public sealed class Asking : PurchaseState
        {
            public Asking(StoreProduct product, StorePrice price)
            {
                Bought = product ?? throw new ArgumentNullException(nameof(product));
                Price = price ?? throw new ArgumentNullException(nameof(price));
            }

            public StoreProduct Bought { get; }

            public StorePrice Price { get; }

            public override StoreProduct? Product => Bought;

            public override string ToString() => $"asking: {Bought.Name} at {Price.Words}";
        }

        /// <summary>The store's payment sheet is up, or the store is taking the payment. Its card has no answer: the store's is awaited.</summary>
        public sealed class Pending : PurchaseState
        {
            public Pending(StoreProduct product)
            {
                Bought = product ?? throw new ArgumentNullException(nameof(product));
            }

            public StoreProduct Bought { get; }

            public override StoreProduct? Product => Bought;

            public override string ToString() => $"pending: {Bought.Name}";
        }

        /// <summary>
        /// The store's payment sheet was opened and the store has not answered: the store was lost while the sheet was
        /// up, or has been silent for <see cref="Checkout.PatienceWithTheSheet"/>. The payment may still be made, and is
        /// then given; nothing says it was not. Its card can be closed: the course goes back to idle, and a payment made
        /// after that is given and shown as any payment given meanwhile.
        /// </summary>
        public sealed class Unanswered : PurchaseState
        {
            public Unanswered(StoreProduct product)
            {
                Bought = product ?? throw new ArgumentNullException(nameof(product));
            }

            public StoreProduct Bought { get; }

            public override StoreProduct? Product => Bought;

            public override string ToString() => $"unanswered: {Bought.Name}";
        }

        /// <summary>In the player's wallet: what came, and whether it was paid for now or given back by the store.</summary>
        public sealed class Delivered : PurchaseState
        {
            public Delivered(StoreProduct product, Reward what, DeliveryReason reason)
            {
                Bought = product ?? throw new ArgumentNullException(nameof(product));
                What = what ?? throw new ArgumentNullException(nameof(what));
                Reason = reason;
            }

            public StoreProduct Bought { get; }

            public Reward What { get; }

            /// <summary>A payment, whose card says it was bought; or the store's giving back, whose card says it came back.</summary>
            public DeliveryReason Reason { get; }

            public override StoreProduct? Product => Bought;

            public override string ToString() => $"delivered ({Reason}): {Bought.Name}, {What}";
        }

        /// <summary>The player cancelled the store's payment sheet; nothing was paid.</summary>
        public sealed class Cancelled : PurchaseState
        {
            public Cancelled(StoreProduct product)
            {
                Bought = product ?? throw new ArgumentNullException(nameof(product));
            }

            public StoreProduct Bought { get; }

            public override StoreProduct? Product => Bought;

            public override string ToString() => $"cancelled: {Bought.Name}";
        }

        /// <summary>Nothing was paid, for this reason.</summary>
        public sealed class Failed : PurchaseState
        {
            public Failed(StoreProduct product, PurchaseFailure why)
            {
                Bought = product ?? throw new ArgumentNullException(nameof(product));
                Why = why;
            }

            public StoreProduct Bought { get; }

            public PurchaseFailure Why { get; }

            public override StoreProduct? Product => Bought;

            public override string ToString() => $"failed: {Bought.Name}, {Why}";
        }

        /// <summary>The payment waits for someone else's approval or for cash at a shop; the product comes when it is paid.</summary>
        public sealed class Deferred : PurchaseState
        {
            public Deferred(StoreProduct product)
            {
                Bought = product ?? throw new ArgumentNullException(nameof(product));
            }

            public StoreProduct Bought { get; }

            public override StoreProduct? Product => Bought;

            public override string ToString() => $"deferred: {Bought.Name}";
        }
    }

    /// <summary>Where asking the store for the player's purchases back stands. A closed set (private constructor).</summary>
    public abstract class RestoreState
    {
        private RestoreState()
        {
        }

        /// <summary>Nothing is asked, or the last answer's card was closed.</summary>
        public static RestoreState None { get; } = new NotAsked();

        public sealed class NotAsked : RestoreState
        {
            internal NotAsked()
            {
            }

            public override string ToString() => "not asked";
        }

        /// <summary>The store has been asked and has not finished.</summary>
        public sealed class Restoring : RestoreState
        {
            public override string ToString() => "restoring";
        }

        /// <summary>The store finished: what came back into the wallet (none when the progress held it all), and whether the store could answer.</summary>
        public sealed class Ended : RestoreState
        {
            public Ended(IReadOnlyList<StoreProduct> broughtBack, bool succeeded)
            {
                BroughtBack = (broughtBack ?? throw new ArgumentNullException(nameof(broughtBack))).ToArray();
                Succeeded = succeeded;
            }

            public IReadOnlyList<StoreProduct> BroughtBack { get; }

            public bool Succeeded { get; }

            public override string ToString() => $"restore {(Succeeded ? "ended" : "failed")}, {BroughtBack.Count} brought back";
        }
    }

    /// <summary>
    /// Whoever holds the player's progress, as the purchase's course needs it: to give what was paid for or given
    /// back, to say whether it is saved, and whether what it saves is kept after the game closes.
    /// </summary>
    public interface IPurchaseKeeper
    {
        /// <summary>Whether the progress this run saves is there when the game opens again; a run that keeps it for itself only is sold nothing.</summary>
        ProgressKeeping Keeping { get; }

        /// <summary>
        /// Gives the product to the player by the rules (WalletLedger.Deliver), keeps the progress after it and saves it
        /// at once. A payment is noted as a purchase made; what the store gives back is not one.
        /// </summary>
        Delivery Deliver(StoreProduct product, StorePayment payment, DeliveryReason reason);

        /// <summary>True while the saved progress is the one held, so every payment given is in it.</summary>
        bool IsSaved { get; }
    }

    /// <summary>A product of the store as the shop offers it: with the store's price, or none while it cannot be bought.</summary>
    public sealed class ProductOffer
    {
        public ProductOffer(StoreProduct product, StorePrice? price)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            Price = price;
        }

        public StoreProduct Product { get; }

        /// <summary>The store's price, as it words it for the player; nothing while the store is not open or does not know the product.</summary>
        public StorePrice? Price { get; }

        public bool CanBeBought => Price != null;
    }

    /// <summary>What the shop and the cards over the museum show of the store at one instant. No Unity dependency.</summary>
    public sealed class StoreView
    {
        public StoreView(
            StoreStatus status, IReadOnlyList<ProductOffer> coinPacks, StoreProduct starterPackProduct, ProductOffer? starterPack, bool offersRestore, PurchaseState purchase,
            RestoreState restore)
        {
            Status = status ?? throw new ArgumentNullException(nameof(status));
            CoinPacks = (coinPacks ?? throw new ArgumentNullException(nameof(coinPacks))).ToArray();
            StarterPackProduct = starterPackProduct ?? throw new ArgumentNullException(nameof(starterPackProduct));
            StarterPack = starterPack;
            OffersRestore = offersRestore;
            Purchase = purchase ?? throw new ArgumentNullException(nameof(purchase));
            Restore = restore ?? throw new ArgumentNullException(nameof(restore));
        }

        public StoreStatus Status { get; }

        /// <summary>The packs of coins, in the order the shop shows them.</summary>
        public IReadOnlyList<ProductOffer> CoinPacks { get; }

        /// <summary>The store's starter pack, offered or not: what a card about getting it back names.</summary>
        public StoreProduct StarterPackProduct { get; }

        /// <summary>The starter pack while it is offered to this player (from its level on, until bought, while the store sells it); nothing otherwise.</summary>
        public ProductOffer? StarterPack { get; }

        /// <summary>True when the shop shows the row that asks for the purchases back (the App Store).</summary>
        public bool OffersRestore { get; }

        public PurchaseState Purchase { get; }

        public RestoreState Restore { get; }

        /// <summary>The store as a shop shows it with no purchase's course behind it: a test build's, or one not yet asked.</summary>
        public static StoreView Unasked(StoreCatalogue catalogue, RealMoneySales sales) => new StoreView(
            sales == RealMoneySales.NotAvailable ? new StoreStatus.NotSold() : (StoreStatus)new StoreStatus.Connecting(),
            catalogue.CoinPacks.Select(pack => new ProductOffer(pack, price: null)).ToArray(), catalogue.StarterPack.Product, starterPack: null, offersRestore: false,
            PurchaseState.Idle, RestoreState.None);
    }

    /// <summary>
    /// The course of a purchase for real money, between the museum's presses and the platform's store, by the closed
    /// set of <see cref="PurchaseState"/>. Nothing is paid without the green button of the card that asks first
    /// (<see cref="Pay"/>), and that card is reached only by a press on a product in the shop. A payment the store took
    /// is given to the player through the keeper, once (the progress counts the payment in the same save as its
    /// coins), and confirmed to the store only once the progress that holds it is saved: a game closed between the two
    /// is told of the payment again when it opens, finds it given, and confirms it then; one closed before the save
    /// gives it then. A confirmation the store does not take is sent again when the store, asked again a while later,
    /// opens. The starter pack is asked for only while it is offered to the player, and a payment for it beyond its
    /// times per player gives nothing. What the store says is taken at <see cref="Step"/>, in the order it came. A
    /// request that does not fit where the course stands (a press that crossed the store's answer) is let go, and says
    /// so by returning false.
    /// <para>
    /// A run whose keeper keeps its progress for itself only (<see cref="ProgressKeeping.ForThisRunOnly"/>) sells
    /// nothing and never asks the store (<see cref="StoreStatus.ClosedForThisRun"/>): whatever it gave would be lost
    /// when the game closes, and a payment of an earlier run that it confirmed would be lost for good.
    /// </para>
    /// <para>
    /// There is no server: the payment is believed as the store hands it to the game on the device, and nothing takes
    /// coins back. A refund the store gives leaves the coins with the player; a player who deletes the game or changes
    /// phones keeps nothing bought before but the starter pack, which the store gives back (by itself on Google Play,
    /// by the restore row on the App Store) and which the card then says came back, not that it was bought.
    /// </para>
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class Checkout
    {
        /// <summary>An unreachable store is asked again at most this often: while the player is in the shop, and wherever the player is while a confirmation it did not take waits.</summary>
        public static readonly TimeSpan PatienceBeforeAskingAgain = TimeSpan.FromSeconds(15);

        /// <summary>
        /// A payment sheet the store has not answered in this long (the session's own time, which stands still while
        /// the game is away behind the store's sheet) lets its card close.
        /// </summary>
        public static readonly TimeSpan PatienceWithTheSheet = TimeSpan.FromSeconds(30);

        /// <summary>Payments delivered while a card stood in front wait to be shown; beyond this many the oldest card is skipped (its coins are given all the same).</summary>
        private const int DeliveredCardsKept = 8;

        private readonly StoreCatalogue _catalogue;
        private readonly IStore _store;
        private readonly IReadOnlyList<StoreListing> _listings;
        private readonly bool _sells;
        private readonly List<StorePayment> _toConfirm = new List<StorePayment>();

        /// <summary>Payments whose confirmation the store did not take: confirmed again once the store opens anew.</summary>
        private readonly List<StorePayment> _toConfirmAgain = new List<StorePayment>();
        private readonly Queue<PurchaseState.Delivered> _deliveredMeanwhile = new Queue<PurchaseState.Delivered>();
        private readonly List<StoreProduct> _restored = new List<StoreProduct>();
        private TimeSpan? _askedAt;

        /// <summary>When the store's payment sheet was opened last: a <see cref="PurchaseState.Pending"/> course waits from here. Nothing before the first sheet.</summary>
        private TimeSpan? _sheetOpenedAt;

        /// <param name="applicationId">The game's application id, which the products' ids in the stores begin with.</param>
        public Checkout(StoreCatalogue catalogue, RealMoneySales sales, string applicationId, IStore store)
        {
            _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _listings = StoreListing.AllOf(catalogue, applicationId);
            _sells = sales switch
            {
                RealMoneySales.NotAvailable => false,
                RealMoneySales.ThroughTheStore => true,
                _ => throw new ArgumentOutOfRangeException(nameof(sales), sales, "A build sells for money or does not."),
            };
            Status = _sells ? new StoreStatus.Connecting() : (StoreStatus)new StoreStatus.NotSold();
        }

        public StoreStatus Status { get; private set; }

        public PurchaseState State { get; private set; } = PurchaseState.Idle;

        public RestoreState Restore { get; private set; } = RestoreState.None;

        /// <summary>True when the shop shows the row that asks for the purchases back: a build that sells, on the App Store, in a run that keeps its progress.</summary>
        public bool OffersRestore => _sells && _store.OffersRestore && !(Status is StoreStatus.ClosedForThisRun);

        /// <summary>Payments given whose confirmation waits for the progress to be saved, or for the store to take it.</summary>
        public IReadOnlyList<StorePayment> WaitingToBeConfirmed => _toConfirm.Concat(_toConfirmAgain).ToArray();

        /// <summary>Every product as the store knows it.</summary>
        public IReadOnlyList<StoreListing> Listings => _listings;

        /// <summary>
        /// Asks the store to open, at the session's start; a test build asks nothing, and nor does a run whose keeper
        /// keeps its progress for this run only: its store is closed for the run.
        /// </summary>
        /// <param name="now">How long the session has run: the game's own time, which no change of the device's clock moves.</param>
        public void Open(TimeSpan now, IPurchaseKeeper keeper)
        {
            if (!_sells)
            {
                return;
            }

            if (keeper.Keeping != ProgressKeeping.AfterClosing)
            {
                Status = new StoreStatus.ClosedForThisRun();
                return;
            }

            Status = new StoreStatus.Connecting();
            AskTheStore(now);
        }

        /// <summary>
        /// Asks an unreachable store again, when the player is in the shop and the last asking is a while ago. The
        /// shop goes on saying the store cannot be reached until it answers: asking is no news to the player.
        /// </summary>
        /// <param name="now">How long the session has run, as <see cref="Open"/> was told.</param>
        public void OpenAgainIfUnreachable(TimeSpan now)
        {
            if (Status is StoreStatus.Unreachable && IsTimeToAskAgain(now))
            {
                AskTheStore(now);
            }
        }

        /// <summary>
        /// The session's time, told at every step, wherever the player is: a payment sheet the store has been silent on
        /// for <see cref="PatienceWithTheSheet"/> becomes <see cref="PurchaseState.Unanswered"/>, whose card can be
        /// closed; and while a confirmation the store did not take waits on a store that was lost, the store is asked
        /// again a while later, so that the confirmation goes to it once it opens. A store that is open and refused a
        /// confirmation all the same is not pressed: the confirmation goes again at its next opening.
        /// </summary>
        /// <param name="now">How long the session has run, as <see cref="Open"/> was told.</param>
        public void PassTime(TimeSpan now)
        {
            if (State is PurchaseState.Pending pending && _sheetOpenedAt is TimeSpan opened && now - opened >= PatienceWithTheSheet)
            {
                State = new PurchaseState.Unanswered(pending.Bought);
            }

            if (_toConfirmAgain.Count > 0 && Status is StoreStatus.Unreachable && IsTimeToAskAgain(now))
            {
                AskTheStore(now);
            }
        }

        /// <summary>
        /// A product of the shop was pressed: the card that asks before paying, with the store's price; or, when it
        /// cannot be bought now, the card that says why. Only while nothing else is being bought.
        /// </summary>
        /// <param name="levelNumber">The level the player stands on, counted from 1: the starter pack is offered from its level on.</param>
        public bool Ask(StoreProduct product, PurchasesMade made, int levelNumber)
        {
            StoreListing listing = ListingOf(product);
            if (!(State is PurchaseState.Nothing) || !_sells)
            {
                return false;
            }

            StarterPack starter = _catalogue.StarterPack;
            PurchaseFailure? notOffered = product.Kind != StoreProductKind.StarterPack ? (PurchaseFailure?)null
                : made.TimesBought(product.IdSuffix) >= starter.TimesPerPlayer ? PurchaseFailure.SoldOut
                : levelNumber < starter.FromLevel ? PurchaseFailure.NotOnSale
                : (PurchaseFailure?)null;
            State = notOffered is PurchaseFailure refused ? new PurchaseState.Failed(product, refused)
                : !(Status is StoreStatus.Open open) ? new PurchaseState.Failed(product, PurchaseFailure.StoreClosed)
                : open.PriceOf(listing) is StorePrice price ? new PurchaseState.Asking(product, price)
                : (PurchaseState)new PurchaseState.Failed(product, PurchaseFailure.NotOnSale);
            return true;
        }

        /// <summary>The card that asks was answered with its green button: the store's payment sheet opens.</summary>
        /// <param name="now">How long the session has run, as <see cref="Open"/> was told: the sheet's silence is counted from here.</param>
        public bool Pay(TimeSpan now)
        {
            if (!(State is PurchaseState.Asking asking))
            {
                return false;
            }

            State = new PurchaseState.Pending(asking.Bought);
            _sheetOpenedAt = now;
            _store.Buy(ListingOf(asking.Bought));
            return true;
        }

        /// <summary>The card that asks was closed: nothing is bought; a payment given meanwhile is shown next.</summary>
        public bool Decline()
        {
            if (!(State is PurchaseState.Asking))
            {
                return false;
            }

            State = DeliveredMeanwhileOrIdle();
            return true;
        }

        /// <summary>
        /// The card that told what a purchase came to was closed, or the card of a sheet the store has not answered: the
        /// next payment given meanwhile, or nothing. An unanswered payment made later is still given, and shown then.
        /// </summary>
        public bool Dismiss()
        {
            bool hasEnded = State is PurchaseState.Delivered || State is PurchaseState.Cancelled || State is PurchaseState.Failed || State is PurchaseState.Deferred
                || State is PurchaseState.Unanswered;
            if (!hasEnded)
            {
                return false;
            }

            State = DeliveredMeanwhileOrIdle();
            return true;
        }

        /// <summary>The shop's row asks the store for the player's purchases back.</summary>
        public bool RestorePurchases()
        {
            if (!OffersRestore || Restore is RestoreState.Restoring)
            {
                return false;
            }

            _restored.Clear();
            Restore = new RestoreState.Restoring();
            _store.Restore();
            return true;
        }

        /// <summary>The card that told what asking for the purchases back came to was closed.</summary>
        public bool DismissTheRestore()
        {
            if (!(Restore is RestoreState.Ended))
            {
                return false;
            }

            Restore = RestoreState.None;
            return true;
        }

        /// <summary>
        /// Takes what the store said since the last step: its prices, the payments it took (given through the keeper),
        /// and its answers to the course; then confirms to the store the payments the saved progress holds. A run whose
        /// store is closed takes nothing from the store, and one whose progress is not kept after closing confirms
        /// nothing.
        /// </summary>
        public void Step(IPurchaseKeeper keeper)
        {
            if (Status is StoreStatus.ClosedForThisRun)
            {
                return; // the store was never asked: whatever it holds waits for a run that can keep it
            }

            foreach (StoreNews news in _store.TakeNews())
            {
                Take(news, keeper);
            }

            bool holdsThePayments = keeper.IsSaved && keeper.Keeping == ProgressKeeping.AfterClosing;
            if (_toConfirm.Count > 0 && holdsThePayments)
            {
                foreach (StorePayment payment in _toConfirm)
                {
                    _store.Confirm(payment);
                }

                _toConfirm.Clear();
            }
        }

        /// <summary>The store as the shop shows it to a player who bought this much and stands on this level (counted from 1).</summary>
        public StoreView ViewFor(PurchasesMade made, int levelNumber)
        {
            StoreStatus.Open? open = Status as StoreStatus.Open;
            ProductOffer[] packs = _listings.Where(listing => listing.Product.Kind == StoreProductKind.CoinPack)
                .Select(listing => new ProductOffer(listing.Product, open?.PriceOf(listing))).ToArray();
            StarterPack starter = _catalogue.StarterPack;
            StorePrice? starterPrice = open?.PriceOf(ListingOf(starter.Product));
            bool offersTheStarter = starterPrice != null && starter.IsOfferedTo(levelNumber, made.TimesBought(starter.Product.IdSuffix));
            return new StoreView(
                Status, packs, starter.Product, offersTheStarter ? new ProductOffer(starter.Product, starterPrice) : null, OffersRestore, State, Restore);
        }

        /// <summary>Asks the store to open and tell its prices and the payments it holds; what the shop shows meanwhile is the status the store answered last.</summary>
        private void AskTheStore(TimeSpan now)
        {
            _askedAt = now;
            _store.Open(_listings);
        }

        private bool IsTimeToAskAgain(TimeSpan now) => _askedAt is TimeSpan asked && now - asked >= PatienceBeforeAskingAgain;

        private void Take(StoreNews news, IPurchaseKeeper keeper)
        {
            switch (news)
            {
                case StoreNews.Opened opened:
                    StorePrice[] known = opened.Prices.Where(price => _listings.Any(listing => listing.StoreId == price.StoreId)).ToArray();
                    Status = known.Length > 0 ? new StoreStatus.Open(known) : (StoreStatus)new StoreStatus.Unreachable();
                    ConfirmAgainWhatTheStoreDidNotTake();
                    break;
                case StoreNews.Unreachable _:
                    Status = new StoreStatus.Unreachable();
                    if (State is PurchaseState.Pending pending)
                    {
                        // The store may still take the payment it was lost in the middle of: nothing says it did not.
                        State = new PurchaseState.Unanswered(pending.Bought);
                    }

                    break;
                case StoreNews.Paid paid:
                    Give(paid.Payment, keeper, DeliveryReason.Payment);
                    break;
                case StoreNews.Owned owned:
                    Give(owned.Payment, keeper, DeliveryReason.Restore);
                    break;
                case StoreNews.NotConfirmed notConfirmed:
                    Queue(notConfirmed.Payment, _toConfirmAgain);
                    break;
                case StoreNews.Cancelled cancelled when IsTheSheetsAnswer(cancelled.StoreId):
                    State = new PurchaseState.Cancelled(State.Product!);
                    break;
                case StoreNews.Failed failed when IsTheSheetsAnswer(failed.StoreId):
                    State = new PurchaseState.Failed(State.Product!, FailureOf(failed.Why));
                    break;
                case StoreNews.Deferred deferred when IsAwaitingTheSheetFor(deferred.StoreId):
                    State = new PurchaseState.Deferred(State.Product!);
                    break;
                case StoreNews.RestoreEnded ended when Restore is RestoreState.Restoring:
                    Restore = new RestoreState.Ended(_restored.ToArray(), ended.Succeeded);
                    break;
                case StoreNews.Cancelled _:
                case StoreNews.Failed _:
                case StoreNews.Deferred _:
                case StoreNews.RestoreEnded _:
                    break; // an answer to no course of this game's: a purchase begun before it was closed, or a restore it did not ask for
                default:
                    throw new InvalidOperationException($"The store said something the course does not know: {news}.");
            }
        }

        /// <summary>
        /// A product, given through the keeper once; one for a product the game does not sell is left alone and never
        /// confirmed, so the store gives the money back. A payment the store will tell of again until it is confirmed
        /// waits for the save; a product the store gives back needs no confirmation.
        /// </summary>
        private void Give(StorePayment payment, IPurchaseKeeper keeper, DeliveryReason reason)
        {
            if (!(_listings.FirstOrDefault(listing => listing.StoreId == payment.StoreId) is StoreListing listing))
            {
                return;
            }

            Delivery delivery = keeper.Deliver(listing.Product, payment, reason);
            if (reason == DeliveryReason.Payment)
            {
                Queue(payment, _toConfirm);
            }

            switch (delivery)
            {
                case Delivery.Given _ when Restore is RestoreState.Restoring:
                    _restored.Add(listing.Product); // the restore's card says what came back
                    break;
                case Delivery.Given given:
                    ShowDelivered(new PurchaseState.Delivered(listing.Product, given.What, reason));
                    break;
                case Delivery.SoldOut _ when IsAwaitingTheSheetFor(payment.StoreId):
                    State = new PurchaseState.Failed(listing.Product, PurchaseFailure.SoldOut);
                    break;
                case Delivery.SoldOut _:
                case Delivery.AlreadyGiven _:
                    break;
                default:
                    throw new InvalidOperationException($"A delivery ended in a way the course does not know: {delivery}.");
            }
        }

        /// <summary>A payment to confirm, once: the store tells of a payment again when it opens anew, and a confirmation sent again finds it there already.</summary>
        private static void Queue(StorePayment payment, List<StorePayment> payments)
        {
            if (!payments.Any(queued => queued.TransactionId == payment.TransactionId))
            {
                payments.Add(payment);
            }
        }

        /// <summary>The store opened anew: the confirmations it did not take go to it again, once the saved progress holds them (as it held them before).</summary>
        private void ConfirmAgainWhatTheStoreDidNotTake()
        {
            foreach (StorePayment payment in _toConfirmAgain)
            {
                Queue(payment, _toConfirm);
            }

            _toConfirmAgain.Clear();
        }

        /// <summary>A payment given is shown now when the course waited for it or for nothing, and after the card in front otherwise.</summary>
        private void ShowDelivered(PurchaseState.Delivered delivered)
        {
            bool waitedForIt = (State is PurchaseState.Pending || State is PurchaseState.Unanswered || State is PurchaseState.Deferred) && State.Product == delivered.Bought;
            if (State is PurchaseState.Nothing || waitedForIt)
            {
                State = delivered;
                return;
            }

            if (_deliveredMeanwhile.Count == DeliveredCardsKept)
            {
                _deliveredMeanwhile.Dequeue();
            }

            _deliveredMeanwhile.Enqueue(delivered);
        }

        private PurchaseState DeliveredMeanwhileOrIdle() => _deliveredMeanwhile.Count > 0 ? _deliveredMeanwhile.Dequeue() : PurchaseState.Idle;

        /// <summary>
        /// True while the store's sheet is up for the product with this id, or the game still waits for its answer; a
        /// store's answer that names no product is taken as the sheet's.
        /// </summary>
        private bool IsAwaitingTheSheetFor(string storeId) =>
            State.Product is StoreProduct awaited && (State is PurchaseState.Pending || State is PurchaseState.Unanswered)
            && (storeId.Length == 0 || ListingOf(awaited).StoreId == storeId);

        /// <summary>
        /// True when a cancel or a refusal answers the sheet that is up: one for its product, or one that names a product
        /// the game does not sell (none, or an id the store mangled). An answer for another of the game's products is a
        /// purchase begun before the game was closed, and is not the sheet's.
        /// </summary>
        private bool IsTheSheetsAnswer(string storeId) =>
            IsAwaitingTheSheetFor(storeId)
            || ((State is PurchaseState.Pending || State is PurchaseState.Unanswered) && !_listings.Any(listing => listing.StoreId == storeId));

        private StoreListing ListingOf(StoreProduct product) =>
            _listings.FirstOrDefault(listing => listing.Product == product) ?? throw new ArgumentException($"The store does not sell the {product}.", nameof(product));

        private static PurchaseFailure FailureOf(StoreRefusal why) => why switch
        {
            StoreRefusal.NotPaid => PurchaseFailure.NotPaid,
            StoreRefusal.AlreadyWaiting => PurchaseFailure.AlreadyWaiting,
            StoreRefusal.NotForSale => PurchaseFailure.NotOnSale,
            _ => throw new ArgumentOutOfRangeException(nameof(why), why, "The store refused in a way the course does not know."),
        };
    }
}
