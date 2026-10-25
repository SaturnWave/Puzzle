#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Museum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks purchases for real money in two parts, and ends the Editor with its verdict on one line ("Purchase check:
    /// PASSED" or "FAILED"). First, in edit mode, the purchase walks (GameWalk.Purchases.cs) on the game's own
    /// session over a scripted store, on the phone: every pack bought and given once, a cancel, a refusal, a payment
    /// that waits and is then made, the starter pack bought and a second one refused, a game closed between a payment
    /// and its delivery and one closed between the save and the confirmation, each payment given once, a restore, and
    /// a store that cannot be reached or has not answered. Then, in play mode (Unity IAP registers its stores only
    /// while a game runs), the game's adapter over Unity In-App Purchasing 5.4.4 and Unity's fake store, without Unity
    /// Gaming Services: the store opens with a price for every product, each pack and the starter pack are bought
    /// through it, given once and confirmed once saved, a sheet cancelled gives nothing, a second starter pack is
    /// refused, a restore on the same progress brings nothing (IAP refuses the restore button in the Windows Editor,
    /// whose store is no App Store, and the check says which answer it got), and a new install gets the starter pack
    /// back once when the store opens (the fake store keeps it, as Google Play does). The fake store's own payment sheet (its "Buy" or "Cancel" window) is
    /// answered as a tester's tap would answer it; it has no refusal and no waiting payment, which are the walks'.
    /// <para>
    /// With <c>-purchaseCheckBreaksTheSave</c> the walks' progress is kept by a store whose save holds nothing (a
    /// broken delivery): the check must then fail, and does, by name.
    /// </para>
    /// usage: Unity.exe -batchmode -projectPath unity -executeMethod Puzzle.Client.EditorTools.PurchaseCheck.Run [-purchaseCheckBreaksTheSave]
    /// WITHOUT -quit: play mode and the store's answers come on later Editor updates; the check ends the Editor itself.
    /// </summary>
    [InitializeOnLoad]
    public static class PurchaseCheck
    {
        private const string BreaksTheSaveArgument = "-purchaseCheckBreaksTheSave";

        /// <summary>Entering play mode loads the scripts anew and forgets every static field; the Editor's session state survives it.</summary>
        private const string IsRunningKey = "Puzzle.Client.PurchaseCheck.IsRunning";
        private const string WalkedKey = "Puzzle.Client.PurchaseCheck.Walked";

        /// <summary>The level the fake store's player stands on: past the starter pack's.</summary>
        private const int StandingOnLevel = 25;

        /// <summary>The fake store's course has failed when it has not ended in this long.</summary>
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

        private static IEnumerator<string?>? _course;
        private static DateTime _deadline;

        /// <summary>Where the fake store's course stands, for a report of one that did not end.</summary>
        private static string _where = "not begun";

        static PurchaseCheck()
        {
            if (SessionState.GetBool(IsRunningKey, false))
            {
                EditorApplication.update += Watch;
            }
        }

        [MenuItem("Puzzle/Checks/Purchases")]
        public static void Run()
        {
            bool breaksTheSave = Environment.GetCommandLineArgs().Contains(BreaksTheSaveArgument);
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                var studio = new Studio(art, LevelPack.LoadPacked(), PaintingGallery.OfThePackedMuseum(), string.Empty);
                GameWalk.RunThePurchaseWalks(new ScreenShape(1080, 2424), studio, breaksTheSave ? GameWalk.Saving.SavedNowhere : GameWalk.Saving.AsTheGameSavesIt);
            }
            catch (InvalidOperationException failure)
            {
                Finish(passed: false, $"the purchase walks over the scripted store{(breaksTheSave ? " with a save that holds nothing" : string.Empty)}: {failure.Message}");
                return;
            }

            SessionState.SetString(WalkedKey, "the purchase walks over the scripted store passed");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(IsRunningKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Watch()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (_course == null)
            {
                _deadline = DateTime.UtcNow + Patience;
                _course = TheFakeStoresCourse();
            }

            try
            {
                if (_course.MoveNext())
                {
                    if (_course.Current is string report)
                    {
                        Finish(passed: true, $"{SessionState.GetString(WalkedKey, string.Empty)}; {report}");
                    }
                    else if (DateTime.UtcNow > _deadline)
                    {
                        Finish(passed: false, $"Unity's fake store did not end its course in {Patience.TotalSeconds:F0} s; it stood at {_where}");
                    }
                }
            }
            catch (InvalidOperationException failure)
            {
                Finish(passed: false, $"Unity's fake store: {failure.Message}");
            }
        }

        private static void Finish(bool passed, string report)
        {
            EditorApplication.update -= Watch;
            SessionState.EraseBool(IsRunningKey);
            SessionState.EraseString(WalkedKey);
            Debug.Log($"Purchase check: {(passed ? "PASSED" : "FAILED")}: {report}");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        /// <summary>
        /// The course over Unity's fake store, a step per Editor update: null while it waits for the store, the report
        /// once it has ended. A step that finds what it requires missing throws.
        /// </summary>
        private static IEnumerator<string?> TheFakeStoresCourse()
        {
            EconomyRules rules = EconomyPack.LoadPacked();
            StoreCatalogue catalogue = rules.Shop.Store;
            var store = new UnityStore(offersRestore: true);
            var keeper = new Keeper(rules, new PlayerProgress(StandingOnLevel - 1, attemptsOnLevel: 0, PlacedPieces.None, rules.StartingWallet));
            var checkout = new Checkout(catalogue, RealMoneySales.ThroughTheStore, Application.identifier, store);

            checkout.Open(TimeSpan.Zero, keeper);
            while (checkout.Status is StoreStatus.Connecting)
            {
                _where = "the store's opening";
                checkout.Step(keeper);
                yield return null;
            }

            StoreView opened = checkout.ViewFor(keeper.Saved.Purchases, StandingOnLevel);
            Require(
                checkout.Status is StoreStatus.Open && opened.CoinPacks.All(pack => pack.CanBeBought) && opened.StarterPack is { CanBeBought: true },
                $"the store opened at {checkout.Status}, without a price for every product");
            string prices = string.Join(", ", opened.CoinPacks.Select(pack => pack.Price!.Words).Append(opened.StarterPack!.Price!.Words));

            foreach (StoreProduct product in catalogue.Products)
            {
                PlayerProgress before = keeper.Saved;
                Require(checkout.Ask(product, before.Purchases, StandingOnLevel) && checkout.State is PurchaseState.Asking && checkout.Pay(TimeSpan.Zero), $"{product.Name} could not be asked for and paid");
                while (checkout.State is PurchaseState.Pending)
                {
                    _where = $"the payment for {product.Name}";
                    AnswerTheFakeStoresSheet(pays: true);
                    checkout.Step(keeper);
                    yield return null;
                }

                checkout.Step(keeper);
                Require(checkout.State is PurchaseState.Delivered, $"the payment for {product.Name} came to {checkout.State}");
                Require(
                    keeper.Saved.Wallet.Coins == before.Wallet.Coins + product.Coins && keeper.Saved.Purchases.TimesBought(product.IdSuffix) == 1
                    && keeper.Saved.Wallet.Shelf.Equals(product.Contents.AddedTo(before.Wallet).Shelf),
                    $"{product.Name} left {keeper.Saved.Wallet.Coins} coins and {keeper.Saved.Purchases} in the saved progress, not one more of it");
                Require(checkout.WaitingToBeConfirmed.Count == 0, $"the payment for {product.Name} was not confirmed once saved");
                checkout.Dismiss();
            }

            // A sheet cancelled: nothing given, nothing to confirm.
            PlayerProgress beforeTheCancel = keeper.Saved;
            Require(checkout.Ask(catalogue.CoinPacks[0], beforeTheCancel.Purchases, StandingOnLevel) && checkout.Pay(TimeSpan.Zero), "the first pack could not be asked for a second time");
            while (checkout.State is PurchaseState.Pending)
            {
                _where = "the sheet to cancel";
                AnswerTheFakeStoresSheet(pays: false);
                checkout.Step(keeper);
                yield return null;
            }

            Require(
                checkout.State is PurchaseState.Cancelled && keeper.Saved.Equals(beforeTheCancel) && checkout.WaitingToBeConfirmed.Count == 0,
                $"a cancelled sheet came to {checkout.State} and left {keeper.Saved.Wallet.Coins} coins");
            checkout.Dismiss();

            int saves = keeper.Saves;
            Require(
                checkout.Ask(catalogue.StarterPack.Product, keeper.Saved.Purchases, StandingOnLevel) && checkout.State is PurchaseState.Failed { Why: PurchaseFailure.SoldOut },
                $"a second starter pack came to {checkout.State}, not refused before the store");
            checkout.Dismiss();

            Require(checkout.RestorePurchases(), "the restore could not be asked for");
            while (checkout.Restore is RestoreState.Restoring)
            {
                _where = "the restore on the progress that holds every purchase";
                checkout.Step(keeper);
                yield return null;
            }

            Require(
                checkout.Restore is RestoreState.Ended ended && ended.BroughtBack.Count == 0 && keeper.Saved.Wallet.Equals(keeper.WalletAfterTheBuying),
                $"a restore on the progress that holds every purchase came to {checkout.Restore} and left {keeper.Saved.Wallet}");
            string restored = checkout.Restore is RestoreState.Ended { Succeeded: true } ? "ended and brought nothing" : "was refused in this Editor and changed nothing";
            checkout.DismissTheRestore();

            // A new install with the same store account: the store tells of the starter pack it keeps, and it is given once.
            var newInstall = new Keeper(rules, new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0, PlacedPieces.None, rules.StartingWallet));
            var afterTheInstall = new Checkout(catalogue, RealMoneySales.ThroughTheStore, Application.identifier, store);
            afterTheInstall.Open(TimeSpan.Zero, newInstall);
            for (int frame = 0; frame < 60 && newInstall.Saved.Purchases.TimesBought(catalogue.StarterPack.Product.IdSuffix) == 0; frame++)
            {
                _where = "the new install's opening";
                afterTheInstall.Step(newInstall);
                yield return null;
            }

            for (int frame = 0; frame < 10; frame++)
            {
                afterTheInstall.Step(newInstall); // a second telling would show here
                yield return null;
            }

            StoreProduct starter = catalogue.StarterPack.Product;
            Require(
                newInstall.Saved.Purchases.TimesBought(starter.IdSuffix) == 1 && newInstall.Saved.Wallet.Coins == rules.StartingWallet.Coins + starter.Coins
                && catalogue.CoinPacks.All(pack => newInstall.Saved.Purchases.TimesBought(pack.IdSuffix) == 0),
                $"the new install holds {newInstall.Saved.Purchases} and {newInstall.Saved.Wallet.Coins} coins, not the starter pack alone, once");
            yield return $"Unity's fake store opened without Unity Gaming Services at {prices}; {catalogue.Products.Count} products bought, each given once and confirmed once saved "
                + $"({saves} saves); a cancelled sheet gave nothing; a second starter pack refused; a restore {restored}; a new install got the starter pack back once when the store opened";
        }

        /// <summary>
        /// Answers Unity's fake store's payment sheet as a tester's tap on it would, by its own "Buy" or "Cancel": the
        /// window is drawn by the Editor's immediate interface, which a batch run never draws, so its answers are found
        /// on it by their names. False while no sheet is up.
        /// </summary>
        /// <exception cref="InvalidOperationException">The sheet has no such answer: the package changed how its fake store asks.</exception>
        private static bool AnswerTheFakeStoresSheet(bool pays)
        {
            GameObject? window = GameObject.Find("UIFakeStoreWindow");
            MonoBehaviour? sheet = window != null ? window.GetComponents<MonoBehaviour>().FirstOrDefault(part => part.GetType().Name == "UIFakeStoreWindow") : null;
            if (sheet == null)
            {
                return false;
            }

            string answer = pays ? "OnOkClicked" : "OnCancelClicked";
            MethodInfo click = sheet.GetType().GetMethod(answer, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Unity's fake store's sheet has no {answer}: the package changed how it asks");
            click.Invoke(sheet, null);
            return true;
        }

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }

        /// <summary>The check's progress: given by the game's rules, saved at once, counted.</summary>
        private sealed class Keeper : IPurchaseKeeper
        {
            private readonly WalletLedger _ledger;

            public Keeper(EconomyRules rules, PlayerProgress start)
            {
                _ledger = new WalletLedger(rules);
                Saved = start;
            }

            public PlayerProgress Saved { get; private set; }

            public int Saves { get; private set; }

            /// <summary>The wallet after the last payment that gave something.</summary>
            public Wallet? WalletAfterTheBuying { get; private set; }

            public bool IsSaved => true;

            public ProgressKeeping Keeping => ProgressKeeping.AfterClosing;

            public Delivery Deliver(StoreProduct product, StorePayment payment, DeliveryReason reason)
            {
                Delivery delivery = _ledger.Deliver(Saved, product, payment.TransactionId);
                switch (delivery)
                {
                    case Delivery.Given given:
                        Saved = given.Progress;
                        WalletAfterTheBuying = given.Progress.Wallet;
                        Saves++;
                        break;
                    case Delivery.SoldOut soldOut:
                        Saved = soldOut.Progress;
                        Saves++;
                        break;
                }

                return delivery;
            }
        }
    }
}
