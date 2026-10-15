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
        /// <param name="settings">The settings the build honours, in the order the card lists them (SettingsTR: sound, then vibration).</param>
        public DeviceAbilities(params DeviceSetting[] settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// What this build honours: nothing yet. The game makes no sound and never vibrates, so the card has no
        /// switch; the sounds and the buzz of play bring <see cref="DeviceSetting.Sound"/> and
        /// <see cref="DeviceSetting.Vibration"/> here when they come.
        /// </summary>
        public static DeviceAbilities OfThisBuild { get; } = new DeviceAbilities();

        /// <summary>The settings whose rows the card draws, in its order.</summary>
        public IReadOnlyList<DeviceSetting> Settings { get; }
    }
}
