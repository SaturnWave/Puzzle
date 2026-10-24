#nullable enable
using System;
using System.Globalization;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the entry page says, in the language the game speaks (<see cref="Speech"/>): the Turkish words are the
    /// design canvas' (EntryTR). The title is the game's working name, and the badge under it says so. No Unity dependency.
    /// </summary>
    public static class EntryWords
    {
        public static string Title => Speech.Say(WordId.EntryTitle);

        /// <summary>The badge under the title, in small capitals as the table writes it.</summary>
        public static string WorkingTitle => Speech.Say(WordId.EntryWorkingTitle);

        public static string Subtitle => Speech.Say(WordId.EntrySubtitle);

        public static string TapToStart => Speech.Say(WordId.EntryTapToStart);

        public static string Studio => Speech.Say(WordId.EntryStudio);

        /// <summary>The bar's line: "Yükleniyor… %40".</summary>
        /// <exception cref="ArgumentOutOfRangeException">The share is not from 0 to 100.</exception>
        public static string Loading(int percent) =>
            percent >= 0 && percent <= 100
                ? Speech.Say(WordId.EntryLoading, ("percent", percent.ToString(CultureInfo.InvariantCulture)))
                : throw new ArgumentOutOfRangeException(nameof(percent), percent, "A share of the start-up is from 0 to 100.");

        /// <summary>What the bar's line says in its place when the game could not start: "Oyun açılamadı; yeniden başlatın".</summary>
        public static string CouldNotStart => Speech.Say(WordId.EntryCouldNotStart);

        /// <summary>The foot's version line: "Sürüm 1.0 · test sürümü".</summary>
        public static string Version(string gameVersion) => Speech.Say(WordId.EntryVersion, ("version", gameVersion));
    }
}
