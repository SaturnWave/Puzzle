#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;

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

        /// <summary>The settings' "Bulut kaydı" switch was turned.</summary>
        public sealed class TurnTheSwitch : AccountRequest
        {
            public TurnTheSwitch(bool isOn)
            {
                IsOn = isOn;
            }

            public bool IsOn { get; }

            public override string ToString() => $"turn the cloud save {(IsOn ? "on" : "off")}";
        }

        /// <summary>"Sil" on the question of "Verilerimi sil": the account and its data in the cloud are deleted (§5.6).</summary>
        public sealed class DeleteTheCloudData : AccountRequest
        {
            public override string ToString() => "delete the data in the cloud";
        }
    }

    /// <summary>Why deleting the data in the cloud did not come about, as the card says it. A closed set.</summary>
    public enum DeletionFailure
    {
        /// <summary>The phone has no network: deleting needs one.</summary>
        NoNetwork,

        /// <summary>Unity's services or the game's server could not be reached, or refused: nothing more was deleted.</summary>
        Unavailable,
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

        /// <summary>The account and its data in the cloud are deleted, or there were none; the switch is off. The card asks about this phone's progress next.</summary>
        public sealed class CloudDataDeleted : AccountNews
        {
            public CloudDataDeleted(bool thereWereNone)
            {
                ThereWereNone = thereWereNone;
            }

            /// <summary>True when the phone had no account in the cloud to delete (the switch never on, or no cloud in this build).</summary>
            public bool ThereWereNone { get; }

            public override string ToString() => ThereWereNone ? "nothing was in the cloud" : "the data in the cloud is deleted";
        }

        /// <summary>The player had the progress on this phone deleted too, after the cloud's: the game starts over (the session tells this, not the cloud).</summary>
        public sealed class StartedAnew : AccountNews
        {
            public override string ToString() => "everything is deleted; the game starts over";
        }

        /// <summary>Nothing more was deleted in the cloud, for this reason: asking again finishes it.</summary>
        public sealed class CloudDataNotDeleted : AccountNews
        {
            public CloudDataNotDeleted(DeletionFailure why)
            {
                Why = why;
            }

            public DeletionFailure Why { get; }

            public override string ToString() => $"the data in the cloud is not deleted: {Why}";
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

        /// <summary>The server the purchase's course asks about each payment (docs/research/server-v1.md §4): the account's own, or none.</summary>
        IPurchaseServer PurchaseServer { get; }

        /// <summary>What the settings show of the account and its cloud save now.</summary>
        AccountView View { get; }
    }

    /// <summary>The Editor's tools' desk: no account, no offer, no news, no upload, no server.</summary>
    public sealed class NoCloudDesk : ICloudDesk
    {
        private NoCloudDesk()
        {
        }

        public static NoCloudDesk Instance { get; } = new NoCloudDesk();

        public IPurchaseServer PurchaseServer => NoPurchaseServer.Instance;

        public AccountView View => AccountView.NoCloud;

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
    /// account and its sync, and the game's server behind the account, in one place the game's root runs in the
    /// background. It never holds the game up: the game plays on its progress file from the first frame, and whatever of
    /// the cloud fails is only written to the log.
    /// <list type="bullet">
    /// <item>With the switch off, or in a build without a cloud project, Unity's services are never started
    /// (<see cref="SyncPolicy.MayStartServices"/>): no account, no sync, no verification, nothing sent, the game as before
    /// the server.</item>
    /// <item>Otherwise, as the game starts, the services start, the player signs in (<see cref="PlayerIdentity"/>), Remote
    /// Config is read, and the sync runs for the start (<see cref="CloudSync"/>): a new phone signed in to the account of
    /// its platform gets the save back, and the museum is told so.</item>
    /// <item>The game's other moments ask for uploads (the background, a painting or a room completed, a payment); the
    /// sync's budget decides what goes.</item>
    /// <item>The purchase's course asks it about each payment (<see cref="IPurchaseServer"/>): it asks the Cloud Code
    /// module as the player signed in, once a start under way has signed in, and tries a start that could not reach Unity
    /// again first. A verified payment leaves the ledger copy stale, so the next merge reads the ledger.</item>
    /// <item>Once, after a won level, an account of this phone is offered the link to the platform. "Bağlan" links it, or,
    /// when the platform's player has an account of their own, asks whether to go over to it; going over merges this
    /// phone's progress into that account's save. The account left behind is not deleted.</item>
    /// <item>Turning the switch off signs out and stops every sync at once; turning it on starts the services, if this
    /// run had not started them.</item>
    /// <item>"Verilerimi sil" (§5.6) signs in to the account the phone keeps if it must (the switch off included: the
    /// player asked for it), has the module delete the data, then deletes the account and forgets it: the switch is off
    /// after it. Nothing is forgotten on the phone unless the module said the data is deleted.</item>
    /// </list>
    /// Nothing here is Unity's: the services, the platform, Remote Config, the cloud's save and the module come in through
    /// seams, and the tests and the Editor's checks run every case on <see cref="FakeUnityServices"/> and
    /// <see cref="FakePurchaseServer"/>.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class CloudSaving : ICloudDesk, IPurchaseServer
    {
        private readonly CloudSync _sync;
        private readonly IAccountService _accounts;
        private readonly PlayerIdentity _identity;
        private readonly IRemoteSettings _remote;
        private readonly IServerModule _module;
        private readonly IProgressHolder _holder;
        private readonly bool _hasACloudProject;
        private readonly Action<string> _log;
        private readonly Action<Exception> _fault;
        private readonly List<AccountNews> _news = new List<AccountNews>();

        /// <summary>The server's verdicts not taken yet; its questions may be answered on another thread than the game's.</summary>
        private readonly List<ServerAnswer> _answers = new List<ServerAnswer>();

        private Account _account;
        private SyncOutcome? _lastSync;

        /// <summary>How many times what the settings show changed: the view's revision.</summary>
        private int _changes;

        /// <summary>The start that runs or ran last; nothing before the first.</summary>
        private Task? _start;

        /// <summary>True while a link, a going over or a deletion runs: a second press waits for none and is let go.</summary>
        private bool _isAnsweringTheCard;

        /// <param name="hasACloudProject">Whether the build has a Unity Cloud project to talk to (false in the Editor, which starts none).</param>
        /// <param name="module">The game's Cloud Code module, as the player signed in calls it.</param>
        /// <param name="log">Where what the cloud came to is written: the device's log.</param>
        /// <param name="fault">Where a fault of the game's own code in the background goes: the device's log as an exception.</param>
        public CloudSaving(
            CloudSync sync, IAccountService accounts, IPlatformSignIn platform, IRemoteSettings remote, IServerModule module, IProgressHolder holder, bool hasACloudProject,
            Action<string> log, Action<Exception> fault)
        {
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
            _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            _identity = new PlayerIdentity(accounts, platform ?? throw new ArgumentNullException(nameof(platform)));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _holder = holder ?? throw new ArgumentNullException(nameof(holder));
            _hasACloudProject = hasACloudProject;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _fault = fault ?? throw new ArgumentNullException(nameof(fault));
            _account = WhyOff() is ServicesOff off ? new Account.Off(off) : (Account)new Account.Starting();
        }

        /// <summary>Where the account stands now.</summary>
        public Account Account
        {
            get => _account;
            private set
            {
                _account = value;
                _changes++;
            }
        }

        /// <summary>What the last sync came to; nothing before the first.</summary>
        public SyncOutcome? LastSync
        {
            get => _lastSync;
            private set
            {
                _lastSync = value;
                _changes++;
            }
        }

        /// <summary>Whether the player's "Bulut kaydı" switch is on.</summary>
        public bool IsSwitchedOn => _sync.State.IsSwitchedOn;

        /// <summary>The work a press on a card started, for the Editor's check to wait on; nothing before the first.</summary>
        public Task? Answering { get; private set; }

        public IPurchaseServer PurchaseServer => this;

        public AccountView View => new AccountView(Account, _sync.State.Player, IsSwitchedOn, _sync.State.LastSynced, _sync.State.Failures > 0, CanLinkTo, _changes, _sync.Now);

        /// <summary>
        /// Payments wait for the module's verdict while the services may run (the switch on, a cloud project) and Remote
        /// Config has not turned the verification off; otherwise the game gives them on the phone, as before the server.
        /// </summary>
        public bool Verifies => !(Account is Account.Off) && _sync.Settings.ValidatesPurchases;

        public StoreBuyer? Buyer => Account.Player is CloudPlayerId player ? new StoreBuyer(player.Value) : null;

        /// <summary>The platform the settings' "Bağlan" links to: an account of this phone, on a build with the platform's sign-in, on a phone where it was not found missing.</summary>
        private SignInPlatform CanLinkTo =>
            Account is Account.Anonymous && _identity.Platform != SignInPlatform.None && !_identity.FoundThePlatformMissing ? _identity.Platform : SignInPlatform.None;

        /// <summary>
        /// Starts the services, signs in and syncs for the game's start, unless the switch or the build keeps the services
        /// off. A run that signed in already does nothing.
        /// </summary>
        public Task StartAsync(CancellationToken cancel)
        {
            Task start = StartTheCloudAsync(cancel);
            _start = start;
            return start;
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
        /// The player turned the "Bulut kaydı" switch: kept at once, or for this run when cloud.json cannot be written
        /// (which the log says). Off signs out (no token is refreshed, nothing is sent from now on; the session stays on
        /// the phone, so on again finds the same account). On starts the services and signs in, as at the game's start.
        /// </summary>
        public async Task TurnTheSwitchAsync(bool isOn, CancellationToken cancel)
        {
            try
            {
                _sync.SwitchTo(isOn);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                _log($"Cloud save: cloud.json could not keep the switch {(isOn ? "on" : "off")}, so it holds for this run only: {failure.Message}");
            }

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
                AccountRequest.TurnTheSwitch turn => TurnTheSwitchAsync(turn.IsOn, CancellationToken.None),
                AccountRequest.DeleteTheCloudData _ => DeleteTheCloudDataAsync(CancellationToken.None),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request, "A request the account does not know."),
            };
            Answering = work;
            ReportTheFaultOf(work);
        }

        public void Ask(PaymentQuestion question) => ReportTheFaultOf(AskTheServerAsync(question ?? throw new ArgumentNullException(nameof(question))));

        public IReadOnlyList<ServerAnswer> TakeAnswers()
        {
            lock (_answers)
            {
                if (_answers.Count == 0)
                {
                    return Array.Empty<ServerAnswer>();
                }

                ServerAnswer[] taken = _answers.ToArray();
                _answers.Clear();
                return taken;
            }
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

        private async Task StartTheCloudAsync(CancellationToken cancel)
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

        /// <summary>The module's verdict on a payment, given as an answer whatever comes of asking: a fault of the game's own code still answers (no verdict) before it is reported.</summary>
        private async Task AskTheServerAsync(PaymentQuestion question)
        {
            ServerVerdict verdict = new ServerVerdict.Unavailable("the game's own code failed while asking");
            try
            {
                verdict = await VerdictOnAsync(question);
            }
            finally
            {
                _log($"Cloud save: the server's verdict on the {question}: {verdict}.");
                lock (_answers)
                {
                    _answers.Add(new ServerAnswer(question.Payment, verdict));
                }
            }
        }

        private async Task<ServerVerdict> VerdictOnAsync(PaymentQuestion question)
        {
            if (_start != null && !_start.IsCompleted)
            {
                await Task.WhenAny(_start); // a sign-in under way is waited for; its faults are its own to report
            }

            if (Account is Account.Unreachable)
            {
                await Task.WhenAny(StartAsync(CancellationToken.None)); // a start that could not reach Unity is tried again for the payment
            }

            if (!(Account.Player is CloudPlayerId))
            {
                return new ServerVerdict.Unavailable($"no account is signed in ({Account})");
            }

            if (question.Payment.Proof == null)
            {
                return new ServerVerdict.Unavailable("the store gave no proof of the payment to ask about");
            }

            ServerVerdict verdict = await _module.ValidatePurchaseAsync(question, CancellationToken.None);
            if (verdict is ServerVerdict.Valid || verdict is ServerVerdict.AlreadyValidated)
            {
                KnowTheLedgerChanged();
            }

            return verdict;
        }

        private void KnowTheLedgerChanged()
        {
            try
            {
                _sync.KnowTheLedgerChanged();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                _log($"Cloud save: cloud.json could not keep that the ledger changed; the next merge of this run reads it all the same: {failure.Message}");
            }
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

        /// <summary>
        /// "Verilerimi sil" (§5.6): the account the phone keeps is signed in to if it must be (a phone whose switch is off
        /// keeps its session), the module deletes its data, and the account is deleted after it; then the phone forgets the
        /// account and the switch is off. A phone with no account in the cloud has nothing to delete there. Whatever cannot
        /// be reached leaves everything as it was, and the card says so.
        /// </summary>
        private async Task DeleteTheCloudDataAsync(CancellationToken cancel)
        {
            if (_isAnsweringTheCard)
            {
                _log("Cloud save: \"delete\" is let go: a card's answer runs.");
                return;
            }

            _isAnsweringTheCard = true;
            try
            {
                AccountNews news = await DeletedAsync(cancel);
                _log($"Cloud save: deleting the data in the cloud came to: {news}.");
                _news.Add(news);
            }
            finally
            {
                _isAnsweringTheCard = false;
            }
        }

        private async Task<AccountNews> DeletedAsync(CancellationToken cancel)
        {
            if (!_hasACloudProject)
            {
                return new AccountNews.CloudDataDeleted(thereWereNone: true);
            }

            if (_start != null && !_start.IsCompleted)
            {
                await Task.WhenAny(_start); // a sign-in under way is waited for, not crossed
            }

            bool signedInForTheDeletion = false;
            if (!(Account.Player is CloudPlayerId))
            {
                if (await _accounts.StartAsync(cancel) is CloudFailure notStarted)
                {
                    return new AccountNews.CloudDataNotDeleted(DeletionFailureOf(notStarted));
                }

                if (!_accounts.HasASessionOnThisPhone)
                {
                    Forget();
                    return new AccountNews.CloudDataDeleted(thereWereNone: true);
                }

                if (await _accounts.SignInAnonymouslyAsync(cancel) is AccountAnswer.Failed notSignedIn)
                {
                    return new AccountNews.CloudDataNotDeleted(DeletionFailureOf(notSignedIn.Failure));
                }

                signedInForTheDeletion = true;
            }

            DataDeletion deletion = await _module.DeleteMyDataAsync(cancel);
            CloudFailure? accountKept = deletion == DataDeletion.Deleted ? await _accounts.DeleteAccountAsync(cancel) : (CloudFailure?)null;
            if (deletion != DataDeletion.Deleted || accountKept != null)
            {
                if (signedInForTheDeletion)
                {
                    _accounts.SignOutKeepingTheSession(); // the switch stays as it was: nothing more is sent
                }

                DeletionFailure why = deletion == DataDeletion.NoNetwork || accountKept == CloudFailure.NoNetwork ? DeletionFailure.NoNetwork : DeletionFailure.Unavailable;
                return new AccountNews.CloudDataNotDeleted(why);
            }

            Forget();
            return new AccountNews.CloudDataDeleted(thereWereNone: false);
        }

        /// <summary>The account is gone: the phone keeps nothing of it, and the switch is off until the player turns it on (a new account then).</summary>
        private void Forget()
        {
            try
            {
                _sync.Forget();
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                _log($"Cloud save: cloud.json could not be written after the deletion, so it forgets the account for this run only: {failure.Message}");
            }

            Account = new Account.Off(ServicesOff.SwitchedOff);
        }

        private static DeletionFailure DeletionFailureOf(CloudFailure failure) => failure == CloudFailure.NoNetwork ? DeletionFailure.NoNetwork : DeletionFailure.Unavailable;

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

        /// <summary>Why the services may not start now; nothing when they may. A build without a cloud project has no switch to speak of.</summary>
        private ServicesOff? WhyOff() =>
            !SyncPolicy.MayStartServices(isSwitchedOn: true, _hasACloudProject) ? ServicesOff.NoCloudProject
            : !_sync.State.IsSwitchedOn ? ServicesOff.SwitchedOff
            : (ServicesOff?)null;
    }
}
