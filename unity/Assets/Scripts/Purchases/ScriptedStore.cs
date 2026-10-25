#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Puzzle.Meta;

namespace Puzzle.Client.Purchases
{
    /// <summary>How the scripted store answers the next payment sheet. A closed set.</summary>
    public enum ScriptedAnswer
    {
        /// <summary>The player pays.</summary>
        Pay,

        /// <summary>The player cancels the sheet.</summary>
        Cancel,

        /// <summary>The store does not take the payment (a card declined).</summary>
        Fail,

        /// <summary>The payment waits for a parent's approval (Ask to Buy) or for cash at a shop; <see cref="ScriptedStore.MakeTheDeferredPayment"/> makes it.</summary>
        Defer,

        /// <summary>
        /// The store says nothing of the sheet: its answer is lost, or it is slow beyond the game's patience.
        /// <see cref="ScriptedStore.MakeTheDeferredPayment"/> makes the payment later, as a store that took it after all.
        /// </summary>
        Silence,
    }

    /// <summary>
    /// A store the tests and the Editor's tools script, never the game's: it opens with the prices it is given, or
    /// is unreachable; answers each payment sheet as <see cref="NextAnswer"/> says; keeps every payment the game has
    /// not confirmed and tells of it again when it is opened anew, as Google Play and the App Store do for a game that
    /// was closed between a payment and its confirmation; may lose its connection for a while
    /// (<see cref="IsDisconnected"/>); keeps the products it keeps (the starter pack) as the
    /// player's, refuses to sell them again, tells of them when it opens and gives them back on a restore. Opened
    /// anew, it forgets what it said and the game did not take: that game was closed. No Unity dependency.
    /// </summary>
    public sealed class ScriptedStore : IStore
    {
        private readonly List<StoreNews> _news = new List<StoreNews>();
        private readonly List<StorePayment> _unconfirmed = new List<StorePayment>();
        private readonly List<StorePayment> _confirmed = new List<StorePayment>();
        private readonly List<StorePayment> _owned = new List<StorePayment>();
        private readonly HashSet<string> _keptIds = new HashSet<string>(StringComparer.Ordinal);
        private IReadOnlyList<StorePrice>? _prices;
        private StoreListing? _deferred;
        private int _payments;

        /// <param name="prices">The prices it opens with; nothing for a store that cannot be reached.</param>
        /// <param name="offersRestore">True to stand for the App Store, whose players ask for their purchases back by a button.</param>
        public ScriptedStore(IReadOnlyList<StorePrice>? prices, bool offersRestore)
        {
            _prices = prices?.ToArray();
            OffersRestore = offersRestore;
        }

        /// <summary>A store that sells every product of the catalogue at its US price, worded as the US store words it ("$1.99").</summary>
        public static ScriptedStore AtUsPrices(StoreCatalogue catalogue, string applicationId, bool offersRestore) =>
            new ScriptedStore(UsPricesOf(catalogue, applicationId), offersRestore);

        /// <summary>Every product of the catalogue at its US price, as the US store words it.</summary>
        public static IReadOnlyList<StorePrice> UsPricesOf(StoreCatalogue catalogue, string applicationId) =>
            StoreListing.AllOf(catalogue, applicationId)
                .Select(listing => new StorePrice(listing.StoreId, listing.Product.UsPrice.ToString(), listing.Product.UsPrice.Cents / 100m, "USD"))
                .ToArray();

        public bool OffersRestore { get; }

        /// <summary>How the next payment sheet is answered.</summary>
        public ScriptedAnswer NextAnswer { get; set; } = ScriptedAnswer.Pay;

        /// <summary>
        /// True (as Google Play does) when opening tells of what the player owns; false for a store that gives it back
        /// only when asked (the App Store on a new device before its restore).
        /// </summary>
        public bool TellsWhatIsOwnedOnOpening { get; set; } = true;

        /// <summary>
        /// True while the store's connection is lost: a confirmation is not taken (the store says it is unreachable and
        /// answers <see cref="StoreNews.NotConfirmed"/>, as UnityStore tells Google Play's "not connected") and the
        /// payment goes on waiting; opening it finds it unreachable.
        /// </summary>
        public bool IsDisconnected { get; set; }

        /// <summary>How many times the game asked the store to open.</summary>
        public int TimesOpened { get; private set; }

        /// <summary>How many payment sheets were opened.</summary>
        public int SheetsOpened { get; private set; }

