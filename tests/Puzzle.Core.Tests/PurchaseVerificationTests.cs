using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Museum;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// Purchases verified by the game's server (Stage 4 of docs/research/server-v1.md, §4.8): the purchase's course over a
/// scripted store and a server the test answers by hand (<see cref="HandServer"/>), and the game's cloud save over the
/// fake services and the fake module (<see cref="FakePurchaseServer"/>). A payment is given only once the server says it
/// is real, with its ledger number, and confirmed only once saved; one the server finds not real is neither given nor
/// confirmed; a server out of reach keeps the payment waiting unconfirmed with the store, asked about again later and at
/// the store's next opening, and given once verified, never twice; a payment two phones of the player hear of is in
/// their merged progress once; and where no server verifies (the switch off), the payment is given on the phone as before.
/// </summary>
public class PurchaseVerificationTests
{
    private static readonly EconomyRules StoreRules = Rules(InAStoreBuild);
    private static readonly StoreCatalogue Catalogue = StoreRules.Shop.Store;
    private static readonly StoreProduct Small = Catalogue.CoinPacks[0];
    private static readonly StoreProduct Large = Catalogue.CoinPacks[1];
    private static readonly StoreProduct Starter = Catalogue.StarterPack.Product;
    private static readonly PlayerProgress OnLevelTwentyFive = new(levelsWon: 24, attemptsOnLevel: 0, PlacedPieces.None, WalletWith(100));
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public void A_payment_the_server_verifies_is_given_with_its_ledger_number_and_confirmed_once_saved()
    {
        var shop = new Shop();
        shop.Buy(Large);
        Assert.IsType<PurchaseState.Verifying>(shop.Checkout.State);
        Assert.Equal(OnLevelTwentyFive, shop.Keeper.Progress);
        Assert.Empty(shop.Store.Confirmed);
        Assert.Equal(new StoreBuyer("player-1").GooglePlayAccount, shop.Store.LastBuyer?.GooglePlayAccount);

        shop.Server.Answers(new ServerVerdict.Valid(new LedgerNumber(1), isTest: false));
        shop.Step();

        Assert.IsType<PurchaseState.Delivered>(shop.Checkout.State);
        Assert.Equal(100 + Large.Coins.Amount, shop.Keeper.Progress.Wallet.Coins.Amount);
        Assert.True(shop.Keeper.Progress.Purchases.LedgerDelivered.Contains(new LedgerNumber(1)));
        Assert.Single(shop.Store.Confirmed);
        Assert.Empty(shop.Checkout.WaitingForTheServer);
    }

    [Fact]
    public void A_payment_the_server_finds_not_real_is_neither_given_nor_confirmed_nor_asked_about_again_in_the_run()
    {
        var shop = new Shop();
        shop.Buy(Large);
        shop.Server.Answers(new ServerVerdict.Invalid("notFromTheStore"));
        shop.Step();

        Assert.Equal(PurchaseFailure.NotVerified, Assert.IsType<PurchaseState.Failed>(shop.Checkout.State).Why);
        Assert.Equal(OnLevelTwentyFive, shop.Keeper.Progress);
        Assert.Empty(shop.Store.Confirmed);
        Assert.Single(shop.Store.Unconfirmed);

        // The store tells of it again when it opens anew: it is left alone.
        int asked = shop.Server.Questions.Count;
        shop.Checkout.Dismiss();
        shop.Store.Open(StoreListing.AllOf(Catalogue, TestApplicationId));
        shop.Step();
        Assert.Equal(asked, shop.Server.Questions.Count);
        Assert.Equal(OnLevelTwentyFive, shop.Keeper.Progress);
    }

