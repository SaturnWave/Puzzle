using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Settings;

/// <summary>
/// Everything the simulator assumes about players and the economy, read from one settings file
/// (<see cref="SettingsLoader"/>). No value of it has a default in code, none reaches a level's limit, its seeds or
/// the engine's assist, and none is a purchase with real money: the first version simulates free players only.
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

    /// <summary>What the file states for the game alone; nothing of it is simulated.</summary>
    public required GameOnlySettings Game { get; init; }

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

/// <summary>What the build of the game the settings are for sells for real money. Closed set; the format knows one answer.</summary>
public enum RealMoneyInTheBuild
{
    /// <summary>Nothing: coin packs are shown and cannot be bought.</summary>
    NotAvailable,
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
/// What the settings file states for the game alone (its startBoosters, shop and build sections). The file is the
/// game's too, so these are read and checked here, and the reports list them apart; the simulation never reads one.
/// A simulated player takes one start booster per attempt, never opens the shop and watches no ad for coins,
/// whatever these say.
/// </summary>
public sealed record GameOnlySettings
{
    /// <summary>Start boosters the game lets a player take into one attempt, each kind once.</summary>
    public required int StartBoostersPerAttempt { get; init; }

    /// <summary>Coins in each pack the shop shows for real money, by the pack's name.</summary>
    public required IReadOnlyDictionary<string, Coins> CoinPacks { get; init; }

    public required IReadOnlyDictionary<string, BoosterSetSettings> BoosterSets { get; init; }

    /// <summary>Coins one watched ad gives in the shop.</summary>
    public required Coins CoinsForAnAd { get; init; }

    public required int AdsForCoinsPerDay { get; init; }

    public required AdsInTheBuild Ads { get; init; }

    public required RealMoneyInTheBuild RealMoneyPurchases { get; init; }
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
