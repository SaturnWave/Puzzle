namespace Puzzle.Economy.Settings;

/// <summary>Where a setting's value comes from, so a reader of a report knows how much to lean on it. Closed set.</summary>
public enum Provenance
{
    /// <summary>A product decision (docs/GAME_DESIGN.md section 19) or a fact of the engine.</summary>
    Decided,

    /// <summary>One of the starting assumptions the owner approved as a default.</summary>
    Assumed,

    /// <summary>An assumption the simulator's design added, or an amount read from its reports and put in use; it waits for the owner's word.</summary>
    Proposed,

    /// <summary>An amount or a price nobody has decided: it is there to be read from the reports and changed.</summary>
    Placeholder,

    /// <summary>Taken from the test server's recordings of people.</summary>
    Recorded,

    /// <summary>How the tool works; it claims nothing about the game.</summary>
    Tool,
}

/// <summary>Who plays by a setting. Closed set.</summary>
public enum SettingUse
{
    /// <summary>The simulation reads it: the numbers of a report rest on it.</summary>
    Simulated,

    /// <summary>
    /// Stated for the game alone. The settings file is the game's too, so the simulator reads the value, checks that
    /// it is well formed and lists it; no simulated player ever meets it and no figure of a report moves with it.
    /// </summary>
    GameOnly,
}

/// <summary>What a field of the settings file is: its path ('*' stands for any one name), its unit, where its value comes from and who plays by it.</summary>
public sealed record SettingDescription(string Path, string Unit, Provenance Provenance, SettingUse Use = SettingUse.Simulated)
{
    public bool Describes(string path)
    {
        string[] pattern = Path.Split('.');
        string[] names = path.Split('.');
        return pattern.Length == names.Length && pattern.Zip(names).All(pair => pair.First == "*" || pair.First == pair.Second);
    }
}

/// <summary>
/// Every field of the settings file, once. The list of settings in a report, the assumptions in a summary and the
/// differences between two files are all read from here, so a field cannot be added to the format without saying
/// what it measures and how firm it is.
/// </summary>
public static class SettingsCatalogue
{
    private const string Coins = "coins";
    private const string Days = "days";
    private const string Moves = "moves";
    private const string Games = "games";
    private const string Boosters = "boosters";
    private const string ShareOfGoals = "percent of the goals";
    private const string YesOrNo = "yes or no";
    private const string AdsADay = "ads a day";