    [Fact]
    public void A_server_out_of_reach_keeps_the_payment_waiting_unconfirmed_and_gives_it_once_verified_later()
    {
        var shop = new Shop();
        shop.Buy(Large);
        shop.Server.Answers(new ServerVerdict.Unavailable("the module is not deployed (NotFound)"));
        shop.Step();

        // The card says it waits, and can be closed; nothing is given, nothing confirmed, nothing lost.
        Assert.IsType<PurchaseState.Unverified>(shop.Checkout.State);
        Assert.True(shop.Checkout.Dismiss());
        Assert.Equal(OnLevelTwentyFive, shop.Keeper.Progress);
        Assert.Single(shop.Checkout.WaitingForTheServer);
        Assert.Single(shop.Store.Unconfirmed);

        // Not asked again before a minute of the session; asked again after it.
        shop.PassTime(TimeSpan.FromSeconds(50));
        Assert.Single(shop.Server.Questions);
        shop.PassTime(TimeSpan.FromSeconds(15));
        Assert.Equal(2, shop.Server.Questions.Count);

        shop.Server.Answers(new ServerVerdict.Valid(new LedgerNumber(7), isTest: false));
        shop.Step();
        Assert.IsType<PurchaseState.Delivered>(shop.Checkout.State);
        Assert.Equal(100 + Large.Coins.Amount, shop.Keeper.Progress.Wallet.Coins.Amount);
        Assert.Single(shop.Store.Confirmed);

        // The store telling of it once more (a reopening) gives nothing more.
        shop.Store.Open(StoreListing.AllOf(Catalogue, TestApplicationId));
        shop.Step();
        Assert.Equal(100 + Large.Coins.Amount, shop.Keeper.Progress.Wallet.Coins.Amount);
    }

    [Fact]
    public void A_server_that_does_not_answer_in_ten_seconds_lets_the_card_close_and_the_payment_goes_on_waiting()
    {
        var shop = new Shop();
        shop.Buy(Small);
        shop.PassTime(Checkout.PatienceWithTheServer - Second);
        Assert.IsType<PurchaseState.Verifying>(shop.Checkout.State);
        Assert.False(shop.Checkout.Dismiss());

        shop.PassTime(Second);
        Assert.IsType<PurchaseState.Unverified>(shop.Checkout.State);
        Assert.True(shop.Checkout.Dismiss());

        // The verdict that comes late is given, and shown as a payment given meanwhile.
        shop.Server.Answers(new ServerVerdict.Valid(new LedgerNumber(1), isTest: true));
        shop.Step();
        Assert.IsType<PurchaseState.Delivered>(shop.Checkout.State);
        Assert.Equal(100 + Small.Coins.Amount, shop.Keeper.Progress.Wallet.Coins.Amount);
    }

    [Fact]
    public void A_payment_the_store_holds_as_not_made_yet_waits_as_a_deferred_one()
    {
        var shop = new Shop();
        shop.Buy(Small);
        shop.Server.Answers(new ServerVerdict.Pending());
        shop.Step();

        Assert.IsType<PurchaseState.Deferred>(shop.Checkout.State);
        Assert.Equal(OnLevelTwentyFive, shop.Keeper.Progress);
        Assert.Empty(shop.Checkout.WaitingForTheServer);
        Assert.Empty(shop.Store.Confirmed);
    }

    [Fact]
    public void A_payment_verified_before_whose_number_the_progress_holds_is_confirmed_and_not_given_again()
    {
        // The progress holds payment #3 already (given on another phone and merged in), under another store id.
        PlayerProgress holding = OnLevelTwentyFive.WithPurchases(OnLevelTwentyFive.Purchases.WithLedgerNumber(new LedgerNumber(3)));
        var shop = new Shop(holding);
        shop.Buy(Large);
        shop.Server.Answers(new ServerVerdict.AlreadyValidated(new LedgerNumber(3), isTest: false));
        shop.Step();

        Assert.Equal(holding.Wallet, shop.Keeper.Progress.Wallet);
        Assert.Single(shop.Store.Confirmed);
        Assert.IsType<PurchaseState.Nothing>(shop.Checkout.State);
    }

