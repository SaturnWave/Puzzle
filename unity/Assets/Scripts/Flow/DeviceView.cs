#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Cloud;
using Puzzle.Client.Words;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the settings card and the cards it opens show of the phone and of this build: the player's settings and
    /// which of them the build honours, the phone's id, the game's version and its levels', the attempt records kept
    /// and where, the build's marks, which also say whether the build has the tester mode, the language the game
    /// speaks among those it offers, and the account and its cloud save. Whoever owns the museum makes it, and makes it
    /// anew when the player has changed something of it (a setting, the records deleted, the language) and when the
    /// cloud's view changed. No Unity dependency.
    /// </summary>
    public sealed class DeviceView
    {
        /// <param name="honoured">The settings the build honours (<see cref="DeviceAbilities"/>), in the card's order.</param>
        /// <param name="gameVersion">The game's version as the build states it ("1.0").</param>
        /// <param name="content">The newest version of the levels the game carries.</param>
        /// <param name="recordsKept">How many attempt records the phone keeps; nothing when they could not be read to count them.</param>
        /// <param name="recordsFile">Where the records lie: the file's full path; nothing for records kept in memory (the tools').</param>
        /// <param name="ads">How this build shows an ad, from its marks.</param>
        /// <param name="realMoney">What this build sells for money, from its marks.</param>
        /// <param name="speaking">The language the game speaks: the player's choice or the phone's, among those offered.</param>
        /// <param name="offered">The languages the language card offers, those whose tables are complete, in the card's order.</param>
        /// <param name="account">The account and its cloud save as the cloud shows them; nothing for a session without a cloud (the Editor's tools).</param>
        /// <exception cref="ArgumentException">The game speaks a language it does not offer.</exception>
        public DeviceView(
            DeviceSettings settings, IReadOnlyList<DeviceSetting> honoured, DeviceId device, string gameVersion, ContentVersion content,
            int? recordsKept, string? recordsFile, AdSource ads, RealMoneySales realMoney, Language speaking, IReadOnlyList<LanguageOffer> offered,
            AccountView? account = null)
        {
            Account = account ?? AccountView.NoCloud;
            Offered = offered ?? throw new ArgumentNullException(nameof(offered));
            if (!offered.Any(offer => offer.Language == speaking))
            {
                throw new ArgumentException($"The game speaks {Languages.CodeOf(speaking)}, which the language card does not offer.", nameof(speaking));
            }

            Speaking = speaking;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Honoured = honoured ?? throw new ArgumentNullException(nameof(honoured));
            Device = device ?? throw new ArgumentNullException(nameof(device));
            GameVersion = gameVersion ?? throw new ArgumentNullException(nameof(gameVersion));
            Content = content;
            RecordsKept = recordsKept;
            RecordsFile = recordsFile;
            Ads = ads;
            RealMoney = realMoney;
        }

        public DeviceSettings Settings { get; }

        /// <summary>The account and its cloud save, as the settings' account row and the account card show them.</summary>
        public AccountView Account { get; }

        /// <summary>True while the cloud save sends the progress: a build with the cloud, its switch on.</summary>
        public bool SendsTheCloudSave => Account.HasTheSwitch && Account.IsSwitchedOn;

        /// <summary>The language the game speaks, which the settings row names and the language card marks.</summary>
        public Language Speaking { get; }

        /// <summary>The languages the language card offers, each named in its own words.</summary>
        public IReadOnlyList<LanguageOffer> Offered { get; }

        /// <summary>The name the settings row gives the language the game speaks: its own.</summary>
        public string SpeakingName => Offered.First(offer => offer.Language == Speaking).OwnName;

        /// <summary>The settings the build honours, in the card's order.</summary>
        public IReadOnlyList<DeviceSetting> Honoured { get; }

        /// <summary>The settings whose rows the settings card draws: those the build honours, and under them the tester mode's while it is on.</summary>
        public IReadOnlyList<DeviceSetting> SettingRows => IsInTesterMode ? Honoured.Append(DeviceSetting.TesterMode).ToArray() : Honoured;

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

        /// <summary>True while the tester mode holds: turned on, in a build that has it.</summary>
        public bool IsInTesterMode => TesterMode.HandlingFor(Settings, Ads, RealMoney) == WalletHandling.Unlimited;

        /// <summary>True when the support card may ask whether to turn the tester mode on: the build has it, and it is off.</summary>
        public bool CanTurnOnTheTesterMode => TesterMode.IsOfferedBy(Ads, RealMoney) && !Settings.TesterMode;
    }
}
