using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Settings;

/// <summary>
/// Everything the simulator assumes about players and the economy, read from one settings file
/// (<see cref="SettingsLoader"/>). No value of it has a default in code, and none reaches a level's limit, its seeds
/// or the engine's assist: what a player buys, with coins or with money, never changes a game they meet. What the
/// game sells for real money is bought by a simulated player whose habit says so (<see cref="PurchaseHabit"/>); the
/// default player is free and buys nothing.
/// </summary>
public sealed record EconomySettings
{
    public required string Name { get; init; }

    public required PopulationSettings Players { get; init; }

    public required HabitSettings Habit { get; init; }

    public required LivesSettings Lives { get; init; }

    public required CoinSettings Coins { get; init; }

    public required RewardSettings Rewards { get; init; }

    public required ExtraMovesOfferSettings ExtraMovesOffer { get; init; }

    public required PriceSettings Prices { get; init; }

    /// <summary>What a new player is given before the first level (the file's start section).</summary>
    public required WelcomeGift Start { get; init; }

    /// <summary>When the boosters used during play open, and what each brings then (the file's boosterUnlocks section).</summary>
    public required BoosterUnlockSettings Unlocks { get; init; }

    /// <summary>What the file states for the game alone; nothing of it is simulated.</summary>
    public required GameOnlySettings Game { get; init; }

    /// <summary>What the game sells for real money (the file's store section); a simulated player buys it only as their purchase habit says.</summary>
    public required StoreSettings Store { get; init; }

    public required PlayerRules Player { get; init; }

    public required MeasurementPlan Measurement { get; init; }

    public required ReportSettings Report { get; init; }

    public required ObservedFigures Observed { get; init; }

    /// <summary>Every value of the file as it was resolved, by its path: what the reports list and two files are compared by.</summary>
    public required IReadOnlyList<StatedSetting> Stated { get; init; }
}

/// <summary>One value of a settings file: where it stands ("habit.sessionCeilingMinutes") and what it says, as written.</summary>
public sealed record StatedSetting(string Path, string Value);

/// <summary>A rung and the share of the players who play like it.</summary>
public sealed record MixShare(SkillRung Rung, Percent Share);

/// <summary>Who is simulated.</summary>
public sealed record PopulationSettings
{
    public required int Count { get; init; }

    /// <summary>Player i's own seed is derived from this and i.</summary>
    public required ulong Seed { get; init; }

    /// <summary>The rungs of the mix, weakest first; the shares sum to 100 and split <see cref="Count"/> exactly.</summary>
    public required IReadOnlyList<MixShare> SkillMix { get; init; }

    /// <summary>The rung the reports single out as the average player.</summary>
    public required SkillRung AverageRung { get; init; }

    /// <summary>A journey is followed for this many days at most.</summary>
    public required int HorizonDays { get; init; }

    public IEnumerable<SkillRung> Rungs => SkillMix.Select(part => part.Rung);
}

/// <summary>When and how long a player plays.</summary>
public sealed record HabitSettings
{
    /// <summary>The times of day a session starts, in order.</summary>
    public required IReadOnlyList<TimeOnly> SessionStarts { get; init; }

    /// <summary>No attempt starts once a session has lasted this long (wins cost no life, so lives alone would never end one).</summary>
    public required TimeSpan SessionCeiling { get; init; }

    public required TimeSpan PerMove { get; init; }

    public required TimeSpan BetweenAttempts { get; init; }

    public required TimeSpan PerAd { get; init; }

    /// <summary>The longest an attempt can last: every move to the measurement's cap, an ad and the pause after it.</summary>
    public TimeSpan LongestAttempt(MoveCount moveCap) => (PerMove * moveCap.Value) + PerAd + BetweenAttempts;
}

public sealed record LivesSettings
{
    public required LifeCount Max { get; init; }

    /// <summary>One life comes back after this long.</summary>
    public required TimeSpan PerLife { get; init; }
}

/// <summary>What a win pays.</summary>
public sealed record CoinSettings
{
    /// <summary>Paid for every move left when a level is won (the finale turns each into a rocket).</summary>
    public required Coins PerMoveLeftInFinale { get; init; }

    public required Coins PerLevelWon { get; init; }
}

