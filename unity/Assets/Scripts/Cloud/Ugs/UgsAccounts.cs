#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// Unity's services and their Authentication 3.8.1 (com.unity.services.authentication, pinned) as the identity needs
    /// them (docs/research/server-v1.md §5): the services start in the environment of this build, the player signs in to
    /// the account whose session the phone keeps or to a new one, to the account of a Google Play Games or Game Center
    /// player, links one to the account, and signs out. Unity keeps the session in the PlayerPrefs (the key
    /// "&lt;project id&gt;.&lt;profile&gt;.unity.services.authentication.session_token" of
    /// shared_prefs/com.saturnwave.museumpuzzle.v2.playerprefs.xml on Android), which Android's backup carries
    /// (Editor/ManifestBackupRules.cs); the game keeps nothing of it. Nothing is asked of Unity before
    /// <see cref="StartAsync"/>, which the game calls only with the player's switch on and a cloud project
    /// (<see cref="CloudSaving"/>); once the services have started, Wire is turned off before the sign-in
    /// (<see cref="WireSwitch"/>).
    /// <para>
    /// Every exception of the services ends here as a <see cref="CloudFailure"/> (§3.8): the network's
    /// (<see cref="CommonErrorCodes.TransportError"/>) as no network, a timeout or a service down as unavailable, a refused
    /// project, player or token (the free tier's limit passed, a project not linked, a banned player) as blocked, too
    /// many requests as rate limited, and what the game asked wrongly (an empty code, a sign-in while signed in) as
    /// refused, and so is a platform the project has not set up (InvalidProvider: the start then signs in to an account of this
    /// phone); Unity's AccountAlreadyLinked is the link's own answer. What Unity said goes to the log, never to the
    /// player.
    /// </para>
    /// </summary>
    public sealed class UgsAccounts : IAccountService
    {
        private readonly string _environment;
        private readonly Action<string> _log;
        private Task<CloudFailure?>? _starting;

        /// <param name="environment">The UGS environment of this build: "development" for a test build, "production" otherwise (§3.1).</param>
        /// <param name="log">Where what Unity said goes: the device's log.</param>
        public UgsAccounts(string environment, Action<string> log)
        {
            _environment = string.IsNullOrEmpty(environment) ? throw new ArgumentException("A build names its environment.", nameof(environment)) : environment;
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public bool HasASessionOnThisPhone => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.SessionTokenExists;

        public Task<CloudFailure?> StartAsync(CancellationToken cancel)
        {
            // A start that runs, or that started the services, is the one start; one that failed (no network as the game
            // opened) is tried anew, as a payment's verification or a deletion asks it.
            bool hasFailed = _starting != null && _starting.IsCompleted && (_starting.IsFaulted || _starting.Result != null);
            if (_starting == null || hasFailed)
            {
                _starting = StartOnceAsync();
            }

            return _starting;
        }

        public Task<AccountAnswer> SignInAnonymouslyAsync(CancellationToken cancel) => SignInAsync(() => AuthenticationService.Instance.SignInAnonymouslyAsync(), "anonymously");

        public Task<AccountAnswer> SignInWithAsync(PlatformCredential credential, CancellationToken cancel) => credential switch
        {
            PlatformCredential.PlayGamesCode code => SignInAsync(() => AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code.Code), "with Google Play Games"),
            PlatformCredential.GameCenterSignature signature => SignInAsync(
                () => AuthenticationService.Instance.SignInWithAppleGameCenterAsync(signature.Signature, signature.TeamPlayerId, signature.PublicKeyUrl, signature.Salt, signature.Timestamp),
                "with Game Center"),
            _ => throw new ArgumentOutOfRangeException(nameof(credential), credential, "A credential Unity's Authentication is not asked with."),
        };

        public async Task<LinkAnswer> LinkWithAsync(PlatformCredential credential, CancellationToken cancel)
        {
            try
            {
                switch (credential)
                {
                    case PlatformCredential.PlayGamesCode code:
                        await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code.Code);
                        break;
                    case PlatformCredential.GameCenterSignature signature:
                        await AuthenticationService.Instance.LinkWithAppleGameCenterAsync(signature.Signature, signature.TeamPlayerId, signature.PublicKeyUrl, signature.Salt, signature.Timestamp);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(credential), credential, "A credential Unity's Authentication is not asked with.");
                }

                return new LinkAnswer.Linked();
            }
            catch (AuthenticationException refused) when (refused.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return new LinkAnswer.HeldByAnother();
            }
            catch (RequestFailedException failure)
            {
                _log($"Unity's Authentication did not link {credential.Platform}: {failure.ErrorCode} {failure.Message}");
                return new LinkAnswer.Failed(FailureOf(failure));
            }
        }

        public void SignOutKeepingTheSession()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(clearCredentials: false);
            }
        }

        public async Task<CloudFailure?> DeleteAccountAsync(CancellationToken cancel)
        {
            try
            {
                // Authentication 3.8.1 signs out and clears the phone's session once the account is deleted.
                await AuthenticationService.Instance.DeleteAccountAsync();
                return null;
            }
            catch (RequestFailedException failure)
            {
                _log($"Unity's Authentication did not delete the account: {failure.ErrorCode} {failure.Message}");
                return FailureOf(failure);
            }
        }

        private async Task<CloudFailure?> StartOnceAsync()
        {
            try
            {
                await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(_environment));
                _log($"Unity's services started in the environment \"{_environment}\".");
                WireSwitch.TurnOff(_log); // before the sign-in, at which Wire would open its websocket
                return null;
            }
            catch (ServicesInitializationException failure)
            {
                _log($"Unity's services did not start: {failure.Message}");
                return CloudFailure.Blocked;
            }
            catch (RequestFailedException failure)
            {
                _log($"Unity's services did not start: {failure.ErrorCode} {failure.Message}");
                return FailureOf(failure);
            }
        }

        private async Task<AccountAnswer> SignInAsync(Func<Task> signIn, string how)
        {
            try
            {
                await signIn();
                IAuthenticationService authentication = AuthenticationService.Instance;
                return new AccountAnswer.SignedIn(new CloudPlayerId(authentication.PlayerId), LinkedTo(authentication.PlayerInfo));
            }
            catch (RequestFailedException failure)
            {
                _log($"Unity's Authentication did not sign in {how}: {failure.ErrorCode} {failure.Message}");
                return new AccountAnswer.Failed(FailureOf(failure));
            }
        }

        /// <summary>The platform the account signed in to is linked to, as the sign-in's answer says; none when it names neither.</summary>
        private static SignInPlatform LinkedTo(PlayerInfo? info)
        {
            if (!string.IsNullOrEmpty(info?.GetGooglePlayGamesId()))
            {
                return SignInPlatform.PlayGames;
            }

            return !string.IsNullOrEmpty(info?.GetAppleGameCenterId()) ? SignInPlatform.GameCenter : SignInPlatform.None;
        }

        /// <summary>What a failure of Unity's services means for the game (the closed set the sync knows).</summary>
        private static CloudFailure FailureOf(RequestFailedException failure)
        {
            int code = failure.ErrorCode;
            if (code == CommonErrorCodes.TransportError)
            {
                return CloudFailure.NoNetwork;
            }

            if (code == CommonErrorCodes.TooManyRequests)
            {
                return CloudFailure.RateLimited;
            }

            bool isRefusedForTheProjectOrPlayer = code == CommonErrorCodes.Forbidden || code == CommonErrorCodes.ProjectPolicyAccessDenied
                || code == CommonErrorCodes.PlayerPolicyAccessDenied || code == CommonErrorCodes.InvalidToken || code == CommonErrorCodes.TokenExpired
                || code == AuthenticationErrorCodes.BannedUser || code == AuthenticationErrorCodes.InvalidSessionToken || code == AuthenticationErrorCodes.EnvironmentMismatch;
            if (isRefusedForTheProjectOrPlayer)
            {
                return CloudFailure.Blocked;
            }

            bool isTheGamesOwnMistake = code == CommonErrorCodes.InvalidRequest || code == AuthenticationErrorCodes.InvalidParameters
                || code == AuthenticationErrorCodes.ClientInvalidUserState || code == AuthenticationErrorCodes.ClientNoActiveSession
                || code == AuthenticationErrorCodes.AccountLinkLimitExceeded || code == AuthenticationErrorCodes.InvalidProvider;
            return isTheGamesOwnMistake ? CloudFailure.Refused : CloudFailure.Unavailable;
        }
    }
}