    [Fact]
    public void A_payment_the_progress_holds_already_is_confirmed_without_asking_the_server()
    {
        var shop = new Shop();
        shop.Keeper.ClosesBeforeTheConfirmation = true;
        shop.Buy(Large);
        shop.Server.Answers(new ServerVerdict.Valid(new LedgerNumber(1), isTest: false));
        shop.Step(); // given and saved; the game closes before the confirmation
        Assert.Single(shop.Store.Unconfirmed);

        // Opened again: the store tells of the payment, which the progress holds; it is confirmed, the server not asked.
        var reopened = new Shop(shop.Keeper.Progress, shop.Store);
        reopened.Step();
        Assert.Empty(reopened.Server.Questions);
        Assert.Empty(shop.Store.Unconfirmed);
        Assert.Equal(shop.Keeper.Progress.Wallet, reopened.Keeper.Progress.Wallet);
    }

    [Fact]
    public void Payments_that_waited_for_a_server_that_stopped_verifying_are_given_on_the_phone()
    {
        var shop = new Shop();
        shop.Buy(Small);
        shop.Server.Answers(new ServerVerdict.Unavailable("no network"));
        shop.Step();
        shop.Server.Verifies = false; // the player turned the "Bulut kaydı" switch off
        shop.Step();

        Assert.Equal(100 + Small.Coins.Amount, shop.Keeper.Progress.Wallet.Coins.Amount);
        Assert.True(shop.Keeper.Progress.Purchases.LedgerDelivered.IsEmpty);
        Assert.Single(shop.Store.Confirmed);
    }

    [Fact]
    public void The_module_replies_read_as_the_fixture_pins_them_and_an_unknown_reply_is_no_verdict()
    {
        using JsonDocument fixture = JsonDocument.Parse(System.IO.File.ReadAllText(RepoFiles.CloudCodeFixturePath("replies.json")));
        JsonElement replies = fixture.RootElement.GetProperty("validatePurchase");
        ServerVerdict VerdictOf(string kind) => ServerVerdict.Of(CloudSupport.Json.Read<PurchaseReplyDocument>(replies.GetProperty(kind).GetRawText()));

        Assert.Equal(3, Assert.IsType<ServerVerdict.Valid>(VerdictOf("valid")).Number.Value);
        Assert.True(Assert.IsType<ServerVerdict.AlreadyValidated>(VerdictOf("alreadyValidated")).IsTest);
        Assert.IsType<ServerVerdict.Pending>(VerdictOf("pending"));
        Assert.Equal("anotherPlayers", Assert.IsType<ServerVerdict.Invalid>(VerdictOf("invalid")).Reason);
        Assert.Equal("storeUnreachable", Assert.IsType<ServerVerdict.Unavailable>(VerdictOf("unavailable")).Reason);
        Assert.IsType<ServerVerdict.Unavailable>(ServerVerdict.Of(new PurchaseReplyDocument { verdict = "valid" })); // no number
        Assert.IsType<ServerVerdict.Unavailable>(ServerVerdict.Of(new PurchaseReplyDocument { verdict = "granted", seq = 2 }));
        Assert.IsType<ServerVerdict.Unavailable>(ServerVerdict.Of(null));
    }

    [Fact]
    public async Task The_cloud_verifies_while_the_switch_is_on_and_gives_on_the_phone_while_it_is_off()
    {
        var services = new FakeUnityServices();
        var on = new CloudPhone(services, new FakePurchaseServer(services, CloudSupport.Json));
        var off = new CloudPhone(services, new FakePurchaseServer(services, CloudSupport.Json), switchedOn: false);
        await on.Cloud.StartAsync(CancellationToken.None);
        await off.Cloud.StartAsync(CancellationToken.None);

        Assert.True(on.Cloud.Verifies);
        Assert.Equal(on.Cloud.Account.Player?.Value, on.Cloud.Buyer?.PlayerId);
        Assert.False(off.Cloud.Verifies);
        Assert.Null(off.Cloud.Buyer);
    }

