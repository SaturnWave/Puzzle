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

        /// <summary>
        /// The tester mode ("Test modu", <see cref="Flow.TesterMode"/>): coins and lives without end in a test build.
        /// Off on a first start; turned on from the support card, and its row shows on the settings card only while it is on.
        /// </summary>
        TesterMode,
    }

    /// <summary>
    /// What the player has set on this phone: each <see cref="DeviceSetting"/> on or off. A phone that never set
    /// anything has <see cref="Default"/>: everything on, as a player who opens a game for the first time expects,
    /// but the tester mode, which only the owner's testers turn on. A value: two settings with the same switches are equal.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class DeviceSettings : IEquatable<DeviceSettings>
    {
        /// <summary>Settings with the tester mode off.</summary>
        public DeviceSettings(bool sound, bool vibration)
            : this(sound, vibration, testerMode: false)
        {
        }

        public DeviceSettings(bool sound, bool vibration, bool testerMode)
        {
            Sound = sound;
            Vibration = vibration;
            TesterMode = testerMode;
        }

        /// <summary>The settings of a phone that never set anything, and of a device file from before the settings.</summary>
        public static DeviceSettings Default { get; } = new DeviceSettings(sound: true, vibration: true, testerMode: false);

        public bool Sound { get; }

        public bool Vibration { get; }

        /// <summary>Whether the player turned the tester mode on; it holds only in a build that has it (<see cref="Flow.TesterMode.HandlingFor"/>).</summary>
        public bool TesterMode { get; }

        public bool IsOn(DeviceSetting setting) => setting switch
        {
            DeviceSetting.Sound => Sound,
            DeviceSetting.Vibration => Vibration,
            DeviceSetting.TesterMode => TesterMode,
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "A setting the game does not know."),
        };

        /// <summary>These settings with one of them turned on or off.</summary>
        public DeviceSettings With(DeviceSetting setting, bool isOn) => setting switch
        {
            DeviceSetting.Sound => new DeviceSettings(isOn, Vibration, TesterMode),
            DeviceSetting.Vibration => new DeviceSettings(Sound, isOn, TesterMode),
            DeviceSetting.TesterMode => new DeviceSettings(Sound, Vibration, isOn),
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "A setting the game does not know."),
        };

        public bool Equals(DeviceSettings? other) =>
            other != null && Sound == other.Sound && Vibration == other.Vibration && TesterMode == other.TesterMode;

        public override bool Equals(object? obj) => Equals(obj as DeviceSettings);

        public override int GetHashCode() => (Sound ? 1 : 0) | (Vibration ? 2 : 0) | (TesterMode ? 4 : 0);

        public override string ToString() =>
            $"sound {(Sound ? "on" : "off")}, vibration {(Vibration ? "on" : "off")}{(TesterMode ? ", tester mode on" : string.Empty)}";
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
