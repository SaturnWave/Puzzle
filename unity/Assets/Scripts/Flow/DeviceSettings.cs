#nullable enable
using System;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// A setting the player turns on or off on the settings card, kept on the phone (device.json), not with the
    /// progress. A closed set.
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

        /// <summary>The game's one piece of music, heard on every screen ("Müzik"); the sounds of play do not depend on it.</summary>
        Music,

        /// <summary>
        /// The play statistics ("Oyun istatistikleri", <see cref="StatisticsChoice"/>): on only once the player has
        /// allowed them, on the question after the entry page or with this switch; off before the question is answered.
        /// </summary>
        Statistics,
    }

    /// <summary>
    /// What the player has said to the play statistics (Statistics/PlayStatistics.cs): asked once, after the entry page
    /// of the first start, and changed with the settings card's switch at any time. Nothing is sent unless the answer
    /// is <see cref="Allowed"/>. A closed set.
    /// </summary>
    public enum StatisticsChoice
    {
        /// <summary>Not asked yet: a first start, or a device file from before the question. Nothing is sent, and the question comes.</summary>
        NotAsked,

        /// <summary>"İzin ver" on the question, or the switch turned on: the game sends its notes.</summary>
        Allowed,

        /// <summary>"İzin verme", the way back on the question, or the switch turned off: nothing is sent.</summary>
        Declined,
    }

    /// <summary>
    /// What the player has set on this phone: each <see cref="DeviceSetting"/> on or off, the language the player
    /// chose on the language card, if they chose one, and their answer to the play statistics. A phone that never set
    /// anything has <see cref="Default"/>: everything on, as a player who opens a game for the first time expects, but
    /// the tester mode, which only the owner's testers turn on, and the statistics, which only the player's own "yes"
    /// turns on and which are not asked yet; and no language chosen, so the game speaks the phone's. A value: two
    /// settings with the same switches and the same choices are equal. This file has no Unity dependency, so the
    /// repository's tests compile it too.
    /// </summary>
    /// <remarks>
    /// The public constructors leave the music on, as every phone had it before the music came, and the statistics not
    /// asked; they are changed with <see cref="With"/> and <see cref="WithTheStatistics"/>, so that no call names a
    /// row of switches long enough to be given in the wrong order.
    /// </remarks>
    public sealed class DeviceSettings : IEquatable<DeviceSettings>
    {
        /// <summary>Settings with the music on and the tester mode off.</summary>
        public DeviceSettings(bool sound, bool vibration)
            : this(sound, vibration, testerMode: false)
        {
        }

        /// <summary>Settings with the music on and no language chosen: the game speaks the phone's.</summary>
        public DeviceSettings(bool sound, bool vibration, bool testerMode)
            : this(sound, vibration, testerMode, language: null)
        {
        }

        /// <summary>Settings with the music on.</summary>
        /// <param name="language">The language the player chose on the language card; nothing for the phone's.</param>
        public DeviceSettings(bool sound, bool vibration, bool testerMode, Language? language)
            : this(sound: sound, music: true, vibration: vibration, testerMode: testerMode, language: language, StatisticsChoice.NotAsked)
        {
        }

        private DeviceSettings(bool sound, bool music, bool vibration, bool testerMode, Language? language, StatisticsChoice statistics)
        {
            Sound = sound;
            Music = music;
            Vibration = vibration;
            TesterMode = testerMode;
            Language = language;
            Statistics = statistics;
        }

        /// <summary>The settings of a phone that never set anything, and of a device file from before the settings.</summary>
        public static DeviceSettings Default { get; } = new DeviceSettings(sound: true, vibration: true, testerMode: false, language: null);

        public bool Sound { get; }

        /// <summary>Whether the game's music is heard. On for a phone whose device file is older than the music (layout 5).</summary>
        public bool Music { get; }

        public bool Vibration { get; }

        /// <summary>Whether the player turned the tester mode on; it holds only in a build that has it (<see cref="Flow.TesterMode.HandlingFor"/>).</summary>
        public bool TesterMode { get; }

        /// <summary>The language the player chose on the language card; nothing while they never chose one, and the game speaks the phone's.</summary>
        public Language? Language { get; }

        /// <summary>What the player said to the play statistics; not asked for a phone whose device file is older than the question (layout 6).</summary>
        public StatisticsChoice Statistics { get; }

        public bool IsOn(DeviceSetting setting) => setting switch
        {
            DeviceSetting.Sound => Sound,
            DeviceSetting.Music => Music,
            DeviceSetting.Vibration => Vibration,
            DeviceSetting.TesterMode => TesterMode,
            DeviceSetting.Statistics => Statistics == StatisticsChoice.Allowed,
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "A setting the game does not know."),
        };

        /// <summary>
        /// These settings with one of them turned on or off. The statistics' switch answers their question: on is
        /// <see cref="StatisticsChoice.Allowed"/>, off <see cref="StatisticsChoice.Declined"/>, never "not asked".
        /// </summary>
        public DeviceSettings With(DeviceSetting setting, bool isOn) => setting switch
        {
            DeviceSetting.Sound => new DeviceSettings(sound: isOn, music: Music, vibration: Vibration, testerMode: TesterMode, language: Language, Statistics),
            DeviceSetting.Music => new DeviceSettings(sound: Sound, music: isOn, vibration: Vibration, testerMode: TesterMode, language: Language, Statistics),
            DeviceSetting.Vibration => new DeviceSettings(sound: Sound, music: Music, vibration: isOn, testerMode: TesterMode, language: Language, Statistics),
            DeviceSetting.TesterMode => new DeviceSettings(sound: Sound, music: Music, vibration: Vibration, testerMode: isOn, language: Language, Statistics),
            DeviceSetting.Statistics => WithTheStatistics(isOn ? StatisticsChoice.Allowed : StatisticsChoice.Declined),
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "A setting the game does not know."),
        };

        /// <summary>These settings with the player's answer to the play statistics; "not asked" for a device file older than the question.</summary>
        public DeviceSettings WithTheStatistics(StatisticsChoice choice) =>
            new DeviceSettings(sound: Sound, music: Music, vibration: Vibration, testerMode: TesterMode, language: Language, choice);

        /// <summary>These settings with the language the player chose on the language card.</summary>
        public DeviceSettings WithLanguage(Language chosen) => WithLanguageOrNone(chosen);

        /// <summary>These settings with no language chosen: the game speaks the phone's.</summary>
        public DeviceSettings WithThePhonesLanguage() => WithLanguageOrNone(null);

        public bool Equals(DeviceSettings? other) =>
            other != null && Sound == other.Sound && Music == other.Music && Vibration == other.Vibration && TesterMode == other.TesterMode
            && Language == other.Language && Statistics == other.Statistics;

        public override bool Equals(object? obj) => Equals(obj as DeviceSettings);

        public override int GetHashCode() =>
            ((Sound ? 1 : 0) | (Vibration ? 2 : 0) | (TesterMode ? 4 : 0) | (Music ? 8 : 0)) + 16 * (int)Statistics
            + 64 * (Language is Language chosen ? 1 + (int)chosen : 0);

        public override string ToString() =>
            $"sound {(Sound ? "on" : "off")}, music {(Music ? "on" : "off")}, vibration {(Vibration ? "on" : "off")}"
            + $"{(TesterMode ? ", tester mode on" : string.Empty)}"
            + $", {(Language is Language chosen ? $"language {Languages.CodeOf(chosen)}" : "the phone's language")}"
            + $", statistics {WordsOf(Statistics)}";

        private static string WordsOf(StatisticsChoice choice) => choice switch
        {
            StatisticsChoice.NotAsked => "not asked",
            StatisticsChoice.Allowed => "allowed",
            StatisticsChoice.Declined => "declined",
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "An answer to the statistics the game does not know."),
        };

        private DeviceSettings WithLanguageOrNone(Language? language) =>
            new DeviceSettings(sound: Sound, music: Music, vibration: Vibration, testerMode: TesterMode, language: language, Statistics);
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
