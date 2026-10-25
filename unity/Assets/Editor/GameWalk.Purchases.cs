#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Meta;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walks of purchases for real money, once on each screen, on the game's own session over a scripted store and
    /// by touches alone (<c>walk-purchase-...</c>): the shop with the store's prices and the starter pack offered in the
    /// first pack's place; the starter pack asked about, paid by the card's green button and given, its card saying
    /// so, the progress saved with it and the payment confirmed to the store, and the first pack back in its place;
    /// every pack bought the same way; a payment the player cancels on the store's sheet (no card, nothing given), one
    /// the store refuses (its card), and one that waits for approval (its card) and is given when it is made; and a
    /// second starter pack refused before the store. A game closed between a payment and its delivery gives it once when it opens again, and one closed
    /// between the save and the confirmation finds it given and confirms it (<c>walk-purchase-restart-...</c>). On the
    /// App Store a new install asks for its purchases back by the shop's row and gets the starter pack once, a second
    /// asking bringing nothing (<c>walk-purchase-restore-...</c>); on Google Play a new install is given the starter
    /// pack back when the store opens, and its card says it came back, not that it was bought
    /// (<c>walk-purchase-givenback-...</c>). A store that cannot be reached leaves the calm line in place of the packs'
    /// buttons, a store that has not answered dims them, and the shop asks again a while later
    /// (<c>walk-purchase-store-...</c>). A payment sheet the store does not answer keeps its card without an answer for
    /// half a minute, then lets it close, and the payment made later is given; a store lost while its sheet is up says
    /// no answer came, never that nothing was paid (<c>walk-purchase-silent-...</c>). A run whose progress cannot be
    /// kept sells nothing and leaves a payment of an earlier run with the store (<c>walk-purchase-unsaved-...</c>).
    /// Every walk fails when a touch does not lead where it should, when a payment is not in the saved progress exactly
    /// once, or when one is confirmed before the save holds it.
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>The level the purchase walks stand on: past the starter pack's, so the shop offers it.</summary>
        private const int PurchaseLevel = 25;

        /// <summary>Walks every purchase on a screen; a delivery the save does not hold fails them.</summary>
        /// <returns>How many pictures were taken.</returns>
        public static int RunThePurchaseWalks(ScreenShape screen, Studio studio) => RunThePurchaseWalks(screen, studio, Saving.AsTheGameSavesIt);

        /// <summary>Walks every purchase on a screen with the saves made as the walk says: as the game makes them, or broken, to prove the walk fails then.</summary>
        /// <returns>How many pictures were taken.</returns>
        public static int RunThePurchaseWalks(ScreenShape screen, Studio studio, Saving saving)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, PurchaseLevel), studio.Levels);
            return WalkThePurchases(standing, screen, studio)
                + WalkAPurchaseCutShort(standing, screen, studio, saving)
                + WalkARestore(standing, screen, studio)
                + WalkAGivingBack(standing, screen, studio)
                + WalkAStoreThatIsNotThere(standing, screen, studio)
                + WalkASilentSheet(standing, screen, studio)
                + WalkARunThatCannotSave(standing, screen, studio);
        }

        /// <summary>How the walks' progress is saved. A closed set.</summary>
        public enum Saving
        {
            /// <summary>As the game saves it: what is saved is what loads.</summary>
            AsTheGameSavesIt,

            /// <summary>A save that says it held and kept nothing: a broken delivery, which the walks must catch.</summary>
            SavedNowhere,
        }

        /// <summary>The shop's prices, the starter pack bought, every pack, a cancel, a refusal, a payment that waits, the starter pack again.</summary>
        private static int WalkThePurchases(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            EconomyRules rules = StoryStudio.Economy;
            StoreCatalogue catalogue = rules.Shop.Store;
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: false);
            var kept = new ProgressInMemory(before);
            using GameSession session = SessionOverAStore(studio, kept, store, screen);
            var reel = new Reel(session, screen, $"walk-purchase-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
            reel.Shoot("shop-with-prices");
            ThroughTheCard.Require(
                Enumerable.Range(1, catalogue.CoinPacks.Count - 1).All(MuseumOf(session).IsShopPackForSale) && MuseumOf(session).ShopStarterPixel != null
                && MuseumOf(session).ShopPackPixel(0) == null,
                "the shop does not sell the packs at the store's price, or does not offer the starter pack in the first pack's place on its level");

            // The starter pack: asked about, paid by the green button, given, saved and confirmed; the first pack is back in its place.
            Buy(reel, store, kept, MuseumOf(session).ShopStarterPixel, catalogue.StarterPack.Product, "starter");
            ThroughTheCard.Require(
                MuseumOf(session).ShopStarterPixel == null && MuseumOf(session).IsShopPackForSale(0), "the starter pack is offered again after it was bought, or the first pack is not back");
            reel.Shoot("shop-after-the-starter");

            // Every pack, the first pictured.
            Buy(reel, store, kept, MuseumOf(session).ShopPackPixel(0), catalogue.CoinPacks[0], "pack");
            for (int pack = 1; pack < catalogue.CoinPacks.Count; pack++)
            {
                Buy(reel, store, kept, MuseumOf(session).ShopPackPixel(pack), catalogue.CoinPacks[pack], shots: null);
            }

            // Cancelled on the store's own sheet: no card, nothing given, nothing confirmed.
            PlayerProgress beforeTheCancel = session.Progress;
            int confirmed = store.Confirmed.Count;
            store.NextAnswer = ScriptedAnswer.Cancel;
            AskAndPay(reel, MuseumOf(session).ShopPackPixel(0), catalogue.CoinPacks[0]);
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == null && session.Progress.Equals(beforeTheCancel) && store.Confirmed.Count == confirmed && session.Checkout.State is PurchaseState.Nothing,
                "a payment cancelled on the store's sheet brought a card, gave something or was confirmed");

            // Refused by the store: its card, nothing given.
            store.NextAnswer = ScriptedAnswer.Fail;
            AskAndPay(reel, MuseumOf(session).ShopPackPixel(0), catalogue.CoinPacks[0]);
            reel.Shoot("pack-refused");
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == PurchaseWords.Of(new PurchaseState.Failed(catalogue.CoinPacks[0], PurchaseFailure.NotPaid))!.Title && session.Progress.Equals(beforeTheCancel),
                "a payment the store refused was not answered with its card, or gave something");
            CloseTheNotice(reel);

            // Waiting for approval: its card, nothing given; given when the payment is made, with its card.
            store.NextAnswer = ScriptedAnswer.Defer;
            AskAndPay(reel, MuseumOf(session).ShopPackPixel(1), catalogue.CoinPacks[1]);
            reel.Shoot("pack-waits");
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == PurchaseWords.Of(new PurchaseState.Deferred(catalogue.CoinPacks[1]))!.Title && session.Progress.Equals(beforeTheCancel),
                "a payment that waits for approval was not answered with its card, or gave something before it was made");
            CloseTheNotice(reel);
            store.MakeTheDeferredPayment();
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Settle();
            reel.Shoot("pack-approved");
            RequireGivenOnce(session, kept, store, catalogue.CoinPacks[1], beforeTheCancel, "the approved payment");
            CloseTheNotice(reel);

            // A second starter pack, asked for as a press that crossed the shop's change would: refused before the store's sheet.
            store.NextAnswer = ScriptedAnswer.Pay;
            int sheets = store.SheetsOpened;
            PlayerProgress afterTheStarter = session.Progress;
            session.Checkout.Ask(catalogue.StarterPack.Product, session.Progress.Purchases, PurchaseLevel);
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Settle();
            reel.Shoot("starter-again");
            ThroughTheCard.Require(
                session.Checkout.State is PurchaseState.Failed { Why: PurchaseFailure.SoldOut } && store.SheetsOpened == sheets && session.Progress.Equals(afterTheStarter),
                "a second starter pack was not refused before the store's sheet");
            CloseTheNotice(reel);
            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: every pack and the starter pack bought once, a cancel, a refusal and a payment that waited; the player now holds \"{session.Progress.Wallet}\".");
            return reel.Taken;
        }

        /// <summary>
        /// A game closed between a payment and its delivery, and one closed between the save and the confirmation: the
        /// store tells of the payment again when the game opens, and it is given once, and confirmed.
        /// </summary>
        private static int WalkAPurchaseCutShort(PlayerProgress before, ScreenShape screen, Studio studio, Saving saving)
        {
            int pictures;
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            StoreProduct chest = catalogue.CoinPacks[catalogue.CoinPacks.Count - 2];
            StoreProduct treasure = catalogue.CoinPacks[catalogue.CoinPacks.Count - 1];
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: false);
            IProgressStore kept = saving == Saving.SavedNowhere ? new SavedNowhere(before) : (IProgressStore)new ProgressInMemory(before);

            // Paid, and the game closed before it took the store's news: nothing given, nothing saved.
            using (GameSession closed = SessionOverAStore(studio, kept, store, screen))
            {
                store.Buy(new StoreListing(chest, Application.identifier));
            }

            using (GameSession reopened = SessionOverAStore(studio, kept, store, screen))
            {
                var reel = new Reel(reopened, screen, $"walk-purchase-restart-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Advance(StoryStudio.FrameSeconds);
                reel.Settle();
                reel.Shoot("given-on-opening");
                RequireGivenOnce(reopened, kept, store, chest, before, "the payment the closed game had not taken");
                CloseTheNotice(reel);
                reel.WriteSheet();
                pictures = reel.Taken;
            }

            // Given and saved, and closed before the confirmation: found given on opening, confirmed then, given no second time.
            PlayerProgress beforeTheTreasure = kept.Load();
            var closingStore = new ClosesBeforeConfirming(store);
            using (GameSession closing = SessionOverAStore(studio, kept, closingStore, screen))
            {
                closing.Advance(StoryStudio.FrameSeconds);
                ThroughTheCard.Require(closing.Checkout.Ask(treasure, closing.Progress.Purchases, PurchaseLevel) && closing.Checkout.Pay(TimeSpan.Zero), "the treasure could not be bought");
                closing.Advance(StoryStudio.FrameSeconds);
                ThroughTheCard.Require(store.Unconfirmed.Count == 1, "the payment of a game closed before its confirmation is not waiting in the store");
            }

            using (GameSession reopened = SessionOverAStore(studio, kept, store, screen))
            {
                StoryStudio.AdvanceBy(StoryStudio.FrameSeconds, reopened);
                ThroughTheCard.Require(
                    store.Unconfirmed.Count == 0 && kept.Load().Wallet.Coins == beforeTheTreasure.Wallet.Coins + treasure.Coins && reopened.Checkout.State is PurchaseState.Nothing,
                    $"the payment found given on opening was given again or not confirmed: the saved progress holds {kept.Load().Wallet.Coins} coins, {store.Unconfirmed.Count} payments wait");
            }

            Debug.Log($"Purchases: a payment cut short before its delivery was given once on opening, and one cut short before its confirmation was confirmed on opening and not given again.");
            return pictures;
        }

        /// <summary>A new install on the App Store asks for its purchases back: the starter pack comes back once, a coin pack never.</summary>
        private static int WalkARestore(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            StoreProduct starter = catalogue.StarterPack.Product;
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: true);
            store.TellsWhatIsOwnedOnOpening = false;
            using (GameSession firstPhone = SessionOverAStore(studio, new ProgressInMemory(before), store, screen))
            {
                firstPhone.Advance(StoryStudio.FrameSeconds);
                foreach (StoreProduct bought in new[] { starter, catalogue.CoinPacks[0] })
                {
                    ThroughTheCard.Require(
                        firstPhone.Checkout.Ask(bought, firstPhone.Progress.Purchases, PurchaseLevel) && firstPhone.Checkout.Pay(TimeSpan.Zero), $"the first phone could not buy {bought.Name}");
                    firstPhone.Advance(StoryStudio.FrameSeconds);
                    firstPhone.Checkout.Dismiss();
                }
            }

            var newInstall = new ProgressInMemory(before);
            using GameSession session = SessionOverAStore(studio, newInstall, store, screen);
            var reel = new Reel(session, screen, $"walk-purchase-restore-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
            reel.Shoot("shop-with-the-restore-row");
            Press(session, MuseumOf(session).ShopRestorePixel, "the shop's row that asks for the purchases back", reel, pressedShot: null);
            reel.Settle();
            reel.Shoot("restored");
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == PurchaseWords.Of(new RestoreState.Ended(Array.Empty<StoreProduct>(), succeeded: true), starter)!.Title
                && newInstall.Load().Wallet.Coins == before.Wallet.Coins + starter.Coins && newInstall.Load().Purchases.TimesBought(starter.IdSuffix) == 1
                && newInstall.Load().Purchases.TimesBought(catalogue.CoinPacks[0].IdSuffix) == 0,
                $"asking for the purchases back did not bring the starter pack back alone: the saved progress holds {newInstall.Load().Wallet.Coins} coins and {newInstall.Load().Purchases}");
            CloseTheNotice(reel);

            Press(session, MuseumOf(session).ShopRestorePixel, "the restore row once more", reel, pressedShot: null);
            reel.Settle();
            reel.Shoot("nothing-more");
            ThroughTheCard.Require(newInstall.Load().Wallet.Coins == before.Wallet.Coins + starter.Coins, "asking again brought the starter pack a second time");
            CloseTheNotice(reel);
            reel.WriteSheet();
            return reel.Taken;
        }

        /// <summary>
        /// A new install on Google Play, which gives the starter pack back by itself when the store opens: it is given
        /// once, and its card says it came back (the restore's words), not that it was bought.
        /// </summary>
        private static int WalkAGivingBack(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            StoreProduct starter = StoryStudio.Economy.Shop.Store.StarterPack.Product;
            var store = ScriptedStore.AtUsPrices(StoryStudio.Economy.Shop.Store, Application.identifier, offersRestore: false);
            using (GameSession firstPhone = SessionOverAStore(studio, new ProgressInMemory(before), store, screen))
            {
                firstPhone.Advance(StoryStudio.FrameSeconds);
                ThroughTheCard.Require(
                    firstPhone.Checkout.Ask(starter, firstPhone.Progress.Purchases, PurchaseLevel) && firstPhone.Checkout.Pay(TimeSpan.Zero), "the first phone could not buy the starter pack");
                firstPhone.Advance(StoryStudio.FrameSeconds);
            }

            var newInstall = new ProgressInMemory(before);
            using GameSession session = SessionOverAStore(studio, newInstall, store, screen);
            var reel = new Reel(session, screen, $"walk-purchase-givenback-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Settle();
            reel.Shoot("given-back");
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == PurchaseWords.Of(new PurchaseState.Delivered(starter, starter.Contents, DeliveryReason.Restore))!.Title
                && MuseumOf(session).NoticeShown != ShopWords.BoughtTitle,
                $"the starter pack the store gave back brought the card \"{MuseumOf(session).NoticeShown}\", not the one that says it came back");
            ThroughTheCard.Require(
                newInstall.Load().Purchases.TimesBought(starter.IdSuffix) == 1 && newInstall.Load().Wallet.Coins == before.Wallet.Coins + starter.Coins,
                $"the new install holds {newInstall.Load().Purchases} and {newInstall.Load().Wallet.Coins} coins, not the starter pack once");
            CloseTheNotice(reel);
            reel.WriteSheet();
            return reel.Taken;
        }

        /// <summary>A store that cannot be reached, and one that has not answered: the shop says so calmly and sells nothing; it asks again a while later.</summary>
        private static int WalkAStoreThatIsNotThere(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            int pictures = 0;
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            var unreachable = new ScriptedStore(prices: null, offersRestore: false);
            using (GameSession session = SessionOverAStore(studio, new ProgressInMemory(before), unreachable, screen))
            {
                var reel = new Reel(session, screen, $"walk-purchase-store-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Settle();
                TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
                reel.Shoot("unreachable");
                ThroughTheCard.Require(
                    MuseumOf(session).ShopPackPixel(0) == null && MuseumOf(session).ShopStarterPixel == null && session.Checkout.Status is StoreStatus.Unreachable
                    && MuseumOf(session).ShopStoreLine == PurchaseWords.StoreUnreachable,
                    "a store that cannot be reached left the packs' buttons or the starter pack in the shop, or did not say so");

                unreachable.OpensWith(ScriptedStore.UsPricesOf(catalogue, Application.identifier));
                reel.Advance((float)Checkout.PatienceBeforeAskingAgain.TotalSeconds + 1f);
                reel.Settle();
                reel.Shoot("open-again");
                ThroughTheCard.Require(unreachable.TimesOpened == 2 && MuseumOf(session).IsShopPackForSale(1), $"the shop did not ask the store again a while later: it asked {unreachable.TimesOpened} times and stands at {session.Checkout.Status}");
                reel.WriteSheet();
                pictures += reel.Taken;
            }

            using (GameSession waiting = SessionOverAStore(studio, new ProgressInMemory(before), new StoreThatNeverAnswers(), screen))
            {
                var reel = new Reel(waiting, screen, $"walk-purchase-connecting-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Settle();
                TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
                reel.Shoot("connecting");
                ThroughTheCard.Require(!MuseumOf(waiting).IsShopPackForSale(0) && MuseumOf(waiting).ShopPackPixel(0) != null, "a store that has not answered left a pack for sale, or its button away");
                reel.WriteSheet();
                pictures += reel.Taken;
            }

            return pictures;
        }

        /// <summary>
        /// A payment sheet the store does not answer: its card stands without an answer for half a minute, then says no
        /// answer came and can be closed; the payment the store makes after that is given, with its card. A store lost
        /// while its sheet is up brings that card at once, whose lines say the payment comes once made, never that
        /// nothing was paid.
        /// </summary>
        private static int WalkASilentSheet(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            StoreProduct bag = catalogue.CoinPacks[1]; // the pouch's place holds the starter pack on this level
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: false);
            store.NextAnswer = ScriptedAnswer.Silence;
            var kept = new ProgressInMemory(before);
            using GameSession session = SessionOverAStore(studio, kept, store, screen);
            var reel = new Reel(session, screen, $"walk-purchase-silent-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
            PlayerProgress beforeTheBag = session.Progress;
            AskAndPay(reel, MuseumOf(session).ShopPackPixel(1), bag);
            reel.Shoot("pending");
            string pendingTitle = PurchaseWords.Of(new PurchaseState.Pending(bag))!.Title;
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == pendingTitle && MuseumOf(session).NoticeDonePixel == null && MuseumOf(session).NoticeClosePixel == null,
                $"the card of a payment the store is taking is \"{MuseumOf(session).NoticeShown}\", or can be closed before the store answers");

            reel.Advance((float)Checkout.PatienceWithTheSheet.TotalSeconds + 1f);
            reel.Settle();
            reel.Shoot("unanswered");
            ThroughTheCard.Require(
                session.Checkout.State is PurchaseState.Unanswered && MuseumOf(session).NoticeShown == pendingTitle && MuseumOf(session).NoticeDonePixel != null,
                $"half a minute of a silent store left the course at {session.Checkout.State}, with no way to close its card");
            CloseTheNotice(reel);
            ThroughTheCard.Require(session.Checkout.State is PurchaseState.Nothing && session.Progress.Equals(beforeTheBag), "closing the unanswered card left the course busy or gave something");

            store.MakeTheDeferredPayment();
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Settle();
            reel.Shoot("paid-later");
            RequireGivenOnce(session, kept, store, bag, beforeTheBag, "the payment the silent store made after its card was closed");
            CloseTheNotice(reel);

            // The store is lost while its sheet is up.
            PlayerProgress beforeTheLoss = session.Progress;
            AskAndPay(reel, MuseumOf(session).ShopPackPixel(1), bag);
            store.Say(new StoreNews.Unreachable("the walk's connection dropped"));
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Settle();
            reel.Shoot("store-lost");
            ThroughTheCard.Require(
                session.Checkout.State is PurchaseState.Unanswered && MuseumOf(session).NoticeShown == pendingTitle && MuseumOf(session).NoticeDonePixel != null
                && session.Progress.Equals(beforeTheLoss),
                $"a store lost while its sheet was up left the course at {session.Checkout.State} with the card \"{MuseumOf(session).NoticeShown}\"");
            CloseTheNotice(reel);
            reel.Shoot("shop-after-the-loss");
            reel.WriteSheet();
            return reel.Taken;
        }

        /// <summary>
        /// A run whose progress file could not be read keeps its progress for itself only: its shop says the store is
        /// closed, in the packs' buttons' place, and never asks the store, so a payment of an earlier run waits there for a
        /// run that can save it.
        /// </summary>
        private static int WalkARunThatCannotSave(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: false);
            store.Buy(new StoreListing(catalogue.CoinPacks[1], Application.identifier)); // an earlier run's payment, never taken
            var forThisRunOnly = ProgressInMemory.ForThisRunOnly(before);
            using GameSession session = SessionOverAStore(studio, forThisRunOnly, store, screen);
            var reel = new Reel(session, screen, $"walk-purchase-unsaved-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
            reel.Advance((float)Checkout.PatienceBeforeAskingAgain.TotalSeconds + 1f);
            reel.Settle();
            reel.Shoot("closed-for-this-run");
            ThroughTheCard.Require(
                session.Checkout.Status is StoreStatus.ClosedForThisRun && MuseumOf(session).ShopStoreLine == PurchaseWords.StoreClosedForThisRun
                && MuseumOf(session).ShopPackPixel(0) == null && MuseumOf(session).ShopStarterPixel == null && MuseumOf(session).ShopRestorePixel == null,
                $"a run that cannot keep its progress shows the store at {session.Checkout.Status}, with \"{MuseumOf(session).ShopStoreLine}\" and its buttons");
            ThroughTheCard.Require(
                store.TimesOpened == 0 && store.Unconfirmed.Count == 1 && store.Confirmed.Count == 0 && session.Progress.Wallet.Equals(before.Wallet),
                $"a run that cannot keep its progress asked the store {store.TimesOpened} times, or took the earlier run's payment ({store.Unconfirmed.Count} wait, {store.Confirmed.Count} confirmed)");
            reel.WriteSheet();
            return reel.Taken;
        }

        /// <summary>A product asked about, paid by the card's green button, given once and confirmed once saved; pictured where a name is given.</summary>
        private static void Buy(Reel reel, ScriptedStore store, IProgressStore kept, Vector2? pixel, StoreProduct product, string? shots)
        {
            GameSession session = reel.Session;
            PlayerProgress before = session.Progress;
            Press(session, pixel, $"the button of {product.Name}", reel, pressedShot: null);
            reel.Settle();
            if (shots != null)
            {
                reel.Shoot($"{shots}-asks");
            }

            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == PurchaseWords.NameOf(product) && MuseumOf(session).NoticeBuyPixel != null && session.Progress.Equals(before),
                $"the button of {product.Name} did not bring the card that asks before paying, or changed something");
            Press(session, MuseumOf(session).NoticeBuyPixel, "the card's Satın al", reel, pressedShot: null);
            reel.Settle();
            if (shots != null)
            {
                reel.Shoot($"{shots}-bought");
            }

            RequireGivenOnce(session, kept, store, product, before, $"{product.Name} bought");
            CloseTheNotice(reel);
        }

        /// <summary>A product asked about and paid by the card's green button, however the store then answers.</summary>
        private static void AskAndPay(Reel reel, Vector2? pixel, StoreProduct product)
        {
            GameSession session = reel.Session;
            Press(session, pixel, $"the button of {product.Name}", reel, pressedShot: null);
            reel.Settle();
            Press(session, MuseumOf(session).NoticeBuyPixel, "the card's Satın al", reel, pressedShot: null);
            reel.Settle();
        }

        /// <summary>The product's card says it was bought; its coins and boosters are in the saved progress once, with its payment; the payment is confirmed.</summary>
        private static void RequireGivenOnce(GameSession session, IProgressStore kept, ScriptedStore store, StoreProduct product, PlayerProgress before, string what)
        {
            PlayerProgress saved = kept.Load();
            Wallet expected = product.Contents.AddedTo(before.Wallet);
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == ShopWords.BoughtTitle,
                $"{what}: the card \"{MuseumOf(session).NoticeShown}\" came down, not the card that says it was bought");
            ThroughTheCard.Require(
                saved.Wallet.Coins == expected.Coins && saved.Wallet.Shelf.Equals(expected.Shelf)
                && saved.Purchases.TimesBought(product.IdSuffix) == before.Purchases.TimesBought(product.IdSuffix) + 1,
                $"{what}: the saved progress holds {saved.Wallet.Coins} coins and {saved.Purchases}, not the {product.Coins} coins of one more {product.Name}");
            ThroughTheCard.Require(
                store.Unconfirmed.Count == 0 && saved.Purchases.Transactions.Contains(store.Confirmed.Last().TransactionId),
                $"{what}: the payment is not confirmed, or was confirmed without the saved progress holding it");
        }

        /// <summary>The game's own session in the hall over a store, kept by the progress store given, laid out for a screen.</summary>
        private static GameSession SessionOverAStore(Studio studio, IProgressStore kept, IStore store, ScreenShape screen) =>
            new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, kept, StoryStudio.AttemptsKeptInMemory(),
                new SettingsInMemory(DeviceSettings.Default), DeviceAbilities.OfThisBuild, CuePlayer.Silent, store, screen, SessionOpening.InTheMuseum);

        /// <summary>A progress store whose save says it held and keeps nothing: a broken delivery, which the walks must catch.</summary>
        private sealed class SavedNowhere : IProgressStore
        {
            private readonly PlayerProgress _first;

            public SavedNowhere(PlayerProgress first)
            {
                _first = first;
            }

            /// <summary>It says it keeps what it is given, as the progress file does: that is its lie.</summary>
            public ProgressKeeping Keeping => ProgressKeeping.AfterClosing;

            public PlayerProgress Load() => _first;

            public void Save(PlayerProgress progress)
            {
            }
        }

        /// <summary>A store whose confirmations are lost: it stands for a game closed right after its save, before it confirmed.</summary>
        private sealed class ClosesBeforeConfirming : IStore
        {
            private readonly IStore _store;

            public ClosesBeforeConfirming(IStore store)
            {
                _store = store;
            }

            public bool OffersRestore => _store.OffersRestore;

            public void Open(IReadOnlyList<StoreListing> listings) => _store.Open(listings);

            public void Buy(StoreListing listing) => _store.Buy(listing);

            public void Confirm(StorePayment payment)
            {
                // The game is closed by now: the confirmation never reaches the store.
            }

            public void Restore() => _store.Restore();

            public IReadOnlyList<StoreNews> TakeNews() => _store.TakeNews();
        }

        /// <summary>A store that is asked and never answers: the shop waits on it with its buttons dimmed.</summary>
        private sealed class StoreThatNeverAnswers : IStore
        {
            public bool OffersRestore => false;

            public void Open(IReadOnlyList<StoreListing> listings)
            {
            }

            public void Buy(StoreListing listing)
            {
            }

            public void Confirm(StorePayment payment)
            {
            }

            public void Restore()
            {
            }

            public IReadOnlyList<StoreNews> TakeNews() => Array.Empty<StoreNews>();
        }
    }
}
