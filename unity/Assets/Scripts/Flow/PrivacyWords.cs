#nullable enable
using System.Collections.Generic;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the privacy card says, in the language the game speaks (<see cref="Speech"/>): what the game keeps on the
    /// phone, that this build sends none of it anywhere, how many attempt records there are, and the question before
    /// they are deleted. The card has no drawing; its words are written in the design canvas' manner.
    /// </summary>
    /// <remarks>
    /// The Turkish text was approved by the owner on 2026-10-16; the other languages translate it sentence by
    /// sentence, and are marked machine-made until a native speaker has read them. Every sentence is true of this
    /// build as it was checked on 2026-10-15: the game's code makes no network call and links no network,
    /// advertising, purchase or account library; the package does not ask for the internet permission
    /// (AndroidBuild.Permissions withdraws it); Unity's services (analytics, ads, purchasing, crash and performance
    /// reports, engine diagnostics) are off in the project, and ProjectSetup switches Unity's own hardware
    /// statistics off; the progress (progress.json), the phone's id and settings (device.json) and the attempt
    /// records (attempts.jsonl) lie in the game's own data folder. Android's own backup, when the player has it on,
    /// may copy that folder into the player's backup, which the game neither asks for nor sees; the last sentence
    /// says so. Whatever changes one of these facts (an ad network, a purchase library, a service that reports)
    /// changes this text, in every language, in the same work. No Unity dependency.
    /// </remarks>
    public static class PrivacyWords
    {
        /// <summary>The card's nameplate, which the settings card's row repeats.</summary>
        public static string Title => Speech.Say(WordId.PrivacyTitle);

        /// <summary>The card's text, a paragraph at a time.</summary>
        public static IReadOnlyList<string> Text => new[]
        {
            Speech.Say(WordId.PrivacyParagraph1),
            Speech.Say(WordId.PrivacyParagraph2),
            Speech.Say(WordId.PrivacyParagraph3),
            Speech.Say(WordId.PrivacyParagraph4),
        };

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
