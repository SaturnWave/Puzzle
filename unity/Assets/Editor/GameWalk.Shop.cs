#nullable enable
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Meta;
using Puzzle.Museum;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walks of the shop, the gift and a painting's reward, once on each screen, on the game's own session and by
    /// touches alone: the coins' plus leads to the shop; a pack of coins is answered with the card that says a test
    /// build sells nothing for money; the first set is bought and its card says so, the row counts the price down;
    /// the last set, which the coins do not cover, is answered with what they lack and nothing is bought; the day's
    /// ads for coins are watched one after another, each paying its coins once it is over, until the free strip says
    /// "Yarın yine"; the hall's gift is taken, its card says what it gave, the frame then counts to the next one, and
    /// a second press is answered with when that is (<c>walk-shop-...</c>); a booster held none of with too few coins
    /// is offered with the way to the shop, which leads there (<c>walk-toshop-...</c>); and the last piece of a
    /// painting is carried home, the painting is whole, and its reward is shown and given once (<c>walk-reward-...</c>).
    /// Every walk fails when a touch does not lead where it should, or when the progress does not hold what the rules
    /// gave, and only that.
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>The ad for coins is pictured this far into its seconds.</summary>
        private const float AdMoment = 1.5f;

        public static void RunTheShopWalks(ScreenShape screen, Studio studio)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, RetriedLevel), studio.Levels);
            WalkTheShop(standing, screen, studio);
            WalkFromTheOfferToTheShop(standing, screen, studio);
            WalkAPaintingRestored(screen, studio);
        }

        /// <summary>In the hall the gift frame is pressed where a gift waits: its card says what it gave, and the progress counts one daily reward more.</summary>
        private static void TakeTheGift(Reel reel)
        {
            GameSession session = reel.Session;
            if (MuseumOf(session).GiftShown is not (true, _))
            {
                return; // the day's gift was taken already
            }

            int before = session.Progress.Wallet.Rewards.Daily;
            Press(session, MuseumOf(session).GiftPixel, "the hall's gift frame", reel, pressedShot: null);
            reel.Settle();
            reel.Shoot("gift");
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == ShopWords.GiftTitle && session.Progress.Wallet.Rewards.Daily == before + 1,
                $"the gift frame brought the card \"{MuseumOf(session).NoticeShown}\" and {session.Progress.Wallet.Rewards.Daily - before} daily rewards, not the day's one");
            Press(session, MuseumOf(session).NoticeDonePixel, "the gift card's Tamam", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).GiftShown is (false, _), "once the day's gift was taken the frame still shows one waiting");
        }

        /// <summary>The shop from the coins' plus: a pack, a set bought, a set the coins do not cover, the day's ads; then the hall's gift, taken and asked for again.</summary>
        private static void WalkTheShop(PlayerProgress standing, ScreenShape screen, Studio studio)
        {
            EconomyRules rules = StoryStudio.Economy;
            BoosterSet first = rules.Shop.BoosterSets[0];
            BoosterSet last = rules.Shop.BoosterSets[rules.Shop.BoosterSets.Count - 1];
            PlayerProgress before = standing.WithWallet(standing.Wallet.WithCoins(first.Price + new Coins(100)));
            ThroughTheCard.Require(!before.Wallet.Coins.Covers(last.Price), $"the walk's {before.Wallet.Coins} coins cover the last set too");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, rules, StoryStudio.Clock, new ProgressInMemory(before), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-shop-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            reel.Shoot("hall");
            Press(session, MuseumOf(session).CoinsPlusPixel, "the coins' plus", reel, "plus-pressed");
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).Showing == Destination.Shop, $"the coins' plus led to {MuseumOf(session).Showing}, not to the shop");
            reel.Shoot("shop");

            // A pack of coins: a test build sells nothing for money, and says so.
            Press(session, MuseumOf(session).ShopPackPixel(0), "the first pack's button", reel, pressedShot: null);
            reel.Settle();
            reel.Shoot("pack-not-on-sale");
            ThroughTheCard.Require(MuseumOf(session).NoticeShown == ShopWords.PacksTitle && session.Progress.Equals(before), "a pack of coins was not answered with the card that says it is not on sale, or something changed");
            CloseTheNotice(reel);

            // The first set, which the coins cover: bought for its price, its boosters on the shelf, its card says so.
            ThroughTheCard.Require(MuseumOf(session).IsShopSetLit(0) && !MuseumOf(session).IsShopSetLit(rules.Shop.BoosterSets.Count - 1), "the sets' buttons are not lit as the coins cover them");
            Press(session, MuseumOf(session).ShopSetPixel(0), "the first set's button", reel, "set-pressed");
            reel.Settle();
            reel.Shoot("set-bought");
            Wallet bought = session.Progress.Wallet;
            ThroughTheCard.Require(
                MuseumOf(session).NoticeShown == ShopWords.BoughtTitle && bought.Coins == before.Wallet.Coins.Less(first.Price) && bought.Shelf.Equals(before.Wallet.Shelf.Plus(first.Boosters)),
                $"the first set's button left the wallet at {bought.Coins} coins and {bought.Shelf}, not the {first} bought");
            CloseTheNotice(reel);
            reel.Advance(Tempo.CoinsCount);
            reel.Shoot("after-the-purchase");
            ThroughTheCard.Require(MuseumOf(session).CoinsShown == bought.Coins, $"the row shows {MuseumOf(session).CoinsShown} coins after the purchase, and the player has {bought.Coins}");

            // The last set, which they do not cover: the rules say what is missing, and nothing changes.
            Press(session, MuseumOf(session).ShopSetPixel(rules.Shop.BoosterSets.Count - 1), "the last set's dimmed button", reel, pressedShot: null);
            reel.Settle();
            reel.Shoot("set-too-few-coins");
            ThroughTheCard.Require(MuseumOf(session).NoticeShown == ShopWords.CoinsShortTitle && session.Progress.Wallet.Equals(bought), "the dimmed set was not answered with what the coins lack, or something was bought");
            CloseTheNotice(reel);

            // The day's ads for coins, one after another, each paying once it is over, until there is none left.
            for (int ad = 0; ad < rules.Shop.AdForCoins.PerDay; ad++)
            {
                Coins coinsBefore = session.Progress.Wallet.Coins;
                Press(session, MuseumOf(session).ShopAdPixel, "the free strip's İzle", reel, pressedShot: null);
                if (ad == 0)
                {
                    reel.Advance(Tempo.CardAfterDark + Tempo.CardComesDown + AdMoment);
                    reel.Shoot("ad");
                    ThroughTheCard.Require(MuseumOf(session).IsShowingTheAd && session.Progress.Wallet.Coins == coinsBefore, "the stand-in ad is not shown, or paid before it was over");
                }

                reel.Settle();
                ThroughTheCard.Require(
                    session.Progress.Wallet.Coins == coinsBefore + rules.Shop.AdForCoins.Coins,
                    $"ad {ad + 1} paid {session.Progress.Wallet.Coins.Less(coinsBefore)} coins, not {rules.Shop.AdForCoins.Coins}");
            }

            reel.Advance(Tempo.CoinsCount);
            reel.Shoot("no-ads-left");
            ThroughTheCard.Require(!MuseumOf(session).ShopOffersAnAd && session.Progress.Wallet.Ads.ForCoins == rules.Shop.AdForCoins.PerDay, "after the day's ads the free strip still offers one");
            Wallet afterTheAds = session.Progress.Wallet;
            ThroughTheCard.Press(session, MuseumOf(session).ShopAdPixel, "the dimmed Yarın yine");
            reel.Settle();
            ThroughTheCard.Require(session.Progress.Wallet.Equals(afterTheAds) && !MuseumOf(session).IsShowingTheAd, "a press on Yarın yine showed an ad or changed the wallet");

            // The hall's gift: taken once, and a second press says when the next one comes.
            TurnThePage(reel, Destination.Home, picturesTheTurn: false);
            reel.Shoot("hall-gift-waits");
            TakeTheGift(reel);
            reel.Shoot("hall-gift-taken");
            Wallet afterTheGift = session.Progress.Wallet;
            Press(session, MuseumOf(session).GiftPixel, "the gift frame once more", reel, pressedShot: null);
            reel.Settle();
            reel.Shoot("gift-not-due");
            ThroughTheCard.Require(MuseumOf(session).NoticeShown == ShopWords.GiftTitle && session.Progress.Wallet.Equals(afterTheGift), "the gift frame gave a second gift on the same day");
            CloseTheNotice(reel);
            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: shop, a pack not on sale, {first.Name} bought, {last.Name} refused, {rules.Shop.AdForCoins.PerDay} ads for coins, the gift taken once; the player now holds \"{session.Progress.Wallet}\".");
        }

        /// <summary>A booster held none of, offered with too few coins: the card's way to the shop takes both cards up and leads there.</summary>
        private static void WalkFromTheOfferToTheShop(PlayerProgress standing, ScreenShape screen, Studio studio)
        {
            const BoosterKind offered = BoosterKind.StartExtraMoves;
            BoosterShelf noneOffered = Enumerable.Range(0, standing.Wallet.Shelf.Count(offered)).Aggregate(standing.Wallet.Shelf, (shelf, _) => shelf.WithOneFewer(offered));
            PlayerProgress before = standing.WithWallet(standing.Wallet.WithShelf(noneOffered).WithCoins(StoryStudio.Economy.Shop.PriceOf(offered).Less(new Coins(1))));

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(before), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-toshop-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            ThroughTheCard.BringDownTheCard(session);
            OpenTheOffer(reel, offered);
            reel.Shoot("offer-too-few-coins");
            Press(session, MuseumOf(session).GetMoreToTheShopPixel, "the offer's way to the shop", reel, pressedShot: null);
            reel.Settle();
            reel.Shoot("shop");
            ThroughTheCard.Require(
                MuseumOf(session).Showing == Destination.Shop && !MuseumOf(session).ShowsTheStartCard && MuseumOf(session).OffersABooster == null && session.Progress.Equals(before),
                "the offer's way to the shop did not take the cards up and lead to the shop, or something changed");
            reel.WriteSheet();
        }

        /// <summary>
        /// The last piece of the first painting carried to its place: the painting is whole, its finished face shows
        /// what it gave, the progress holds that reward once, and leaving the jigsaw gives nothing more.
        /// </summary>
        private static void WalkAPaintingRestored(ScreenShape screen, Studio studio)
        {
            var painting = new PaintingNumber(1);
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, painting.LastLevel.Value + 1), studio.Levels);
            PlayerProgress before = standing.WithWaitingPiecesPlaced(painting.Size.PieceCount - 1);
            Reward reward = StoryStudio.Economy.PaintingCompleted.NextFor(before.Wallet);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(before), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-reward-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            reel.Shoot("hall");
            Press(session, MuseumOf(session).JigsawButtonPixel, "the hall's Tablo", reel, pressedShot: null);
            WaitFor(session, () => session.Jigsaw != null, "the hall's Tablo opened no jigsaw");
            reel.Settle();
            reel.Shoot("jigsaw");
            CarryThePieceHome(reel);
            PictureTheEndOfThePlacing(reel);

            Wallet given = session.Progress.Wallet;
            ThroughTheCard.Require(
                session.Progress.Museum.IsComplete(painting) && JigsawOf(session).RewardShown is Reward shown && shown.Equals(reward),
                $"the whole painting shows {JigsawOf(session).RewardShown?.ToString() ?? "no reward"}, not {reward}");
            ThroughTheCard.Require(
                given.Rewards.ForPaintings == before.Wallet.Rewards.ForPaintings + 1 && given.Coins == before.Wallet.Coins + reward.Coins
                && given.Shelf.Count(reward.Boosters[0]) == before.Wallet.Shelf.Count(reward.Boosters[0]) + 1,
                $"completing the painting left the wallet at \"{given}\", not with its reward of {reward}");

            Press(session, JigsawOf(session).BackButtonPixel, "the jigsaw's back button", reel, pressedShot: null);
            WaitFor(session, () => session.Museum != null, "the jigsaw's back button did not lead to the museum");
            reel.Settle();
            reel.Advance(Tempo.CoinsCount);
            reel.Shoot("hall-after");
            ThroughTheCard.Require(session.Progress.Wallet.Equals(given) && !session.HasUnsavedProgress, "leaving the whole painting changed the wallet again, or left it unsaved");
            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: painting {painting} completed, its reward {reward} given once.");
        }

        private static void CloseTheNotice(Reel reel)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).NoticeDonePixel, "the card's Tamam", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).NoticeShown == null, "the card's Tamam did not take it up");
        }
    }
}
