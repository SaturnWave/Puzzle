#nullable enable
using System.Collections.Generic;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the privacy card says: what the game keeps on the phone, that this build sends none of it anywhere, how
    /// many attempt records there are, and the question before they are deleted. The card has no drawing; its words
    /// are written in the design canvas' manner.
    /// </summary>
    /// <remarks>
    /// DRAFT, for the owner to approve before any public build. Every sentence is true of this build as it was checked
    /// on 2026-10-15: the game's code makes no network call and links no network, advertising, purchase or account
    /// library; Unity's services (analytics, ads, purchasing, crash and performance reports, engine diagnostics) are
    /// off in the project, and ProjectSetup switches Unity's own hardware statistics off; the progress
    /// (progress.json), the phone's id and settings (device.json) and the attempt records (attempts.jsonl) lie in the
    /// game's own data folder. Android's own backup, when the player has it on, may copy that folder into the
    /// player's backup, which the game neither asks for nor sees; the last sentence says so. Whatever changes one of
    /// these facts (an ad network, a purchase library, a service that reports) changes this text in the same work.
    /// No Unity dependency.
    /// </remarks>
    public static class PrivacyWords
    {
        /// <summary>The card's nameplate, which the settings card's row repeats.</summary>
        public const string Title = "Gizlilik ve veri";

        /// <summary>The card's text, a paragraph at a time.</summary>
        public static IReadOnlyList<string> Text { get; } = new[]
        {
            "Oyun her şeyi yalnızca bu telefonda tutar: ilerlemeni, cüzdanını, ayarlarını ve oynadığın denemelerin kayıtlarını.",
            "Bu sürüm hiçbir şeyi telefondan dışarı göndermez. Hesap, reklam ağı ve satın alma yoktur; Unity’nin istatistikleri kapalıdır.",
            "Kayıtlarda adın ya da telefonunun bilgisi yoktur, yalnızca bu telefona verilmiş rastgele bir kimlik vardır.",
            "Telefonunun yedeklemesi açıksa Android bu dosyaları kendi yedeğine katabilir.",
        };

        /// <summary>The button that deletes the records, after one question.</summary>
        public const string DeleteTheRecords = "Kayıtları sil";

        /// <summary>The question's answers.</summary>
        public const string KeepThem = "Vazgeç";
        public const string Delete = "Sil";

        /// <summary>What deleting the records leaves as it was, under the question.</summary>
        public const string WhatStays = "İlerlemen, cüzdanın ve ayarların yerinde kalır.";

        /// <summary>The line under the count once the records were deleted.</summary>
        public const string Deleted = "Bütün kayıtlar silindi.";

        /// <summary>The line under the count when the records cannot be read to count them.</summary>
        public const string CannotCount = "Kayıtlar şu an okunamıyor.";

        /// <summary>How many records the phone keeps: "Deneme kayıtları: 37".</summary>
        public static string RecordsKept(int count) => $"Deneme kayıtları: {NumberWords.Amount(count)}";

        /// <summary>The count when the records could not be read.</summary>
        public const string RecordsUncounted = "Deneme kayıtları: ?";

        /// <summary>The file's name and what a line of it is, under the count.</summary>
        public static string FileLine(string fileName) => $"{fileName} · her biten deneme bir satır";

        /// <summary>The question before the records are deleted: "37 kayıt silinsin mi?".</summary>
        public static string Question(int count) => $"{NumberWords.Amount(count)} kayıt silinsin mi?";
    }
}