    [Fact]
    public async Task A_payment_asked_about_while_the_account_signs_in_waits_for_the_sign_in()
    {
        var services = new FakeUnityServices();
        var server = new FakePurchaseServer(services, CloudSupport.Json);
        var phone = new CloudPhone(services, server, gatesTheStart: true);
        var question = new PaymentQuestion(new StorePayment("app.coins.large", "GPA.1", "token-1"), Large);

        Task start = phone.Cloud.StartAsync(CancellationToken.None);
        phone.Cloud.Ask(question);
        Assert.Empty(phone.Cloud.TakeAnswers());
        Assert.Equal(0, server.Validations);
        phone.OpenTheStart();
        await start;

        ServerAnswer answer = Assert.Single(phone.Cloud.TakeAnswers());
        Assert.Equal(1, Assert.IsType<ServerVerdict.Valid>(answer.Verdict).Number.Value);
        Assert.Equal(1, server.Validations);
    }

    [Fact]
    public async Task A_verified_payment_makes_the_next_merge_read_the_ledger_and_a_player_with_none_reads_it_once()
    {
        var services = new FakeUnityServices();
        var server = new FakePurchaseServer(services, CloudSupport.Json);
        var phone = new CloudPhone(services, server, start: OnLevelTwentyFive);
        await phone.Cloud.StartAsync(CancellationToken.None);
        CloudPlayerId player = phone.Cloud.Account.Player!.Value;
        FakeCloudSave cloud = services.CloudOf(player);
        await phone.Cloud.UploadAsync(SyncTrigger.PaintingOrRoomCompleted, CancellationToken.None);

        // A player with no payment: the start read the save (none yet) and no ledger; later merges read none either.
        phone.Clock.Passes(TimeSpan.FromDays(2));
        await phone.Cloud.StartAgainForTheTestAsync();
        int ledgerReads = cloud.LedgerReads;
        phone.Clock.Passes(TimeSpan.FromDays(2));
        await phone.Cloud.StartAgainForTheTestAsync();
        Assert.Equal(ledgerReads, cloud.LedgerReads);

        // A payment verified on this phone: the copy is stale, and the next merge reads the ledger.
        phone.Cloud.Ask(new PaymentQuestion(new StorePayment("app.coins.large", "GPA.1", "token-1"), Large));
        Assert.IsType<ServerVerdict.Valid>(Assert.Single(phone.Cloud.TakeAnswers()).Verdict);
        Assert.True(phone.States.Load().State.Ledger?.IsStale ?? true);
        phone.Clock.Passes(TimeSpan.FromDays(2));
        await phone.Cloud.StartAgainForTheTestAsync();
        Assert.Equal(ledgerReads + 1, cloud.LedgerReads);
        Assert.False(phone.States.Load().State.Ledger!.IsStale);
    }

    [Fact]
    public async Task Deleting_the_data_deletes_the_save_the_ledger_and_the_account_and_turns_the_switch_off()
    {
        var services = new FakeUnityServices();
        var server = new FakePurchaseServer(services, CloudSupport.Json);
        var phone = new CloudPhone(services, server, start: OnLevelTwentyFive);
        await phone.Cloud.StartAsync(CancellationToken.None);
        CloudPlayerId player = phone.Cloud.Account.Player!.Value;
        await phone.Cloud.UploadAsync(SyncTrigger.PaintingOrRoomCompleted, CancellationToken.None);

        phone.Cloud.Ask(new AccountRequest.DeleteTheCloudData());
        await phone.Cloud.Answering!;

        Assert.False(Assert.IsType<AccountNews.CloudDataDeleted>(Assert.Single(phone.Cloud.TakeNews())).ThereWereNone);
        Assert.DoesNotContain(player, services.Accounts);
        Assert.Equal(1, server.Deletions);
        Assert.False(phone.Cloud.IsSwitchedOn);
        Assert.IsType<Account.Off>(phone.Cloud.Account);
        Assert.Null(phone.States.Load().State.Player);
        Assert.False(phone.Cloud.Verifies);
    }

