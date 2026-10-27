#nullable enable
using System.Collections.Generic;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the privacy card says, in the language the game speaks (<see cref="Speech"/>): what the game keeps on the
    /// phone, what its cloud save keeps on Unity's servers and how the platform links the account, what leaves the
    /// phone (the purchases, through the store, to Unity and to the game's server for verification; the play statistics,
    /// only with the player's yes), what comes back on a new phone and how the data is deleted, how many attempt records
    /// there are, and the question before they are deleted. The card has no drawing; its words are written in the design
    /// canvas' manner.
    /// </summary>
    /// <remarks>
    /// The text is the v1 draft of docs/research/privacy-v1.md §3.2 (2026-10-24) with the cloud save's paragraphs of
    /// docs/research/server-v1.md §6.5 (2026-10-27: paragraphs 1, 2 and the last changed, three added), waiting for the
    /// owner's approval; the first text, approved on 2026-10-16, said that the game sent nothing, which v1 no longer holds. The other
    /// languages translate it sentence by sentence, and are marked machine-made until a native speaker has read them.
    /// Every sentence is to be true of the build, as these facts make it: the progress (progress.json), the phone's id
    /// and settings (device.json) and the attempt records (attempts.jsonl) lie in the game's own data folder; with the
    /// "Bulut kaydı" switch on (the default) the progress and the wallet are synced to Unity Cloud Save under an
    /// anonymous Unity account, linked to Google Play Games or Game Center where the platform signs in (Cloud/CloudSaving),
    /// each payment's receipt goes to the game's Cloud Code module, which asks the store and keeps a ledger, and
    /// "Verilerimi sil" deletes the account and its data, keeping an anonymous trace of the purchases for 36 months
    /// (server/CloudCode); the switch off starts none of it; Google Play or the App Store takes every payment in its own sheet; Unity In-App
    /// Purchasing 5.4.4 sends Unity an event of every purchase attempt, which it offers no way to turn off
    /// (docs/research/iap-and-analytics.md §3.4); the play statistics go to Aptabase's European region only after the
    /// player's "İzin ver" (Statistics/PlayStatistics.cs), with the notes Statistics/PlayNote.cs lists and nothing that
    /// tells one phone from another; the phone's id goes into no note; there is no ad network and no advertising id. Android's own backup, when the player has it on, may copy the data folder into the player's
    /// backup, which the game neither asks for nor sees. Whatever changes one of these facts changes this text, in
    /// every language, in the same work. No Unity dependency.
    /// </remarks>
    public static class PrivacyWords
    {
        /// <summary>The card's nameplate, which the settings card's row repeats.</summary>
        public static string Title => Speech.Say(WordId.PrivacyTitle);

        /// <summary>
        /// The card's text, a paragraph at a time: what stays on the phone, the cloud save, the platform's link, the
        /// store, the verification of purchases, Unity's events, the play statistics and the choice about them, the
        /// records' id, the phone's backup, and what comes back, how the data is deleted and what is kept.
        /// </summary>
        public static IReadOnlyList<string> Text => new[]
        {
            Speech.Say(WordId.PrivacyParagraph1),
            Speech.Say(WordId.PrivacyParagraphCloud),
            Speech.Say(WordId.PrivacyParagraphPlatform),
            Speech.Say(WordId.PrivacyParagraph2),
            Speech.Say(WordId.PrivacyParagraphVerification),
            Speech.Say(WordId.PrivacyParagraphUnity),
            Speech.Say(WordId.PrivacyParagraphStatistics),
            Speech.Say(WordId.PrivacyParagraphStatisticsChoice),
            Speech.Say(WordId.PrivacyParagraph3),
            Speech.Say(WordId.PrivacyParagraph4),
            Speech.Say(WordId.PrivacyParagraphNoServer),
        };

        /// <summary>Which page of the text the card shows, between the arrows that turn its pages: "2 / 3".</summary>
        public static string PageOf(int page, int pages) =>
            Speech.Say(WordId.PrivacyPageOf, ("page", NumberWords.Amount(page)), ("pages", NumberWords.Amount(pages)));

        /// <summary>The button that deletes the records, after one question.</summary>
        public static string DeleteTheRecords => Speech.Say(WordId.PrivacyDeleteTheRecords);

        /// <summary>The question's answers.</summary>
        public static string KeepThem => Speech.Say(WordId.PrivacyKeep);
        public static string Delete => Speech.Say(WordId.PrivacyDelete);

        /// <summary>What deleting the records leaves as it was, under the question.</summary>
        public static string WhatStays => Speech.Say(WordId.PrivacyWhatStays);

        /// <summary>The line under the count once the records were deleted.</summary>
        public static string Deleted => Speech.Say(WordId.PrivacyDeleted);

        /// <summary>The line under the count when the records cannot be read to count them.</summary>
        public static string CannotCount => Speech.Say(WordId.PrivacyCannotCount);

        /// <summary>How many records the phone keeps: "Deneme kayıtları: 37".</summary>
        public static string RecordsKept(int count) => Speech.Say(WordId.PrivacyRecordsKept, ("count", NumberWords.Amount(count)));

        /// <summary>The count when the records could not be read.</summary>
        public static string RecordsUncounted => Speech.Say(WordId.PrivacyRecordsUncounted);

        /// <summary>The file's name and what a line of it is, under the count.</summary>
        public static string FileLine(string fileName) => Speech.Say(WordId.PrivacyFileLine, ("file", fileName));

        /// <summary>The question before the records are deleted: "37 kayıt silinsin mi?".</summary>
        public static string Question(int count) => Speech.SayCounted(WordId.PrivacyQuestion, count, ("count", NumberWords.Amount(count)));
    }
}
