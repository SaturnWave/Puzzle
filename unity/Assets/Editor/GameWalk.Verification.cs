#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Cloud;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Meta;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walks of purchases verified by the game's server (Stage 4 of docs/research/server-v1.md, §4.8), on the game's
    /// own session over a scripted store, with its cloud on Unity's services in memory and the Cloud Code module in memory
    /// (Cloud/FakePurchaseServer), by touches alone (<c>walk-verify-...</c>): a payment the server verifies, whose card
    /// says it is verified while the server is asked and then that it was bought, given once with its ledger number,
    /// confirmed once saved and uploaded; a payment the server finds not real, neither given nor confirmed, whose card
    /// says the store gives back the money; a server out of reach (the module not deployed), whose card says calmly that
    /// nothing is lost and can be closed, the payment waiting unconfirmed and given once the server answers a minute
    /// later; and two phones of one Play Games player, the second finding the first's payment in the cloud's save and
    /// confirming it without giving it a second time. Every walk fails when a payment is given twice, given before it is
    /// verified, or confirmed before the save holds it.
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>Walks every verification on a screen.</summary>
        /// <returns>How many pictures were taken.</returns>
        public static int RunTheVerificationWalks(ScreenShape screen, Studio studio)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, PurchaseLevel), studio.Levels);
            return WalkAVerifiedPayment(standing, screen, studio)
                + WalkAPaymentNotVerified(standing, screen, studio)
                + WalkAServerOutOfReach(standing, screen, studio)
                + WalkTwoPhonesOfOnePlayer(standing, screen, studio);
        }

        /// <summary>The bag paid for: "Doğrulanıyor" while the server is asked, then bought, given once with its number, confirmed and uploaded.</summary>
        private static int WalkAVerifiedPayment(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            StoreProduct bag = catalogue.CoinPacks[1]; // the pouch's place holds the starter pack on this level
            var services = new FakeUnityServices();
            var server = new FakePurchaseServer(services, new UnityDocumentJson());
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: false);
            var kept = new ProgressInMemory(before);
            var held = new HeldVerdicts();
            (GameSession session, CloudSaving cloud) = SessionWithTheCloud(studio, kept, store, screen, services, server, NoPlatform(), held);
            using (session)
            {
                CloudPlayerId player = cloud.Account.Player ?? throw new InvalidOperationException("The phone's cloud did not sign in on the fake services.");
                var reel = new Reel(session, screen, $"walk-verify-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Settle();
                TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
                AskAndPay(reel, MuseumOf(session).ShopPackPixel(1), bag);
                reel.Shoot("verifying");
                ThroughTheCard.Require(
                    session.Checkout.State is PurchaseState.Verifying && MuseumOf(session).NoticeShown == PurchaseWords.Of(new PurchaseState.Verifying(bag))!.Title
                    && MuseumOf(session).NoticeDonePixel == null && MuseumOf(session).NoticeClosePixel == null,
                    $"a payment the server is asked about stands at {session.Checkout.State} with the card \"{MuseumOf(session).NoticeShown}\", or can be closed");
                ThroughTheCard.Require(
                    session.Progress.Equals(before) && store.Unconfirmed.Count == 1 && store.Confirmed.Count == 0 && store.LastBuyer?.PlayerId == player.Value,
                    "a payment the server has not verified yet was given or confirmed, or its sheet did not name the player to the store");

                held.LetGo();
                reel.Advance(StoryStudio.FrameSeconds);
                reel.Settle();
                reel.Shoot("verified");
                RequireGivenOnce(session, kept, store, bag, before, "the verified payment");
                ThroughTheCard.Require(
                    kept.Load().Purchases.LedgerDelivered.Contains(new LedgerNumber(1)) && server.LedgerOf(player).Count == 1 && server.Validations == 1,
                    $"the verified payment is not in the saved progress with its ledger number #1, or the server was asked {server.Validations} times");
                ThroughTheCard.Require(
                    cloud.LastSync is SyncOutcome.Done { Upload: UploadResult.Written } && services.CloudOf(player).Save != null,
                    $"the verified payment was not uploaded at once: the last sync came to {cloud.LastSync}");
                CloseTheNotice(reel);
                reel.WriteSheet();
                Debug.Log($"Purchases verified: {bag.Name} verified as #1, given once, confirmed once saved and uploaded; the server was asked once.");
                return reel.Taken;
            }
        }

        /// <summary>A payment no store made (a forged receipt): the card says the store gives back the money; nothing is given or confirmed.</summary>
        private static int WalkAPaymentNotVerified(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            StoreProduct bag = catalogue.CoinPacks[1];
            var services = new FakeUnityServices();
            var server = new FakePurchaseServer(services, new UnityDocumentJson());
            server.Forged(ScriptedStore.ProofOf("SCRIPTED.0001")); // the scripted store's first payment
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: false);
            var kept = new ProgressInMemory(before);
            (GameSession session, CloudSaving _) = SessionWithTheCloud(studio, kept, store, screen, services, server, NoPlatform(), held: null);
            using (session)
            {
                var reel = new Reel(session, screen, $"walk-verify-refused-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Settle();
                TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
                AskAndPay(reel, MuseumOf(session).ShopPackPixel(1), bag);
                reel.Shoot("not-verified");
                ThroughTheCard.Require(
                    session.Checkout.State is PurchaseState.Failed { Why: PurchaseFailure.NotVerified }
                    && MuseumOf(session).NoticeShown == PurchaseWords.Of(new PurchaseState.Failed(bag, PurchaseFailure.NotVerified))!.Title,
                    $"a payment the server found not real came to {session.Checkout.State} with the card \"{MuseumOf(session).NoticeShown}\"");
                ThroughTheCard.Require(
                    kept.Load().Equals(before) && store.Unconfirmed.Count == 1 && store.Confirmed.Count == 0,
                    "a payment the server found not real was given or confirmed");
                CloseTheNotice(reel);
                reel.WriteSheet();
                Debug.Log("Purchases verified: a forged payment was neither given nor confirmed; the store gives the money back.");
                return reel.Taken;
            }
        }

        /// <summary>
        /// The module not deployed yet: the card says calmly that nothing is lost and closes; the payment waits unconfirmed;
        /// once the module answers, a minute later, it is given once and confirmed.
        /// </summary>
        private static int WalkAServerOutOfReach(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            StoreProduct bag = catalogue.CoinPacks[1];
            var services = new FakeUnityServices();
            var server = new FakePurchaseServer(services, new UnityDocumentJson());
            server.GoesOut(FakeServerOutage.NotDeployed);
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: false);
            var kept = new ProgressInMemory(before);
            (GameSession session, CloudSaving _) = SessionWithTheCloud(studio, kept, store, screen, services, server, NoPlatform(), held: null);
            using (session)
            {
                var reel = new Reel(session, screen, $"walk-verify-later-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Settle();
                TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
                AskAndPay(reel, MuseumOf(session).ShopPackPixel(1), bag);
                reel.Shoot("waits");
                ThroughTheCard.Require(
                    session.Checkout.State is PurchaseState.Unverified && MuseumOf(session).NoticeShown == PurchaseWords.Of(new PurchaseState.Unverified(bag))!.Title
                    && MuseumOf(session).NoticeDonePixel != null,
                    $"a payment the server could not verify stands at {session.Checkout.State} with the card \"{MuseumOf(session).NoticeShown}\", which cannot be closed");
                CloseTheNotice(reel);
                ThroughTheCard.Require(
                    kept.Load().Equals(before) && session.Checkout.WaitingForTheServer.Count == 1 && store.Unconfirmed.Count == 1 && store.Confirmed.Count == 0,
                    "a payment the server could not verify was given or confirmed, or does not wait for it");

                server.ComesBack();
                reel.Advance(61f);
                reel.Settle();
                reel.Shoot("verified-later");
                RequireGivenOnce(session, kept, store, bag, before, "the payment verified a minute later");
                ThroughTheCard.Require(server.Validations == 1 && session.Checkout.WaitingForTheServer.Count == 0, $"the module was asked {server.Validations} times once it answered");
                CloseTheNotice(reel);
                reel.WriteSheet();
                Debug.Log("Purchases verified: with the module out of reach the payment waited unconfirmed, and was given once a minute later when it answered.");
                return reel.Taken;
            }
        }

        /// <summary>
        /// Two phones of one Google Play Games player and one store account: the first pays for the bag, the server verifies
        /// it and the save holds it, and the game is closed before it confirms it; the second is a new install, gets the save
        /// back, is told of the unconfirmed payment by the store, and confirms it without giving it again.
        /// </summary>
        private static int WalkTwoPhonesOfOnePlayer(PlayerProgress before, ScreenShape screen, Studio studio)
        {
            StoreCatalogue catalogue = StoryStudio.Economy.Shop.Store;
            StoreProduct bag = catalogue.CoinPacks[1];
            var services = new FakeUnityServices();
            var server = new FakePurchaseServer(services, new UnityDocumentJson());
            var store = ScriptedStore.AtUsPrices(catalogue, Application.identifier, offersRestore: false);
            var keptOnTheFirst = new ProgressInMemory(before);
            (GameSession first, CloudSaving firstCloud) = SessionWithTheCloud(
                studio, keptOnTheFirst, new ClosesBeforeConfirming(store), screen, services, server, new FakePlatformSignIn(SignInPlatform.PlayGames, "alice", isSignedIn: true), held: null);
            using (first)
            {
                var reel = new Reel(first, screen, $"walk-verify-first-phone-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Settle();
                TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
                AskAndPay(reel, MuseumOf(first).ShopPackPixel(1), bag);
                reel.Shoot("bought");
                ThroughTheCard.Require(
                    keptOnTheFirst.Load().Wallet.Coins == bag.Contents.AddedTo(before.Wallet).Coins && keptOnTheFirst.Load().Purchases.LedgerDelivered.Contains(new LedgerNumber(1))
                    && store.Unconfirmed.Count == 1 && firstCloud.Account is Account.Linked,
                    "the first phone did not give the verified bag with its number, or confirmed it, or is not on the Play Games player's account");
                reel.WriteSheet();
            }

            PlayerProgress newInstall = StoryStudio.StandingOn(studio.Levels[0], studio.Levels);
            var keptOnTheSecond = new ProgressInMemory(
                new PlayerProgress(newInstall.LevelsWon, newInstall.AttemptsOnLevel, newInstall.Museum.Placed, newInstall.Wallet, TutorialSeen.All));
            (GameSession second, CloudSaving secondCloud) = SessionWithTheCloud(
                studio, keptOnTheSecond, store, screen, services, server, new FakePlatformSignIn(SignInPlatform.PlayGames, "alice", isSignedIn: true), held: null);
            using (second)
            {
                var reel = new Reel(second, screen, $"walk-verify-second-phone-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Advance(StoryStudio.FrameSeconds);
                reel.Settle();
                reel.Shoot("save-came-back");
                PlayerProgress onTheSecond = keptOnTheSecond.Load();
                ThroughTheCard.Require(
                    onTheSecond.Wallet.Coins == keptOnTheFirst.Load().Wallet.Coins && onTheSecond.Purchases.TimesBought(bag.IdSuffix) == 1
                    && onTheSecond.Purchases.LedgerDelivered.Contains(new LedgerNumber(1)),
                    $"the second phone holds {onTheSecond.Wallet.Coins} coins and {onTheSecond.Purchases}, not the first phone's save with the bag once");
                ThroughTheCard.Require(
                    store.Unconfirmed.Count == 0 && store.Confirmed.Count(payment => payment.StoreId.EndsWith(bag.IdSuffix, StringComparison.Ordinal)) == 1 && server.Validations == 1
                    && secondCloud.Account.Player == firstCloud.Account.Player,
                    $"the second phone did not confirm the first phone's payment once without asking the server again ({store.Unconfirmed.Count} wait, the server was asked {server.Validations} times)");
                reel.WriteSheet();
                Debug.Log("Purchases verified: a payment two phones of one player heard of was given once, on the phone that paid, and confirmed by the other.");
                return reel.Taken;
            }
        }

        /// <summary>The game's own session over a store, with its cloud on the fake services and the fake module, started and at rest.</summary>
        private static (GameSession Session, CloudSaving Cloud) SessionWithTheCloud(
            Studio studio, IProgressStore kept, IStore store, ScreenShape screen, FakeUnityServices services, FakePurchaseServer server, IPlatformSignIn platform, HeldVerdicts? held)
        {
            GameSession session = SessionOverAStore(studio, kept, store, screen);
            FakePhoneAccounts accounts = services.Phone();
            var sync = new CloudSync(
                accounts, new CloudStateInMemory(CloudState.New(RandomIds.New())), session.ProgressForTheCloud, StoryStudio.Economy, new UnityDocumentJson(), StoryStudio.Clock,
                "1.1.0", RandomIds.New);
            var cloud = new CloudSaving(
                sync, accounts, platform, NoRemoteSettings.Instance, server.Phone(accounts), session.ProgressForTheCloud, hasACloudProject: true, Debug.Log,
                failure => throw new InvalidOperationException($"the cloud's background work failed: {failure.Message}", failure));
            held?.Hold(cloud);
            session.OpenToTheCloud(held == null ? (ICloudDesk)cloud : new DeskWithHeldVerdicts(cloud, held));
            Task start = cloud.StartAsync(CancellationToken.None);
            ThroughTheCard.Require(start.IsCompleted && !start.IsFaulted, "the cloud's start on the fakes did not end at once");
            return (session, cloud);
        }

        private static FakePlatformSignIn NoPlatform() => new FakePlatformSignIn(SignInPlatform.None, player: null, isSignedIn: false);

        /// <summary>The server's verdicts, held back until the walk lets them go: a module that takes a while to ask the store.</summary>
        private sealed class HeldVerdicts : IPurchaseServer
        {
            private readonly List<ServerAnswer> _held = new List<ServerAnswer>();
            private IPurchaseServer _server = NoPurchaseServer.Instance;
            private bool _isHolding = true;

            public bool Verifies => _server.Verifies;

            public StoreBuyer? Buyer => _server.Buyer;

            public void Hold(IPurchaseServer server) => _server = server;

            /// <summary>The verdicts held, and every later one, come with the next step.</summary>
            public void LetGo() => _isHolding = false;

            public void Ask(PaymentQuestion question) => _server.Ask(question);

            public IReadOnlyList<ServerAnswer> TakeAnswers()
            {
                _held.AddRange(_server.TakeAnswers());
                if (_isHolding)
                {
                    return Array.Empty<ServerAnswer>();
                }

                ServerAnswer[] taken = _held.ToArray();
                _held.Clear();
                return taken;
            }
        }

        /// <summary>The cloud as the session asks it, its server's verdicts held back.</summary>
        private sealed class DeskWithHeldVerdicts : ICloudDesk
        {
            private readonly ICloudDesk _desk;

            public DeskWithHeldVerdicts(ICloudDesk desk, IPurchaseServer server)
            {
                _desk = desk;
                PurchaseServer = server;
            }

            public IPurchaseServer PurchaseServer { get; }

            public AccountView View => _desk.View;

            public SignInPlatform? OfferTheLink(int levelsWon) => _desk.OfferTheLink(levelsWon);

            public void Ask(AccountRequest request) => _desk.Ask(request);

            public IReadOnlyList<AccountNews> TakeNews() => _desk.TakeNews();

            public void AskForAnUpload(SyncTrigger trigger) => _desk.AskForAnUpload(trigger);
        }
    }
}
