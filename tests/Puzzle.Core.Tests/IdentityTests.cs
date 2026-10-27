using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Cloud;

namespace Puzzle.Core.Tests;

/// <summary>
/// The player's identity in the cloud (docs/research/server-v1.md §5), on Unity's Authentication and the platform's
/// sign-in in memory: how a start signs in, with a session on the phone or without, with Google Play Games or without;
/// how the account is linked by hand; what a platform player who has an account of their own does to a link; and how
/// the phone goes over to that account, or back to its own when it cannot.
/// </summary>
public sealed class IdentityTests
{
    private const string Alice = "alice";

    [Fact]
    public async Task A_first_start_on_a_phone_without_the_platform_signs_in_to_a_new_account_of_this_phone()
    {
        var services = new FakeUnityServices();
        var phone = services.Phone();

        Account account = await new PlayerIdentity(phone, new FakePlatformSignIn(SignInPlatform.PlayGames, player: null, isSignedIn: false)).SignInAtStartAsync(CancellationToken.None);

        var anonymous = Assert.IsType<Account.Anonymous>(account);
        Assert.Equal(anonymous.SignedInAs, phone.SignedIn);
        Assert.True(phone.HasASessionOnThisPhone);
        Assert.Equal(SignInPlatform.None, services.PlatformOf(anonymous.SignedInAs));
    }

    [Fact]
    public async Task A_first_start_with_the_platform_signed_in_makes_an_account_linked_to_it()
    {
        var services = new FakeUnityServices();

        Account account = await new PlayerIdentity(services.Phone(), new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true)).SignInAtStartAsync(CancellationToken.None);