    [Fact]
    public async Task A_deletion_the_module_cannot_make_leaves_everything_as_it_was()
    {
        var services = new FakeUnityServices();
        var server = new FakePurchaseServer(services, CloudSupport.Json);
        var phone = new CloudPhone(services, server, start: OnLevelTwentyFive);
        await phone.Cloud.StartAsync(CancellationToken.None);
        CloudPlayerId player = phone.Cloud.Account.Player!.Value;
        server.GoesOut(FakeServerOutage.NotDeployed);

        phone.Cloud.Ask(new AccountRequest.DeleteTheCloudData());
        await phone.Cloud.Answering!;

        Assert.Equal(DeletionFailure.Unavailable, Assert.IsType<AccountNews.CloudDataNotDeleted>(Assert.Single(phone.Cloud.TakeNews())).Why);
        Assert.Contains(player, services.Accounts);
        Assert.True(phone.Cloud.IsSwitchedOn);
        Assert.Equal(player, phone.Cloud.Account.Player);
    }

    [Fact]
    public async Task A_phone_whose_switch_is_off_signs_in_to_the_account_it_keeps_for_the_deletion()
    {
        var services = new FakeUnityServices();
        var server = new FakePurchaseServer(services, CloudSupport.Json);
        var phone = new CloudPhone(services, server, start: OnLevelTwentyFive);
        await phone.Cloud.StartAsync(CancellationToken.None);
        CloudPlayerId player = phone.Cloud.Account.Player!.Value;
        await phone.Cloud.TurnTheSwitchAsync(isOn: false, CancellationToken.None);

        phone.Cloud.Ask(new AccountRequest.DeleteTheCloudData());
        await phone.Cloud.Answering!;

        Assert.IsType<AccountNews.CloudDataDeleted>(Assert.Single(phone.Cloud.TakeNews()));
        Assert.DoesNotContain(player, services.Accounts);
    }

    [Fact]
    public async Task A_payment_two_phones_of_the_player_hear_of_is_in_their_merged_progress_once()
    {
        // Two phones of one account (one Play Games player), one store account: phone A pays for the large pack and is
        // closed after the server verified it and before it confirmed it; Google tells phone B of the unconfirmed payment.
        var services = new FakeUnityServices();
        var server = new FakePurchaseServer(services, CloudSupport.Json);
        CloudPlayerId player = services.NewAccount(SignInPlatform.PlayGames, "alice");
        var store = ScriptedStore.AtUsPrices(Catalogue, TestApplicationId, offersRestore: false);

        var a = new CloudPhone(services, server, start: OnLevelTwentyFive, signedInAs: "alice");
        await a.Cloud.StartAsync(CancellationToken.None);
        PlayerProgress before = a.Progress.Current;
        var phoneA = new Shop(before, store, a.Cloud);
        phoneA.Keeper.ClosesBeforeTheConfirmation = true;
        phoneA.Buy(Large);
        phoneA.Step();
        Assert.True(phoneA.Keeper.Progress.Purchases.LedgerDelivered.Contains(new LedgerNumber(1)));

        var b = new CloudPhone(services, server, start: before, signedInAs: "alice");
        await b.Cloud.StartAsync(CancellationToken.None);
        var phoneB = new Shop(before, store, b.Cloud);
        phoneB.Step(); // the store tells of the payment
        phoneB.Step(); // the server's verdict: verified before, as #1
        Assert.True(phoneB.Keeper.Progress.Purchases.LedgerDelivered.Contains(new LedgerNumber(1)));

        // Both phones gave the pack; merged, the pack's coins are counted once.
        PurchaseLedger ledger = PurchaseLedger.Read(CloudSupport.Json.Read<PurchaseLedgerDocument>(services.CloudOf(player).Purchases!.Text)).Ledger;
        MergeOutcome merged = new ProgressMerge(StoreRules).Merge(before, phoneA.Keeper.Progress, phoneB.Keeper.Progress, ledger);
        Assert.Equal(before.Wallet.Coins.Amount + Large.Coins.Amount, Assert.IsType<MergeOutcome.Merged>(merged).Progress.Wallet.Coins.Amount);
        Assert.Equal(player, a.Cloud.Account.Player);
    }

