#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the shop, the hall's gift frame and the cards that answer them say, in the player's language. The words
    /// are the design canvas' (ShopTR, HomeTR) where it has them; the cards have no drawing, and theirs are written in
    /// the canvas' manner. No Unity dependency.
    /// </summary>
    public static class ShopWords
    {
        /// <summary>The sign over the shop, and its three sections.</summary>
        public const string Title = "Mağaza";
        public const string Coins = "ALTIN";
        public const string Boosters = "GÜÇLENDİRİCİLER";
        public const string Free = "ÜCRETSİZ";

        /// <summary>The button of a pack of coins, which a test build does not sell.</summary>
        public const string ComingSoon = "Yakında";

        /// <summary>The blue button of the free strip, and what it says once the day's ads are used up.</summary>
        public const string Watch = "İzle";
        public const string TomorrowAgain = "Yarın yine";

        /// <summary>The way to the shop under the dimmed button of a card that offers a booster the coins do not cover.</summary>
        public const string ToTheShop = "Mağazaya git";

        /// <summary>The hall's gift frame while the daily reward waits.</summary>
        public const string Gift = "Hediye";

        /// <summary>The card a pack of coins answers with: a test build sells nothing for money.</summary>
        public const string PacksTitle = "Altın paketleri";
        public const string NotOnSale = "Bu test sürümünde satın alma yok.";
        public const string WhereCoinsComeFrom = "Altın, bölüm kazanarak ve reklam izleyerek gelir.";

        /// <summary>The card of a set the coins do not cover.</summary>
        public const string CoinsShortTitle = "Altın yetmiyor";
        public const string GatherCoins = "Bölüm kazanarak ve reklam izleyerek altın topla.";

        /// <summary>The card of a set that was bought.</summary>
        public const string BoughtTitle = "Satın alındı";

        /// <summary>The card of the daily reward, given or not due yet.</summary>
        public const string GiftTitle = "Günün hediyesi";
        public const string GiftTaken = "Bugünkü hediyeni aldın.";

        /// <summary>The tray's words over the reward of a painting that has just become whole.</summary>
        public const string PaintingsReward = "Tablo tamamlandı! İşte ödülün";

        /// <summary>The card of an ad for coins on a day that has none left.</summary>
        public const string NoAdTitle = "Bugünlük bitti";
        public const string NoAdLeft = "Bugünkü ücretsiz altınların bitti.";
        public const string ComeTomorrow = "Yarın yine gel.";

        /// <summary>What a set is called in the shop, by the settings' name for it.</summary>
        /// <exception cref="ArgumentException">The settings name a set the game has no words for: a new set needs its name here first.</exception>
        public static string NameOf(BoosterSet set) => set.Name switch
        {
            "starter" => "Başlangıç seti",
            "master" => "Usta seti",
            _ => throw new ArgumentException($"The settings' set \"{set.Name}\" has no name in the player's language.", nameof(set)),
        };

        /// <summary>A booster's name, whether it is taken before a level or used during play.</summary>
        public static string NameOf(BoosterKind booster) => booster.IsStart()
            ? LevelStartWords.NameOf(booster, LevelStart.Rules.StartBoosterExtraMoves)
            : BoosterWords.NameOf(booster);

        /// <summary>How many of a booster a set holds, under its disc: "×3".</summary>
        public static string Times(int count) => $"×{Number(count)}";

        /// <summary>How many of something a reward gives, under its picture: "+1", "+80".</summary>
        public static string More(long count) => $"+{NumberWords.Amount(count)}";

        /// <summary>The free strip's line: "Reklam izle, 4 altın kazan".</summary>
        public static string WatchForCoins(Coins coins) => $"Reklam izle, {NumberWords.Amount(coins.Amount)} altın kazan";

        /// <summary>The free strip's small capitals: how many ads are left today, or that none is.</summary>
        public static string FreeToday(int adsLeft) => adsLeft > 0 ? $"{Free} · BUGÜN {Number(adsLeft)} HAK" : $"{Free} · BUGÜNLÜK BİTTİ";

        /// <summary>The gift frame's pill while the next reward waits for the player's midnight: "5 sa 12 dk".</summary>
        public static string GiftIn(TimeSpan left) => NumberWords.HoursAndMinutes(left);

        /// <summary>The card of a set the coins do not cover: "Usta seti için 5.050 altın daha gerek."</summary>
        public static string CoinsShortFor(BoosterSet set, Coins missing) => $"{NameOf(set)} için {NumberWords.Amount(missing.Amount)} altın daha gerek.";

        /// <summary>The card of a set that was bought: "Usta seti çantanda."</summary>
        public static string InYourBag(BoosterSet set) => $"{NameOf(set)} çantanda.";

        /// <summary>The line under the boosters a reward or a set gave: their names when one kind, how many otherwise.</summary>
        public static string WhatCame(Reward reward) => reward.Boosters.Count == 1
            ? NameOf(reward.Boosters[0])
            : $"{Number(reward.Boosters.Count)} güçlendirici";

        /// <summary>Where the boosters just received are used: before a level, during one, or, for a mix, how many came.</summary>
        public static string HowToUse(Reward reward) =>
            reward.Boosters.Count > 0 && reward.Boosters.All(BoosterKinds.IsStart) ? "Bölüm kartında seçip kullan."
            : reward.Boosters.Count > 0 && reward.Boosters.All(BoosterKinds.IsInLevel) ? "Bölümde raftan seçip kullan."
            : WhatCame(reward);

        /// <summary>When the next daily reward comes, said on its card: "Yarın yeni bir hediye var." for a reward a day.</summary>
        public static string NextGiftAfter(int days) => days == 1 ? "Yarın yeni bir hediye var." : $"{Number(days)} gün sonra yeni bir hediye var.";

        /// <summary>The card of a gift that is not due yet: "Yenisi 5 sa 12 dk sonra."</summary>
        public static string NextGiftIn(TimeSpan left) => $"Yenisi {NumberWords.HoursAndMinutes(left)} sonra.";

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
