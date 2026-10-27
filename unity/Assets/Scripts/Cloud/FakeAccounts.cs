#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// Unity's Authentication and Cloud Save in memory, as they behave for the players of one game
    /// (docs/research/server-v1.md §5): accounts, each with its save in a cloud of its own (<see cref="FakeCloudSave"/>)
    /// and the platform players linked to it, one of each platform at most, and a platform player linked to one account
    /// at most. Each phone of the tests has its own view (<see cref="Phone"/>), which keeps its session as Unity keeps
    /// it in the PlayerPrefs. The services can be out of reach (no network, down, closed for the free tier). For the
    /// tests and the Editor's checks: nothing here talks to a network.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class FakeUnityServices
    {
        private readonly Dictionary<CloudPlayerId, FakeCloudSave> _clouds = new Dictionary<CloudPlayerId, FakeCloudSave>();
        private readonly Dictionary<(SignInPlatform Platform, string Id), CloudPlayerId> _links = new Dictionary<(SignInPlatform, string), CloudPlayerId>();
        private int _accountsMade;

        /// <summary>Why the services are out of reach; nothing while they answer.</summary>
        public CloudFailure? Down { get; private set; }

        /// <summary>How many times a phone started the services: none while every switch is off.</summary>
        public int Starts { get; private set; }

        /// <summary>The accounts there are.</summary>
        public IReadOnlyCollection<CloudPlayerId> Accounts => _clouds.Keys;

        /// <summary>The services are out of reach for this reason until <see cref="ComeBack"/>.</summary>
        public void GoDown(CloudFailure failure) => Down = failure;

        public void ComeBack() => Down = null;

        /// <summary>A phone of the game: its view of the services, with the session it keeps (none on a new phone).</summary>
        public FakePhoneAccounts Phone() => new FakePhoneAccounts(this);

        /// <summary>The cloud of an account.</summary>
        /// <exception cref="KeyNotFoundException">No such account.</exception>
        public FakeCloudSave CloudOf(CloudPlayerId player) => _clouds[player];

        /// <summary>The account the platform's player is linked to; nothing when none is.</summary>
        public CloudPlayerId? AccountOf(SignInPlatform platform, string platformPlayer) =>
            _links.TryGetValue((platform, platformPlayer), out CloudPlayerId player) ? player : (CloudPlayerId?)null;

        /// <summary>The platform an account is linked to; none for an account of a phone only.</summary>
        public SignInPlatform PlatformOf(CloudPlayerId player)
        {
            foreach (KeyValuePair<(SignInPlatform Platform, string Id), CloudPlayerId> link in _links)
            {
                if (link.Value == player)
                {
                    return link.Key.Platform;
                }
            }

            return SignInPlatform.None;
        }

        /// <summary>A new account, with a cloud of its own; linked to the platform's player when one is given.</summary>
        public CloudPlayerId NewAccount(SignInPlatform platform = SignInPlatform.None, string? platformPlayer = null)
        {
            var player = new CloudPlayerId("player-" + (++_accountsMade).ToString(CultureInfo.InvariantCulture));
            _clouds.Add(player, new FakeCloudSave());
            if (platform != SignInPlatform.None)
            {
                Link(player, platform, platformPlayer ?? throw new ArgumentNullException(nameof(platformPlayer), "A link names the platform's player."));
            }

            return player;
        }

        /// <summary>The account is deleted with its links; its cloud goes with it (the server deleted its data before).</summary>
        internal void Delete(CloudPlayerId player)
        {
            _clouds.Remove(player);
            foreach ((SignInPlatform Platform, string Id) link in _links.Where(link => link.Value == player).Select(link => link.Key).ToArray())
            {
                _links.Remove(link);
            }
        }

        internal Task<CloudFailure?> Started()
        {
            Starts++;
            return Task.FromResult(Down);
        }

        /// <summary>Links the platform's player to the account; false when the player is linked to another already, or the account to another player of the platform.</summary>
        internal bool Link(CloudPlayerId player, SignInPlatform platform, string platformPlayer)
        {
            bool isTaken = _links.ContainsKey((platform, platformPlayer)) || PlatformOf(player) == platform;
            if (isTaken)
            {
                return _links.TryGetValue((platform, platformPlayer), out CloudPlayerId linked) && linked == player;
            }

            _links.Add((platform, platformPlayer), player);
            return true;
        }

        /// <summary>The platform's player a credential proves, as <see cref="FakePlatformSignIn"/> makes them.</summary>
        internal static (SignInPlatform Platform, string Player) WhoProves(PlatformCredential credential) => credential switch
        {
            PlatformCredential.PlayGamesCode code => (SignInPlatform.PlayGames, code.Code.Substring(FakePlatformSignIn.CodePrefix.Length)),
            PlatformCredential.GameCenterSignature signature => (SignInPlatform.GameCenter, signature.TeamPlayerId),
            _ => throw new ArgumentOutOfRangeException(nameof(credential), credential, "A credential the fake services do not know."),
        };
    }

    /// <summary>
    /// One phone's view of <see cref="FakeUnityServices"/>: Unity's Authentication with the session this phone keeps,
    /// and the Cloud Save of the account signed in. Signing in to another account replaces the session, as Unity's does.
    /// </summary>
    public sealed class FakePhoneAccounts : IAccountService, ICloudSave
    {
        private readonly FakeUnityServices _services;

        /// <summary>The account whose session the phone keeps; nothing on a new phone.</summary>
        private CloudPlayerId? _session;

        internal FakePhoneAccounts(FakeUnityServices services)
        {
            _services = services;
        }

        /// <summary>The account signed in; nothing while none is.</summary>
        public CloudPlayerId? SignedIn { get; private set; }

        public bool HasASessionOnThisPhone => _session != null;

        /// <summary>The game is deleted and installed again, and the phone's backup did not bring Unity's session back.</summary>
        public void LosesItsSession()
        {
            _session = null;
            SignedIn = null;
        }

        public Task<CloudFailure?> StartAsync(CancellationToken cancel) => _services.Started();

        public Task<AccountAnswer> SignInAnonymouslyAsync(CancellationToken cancel)
        {
            if (_services.Down is CloudFailure failure)
            {
                return Answer(new AccountAnswer.Failed(failure));
            }

            CloudPlayerId player = _session ?? _services.NewAccount();
            return SignedInTo(player);
        }

        public Task<AccountAnswer> SignInWithAsync(PlatformCredential credential, CancellationToken cancel)
        {
            if (_services.Down is CloudFailure failure)
            {
                return Answer(new AccountAnswer.Failed(failure));
            }

            (SignInPlatform platform, string platformPlayer) = FakeUnityServices.WhoProves(credential);
            CloudPlayerId player = _services.AccountOf(platform, platformPlayer) ?? _services.NewAccount(platform, platformPlayer);
            return SignedInTo(player);
        }

        public Task<LinkAnswer> LinkWithAsync(PlatformCredential credential, CancellationToken cancel)
        {
            if (_services.Down is CloudFailure failure)
            {
                return Task.FromResult<LinkAnswer>(new LinkAnswer.Failed(failure));
            }

            if (!(SignedIn is CloudPlayerId player))
            {
                return Task.FromResult<LinkAnswer>(new LinkAnswer.Failed(CloudFailure.Refused));
            }

            (SignInPlatform platform, string platformPlayer) = FakeUnityServices.WhoProves(credential);
            if (_services.AccountOf(platform, platformPlayer) is CloudPlayerId holder && holder != player)
            {
                return Task.FromResult<LinkAnswer>(new LinkAnswer.HeldByAnother());
            }

            return Task.FromResult<LinkAnswer>(_services.Link(player, platform, platformPlayer) ? new LinkAnswer.Linked() : (LinkAnswer)new LinkAnswer.Failed(CloudFailure.Refused));
        }

        public void SignOutKeepingTheSession() => SignedIn = null;

        public Task<CloudFailure?> DeleteAccountAsync(CancellationToken cancel)
        {
            if (_services.Down is CloudFailure failure)
            {
                return Task.FromResult<CloudFailure?>(failure);
            }

            if (!(SignedIn is CloudPlayerId player))
            {
                return Task.FromResult<CloudFailure?>(CloudFailure.Refused);
            }

            _services.Delete(player);
            _session = null;
            SignedIn = null;
            return Task.FromResult<CloudFailure?>(null);
        }

        public Task<CloudRead> ReadSaveAsync(CancellationToken cancel) =>
            SignedIn is CloudPlayerId player ? _services.CloudOf(player).ReadSaveAsync(cancel) : Task.FromResult<CloudRead>(new CloudRead.Failed(CloudFailure.Blocked));

        public Task<CloudRead> ReadLedgerAsync(CancellationToken cancel) =>
            SignedIn is CloudPlayerId player ? _services.CloudOf(player).ReadLedgerAsync(cancel) : Task.FromResult<CloudRead>(new CloudRead.Failed(CloudFailure.Blocked));

        public Task<CloudWrite> WriteSaveAsync(string text, CloudWriteLock? expected, CancellationToken cancel) =>
            SignedIn is CloudPlayerId player ? _services.CloudOf(player).WriteSaveAsync(text, expected, cancel) : Task.FromResult<CloudWrite>(new CloudWrite.Failed(CloudFailure.Blocked));

        private Task<AccountAnswer> SignedInTo(CloudPlayerId player)
        {
            _session = player;
            SignedIn = player;
            return Answer(new AccountAnswer.SignedIn(player, _services.PlatformOf(player)));
        }

        private static Task<AccountAnswer> Answer(AccountAnswer answer) => Task.FromResult(answer);
    }

    /// <summary>
    /// The platform's sign-in in memory: a phone with or without Google Play Games (or Game Center), its player, whether
    /// that player is signed in already (the quiet sign-in then succeeds), and whether the player declines when asked.
    /// Its credentials name the platform's player, which <see cref="FakeUnityServices"/> reads back.
    /// </summary>
    public sealed class FakePlatformSignIn : IPlatformSignIn
    {
        internal const string CodePrefix = "fake-code:";

        private readonly string? _player;

        /// <param name="player">The platform's player on this phone; nothing for a phone without the platform.</param>
        /// <param name="isSignedIn">Whether that player is signed in to the platform already: the quiet sign-in finds them.</param>
        public FakePlatformSignIn(SignInPlatform platform, string? player, bool isSignedIn)
        {
            Platform = platform;
            _player = player;
            IsSignedIn = isSignedIn && player != null;
        }

        public SignInPlatform Platform { get; }

        /// <summary>Whether the platform's player is signed in on the phone.</summary>
        public bool IsSignedIn { get; private set; }

        /// <summary>The player closes the platform's sheet when asked by hand.</summary>
        public bool DeclinesByHand { get; set; }

        /// <summary>How many times the platform was asked, quietly or by hand.</summary>
        public int TimesAsked { get; private set; }

        public Task<PlatformSignIn> SignInQuietlyAsync(CancellationToken cancel)
        {
            TimesAsked++;
            return Task.FromResult(_player == null ? Refused(PlatformRefusal.NotOnThisPhone) : IsSignedIn ? Credential() : Refused(PlatformRefusal.Declined));
        }

        public Task<PlatformSignIn> SignInByHandAsync(CancellationToken cancel)
        {
            TimesAsked++;
            if (_player == null)
            {
                return Task.FromResult(Refused(PlatformRefusal.NotOnThisPhone));
            }

            if (DeclinesByHand)
            {
                return Task.FromResult(Refused(PlatformRefusal.Declined));
            }

            IsSignedIn = true;
            return Task.FromResult(Credential());
        }

        private PlatformSignIn Credential()
        {
            string player = _player ?? throw new InvalidOperationException("A phone without the platform has no credential.");
            PlatformCredential credential = Platform == SignInPlatform.GameCenter
                ? new PlatformCredential.GameCenterSignature("c2lnbmF0dXJl", player, "https://static.gc.apple.com/public-key/gc-prod-9.cer", "c2FsdA==", 1793700000000UL)
                : (PlatformCredential)new PlatformCredential.PlayGamesCode(CodePrefix + player);
            return new PlatformSignIn.Signed(credential);
        }

        private static PlatformSignIn Refused(PlatformRefusal why) => new PlatformSignIn.NotSignedIn(why);
    }
}
