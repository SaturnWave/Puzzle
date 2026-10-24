using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Purchases;
using Puzzle.Client.Words;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// Everything the game's screens say, in every language it offers: each word class is asked for each of its words,
/// with what a player meets, and nothing comes back with a place left unfilled. The formats a language writes its
/// numbers, waits, counts and ordinals in are pinned for Turkish, English and Korean, and for a language of each
/// script and way of writing a number the first release adds: German, French, Russian, Japanese and both Chinese.
/// </summary>
public class GameWordsTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly Lazy<EconomyRules> Rules = new(() =>
        EconomyFileMapper.ToRules(JsonSerializer.Deserialize<EconomyFile>(File.ReadAllText(RepoFiles.EconomyPackPath), AsUnityMapsIt)!));

    private static readonly Lazy<MuseumCatalogue> Shipped = new(() => CatalogueFileMapper.ToCatalogue(
        JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!));

    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(3));

    public static TheoryData<Language> OfferedLanguages => WordTablesTests.EveryLanguage();

    [Theory]
    [MemberData(nameof(OfferedLanguages))]
    public void Every_word_the_screens_say_is_said_whole_in_every_language_the_game_offers(Language language)
    {
        using IDisposable speaking = TestWords.Speak(language);

        IReadOnlyList<(string What, string Said)> said = EverythingSaid();

        Assert.True(said.Count > 250, $"only {said.Count} words were said");
        Assert.All(said, each =>
        {
            Assert.False(string.IsNullOrWhiteSpace(each.Said), $"{each.What} says nothing");
            Assert.DoesNotContain("{", each.Said);
            Assert.DoesNotContain("}", each.Said);
        });
    }

    /// <summary>
    /// The interface words, not the museum's: a painter keeps their name, and a portrait's title is often a name
    /// too. Of the interface, only the spatula's name (on its pill, its card and in the shop) and the tester mode's
    /// "Test" (its pill and the lives pill in it) are the same words in English.
    /// </summary>
    [Theory]
    [MemberData(nameof(WordTablesTests.Translations), MemberType = typeof(WordTablesTests))]
    public void A_translation_says_nothing_in_Turkish(Language language)
    {
        (string What, string Said)[] turkish = Interface(Language.Turkish);

        (string What, string Said)[] translated = Interface(language);
        Assert.All(translated, words => Assert.DoesNotMatch("[ğĞıİşŞ]", words.Said));
        string[] sameAsTurkish = translated.Zip(turkish)
            .Where(pair => pair.First.Said == pair.Second.Said && pair.First.Said.Any(char.IsLetter))
            .Select(pair => pair.First.What)
            .ToArray();
        Assert.True(sameAsTurkish.Length <= 5, $"{Languages.CodeOf(language)} says word for word as Turkish does: {string.Join(", ", sameAsTurkish)}");
    }

    private static (string What, string Said)[] Interface(Language language)
    {
        using IDisposable speaking = TestWords.Speak(language);
        return EverythingSaid().Where(each => !each.What.StartsWith("MuseumWords.TitleOf ", StringComparison.Ordinal)
            && !each.What.StartsWith("MuseumWords.ByLineOf", StringComparison.Ordinal)).ToArray();
    }

    [Theory]
    [InlineData(Language.Turkish, "1.250", "950", "1.000.000")]
    [InlineData(Language.English, "1,250", "950", "1,000,000")]
    [InlineData(Language.Korean, "1,250", "950", "1,000,000")]
    [InlineData(Language.German, "1.250", "950", "1.000.000")]
    [InlineData(Language.French, "1 250", "950", "1 000 000")]
    [InlineData(Language.Russian, "1 250", "950", "1 000 000")]
    [InlineData(Language.Japanese, "1,250", "950", "1,000,000")]
    [InlineData(Language.ChineseSimplified, "1,250", "950", "1,000,000")]
    public void An_amount_is_written_with_the_thousands_mark_of_the_language(Language language, string thousands, string hundreds, string million)
    {
        using IDisposable speaking = TestWords.Speak(language);

        Assert.Equal((thousands, hundreds, million), (NumberWords.Amount(1250), NumberWords.Amount(950), NumberWords.Amount(1_000_000)));
        Assert.Equal(thousands, WalletWords.Amount(new Coins(1250)));
    }

    [Theory]
    [InlineData(Language.Turkish, "2 sa 59 dk", "42 dk", "2 sa", "7 sn")]
    [InlineData(Language.English, "2h 59m", "42m", "2h", "7s")]
    [InlineData(Language.Korean, "2시간 59분", "42분", "2시간", "7초")]
    [InlineData(Language.German, "2 Std. 59 Min.", "42 Min.", "2 Std.", "7 Sek.")]
    [InlineData(Language.French, "2 h 59 min", "42 min", "2 h", "7 s")]
    [InlineData(Language.Russian, "2 ч 59 мин", "42 мин", "2 ч", "7 с")]
    [InlineData(Language.Japanese, "2時間59分", "42分", "2時間", "7秒")]
    [InlineData(Language.ChineseSimplified, "2小时59分", "42分钟", "2小时", "7秒")]
    [InlineData(Language.ChineseTraditional, "2小時59分", "42分鐘", "2小時", "7秒")]
    public void A_wait_is_written_in_the_short_hours_minutes_and_seconds_of_the_language(Language language, string hoursAndMinutes, string minutes, string hours, string seconds)
    {
        using IDisposable speaking = TestWords.Speak(language);

        Assert.Equal(hoursAndMinutes, NumberWords.HoursAndMinutes(new TimeSpan(2, 58, 30)));
        Assert.Equal(minutes, NumberWords.HoursAndMinutes(TimeSpan.FromMinutes(42)));
        Assert.Equal(hours, NumberWords.HoursAndMinutes(TimeSpan.FromHours(2)));
        Assert.Equal(seconds, AdWords.SecondsLeft(6.2f));
        Assert.Equal("12:34", NumberWords.Countdown(new TimeSpan(0, 12, 34)));
    }

    [Theory]
    [InlineData(Language.Turkish, "Bölüm 47 · 2. deneme", "2. deneme · taşlar yeniden dizildi")]
    [InlineData(Language.English, "Level 47 · attempt 2", "Attempt 2 · fresh tiles")]
    [InlineData(Language.Korean, "레벨 47 · 2번째 시도", "2번째 시도 · 블록을 새로 놓았어요")]
    [InlineData(Language.German, "Level 47 · Versuch 2", "Versuch 2 · neu gemischt")]
    [InlineData(Language.Russian, "Уровень 47 · попытка 2", "Попытка 2 · фишки заново")]
    [InlineData(Language.Japanese, "レベル47 · 2回目の挑戦", "2回目の挑戦 · ブロックを配り直しました")]
    [InlineData(Language.ChineseSimplified, "第47关 · 第2次尝试", "第2次尝试 · 方块已重新摆放")]
    public void Which_try_a_game_is_is_said_as_the_language_counts_tries(Language language, string onTheCard, string onTheShelf)
    {
        using IDisposable speaking = TestWords.Speak(language);

        Assert.Equal(onTheCard, ResultWords.Headline(new LevelResult.Lost(47, LossReason.OutOfMoves, 2, Array.Empty<GoalFigure>())));
        Assert.Equal(onTheShelf, AttemptWords.OnTheShelf(new AttemptBrief(attemptNumber: 2, isOnLastLevel: false, Rules.Value.Win)));
    }

    /// <summary>A start booster's name breaks over two lines under its disc and stands on one on its card: German's word broken after a hyphen is whole there again.</summary>
    [Theory]
    [InlineData(Language.German, BoosterKind.StartColorBomb, "Regenbogen-\npalette", "Regenbogenpalette")]
    [InlineData(Language.German, BoosterKind.StartRocketAndBomb, "Pinsel +\nKlecks", "Pinsel + Klecks")]
    [InlineData(Language.French, BoosterKind.StartColorBomb, "Palette\narc-en-ciel", "Palette arc-en-ciel")]
    [InlineData(Language.English, BoosterKind.StartColorBomb, "Rainbow\npalette", "Rainbow palette")]
    public void A_start_boosters_name_on_one_line_joins_its_lines_and_a_word_broken_after_a_hyphen(Language language, BoosterKind booster, string onTheShelf, string onOneLine)
    {
        using IDisposable speaking = TestWords.Speak(language);

        Assert.Equal(onTheShelf, LevelStartWords.OnTheShelf(booster, 3));
        Assert.Equal(onOneLine, LevelStartWords.NameOf(booster, 3));
    }

    [Theory]
    [InlineData(1, "Watch an ad, win 1 coin", "Delete 1 record?", "+1 move")]
    [InlineData(4, "Watch an ad, win 4 coins", "Delete 4 records?", "+4 moves")]
    public void English_says_one_thing_and_several_in_their_own_forms(int count, string watch, string question, string moves)
    {
        using IDisposable speaking = TestWords.Speak(Language.English);

        Assert.Equal(watch, ShopWords.WatchForCoins(new Coins(count)));
        Assert.Equal(question, PrivacyWords.Question(count));
        Assert.Equal(moves, LevelStartWords.PlusMoves(count));
    }

    [Theory]
    [InlineData(Language.Turkish, "Oyuncu kimliği: 6F1C-0E9A · Sürüm 1.0", "attempts.jsonl · her biten deneme bir satır", "sürüm 1")]
    [InlineData(Language.English, "Player ID: 6F1C-0E9A · Version 1.0", "attempts.jsonl · one line per finished attempt", "version 1")]
    [InlineData(Language.Korean, "플레이어 ID: 6F1C-0E9A · 버전 1.0", "attempts.jsonl · 끝난 시도마다 한 줄", "버전 1")]
    public void The_lines_of_the_records_file_and_of_support_are_said_in_the_language(Language language, string foot, string fileLine, string content)
    {
        using IDisposable speaking = TestWords.Speak(language);

        Assert.Equal(foot, SettingsWords.Foot(new DeviceId("6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a"), "1.0"));
        Assert.Equal(fileLine, PrivacyWords.FileLine("attempts.jsonl"));
        Assert.Equal(content, SettingsWords.ContentVersionOf(ContentVersion.First));
    }

    [Theory]
    [InlineData(Language.Turkish, "Bölüm 51'de açılır")]
    [InlineData(Language.English, "Opens at level 51")]
    [InlineData(Language.Korean, "레벨 51에서 열려요")]
    [InlineData(Language.German, "Öffnet bei Level 51")]
    [InlineData(Language.French, "Ouvre au niveau 51")]
    [InlineData(Language.Russian, "Откроется на уровне 51")]
    [InlineData(Language.Japanese, "レベル51で開放")]
    [InlineData(Language.ChineseSimplified, "第51关开放")]
    [InlineData(Language.ChineseTraditional, "第51關開放")]
    public void A_locked_room_says_the_level_it_opens_at_as_the_language_writes_a_number_in_a_sentence(Language language, string said)
    {
        using IDisposable speaking = TestWords.Speak(language);

        Assert.Equal(said, MuseumWords.OpensAt(new RoomNumber(2)));
    }

    /// <summary>Every word of every word class, asked with what a player meets: one of each kind of result, booster, setting and card.</summary>
    private static IReadOnlyList<(string What, string Said)> EverythingSaid()
    {
        EconomyRules rules = Rules.Value;
        MuseumCatalogue museum = Shipped.Value;
        PieceReward reward = PieceReward.ForWinning(new PlayerProgress(levelsWon: 46, attemptsOnLevel: 0), 1000, museum)!;
        PieceReward twoPieces = PieceReward.ForWinning(new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0), 1000, museum)!;
        GoalFigure[] goals = Array.Empty<GoalFigure>();
        LevelResult[] results =
        {
            new LevelResult.Won(47, 12, 3, isLastLevel: false, reward, new Coins(45)),
            new LevelResult.Won(48, 20, 0, isLastLevel: false, null, new Coins(45)),
            new LevelResult.Won(1000, 1, 1, isLastLevel: true, null, new Coins(45)),
            new LevelResult.OutOfMoves(47, 2, goals, isClose: true),
            new LevelResult.OutOfMoves(47, 2, goals, isClose: false),
            new LevelResult.Leaving(47, 1, hasPlayed: false, goals),
            new LevelResult.Leaving(47, 3, hasPlayed: true, goals),
        };
        IEnumerable<LevelResult> lost = Enum.GetValues<LossReason>().Select(reason => (LevelResult)new LevelResult.Lost(47, reason, 2, goals));
        WalletView wallet = WalletView.Of(rules.StartingWallet, rules.Lives, Now);
        WalletView unlimited = WalletView.Of(rules.StartingWallet, rules.Lives, Now, WalletHandling.Unlimited);
        ExtraMovesTerms terms = rules.ExtraMoves.TermsFor(rules.StartingWallet, DayStamp.Of(Now));
        ExtraMovesTerms coinsCoverIt = rules.ExtraMoves.TermsFor(rules.StartingWallet.WithCoins(new Coins(5000)), DayStamp.Of(Now));
        MuseumRoom firstRoom = museum.Room(new RoomNumber(1));
        Artwork waterLilies = firstRoom.Artworks[3];

        StoreCatalogue store = rules.Shop.Store;
        StoreProduct pack = store.CoinPacks[0];
        StoreProduct starter = store.StarterPack.Product;
        var purchaseCards = new List<(string What, PurchaseCardWords? Words)>
        {
            ("Asking", PurchaseWords.Asking(pack, new StorePrice("com.example.coins.pouch", "₺44,99", 44.99m, "TRY"))),
            ("Pending", PurchaseWords.Of(new PurchaseState.Pending(pack))),
            ("Deferred", PurchaseWords.Of(new PurchaseState.Deferred(pack))),
            ("Delivered pack", PurchaseWords.Of(new PurchaseState.Delivered(pack, pack.Contents))),
            ("Delivered one coin", PurchaseWords.Of(new PurchaseState.Delivered(pack, new Reward(new Coins(1), Array.Empty<BoosterKind>())))),
            ("Delivered starter", PurchaseWords.Of(new PurchaseState.Delivered(starter, starter.Contents))),
            ("Restore failed", PurchaseWords.Of(new RestoreState.Ended(Array.Empty<StoreProduct>(), succeeded: false), starter)),
            ("Restore nothing", PurchaseWords.Of(new RestoreState.Ended(Array.Empty<StoreProduct>(), succeeded: true), starter)),
            ("Restore starter", PurchaseWords.Of(new RestoreState.Ended(new[] { starter }, succeeded: true), starter)),
        };
        purchaseCards.AddRange(Enum.GetValues<PurchaseFailure>().Select(why => ($"Failed {why}", PurchaseWords.Of(new PurchaseState.Failed(starter, why)))));

        var said = new List<(string, string)>
        {
            ("MuseumWords.ShopTab", MuseumWords.ShopTab), ("MuseumWords.HomeTab", MuseumWords.HomeTab), ("MuseumWords.MapTab", MuseumWords.MapTab),
            ("MuseumWords.CollectionTab", MuseumWords.CollectionTab), ("MuseumWords.CollectionTitle", MuseumWords.CollectionTitle),
            ("MuseumWords.PieceLabel", MuseumWords.PieceLabel), ("MuseumWords.RestoredLabel", MuseumWords.RestoredLabel),
            ("MuseumWords.DragThePiece", MuseumWords.DragThePiece), ("MuseumWords.DragThePieces", MuseumWords.DragThePieces),
            ("MuseumWords.NoPieceWaits", MuseumWords.NoPieceWaits), ("MuseumWords.PaintingRestored", MuseumWords.PaintingRestored),
            ("MuseumWords.PlaceThemAll", MuseumWords.PlaceThemAll), ("MuseumWords.Done", MuseumWords.Done), ("MuseumWords.ToTheJigsaw", MuseumWords.ToTheJigsaw),
            ("MuseumWords.TouchToClose", MuseumWords.TouchToClose),
            ("MuseumWords.BackToTheMuseum", MuseumWords.BackToTheMuseum), ("MuseumWords.PaintingToCome", MuseumWords.PaintingToCome),
            ("MuseumWords.RoomToCome", MuseumWords.RoomToCome), ("MuseumWords.RoomWithoutAName", MuseumWords.RoomWithoutAName),
            ("MuseumWords.MorePaintingsToCome", MuseumWords.MorePaintingsToCome), ("MuseumWords.MuseumRestored", MuseumWords.MuseumRestored),
            ("MuseumWords.PlayAgain", MuseumWords.PlayAgain), ("MuseumWords.RoomRestored", MuseumWords.RoomRestored), ("MuseumWords.OnFromTheRoom", MuseumWords.OnFromTheRoom),
            ("MuseumWords.RoomOfRooms", MuseumWords.RoomOfRooms(new RoomNumber(3))),
            ("MuseumWords.Restored", MuseumWords.Restored(4)), ("MuseumWords.PaintingsWhole", MuseumWords.PaintingsWhole(4)),
            ("MuseumWords.NameOf", MuseumWords.NameOf(firstRoom)), ("MuseumWords.AfterTheRoom", MuseumWords.AfterTheRoom(new RoomNumber(1), museum)),
            ("MuseumWords.AfterTheRoom 20", MuseumWords.AfterTheRoom(new RoomNumber(20), museum)), ("MuseumWords.RoomAndItsPaintings", MuseumWords.RoomAndItsPaintings(MuseumWords.NameOf(firstRoom))),
            ("MuseumWords.Play", MuseumWords.Play(47)), ("MuseumWords.YouAreHere", MuseumWords.YouAreHere(47)), ("MuseumWords.OpensAt", MuseumWords.OpensAt(new RoomNumber(11))),
            ("MuseumWords.TitleOf", MuseumWords.TitleOf(PaintingNumber.All.ElementAt(3), waterLilies)), ("MuseumWords.TitleOf none", MuseumWords.TitleOf(PaintingNumber.All.ElementAt(3), null)),
            ("MuseumWords.ByLineOf", MuseumWords.ByLineOf(waterLilies)), ("MuseumWords.ByLineOf none", MuseumWords.ByLineOf(null)),
            ("MuseumWords.Earned", MuseumWords.Earned(reward)), ("MuseumWords.Earned two", MuseumWords.Earned(twoPieces)),
            ("MuseumWords.CollectionCount", MuseumWords.CollectionCount(4)), ("MuseumWords.PiecesInPlace", MuseumWords.PiecesInPlace(6, 10)),
            ("MuseumWords.PiecesInPlace one", MuseumWords.PiecesInPlace(0, 1)), ("MuseumWords.LevelsOf", MuseumWords.LevelsOf(PaintingNumber.All.ElementAt(2))),
            ("MuseumWords.PlaceWhatWasWon 1", MuseumWords.PlaceWhatWasWon(1)), ("MuseumWords.PlaceWhatWasWon 4", MuseumWords.PlaceWhatWasWon(4)),
            ("ShopWords.Title", ShopWords.Title), ("ShopWords.Coins", ShopWords.Coins), ("ShopWords.Boosters", ShopWords.Boosters), ("ShopWords.Free", ShopWords.Free),
            ("ShopWords.ComingSoon", ShopWords.ComingSoon), ("ShopWords.Watch", ShopWords.Watch), ("ShopWords.TomorrowAgain", ShopWords.TomorrowAgain),
            ("ShopWords.ToTheShop", ShopWords.ToTheShop), ("ShopWords.Gift", ShopWords.Gift), ("ShopWords.PacksTitle", ShopWords.PacksTitle),
            ("ShopWords.NotOnSale", ShopWords.NotOnSale), ("ShopWords.WhereCoinsComeFrom", ShopWords.WhereCoinsComeFrom), ("ShopWords.CoinsShortTitle", ShopWords.CoinsShortTitle),
            ("ShopWords.GatherCoins", ShopWords.GatherCoins), ("ShopWords.BoughtTitle", ShopWords.BoughtTitle), ("ShopWords.GiftTitle", ShopWords.GiftTitle),
            ("ShopWords.GiftTaken", ShopWords.GiftTaken), ("ShopWords.PaintingsReward", ShopWords.PaintingsReward), ("ShopWords.NoAdTitle", ShopWords.NoAdTitle),
            ("ShopWords.NoAdLeft", ShopWords.NoAdLeft), ("ShopWords.ComeTomorrow", ShopWords.ComeTomorrow),
            ("ShopWords.WatchForCoins", ShopWords.WatchForCoins(new Coins(4))), ("ShopWords.FreeToday 2", ShopWords.FreeToday(2)), ("ShopWords.FreeToday 0", ShopWords.FreeToday(0)),
            ("ShopWords.GiftIn", ShopWords.GiftIn(new TimeSpan(5, 12, 0))), ("ShopWords.NextGiftAfter 1", ShopWords.NextGiftAfter(1)), ("ShopWords.NextGiftAfter 3", ShopWords.NextGiftAfter(3)),
            ("ShopWords.NextGiftIn", ShopWords.NextGiftIn(new TimeSpan(5, 12, 0))),
            ("ShopWords.WhatCame one", ShopWords.WhatCame(new Reward(new Coins(80), new[] { BoosterKind.HitCell }))),
            ("ShopWords.WhatCame several", ShopWords.WhatCame(new Reward(new Coins(80), new[] { BoosterKind.HitCell, BoosterKind.StartColorBomb }))),
            ("ShopWords.HowToUse start", ShopWords.HowToUse(new Reward(Coins.None, new[] { BoosterKind.StartColorBomb }))),
            ("ShopWords.HowToUse play", ShopWords.HowToUse(new Reward(Coins.None, new[] { BoosterKind.HitRow }))),
            ("ShopWords.HowToUse mix", ShopWords.HowToUse(new Reward(Coins.None, new[] { BoosterKind.HitRow, BoosterKind.StartColorBomb }))),
            ("PurchaseWords.StarterOnce", PurchaseWords.StarterOnce),
            ("PurchaseWords.PriceComing", PurchaseWords.PriceComing), ("PurchaseWords.PackNotOnSale", PurchaseWords.PackNotOnSale),
            ("PurchaseWords.StoreUnreachable", PurchaseWords.StoreUnreachable), ("PurchaseWords.Restore", PurchaseWords.Restore),
            ("PurchaseWords.Buy", PurchaseWords.Buy), ("PurchaseWords.Decline", PurchaseWords.Decline),
            ("WalletWords.Full", WalletWords.Full), ("WalletWords.LivesTitle", WalletWords.LivesTitle), ("WalletWords.AllLivesThere", WalletWords.AllLivesThere),
            ("WalletWords.PlayWhenALifeComes", WalletWords.PlayWhenALifeComes), ("WalletWords.TesterModePill", WalletWords.TesterModePill),
            ("WalletWords.UnlimitedLives", WalletWords.UnlimitedLives), ("WalletWords.TesterModeOn", WalletWords.TesterModeOn), ("WalletWords.NoLifeIsLost", WalletWords.NoLifeIsLost),
            ("WalletWords.LivesPill", WalletWords.LivesPill(wallet)), ("WalletWords.LivesPill unlimited", WalletWords.LivesPill(unlimited)),
            ("WalletWords.LivesOfMost", WalletWords.LivesOfMost(wallet)), ("WalletWords.LivesOfMost unlimited", WalletWords.LivesOfMost(unlimited)),
            ("WalletWords.NextLife", WalletWords.NextLife(new TimeSpan(0, 12, 34))), ("WalletWords.FullAfter", WalletWords.FullAfter(new TimeSpan(1, 42, 0))),
            ("SettingsWords.Title", SettingsWords.Title), ("SettingsWords.Language", SettingsWords.Language), ("SettingsWords.Privacy", SettingsWords.Privacy),
            ("SettingsWords.PrivacyLine", SettingsWords.PrivacyLine), ("SettingsWords.Support", SettingsWords.Support), ("SettingsWords.PlayerIdLabel", SettingsWords.PlayerIdLabel),
            ("SettingsWords.GameVersionLabel", SettingsWords.GameVersionLabel), ("SettingsWords.ContentVersionLabel", SettingsWords.ContentVersionLabel),
            ("SettingsWords.RecordsFileLabel", SettingsWords.RecordsFileLabel), ("SettingsWords.RecordsKeptInMemory", SettingsWords.RecordsKeptInMemory),
            ("SettingsWords.WhatToTell", SettingsWords.WhatToTell), ("SettingsWords.TesterModeQuestion", SettingsWords.TesterModeQuestion),
            ("SettingsWords.TesterModeTells", SettingsWords.TesterModeTells), ("SettingsWords.TurnTesterModeOn", SettingsWords.TurnTesterModeOn),
            ("SettingsWords.LeaveTesterModeOff", SettingsWords.LeaveTesterModeOff), ("SettingsWords.LanguageSpoken", SettingsWords.LanguageSpoken),
            ("SettingsWords.Foot", SettingsWords.Foot(new DeviceId("6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a"), "1.0")),
            ("SettingsWords.ContentVersionOf", SettingsWords.ContentVersionOf(ContentVersion.First)),
            ("SettingsWords.BuildLine", SettingsWords.BuildLine(rules.Ads, rules.Shop.RealMoney)),
            ("PrivacyWords.Title", PrivacyWords.Title), ("PrivacyWords.DeleteTheRecords", PrivacyWords.DeleteTheRecords), ("PrivacyWords.KeepThem", PrivacyWords.KeepThem),
            ("PrivacyWords.Delete", PrivacyWords.Delete), ("PrivacyWords.WhatStays", PrivacyWords.WhatStays), ("PrivacyWords.Deleted", PrivacyWords.Deleted),
            ("PrivacyWords.CannotCount", PrivacyWords.CannotCount), ("PrivacyWords.RecordsKept", PrivacyWords.RecordsKept(37)), ("PrivacyWords.RecordsUncounted", PrivacyWords.RecordsUncounted),
            ("PrivacyWords.FileLine", PrivacyWords.FileLine("attempts.jsonl")), ("PrivacyWords.Question", PrivacyWords.Question(37)), ("PrivacyWords.Question one", PrivacyWords.Question(1)),
            ("BoardWords.Level", BoardWords.Level), ("BoardWords.Moves", BoardWords.Moves), ("BoardWords.Goals", BoardWords.Goals),
            ("BoardWords.LevelNumbered", BoardWords.LevelNumbered(47)), ("BoardWords.PieceOf", BoardWords.PieceOf(reward)), ("BoardWords.OpensOn", BoardWords.OpensOn(12)),
            ("BoosterWords.EveryLevelWithout", BoosterWords.EveryLevelWithout), ("BoosterWords.CostsNoMove", BoosterWords.CostsNoMove),
            ("BoosterWords.TouchToClose", BoosterWords.TouchToClose),
            ("LevelStartWords.Goals", LevelStartWords.Goals), ("LevelStartWords.ChooseBoosters", LevelStartWords.ChooseBoosters), ("LevelStartWords.Moves", LevelStartWords.Moves),
            ("LevelStartWords.Play", LevelStartWords.Play), ("LevelStartWords.Buy", LevelStartWords.Buy), ("LevelStartWords.CoinsShort", LevelStartWords.CoinsShort),
            ("LevelStartWords.BuyToChoose", LevelStartWords.BuyToChoose), ("LevelStartWords.Title", LevelStartWords.Title(47)),
            ("LevelStartWords.Reward", LevelStartWords.Reward(reward)), ("LevelStartWords.Reward two", LevelStartWords.Reward(twoPieces)),
            ("AttemptWords.HowToPlay", AttemptWords.HowToPlay),
            ("AttemptWords.OnTheShelf 3", AttemptWords.OnTheShelf(new AttemptBrief(attemptNumber: 3, isOnLastLevel: false, rules.Win))),
            ("AdWords.Title", AdWords.Title), ("AdWords.TestBuild", AdWords.TestBuild), ("AdWords.NothingIsShown", AdWords.NothingIsShown),
            ("AdWords.ItPaysFor moves", AdWords.ItPaysFor(new AdPrize.ExtraMoves())), ("AdWords.ItPaysFor coins", AdWords.ItPaysFor(new AdPrize.CoinsInTheShop(new Coins(4)))),
            ("AdWords.SecondsLeft", AdWords.SecondsLeft(4.2f)),
            ("ResultWords.GiveUp", ResultWords.GiveUp), ("ResultWords.WatchAnAd", ResultWords.WatchAnAd), ("ResultWords.NoAdLeftToday", ResultWords.NoAdLeftToday),
            ("ResultWords.NoLifeInTheTesterMode", ResultWords.NoLifeInTheTesterMode),
            ("ResultWords.TakeTheMovesForCoins", ResultWords.TakeTheMovesForCoins(terms)), ("ResultWords.TakeTheMovesForCoins covered", ResultWords.TakeTheMovesForCoins(coinsCoverIt)),
            ("ResultWords.TakeTheMovesForAnAd", ResultWords.TakeTheMovesForAnAd(terms)), ("ResultWords.PriceOfTheMoves", ResultWords.PriceOfTheMoves(terms)),
            ("ResultWords.CoinsWon", ResultWords.CoinsWon(new Coins(45))),
            ("SettingsWords.BuildLine test build", SettingsWords.BuildLine(rules.Ads, RealMoneySales.NotAvailable)),
        };

        foreach (StoreProduct product in store.Products)
        {
            said.Add(($"PurchaseWords.NameOf {product.Name}", PurchaseWords.NameOf(product)));
        }

        foreach ((string what, PurchaseCardWords? words) in purchaseCards)
        {
            PurchaseCardWords card = words ?? throw new InvalidOperationException($"The purchase card {what} says nothing.");
            said.Add(($"PurchaseWords {what} title", card.Title));
            said.Add(($"PurchaseWords {what} first line", card.FirstLine));
            said.Add(($"PurchaseWords {what} second line", card.SecondLine));
        }

        foreach (DeviceSetting setting in Enum.GetValues<DeviceSetting>())
        {
            said.Add(($"SettingsWords.NameOf {setting}", SettingsWords.NameOf(setting)));
        }

        foreach (BoosterKind booster in BoosterKinds.InLevel)
        {
            said.Add(($"BoosterWords.NameOf {booster}", BoosterWords.NameOf(booster)));
            said.Add(($"BoosterWords.Armed {booster}", BoosterWords.Armed(booster)));
            said.Add(($"BoosterWords.Refused {booster}", BoosterWords.Refused(booster)));
            said.Add(($"TutorialWords.OfTheBooster {booster}", TutorialWords.OfTheBooster(booster, isArmed: false).ToString()));
            if (booster != BoosterKind.Shuffle)
            {
                said.Add(($"TutorialWords.OfTheBooster armed {booster}", TutorialWords.OfTheBooster(booster, isArmed: true).ToString()));
            }

            said.Add(($"BoosterWords.OnTheCard {booster}", BoosterWords.OnTheCard(booster)));
            said.Add(($"ShopWords.NameOf {booster}", ShopWords.NameOf(booster)));
            if (booster != BoosterKind.Shuffle)
            {
                said.Add(($"BoosterWords.WhatToPick {booster}", BoosterWords.WhatToPick(booster)));
            }
        }

        foreach (BoosterKind booster in LevelBrief.StartBoosters)
        {
            said.Add(($"LevelStartWords.OnTheShelf {booster}", LevelStartWords.OnTheShelf(booster, 3)));
            said.Add(($"LevelStartWords.WhatItDoes {booster}", LevelStartWords.WhatItDoes(booster, 3)));
            said.Add(($"ShopWords.NameOf {booster}", ShopWords.NameOf(booster)));
            said.Add(($"TutorialWords.OfTheStartBooster {booster}", TutorialWords.OfTheStartBooster(booster, 3).ToString()));
            said.Add(($"TutorialWords.OfTheStartBooster one {booster}", TutorialWords.OfTheStartBooster(booster, 1).ToString()));
        }

        foreach (BoosterSet set in rules.Shop.BoosterSets)
        {
            said.Add(($"ShopWords.NameOf {set.Name}", ShopWords.NameOf(set)));
            said.Add(($"ShopWords.CoinsShortFor {set.Name}", ShopWords.CoinsShortFor(set, new Coins(5050))));
            said.Add(($"ShopWords.InYourBag {set.Name}", ShopWords.InYourBag(set)));
        }

        foreach (LevelResult result in results.Concat(lost))
        {
            string kind = result.GetType().Name;
            said.Add(($"ResultWords.Title {kind}", ResultWords.Title(result)));
            said.Add(($"ResultWords.Headline {kind}", ResultWords.Headline(result)));
            if (result is LevelResult.Won won)
            {
                said.Add(($"ResultWords.MovesLeftOver {won.MovesLeft}", ResultWords.MovesLeftOver(won)));
            }
            else
            {
                said.Add(($"ResultWords.Lives {kind}", ResultWords.Lives(result)));
                said.Add(($"ResultWords.Lives tester {kind}", ResultWords.Lives(result, WalletHandling.Unlimited)));
            }

            if (!(result is LevelResult.Won) && !(result is LevelResult.OutOfMoves))
            {
                said.Add(($"ResultWords.Footnote {kind}", ResultWords.Footnote(result)));
            }

            if (!(result is LevelResult.OutOfMoves))
            {
                said.Add(($"ResultWords.GoOn {kind}", ResultWords.GoOn(result)));
                said.Add(($"ResultWords.GoOnOnANarrowButton {kind}", ResultWords.GoOnOnANarrowButton(result)));
            }
        }

        foreach (MuseumRoom room in museum.Rooms)
        {
            said.Add(($"MuseumWords.NameOf room {room.Number.Value}", MuseumWords.NameOf(room)));
            foreach (Artwork artwork in room.Artworks)
            {
                said.Add(($"MuseumWords.TitleOf {artwork.Id.Value}", MuseumWords.TitleOf(PaintingNumber.All.First(), artwork)));
                said.Add(($"MuseumWords.ByLineOf {artwork.Id.Value}", MuseumWords.ByLineOf(artwork)));
            }
        }

        return said;
    }
}
