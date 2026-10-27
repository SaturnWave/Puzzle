using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;

namespace Puzzle.Core.Tests;

/// <summary>
/// The game's cloud save (docs/research/server-v1.md §3–§5) on Unity's services in memory: the switch and a build
/// without a cloud project never start the services; a start signs in and syncs; a new phone gets the save back and is
/// told so; the link is offered once, after a won level, to an account of this phone on a build with the platform's
/// sign-in; "Bağlan" links, or finds the platform player's own save and goes over to it at the player's word; a
/// platform that is missing or declined, and services out of reach, leave the game as it was.
/// </summary>
public sealed class CloudSavingTests
{
    private const string Alice = "alice";

    [Fact]
    public async Task With_the_switch_off_the_services_are_never_started_and_nothing_is_offered()
    {
        var services = new FakeUnityServices();
        var phone = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true), CloudSupport.NewPlayer().Won(3), switchedOn: false);

        await phone.Cloud.StartAsync(CancellationToken.None);
        await phone.Cloud.UploadAsync(SyncTrigger.Background, CancellationToken.None);

        Assert.Equal(ServicesOff.SwitchedOff, Assert.IsType<Account.Off>(phone.Cloud.Account).Why);
        Assert.Equal(0, services.Starts);
        Assert.Empty(services.Accounts);
        Assert.Null(phone.Cloud.OfferTheLink(levelsWon: 3));
    }

    [Fact]
    public async Task A_build_without_a_cloud_project_never_starts_the_services()
    {
        var services = new FakeUnityServices();
        var phone = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true), CloudSupport.NewPlayer().Won(3), hasACloudProject: false);

        await phone.Cloud.StartAsync(CancellationToken.None);

        Assert.Equal(ServicesOff.NoCloudProject, Assert.IsType<Account.Off>(phone.Cloud.Account).Why);
        Assert.Equal(0, services.Starts);
    }

    [Fact]
    public async Task A_start_signs_in_and_uploads_the_progress_of_a_player_who_won_a_level()
    {
        var services = new FakeUnityServices();
        var phone = new Phone(services, NoPlatform(), CloudSupport.NewPlayer().Won(2));

        await phone.Cloud.StartAsync(CancellationToken.None);

        CloudPlayerId player = Assert.IsType<Account.Anonymous>(phone.Cloud.Account).SignedInAs;
        Assert.Equal(2, CloudSupport.ProgressIn(services.CloudOf(player).Save).LevelsWon);
        Assert.Equal(UploadResult.Written, Assert.IsType<SyncOutcome.Done>(phone.Cloud.LastSync).Upload);
        Assert.Empty(phone.Cloud.TakeNews());
    }

    [Fact]
    public async Task A_new_phone_signed_in_to_the_platform_gets_the_save_back_and_the_museum_is_told()
    {
        var services = new FakeUnityServices();
        var old = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true), CloudSupport.NewPlayer().Won(30));
        await old.Cloud.StartAsync(CancellationToken.None);
        var fresh = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true), CloudSupport.NewPlayer());

        await fresh.Cloud.StartAsync(CancellationToken.None);

        Assert.Equal(30, fresh.Progress.Current.LevelsWon);
        Assert.IsType<AccountNews.SaveCameBack>(Assert.Single(fresh.Cloud.TakeNews()));
        Assert.Equal(old.Cloud.Account.Player, fresh.Cloud.Account.Player);
    }

    [Fact]
    public async Task The_link_is_offered_once_after_a_won_level_and_never_again_on_the_installation()
    {
        var services = new FakeUnityServices();
        var phone = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false), CloudSupport.NewPlayer());
        await phone.Cloud.StartAsync(CancellationToken.None);

        SignInPlatform? beforeAWin = phone.Cloud.OfferTheLink(levelsWon: 0);
        SignInPlatform? afterTheFirstWin = phone.Cloud.OfferTheLink(levelsWon: 1);
        SignInPlatform? afterTheSecond = phone.Cloud.OfferTheLink(levelsWon: 2);
        SignInPlatform? afterTheGameOpensAgain = phone.OpenedAgain().Cloud.OfferTheLink(levelsWon: 3);

        Assert.Null(beforeAWin);
        Assert.Equal(SignInPlatform.PlayGames, afterTheFirstWin);
        Assert.Null(afterTheSecond);
        Assert.Null(afterTheGameOpensAgain);
        Assert.True(phone.States.Load().State.LinkOffered);
    }

    [Fact]
    public async Task No_link_is_offered_to_an_account_linked_already_nor_without_the_platform_nor_before_the_sign_in()
    {
        var services = new FakeUnityServices();
        var linked = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true), CloudSupport.NewPlayer().Won(1));
        await linked.Cloud.StartAsync(CancellationToken.None);
        var without = new Phone(services, NoPlatform(), CloudSupport.NewPlayer().Won(1));
        await without.Cloud.StartAsync(CancellationToken.None);
        var notSignedIn = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, "bob", isSignedIn: false), CloudSupport.NewPlayer().Won(1));
        var missing = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, player: null, isSignedIn: false), CloudSupport.NewPlayer().Won(1));
        await missing.Cloud.StartAsync(CancellationToken.None);

        Assert.Null(linked.Cloud.OfferTheLink(levelsWon: 1));
        Assert.Null(without.Cloud.OfferTheLink(levelsWon: 1));
        Assert.Null(notSignedIn.Cloud.OfferTheLink(levelsWon: 1));
        Assert.Null(missing.Cloud.OfferTheLink(levelsWon: 1));
        Assert.False(notSignedIn.States.Load().State.LinkOffered);
    }

    [Fact]
    public async Task Linking_says_so_and_puts_the_save_in_the_cloud_for_the_next_phone()
    {
        var services = new FakeUnityServices();
        var phone = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false), CloudSupport.NewPlayer().Won(1));
        await phone.Cloud.StartAsync(CancellationToken.None);
        phone.Progress.Play(progress => progress.Won(1)); // the budget holds this upload back; the link sends it

        phone.Cloud.Ask(new AccountRequest.Link());
        await phone.Answered();

        var linked = Assert.IsType<Account.Linked>(phone.Cloud.Account);
        Assert.Equal(SignInPlatform.PlayGames, Assert.IsType<AccountNews.Linked>(Assert.Single(phone.Cloud.TakeNews())).Platform);
        Assert.Equal(2, CloudSupport.ProgressIn(services.CloudOf(linked.SignedInAs).Save).LevelsWon);
    }

    [Fact]
    public async Task A_platform_player_with_a_save_of_their_own_is_asked_about_and_going_over_merges_this_phones_progress_into_it()
    {
        var services = new FakeUnityServices();
        var old = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true), CloudSupport.NewPlayer().Won(40));
        await old.Cloud.StartAsync(CancellationToken.None);
        var phone = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false), CloudSupport.NewPlayer().Won(3));
        await phone.Cloud.StartAsync(CancellationToken.None);
        long coinsOnTheOldPhone = old.Progress.Current.Wallet.Coins.Amount;

        phone.Cloud.Ask(new AccountRequest.Link());
        await phone.Answered();
        AccountNews question = Assert.Single(phone.Cloud.TakeNews());
        phone.Cloud.Ask(new AccountRequest.SwitchToTheLinked());
        await phone.Answered();

        Assert.Equal(SignInPlatform.PlayGames, Assert.IsType<AccountNews.OtherSaveFound>(question).Platform);
        Assert.IsType<AccountNews.SaveCameBack>(Assert.Single(phone.Cloud.TakeNews()));
        Assert.Equal(old.Cloud.Account.Player, Assert.IsType<Account.Linked>(phone.Cloud.Account).SignedInAs);
        Assert.Equal(40, phone.Progress.Current.LevelsWon);
        Assert.True(phone.Progress.Current.Wallet.Coins.Amount > coinsOnTheOldPhone, "the coins this phone won are added to the save it went over to");
    }

    [Fact]
    public async Task A_player_who_declines_on_the_platforms_sheet_hears_nothing_and_one_without_the_platform_is_told()
    {
        var services = new FakeUnityServices();
        var declining = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false) { DeclinesByHand = true }, CloudSupport.NewPlayer().Won(1));
        await declining.Cloud.StartAsync(CancellationToken.None);
        var without = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, player: null, isSignedIn: false), CloudSupport.NewPlayer().Won(1));
        await without.Cloud.StartAsync(CancellationToken.None);

        declining.Cloud.Ask(new AccountRequest.Link());
        await declining.Answered();
        without.Cloud.Ask(new AccountRequest.Link());
        await without.Answered();

        Assert.Empty(declining.Cloud.TakeNews());
        Assert.IsType<Account.Anonymous>(declining.Cloud.Account);
        Assert.Equal(LinkFailure.NotOnThisPhone, Assert.IsType<AccountNews.NotLinked>(Assert.Single(without.Cloud.TakeNews())).Why);
    }

    [Fact]
    public async Task Services_out_of_reach_leave_the_progress_as_it_is_and_nothing_is_sent()
    {
        var services = new FakeUnityServices();
        services.GoDown(CloudFailure.Blocked);
        PlayerProgress start = CloudSupport.NewPlayer().Won(5);
        var phone = new Phone(services, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true), start);

        await phone.Cloud.StartAsync(CancellationToken.None);
        await phone.Cloud.UploadAsync(SyncTrigger.PaintingOrRoomCompleted, CancellationToken.None);

        Assert.Equal(CloudFailure.Blocked, Assert.IsType<Account.Unreachable>(phone.Cloud.Account).Failure);
        Assert.Equal(start, phone.Progress.Current);
        Assert.Null(phone.Cloud.LastSync);
        Assert.Null(phone.Cloud.OfferTheLink(levelsWon: 5));
    }

    /// <summary>
    /// The switch off signs out and nothing is sent; on again, the same account is signed in to and what was played while
    /// the switch was off goes at once, ten minutes after the start's upload, the hour's budget notwithstanding (T562).
    /// </summary>
    [Fact]
    public async Task The_switch_turned_off_signs_out_and_stops_the_sync_and_on_again_finds_the_same_account_and_uploads_at_once()
    {
        var services = new FakeUnityServices();
        var phone = new Phone(services, NoPlatform(), CloudSupport.NewPlayer().Won(1));
        await phone.Cloud.StartAsync(CancellationToken.None);
        CloudPlayerId player = phone.Cloud.Account.Player!.Value;
        int writes = services.CloudOf(player).Writes;

        await phone.Cloud.TurnTheSwitchAsync(isOn: false, CancellationToken.None);
        phone.Progress.Play(progress => progress.WithTutorialStepSeen(TutorialStep.Chain).WithCoins(900));
        phone.Clock.Passes(TimeSpan.FromMinutes(10)); // well within the budget of the start's upload
        await phone.Cloud.UploadAsync(SyncTrigger.PaintingOrRoomCompleted, CancellationToken.None);
        bool isOffAfterward = phone.Accounts.SignedIn == null && !phone.States.Load().State.IsSwitchedOn && services.CloudOf(player).Writes == writes;
        await phone.Cloud.TurnTheSwitchAsync(isOn: true, CancellationToken.None);

        Assert.True(isOffAfterward);
        Assert.Equal(player, phone.Cloud.Account.Player);
        Assert.Equal(UploadResult.Written, Assert.IsType<SyncOutcome.Done>(phone.Cloud.LastSync).Upload);
        Assert.Equal(phone.Progress.Current, CloudSupport.ProgressIn(services.CloudOf(player).Save));
    }

    /// <summary>
    /// A module that does not answer "Verilerimi sil" within its deadline (Cloud Code took 17 seconds to say an undeployed
    /// module was not there, 27 October): the card hears that nothing could be deleted now, and the account, its save and
    /// the switch stay as they were.
    /// </summary>
    [Fact]
    public async Task A_deletion_the_module_does_not_answer_in_time_deletes_nothing_and_says_so_calmly()
    {
        var services = new FakeUnityServices();
        var phone = new Phone(services, NoPlatform(), CloudSupport.NewPlayer().Won(1), module: new ModuleWithADeadline(new SilentModule(), TimeSpan.FromMilliseconds(50)));
        await phone.Cloud.StartAsync(CancellationToken.None);
        CloudPlayerId player = phone.Cloud.Account.Player!.Value;

        phone.Cloud.Ask(new AccountRequest.DeleteTheCloudData());
        await phone.Answered();

        var notDeleted = Assert.IsType<AccountNews.CloudDataNotDeleted>(Assert.Single(phone.Cloud.TakeNews()));
        Assert.Equal(DeletionFailure.Unavailable, notDeleted.Why);
        Assert.Contains(player, services.Accounts);
        Assert.True(phone.Cloud.IsSwitchedOn);
        Assert.NotNull(services.CloudOf(player).Save);
    }

    [Fact]
    public async Task A_module_that_answers_in_time_is_heard_through_its_deadline()
    {
        var services = new FakeUnityServices();
        var server = new FakePurchaseServer(services, CloudSupport.Json);
        var phone = new Phone(services, NoPlatform(), CloudSupport.NewPlayer().Won(1), server: server);
        await phone.Cloud.StartAsync(CancellationToken.None);
        var punctual = new ModuleWithADeadline(server.Phone(phone.Accounts), ModuleWithADeadline.GameDeadline);

        Assert.Equal(DataDeletion.Deleted, await punctual.DeleteMyDataAsync(CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(10), ModuleWithADeadline.GameDeadline);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleWithADeadline(server.Phone(phone.Accounts), TimeSpan.Zero));
    }

    private static FakePlatformSignIn NoPlatform() => new FakePlatformSignIn(SignInPlatform.None, player: null, isSignedIn: false);

    /// <summary>A module that never answers a deletion: Cloud Code holding the call.</summary>
    private sealed class SilentModule : IServerModule
    {
        public Task<ServerVerdict> ValidatePurchaseAsync(PaymentQuestion question, CancellationToken cancel) =>
            Task.FromResult<ServerVerdict>(new ServerVerdict.Unavailable("silent"));

        public Task<DataDeletion> DeleteMyDataAsync(CancellationToken cancel) => new TaskCompletionSource<DataDeletion>().Task;
    }

    /// <summary>A phone of the player: its progress, its cloud.json in memory, its view of Unity's services and its cloud save.</summary>
    private sealed class Phone
    {
        private readonly FakePlatformSignIn _platform;
        private readonly bool _hasACloudProject;
        private readonly IServerModule? _module;
        private int _writes;

        /// <param name="module">The module the phone asks; the shared server's, as this phone's account calls it, when none is given.</param>
        public Phone(
            FakeUnityServices services, FakePlatformSignIn platform, PlayerProgress start, bool switchedOn = true, bool hasACloudProject = true, FakePurchaseServer? server = null,
            IServerModule? module = null)
            : this(
                services.Phone(), platform, new HeldProgress(start), new CloudStateInMemory(switchedOn ? CloudState.New("writer") : CloudState.Forgotten("writer")),
                new MovableClock(CloudSupport.Noon), hasACloudProject, server ?? new FakePurchaseServer(services, CloudSupport.Json), module)
        {
        }

        private Phone(
            FakePhoneAccounts accounts, FakePlatformSignIn platform, HeldProgress progress, CloudStateInMemory states, MovableClock clock, bool hasACloudProject, FakePurchaseServer server,
            IServerModule? module)
        {
            _platform = platform;
            _hasACloudProject = hasACloudProject;
            _module = module;
            Server = server;
            Accounts = accounts;
            Progress = progress;
            States = states;
            Clock = clock;
            var sync = new CloudSync(accounts, states, progress, CloudSupport.Rules, CloudSupport.Json, clock, "1.1.0", () => $"write-{++_writes}");
            Cloud = new CloudSaving(sync, accounts, platform, NoRemoteSettings.Instance, module ?? Server.Phone(accounts), progress, hasACloudProject, _ => { }, failure => throw failure);
        }

        public FakePhoneAccounts Accounts { get; }

        /// <summary>The game's server the phone asks, shared by every phone of the test that is given it.</summary>
        public FakePurchaseServer Server { get; }

        public HeldProgress Progress { get; }

        public CloudStateInMemory States { get; }

        public MovableClock Clock { get; }

        public CloudSaving Cloud { get; }

        /// <summary>The game closed and opened again on this phone: the same files, the same session.</summary>
        public Phone OpenedAgain() => new Phone(Accounts, _platform, Progress, States, Clock, _hasACloudProject, Server, _module);

        public Task Answered() => Cloud.Answering ?? Task.CompletedTask;
    }
}
