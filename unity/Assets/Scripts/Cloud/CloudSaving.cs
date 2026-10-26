#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Flow;

namespace Puzzle.Client.Cloud
{
    /// <summary>What the player asked of the account on a card in the museum. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class AccountRequest
    {
        private AccountRequest()
        {
        }

        /// <summary>"Bağlan" on the offer to link: the account is linked to the platform, which the player signs in to.</summary>
        public sealed class Link : AccountRequest
        {
            public override string ToString() => "link the account";
        }

        /// <summary>"Geç" on the question of the other save: the phone goes over to the platform player's own account.</summary>
        public sealed class SwitchToTheLinked : AccountRequest
        {
            public override string ToString() => "go over to the platform's account";
        }
    }

    /// <summary>Why linking did not come about, as the card says it. A closed set.</summary>
    public enum LinkFailure
    {
        /// <summary>The platform's sign-in is not on this phone.</summary>
        NotOnThisPhone,

        /// <summary>The platform or Unity could not be reached, or refused.</summary>
        Unreachable,
    }

    /// <summary>What the account has to tell the player, on a card in the museum. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class AccountNews
    {
        private AccountNews()
        {
        }

        /// <summary>The account is linked to the platform: a new phone signed in to it finds the save again.</summary>
        public sealed class Linked : AccountNews
        {
            public Linked(SignInPlatform platform)
            {
                Platform = platform;
            }

            public SignInPlatform Platform { get; }

            public override string ToString() => $"linked to {Platform}";
        }

        /// <summary>The account was not linked; the player plays on as before. No card says it when the player declined on the platform's own sheet.</summary>
        public sealed class NotLinked : AccountNews
        {
            public NotLinked(SignInPlatform platform, LinkFailure why)
            {
                Platform = platform;
                Why = why;
            }

            public SignInPlatform Platform { get; }

            public LinkFailure Why { get; }

            public override string ToString() => $"not linked to {Platform}: {Why}";
        }

        /// <summary>The platform's player has a save of their own: the card asks whether to go over to it (§3.6).</summary>
        public sealed class OtherSaveFound : AccountNews
        {
            public OtherSaveFound(SignInPlatform platform)
            {
                Platform = platform;
            }

            public SignInPlatform Platform { get; }

            public override string ToString() => $"the {Platform} player has a save of their own";
        }

        /// <summary>The cloud's save came back to this phone (a new phone, the game installed again, an account gone over to): the card says where the player stands.</summary>
        public sealed class SaveCameBack : AccountNews
        {
            public override string ToString() => "the save came back";
        }

        /// <summary>Going over to the platform's account failed: the player is on the account they were on.</summary>
        public sealed class NotSwitched : AccountNews
        {
            public override string ToString() => "not switched";
        }
    }

    /// <summary>
    /// What the game's session asks of the cloud and hears back: the offer to link the account and the answers to its
    /// cards, in the museum, and the uploads its moments ask for. It is the seam between the session and the cloud: the
    /// game's is <see cref="CloudSaving"/>, the Editor's tools have <see cref="NoCloudDesk"/>.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public interface ICloudDesk
    {
        /// <summary>
        /// The platform to offer the link to, now: once ever on the installation, and only after a won level, to a
        /// player signed in to an account of this phone on a build with the platform's sign-in, on a phone where the
        /// start did not find it missing (the owner's decision 2, 2026-10-26). The offer counts as made from the moment
        /// this answers with a platform. Nothing otherwise.
        /// </summary>
        SignInPlatform? OfferTheLink(int levelsWon);

        /// <summary>The player answered a card: it is done in the background, and what came of it is news.</summary>
        void Ask(AccountRequest request);

        /// <summary>What the account has to tell since this was asked last, in order, each once.</summary>
        IReadOnlyList<AccountNews> TakeNews();

        /// <summary>A moment of the game asks for an upload (a painting or a room completed, a payment delivered): it is done in the background, as the sync's budget says.</summary>
        void AskForAnUpload(SyncTrigger trigger);
    }

    /// <summary>The Editor's tools' desk: no account, no offer, no news, no upload.</summary>
    public sealed class NoCloudDesk : ICloudDesk
    {
        private NoCloudDesk()
        {
        }

        public static NoCloudDesk Instance { get; } = new NoCloudDesk();

        public SignInPlatform? OfferTheLink(int levelsWon) => null;

        public void Ask(AccountRequest request)
        {
        }

        public IReadOnlyList<AccountNews> TakeNews() => Array.Empty<AccountNews>();

        public void AskForAnUpload(SyncTrigger trigger)
        {
        }
    }