/// <summary>What a reward gives each time it is given. Nothing is random: the boosters come from the rotation in turn.</summary>
public sealed record RewardRule
{
    public required Coins Coins { get; init; }

    public required int BoostersEachTime { get; init; }

    public required IReadOnlyList<BoosterKind> BoosterRotation { get; init; }

    /// <summary>The boosters of reward number <paramref name="rewardNumber"/> of its kind (from 0): the next ones of the rotation.</summary>
    public IEnumerable<BoosterKind> BoostersOf(int rewardNumber)
    {
        for (int booster = 0; booster < BoostersEachTime; booster++)
        {
            yield return BoosterRotation[(int)((((long)rewardNumber * BoostersEachTime) + booster) % BoosterRotation.Count)];
        }
    }
}

public sealed record RewardSettings
{
    /// <summary>Given when a painting's last level is won.</summary>
    public required RewardRule PaintingCompleted { get; init; }

    /// <summary>Given at the first session of a day, every <see cref="DailyEveryDays"/> days from the first.</summary>
    public required RewardRule Daily { get; init; }

    public required int DailyEveryDays { get; init; }
}

public sealed record ExtraMovesOfferSettings
{
    /// <summary>The offer costs an ad this many times a day; beyond that it costs coins.</summary>
    public required int FreeAdsPerDay { get; init; }

    public required Coins Price { get; init; }
}

public sealed record PriceSettings
{
    public required IReadOnlyDictionary<BoosterKind, Coins> Boosters { get; init; }

    /// <summary>Nothing buys one in this version; balances are also shown as the pieces they would buy.</summary>
    public required Coins SideCollectionPiece { get; init; }
}

/// <summary>How an ad is shown by the build of the game the settings are for. Closed set; the format knows one answer.</summary>
public enum AdsInTheBuild
{
    /// <summary>A card stands in for the ad: nothing is advertised.</summary>
    Simulated,
}

/// <summary>What the build of the game the settings are for sells for real money. Closed set.</summary>
public enum RealMoneyInTheBuild
{
    /// <summary>Nothing: the store's coin packs are shown and cannot be bought (a test build).</summary>
    NotAvailable,

    /// <summary>The store's products, through the platform's own store, which takes the money.</summary>
    ThroughTheStore,
}

/// <summary>A set of boosters the game's shop sells for coins.</summary>
public sealed record BoosterSetSettings(Coins Price, IReadOnlyDictionary<BoosterKind, int> Boosters);

/// <summary>
/// What a new player is given before the first level, in the game and in the simulation alike: coins, and boosters
/// of every kind (a count may be 0).
/// </summary>
public sealed record WelcomeGift(Coins Coins, IReadOnlyDictionary<BoosterKind, int> Boosters)
{
    /// <summary>The boosters one by one, in the engine's order of kinds.</summary>
    public IEnumerable<BoosterKind> BoostersOneByOne => BoosterKinds.All.SelectMany(kind => Enumerable.Repeat(kind, Boosters[kind]));
}

/// <summary>
/// When the boosters used during play open to a player, and the gift each brings then: the welcome gift's part for
/// them, given on the booster's level instead of before the first (the owner's decision of 2026-10-15). A simulated
/// player uses none before its level. The game keeps the same rule (Puzzle.Meta's BoosterUnlocks).
/// </summary>
/// <param name="Levels">The level each booster used during play opens on.</param>
/// <param name="Gift">Boosters of its own kind each brings as it opens; a count may be 0.</param>
public sealed record BoosterUnlockSettings(IReadOnlyDictionary<BoosterKind, LevelId> Levels, IReadOnlyDictionary<BoosterKind, int> Gift)
{
    /// <summary>True when the booster can be used on the level: a start booster always, one used during play from its own level on.</summary>
    public bool IsOpenOn(BoosterKind kind, LevelId level) => kind.IsStart() || level.Value >= Levels[kind].Value;

    /// <summary>The gift of every booster that opens on exactly this level, booster by booster, in the engine's order of kinds.</summary>
    public IEnumerable<BoosterKind> GiftOpeningOn(LevelId level) =>
        BoosterKinds.All.Where(kind => kind.IsInLevel() && Levels[kind] == level).SelectMany(kind => Enumerable.Repeat(kind, Gift[kind]));
}