        /// <summary>Payments taken that the game has not confirmed: the store tells of them again when it opens.</summary>
        public IReadOnlyList<StorePayment> Unconfirmed => _unconfirmed;

        /// <summary>Payments the game confirmed, in the order it confirmed them.</summary>
        public IReadOnlyList<StorePayment> Confirmed => _confirmed;

        /// <summary>From now on the store opens with these prices, or cannot be reached.</summary>
        public void OpensWith(IReadOnlyList<StorePrice>? prices) => _prices = prices?.ToArray();

        public void Open(IReadOnlyList<StoreListing> listings)
        {
            TimesOpened++;
            _news.Clear();
            foreach (StoreListing listing in listings.Where(listing => listing.IsKeptByTheStore))
            {
                _keptIds.Add(listing.StoreId);
            }

            if (_prices == null || IsDisconnected)
            {
                _news.Add(new StoreNews.Unreachable("the scripted store cannot be reached"));
                return;
            }

            _news.Add(new StoreNews.Opened(_prices));
            if (TellsWhatIsOwnedOnOpening)
            {
                _news.AddRange(_owned.Where(payment => !_unconfirmed.Contains(payment)).Select(payment => new StoreNews.Owned(payment)));
            }

            _news.AddRange(_unconfirmed.Select(payment => new StoreNews.Paid(payment)));
        }

        public void Buy(StoreListing listing)
        {
            SheetsOpened++;
            if (listing.IsKeptByTheStore && _owned.Any(payment => payment.StoreId == listing.StoreId))
            {
                _news.Add(new StoreNews.Failed(listing.StoreId, StoreRefusal.NotForSale, "the player owns it already"));
                return;
            }

            switch (NextAnswer)
            {
                case ScriptedAnswer.Pay:
                    TakeThePayment(listing.StoreId);
                    break;
                case ScriptedAnswer.Cancel:
                    _news.Add(new StoreNews.Cancelled(listing.StoreId));
                    break;
                case ScriptedAnswer.Fail:
                    _news.Add(new StoreNews.Failed(listing.StoreId, StoreRefusal.NotPaid, "the scripted card was declined"));
                    break;
                case ScriptedAnswer.Defer:
                    _deferred = listing;
                    _news.Add(new StoreNews.Deferred(listing.StoreId));
                    break;
                case ScriptedAnswer.Silence:
                    _deferred = listing;
                    break;
                default:
                    throw new InvalidOperationException($"The scripted store has no answer {NextAnswer}.");
            }
        }

        /// <summary>The payment that waited is made (the parent approved, the cash was paid, the silent store took it): the store tells of it.</summary>
        /// <exception cref="InvalidOperationException">No payment waits.</exception>
        public void MakeTheDeferredPayment()
        {
            StoreListing waiting = _deferred ?? throw new InvalidOperationException("No payment waits.");
            _deferred = null;
            TakeThePayment(waiting.StoreId);
        }

        public void Confirm(StorePayment payment)
        {
            if (!_unconfirmed.Contains(payment))
            {
                throw new InvalidOperationException($"The game confirmed the {payment}, which the store does not hold as waiting.");
            }

            if (IsDisconnected)
            {
                _news.Add(new StoreNews.Unreachable("the scripted store lost its connection"));
                _news.Add(new StoreNews.NotConfirmed(payment, "the scripted store is not connected"));
                return;
            }

            _unconfirmed.Remove(payment);
            _confirmed.Add(payment);
        }

        public void Restore()
        {
            _news.AddRange(_owned.Where(payment => !_unconfirmed.Contains(payment)).Select(payment => new StoreNews.Owned(payment)));
            _news.Add(new StoreNews.RestoreEnded(succeeded: true));
        }

        /// <summary>The store says something of its own accord, as a test has it: an answer to a sheet the game did not open, a lost connection.</summary>
        public void Say(StoreNews news) => _news.Add(news ?? throw new ArgumentNullException(nameof(news)));

        public IReadOnlyList<StoreNews> TakeNews()
        {
            StoreNews[] taken = _news.ToArray();
            _news.Clear();
            return taken;
        }

        private void TakeThePayment(string storeId)
        {
            _payments++;
            var payment = new StorePayment(storeId, string.Format(CultureInfo.InvariantCulture, "SCRIPTED.{0:0000}", _payments));
            _unconfirmed.Add(payment);
            if (_keptIds.Contains(storeId))
            {
                _owned.Add(payment);
            }

            _news.Add(new StoreNews.Paid(payment));
        }
    }
}