    /// <summary>A server the test answers by hand: it keeps the questions, and answers the oldest unanswered one when told.</summary>
    private sealed class HandServer : IPurchaseServer
    {
        private readonly List<ServerAnswer> _answers = [];
        private readonly Queue<PaymentQuestion> _unanswered = new();

        public bool Verifies { get; set; } = true;

        public StoreBuyer? Buyer { get; } = new StoreBuyer("player-1");

        public List<PaymentQuestion> Questions { get; } = [];

        public void Ask(PaymentQuestion question)
        {
            Questions.Add(question);
            _unanswered.Enqueue(question);
        }

        public void Answers(ServerVerdict verdict) => _answers.Add(new ServerAnswer(_unanswered.Dequeue().Payment, verdict));

        public IReadOnlyList<ServerAnswer> TakeAnswers()
        {
            ServerAnswer[] taken = [.. _answers];
            _answers.Clear();
            return taken;
        }
    }

    /// <summary>A shop of a phone: its purchase's course over a store, a server and a keeper of its progress, on the session's clock.</summary>
    private sealed class Shop
    {
        private TimeSpan _now;

        public Shop(PlayerProgress? start = null, ScriptedStore? store = null, IPurchaseServer? server = null)
        {
            Store = store ?? ScriptedStore.AtUsPrices(Catalogue, TestApplicationId, offersRestore: false);
            Server = server as HandServer ?? new HandServer();
            Keeper = new SavingKeeper(start ?? OnLevelTwentyFive);
            Checkout = new Checkout(Catalogue, RealMoneySales.ThroughTheStore, TestApplicationId, Store);
            Checkout.VerifyWith(server ?? Server);
            Checkout.Open(_now, Keeper);
            Checkout.Step(Keeper);
        }

        public ScriptedStore Store { get; }

        public HandServer Server { get; }

        public SavingKeeper Keeper { get; }

        public Checkout Checkout { get; }

        public void Buy(StoreProduct product)
        {
            Assert.True(Checkout.Ask(product, Keeper.Progress.Purchases, levelNumber: 25));
            Assert.True(Checkout.Pay(_now));
            Step();
        }

        public void Step() => Checkout.Step(Keeper);

        public void PassTime(TimeSpan time)
        {
            _now += time;
            Checkout.PassTime(_now);
            Checkout.Step(Keeper);
        }
    }

    /// <summary>The session's part: gives by the ledger, keeps the progress and saves it; or is closed right after its save.</summary>
    private sealed class SavingKeeper(PlayerProgress start) : IPurchaseKeeper
    {
        private readonly WalletLedger _ledger = new(StoreRules);

        public PlayerProgress Progress { get; private set; } = start;

        public bool ClosesBeforeTheConfirmation { get; set; }

        public ProgressKeeping Keeping => ProgressKeeping.AfterClosing;

        public bool IsSaved => !ClosesBeforeTheConfirmation;

        public bool HasGiven(StorePayment payment) => Progress.Purchases.HasSeen(payment.TransactionId);

        public Delivery Deliver(StoreProduct product, StorePayment payment, DeliveryReason reason, LedgerNumber? number)
        {
            Delivery delivery = _ledger.Deliver(Progress, product, payment.TransactionId, number);
            Progress = delivery switch
            {
                Delivery.Given given => given.Progress,
                Delivery.SoldOut soldOut => soldOut.Progress,
                _ => Progress,
            };
            return delivery;
        }
    }

    /// <summary>A phone's cloud save over the fake services and the fake module: its progress, its cloud.json in memory, its clock.</summary>
    private sealed class CloudPhone
    {
        private int _writes;

        private readonly TaskCompletionSource<bool> _startGate = new();