/// <summary>
/// What the settings file states for the game alone (its startBoosters, hints, shop and build sections). The file is
/// the game's too, so these are read and checked here, and the reports list them apart; the simulation never reads
/// one. A simulated player takes one start booster per attempt, is shown no hint, never opens the shop and watches
/// no ad for coins, whatever these say.
/// </summary>
public sealed record GameOnlySettings
{
    /// <summary>Start boosters the game lets a player take into one attempt, each kind once.</summary>
    public required int StartBoostersPerAttempt { get; init; }

    /// <summary>When the game shows a hint by itself, how many an attempt, and whose move it shows.</summary>
    public required HintSettings Hints { get; init; }

    public required IReadOnlyDictionary<string, BoosterSetSettings> BoosterSets { get; init; }

    /// <summary>Coins one watched ad gives in the shop.</summary>
    public required Coins CoinsForAnAd { get; init; }

    public required int AdsForCoinsPerDay { get; init; }

    public required AdsInTheBuild Ads { get; init; }

    public required RealMoneyInTheBuild RealMoneyPurchases { get; init; }
}

/// <summary>
/// The game's hint (Puzzle.Bot's HintAdvisor): after <see cref="Idle"/> on a board at rest with no move and no touch,
/// the move of the player model <see cref="Bot"/> is shown, at most <see cref="PerAttempt"/> times an attempt.
/// </summary>
public sealed record HintSettings
{
    /// <summary>How long the board lies at rest, untouched, before a hint shows itself: a whole number of seconds, at least one.</summary>
    public required TimeSpan Idle { get; init; }

    /// <summary>Hints an attempt shows at most; 0 shows none.</summary>
    public required int PerAttempt { get; init; }

    /// <summary>The player model whose move a hint shows, by its name in the bot family (BotPolicies.IsPlayerModel).</summary>
    public required string Bot { get; init; }
}

/// <summary>
/// Something the game sells for real money: what it gives, its price in the US store, and the end of its product id
/// in the stores. Its contents are listed, so nothing of it is random, and nothing of it reaches a level.
/// </summary>
/// <param name="Name">A coin pack's name in the file ("pouch"), or <see cref="StarterPackSettings.Name"/>.</param>
/// <param name="ProductIdSuffix">
/// What follows the game's application id and a dot in the product's id in both stores: with the application id
/// com.saturnwave.puzzle, "coins.pouch" is the product com.saturnwave.puzzle.coins.pouch. The game puts the two
/// together, so the ids follow the application id when that is decided.
/// </param>
/// <param name="Price">The price point in the US store; every other country takes the store's own equivalent.</param>
/// <param name="Boosters">The boosters it gives besides the coins, each kind with a count of at least 1; none for a coin pack.</param>
public sealed record StoreProduct(string Name, string ProductIdSuffix, UsDollars Price, Coins Coins, IReadOnlyDictionary<BoosterKind, int> Boosters)
{
    /// <summary>The boosters one by one, in the engine's order of kinds.</summary>
    public IEnumerable<BoosterKind> BoostersOneByOne => BoosterKinds.All.SelectMany(kind => Enumerable.Repeat(kind, Boosters.GetValueOrDefault(kind)));

    /// <summary>Its coins and its boosters at the shop's coin prices: what the pricing rules weigh against its price.</summary>
    public Coins WorthAt(IReadOnlyDictionary<BoosterKind, Coins> boosterPrices) =>
        Boosters.Aggregate(Coins, (worth, held) => worth + (boosterPrices[held.Key] * held.Value));
}

/// <summary>The starter pack: a product sold a limited number of times to a player, and only from a level on.</summary>
/// <param name="TimesPerPlayer">How many times one player can buy it in the game. A simulated player buys it once, when it is first offered.</param>
/// <param name="FromLevel">The shop offers it once the player has come to this level.</param>
public sealed record StarterPackSettings(StoreProduct Product, int TimesPerPlayer, LevelId FromLevel)
{
    /// <summary>The starter pack's name in the reports and in a purchase.</summary>
    public const string Name = "starter-pack";
}

/// <summary>What the game sells for real money: coin packs, in the order the shop shows them, and the starter pack.</summary>
public sealed record StoreSettings(IReadOnlyList<StoreProduct> CoinPacks, StarterPackSettings StarterPack)
{
    /// <summary>Every product, the coin packs first.</summary>
    public IEnumerable<StoreProduct> Products => CoinPacks.Append(StarterPack.Product);
}

