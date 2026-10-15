using Puzzle.Client.Flow;

namespace Puzzle.Core.Tests;

/// <summary>
/// The player's settings on the phone, before anything is drawn: what a first start has, how one setting is turned
/// without the others, what this build honours, and the store the tools keep them in. <c>DeviceSettings</c> and
/// <c>DeviceAbilities</c> are the Unity project's source files, compiled into these tests; the device file that keeps
/// them on a phone is checked with Unity's own reader by unity/Assets/Editor/AttemptLogCheck.cs.
/// </summary>
public class DeviceSettingsTests
{
    [Fact]
    public void A_first_start_has_every_setting_on()
    {
        Assert.All(Enum.GetValues<DeviceSetting>(), setting => Assert.True(DeviceSettings.Default.IsOn(setting)));
    }

    [Theory]
    [InlineData(DeviceSetting.Sound)]
    [InlineData(DeviceSetting.Vibration)]
    public void Turning_one_setting_changes_that_one_and_no_other(DeviceSetting turned)
    {
        DeviceSettings off = DeviceSettings.Default.With(turned, isOn: false);

        foreach (DeviceSetting setting in Enum.GetValues<DeviceSetting>())
        {
            Assert.Equal(setting != turned, off.IsOn(setting));
        }

        Assert.Equal(DeviceSettings.Default, off.With(turned, isOn: true));
    }

    [Fact]
    public void Settings_with_the_same_switches_are_equal()
    {
        Assert.Equal(new DeviceSettings(sound: false, vibration: true), new DeviceSettings(sound: false, vibration: true));
        Assert.NotEqual(new DeviceSettings(sound: false, vibration: true), new DeviceSettings(sound: true, vibration: false));
    }

    [Fact]
    public void This_build_honours_no_setting_yet_so_the_card_offers_no_switch()
    {
        // The game makes no sound and never vibrates: a switch for either would change nothing.
        Assert.Empty(DeviceAbilities.OfThisBuild.Settings);
    }

    [Fact]
    public void Settings_kept_in_memory_are_the_ones_given_last()
    {
        var store = new SettingsInMemory(DeviceSettings.Default);
        DeviceSettings quiet = DeviceSettings.Default.With(DeviceSetting.Sound, isOn: false);

        store.Keep(quiet);

        Assert.Equal(quiet, store.Settings);
    }
}
