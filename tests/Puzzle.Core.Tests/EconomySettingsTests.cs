using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;

namespace Puzzle.Core.Tests;

/// <summary>
/// The economy's settings files (economy/*.json): every assumption is a value there, stated once, with a unit and a
/// word on where it comes from; a file that contradicts itself is refused before it prices anything. The rules are
/// tested on the tests' own fixture; of the shipped files only what is decided, and what each variant is for.
/// </summary>
public class EconomySettingsTests
{
    private static readonly string ShippedFolder = Path.GetDirectoryName(RepoFiles.EconomySettingsPath("default.json"))!;

    private static string[] ShippedFiles => Directory.GetFiles(ShippedFolder, "*.json").Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray();

    private static EconomySettings Shipped(string name) => SettingsLoader.Load(RepoFiles.EconomySettingsPath(name));

    private static EconomySettingsFormatException Refused(params string[] changes) =>
        Assert.Throws<EconomySettingsFormatException>(() => EconomySupport.Settings(changes));

    [Fact]
    public void The_shipped_settings_files_load_and_hold_what_is_decided()
    {
        // Every shipped file loads, and no two share a name: the name is the folder a file's reports go to.
        Assert.Contains("default.json", ShippedFiles);
        string[] names = ShippedFiles.Select(file => Shipped(file).Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.Equal("default", Shipped("default.json").Name);

        // Decided (GAME_DESIGN section 19): five lives, one back every thirty minutes, and the average player is rung 55.
        // Everything else of the default is an assumption or a placeholder its owner changes: no test holds it.
        EconomySettings standard = Shipped("default.json");
        Assert.Equal(new LifeCount(5), standard.Lives.Max);
        Assert.Equal(TimeSpan.FromMinutes(30), standard.Lives.PerLife);
        Assert.Equal("skill-55", standard.Players.AverageRung.Name);
        Assert.Contains(standard.Players.AverageRung, standard.Players.Rungs);

        // What each variant is for, against the default beside it and not against a number.
        Assert.True(Shipped("sessions-6-minutes.json").Habit.SessionCeiling < standard.Habit.SessionCeiling);
        Assert.True(Shipped("sessions-20-minutes.json").Habit.SessionCeiling > standard.Habit.SessionCeiling);
        Assert.True(Shipped("sessions-60-minutes.json").Habit.SessionCeiling > Shipped("sessions-20-minutes.json").Habit.SessionCeiling);
        Assert.True(Shipped("one-life.json").Lives.Max.CompareTo(standard.Lives.Max) < 0);
        Assert.True(Shipped("life-every-16-hours.json").Lives.PerLife > standard.Lives.PerLife);
        Assert.True(Shipped("extra-moves-1800.json").ExtraMovesOffer.Price > standard.ExtraMovesOffer.Price);
        ExtraMovesHabit takesNothing = Shipped("no-offers.json").Player.ExtraMoves;
        Assert.False(takesNothing.WatchesAds);
        Assert.False(takesNothing.PaysCoins);
        Assert.Null(Shipped("no-offers.json").Player.StartBoosters.UseAfterLossesOnLevel);
        Assert.Null(Shipped("no-offers.json").Player.StartBoosters.BuyAfterLossesOnLevel);
    }

    [Fact]
    public void The_fixture_of_the_rule_tests_is_a_whole_settings_file_of_its_own()
    {
        EconomySettings fixture = EconomySupport.Settings();

        Assert.Equal("fixture", fixture.Name);
        Assert.NotEqual(Path.GetFullPath(RepoFiles.EconomySettingsPath("default.json")), Path.GetFullPath(EconomySupport.FixturePath));

        // The numbers the rule tests count with: they are the fixture's, whatever the shipped file says today.
        Assert.Equal(new Coins(900), fixture.ExtraMovesOffer.Price);
        Assert.Equal(new Coins(10), fixture.Coins.PerMoveLeftInFinale);
        Assert.Equal(new Coins(500), fixture.Prices.Boosters[BoosterKind.StartExtraMoves]);
        Assert.Equal(TimeSpan.FromMinutes(20), fixture.Habit.SessionCeiling);
        Assert.Equal(3, fixture.ExtraMovesOffer.FreeAdsPerDay);

        // It has every field the shipped format has, so a field added to one cannot be forgotten in the other.
        Assert.Equal(
            Shipped("default.json").Stated.Select(stated => stated.Path).Order(StringComparer.Ordinal),
            fixture.Stated.Select(stated => stated.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_missing_an_unknown_or_a_repeated_field_is_refused()
    {
        using var folder = new TemporaryEconomyFolder();
        JsonObject withoutAField = EconomySupport.FixtureDocument();
        withoutAField["lives"]!.AsObject().Remove("minutesPerLife");
        string statedTwice = folder.File("twice.json");
        File.WriteAllText(statedTwice, """{ "extends": "default.json", "name": "twice", "lives": { "max": 5, "max": 6 } }""");

        EconomySettingsFormatException missing = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Read(withoutAField, "a test"));
        EconomySettingsFormatException unknown = Refused("""{ "lives": { "refillPriceCoins": 100 } }""");
        EconomySettingsFormatException repeated = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Load(statedTwice));

        Assert.Contains("minutesPerLife", missing.Message);
        Assert.Contains("refillPriceCoins", unknown.Message);
        Assert.Contains("'max' twice", repeated.Message);
    }

    [Fact]
    public void A_variant_file_changes_only_what_it_states()
    {
        using var folder = new TemporaryEconomyFolder();
        File.Copy(EconomySupport.FixturePath, folder.File("base.json"));
        File.WriteAllText(folder.File("shorter.json"), """{ "extends": "base.json", "name": "shorter", "habit": { "sessionCeilingMinutes": 10 } }""");

        Dictionary<string, string> standard = SettingsLoader.Load(folder.File("base.json")).Stated.ToDictionary(stated => stated.Path, stated => stated.Value);
        Dictionary<string, string> variant = SettingsLoader.Load(folder.File("shorter.json")).Stated.ToDictionary(stated => stated.Path, stated => stated.Value);

        Assert.Equal(standard.Keys.Order(), variant.Keys.Order());
        Assert.Equal(["habit.sessionCeilingMinutes", "name"], standard.Keys.Where(field => standard[field] != variant[field]).Order());
        Assert.Equal("10", variant["habit.sessionCeilingMinutes"]);

        // A variant extends a whole file, one level deep.
        File.WriteAllText(folder.File("deeper.json"), """{ "extends": "shorter.json", "name": "deeper" }""");
        EconomySettingsFormatException tooDeep = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Load(folder.File("deeper.json")));
        Assert.Contains("extends a file itself", tooDeep.Message);
    }

    [Fact]
    public void A_variant_file_must_state_a_name_of_its_own()
    {
        // Without one it would run as its base and write its numbers into the base's report folder.
        using var folder = new TemporaryEconomyFolder();
        File.Copy(EconomySupport.FixturePath, folder.File("base.json"));
        File.WriteAllText(folder.File("unnamed.json"), """{ "extends": "base.json", "habit": { "sessionCeilingMinutes": 10 } }""");
        File.WriteAllText(folder.File("same-name.json"), """{ "extends": "base.json", "name": "fixture", "habit": { "sessionCeilingMinutes": 10 } }""");

        EconomySettingsFormatException unnamed = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Load(folder.File("unnamed.json")));
        EconomySettingsFormatException sameName = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Load(folder.File("same-name.json")));