        public CloudPhone(
            FakeUnityServices services, FakePurchaseServer server, bool switchedOn = true, PlayerProgress? start = null, string? signedInAs = null, bool gatesTheStart = false)
        {
            FakePhoneAccounts accounts = services.Phone();
            Progress = new HeldProgress(start ?? OnLevelTwentyFive);
            States = new CloudStateInMemory(switchedOn ? CloudState.New("writer") : CloudState.Forgotten("writer"));
            Clock = new MovableClock(CloudSupport.Noon);
            var sync = new CloudSync(accounts, States, Progress, StoreRules, CloudSupport.Json, Clock, "1.1.0", () => $"write-{++_writes}");
            IPlatformSignIn platform = signedInAs == null
                ? new FakePlatformSignIn(SignInPlatform.None, player: null, isSignedIn: false)
                : new FakePlatformSignIn(SignInPlatform.PlayGames, signedInAs, isSignedIn: true);
            IAccountService startedHow = gatesTheStart ? new GatedStart(accounts, _startGate.Task) : accounts;
            Cloud = new StartableCloud(sync, startedHow, platform, server.Phone(accounts), Progress);
        }

        /// <summary>Unity's services, which this phone's start waits on, answer now.</summary>
        public void OpenTheStart() => _startGate.SetResult(true);

        public HeldProgress Progress { get; }

        public CloudStateInMemory States { get; }

        public MovableClock Clock { get; }

        public StartableCloud Cloud { get; }
    }

    /// <summary>A phone's services whose start answers only when the test opens it: a sign-in under way.</summary>
    private sealed class GatedStart(FakePhoneAccounts phone, Task gate) : IAccountService
    {
        public bool HasASessionOnThisPhone => phone.HasASessionOnThisPhone;

        public async Task<CloudFailure?> StartAsync(CancellationToken cancel)
        {
            await gate;
            return await phone.StartAsync(cancel);
        }

        public Task<AccountAnswer> SignInAnonymouslyAsync(CancellationToken cancel) => phone.SignInAnonymouslyAsync(cancel);

        public Task<AccountAnswer> SignInWithAsync(PlatformCredential credential, CancellationToken cancel) => phone.SignInWithAsync(credential, cancel);

        public Task<LinkAnswer> LinkWithAsync(PlatformCredential credential, CancellationToken cancel) => phone.LinkWithAsync(credential, cancel);

        public void SignOutKeepingTheSession() => phone.SignOutKeepingTheSession();

        public Task<CloudFailure?> DeleteAccountAsync(CancellationToken cancel) => phone.DeleteAccountAsync(cancel);
    }

    /// <summary>The cloud save of a test phone, which can stand for the game opened again on the same account (a cold start's sync).</summary>
    private sealed class StartableCloud(CloudSync sync, IAccountService accounts, IPlatformSignIn platform, IServerModule module, IProgressHolder holder)
        : IPurchaseServer
    {
        private readonly CloudSaving _cloud = new(sync, accounts, platform, NoRemoteSettings.Instance, module, holder, hasACloudProject: true, _ => { }, failure => throw failure);

        public Account Account => _cloud.Account;

        public bool IsSwitchedOn => _cloud.IsSwitchedOn;

        public Task? Answering => _cloud.Answering;

        public bool Verifies => _cloud.Verifies;

        public StoreBuyer? Buyer => _cloud.Buyer;

        public Task StartAsync(CancellationToken cancel) => _cloud.StartAsync(cancel);

        /// <summary>The cold start's sync again, on the account signed in: what the game's next opening does.</summary>
        public Task<SyncOutcome> StartAgainForTheTestAsync() => sync.SyncAsync(_cloud.Account.Player!.Value, SyncTrigger.ColdStart, CancellationToken.None);

        public Task UploadAsync(SyncTrigger trigger, CancellationToken cancel) => _cloud.UploadAsync(trigger, cancel);

        public Task TurnTheSwitchAsync(bool isOn, CancellationToken cancel) => _cloud.TurnTheSwitchAsync(isOn, cancel);

        public void Ask(AccountRequest request) => _cloud.Ask(request);

        public void Ask(PaymentQuestion question) => _cloud.Ask(question);

        public IReadOnlyList<ServerAnswer> TakeAnswers() => _cloud.TakeAnswers();

        public IReadOnlyList<AccountNews> TakeNews() => _cloud.TakeNews();
    }
}
