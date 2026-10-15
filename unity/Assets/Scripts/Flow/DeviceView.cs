#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the settings card and the cards it opens show of the phone and of this build: the player's settings and
    /// which of them the build honours, the phone's id, the game's version and its levels', the attempt records kept
    /// and where, and the build's marks. Whoever owns the museum makes it, and makes it anew when the player has
    /// changed something of it (a setting, the records deleted). No Unity dependency.
    /// </summary>
    public sealed class DeviceView
    {
        /// <param name="settingRows">The settings whose rows the card draws: those the build honours (<see cref="DeviceAbilities"/>).</param>
        /// <param name="gameVersion">The game's version as the build states it ("1.0").</param>
        /// <param name="content">The newest version of the levels the game carries.</param>
        /// <param name="recordsKept">How many attempt records the phone keeps; nothing when they could not be read to count them.</param>
        /// <param name="recordsFile">Where the records lie: the file's full path; nothing for records kept in memory (the tools').</param>
        /// <param name="ads">How this build shows an ad, from its marks.</param>
        /// <param name="realMoney">What this build sells for money, from its marks.</param>
        public DeviceView(
            DeviceSettings settings, IReadOnlyList<DeviceSetting> settingRows, DeviceId device, string gameVersion, ContentVersion content,
            int? recordsKept, string? recordsFile, AdSource ads, RealMoneySales realMoney)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            SettingRows = settingRows ?? throw new ArgumentNullException(nameof(settingRows));
            Device = device ?? throw new ArgumentNullException(nameof(device));
            GameVersion = gameVersion ?? throw new ArgumentNullException(nameof(gameVersion));
            Content = content;
            RecordsKept = recordsKept;
            RecordsFile = recordsFile;
            Ads = ads;
            RealMoney = realMoney;
        }

        public DeviceSettings Settings { get; }

        public IReadOnlyList<DeviceSetting> SettingRows { get; }

        public DeviceId Device { get; }

        public string GameVersion { get; }

        public ContentVersion Content { get; }

        /// <summary>How many attempt records the phone keeps; nothing when they could not be read to count them.</summary>
        public int? RecordsKept { get; }

        /// <summary>Where the records lie: the file's full path; nothing for records kept in memory.</summary>
        public string? RecordsFile { get; }

        public AdSource Ads { get; }

        public RealMoneySales RealMoney { get; }

        /// <summary>True when there is a record to delete.</summary>
        public bool HasRecords => RecordsKept > 0;
    }
}
