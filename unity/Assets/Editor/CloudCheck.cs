#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Cloud;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Canvases;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Meta;
using Unity.Services.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks the player's account and the cloud save on the game's own sessions, by touches alone, with Unity's services
    /// and the platform's sign-in in memory (Cloud/FakeAccounts.cs; docs/research/server-v1.md §5):
    /// <list type="bullet">
    /// <item>this build's own cloud in the Editor starts none of Unity's services (no cloud project in the Editor);</item>
    /// <item>with the "Bulut kaydı" switch off nothing is started, and a level is won as before;</item>
    /// <item>a phone signed in to an account of its own is offered the link to Google Play Games once, after its first
    /// won level, and "Bağlan" links it and says so; a new phone signed in to Play Games gets the save back, the hall
    /// opens on it and a card says so;</item>
    /// <item>"Şimdi değil" is not asked again; a platform player with a save of their own is asked about it, and "Ona geç"
    /// goes over to it with this phone's progress added; a phone without Play Games is offered nothing; services out of
    /// reach leave the game as it was.</item>
    /// </list>
    /// The levels' goals are cut to almost nothing (EarlyWin), so the expert bot wins each in a few moves, and the
    /// players have seen the tutorial. The cards are photographed into out/unity/shots/cloud and onto a sheet.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.CloudCheck.Run
    /// The command ends with exit code 1 when a check failed; the log's last line of the check says what came of it.
    /// </summary>
    public static class CloudCheck
    {
        private const string Alice = "alice";
        private const ulong BotSeed = 20261026;
        private const ulong BotStream = 2;

        /// <summary>Every goal of a level cut to this part of its count, one at least.</summary>
        private const float GoalsLeft = 0.01f;

        private static readonly ScreenShape Phone = new ScreenShape(1080, 2424, 0, 0);

        public static void Run()
        {
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath)
                    ?? throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked().Select(level => EarlyWin.Of(level, GoalsLeft)).ToArray();
                var bench = new Bench(art, levels, PaintingGallery.OfThePackedMuseum(), new ContactSheet(shrinkBy: 4, mostInARow: 4));
                var findings = new List<string>
                {
                    CheckThatThisBuildStartsNothingInTheEditor(levels),
                    CheckThatTheSwitchOffStartsNothing(bench),
                    CheckTheLinkAndTheRestoreOnANewPhone(bench),
                    CheckThatNotNowIsNotAskedAgain(bench),
                    CheckGoingOverToThePlatformPlayersSave(bench),
                    CheckAPhoneWithoutThePlatform(bench),
                    CheckServicesOutOfReach(bench),
                };
                StoryStudio.WriteSheet(bench.Sheet, "cloud-sheet.png");
                Debug.Log($"Cloud check: passed. {string.Join(" ", findings)}");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is LevelFormatException || failure is ArgumentException)
            {
                Debug.LogError($"Cloud check: failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The game's own cloud, as GameRoot makes it, started in the Editor: no cloud project here, so Unity's services stay uninitialised.</summary>
        private static string CheckThatThisBuildStartsNothingInTheEditor(IReadOnlyList<LevelDefinition> levels)
        {
            var said = new List<string>();
            var holder = new ProgressHeld(StoryStudio.StandingOn(levels[0], levels));
            CloudSaving cloud = CloudOfThisBuild.Make(holder, StoryStudio.Economy, said.Add, failure => throw new InvalidOperationException($"the cloud's background work failed: {failure.Message}", failure));
            Task start = cloud.StartAsync(CancellationToken.None);
            Require(start.IsCompleted && !start.IsFaulted, "the start of the game's own cloud in the Editor did not end at once");
            Require(cloud.Account is Account.Off, $"the game's own cloud in the Editor is {cloud.Account}, not off");
            Require(UnityServices.State == ServicesInitializationState.Uninitialized, $"Unity's services are {UnityServices.State} after the game's own cloud started in the Editor");
            return $"In the Editor this build's cloud is {cloud.Account} and Unity's services {UnityServices.State}.";
        }

        /// <summary>The switch off: the services are never started, no account is made, and a level is won and kept as before.</summary>
        private static string CheckThatTheSwitchOffStartsNothing(Bench bench)
        {
            var services = new FakeUnityServices();
            using PhoneOfThePlayer phone = bench.NewPhone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true), switchedOn: false);
            bench.WinTheLevel(phone.Session);
            Require(services.Starts == 0 && services.Accounts.Count == 0, $"with the switch off the services were started {services.Starts} times and hold {services.Accounts.Count} accounts");
            Require(phone.Session.Progress.LevelsWon == 1 && phone.Museum.NoticeShown == null, "with the switch off the won level was not kept, or a card came down over the hall");
            return "Switch off: no start, no account, the level kept.";
        }

        /// <summary>
        /// A first phone on an account of its own: no card at the start; after the first won level the offer comes down,
        /// "Bağlan" links the account and the card says so, and the save is in the cloud. A second won level offers
        /// nothing. A new phone signed in to Play Games gets that save: the hall opens on it, and its card says so.
        /// </summary>
        private static string CheckTheLinkAndTheRestoreOnANewPhone(Bench bench)
        {
            var services = new FakeUnityServices();
            string linked;
            using (PhoneOfThePlayer first = bench.NewPhone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false)))
            {
                Require(first.Cloud.Account is Account.Anonymous && first.Museum.NoticeShown == null, $"the first phone starts as {first.Cloud.Account} with \"{first.Museum.NoticeShown}\" over the hall");
                bench.WinTheLevel(first.Session);
                RequireTheCard(first, AccountWords.Offer(SignInPlatform.PlayGames), NoticeAnswers.LinkOrNotNow, "after the first won level");
                bench.Shoot(first, "link-offer.png");
                ThroughTheCard.Press(first.Session, first.Museum.NoticeGreenAnswerPixel, "the offer's Bağlan");
                StoryStudio.AdvanceUntilSettled(first.Session);
                RequireTheCard(first, AccountWords.Of(new AccountNews.Linked(SignInPlatform.PlayGames), 2, first.Session.Progress.Wallet.Coins), NoticeAnswers.Done, "after Bağlan");
                bench.Shoot(first, "linked.png");
                ThroughTheCard.Press(first.Session, first.Museum.NoticeDonePixel, "the card's Tamam");
                StoryStudio.AdvanceUntilSettled(first.Session);
                CloudPlayerId account = services.AccountOf(SignInPlatform.PlayGames, Alice) ?? throw new InvalidOperationException("Bağlan linked no account to Play Games");
                Require(first.Cloud.Account is Account.Linked && services.CloudOf(account).Save != null, $"after Bağlan the phone is {first.Cloud.Account} and its account's save is {(services.CloudOf(account).Save == null ? "missing" : "there")}");

                bench.WinTheLevel(first.Session);
                Require(first.Museum.NoticeShown == null, $"a second won level brought \"{first.Museum.NoticeShown}\" down: the offer is made once");
                first.Session.SaveProgress();
                first.Cloud.AskForAnUpload(SyncTrigger.PaintingOrRoomCompleted); // an upload outside the budget, as a completed painting asks
                linked = $"{account}";
            }

            using PhoneOfThePlayer fresh = bench.NewPhone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true));
            Require(fresh.Session.Progress.LevelsWon == 2, $"the new phone stands at {fresh.Session.Progress.LevelsWon} levels won after its start, not at the save's 2");
            RequireTheCard(fresh, AccountWords.Of(new AccountNews.SaveCameBack(), 3, fresh.Session.Progress.Wallet.Coins), NoticeAnswers.Done, "on the new phone");
            bench.Shoot(fresh, "save-came-back.png");
            return $"Linked: once after the first win, account {linked}; a new phone came back to level 3.";
        }

        /// <summary>"Şimdi değil": the card goes up, nothing is asked of the account, and the next won level offers nothing.</summary>
        private static string CheckThatNotNowIsNotAskedAgain(Bench bench)
        {
            var services = new FakeUnityServices();
            using PhoneOfThePlayer phone = bench.NewPhone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false));
            bench.WinTheLevel(phone.Session);
            RequireTheCard(phone, AccountWords.Offer(SignInPlatform.PlayGames), NoticeAnswers.LinkOrNotNow, "after the first won level");
            ThroughTheCard.Press(phone.Session, phone.Museum.NoticeBlueAnswerPixel, "the offer's Şimdi değil");
            StoryStudio.AdvanceUntilSettled(phone.Session);
            Require(phone.Museum.NoticeShown == null && phone.Cloud.Account is Account.Anonymous, $"after Şimdi değil \"{phone.Museum.NoticeShown}\" hangs and the account is {phone.Cloud.Account}");
            bench.WinTheLevel(phone.Session);
            Require(phone.Museum.NoticeShown == null && phone.States.Load().State.LinkOffered, "the offer came again after Şimdi değil, or was not kept as made");
            return "Şimdi değil: not asked again.";
        }

        /// <summary>
        /// The platform player has a save of their own: "Bağlan" asks whether to go over to it; "Ona geç" signs in to it,
        /// this phone's coins are added to the save's, and the card says the save came back.
        /// </summary>
        private static string CheckGoingOverToThePlatformPlayersSave(Bench bench)
        {
            var services = new FakeUnityServices();
            using (PhoneOfThePlayer old = bench.NewPhone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true)))
            {
                bench.WinTheLevel(old.Session);
                bench.WinTheLevel(old.Session);
                old.Cloud.AskForAnUpload(SyncTrigger.PaintingOrRoomCompleted);
            }

            CloudPlayerId theirs = services.AccountOf(SignInPlatform.PlayGames, Alice) ?? throw new InvalidOperationException("the old phone made no account linked to Play Games");
            long coinsOfTheSave = CoinsIn(services.CloudOf(theirs));
            using PhoneOfThePlayer phone = bench.NewPhone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false));
            bench.WinTheLevel(phone.Session);
            ThroughTheCard.Press(phone.Session, phone.Museum.NoticeGreenAnswerPixel, "the offer's Bağlan");
            StoryStudio.AdvanceUntilSettled(phone.Session);
            RequireTheCard(phone, AccountWords.Of(new AccountNews.OtherSaveFound(SignInPlatform.PlayGames), 2, phone.Session.Progress.Wallet.Coins), NoticeAnswers.SwitchOrStay, "after Bağlan to a player with a save");
            bench.Shoot(phone, "other-save.png");
            ThroughTheCard.Press(phone.Session, phone.Museum.NoticeGreenAnswerPixel, "the question's Ona geç");
            StoryStudio.AdvanceUntilSettled(phone.Session);
            Require(phone.Cloud.Account is Account.Linked { SignedInAs: var player } && player == theirs, $"after Ona geç the phone is {phone.Cloud.Account}, not on {theirs}");
            Require(phone.Session.Progress.LevelsWon == 2 && phone.Session.Progress.Wallet.Coins.Amount > coinsOfTheSave, $"going over left {phone.Session.Progress}, not the save's 2 levels with this phone's coins added to its {coinsOfTheSave}");
            RequireTheCard(phone, AccountWords.Of(new AccountNews.SaveCameBack(), 3, phone.Session.Progress.Wallet.Coins), NoticeAnswers.Done, "after Ona geç");
            return $"Gone over to {theirs}: {phone.Session.Progress.Wallet.Coins.Amount} coins of the save's {coinsOfTheSave} and this phone's.";
        }

        /// <summary>A phone without Play Games plays on an account of its own and is never offered the link.</summary>
        private static string CheckAPhoneWithoutThePlatform(Bench bench)
        {
            var services = new FakeUnityServices();
            using PhoneOfThePlayer phone = bench.NewPhone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, player: null, isSignedIn: false));
            bench.WinTheLevel(phone.Session);
            Require(phone.Cloud.Account is Account.Anonymous, $"a phone without Play Games is {phone.Cloud.Account}");
            Require(phone.Museum.NoticeShown == null, $"a phone without Play Games was shown \"{phone.Museum.NoticeShown}\"");
            using PhoneOfThePlayer without = bench.NewPhone(new FakeUnityServices(), NoPlatformSignIn.Instance);
            bench.WinTheLevel(without.Session);
            Require(without.Museum.NoticeShown == null, $"a build without the platform's sign-in was shown \"{without.Museum.NoticeShown}\"");
            return "No Play Games: no offer.";
        }

        /// <summary>Services out of reach (no network, the free tier's limit): the game plays and keeps its progress as before, with no card.</summary>
        private static string CheckServicesOutOfReach(Bench bench)
        {
            var services = new FakeUnityServices();
            services.GoDown(CloudFailure.Blocked);
            using PhoneOfThePlayer phone = bench.NewPhone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true));
            bench.WinTheLevel(phone.Session);
            Require(phone.Cloud.Account is Account.Unreachable, $"with the services blocked the account is {phone.Cloud.Account}");
            Require(phone.Session.Progress.LevelsWon == 1 && !phone.Session.HasUnsavedProgress && phone.Museum.NoticeShown == null, "with the services blocked the level was not kept, or a card came down");
            return "Out of reach: the game as before.";
        }

        private static void RequireTheCard(PhoneOfThePlayer phone, AccountCardWords words, NoticeAnswers answers, string when)
        {
            MuseumStage museum = phone.Museum;
            Require(museum.NoticeShown == words.Title, $"{when} the card in front is \"{museum.NoticeShown}\", not \"{words.Title}\"");
            Require(museum.NoticeAnswersShown == answers, $"{when} the card \"{words.Title}\" is answered {museum.NoticeAnswersShown}, not {answers}");
        }

        private static long CoinsIn(FakeCloudSave cloud) =>
            CloudEnvelope.Read((cloud.Save ?? throw new InvalidOperationException("the account has no save")).Text, new UnityDocumentJson(), StoryStudio.Economy.StartingWallet) is CloudSaveReading.Sound sound
                ? sound.Save.Progress.Wallet.Coins.Amount
                : throw new InvalidOperationException("the account's save does not read");

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }

        /// <summary>What every phone of the check shares: the art, the levels with their goals cut, the museum, the bot, and the sheet of the cards.</summary>
        private sealed class Bench
        {
            private readonly ArtCatalogue _art;
            private readonly PaintingGallery _paintings;

            public Bench(ArtCatalogue art, IReadOnlyList<LevelDefinition> levels, PaintingGallery paintings, ContactSheet sheet)
            {
                _art = art;
                Levels = levels;
                _paintings = paintings;
                Sheet = sheet;
            }

            public IReadOnlyList<LevelDefinition> Levels { get; }

            public ContactSheet Sheet { get; }

            /// <summary>A phone of a new player who has seen the tutorial, at the hall, on its cloud of these services, started and at rest.</summary>
            public PhoneOfThePlayer NewPhone(FakeUnityServices services, IPlatformSignIn platform, bool switchedOn = true)
            {
                // A scene of its own that is never saved: nothing of an earlier phone is left in the next.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EconomyRules rules = StoryStudio.Economy;
                PlayerProgress newPlayer = StoryStudio.StandingOn(Levels[0], Levels);
                var progress = new ProgressInMemory(new PlayerProgress(newPlayer.LevelsWon, newPlayer.AttemptsOnLevel, newPlayer.Museum.Placed, newPlayer.Wallet, TutorialSeen.All));
                var session = new GameSession(
                    Levels, _art, _paintings, rules, StoryStudio.Clock, progress, StoryStudio.AttemptsKeptInMemory(), new SettingsInMemory(DeviceSettings.Default),
                    DeviceAbilities.OfThisBuild, CuePlayer.Silent, ScriptedStore.AtUsPrices(rules.Shop.Store, Application.identifier, offersRestore: false), Phone,
                    SessionOpening.InTheMuseum);
                FakePhoneAccounts accounts = services.Phone();
                var states = new CloudStateInMemory(switchedOn ? CloudState.New(RandomIds.New()) : CloudState.Forgotten(RandomIds.New()));
                var sync = new CloudSync(accounts, states, session.ProgressForTheCloud, rules, new UnityDocumentJson(), StoryStudio.Clock, "1.1.0", RandomIds.New);
                var cloud = new CloudSaving(
                    sync, accounts, platform, NoRemoteSettings.Instance, session.ProgressForTheCloud, hasACloudProject: true, Debug.Log,
                    failure => throw new InvalidOperationException($"the cloud's background work failed: {failure.Message}", failure));
                session.OpenToTheCloud(cloud);
                Task start = cloud.StartAsync(CancellationToken.None);
                Require(start.IsCompleted && !start.IsFaulted, "the cloud's start on the fakes did not end at once");

                // A step at least, as the game takes one every frame: a save that came back is taken in at the next step.
                session.Advance(StoryStudio.FrameSeconds);
                StoryStudio.AdvanceUntilSettled(session);
                return new PhoneOfThePlayer(session, cloud, states);
            }

            /// <summary>From the hall into the level the player stands on, won by the expert bot, and through the won card's Devam back to the hall, at rest.</summary>
            public void WinTheLevel(GameSession session)
            {
                ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
                IBotPolicy expert = BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot) ? bot : throw new InvalidOperationException("The bots' list has no expert.");
                FlowStory.PlayTheGame(session, expert, new Rng(BotSeed, BotStream));
                StoryStudio.AdvanceUntilSettled(session);
                Require(session.Stage.Game.Status == GameStatus.Won, $"the expert bot did not win level {session.Stage.Game.Level.Id.Value} with its goals cut ({session.Stage.Game.Status})");
                ThroughTheCard.Press(session, session.Stage.ResultButtonPixel, "the won card's Devam");
                ThroughTheCard.WaitFor(session, () => session.Museum != null, "the won card's Devam opened no museum");
                StoryStudio.AdvanceUntilSettled(session);
            }

            public void Shoot(PhoneOfThePlayer phone, string file) => StoryStudio.Shoot(phone.Session, Phone, Sheet, "cloud/" + file);
        }

        /// <summary>A phone of the check: its session, its cloud and the cloud.json it keeps in memory.</summary>
        private sealed class PhoneOfThePlayer : IDisposable
        {
            public PhoneOfThePlayer(GameSession session, CloudSaving cloud, CloudStateInMemory states)
            {
                Session = session;
                Cloud = cloud;
                States = states;
            }

            public GameSession Session { get; }

            public CloudSaving Cloud { get; }

            public CloudStateInMemory States { get; }

            public MuseumStage Museum => Session.Museum ?? throw new InvalidOperationException("The phone is not in the museum, where the check expected it.");

            public void Dispose() => Session.Dispose();
        }

        /// <summary>A progress held for the cloud of this build in the Editor: it is never synced (no services start).</summary>
        private sealed class ProgressHeld : IProgressHolder
        {
            public ProgressHeld(PlayerProgress progress)
            {
                Current = progress;
            }

            public PlayerProgress Current { get; private set; }

            public ProgressKeeping Keeping => ProgressKeeping.AfterClosing;

            public bool IsTesterMode => false;

            public void Adopt(PlayerProgress progress) => Current = progress;
        }
    }
}
