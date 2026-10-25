#nullable enable
using System;
using Puzzle.Client.Words;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the settings card, the language card and the support card say, in the language the game speaks
    /// (<see cref="Speech"/>). The settings card's Turkish words are the design canvas' (SettingsTR) where they are
    /// true of this build; the line under "Gizlilik ve veri" is not the canvas' ("Yalnızca anonim oyun verisi
    /// toplanır"): it says what leaves the phone, the purchases and, while the player allows them, the play
    /// statistics, which carry no id. The language card and the support card have no drawing, and their words are
    /// written in the canvas' manner. No Unity dependency.
    /// </summary>
    public static class SettingsWords
    {
        /// <summary>The settings card's nameplate.</summary>
        public static string Title => Speech.Say(WordId.SettingsTitle);

        /// <summary>The language row, which opens the language card, and that card's nameplate.</summary>
        public static string Language => Speech.Say(WordId.SettingsLanguage);

        /// <summary>The row that opens the privacy card.</summary>
        public static string Privacy => PrivacyWords.Title;

        /// <summary>
        /// The privacy row's smaller line: what leaves the phone as the player has set it. Only the purchases, while
        /// the play statistics are not allowed (not asked yet, or declined); the purchases and statistics without an
        /// id, once they are.
        /// </summary>
        public static string PrivacyLine(DeviceSettings settings) => settings.IsOn(DeviceSetting.Statistics)
            ? Speech.Say(WordId.SettingsPrivacyLineWithStatistics)
            : Speech.Say(WordId.SettingsPrivacyLine);

        /// <summary>The row that opens the support card, and that card's nameplate.</summary>
        public static string Support => Speech.Say(WordId.SettingsSupport);

        /// <summary>The support card's rows: what to tell when a problem is reported.</summary>
        public static string PlayerIdLabel => Speech.Say(WordId.SupportPlayerId);
        public static string GameVersionLabel => Speech.Say(WordId.SupportGameVersion);
        public static string ContentVersionLabel => Speech.Say(WordId.SupportContentVersion);
        public static string RecordsFileLabel => Speech.Say(WordId.SupportRecordsFile);
        public static string RecordsKeptInMemory => Speech.Say(WordId.SupportRecordsInMemory);
        public static string WhatToTell => Speech.Say(WordId.SupportWhatToTell);

        /// <summary>
        /// The question the support card asks after seven taps in a row on its version row, in a test build, the line
        /// under it, and its two answers.
        /// </summary>
        public static string TesterModeQuestion => Speech.Say(WordId.TesterQuestion);
        public static string TesterModeTells => Speech.Say(WordId.TesterTells);
        public static string TurnTesterModeOn => Speech.Say(WordId.TesterTurnOn);
        public static string LeaveTesterModeOff => Speech.Say(WordId.TesterLeaveOff);

        /// <summary>
        /// The name of the language the game speaks, in that language: the settings row's value. The language card
        /// names each language it offers in that language's own words (<see cref="OwnNameOf"/>).
        /// </summary>
        public static string LanguageSpoken => Speech.Say(WordId.LanguageOwnName);

        /// <summary>A language's name in its own words, from its own table: "Türkçe", "English", "한국어".</summary>
        public static string OwnNameOf(Wording wording) => wording.Words.WordsOf(WordId.LanguageOwnName, PluralForm.Other);

        /// <summary>The name of a setting's row.</summary>
        public static string NameOf(DeviceSetting setting) => setting switch
        {
            DeviceSetting.Sound => Speech.Say(WordId.SettingSound),
            DeviceSetting.Music => Speech.Say(WordId.SettingMusic),
            DeviceSetting.Vibration => Speech.Say(WordId.SettingVibration),
            DeviceSetting.TesterMode => Speech.Say(WordId.SettingTesterMode),
            DeviceSetting.Statistics => Speech.Say(WordId.SettingStatistics),
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "A setting without a name."),
        };

        /// <summary>
        /// The player's id as the cards show it: the first eight characters of the phone's id (which has at least
        /// eight) in two groups of four, in capitals ("6F1C-0E9A"), enough to tell one tester's records from another's.
        /// </summary>
        public static string PlayerId(DeviceId device)
        {
            string id = device.Value.ToUpperInvariant();
            return $"{id.Substring(0, 4)}-{id.Substring(4, 4)}";
        }

        /// <summary>The settings card's foot: "Oyuncu kimliği: 6F1C-0E9A · Sürüm 1.0".</summary>
        public static string Foot(DeviceId device, string gameVersion) =>
            Speech.Say(WordId.SettingsFoot, ("playerId", PlayerId(device)), ("version", gameVersion));

        /// <summary>The support card's content version: "sürüm 1".</summary>
        public static string ContentVersionOf(ContentVersion content) =>
            Speech.Say(WordId.SupportContentVersionValue, ("version", content.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        /// <summary>
        /// The line under the foot that says what kind of build this is, from the build's marks in the economy's
        /// settings: "Test sürümü · reklamlar canlandırma, satın alma kapalı", or "… satın alma mağazadan" for a build
        /// that sells through the platform's store. A build with real ads must say so here before it can be made.
        /// </summary>
        public static string BuildLine(AdSource ads, RealMoneySales realMoney)
        {
            string adsAre = ads switch
            {
                AdSource.Simulated => Speech.Say(WordId.BuildAdsSimulated),
                _ => throw new ArgumentOutOfRangeException(nameof(ads), ads, "A source of ads the settings card cannot name."),
            };
            string buying = realMoney switch
            {
                RealMoneySales.NotAvailable => Speech.Say(WordId.BuildNoBuying),
                RealMoneySales.ThroughTheStore => Speech.Say(WordId.BuildStoreBuying),
                _ => throw new ArgumentOutOfRangeException(nameof(realMoney), realMoney, "A way of selling the settings card cannot name."),
            };
            return Speech.Say(WordId.SettingsBuildLine, ("ads", adsAre), ("buying", buying));
        }
    }
}
