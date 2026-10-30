#nullable enable
#if UNITY_ANDROID
using System;
using System.Threading;
using System.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// Google Play Games Services v2 through Google's plugin for Unity 2.3.0 (unity/Packages/com.google.play.games, its
    /// runtime as Google ships it), as the identity needs it (docs/research/server-v1.md §5.1): the quiet sign-in at the
    /// start (the SDK's own, which signs in a player who used Play Games on this phone before), the sign-in by hand
    /// ("Bağlan"), and after either a one-time server code for Unity's Authentication (RequestServerSideAccess with the
    /// default scopes only, so Google asks the player for nothing more).
    /// <para>
    /// It works only in a build set up for the game's Play Games project: the plugin's settings
    /// (Resources/PlayGamesSettings) with the game's id and the web client's id, which the owner's resources from the
    /// Play Console give (plan §9 B2–B4, Stage 6), and the same id in the manifest. A build without them has no sign-in of
    /// the platform (<see cref="OfThisBuild"/>), and the game offers no link. The plugin is activated (Google's SDK
    /// initialised) at the first sign-in only, which the game asks for once Unity's services have started: with the
    /// player's switch off, nothing of Play Games runs either.
    /// </para>
    /// The plugin answers on Unity's main thread; its errors are written to the log by the plugin itself and come here as
    /// a status, which ends as a <see cref="PlatformRefusal"/>.
    /// </summary>
    public sealed class PlayGamesSignIn : IPlatformSignIn
    {
        private bool _isActivated;

        private PlayGamesSignIn()
        {
        }

        public SignInPlatform Platform => SignInPlatform.PlayGames;

        /// <summary>The sign-in of this build: Play Games where the build is set up for the game's project, none otherwise, with why in the log.</summary>
        public static IPlatformSignIn OfThisBuild(Action<string> log)
        {
            PlayGamesSettings? settings = PlayGamesSettings.LoadInstance();
            if (settings == null || string.IsNullOrEmpty(settings.AppId) || string.IsNullOrEmpty(settings.WebClientId))
            {
                log("Google Play Games: this build is not set up for the game's Play Games project (no game id or web client id), so the account stays on this phone.");
                return NoPlatformSignIn.Instance;
            }

            return new PlayGamesSignIn();
        }

        public Task<PlatformSignIn> SignInQuietlyAsync(CancellationToken cancel) => SignInAsync(byHand: false);

        public Task<PlatformSignIn> SignInByHandAsync(CancellationToken cancel) => SignInAsync(byHand: true);

        private async Task<PlatformSignIn> SignInAsync(bool byHand)
        {
            if (!_isActivated)
            {
                PlayGamesPlatform.Activate();
                _isActivated = true;
            }

            var signedIn = new TaskCompletionSource<SignInStatus>();
            if (byHand)
            {
                PlayGamesPlatform.Instance.ManuallyAuthenticate(status => signedIn.TrySetResult(status));
            }
            else
            {
                PlayGamesPlatform.Instance.Authenticate(status => signedIn.TrySetResult(status));
            }

            SignInStatus status = await signedIn.Task;
            if (status != SignInStatus.Success)
            {
                // Canceled: the player closed Google's sheet, or (quietly) has not signed in to Play Games on this phone.
                return new PlatformSignIn.NotSignedIn(status == SignInStatus.Canceled ? PlatformRefusal.Declined : PlatformRefusal.Failed);
            }

            var code = new TaskCompletionSource<string?>();
            PlayGamesPlatform.Instance.RequestServerSideAccess(forceRefreshToken: false, given => code.TrySetResult(given));
            string? serverCode = await code.Task;
            return string.IsNullOrEmpty(serverCode)
                ? new PlatformSignIn.NotSignedIn(PlatformRefusal.Failed)
                : (PlatformSignIn)new PlatformSignIn.Signed(new PlatformCredential.PlayGamesCode(serverCode!));
        }
    }
}
#endif