        Assert.Contains("must state a name of its own", unnamed.Message);
        Assert.Contains("not 'fixture'", sameName.Message);
    }

    [Fact]
    public void A_settings_path_that_cannot_be_read_is_refused_by_name()
    {
        using var folder = new TemporaryEconomyFolder();
        File.Copy(EconomySupport.FixturePath, folder.File("base.json"));
        File.WriteAllText(folder.File("extends-nothing.json"), """{ "extends": "", "name": "nothing" }""");

        // A folder where a file is expected is what a mistyped path, or an empty "extends", comes to.
        EconomySettingsFormatException aFolder = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Load(folder.Path));
        EconomySettingsFormatException noFile = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Load(folder.File("missing.json")));
        EconomySettingsFormatException emptyBase = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Load(folder.File("extends-nothing.json")));

        Assert.Contains("Cannot read the economy settings", aFolder.Message);
        Assert.Contains("missing.json", noFile.Message);
        Assert.Contains("Cannot read the economy settings", emptyBase.Message);
    }

    [Fact]
    public void A_price_the_finale_pays_back_is_refused()
    {
        // Ten coins a move left: the offer's five moves can leave four, the booster's three can all be left.
        Assert.Contains("extraMovesOffer.priceCoins", Refused("""{ "extraMovesOffer": { "priceCoins": 40 } }""").Message);
        Assert.Equal(new Coins(41), EconomySupport.Settings("""{ "extraMovesOffer": { "priceCoins": 41 } }""").ExtraMovesOffer.Price);
        Assert.Contains("prices.boosters.start-extra-moves", Refused("""{ "prices": { "boosters": { "start-extra-moves": 30 } } }""").Message);
        Assert.Contains("extraMovesOffer.priceCoins", Refused("""{ "coins": { "perMoveLeftInFinale": 225 } }""").Message);
    }

    [Fact]
    public void A_mark_that_was_not_measured_is_refused()
    {
        Assert.Contains("player.extraMoves.takeWhenGoalsDonePercent", Refused("""{ "player": { "extraMoves": { "takeWhenGoalsDonePercent": 70 } } }""").Message);
        Assert.Contains("player.inLevelBoosters.useWhenGoalsDonePercent", Refused("""{ "player": { "inLevelBoosters": { "useWhenGoalsDonePercent": 60 } } }""").Message);
        Assert.Equal(new Percent(0), EconomySupport.Settings("""{ "player": { "extraMoves": { "takeWhenGoalsDonePercent": 0 } } }""").Player.ExtraMoves.TakeWhenGoalsDone);
        Assert.Equal(new Percent(90), EconomySupport.Settings("""{ "player": { "extraMoves": { "takeWhenGoalsDonePercent": 90 } } }""").Player.ExtraMoves.TakeWhenGoalsDone);
    }

    [Fact]
    public void Shares_that_do_not_sum_to_a_hundred_are_refused()
    {
        Assert.Contains("must sum to 100, not 105", Refused("""{ "players": { "skillMixPercent": { "skill-65": 30 } } }""").Message);
        Assert.Contains("no rung of the skill ladder", Refused("""{ "players": { "skillMixPercent": { "skill-52": 0 } } }""").Message);
        Assert.Contains("does not split into whole players", Refused("""{ "players": { "count": 10 } }""").Message);
        Assert.Contains("players.averageRung", Refused("""{ "players": { "averageRung": "skill-70" } }""").Message);

        // A share of 0 leaves a rung out: how a variant file simulates one rung of its base alone.
        EconomySettings oneRung = EconomySupport.Settings("""{ "players": { "skillMixPercent": { "skill-45": 0, "skill-55": 100, "skill-65": 0 } } }""");
        Assert.Equal([EconomySupport.Average], oneRung.Players.Rungs);
    }

    [Theory]
    [InlineData("""{ "habit": { "sessionStarts": ["13:00", "08:30", "21:30"] } }""", "habit.sessionStarts")]
    [InlineData("""{ "habit": { "sessionStarts": ["08:30", "08:50", "21:30"] } }""", "so that sessions never overlap")]
    [InlineData("""{ "habit": { "sessionStarts": ["08:30", "13:00", "23:45"] } }""", "ends on its own day")]
    [InlineData("""{ "habit": { "sessionStarts": ["8.30"] } }""", "is no time of day")]
    [InlineData("""{ "player": { "startBoosters": { "preference": ["hit-row"] } } }""", "is no start booster")]
    [InlineData("""{ "player": { "startBoosters": { "preference": ["start-color-bomb", "start-color-bomb"] } } }""", "names a booster twice")]
    [InlineData("""{ "player": { "startBoosters": { "perAttempt": 2 } } }""", "two board boosters together are not measured")]
    [InlineData("""{ "prices": { "boosters": { "start-freeze": 100 } } }""", "prices.boosters names 'start-freeze'")]
    [InlineData("""{ "measurement": { "plainGamesBySeedPosition": [64, 31] } }""", "must be even")]
    [InlineData("""{ "measurement": { "plainGamesBySeedPosition": [8, 16] } }""", "must not grow with the position")]
    [InlineData("""{ "measurement": { "boardStarts": ["start-rocket-and-bomb", "start-color-bomb"] } }""", "must hold 'plain'")]
    [InlineData("""{ "measurement": { "boardStarts": ["plain", "start-rocket-and-bomb"] } }""", "lacks 'start-color-bomb'")]
    [InlineData("""{ "report": { "paceTarget": { "level": 150 } } }""", "must be one of report.milestones")]
    [InlineData("""{ "report": { "balanceAtDays": [1, 400] } }""", "within players.horizonDays")]
    [InlineData("""{ "name": "my settings" }""", "letters, digits and '-'")]
    [InlineData("""{ "lives": { "max": 0 } }""", "lives.max 0 must be at least 1")]
    [InlineData("""{ "start": { "boosters": { "start-freeze": 1 } } }""", "start.boosters names 'start-freeze'")]
    [InlineData("""{ "start": { "boosters": { "shuffle": -1 } } }""", "start.boosters.shuffle -1 must be at least 0")]
    [InlineData("""{ "startBoosters": { "perAttempt": 0 } }""", "startBoosters.perAttempt 0 must be 1 to 3")]
    [InlineData("""{ "startBoosters": { "perAttempt": 4 } }""", "startBoosters.perAttempt 4 must be 1 to 3")]
    [InlineData("""{ "shop": { "coinPacks": { "big.one": { "coins": 100 } } } }""", "shop.coinPacks has an entry named 'big.one'")]
    [InlineData("""{ "shop": { "coinPacks": { "small": { "coins": 0 } } } }""", "shop.coinPacks.small.coins 0 must be at least 1")]
    [InlineData("""{ "shop": { "boosterSets": { "starter": { "priceCoins": 0 } } } }""", "shop.boosterSets.starter.priceCoins 0 must be at least 1")]
    [InlineData("""{ "shop": { "boosterSets": { "starter": { "boosters": { "hit-diagonal": 1 } } } } }""", "shop.boosterSets.starter.boosters names 'hit-diagonal'")]
    [InlineData("""{ "shop": { "boosterSets": { "starter": { "boosters": { "shuffle": 0 } } } } }""", "shop.boosterSets.starter.boosters.shuffle 0 must be at least 1")]
    [InlineData("""{ "shop": { "boosterSets": { "none": { "priceCoins": 10, "boosters": {} } } } }""", "shop.boosterSets.none.boosters is empty")]
    [InlineData("""{ "shop": { "adForCoins": { "perDay": -1 } } }""", "shop.adForCoins.perDay -1 must be at least 0")]
    [InlineData("""{ "build": { "ads": "live" } }""", "build.ads is 'live'")]
    [InlineData("""{ "build": { "realMoneyPurchases": "store" } }""", "build.realMoneyPurchases is 'store'")]
    public void Settings_that_contradict_themselves_are_refused(string change, string expectedInTheMessage) =>
        Assert.Contains(expectedInTheMessage, Refused(change).Message);

    [Theory]
    [InlineData("""{ "coins": { "perMoveLeftInFinale": 3000000000000000000 } }""", "coins.perMoveLeftInFinale")]
    [InlineData("""{ "coins": { "perLevelWon": 9000000000000000000 } }""", "coins.perLevelWon")]
    [InlineData("""{ "rewards": { "daily": { "coins": 9000000000000000000 } } }""", "rewards.daily.coins")]
    [InlineData("""{ "rewards": { "paintingCompleted": { "coins": 1000001 } } }""", "rewards.paintingCompleted.coins")]
    [InlineData("""{ "extraMovesOffer": { "priceCoins": 1000001 } }""", "extraMovesOffer.priceCoins")]
    [InlineData("""{ "prices": { "boosters": { "shuffle": 1000001 } } }""", "prices.boosters.shuffle")]
    [InlineData("""{ "prices": { "sideCollectionPiece": 1000001 } }""", "prices.sideCollectionPiece")]
    [InlineData("""{ "start": { "coins": 1000001 } }""", "start.coins")]
    [InlineData("""{ "shop": { "coinPacks": { "large": { "coins": 1000001 } } } }""", "shop.coinPacks.large.coins")]
    [InlineData("""{ "shop": { "boosterSets": { "master": { "priceCoins": 1000001 } } } }""", "shop.boosterSets.master.priceCoins")]
    [InlineData("""{ "shop": { "adForCoins": { "coins": 1000001 } } }""", "shop.adForCoins.coins")]
    public void An_amount_of_coins_no_sum_could_hold_is_refused_by_its_field(string change, string field)
    {
        // Without the bound the loader, or the simulation halfway through its players, ended in an overflow with no field named.
        EconomySettingsFormatException refused = Refused(change);

        Assert.Contains(field, refused.Message);
        Assert.Contains("from 0 to 1000000", refused.Message);
    }

    [Fact]
    public void A_player_who_buys_start_boosters_uses_the_ones_they_hold_by_then()
    {
        // "Never use one I hold, buy after N losses": the first reward booster would lie unused and keep the player
        // from ever buying, and the report would read as nobody wanting to buy. The same for using later than buying.
        const string NeverUsesButBuys = """{ "player": { "startBoosters": { "useAfterLossesOnLevel": null, "buyAfterLossesOnLevel": 0 } } }""";
        const string UsesLaterThanBuys = """{ "player": { "startBoosters": { "useAfterLossesOnLevel": 5, "buyAfterLossesOnLevel": 4 } } }""";
        const string UsesAndBuysTogether = """{ "player": { "startBoosters": { "useAfterLossesOnLevel": 3, "buyAfterLossesOnLevel": 3 } } }""";
        const string UsesAndNeverBuys = """{ "player": { "startBoosters": { "useAfterLossesOnLevel": 3, "buyAfterLossesOnLevel": null } } }""";

        Assert.Contains("needs useAfterLossesOnLevel stated and at most 0", Refused(NeverUsesButBuys).Message);
        Assert.Contains("needs useAfterLossesOnLevel stated and at most 4", Refused(UsesLaterThanBuys).Message);
        Assert.Equal(3, EconomySupport.Settings(UsesAndBuysTogether).Player.StartBoosters.BuyAfterLossesOnLevel);
        Assert.Null(EconomySupport.Settings(UsesAndNeverBuys).Player.StartBoosters.BuyAfterLossesOnLevel);
    }

    [Fact]
    public void Every_setting_has_a_unit_and_a_provenance()
    {
        foreach (string file in ShippedFiles.Append(EconomySupport.FixturePath))
        {
            EconomySettings settings = Path.IsPathRooted(file) ? SettingsLoader.Load(file) : Shipped(file);
            Assert.All(settings.Stated, stated =>
            {
                SettingDescription? described = SettingsCatalogue.Find(stated.Path);
                Assert.True(described is not null, $"{file}: '{stated.Path}' has no entry in the catalogue");
                Assert.False(string.IsNullOrWhiteSpace(described.Unit));
                Assert.True(Enum.IsDefined(described.Provenance));
            });

            // And the catalogue describes nothing the format no longer has.
            Assert.All(SettingsCatalogue.Fields, field => Assert.Contains(settings.Stated, stated => field.Describes(stated.Path)));
        }
    }

    [Fact]
    public void What_the_file_states_for_the_game_alone_is_read_whole_and_marked_as_not_simulated()
    {
        GameOnlySettings game = EconomySupport.Settings().Game;

        // The fixture's start, cap, shop and build, as the file states them.
        Assert.Equal(Coins.None, game.StartCoins);
        Assert.Equal(BoosterKinds.All.Order(), game.StartBoosters.Keys.Order());
        Assert.All(game.StartBoosters.Values, count => Assert.Equal(1, count));
        Assert.Equal(3, game.StartBoostersPerAttempt);
        Assert.Equal(new Coins(1200), game.CoinPacks["medium"]);
        Assert.Equal(["large", "medium", "small"], game.CoinPacks.Keys.Order());
        Assert.Equal(new Coins(2100), game.BoosterSets["starter"].Price);
        Assert.Equal(BoosterKinds.All.Where(BoosterKinds.IsStart).Order(), game.BoosterSets["starter"].Boosters.Keys.Order());
        Assert.Equal(3, game.BoosterSets["master"].Boosters[BoosterKind.HitColumn]);
        Assert.Equal((new Coins(50), 3), (game.CoinsForAnAd, game.AdsForCoinsPerDay));
        Assert.Equal((AdsInTheBuild.Simulated, RealMoneyInTheBuild.NotAvailable), (game.Ads, game.RealMoneyPurchases));

        // The catalogue says of each of these that no simulated player meets it, and of nothing else.
        string[] gameOnly = SettingsCatalogue.Fields.Where(field => field.Use == SettingUse.GameOnly).Select(field => field.Path.Split('.')[0]).Distinct().ToArray();
        Assert.Equal(["start", "startBoosters", "shop", "build"], gameOnly);
        Assert.Equal(SettingUse.Simulated, SettingsCatalogue.Describe("lives.max").Use);
        Assert.Equal(SettingUse.Simulated, SettingsCatalogue.Describe("player.startBoosters.perAttempt").Use);
        Assert.Equal(SettingUse.GameOnly, SettingsCatalogue.Describe("startBoosters.perAttempt").Use);
        Assert.Equal(SettingUse.GameOnly, SettingsCatalogue.Describe("shop.boosterSets.starter.boosters.start-color-bomb").Use);
        Assert.All(
            SettingsCatalogue.Fields.Where(field => field.Use == SettingUse.GameOnly),
            field => Assert.Contains(field.Provenance, new[] { Provenance.Placeholder, Provenance.Proposed }));

        // The game may let a player take more start boosters than a simulated player takes: the two are stated apart.
        EconomySettings oneForBoth = EconomySupport.Settings("""{ "startBoosters": { "perAttempt": 1 } }""");
        Assert.Equal(1, oneForBoth.Game.StartBoostersPerAttempt);
    }

    [Fact]
    public void Every_engine_booster_has_a_kind_and_a_price()
    {
        string[] startBoosters = Enum.GetValues<StartBooster>().Select(BoosterNames.Of).ToArray();
        string[] inLevelBoosters = typeof(BoosterUse).GetNestedTypes()
            .Where(type => type.IsSubclassOf(typeof(BoosterUse)))
            .Select(type => BoosterNames.Of((BoosterUse)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type)))
            .ToArray();
        EconomySettings settings = EconomySupport.Settings();

        Assert.Equal(startBoosters.Order(), BoosterKinds.All.Where(BoosterKinds.IsStart).Select(BoosterKinds.Name).Order());
        Assert.Equal(inLevelBoosters.Order(), BoosterKinds.All.Where(BoosterKinds.IsInLevel).Select(BoosterKinds.Name).Order());
        Assert.All(BoosterKinds.All, kind => Assert.True(settings.Prices.Boosters[kind] > Coins.None));
        Assert.Equal(BoosterKinds.All.Order(), Shipped("default.json").Prices.Boosters.Keys.Order());
        Assert.Equal(inLevelBoosters.Order(), settings.Player.InLevelBoosters.AssumedMovesWorth.Keys.Select(BoosterKinds.Name).Order());
        Assert.Equal(BoosterKinds.All.Order(), settings.Rewards.Daily.BoosterRotation.Order());

        // A board booster has measured games of its own; the booster that only adds moves reads the plain ones.
        Assert.Equal(BoardStart.RocketAndBomb, BoosterKind.StartRocketAndBomb.BoardStart());
        Assert.Equal(BoardStart.ColorBomb, BoosterKind.StartColorBomb.BoardStart());
        Assert.Equal(BoardStart.Plain, BoosterKind.StartExtraMoves.BoardStart());
        Assert.Equal([StartBooster.ColorBomb], BoardStart.ColorBomb.Boosters());
    }

    [Fact]
    public void The_simulation_and_the_reports_cannot_play_a_game_and_no_setting_reaches_a_level()
    {
        Assembly economy = typeof(PlayerSimulator).Assembly;
        Type[] simulatingAndReporting = economy.GetTypes()
            .Where(type => type.Namespace is "Puzzle.Economy.Simulating" or "Puzzle.Economy.Reporting")
            .ToArray();
        Assert.Contains(typeof(PlayerSimulator), simulatingAndReporting);
        Assert.Contains(typeof(Puzzle.Economy.Reporting.Summary), simulatingAndReporting);

        // Read from the compiled code, not from its text: no method of the two namespaces calls, constructs or reads
        // anything of the engine, of a bot, or of the three types of the economy that play games themselves.
        Type[] thatPlay = [typeof(EconomyGame), typeof(Measurer), typeof(MeasurementGuard)];
        Assert.All(simulatingAndReporting, type => Assert.All(IlReferences.TypesUsedBy(type), used =>
        {
            Assert.False(used.Namespace == typeof(Game).Namespace, $"{type.FullName} reaches the engine: {used.FullName}");
            Assert.False(used.Assembly == typeof(GreedyPolicy).Assembly, $"{type.FullName} reaches the bots: {used.FullName}");
            Assert.False(thatPlay.Contains(used), $"{type.FullName} reaches {used.Name}, which plays games");
        }));

        // The check sees what it is for: the measurer does reach the one method that plays, and that one the engine and a bot.
        Assert.Contains(typeof(EconomyGame), TypesUsedByAll(economy, typeof(Measurer)));
        Assert.Contains(typeof(Game), IlReferences.TypesUsedBy(typeof(EconomyGame)));
        Assert.Contains(typeof(GreedyPolicy), IlReferences.TypesUsedBy(typeof(EconomyGame)));

        // Only the one method that plays sets a game's options, and it never sets assist; no setting names a limit, a seed pool or assist.
        Type[] settingGameOptions = economy.GetTypes().Where(type => IlReferences.TypesUsedBy(type).Contains(typeof(GameOptions))).ToArray();
        Assert.Equal([typeof(EconomyGame)], settingGameOptions);
        Assert.DoesNotContain(IlReferences.MembersUsedBy(typeof(EconomyGame)), member => member.Name.Contains(nameof(GameOptions.AssistPercent), StringComparison.Ordinal));
        Assert.DoesNotContain(SettingsCatalogue.Fields, field => field.Path.Contains("assist", StringComparison.OrdinalIgnoreCase)
            || field.Path.Contains("moveLimit", StringComparison.OrdinalIgnoreCase)
            || field.Path.Contains("seedPool", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>What a type and the types the compiler nested in it (its lambdas and iterators) use.</summary>
    private static IEnumerable<Type> TypesUsedByAll(Assembly assembly, Type type) =>
        assembly.GetTypes().Where(candidate => candidate == type || candidate.DeclaringType == type).SelectMany(IlReferences.TypesUsedBy);
}