    public static IReadOnlyList<SettingDescription> Fields { get; } =
    [
        new("name", "name", Provenance.Tool),
        new("players.count", "players", Provenance.Tool),
        new("players.seed", "seed", Provenance.Tool),
        new("players.skillMixPercent.*", "percent of the players", Provenance.Assumed),
        new("players.averageRung", "ladder rung", Provenance.Decided),
        new("players.horizonDays", Days, Provenance.Tool),
        new("habit.sessionStarts", "times of day", Provenance.Assumed),
        new("habit.sessionCeilingMinutes", "minutes", Provenance.Assumed),
        new("habit.millisecondsPerMove", "milliseconds", Provenance.Recorded),
        new("habit.secondsBetweenAttempts", "seconds", Provenance.Proposed),
        new("habit.secondsPerAd", "seconds", Provenance.Proposed),
        new("lives.max", "lives", Provenance.Decided),
        new("lives.minutesPerLife", "minutes", Provenance.Decided),
        new("coins.perMoveLeftInFinale", Coins, Provenance.Proposed),
        new("coins.perLevelWon", Coins, Provenance.Placeholder),
        new("rewards.paintingCompleted.coins", Coins, Provenance.Proposed),
        new("rewards.paintingCompleted.boostersEachTime", Boosters, Provenance.Assumed),
        new("rewards.paintingCompleted.boosterRotation", "boosters in turn", Provenance.Assumed),
        new("rewards.daily.coins", Coins, Provenance.Placeholder),
        new("rewards.daily.everyDays", Days, Provenance.Assumed),
        new("rewards.daily.boostersEachTime", Boosters, Provenance.Assumed),
        new("rewards.daily.boosterRotation", "boosters in turn", Provenance.Assumed),
        new("extraMovesOffer.freeAdsPerDay", AdsADay, Provenance.Assumed),
        new("extraMovesOffer.priceCoins", Coins, Provenance.Proposed),

        // The start boosters are measured, so their prices are read from the reports; the first entry that describes
        // a field is the one that counts, and the in-level boosters, which no bot plays, fall to the last one.
        new("prices.boosters.start-rocket-and-bomb", Coins, Provenance.Proposed),
        new("prices.boosters.start-color-bomb", Coins, Provenance.Proposed),
        new("prices.boosters.start-extra-moves", Coins, Provenance.Proposed),
        new("prices.boosters.*", Coins, Provenance.Placeholder),
        new("prices.sideCollectionPiece", Coins, Provenance.Placeholder),

        // What the game reads and no simulated player meets: a simulated player starts with nothing, takes one start
        // booster per attempt (player.startBoosters.perAttempt), never opens the shop and watches no ad for coins.
        new("start.coins", Coins, Provenance.Decided, SettingUse.GameOnly),
        new("start.boosters.*", Boosters, Provenance.Decided, SettingUse.GameOnly),
        new("startBoosters.perAttempt", Boosters, Provenance.Proposed, SettingUse.GameOnly),
        new("shop.coinPacks.*.coins", Coins, Provenance.Placeholder, SettingUse.GameOnly),
        new("shop.boosterSets.*.priceCoins", Coins, Provenance.Placeholder, SettingUse.GameOnly),
        new("shop.boosterSets.*.boosters.*", Boosters, Provenance.Placeholder, SettingUse.GameOnly),
        new("shop.adForCoins.coins", Coins, Provenance.Decided, SettingUse.GameOnly),
        new("shop.adForCoins.perDay", AdsADay, Provenance.Placeholder, SettingUse.GameOnly),
        new("build.ads", "how an ad is shown", Provenance.Proposed, SettingUse.GameOnly),
        new("build.realMoneyPurchases", "what is sold for money", Provenance.Proposed, SettingUse.GameOnly),
        new("player.extraMoves.takeWhenGoalsDonePercent", ShareOfGoals, Provenance.Proposed),
        new("player.extraMoves.watchesAds", YesOrNo, Provenance.Proposed),
        new("player.extraMoves.paysCoins", YesOrNo, Provenance.Proposed),
        new("player.startBoosters.useAfterLossesOnLevel", "losses on the level", Provenance.Proposed),
        new("player.startBoosters.buyAfterLossesOnLevel", "losses on the level", Provenance.Proposed),
        new("player.startBoosters.preference", "boosters in order", Provenance.Proposed),
        new("player.startBoosters.perAttempt", Boosters, Provenance.Proposed),
        new("player.inLevelBoosters.assumedMovesWorth.*", "moves (not measured)", Provenance.Proposed),
        new("player.inLevelBoosters.useWhenGoalsDonePercent", ShareOfGoals, Provenance.Proposed),
        new("player.inLevelBoosters.perAttempt", Boosters, Provenance.Proposed),
        new("measurement.moveCap", Moves, Provenance.Tool),
        new("measurement.firstRepeat", "bot repeat", Provenance.Tool),
        new("measurement.progressMarksPercent", ShareOfGoals, Provenance.Tool),
        new("measurement.plainGamesBySeedPosition", Games, Provenance.Tool),
        new("measurement.boostedGamesPerSeed", Games, Provenance.Tool),
        new("measurement.boardStarts", "board starts", Provenance.Tool),
        new("report.milestones", "levels", Provenance.Tool),
        new("report.paceTarget.level", "level", Provenance.Decided),
        new("report.paceTarget.fromDays", Days, Provenance.Decided),
        new("report.paceTarget.toDays", Days, Provenance.Decided),
        new("report.balanceAtDays", Days, Provenance.Tool),
        new("observed.players", "people", Provenance.Recorded),
        new("observed.medianAttemptSeconds", "seconds", Provenance.Recorded),
        new("observed.medianAttemptsPerSession", "attempts", Provenance.Recorded),
    ];

    /// <summary>What the field at <paramref name="path"/> is; absent when the catalogue has no such field.</summary>
    public static SettingDescription? Find(string path) => Fields.FirstOrDefault(field => field.Describes(path));

    /// <exception cref="InvalidOperationException">The settings format has a field the catalogue does not describe.</exception>
    public static SettingDescription Describe(string path) => Find(path)
        ?? throw new InvalidOperationException($"The settings field '{path}' has no entry in the catalogue: say its unit and where its value comes from.");
}