    /// <summary>What Remote Config says of the sync (the four keys, <see cref="RemoteConfigKeys"/>). It is a seam: the game's adapter fetches it, the tests have none.</summary>
    public interface IRemoteSettings
    {
        /// <summary>The settings Remote Config gives; nothing when it could not be fetched, and the sync keeps what it has.</summary>
        Task<SyncSettings?> FetchAsync(CancellationToken cancel);
    }

    /// <summary>No Remote Config: the sync keeps its defaults.</summary>
    public sealed class NoRemoteSettings : IRemoteSettings
    {
        private NoRemoteSettings()
        {
        }

        public static NoRemoteSettings Instance { get; } = new NoRemoteSettings();

        public Task<SyncSettings?> FetchAsync(CancellationToken cancel) => Task.FromResult<SyncSettings?>(null);
    }

    /// <summary>
    /// The game's cloud save (docs/research/server-v1.md §3–§5): the player's "Bulut kaydı" switch, Unity's services, the
    /// account and its sync, in one place the game's root runs in the background. It never holds the game up: the game
    /// plays on its progress file from the first frame, and whatever of the cloud fails is only written to the log.
    /// <list type="bullet">
    /// <item>With the switch off, or in a build without a cloud project, Unity's services are never started
    /// (<see cref="SyncPolicy.MayStartServices"/>): no account, no sync, nothing sent, the game as before the server.</item>
    /// <item>Otherwise, as the game starts, the services start, the player signs in (<see cref="PlayerIdentity"/>), Remote
    /// Config is read, and the sync runs for the start (<see cref="CloudSync"/>): a new phone signed in to the account of
    /// its platform gets the save back, and the museum is told so.</item>
    /// <item>The game's other moments ask for uploads (the background, a painting or a room completed, a payment); the
    /// sync's budget decides what goes.</item>
    /// <item>Once, after a won level, an account of this phone is offered the link to the platform. "Bağlan" links it, or,
    /// when the platform's player has an account of their own, asks whether to go over to it; going over merges this
    /// phone's progress into that account's save. The account left behind is not deleted (that waits for "Verilerimi
    /// sil", Stage 5).</item>
    /// <item>Turning the switch off signs out and stops every sync at once; turning it on starts the services, if this
    /// run had not started them.</item>
    /// </list>
    /// Nothing here is Unity's: the services, the platform, Remote Config and the cloud's save come in through seams, and
    /// the tests and the Editor's CloudCheck run every case on <see cref="FakeUnityServices"/>.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class CloudSaving : ICloudDesk
    {
        private readonly CloudSync _sync;
        private readonly IAccountService _accounts;
        private readonly PlayerIdentity _identity;
        private readonly IRemoteSettings _remote;
        private readonly IProgressHolder _holder;
        private readonly bool _hasACloudProject;
        private readonly Action<string> _log;
        private readonly Action<Exception> _fault;
        private readonly List<AccountNews> _news = new List<AccountNews>();

        /// <summary>True while a link or a going over runs: a second press waits for none and is let go.</summary>
        private bool _isAnsweringTheCard;

        /// <param name="hasACloudProject">Whether the build has a Unity Cloud project to talk to (false in the Editor, which starts none).</param>
        /// <param name="log">Where what the cloud came to is written: the device's log.</param>
        /// <param name="fault">Where a fault of the game's own code in the background goes: the device's log as an exception.</param>
        public CloudSaving(
            CloudSync sync, IAccountService accounts, IPlatformSignIn platform, IRemoteSettings remote, IProgressHolder holder, bool hasACloudProject,
            Action<string> log, Action<Exception> fault)
        {
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
            _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            _identity = new PlayerIdentity(accounts, platform ?? throw new ArgumentNullException(nameof(platform)));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _holder = holder ?? throw new ArgumentNullException(nameof(holder));
            _hasACloudProject = hasACloudProject;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _fault = fault ?? throw new ArgumentNullException(nameof(fault));
            Account = WhyOff() is ServicesOff off ? new Account.Off(off) : (Account)new Account.Starting();
        }

        /// <summary>Where the account stands now.</summary>
        public Account Account { get; private set; }

        /// <summary>What the last sync came to; nothing before the first.</summary>
        public SyncOutcome? LastSync { get; private set; }

        /// <summary>Whether the player's "Bulut kaydı" switch is on.</summary>
        public bool IsSwitchedOn => _sync.State.IsSwitchedOn;

        /// <summary>The work a press on a card started, for the Editor's check to wait on; nothing before the first.</summary>
        public Task? Answering { get; private set; }

        /// <summary>
        /// Starts the services, signs in and syncs for the game's start, unless the switch or the build keeps the services
        /// off. A run that signed in already does nothing.
        /// </summary>
        public async Task StartAsync(CancellationToken cancel)
        {
            if (WhyOff() is ServicesOff off)
            {
                Account = new Account.Off(off);
                _log($"Cloud save: Unity's services are not started ({off}); the game plays on this phone alone.");
                return;
            }

            if (Account.Player != null)
            {
                return;
            }

            Account = new Account.Starting();
            if (await _accounts.StartAsync(cancel) is CloudFailure failure)
            {
                Account = new Account.Unreachable(failure);
                _log($"Cloud save: Unity's services did not start ({failure}); the game plays on this phone alone.");
                return;
            }

            Account = await _identity.SignInAtStartAsync(cancel);
            _log($"Cloud save: the account is {Account} (Play Games / Game Center of this build: {_identity.Platform}).");
            if (!(Account.Player is CloudPlayerId player))
            {
                return;
            }

            if (await _remote.FetchAsync(cancel) is SyncSettings settings)
            {
                _sync.Settings = settings;
                _log($"Cloud save: Remote Config says the sync {settings}.");
            }

            await SyncTellingWhenTheSaveCameBackAsync(player, SyncTrigger.ColdStart, cancel);
        }

        /// <summary>Asks the sync to upload for this moment of the game; nothing while no account is signed in.</summary>
        public async Task UploadAsync(SyncTrigger trigger, CancellationToken cancel)
        {
            if (Account.Player is CloudPlayerId player)
            {
                await SyncAsync(player, trigger, cancel);
            }
        }

        /// <summary>
        /// The player turned the "Bulut kaydı" switch: kept at once. Off signs out (no token is refreshed, nothing is sent
        /// from now on; the session stays on the phone, so on again finds the same account). On starts the services and
        /// signs in, as at the game's start.
        /// </summary>
        /// <exception cref="IOException">cloud.json could not be written: the switch is turned for this run.</exception>
        public async Task TurnTheSwitchAsync(bool isOn, CancellationToken cancel)
        {
            _sync.SwitchTo(isOn);
            if (isOn)
            {
                await StartAsync(cancel);
                return;
            }

            if (Account.Player != null)
            {
                _accounts.SignOutKeepingTheSession();
            }

            Account = new Account.Off(ServicesOff.SwitchedOff);
            _log("Cloud save: the player turned the switch off; nothing reaches Unity from now on.");
        }

        public SignInPlatform? OfferTheLink(int levelsWon)
        {
            bool hasThePlatform = _identity.Platform != SignInPlatform.None && !_identity.FoundThePlatformMissing;
            bool isDue = Account is Account.Anonymous && hasThePlatform && !_sync.State.LinkOffered && levelsWon >= 1;
            if (!isDue)
            {
                return null;
            }

            try
            {
                _sync.KeepTheLinkOffered();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                _log($"Cloud save: cloud.json could not keep that the link was offered, so it is not offered again this run only: {failure.Message}");
            }

            _log($"Cloud save: the link to {_identity.Platform} is offered, once.");
            return _identity.Platform;
        }

        public void Ask(AccountRequest request)
        {
            Task work = request switch
            {
                AccountRequest.Link _ => LinkAsync(CancellationToken.None),
                AccountRequest.SwitchToTheLinked _ => SwitchAsync(CancellationToken.None),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request, "A request the account does not know."),
            };
            Answering = work;
            ReportTheFaultOf(work);
        }

