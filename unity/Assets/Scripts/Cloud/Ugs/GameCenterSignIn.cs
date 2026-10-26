#nullable enable
#if UNITY_IOS && PUZZLE_APPLE_GAMEKIT
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Apple.GameKit;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// Game Center through Apple's GameKit plugin for Unity (Apple.Core and Apple.GameKit, built on the Mac from Apple's
    /// own repository, at least iOS 15.6), as the identity needs it (docs/research/server-v1.md §5.2): Apple's own
    /// sign-in (GKLocalPlayer.Authenticate, which shows Apple's sheet once to a player not signed in to Game Center),
    /// then the identity verification (FetchItems) for Unity's Authentication. The signature is good for ten minutes, so
    /// it is fetched anew for every sign-in and link. The team player id goes to Unity only, never on a screen (Apple's
    /// rule 4.5.5).
    /// <para>
    /// This file is compiled only for iOS with PUZZLE_APPLE_GAMEKIT defined, which the iOS stage sets when it adds Apple's
    /// packages (they are built on the Mac and are not in this project); without it an iPhone has no sign-in of the
    /// platform and the account stays on the phone. Written against the plugin's documented API and Unity's sample;
    /// first compiled and tried in the iOS stage.
    /// </para>
    /// Apple's sheet closed, Game Center turned off and the plugin's errors all come here as GameKitException: Game
    /// Center then is not on this phone for the game (iOS asks again only from its own settings).
    /// </summary>
    public sealed class GameCenterSignIn : IPlatformSignIn
    {
        private readonly Action<string> _log;

        public GameCenterSignIn(Action<string> log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public SignInPlatform Platform => SignInPlatform.GameCenter;

        public Task<PlatformSignIn> SignInQuietlyAsync(CancellationToken cancel) => SignInAsync();

        public Task<PlatformSignIn> SignInByHandAsync(CancellationToken cancel) => SignInAsync();

        private async Task<PlatformSignIn> SignInAsync()
        {
            try
            {
                if (!GKLocalPlayer.Local.IsAuthenticated)
                {
                    await GKLocalPlayer.Authenticate();
                }

                GKIdentityVerificationResponse verification = await GKLocalPlayer.Local.FetchItems();
                return new PlatformSignIn.Signed(new PlatformCredential.GameCenterSignature(
                    Convert.ToBase64String(verification.GetSignature()),
                    GKLocalPlayer.Local.TeamPlayerId,
                    verification.PublicKeyUrl,
                    Convert.ToBase64String(verification.GetSalt()),
                    Convert.ToUInt64(verification.Timestamp, CultureInfo.InvariantCulture))); // milliseconds since 1970, whatever integer type the plugin gives
            }
            catch (GameKitException failure)
            {
                _log($"Game Center did not sign in: {failure.Code} {failure.Message}");
                return new PlatformSignIn.NotSignedIn(PlatformRefusal.NotOnThisPhone);
            }
        }
    }
}
#endif
