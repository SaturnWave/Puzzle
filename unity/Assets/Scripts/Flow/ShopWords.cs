#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Words;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the shop, the hall's gift frame and the cards that answer them say, in the language the game speaks
    /// (<see cref="Speech"/>). The Turkish words are the design canvas' (ShopTR, HomeTR) where it has them; the
    /// cards have no drawing, and theirs are written in the canvas' manner. No Unity dependency.
    /// </summary>
    public static class ShopWords
    {
        /// <summary>The sign over the shop, and its three sections.</summary>
        public static string Title => Speech.Say(WordId.ShopTitle);
        public static string Coins => Speech.Say(WordId.ShopCoinsHeader);
        public static string Boosters => Speech.Say(WordId.ShopBoostersHeader);
        public static string Free => Speech.Say(WordId.ShopFreeHeader);

        /// <summary>The button of a pack of coins, which a test build does not sell.</summary>
        public static string ComingSoon => Speech.Say(WordId.ShopComingSoon);

        /// <summary>The blue button of the free strip, and what it says once the day's ads are used up.</summary>
        public static string Watch => Speech.Say(WordId.ShopWatch);
        public static string TomorrowAgain => Speech.Say(WordId.ShopTomorrowAgain);

        /// <summary>The way to the shop under the dimmed button of a card that offers a booster the coins do not cover.</summary>
        public static string ToTheShop => Speech.Say(WordId.ToTheShop);

        /// <summary>The hall's gift frame while the daily reward waits.</summary>
        public static string Gift => Speech.Say(WordId.HallGift);

        /// <summary>The card a pack of coins answers with: a test build sells nothing for money.</summary>
        public static string PacksTitle => Speech.Say(WordId.NoticePacksTitle);
        public static string NotOnSale => Speech.Say(WordId.NoticeNotOnSale);
        public static string WhereCoinsComeFrom => Speech.Say(WordId.NoticeWhereCoinsComeFrom);

        /// <summary>The card of a set the coins do not cover.</summary>
        public static string CoinsShortTitle => Speech.Say(WordId.NoticeCoinsShortTitle);
        public static string GatherCoins => Speech.Say(WordId.NoticeGatherCoins);

        /// <summary>The card of a set that was bought.</summary>
        public static string BoughtTitle => Speech.Say(WordId.NoticeBoughtTitle);

        /// <summary>The card of the daily reward, given or not due yet.</summary>
        public static string GiftTitle => Speech.Say(WordId.NoticeGiftTitle);
        public static string GiftTaken => Speech.Say(WordId.NoticeGiftTaken);

        /// <summary>The tray's words over the reward of a painting that has just become whole.</summary>
        public static string PaintingsReward => Speech.Say(WordId.JigsawPaintingsReward);

        /// <summary>The card of an ad for coins on a day that has none left.</summary>
        public static string NoAdTitle => Speech.Say(WordId.NoticeNoAdTitle);
        public static string NoAdLeft => Speech.Say(WordId.NoticeNoAdLeft);
        public static string ComeTomorrow => Speech.Say(WordId.NoticeComeTomorrow);

        /// <summary>What a set is called in the shop, by the settings' name for it.</summary>
        /// <exception cref="ArgumentException">The settings name a set the game has no words for: a new set needs its words first.</exception>
        public static string NameOf(BoosterSet set) => set.Name switch
        {
            "starter" => Speech.Say(WordId.SetStarter),
            "master" => Speech.Say(WordId.SetMaster),
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
        public static string WatchForCoins(Coins coins) =>
            Speech.SayCounted(WordId.ShopWatchForCoins, coins.Amount, ("coins", NumberWords.Amount(coins.Amount)));

        /// <summary>The free strip's small capitals: how many ads are left today, or that none is.</summary>
        public static string FreeToday(int adsLeft) => adsLeft > 0
            ? Speech.Say(WordId.ShopFreeToday, ("ads", Number(adsLeft)))
            : Speech.Say(WordId.ShopFreeNoneToday);

        /// <summary>The gift frame's pill while the next reward waits for the player's midnight: "5 sa 12 dk".</summary>
        public static string GiftIn(TimeSpan left) => NumberWords.HoursAndMinutes(left);

        /// <summary>The card of a set the coins do not cover: "Usta seti için 5.050 altın daha gerek."</summary>
        public static string CoinsShortFor(BoosterSet set, Coins missing) =>
            Speech.SayCounted(WordId.NoticeCoinsShortFor, missing.Amount, ("set", NameOf(set)), ("coins", NumberWords.Amount(missing.Amount)));

        /// <summary>The card of a set that was bought: "Usta seti çantanda."</summary>
        public static string InYourBag(BoosterSet set) => Speech.Say(WordId.NoticeInYourBag, ("set", NameOf(set)));

        /// <summary>The line under the boosters a reward or a set gave: their names when one kind, how many otherwise.</summary>
        public static string WhatCame(Reward reward) => reward.Boosters.Count == 1
            ? NameOf(reward.Boosters[0])
            : Speech.SayCounted(WordId.BoostersCount, reward.Boosters.Count, ("count", Number(reward.Boosters.Count)));

        /// <summary>Where the boosters just received are used: before a level, during one, or, for a mix, how many came.</summary>
        public static string HowToUse(Reward reward) =>
            reward.Boosters.Count > 0 && reward.Boosters.All(BoosterKinds.IsStart) ? Speech.Say(WordId.NoticeUseOnTheLevelCard)
            : reward.Boosters.Count > 0 && reward.Boosters.All(BoosterKinds.IsInLevel) ? Speech.Say(WordId.NoticeUseFromTheShelf)
            : WhatCame(reward);

        /// <summary>When the next daily reward comes, said on its card: "Yarın yeni bir hediye var." for a reward a day.</summary>
        public static string NextGiftAfter(int days) => days == 1
            ? Speech.Say(WordId.NoticeNextGiftTomorrow)
            : Speech.SayCounted(WordId.NoticeNextGiftInDays, days, ("days", Number(days)));

        /// <summary>The card of a gift that is not due yet: "Yenisi 5 sa 12 dk sonra."</summary>
        public static string NextGiftIn(TimeSpan left) => Speech.Say(WordId.NoticeNextGiftIn, ("wait", NumberWords.HoursAndMinutes(left)));

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
