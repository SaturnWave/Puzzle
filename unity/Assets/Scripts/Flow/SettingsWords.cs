#nullable enable
using System;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the settings card and the support card say, in the player's language. The settings card's words are the
    /// design canvas' (SettingsTR) where they are true of this build; the line under "Gizlilik ve veri" is not the
    /// canvas' ("Yalnızca anonim oyun verisi toplanır"), because this build gathers nothing. The support card has no
    /// drawing, and its words are written in the canvas' manner. No Unity dependency.
    /// </summary>
    public static class SettingsWords
    {
        /// <summary>The settings card's nameplate.</summary>
        public const string Title = "Ayarlar";

        /// <summary>The language row, which only says the one language there is.</summary>
        public const string Language = "Dil";
        public const string Turkish = "Türkçe";

        /// <summary>
        /// The row that opens the privacy card, and its smaller line: what the game does with the data, which holds
        /// beside Android's own backup that the privacy card tells of.
        /// </summary>
        public const string Privacy = PrivacyWords.Title;
        public const string PrivacyLine = "Oyun hiçbir şey göndermez";

        /// <summary>The row that opens the support card, and that card's nameplate.</summary>
        public const string Support = "Destek";

        /// <summary>The support card's rows: what to tell when a problem is reported.</summary>
        public const string PlayerIdLabel = "Oyuncu kimliği";
        public const string GameVersionLabel = "Sürüm";
        public const string ContentVersionLabel = "Bölüm içeriği";
        public const string RecordsFileLabel = "Kayıt dosyası";
        public const string RecordsKeptInMemory = "bu oturumda bellekte";
        public const string WhatToTell = "Bir sorun bildirirken oyuncu kimliğini ve sürümü söyle.";

        /// <summary>
        /// The question the support card asks after seven taps in a row on its version row, in a test build, the line
        /// under it, and its two answers.
        /// </summary>
        public const string TesterModeQuestion = "Test modu açılsın mı?";
        public const string TesterModeTells = "Altın ve can sınırsız olur; Ayarlar'dan kapatılır.";
        public const string TurnTesterModeOn = "Aç";
        public const string LeaveTesterModeOff = "Vazgeç";

        /// <summary>The name of a setting's row.</summary>
        public static string NameOf(DeviceSetting setting) => setting switch
        {
            DeviceSetting.Sound => "Ses",
            DeviceSetting.Vibration => "Titreşim",
            DeviceSetting.TesterMode => "Test modu",
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
        public static string Foot(DeviceId device, string gameVersion) => $"{PlayerIdLabel}: {PlayerId(device)} · {GameVersionLabel} {gameVersion}";

        /// <summary>The support card's content version: "sürüm 1".</summary>
        public static string ContentVersionOf(ContentVersion content) => $"sürüm {content.Value}";

        /// <summary>
        /// The line under the foot that says what kind of build this is, from the build's marks in the economy's
        /// settings: "Test sürümü · reklamlar canlandırma, satın alma kapalı". Each mark has one value today, a test
        /// build's; a build with real ads or real purchases must say so here before it can be made.
        /// </summary>
        public static string BuildLine(AdSource ads, RealMoneySales realMoney)
        {
            string adsAre = ads switch
            {
                AdSource.Simulated => "reklamlar canlandırma",
                _ => throw new ArgumentOutOfRangeException(nameof(ads), ads, "A source of ads the settings card cannot name."),
            };
            string buying = realMoney switch
            {
                RealMoneySales.NotAvailable => "satın alma kapalı",
                _ => throw new ArgumentOutOfRangeException(nameof(realMoney), realMoney, "A way of selling the settings card cannot name."),
            };
            return $"Test sürümü · {adsAre}, {buying}";
        }
    }
}
