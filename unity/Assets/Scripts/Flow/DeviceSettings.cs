#nullable enable
using System;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// A setting the player turns on or off on the settings card, kept on the phone (device.json), not with the
    /// progress. A closed set: music joins it once the game has a track to play.
    /// </summary>
    public enum DeviceSetting
    {
        /// <summary>The sounds of play: pieces that pop, boosters, the finale ("Ses").</summary>
        Sound,

        /// <summary>The phone's short buzz under a special piece or a win ("Titreşim").</summary>
        Vibration,
    }

    /// <summary>
    /// What the player has set on this phone: each <see cref="DeviceSetting"/> on or off. A phone that never set
    /// anything has <see cref="Default"/>: everything on, as a player who opens a game for the first time expects.
    /// A value: two settings with the same switches are equal.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class DeviceSettings : IEquatable<DeviceSettings>
    {
        public DeviceSettings(bool sound, bool vibration)
        {
            Sound = sound;
            Vibration = vibration;
        }

        /// <summary>The settings of a phone that never set anything, and of a device file from before the settings.</summary>
        public static DeviceSettings Default { get; } = new DeviceSettings(sound: true, vibration: true);

        public bool Sound { get; }

        public bool Vibration { get; }

        public bool IsOn(DeviceSetting setting) => setting switch
        {
            DeviceSetting.Sound => Sound,
            DeviceSetting.Vibration => Vibration,
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "A setting the game does not know."),
        };

        /// <summary>These settings with one of them turned on or off.</summary>
        public DeviceSettings With(DeviceSetting setting, bool isOn) => setting switch
        {
            DeviceSetting.Sound => new DeviceSettings(isOn, Vibration),
            DeviceSetting.Vibration => new DeviceSettings(Sound, isOn),
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "A setting the game does not know."),
        };

        public bool Equals(DeviceSettings? other) => other != null && Sound == other.Sound && Vibration == other.Vibration;

        public override bool Equals(object? obj) => Equals(obj as DeviceSettings);

        public override int GetHashCode() => (Sound ? 1 : 0) | (Vibration ? 2 : 0);

        public override string ToString() => $"sound {(Sound ? "on" : "off")}, vibration {(Vibration ? "on" : "off")}";
    }

    /// <summary>Where the player's settings are kept: the phone's device file, or memory for the Editor's tools and for tests.</summary>
    public interface ISettingsStore
    {
        /// <summary>The settings as last kept; those of a phone that never set anything where none ever were.</summary>
        DeviceSettings Settings { get; }

        /// <summary>
        /// Keeps the settings at once, so that the game finds them after a restart. They are the store's from this
        /// call on, also when they could not be written: this run goes on with them.
        /// </summary>
        /// <exception cref="System.IO.IOException">The settings could not be written now; they are written with the next change.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not write them now; as after an <see cref="System.IO.IOException"/>.</exception>
        void Keep(DeviceSettings settings);
    }

    /// <summary>Settings that are forgotten when the game closes: for the Editor's tools and for tests, which must not touch a player's own file.</summary>
    public sealed class SettingsInMemory : ISettingsStore
    {
        public SettingsInMemory(DeviceSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public DeviceSettings Settings { get; private set; }

        public void Keep(DeviceSettings settings) => Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }
}