        public void AskForAnUpload(SyncTrigger trigger) => ReportTheFaultOf(UploadAsync(trigger, CancellationToken.None));

        /// <summary>
        /// Work that runs in the background reports a fault of the game's own code where the game's root says (the
        /// device's log, as an exception). The cloud's failures are no faults: the seams give them as answers.
        /// </summary>
        public void ReportTheFaultOf(Task work) =>
            work.ContinueWith(done => _fault(done.Exception!), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        public IReadOnlyList<AccountNews> TakeNews()
        {
            if (_news.Count == 0)
            {
                return Array.Empty<AccountNews>();
            }

            AccountNews[] taken = _news.ToArray();
            _news.Clear();
            return taken;
        }

        private async Task LinkAsync(CancellationToken cancel)
        {
            if (!(Account is Account.Anonymous anonymous) || _isAnsweringTheCard)
            {
                _log($"Cloud save: \"link\" is let go: the account is {Account}{(_isAnsweringTheCard ? ", and a card's answer runs" : string.Empty)}.");
                return;
            }

            _isAnsweringTheCard = true;
            try
            {
                LinkOutcome outcome = await _identity.LinkAsync(cancel);
                _log($"Cloud save: the link came to: {outcome}.");
                switch (outcome)
                {
                    case LinkOutcome.Linked linked:
                        Account = new Account.Linked(anonymous.SignedInAs, linked.Platform);
                        _news.Add(new AccountNews.Linked(linked.Platform));
                        await SyncAsync(anonymous.SignedInAs, SyncTrigger.AccountChanged, cancel); // the save is in the cloud for the next phone
                        break;
                    case LinkOutcome.HeldByAnother held:
                        _news.Add(new AccountNews.OtherSaveFound(held.Platform));
                        break;
                    case LinkOutcome.NotSignedIn { Why: PlatformRefusal.Declined }:
                        break; // the player closed the platform's own sheet: nothing to say, and nothing asked again
                    case LinkOutcome.NotSignedIn notSignedIn:
                        _news.Add(new AccountNews.NotLinked(_identity.Platform, notSignedIn.Why == PlatformRefusal.NotOnThisPhone ? LinkFailure.NotOnThisPhone : LinkFailure.Unreachable));
                        break;
                    case LinkOutcome.Unreachable _:
                        _news.Add(new AccountNews.NotLinked(_identity.Platform, LinkFailure.Unreachable));
                        break;
                    default:
                        throw new InvalidOperationException($"The cloud save does not know the link's outcome \"{outcome}\".");
                }
            }
            finally
            {
                _isAnsweringTheCard = false;
            }
        }

        private async Task SwitchAsync(CancellationToken cancel)
        {
            if (!(Account is Account.Anonymous) || _isAnsweringTheCard)
            {
                _log($"Cloud save: \"go over\" is let go: the account is {Account}{(_isAnsweringTheCard ? ", and a card's answer runs" : string.Empty)}.");
                return;
            }

            _isAnsweringTheCard = true;
            try
            {
                SwitchOutcome outcome = await _identity.SwitchToTheLinkedAccountAsync(cancel);
                _log($"Cloud save: going over came to: {outcome}.");
                switch (outcome)
                {
                    case SwitchOutcome.Switched switched:
                        Account = switched.Account;
                        if (await SyncAsync(switched.Account.SignedInAs, SyncTrigger.AccountChanged, cancel) is SyncOutcome.Done)
                        {
                            _news.Add(new AccountNews.SaveCameBack());
                        }

                        break;
                    case SwitchOutcome.NotSignedIn { Why: PlatformRefusal.Declined }:
                        break;
                    case SwitchOutcome.NotSignedIn _:
                        _news.Add(new AccountNews.NotSwitched());
                        break;
                    case SwitchOutcome.NotSwitched notSwitched:
                        Account = notSwitched.Account;
                        _news.Add(new AccountNews.NotSwitched());
                        break;
                    default:
                        throw new InvalidOperationException($"The cloud save does not know going over's outcome \"{outcome}\".");
                }
            }
            finally
            {
                _isAnsweringTheCard = false;
            }
        }

        /// <summary>Syncs, and tells the museum when the cloud's save brought the player further than the phone was (a new phone, the game installed again).</summary>
        private async Task SyncTellingWhenTheSaveCameBackAsync(CloudPlayerId player, SyncTrigger trigger, CancellationToken cancel)
        {
            int levelsBefore = _holder.Current.LevelsWon;
            if (await SyncAsync(player, trigger, cancel) is SyncOutcome.Done { Adopted: true } && _holder.Current.LevelsWon > levelsBefore)
            {
                _news.Add(new AccountNews.SaveCameBack());
            }
        }

        /// <summary>Runs the sync and writes what it came to into the log; nothing when cloud.json could not be written, which the log says too.</summary>
        private async Task<SyncOutcome?> SyncAsync(CloudPlayerId player, SyncTrigger trigger, CancellationToken cancel)
        {
            try
            {
                SyncOutcome outcome = await _sync.SyncAsync(player, trigger, cancel);
                LastSync = outcome;
                _log($"Cloud save: the sync for {trigger} came to: {outcome}.");
                if (outcome is SyncOutcome.Done done)
                {
                    foreach (string note in done.Notes)
                    {
                        _log($"Cloud save: {note}.");
                    }
                }

                return outcome;
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                _log($"Cloud save: cloud.json could not be written, so the sync for {trigger} stopped and nothing more was sent: {failure.Message}");
                return null;
            }
        }

        /// <summary>Why the services may not start now; nothing when they may.</summary>
        private ServicesOff? WhyOff() =>
            !_sync.State.IsSwitchedOn ? ServicesOff.SwitchedOff
            : !SyncPolicy.MayStartServices(isSwitchedOn: true, _hasACloudProject) ? ServicesOff.NoCloudProject
            : (ServicesOff?)null;
    }
}
