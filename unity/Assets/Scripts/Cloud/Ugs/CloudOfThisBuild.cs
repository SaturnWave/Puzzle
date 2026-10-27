#nullable enable
using System;
using Puzzle.Client.Flow;
using Puzzle.Meta;
using UnityEngine;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// The game's cloud save as this build has it (docs/research/server-v1.md §3–§5): the sync's state in cloud.json beside
    /// the progress file, Unity's services in the environment of the build ("development" for a test build, which sells
    /// nothing for money, "production" otherwise, §3.1), Cloud Save, Remote Config, the game's Cloud Code module (which
    /// verifies the payments of the build's store and deletes the player's data), and the platform's sign-in of the
    /// build: Google Play Games on Android where the build is set up for it, Game Center on iOS once Apple's plugin is in
    /// (PUZZLE_APPLE_GAMEKIT), none elsewhere. A build with no Unity Cloud project linked, and the Editor, start no
    /// services: the Editor's checks run the cloud on fakes (CloudCheck), and its play mode plays as a phone with the
    /// cloud out of reach.
    /// </summary>
    public static class CloudOfThisBuild
    {
        public const string DevelopmentEnvironment = "development";
        public const string ProductionEnvironment = "production";

        /// <summary>The cloud save on the session's progress; it starts nothing until <see cref="CloudSaving.StartAsync"/>.</summary>
        /// <param name="log">Where what the cloud came to is written: the device's log.</param>
        /// <param name="fault">Where a fault of the game's own code in the background goes.</param>
        /// <exception cref="System.IO.IOException">cloud.json is there but cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not read cloud.json.</exception>
        public static CloudSaving Make(IProgressHolder holder, EconomyRules rules, Action<string> log, Action<Exception> fault)
        {
            var json = new UnityDocumentJson();
            var state = new CloudStateFile(Application.persistentDataPath, json, rules.StartingWallet, RandomIds.New);
            var sync = new CloudSync(new UgsCloudSave(log), state, holder, rules, json, new SystemWallClock(), Application.version, RandomIds.New);
            if (sync.StateWasDamaged)
            {
                log($"Cloud save: {CloudStateFile.FileName} did not read and was set aside; the switch is off until the player turns it on.");
            }

            return new CloudSaving(
                sync, new UgsAccounts(EnvironmentOf(rules), log), PlatformSignInOfThisBuild(log), new UgsRemoteSettings(log),
                new UgsServerModule(UgsServerModule.StoreOfThisPlatform(), log), holder, HasACloudProject(log), log, fault);
        }

        /// <summary>The UGS environment a build talks to: a test build's is "development", a store build's "production".</summary>
        public static string EnvironmentOf(EconomyRules rules) =>
            rules.Shop.RealMoney == RealMoneySales.NotAvailable ? DevelopmentEnvironment : ProductionEnvironment;

        private static bool HasACloudProject(Action<string> log)
        {
            if (Application.isEditor)
            {
                log("Cloud save: the Editor starts no cloud services; CloudCheck checks the cloud on fakes.");
                return false;
            }

            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                log("Cloud save: this build has no Unity Cloud project linked, so Unity's services are never started.");
                return false;
            }

            return true;
        }

        private static IPlatformSignIn PlatformSignInOfThisBuild(Action<string> log)
        {
#if UNITY_ANDROID
            return PlayGamesSignIn.OfThisBuild(log);
#elif UNITY_IOS && PUZZLE_APPLE_GAMEKIT
            return new GameCenterSignIn(log);
#else
            return NoPlatformSignIn.Instance;
#endif
        }
    }
}
