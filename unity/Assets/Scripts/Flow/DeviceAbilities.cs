#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What this build of the game can do on the phone, as far as the settings card is concerned: a setting's row is
    /// drawn only for what the build honours, so the card never offers a switch that changes nothing.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class DeviceAbilities
    {
        /// <param name="settings">The settings the build honours, in the order the card lists them (SettingsTR: sound, music, vibration).</param>
        public DeviceAbilities(params DeviceSetting[] settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// What this build honours: its sounds (stand-ins, SoundCue.cs), its one piece of music (MusicPlayer) and the
        /// short buzz under a few of the sounds, so the card shows the three switches the design draws, in its order.
        /// </summary>
        public static DeviceAbilities OfThisBuild { get; } = new DeviceAbilities(DeviceSetting.Sound, DeviceSetting.Music, DeviceSetting.Vibration);

        /// <summary>The settings whose rows the card draws, in its order.</summary>
        public IReadOnlyList<DeviceSetting> Settings { get; }
    }
}
