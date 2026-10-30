#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// The platform's own sign-in the game's account can be linked to (docs/research/server-v1.md §5): Google Play Games
    /// on Android, Game Center on iPhone and iPad, none where the build has neither (the Editor, a build not set up for
    /// it). A closed set.
    /// </summary>
    public enum SignInPlatform
    {
        None,
        PlayGames,
        GameCenter,
    }

    /// <summary>What the game says of the platforms.</summary>
    public static class SignInPlatforms
    {
        /// <summary>The platform's name as the player knows it: a product's name, the same in every language.</summary>
        /// <exception cref="ArgumentOutOfRangeException">No platform: there is nothing to name.</exception>
        public static string NameOf(SignInPlatform platform) => platform switch
        {
            SignInPlatform.PlayGames => "Google Play Games",
            SignInPlatform.GameCenter => "Game Center",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Only a platform with a sign-in has a name to say."),
        };
    }

    /// <summary>
    /// What the platform hands over to prove to Unity's Authentication who the player is there. A closed set: the
    /// private constructor keeps every kind inside this class. Each is used once: Google's server code is spent when
    /// Unity takes it, Game Center's signature is good for ten minutes (§5.2), so a credential is asked for anew for
    /// every sign-in and link. Its text never goes into a log.
    /// </summary>
    public abstract class PlatformCredential
    {
        private PlatformCredential()
        {
        }

        public abstract SignInPlatform Platform { get; }

        /// <summary>Google Play Games' one-time server code (RequestServerSideAccess), for SignInWithGooglePlayGamesAsync and LinkWithGooglePlayGamesAsync.</summary>
        public sealed class PlayGamesCode : PlatformCredential
        {
            /// <exception cref="ArgumentException">An empty code: Google never hands one out.</exception>
            public PlayGamesCode(string code)
            {
                Code = string.IsNullOrEmpty(code) ? throw new ArgumentException("A server code is not empty.", nameof(code)) : code;
            }

            public string Code { get; }

            public override SignInPlatform Platform => SignInPlatform.PlayGames;

            public override string ToString() => "a Play Games server code";
        }

        /// <summary>Game Center's identity verification (GKLocalPlayer.FetchItems), for SignInWithAppleGameCenterAsync and LinkWithAppleGameCenterAsync.</summary>
        public sealed class GameCenterSignature : PlatformCredential
        {
            /// <param name="signature">The signature, in base 64.</param>
            /// <param name="teamPlayerId">The player's id for the team's games: given to Unity only, never shown (Apple's rule 4.5.5, §5.2).</param>
            /// <param name="salt">The salt, in base 64.</param>
            /// <param name="timestamp">When Apple signed, in milliseconds since 1970.</param>
            /// <exception cref="ArgumentException">A part is empty.</exception>
            public GameCenterSignature(string signature, string teamPlayerId, string publicKeyUrl, string salt, ulong timestamp)
            {
                Signature = Required(signature, nameof(signature));
                TeamPlayerId = Required(teamPlayerId, nameof(teamPlayerId));
                PublicKeyUrl = Required(publicKeyUrl, nameof(publicKeyUrl));
                Salt = Required(salt, nameof(salt));
                Timestamp = timestamp;
            }

            public string Signature { get; }

            public string TeamPlayerId { get; }

            public string PublicKeyUrl { get; }

            public string Salt { get; }

            public ulong Timestamp { get; }

            public override SignInPlatform Platform => SignInPlatform.GameCenter;

            public override string ToString() => "a Game Center signature";

            private static string Required(string part, string name) =>
                string.IsNullOrEmpty(part) ? throw new ArgumentException("A part of Game Center's signature is not empty.", name) : part;
        }
    }

    /// <summary>Why the platform did not sign the player in. A closed set.</summary>
    public enum PlatformRefusal
    {
        /// <summary>No such sign-in on this phone: no Play Games, Game Center turned off, or a build not set up for it (no game id of the platform).</summary>
        NotOnThisPhone,

        /// <summary>The player said no on the platform's own sheet, or closed it: the game asks no further.</summary>
        Declined,

        /// <summary>The platform failed: no network, its service out of reach, or an error of its own. The log says which.</summary>
        Failed,
    }

    /// <summary>What asking the platform to sign the player in came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class PlatformSignIn
    {
        private PlatformSignIn()
        {
        }

        /// <summary>Signed in: the credential for Unity's Authentication.</summary>
        public sealed class Signed : PlatformSignIn
        {
            public Signed(PlatformCredential credential)
            {
                Credential = credential ?? throw new ArgumentNullException(nameof(credential));
            }

            public PlatformCredential Credential { get; }

            public override string ToString() => $"signed in, with {Credential}";
        }

        public sealed class NotSignedIn : PlatformSignIn
        {
            public NotSignedIn(PlatformRefusal why)
            {
                Why = why;
            }

            public PlatformRefusal Why { get; }

            public override string ToString() => $"not signed in: {Why}";
        }
    }

    /// <summary>
    /// The platform's own sign-in (Google Play Games v2, Game Center), as the identity needs it. It is a seam: the game's
    /// adapters talk to Google's plugin and Apple's GameKit, the tests and the Editor's checks use one in memory
    /// (<see cref="FakePlatformSignIn"/>). An adapter turns every error of the platform into a <see cref="PlatformRefusal"/>:
    /// nothing of the platform's own errors goes further.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public interface IPlatformSignIn
    {
        /// <summary>Which platform this is; <see cref="SignInPlatform.None"/> for a build without one, which never signs in.</summary>
        SignInPlatform Platform { get; }

        /// <summary>
        /// The sign-in that asks nothing of the player, as the game starts: Play Games v2 signs a player in who used it
        /// before on this phone; Game Center shows Apple's own sheet once, the first time. Also how a fresh credential is
        /// had when the platform signed the player in a moment ago.
        /// </summary>
        Task<PlatformSignIn> SignInQuietlyAsync(CancellationToken cancel);

        /// <summary>The sign-in the player asked for ("Bağlan"): the platform may show its own sheet.</summary>
        Task<PlatformSignIn> SignInByHandAsync(CancellationToken cancel);
    }

    /// <summary>A build without the platform's sign-in: it never signs in, and no offer to link is made.</summary>
    public sealed class NoPlatformSignIn : IPlatformSignIn
    {
        private static readonly Task<PlatformSignIn> NotHere = Task.FromResult<PlatformSignIn>(new PlatformSignIn.NotSignedIn(PlatformRefusal.NotOnThisPhone));

        private NoPlatformSignIn()
        {
        }

        public static NoPlatformSignIn Instance { get; } = new NoPlatformSignIn();

        public SignInPlatform Platform => SignInPlatform.None;

        public Task<PlatformSignIn> SignInQuietlyAsync(CancellationToken cancel) => NotHere;

        public Task<PlatformSignIn> SignInByHandAsync(CancellationToken cancel) => NotHere;
    }

    /// <summary>What signing in to the game's account came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class AccountAnswer
    {
        private AccountAnswer()
        {
        }

        /// <summary>Signed in to this account, which is linked to this platform (none for an account of this phone only).</summary>
        public sealed class SignedIn : AccountAnswer
        {
            public SignedIn(CloudPlayerId player, SignInPlatform linkedTo)
            {
                Player = player;
                LinkedTo = linkedTo;
            }

            public CloudPlayerId Player { get; }

            public SignInPlatform LinkedTo { get; }

            public override string ToString() => LinkedTo == SignInPlatform.None ? $"signed in to {Player}" : $"signed in to {Player}, linked to {LinkedTo}";
        }

        public sealed class Failed : AccountAnswer
        {
            public Failed(CloudFailure failure)
            {
                Failure = failure;
            }

            public CloudFailure Failure { get; }

            public override string ToString() => $"not signed in: {Failure}";
        }
    }

    /// <summary>What linking the account to the platform came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class LinkAnswer
    {
        private LinkAnswer()
        {
        }

        public sealed class Linked : LinkAnswer
        {
            public override string ToString() => "linked";
        }

        /// <summary>The platform's player is linked to another account already (Unity's AccountAlreadyLinked): nothing was changed.</summary>
        public sealed class HeldByAnother : LinkAnswer
        {
            public override string ToString() => "held by another account";
        }

        public sealed class Failed : LinkAnswer
        {
            public Failed(CloudFailure failure)
            {
                Failure = failure;
            }

            public CloudFailure Failure { get; }

            public override string ToString() => $"not linked: {Failure}";
        }
    }

    /// <summary>
    /// Unity's services and their Authentication (docs/research/server-v1.md §5), as the identity needs them: start the
    /// services, sign in to the account of this phone or to the platform's, link the platform to the account, sign out.
    /// It is a seam: the game's adapter talks to Unity's packages, the tests and the Editor's checks use accounts in
    /// memory (<see cref="FakeUnityServices"/>). An adapter turns every exception of the services into a
    /// <see cref="CloudFailure"/>: nothing of their own errors goes further, and none reaches a player. Nothing is
    /// asked of the services before <see cref="StartAsync"/>; the game calls it only with the player's switch on and a
    /// cloud project (<see cref="SyncPolicy.MayStartServices"/>).
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>Starts Unity's services (once: a later call answers at once); nothing when they started, else why not.</summary>
        Task<CloudFailure?> StartAsync(CancellationToken cancel);

        /// <summary>True when this phone keeps the session of an account it signed in to before (Unity keeps it in the PlayerPrefs).</summary>
        bool HasASessionOnThisPhone { get; }

        /// <summary>Signs in to the account whose session this phone keeps, linked or not; to a new account of this phone when it keeps none.</summary>
        Task<AccountAnswer> SignInAnonymouslyAsync(CancellationToken cancel);

        /// <summary>Signs in to the account the platform's player is linked to; to a new one linked to it when there is none.</summary>
        Task<AccountAnswer> SignInWithAsync(PlatformCredential credential, CancellationToken cancel);

        /// <summary>Links the platform's player to the account signed in.</summary>
        Task<LinkAnswer> LinkWithAsync(PlatformCredential credential, CancellationToken cancel);

        /// <summary>
        /// Signs out and keeps the session on the phone, so the account can be signed in to again; no token is refreshed
        /// meanwhile. A sign-in to another account replaces the session.
        /// </summary>
        void SignOutKeepingTheSession();

        /// <summary>
        /// Deletes the account signed in, for good, and forgets its session on the phone (docs/research/server-v1.md §5.6:
        /// only after the server deleted its data, which deleting the account does not). Nothing when it is deleted, else why not.
        /// </summary>
        Task<CloudFailure?> DeleteAccountAsync(CancellationToken cancel);
    }

    /// <summary>Why Unity's services do not run at all. A closed set.</summary>
    public enum ServicesOff
    {
        /// <summary>The player's "Bulut kaydı" switch is off: the services are never started (the owner's decision 1).</summary>
        SwitchedOff,

        /// <summary>The build has no Unity Cloud project to talk to (none is linked yet), or it is the Editor, which starts none.</summary>
        NoCloudProject,
    }

    /// <summary>
    /// Where the player's account stands. A closed set (the plan's identity: none, an account of this phone, one linked to
    /// Google Play Games or to Game Center); the private constructor keeps every kind inside this class.
    /// </summary>
    public abstract class Account
    {
        private Account()
        {
        }

        /// <summary>The account the cloud keeps the player's items under; nothing while none is signed in.</summary>
        public virtual CloudPlayerId? Player => null;

        /// <summary>The services do not run, and nothing reaches Unity.</summary>
        public sealed class Off : Account
        {
            public Off(ServicesOff why)
            {
                Why = why;
            }

            public ServicesOff Why { get; }

            public override string ToString() => $"off: {Why}";
        }

        /// <summary>The services start, or the sign-in runs.</summary>
        public sealed class Starting : Account
        {
            public override string ToString() => "starting";
        }

        /// <summary>The services or the sign-in could not be reached this run; the game plays on as without them.</summary>
        public sealed class Unreachable : Account
        {
            public Unreachable(CloudFailure failure)
            {
                Failure = failure;
            }

            public CloudFailure Failure { get; }

            public override string ToString() => $"unreachable: {Failure}";
        }

        /// <summary>Signed in to an account of this phone only: no platform is linked to it.</summary>
        public sealed class Anonymous : Account
        {
            public Anonymous(CloudPlayerId player)
            {
                SignedInAs = player;
            }

            public CloudPlayerId SignedInAs { get; }

            public override CloudPlayerId? Player => SignedInAs;

            public override string ToString() => $"anonymous {SignedInAs}";
        }

        /// <summary>Signed in to an account linked to the platform: a new phone signed in to the platform finds it again.</summary>
        public sealed class Linked : Account
        {
            /// <exception cref="ArgumentException">No platform: an account linked to none is an anonymous one.</exception>
            public Linked(CloudPlayerId player, SignInPlatform platform)
            {
                SignedInAs = player;
                Platform = platform == SignInPlatform.None ? throw new ArgumentException("A linked account is linked to a platform.", nameof(platform)) : platform;
            }

            public CloudPlayerId SignedInAs { get; }

            public SignInPlatform Platform { get; }

            public override CloudPlayerId? Player => SignedInAs;

            public override string ToString() => $"{SignedInAs}, linked to {Platform}";
        }

        /// <summary>The account a sign-in came to: linked or of this phone; unreachable when it failed.</summary>
        public static Account Of(AccountAnswer answer) => answer switch
        {
            AccountAnswer.SignedIn { LinkedTo: SignInPlatform.None } signed => new Anonymous(signed.Player),
            AccountAnswer.SignedIn signed => new Linked(signed.Player, signed.LinkedTo),
            AccountAnswer.Failed failed => new Unreachable(failed.Failure),
            _ => throw new ArgumentOutOfRangeException(nameof(answer), answer, "A sign-in ended in a way the identity does not know."),
        };
    }

    /// <summary>What linking the account came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class LinkOutcome
    {
        private LinkOutcome()
        {
        }

        public sealed class Linked : LinkOutcome
        {
            public Linked(SignInPlatform platform)
            {
                Platform = platform;
            }

            public SignInPlatform Platform { get; }

            public override string ToString() => $"linked to {Platform}";
        }

        /// <summary>The platform's player has an account of their own already: the player is asked whether to go over to it (§3.6).</summary>
        public sealed class HeldByAnother : LinkOutcome
        {
            public HeldByAnother(SignInPlatform platform)
            {
                Platform = platform;
            }

            public SignInPlatform Platform { get; }

            public override string ToString() => $"the {Platform} player has another account";
        }

        /// <summary>The platform did not sign the player in.</summary>
        public sealed class NotSignedIn : LinkOutcome
        {
            public NotSignedIn(PlatformRefusal why)
            {
                Why = why;
            }

            public PlatformRefusal Why { get; }

            public override string ToString() => $"the platform did not sign in: {Why}";
        }

        /// <summary>Unity's Authentication could not be reached, or refused the link.</summary>
        public sealed class Unreachable : LinkOutcome
        {
            public Unreachable(CloudFailure failure)
            {
                Failure = failure;
            }

            public CloudFailure Failure { get; }

            public override string ToString() => $"not linked: {Failure}";
        }
    }

    /// <summary>What going over to the platform's account came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class SwitchOutcome
    {
        private SwitchOutcome()
        {
        }

        /// <summary>The phone is signed in to the platform's account now.</summary>
        public sealed class Switched : SwitchOutcome
        {
            public Switched(Account.Linked account)
            {
                Account = account ?? throw new ArgumentNullException(nameof(account));
            }

            public Account.Linked Account { get; }

            public override string ToString() => $"switched to {Account}";
        }

        /// <summary>The platform did not sign the player in: nothing changed.</summary>
        public sealed class NotSignedIn : SwitchOutcome
        {
            public NotSignedIn(PlatformRefusal why)
            {
                Why = why;
            }

            public PlatformRefusal Why { get; }

            public override string ToString() => $"the platform did not sign in: {Why}";
        }

        /// <summary>The platform's account could not be signed in to: the phone signed in again to the account it was on, or could not reach either.</summary>
        public sealed class NotSwitched : SwitchOutcome
        {
            public NotSwitched(CloudFailure failure, Account account)
            {
                Failure = failure;
                Account = account ?? throw new ArgumentNullException(nameof(account));
            }

            public CloudFailure Failure { get; }

            /// <summary>Where the phone stands after it: the account it was on, or unreachable.</summary>
            public Account Account { get; }

            public override string ToString() => $"not switched ({Failure}); {Account}";
        }
    }

    /// <summary>
    /// The player's identity in the cloud (docs/research/server-v1.md §5): how the game signs in as it starts, links the
    /// account to the platform at the player's wish, and goes over to the platform's own account when it has one.
    /// <list type="bullet">
    /// <item>At the start (§5.1, §5.2) a phone that keeps the session of an account signs in to it, linked or not, and asks
    /// the platform nothing (a Google account changed on the phone does not change the game's). A phone that keeps none
    /// (a first start, a new phone, the game installed again) asks the platform quietly: signed in there, it signs in to
    /// the platform's account, which brings the player's save back on a new phone, or to a new one linked to it. When
    /// the platform has no player, the player declines, or Unity refuses the platform's credential, it signs in to a new
    /// account of this phone.</item>
    /// <item>Linking (§5.3) asks the platform by hand; a platform player who has an account of their own already is not
    /// linked: the game asks whether to go over to that account.</item>
    /// <item>Going over (§3.6) signs out of the account of this phone, keeping its session, and signs in to the
    /// platform's; when that fails, the phone signs in again to the account it was on, so nothing is lost.</item>
    /// </list>
    /// Nothing here is Unity's: Unity's Authentication and the platform come in through seams.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class PlayerIdentity
    {
        private readonly IAccountService _accounts;
        private readonly IPlatformSignIn _platform;

        public PlayerIdentity(IAccountService accounts, IPlatformSignIn platform)
        {
            _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>The platform this build links accounts to; none where it has no sign-in.</summary>
        public SignInPlatform Platform => _platform.Platform;

        /// <summary>
        /// True once the start's quiet sign-in found the platform's sign-in missing on this phone (no Play Games, Game
        /// Center turned off): the link is not offered there. A start that signed in with the session the phone keeps
        /// asks the platform nothing, and finds nothing missing.
        /// </summary>
        public bool FoundThePlatformMissing { get; private set; }

        /// <summary>Signs in as the game starts. Unity's services have started.</summary>
        public async Task<Account> SignInAtStartAsync(CancellationToken cancel)
        {
            if (_accounts.HasASessionOnThisPhone)
            {
                return Account.Of(await _accounts.SignInAnonymouslyAsync(cancel));
            }

            PlatformSignIn quietly = await _platform.SignInQuietlyAsync(cancel);
            FoundThePlatformMissing = quietly is PlatformSignIn.NotSignedIn { Why: PlatformRefusal.NotOnThisPhone };
            if (quietly is PlatformSignIn.Signed signed)
            {
                AccountAnswer answer = await _accounts.SignInWithAsync(signed.Credential, cancel);
                bool isRefusedByUnity = answer is AccountAnswer.Failed { Failure: CloudFailure.Refused } || answer is AccountAnswer.Failed { Failure: CloudFailure.Blocked };
                if (!isRefusedByUnity)
                {
                    return Account.Of(answer);
                }
            }

            return Account.Of(await _accounts.SignInAnonymouslyAsync(cancel));
        }

        /// <summary>Links the account signed in, an account of this phone, to the platform, which the player is asked to sign in to.</summary>
        public async Task<LinkOutcome> LinkAsync(CancellationToken cancel)
        {
            PlatformSignIn signIn = await _platform.SignInByHandAsync(cancel);
            if (signIn is PlatformSignIn.NotSignedIn refused)
            {
                return new LinkOutcome.NotSignedIn(refused.Why);
            }

            PlatformCredential credential = ((PlatformSignIn.Signed)signIn).Credential;
            return await _accounts.LinkWithAsync(credential, cancel) switch
            {
                LinkAnswer.Linked _ => new LinkOutcome.Linked(credential.Platform),
                LinkAnswer.HeldByAnother _ => new LinkOutcome.HeldByAnother(credential.Platform),
                LinkAnswer.Failed failed => new LinkOutcome.Unreachable(failed.Failure),
                _ => throw new InvalidOperationException("A link ended in a way the identity does not know."),
            };
        }

        /// <summary>
        /// Goes over from the account of this phone to the one the platform's player has: the platform signed the player
        /// in a moment ago, so it is asked quietly for a fresh credential.
        /// </summary>
        public async Task<SwitchOutcome> SwitchToTheLinkedAccountAsync(CancellationToken cancel)
        {
            PlatformSignIn signIn = await _platform.SignInQuietlyAsync(cancel);
            if (signIn is PlatformSignIn.NotSignedIn refused)
            {
                return new SwitchOutcome.NotSignedIn(refused.Why);
            }

            _accounts.SignOutKeepingTheSession();
            switch (await _accounts.SignInWithAsync(((PlatformSignIn.Signed)signIn).Credential, cancel))
            {
                case AccountAnswer.SignedIn { LinkedTo: SignInPlatform.None } signed:
                    throw new InvalidOperationException($"Signing in with the platform came to {signed.Player}, an account linked to no platform.");
                case AccountAnswer.SignedIn signed:
                    return new SwitchOutcome.Switched(new Account.Linked(signed.Player, signed.LinkedTo));
                case AccountAnswer.Failed failed:
                    return new SwitchOutcome.NotSwitched(failed.Failure, Account.Of(await _accounts.SignInAnonymouslyAsync(cancel)));
                default:
                    throw new InvalidOperationException("A sign-in ended in a way the identity does not know.");
            }
        }
    }
}
