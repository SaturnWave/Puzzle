using Puzzle.Client.Flow;
using Puzzle.Client.Words;

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
    public void A_first_start_has_every_setting_on_but_the_tester_mode_and_the_statistics_which_it_has_not_asked_about_yet()
    {
        Assert.All(
            Enum.GetValues<DeviceSetting>(),
            setting => Assert.Equal(setting != DeviceSetting.TesterMode && setting != DeviceSetting.Statistics, DeviceSettings.Default.IsOn(setting)));
        Assert.Equal(StatisticsChoice.NotAsked, DeviceSettings.Default.Statistics);
    }

    [Theory]
    [InlineData(DeviceSetting.Sound)]
    [InlineData(DeviceSetting.Music)]
    [InlineData(DeviceSetting.Vibration)]
    [InlineData(DeviceSetting.TesterMode)]
    [InlineData(DeviceSetting.Statistics)]
    public void Turning_one_setting_changes_that_one_and_no_other(DeviceSetting turned)
    {
        // Answered once: the statistics' switch then turns between the two answers, as the others turn between on and off.
        DeviceSettings first = DeviceSettings.Default.WithLanguage(Language.Korean).WithTheStatistics(StatisticsChoice.Declined);
        DeviceSettings changed = first.With(turned, isOn: !first.IsOn(turned));

        foreach (DeviceSetting setting in Enum.GetValues<DeviceSetting>())
        {
            Assert.Equal(setting == turned ? !first.IsOn(setting) : first.IsOn(setting), changed.IsOn(setting));
        }

        Assert.Equal(Language.Korean, changed.Language);
        Assert.Equal(first, changed.With(turned, isOn: first.IsOn(turned)));
    }

    [Fact]
    public void Settings_with_the_same_switches_are_equal()
    {
        DeviceSettings withoutMusic = DeviceSettings.Default.With(DeviceSetting.Music, isOn: false);

        Assert.Equal(new DeviceSettings(sound: false, vibration: true), new DeviceSettings(sound: false, vibration: true));
        Assert.NotEqual(new DeviceSettings(sound: false, vibration: true), new DeviceSettings(sound: true, vibration: false));
        Assert.NotEqual(DeviceSettings.Default, withoutMusic);
        Assert.NotEqual(DeviceSettings.Default.GetHashCode(), withoutMusic.GetHashCode());
    }

    [Fact]
    public void Settings_made_without_naming_the_music_have_it_on_and_a_language_chosen_or_forgotten_leaves_it_as_it_was()
    {
        DeviceSettings withoutMusic = DeviceSettings.Default.With(DeviceSetting.Music, isOn: false);

        Assert.True(new DeviceSettings(sound: false, vibration: false, testerMode: true, Language.English).Music);
        Assert.False(withoutMusic.WithLanguage(Language.Japanese).Music);
        Assert.Equal(withoutMusic, withoutMusic.WithLanguage(Language.Japanese).WithThePhonesLanguage());
        Assert.Equal("sound on, music off, vibration on, the phone's language, statistics not asked", withoutMusic.ToString());
    }

    [Fact]
    public void The_statistics_switch_answers_the_question_and_never_takes_the_answer_back_to_not_asked()
    {
        DeviceSettings allowed = DeviceSettings.Default.With(DeviceSetting.Statistics, isOn: true);
        DeviceSettings declined = DeviceSettings.Default.With(DeviceSetting.Statistics, isOn: false);

        Assert.Equal((StatisticsChoice.Allowed, true), (allowed.Statistics, allowed.IsOn(DeviceSetting.Statistics)));
        Assert.Equal((StatisticsChoice.Declined, false), (declined.Statistics, declined.IsOn(DeviceSetting.Statistics)));
        Assert.NotEqual(DeviceSettings.Default, declined);
        Assert.NotEqual(DeviceSettings.Default.GetHashCode(), declined.GetHashCode());
        Assert.Equal(StatisticsChoice.Allowed, allowed.WithLanguage(Language.German).With(DeviceSetting.Music, isOn: false).Statistics);
        Assert.Equal("sound on, music on, vibration on, the phone's language, statistics allowed", allowed.ToString());
    }

    [Fact]
    public void This_build_honours_sound_music_vibration_and_the_statistics_so_the_card_offers_the_drawings_three_switches_and_the_statistics_under_them()
    {
        // The game sounds its cues, plays its music and buzzes under a few cues: each switch changes something
        // (SoundScoreTests; MusicFadeTests and the Editor's MusicCheck for the music). SettingsTR draws Ses, Müzik, Titreşim;
        // the statistics' switch lets the play notes leave the phone or stops them (PlayStatisticsTests).
        Assert.Equal(
            new[] { DeviceSetting.Sound, DeviceSetting.Music, DeviceSetting.Vibration, DeviceSetting.Statistics }, DeviceAbilities.OfThisBuild.Settings);
    }

    [Fact]
    public void On_a_device_with_nothing_to_buzz_the_card_offers_sound_music_and_the_statistics()
    {
        // An iPad has no Taptic Engine and some tablets have no vibrator (Haptics.DeviceHasNone): a vibration switch
        // there would change nothing, and the card offers no such switch.
        Assert.Equal(new[] { DeviceSetting.Sound, DeviceSetting.Music, DeviceSetting.Statistics }, DeviceAbilities.OfThisBuildWithoutVibrator.Settings);
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