        var linked = Assert.IsType<Account.Linked>(account);
        Assert.Equal(SignInPlatform.PlayGames, linked.Platform);
        Assert.Equal(linked.SignedInAs, services.AccountOf(SignInPlatform.PlayGames, Alice));
    }

    [Fact]
    public async Task A_new_phone_signed_in_to_the_platform_finds_the_account_the_old_phone_linked_and_no_other_is_made()
    {
        var services = new FakeUnityServices();
        CloudPlayerId old = services.NewAccount(SignInPlatform.GameCenter, Alice);

        Account account = await new PlayerIdentity(services.Phone(), new FakePlatformSignIn(SignInPlatform.GameCenter, Alice, isSignedIn: true)).SignInAtStartAsync(CancellationToken.None);

        Assert.Equal(old, Assert.IsType<Account.Linked>(account).SignedInAs);
        Assert.Single(services.Accounts);
    }

    [Fact]
    public async Task A_phone_that_keeps_a_session_signs_in_to_its_account_and_asks_the_platform_nothing()
    {
        var services = new FakeUnityServices();
        var phone = services.Phone();
        var platform = new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false);
        var identity = new PlayerIdentity(phone, platform);
        CloudPlayerId first = Assert.IsType<Account.Anonymous>(await identity.SignInAtStartAsync(CancellationToken.None)).SignedInAs;
        int asked = platform.TimesAsked;

        phone.SignOutKeepingTheSession(); // the game is closed and opened again
        Account again = await identity.SignInAtStartAsync(CancellationToken.None);

        Assert.Equal(first, Assert.IsType<Account.Anonymous>(again).SignedInAs);
        Assert.Equal(asked, platform.TimesAsked);
    }

    [Fact]
    public async Task An_account_linked_by_hand_is_found_again_by_a_new_phone_signed_in_to_the_platform()
    {
        var services = new FakeUnityServices();
        var platform = new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false);
        var identity = new PlayerIdentity(services.Phone(), platform);
        CloudPlayerId player = Assert.IsType<Account.Anonymous>(await identity.SignInAtStartAsync(CancellationToken.None)).SignedInAs;

        LinkOutcome linked = await identity.LinkAsync(CancellationToken.None);
        Account onTheNewPhone = await new PlayerIdentity(services.Phone(), new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true)).SignInAtStartAsync(CancellationToken.None);

        Assert.Equal(SignInPlatform.PlayGames, Assert.IsType<LinkOutcome.Linked>(linked).Platform);
        Assert.Equal(player, Assert.IsType<Account.Linked>(onTheNewPhone).SignedInAs);
    }

    [Fact]
    public async Task A_link_to_a_platform_player_who_has_an_account_of_their_own_changes_nothing_and_says_so()
    {
        var services = new FakeUnityServices();
        CloudPlayerId theirs = services.NewAccount(SignInPlatform.PlayGames, Alice);
        var phone = services.Phone();
        var identity = new PlayerIdentity(phone, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false));
        CloudPlayerId mine = Assert.IsType<Account.Anonymous>(await identity.SignInAtStartAsync(CancellationToken.None)).SignedInAs;

        LinkOutcome outcome = await identity.LinkAsync(CancellationToken.None);

        Assert.IsType<LinkOutcome.HeldByAnother>(outcome);
        Assert.Equal(theirs, services.AccountOf(SignInPlatform.PlayGames, Alice));
        Assert.Equal(SignInPlatform.None, services.PlatformOf(mine));
        Assert.Equal(mine, phone.SignedIn);
    }

    [Theory]
    [InlineData(null, false, PlatformRefusal.NotOnThisPhone)]
    [InlineData(Alice, true, PlatformRefusal.Declined)]
    public async Task A_platform_that_is_missing_or_declined_links_nothing(string? platformPlayer, bool declines, PlatformRefusal why)
    {
        var services = new FakeUnityServices();
        var identity = new PlayerIdentity(services.Phone(), new FakePlatformSignIn(SignInPlatform.PlayGames, platformPlayer, isSignedIn: false) { DeclinesByHand = declines });
        CloudPlayerId mine = Assert.IsType<Account.Anonymous>(await identity.SignInAtStartAsync(CancellationToken.None)).SignedInAs;

        LinkOutcome outcome = await identity.LinkAsync(CancellationToken.None);

        Assert.Equal(why, Assert.IsType<LinkOutcome.NotSignedIn>(outcome).Why);
        Assert.Equal(SignInPlatform.None, services.PlatformOf(mine));
    }

    [Fact]
    public async Task Going_over_signs_in_to_the_platform_players_account_and_the_phone_keeps_its_session()
    {
        var services = new FakeUnityServices();
        CloudPlayerId theirs = services.NewAccount(SignInPlatform.PlayGames, Alice);
        var phone = services.Phone();
        var identity = new PlayerIdentity(phone, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false));
        await identity.SignInAtStartAsync(CancellationToken.None);
        await identity.LinkAsync(CancellationToken.None);

        SwitchOutcome outcome = await identity.SwitchToTheLinkedAccountAsync(CancellationToken.None);
        phone.SignOutKeepingTheSession(); // the game is closed and opened again
        Account again = await identity.SignInAtStartAsync(CancellationToken.None);

        Assert.Equal(theirs, Assert.IsType<SwitchOutcome.Switched>(outcome).Account.SignedInAs);
        Assert.Equal(theirs, Assert.IsType<Account.Linked>(again).SignedInAs);
    }

    [Fact]
    public async Task Going_over_that_fails_signs_the_phone_in_again_to_the_account_it_was_on()
    {
        var services = new FakeUnityServices();
        services.NewAccount(SignInPlatform.PlayGames, Alice);
        var phone = services.Phone();
        var identity = new PlayerIdentity(phone, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: false));
        CloudPlayerId mine = Assert.IsType<Account.Anonymous>(await identity.SignInAtStartAsync(CancellationToken.None)).SignedInAs;
        await identity.LinkAsync(CancellationToken.None);
        var stalling = new StallingServices(phone);

        SwitchOutcome outcome = await new PlayerIdentity(stalling, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true)).SwitchToTheLinkedAccountAsync(CancellationToken.None);

        var notSwitched = Assert.IsType<SwitchOutcome.NotSwitched>(outcome);
        Assert.Equal(CloudFailure.Unavailable, notSwitched.Failure);
        Assert.Equal(mine, Assert.IsType<Account.Anonymous>(notSwitched.Account).SignedInAs);
        Assert.Equal(mine, phone.SignedIn);
    }

    [Fact]
    public async Task Services_out_of_reach_leave_the_player_with_no_account_and_the_reason()
    {
        var services = new FakeUnityServices();
        services.GoDown(CloudFailure.NoNetwork);

        Account account = await new PlayerIdentity(services.Phone(), new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true)).SignInAtStartAsync(CancellationToken.None);

        Assert.Equal(CloudFailure.NoNetwork, Assert.IsType<Account.Unreachable>(account).Failure);
        Assert.Empty(services.Accounts);
    }

    [Fact]
    public async Task Unity_refusing_the_platforms_credential_leaves_the_player_on_an_account_of_this_phone()
    {
        var services = new FakeUnityServices();
        var refusing = new RefusingThePlatform(services.Phone());

        Account account = await new PlayerIdentity(refusing, new FakePlatformSignIn(SignInPlatform.PlayGames, Alice, isSignedIn: true)).SignInAtStartAsync(CancellationToken.None);

        Assert.IsType<Account.Anonymous>(account);
    }

    [Fact]
    public void A_credential_never_says_what_it_holds()
    {
        var code = new PlatformCredential.PlayGamesCode("4/0secret");
        var signature = new PlatformCredential.GameCenterSignature("c2lnbmF0dXJl", "T:secret", "https://static.gc.apple.com/public-key/gc-prod-9.cer", "c2FsdA==", 1);

        Assert.DoesNotContain("secret", code.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", signature.ToString(), StringComparison.Ordinal);
        Assert.Equal("Google Play Games", SignInPlatforms.NameOf(SignInPlatform.PlayGames));
        Assert.Equal("Game Center", SignInPlatforms.NameOf(SignInPlatform.GameCenter));
        Assert.Throws<ArgumentOutOfRangeException>(() => SignInPlatforms.NameOf(SignInPlatform.None));
    }

    /// <summary>A phone's services whose sign-in with the platform does not answer (a timeout): the rest as the phone's.</summary>
    private sealed class StallingServices : IAccountService
    {
        private readonly FakePhoneAccounts _phone;

        public StallingServices(FakePhoneAccounts phone)
        {
            _phone = phone;
        }

        public bool HasASessionOnThisPhone => _phone.HasASessionOnThisPhone;

        public Task<CloudFailure?> StartAsync(CancellationToken cancel) => _phone.StartAsync(cancel);

        public Task<AccountAnswer> SignInAnonymouslyAsync(CancellationToken cancel) => _phone.SignInAnonymouslyAsync(cancel);

        public Task<AccountAnswer> SignInWithAsync(PlatformCredential credential, CancellationToken cancel) =>
            Task.FromResult<AccountAnswer>(new AccountAnswer.Failed(CloudFailure.Unavailable));

        public Task<LinkAnswer> LinkWithAsync(PlatformCredential credential, CancellationToken cancel) => _phone.LinkWithAsync(credential, cancel);

        public void SignOutKeepingTheSession() => _phone.SignOutKeepingTheSession();

        public Task<CloudFailure?> DeleteAccountAsync(CancellationToken cancel) => _phone.DeleteAccountAsync(cancel);
    }

    /// <summary>A phone's services on a project with no Play Games provider: Unity refuses the platform's credential.</summary>
    private sealed class RefusingThePlatform : IAccountService
    {
        private readonly FakePhoneAccounts _phone;

        public RefusingThePlatform(FakePhoneAccounts phone)
        {
            _phone = phone;
        }

        public bool HasASessionOnThisPhone => _phone.HasASessionOnThisPhone;

        public Task<CloudFailure?> StartAsync(CancellationToken cancel) => _phone.StartAsync(cancel);

        public Task<AccountAnswer> SignInAnonymouslyAsync(CancellationToken cancel) => _phone.SignInAnonymouslyAsync(cancel);

        public Task<AccountAnswer> SignInWithAsync(PlatformCredential credential, CancellationToken cancel) =>
            Task.FromResult<AccountAnswer>(new AccountAnswer.Failed(CloudFailure.Refused));

        public Task<LinkAnswer> LinkWithAsync(PlatformCredential credential, CancellationToken cancel) =>
            Task.FromResult<LinkAnswer>(new LinkAnswer.Failed(CloudFailure.Refused));

        public void SignOutKeepingTheSession() => _phone.SignOutKeepingTheSession();

        public Task<CloudFailure?> DeleteAccountAsync(CancellationToken cancel) => _phone.DeleteAccountAsync(cancel);
    }
}