/// <summary>A coin pack bought on day 1 and every <paramref name="EveryDays"/> days after, whatever the player's purse holds.</summary>
public sealed record ScheduledPurchase(StoreProduct Pack, int EveryDays)
{
    public bool IsDueOn(DayNumber day) => (day.Value - 1) % EveryDays == 0;
}

/// <summary>What a simulated player buys for real money (the settings' player.purchases). A free player buys nothing.</summary>
public sealed record PurchaseHabit
{
    /// <summary>
    /// The coin pack bought when the extra moves are wanted, no free ad is left today and the coins fall short of
    /// their price; absent for a player who buys no coins for them. The pack pays for the moves by itself.
    /// </summary>
    public required StoreProduct? PackWhenShortForExtraMoves { get; init; }

    /// <summary>The starter pack is bought the first time the shop offers it.</summary>
    public required bool BuysTheStarterPack { get; init; }

    /// <summary>A coin pack bought on a schedule; absent for a player who buys none so.</summary>
    public required ScheduledPurchase? OnASchedule { get; init; }

    /// <summary>True when the habit buys anything at all; a player without one is a free player.</summary>
    public bool PaysMoney => PackWhenShortForExtraMoves is not null || BuysTheStarterPack || OnASchedule is not null;
}

/// <summary>When a player takes the extra-moves offer.</summary>
public sealed record ExtraMovesHabit
{
    /// <summary>The offer is wanted when at least this share of the goals is done; 0 means always.</summary>
    public required Percent TakeWhenGoalsDone { get; init; }

    public required bool WatchesAds { get; init; }

    public required bool PaysCoins { get; init; }
}

/// <summary>When a player starts an attempt with a booster.</summary>
public sealed record StartBoosterHabit
{
    /// <summary>An owned booster is used after this many losses on the level; absent means never.</summary>
    public required int? UseAfterLossesOnLevel { get; init; }

    /// <summary>
    /// With none owned, one is bought after this many losses on the level; absent means never. Never fewer losses than
    /// <see cref="UseAfterLossesOnLevel"/>, which is then stated: a player who buys a booster uses it, so one they
    /// hold is used by then and cannot lie unused in a purchase's way.
    /// </summary>
    public required int? BuyAfterLossesOnLevel { get; init; }

    /// <summary>The start boosters in the order the player reaches for them.</summary>
    public required IReadOnlyList<BoosterKind> Preference { get; init; }
}

/// <summary>
/// A rule of thumb, not a measurement: no bot knows when to use an in-level booster, so none was played. A booster
/// whose assumed worth is above 0 is read as that many more moves of the stored game.
/// </summary>
public sealed record InLevelBoosterHabit
{
    public required IReadOnlyDictionary<BoosterKind, MoveCount> AssumedMovesWorth { get; init; }

    /// <summary>One is used when the moves ran out with at least this share of the goals done.</summary>
    public required Percent UseWhenGoalsDone { get; init; }

    public MoveCount LargestAssumedWorth => AssumedMovesWorth.Values.Max();

    public bool AssumesAnEffect => LargestAssumedWorth > MoveCount.None;
}

public sealed record PlayerRules
{
    public required ExtraMovesHabit ExtraMoves { get; init; }

    public required StartBoosterHabit StartBoosters { get; init; }

    public required InLevelBoosterHabit InLevelBoosters { get; init; }

    public required PurchaseHabit Purchases { get; init; }
}

/// <summary>The pace the owner wants a free, average player to have: the level won between these two days. It is reported against and never forced.</summary>
public sealed record PaceTarget(LevelId Level, DayNumber From, DayNumber To);

public sealed record ReportSettings
{
    public required IReadOnlyList<LevelId> Milestones { get; init; }

    public required PaceTarget PaceTarget { get; init; }

    /// <summary>The days at whose end balances are listed.</summary>
    public required IReadOnlyList<DayNumber> BalanceAtDays { get; init; }
}

/// <summary>What recordings of people showed. The simulation never reads these; the summary prints them beside what the model implies.</summary>
public sealed record ObservedFigures
{
    public required int Players { get; init; }

    public required int MedianAttemptSeconds { get; init; }

    public required int MedianAttemptsPerSession { get; init; }
}
