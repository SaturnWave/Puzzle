using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;

namespace Puzzle.Core.Tests;

/// <summary>
/// A phone as the settings card and the cards it opens see it (<c>DeviceView</c>): the default settings, the id
/// "6f1c0e9a…", the game's version 1.0, the store build that sells nothing yet, Turkish, and the account the test gives
/// (no cloud when none).
/// </summary>
internal static class TestDevices
{
    public static readonly DeviceId PhoneId = new("6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a");

    /// <summary>An account of the cloud, as Unity's Authentication names one: 28 letters and digits.</summary>
    public static readonly CloudPlayerId CloudPlayer = new("hWHj8vr9oPrG57qgmyc8DctDWkfF");

    public static DeviceView Phone(AccountView? account = null) => new(
        DeviceSettings.Default, DeviceAbilities.OfThisBuild.Settings, PhoneId, "1.0", ContentVersion.First,
        recordsKept: 0, recordsFile: null, AdSource.Simulated, RealMoneySales.ComingSoon, Language.Turkish,
        new[] { new LanguageOffer(Language.Turkish, "Türkçe") }, account);

    /// <summary>The cloud's view of an account of this phone: signed in to <see cref="CloudPlayer"/>, or not yet signed in, the switch on or off.</summary>
    public static AccountView Cloud(bool isSwitchedOn, bool isSignedIn) => new(
        isSignedIn ? new Account.Anonymous(CloudPlayer) : isSwitchedOn ? new Account.Starting() : new Account.Off(ServicesOff.SwitchedOff),
        knownPlayer: null, isSwitchedOn, lastSynced: null, lastTryFailed: false, SignInPlatform.None, revision: 1, DateTimeOffset.UnixEpoch);
}
