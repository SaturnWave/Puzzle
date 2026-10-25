#nullable enable
using System.Collections.Generic;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the question about the play statistics says, in the language the game speaks (<see cref="Speech"/>): the
    /// card that comes down once, over the hall, after the entry page of the first start (Screens/ConsentCard.cs).
    /// Its nameplate is the settings row's name, so the player finds the same words where the choice is changed; under
    /// it the question, what is counted and by whom, what never goes, that no is a full answer, and the two answers,
    /// which are as large and as bright as each other.
    /// </summary>
    /// <remarks>
    /// The Turkish text is the v1 draft of docs/research/privacy-v1.md §3.3 (2026-10-24), waiting for the owner's
    /// approval; the other languages translate it sentence by sentence, machine-made until a native speaker has read
    /// them. What it says of the notes is to stay what Statistics/PlayNote.cs sends, as the privacy card's text is
    /// (<see cref="PrivacyWords"/>). No Unity dependency.
    /// </remarks>
    public static class ConsentWords
    {
        /// <summary>The card's nameplate: "Oyun istatistikleri", as the settings row names the switch.</summary>
        public static string Title => Speech.Say(WordId.SettingStatistics);

        /// <summary>The question, under the nameplate.</summary>
        public static string Question => Speech.Say(WordId.ConsentQuestion);

        /// <summary>The card's text, a paragraph at a time: what is counted, what never goes, and that no is a full answer.</summary>
        public static IReadOnlyList<string> Text => new[]
        {
            Speech.Say(WordId.ConsentParagraph1),
            Speech.Say(WordId.ConsentParagraph2),
            Speech.Say(WordId.ConsentParagraph3),
        };

        /// <summary>The two answers.</summary>
        public static string Allow => Speech.Say(WordId.ConsentAllow);

        public static string Decline => Speech.Say(WordId.ConsentDecline);
    }
}
