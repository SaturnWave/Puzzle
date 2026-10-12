using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Puzzle.Bank.Measuring;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Settings;

/// <summary>An economy settings file is malformed; the message names the file and the field.</summary>
public sealed class EconomySettingsFormatException : Exception
{
    public EconomySettingsFormatException(string message)
        : base(message)
    {
    }

    public EconomySettingsFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Reads economy/*.json. Every field must be stated, so the code holds no default; a field the format does not have
/// and a field stated twice are refused, because either would load in silence and price a thousand levels with a
/// number nobody meant. Comments and trailing commas are allowed. A variant file names the file beside it that it
/// <c>extends</c> and states only what differs: objects merge by field, lists and values replace; the merged
/// document is then read as strictly as a whole file.
/// </summary>
public static class SettingsLoader
{
    private const string ExtendsField = "extends";
    private const string NameField = "name";

    /// <summary>
    /// No amount of coins in a settings file may be above this. It is far above any price a game has and far below
    /// what whole-number arithmetic holds, so a sum over every win of every player cannot run over.
    /// </summary>
    private const long LargestAmount = 1_000_000;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly JsonReaderOptions ReaderOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The first bot repeat no purpose of the level bank plays: an economy game is never a game a limit or a pool was chosen from.</summary>
    public static int FirstRepeatOutsideTheBank { get; } =
        Enum.GetValues<GamePurpose>().Max(purpose => purpose.FirstRepeat()) + GamePurposes.RepeatsPerPurpose;

    /// <exception cref="EconomySettingsFormatException">The file is missing, not JSON, incomplete or inconsistent.</exception>
    public static EconomySettings Load(string path) => Read(Resolved(path), path);

    /// <summary>The settings a whole document states (a file's own, or a variant laid over its base).</summary>
    /// <param name="path">Where the document comes from, for the messages.</param>
    /// <exception cref="EconomySettingsFormatException">The document is incomplete or inconsistent.</exception>
    internal static EconomySettings Read(JsonObject resolved, string path)
    {
        SettingsFile file;
        try
        {
            file = resolved.Deserialize<SettingsFile>(Options) ?? throw new EconomySettingsFormatException($"'{path}' is empty.");
        }
        catch (JsonException ex)
        {
            // A missing field, a field the format does not have, or a value of the wrong kind; the message says which.
            throw new EconomySettingsFormatException($"'{path}' is not a valid economy settings file at {ex.Path ?? "$"}: {ex.Message}", ex);
        }

        return new Builder(path).Build(file, Leaves(resolved, prefix: string.Empty).ToArray());
    }

    /// <summary>The file's whole document: its own, or the base it extends with the file's own fields laid over it.</summary>
    internal static JsonObject Resolved(string path)
    {
        JsonObject own = ReadObject(path);
        if (!own.TryGetPropertyValue(ExtendsField, out JsonNode? extended))
        {
            return own;
        }

        own.Remove(ExtendsField);
        string basePath = extended is JsonValue name && name.TryGetValue(out string? baseName)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, baseName)
            : throw new EconomySettingsFormatException($"'{path}': {ExtendsField} must name the settings file beside it that it changes.");
        JsonObject foundation = ReadObject(basePath);
        if (foundation.ContainsKey(ExtendsField))
        {
            throw new EconomySettingsFormatException($"'{path}' extends '{basePath}', which extends a file itself; a variant extends a whole settings file.");
        }

        RequireANameOfItsOwn(own, foundation, path, basePath);
        LayOver(foundation, own);
        return foundation;
    }

    /// <summary>
    /// A variant that kept its base's name would run under that name, and its reports would take the place of the
    /// base's: numbers of one settings file under the name of another.
    /// </summary>
    private static void RequireANameOfItsOwn(JsonObject variant, JsonObject foundation, string path, string basePath)
    {
        string? baseName = TextOf(foundation[NameField]);
        string? ownName = TextOf(variant[NameField]);
        if (ownName is null || ownName == baseName)
        {
            throw new EconomySettingsFormatException(
                $"'{path}' extends '{basePath}' and must state a {NameField} of its own (not '{baseName}'): the name is the folder its reports go to.");
        }
    }

    private static string? TextOf(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>Puts every field of <paramref name="changes"/> into <paramref name="onto"/>: an object into the object of the same name field by field, anything else in its place.</summary>
    internal static void LayOver(JsonObject onto, JsonObject changes)
    {
        foreach ((string name, JsonNode? value) in changes)
        {
            if (value is JsonObject changedFields && onto[name] is JsonObject fields)
            {
                LayOver(fields, changedFields);
            }
            else
            {
                onto[name] = value?.DeepClone();
            }
        }
    }

    private static JsonObject ReadObject(string path)
    {
        try
        {
            string text = File.ReadAllText(path);
            RequireNoRepeatedField(text, path);
            return JsonNode.Parse(text, documentOptions: DocumentOptions) as JsonObject
                ?? throw new EconomySettingsFormatException($"'{path}' is not a settings file: a JSON object is expected.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A missing file, a folder where a file is expected, or a file this user may not read.
            throw new EconomySettingsFormatException($"Cannot read the economy settings '{path}': {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new EconomySettingsFormatException($"'{path}' is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>The JSON reader keeps the last of two fields of one name and says nothing; here that would be a price nobody meant.</summary>
    /// <exception cref="EconomySettingsFormatException">An object states a field twice.</exception>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    private static void RequireNoRepeatedField(string text, string path)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), ReaderOptions);
        var openObjects = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    openObjects.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    openObjects.Pop();
                    break;
                case JsonTokenType.PropertyName when !openObjects.Peek().Add(reader.GetString()!):
                    throw new EconomySettingsFormatException($"'{path}' states '{reader.GetString()}' twice in one object; say it once.");
                default:
                    break;
            }
        }
    }

    /// <summary>Every value of the document with the path of field names that leads to it; a list is one value.</summary>
    private static IEnumerable<StatedSetting> Leaves(JsonObject fields, string prefix)
    {
        foreach ((string name, JsonNode? value) in fields)
        {
            string path = prefix.Length == 0 ? name : $"{prefix}.{name}";
            if (value is JsonObject inner)
            {
                foreach (StatedSetting leaf in Leaves(inner, path))
                {
                    yield return leaf;
                }
            }
            else
            {
                yield return new StatedSetting(path, AsWritten(value));
            }
        }
    }

    private static string AsWritten(JsonNode? value) => value switch
    {
        null => "never",
        JsonArray list => string.Join(' ', list.Select(AsWritten)),
        JsonValue text when text.TryGetValue(out string? words) => words,
        _ => value.ToJsonString(),
    };

    /// <summary>Turns the file's plain values into the settings, refusing what contradicts itself. One instance per file, for its messages.</summary>
    private sealed class Builder(string path)
    {
        public EconomySettings Build(SettingsFile file, IReadOnlyList<StatedSetting> stated)
        {
            MeasurementPlan measurement = MeasurementOf(file.Measurement);
            CoinSettings coins = new()
            {
                PerMoveLeftInFinale = CoinsOf(file.Coins.PerMoveLeftInFinale, "coins.perMoveLeftInFinale"),
                PerLevelWon = CoinsOf(file.Coins.PerLevelWon, "coins.perLevelWon"),
            };
            PopulationSettings players = PlayersOf(file.Players);
            PlayerRules player = RulesOf(file.Player, measurement);
            RequireStartsARuleCanUse(measurement, player.StartBoosters);
            return new EconomySettings
            {
                Name = file.Name.Length > 0 && file.Name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
                    ? file.Name
                    : throw Invalid($"name '{file.Name}' must be letters, digits and '-' (it names the folder the reports go to)"),
                Players = players,
                Habit = HabitOf(file.Habit, measurement.MoveCap),
                Lives = new LivesSettings
                {
                    Max = new LifeCount(AtLeast(file.Lives.Max, 1, "lives.max")),
                    PerLife = TimeSpan.FromMinutes(AtLeast(file.Lives.MinutesPerLife, 1, "lives.minutesPerLife")),
                },
                Coins = coins,
                Rewards = new RewardSettings
                {
                    PaintingCompleted = RewardOf(file.Rewards.PaintingCompleted.Coins, file.Rewards.PaintingCompleted.BoostersEachTime, file.Rewards.PaintingCompleted.BoosterRotation, "rewards.paintingCompleted"),
                    Daily = RewardOf(file.Rewards.Daily.Coins, file.Rewards.Daily.BoostersEachTime, file.Rewards.Daily.BoosterRotation, "rewards.daily"),
                    DailyEveryDays = AtLeast(file.Rewards.Daily.EveryDays, 1, "rewards.daily.everyDays"),
                },
                ExtraMovesOffer = OfferOf(file.ExtraMovesOffer, coins),
                Prices = PricesOf(file.Prices, coins),
                Player = player,
                Measurement = measurement,
                Report = ReportOf(file.Report, players.HorizonDays),
                Observed = new ObservedFigures
                {
                    Players = AtLeast(file.Observed.Players, 0, "observed.players"),
                    MedianAttemptSeconds = AtLeast(file.Observed.MedianAttemptSeconds, 0, "observed.medianAttemptSeconds"),
                    MedianAttemptsPerSession = AtLeast(file.Observed.MedianAttemptsPerSession, 0, "observed.medianAttemptsPerSession"),
                },
                Stated = stated,
            };
        }

        private PopulationSettings PlayersOf(PlayersFile file)
        {
            int count = AtLeast(file.Count, 1, "players.count");
            // A share of 0 leaves the rung out: it is how a variant file takes a rung of its base out of the mix.
            MixShare[] mix = file.SkillMixPercent
                .Select(part => new MixShare(
                    SkillRung.Find(part.Key) ?? throw Invalid($"players.skillMixPercent names '{part.Key}', which is no rung of the skill ladder"),
                    PercentOf(part.Value, $"players.skillMixPercent.{part.Key}")))
                .Where(part => part.Share.Value > 0)
                .OrderBy(part => part.Rung)
                .ToArray();
            Require(mix.Sum(part => part.Share.Value) == 100, $"players.skillMixPercent must sum to 100, not {mix.Sum(part => part.Share.Value)}");
            Require(
                mix.All(part => count * part.Share.Value % 100 == 0),
                $"players.count {count} does not split into whole players by the shares of players.skillMixPercent");
            SkillRung average = SkillRung.Find(file.AverageRung) ?? throw Invalid($"players.averageRung '{file.AverageRung}' is no rung of the skill ladder");
            Require(mix.Any(part => part.Rung == average), $"players.averageRung {average} must be one of the rungs of players.skillMixPercent");
            return new PopulationSettings
            {
                Count = count,
                Seed = file.Seed,
                SkillMix = mix,
                AverageRung = average,
                HorizonDays = AtLeast(file.HorizonDays, 1, "players.horizonDays"),
            };
        }

        private HabitSettings HabitOf(HabitFile file, MoveCount moveCap)
        {
            TimeOnly[] starts = file.SessionStarts
                .Select(text => TimeOnly.TryParseExact(text, "HH:mm", Invariant, DateTimeStyles.None, out TimeOnly start)
                    ? start
                    : throw Invalid($"habit.sessionStarts: '{text}' is no time of day (\"08:30\")"))
                .ToArray();
            Require(starts.Length > 0, "habit.sessionStarts must name at least one session");
            var habit = new HabitSettings
            {
                SessionStarts = starts,
                SessionCeiling = TimeSpan.FromMinutes(AtLeast(file.SessionCeilingMinutes, 1, "habit.sessionCeilingMinutes")),
                PerMove = TimeSpan.FromMilliseconds(AtLeast(file.MillisecondsPerMove, 1, "habit.millisecondsPerMove")),
                BetweenAttempts = TimeSpan.FromSeconds(AtLeast(file.SecondsBetweenAttempts, 0, "habit.secondsBetweenAttempts")),
                PerAd = TimeSpan.FromSeconds(AtLeast(file.SecondsPerAd, 0, "habit.secondsPerAd")),
            };

            // A session may run past its ceiling by the attempt that was under way; the next must not have begun by then.
            TimeSpan longestSession = habit.SessionCeiling + habit.LongestAttempt(moveCap);
            for (int session = 1; session < starts.Length; session++)
            {
                Require(
                    starts[session] > starts[session - 1] && starts[session] - starts[session - 1] >= longestSession,
                    $"habit.sessionStarts must be in order and at least {longestSession.TotalMinutes:0} minutes apart (the ceiling and the longest attempt), so that sessions never overlap");
            }

            Require(
                starts[^1].ToTimeSpan() + longestSession <= TimeSpan.FromDays(1),
                $"habit.sessionStarts: the last session must start at least {longestSession.TotalMinutes:0} minutes before midnight, so that it ends on its own day");
            return habit;
        }

        private RewardRule RewardOf(long coins, int boostersEachTime, List<string> rotation, string field)
        {
            BoosterKind[] boosters = rotation
                .Select(name => BoosterKinds.Find(name) ?? throw Invalid($"{field}.boosterRotation names '{name}', which is no booster ({BoosterNamesListed(BoosterKinds.All)})"))
                .ToArray();
            int each = AtLeast(boostersEachTime, 0, $"{field}.boostersEachTime");
            Require(each == 0 || boosters.Length > 0, $"{field}.boosterRotation is empty, so {field}.boostersEachTime must be 0");
            return new RewardRule { Coins = CoinsOf(coins, $"{field}.coins"), BoostersEachTime = each, BoosterRotation = boosters };
        }

        /// <summary>A price the finale pays back is no price: the moves an offer adds would return more coins than they cost.</summary>
        private ExtraMovesOfferSettings OfferOf(ExtraMovesOfferFile file, CoinSettings coins)
        {
            Coins price = CoinsOf(file.PriceCoins, "extraMovesOffer.priceCoins");
            Coins paidBack = coins.PerMoveLeftInFinale * (ExtraMovesOffer.Moves - 1);
            Require(
                price > paidBack,
                $"extraMovesOffer.priceCoins {price} must be above {paidBack}: a win on the first of the {ExtraMovesOffer.Moves} bought moves leaves {ExtraMovesOffer.Moves - 1}, and the finale would pay the purchase back");
            return new ExtraMovesOfferSettings { FreeAdsPerDay = AtLeast(file.FreeAdsPerDay, 0, "extraMovesOffer.freeAdsPerDay"), Price = price };
        }

        private PriceSettings PricesOf(PricesFile file, CoinSettings coins)
        {
            Dictionary<BoosterKind, Coins> boosters = ByBooster(file.Boosters, BoosterKinds.All, "prices.boosters")
                .ToDictionary(price => price.Key, price => CoinsOf(price.Value, $"prices.boosters.{price.Key.Name()}"));
            int addedMoves = RuleSet.Default.StartBoosterExtraMoves;
            Coins paidBack = coins.PerMoveLeftInFinale * addedMoves;
            Require(
                boosters[BoosterKind.StartExtraMoves] > paidBack,
                $"prices.boosters.{BoosterKind.StartExtraMoves.Name()} {boosters[BoosterKind.StartExtraMoves]} must be above {paidBack}: a win leaves the {addedMoves} added moves, and the finale would pay the purchase back");
            return new PriceSettings
            {
                Boosters = boosters,
                SideCollectionPiece = CoinsOf(AtLeast(file.SideCollectionPiece, 1L, "prices.sideCollectionPiece"), "prices.sideCollectionPiece"),
            };
        }

        private PlayerRules RulesOf(PlayerFile file, MeasurementPlan measurement)
        {
            BoosterKind[] preference = file.StartBoosters.Preference
                .Select(name => BoosterKinds.Find(name) is BoosterKind kind && kind.IsStart()
                    ? kind
                    : throw Invalid($"player.startBoosters.preference names '{name}', which is no start booster ({BoosterNamesListed(BoosterKinds.All.Where(BoosterKinds.IsStart))})"))
                .ToArray();
            Require(preference.Distinct().Count() == preference.Length, "player.startBoosters.preference names a booster twice");
            Require(
                file.StartBoosters.PerAttempt == 1,
                "player.startBoosters.perAttempt must be 1 in this version: two board boosters together are not measured");
            Require(file.InLevelBoosters.PerAttempt == 1, "player.inLevelBoosters.perAttempt must be 1 in this version");
            int? useAfter = LossesOf(file.StartBoosters.UseAfterLossesOnLevel, "player.startBoosters.useAfterLossesOnLevel");
            int? buyAfter = LossesOf(file.StartBoosters.BuyAfterLossesOnLevel, "player.startBoosters.buyAfterLossesOnLevel");
            Require(
                buyAfter is null || useAfter <= buyAfter,
                $"player.startBoosters.buyAfterLossesOnLevel {buyAfter} needs useAfterLossesOnLevel stated and at most {buyAfter}: a player who buys a start booster uses it, "
                + "so a booster they already hold is used by then, and one held unused would keep them from ever buying. To study purchases alone, give no start booster as a reward");
            return new PlayerRules
            {
                ExtraMoves = new ExtraMovesHabit
                {
                    TakeWhenGoalsDone = MarkOf(file.ExtraMoves.TakeWhenGoalsDonePercent, "player.extraMoves.takeWhenGoalsDonePercent", measurement),
                    WatchesAds = file.ExtraMoves.WatchesAds,
                    PaysCoins = file.ExtraMoves.PaysCoins,
                },
                StartBoosters = new StartBoosterHabit
                {
                    UseAfterLossesOnLevel = useAfter,
                    BuyAfterLossesOnLevel = buyAfter,
                    Preference = preference,
                },
                InLevelBoosters = new InLevelBoosterHabit
                {
                    AssumedMovesWorth = ByBooster(file.InLevelBoosters.AssumedMovesWorth, BoosterKinds.All.Where(BoosterKinds.IsInLevel).ToArray(), "player.inLevelBoosters.assumedMovesWorth")
                        .ToDictionary(worth => worth.Key, worth => new MoveCount(AtLeast(worth.Value, 0, $"player.inLevelBoosters.assumedMovesWorth.{worth.Key.Name()}"))),
                    UseWhenGoalsDone = MarkOf(file.InLevelBoosters.UseWhenGoalsDonePercent, "player.inLevelBoosters.useWhenGoalsDonePercent", measurement),
                },
            };
        }

        private MeasurementPlan MeasurementOf(MeasurementBlock file)
        {
            int moveCap = AtLeast(file.MoveCap, 1, "measurement.moveCap");
            Require(moveCap <= LevelDefinition.MaxMoveLimit, $"measurement.moveCap can be at most {LevelDefinition.MaxMoveLimit}");
            Require(
                file.FirstRepeat >= FirstRepeatOutsideTheBank,
                $"measurement.firstRepeat {file.FirstRepeat} lies inside the level bank's bot repeats (0..{FirstRepeatOutsideTheBank - 1}): an economy game must not be a game a limit or a seed pool was chosen from");
            Percent[] marks = file.ProgressMarksPercent.Select(mark => PercentOf(mark, "measurement.progressMarksPercent")).ToArray();
            Require(marks.All(mark => mark.Value > 0) && IsRising(marks.Select(mark => mark.Value)), "measurement.progressMarksPercent must rise, from above 0");
            int[] plain = [.. file.PlainGamesBySeedPosition];
            Require(plain.Length > 0, "measurement.plainGamesBySeedPosition must give at least one number of games");
            Require(
                plain.Append(file.BoostedGamesPerSeed).All(games => games >= 2 && games % 2 == 0),
                "measurement.plainGamesBySeedPosition and boostedGamesPerSeed must be even and at least 2: a run reads each half of a seed's games on its own");
            Require(
                plain.Zip(plain.Skip(1)).All(pair => pair.Second <= pair.First),
                "measurement.plainGamesBySeedPosition must not grow with the position: a later seed of a pool is met less often than an earlier one");
            BoardStart[] starts = file.BoardStarts
                .Select(name => BoardStarts.Find(name) ?? throw Invalid($"measurement.boardStarts names '{name}', which is no board start ({string.Join(", ", BoardStarts.All.Select(BoardStarts.FileName))})"))
                .ToArray();
            Require(starts.Distinct().Count() == starts.Length, "measurement.boardStarts names a board start twice");
            Require(starts.Contains(BoardStart.Plain), $"measurement.boardStarts must hold '{BoardStart.Plain.FileName()}': every other reading starts from it");
            return new MeasurementPlan
            {
                MoveCap = new MoveCount(moveCap),
                FirstRepeat = file.FirstRepeat,
                ProgressMarks = marks,
                PlainGamesBySeedPosition = plain,
                BoostedGamesPerSeed = file.BoostedGamesPerSeed,
                BoardStarts = starts,
            };
        }

        /// <summary>A booster that changes the first board can only be simulated on games that started with it.</summary>
        private void RequireStartsARuleCanUse(MeasurementPlan measurement, StartBoosterHabit habit)
        {
            if (habit.UseAfterLossesOnLevel is null && habit.BuyAfterLossesOnLevel is null)
            {
                return;
            }

            foreach (BoosterKind booster in habit.Preference.Where(booster => !measurement.BoardStarts.Contains(booster.BoardStart())))
            {
                throw Invalid($"measurement.boardStarts lacks '{booster.BoardStart().FileName()}', which player.startBoosters lets a player start with");
            }
        }

        private ReportSettings ReportOf(ReportFile file, int horizonDays)
        {
            Require(file.Milestones.Count > 0 && file.Milestones[0] >= 1 && IsRising(file.Milestones), "report.milestones must be levels in rising order");
            Require(file.Milestones.Contains(file.PaceTarget.Level), $"report.paceTarget.level {file.PaceTarget.Level} must be one of report.milestones");
            Require(file.PaceTarget.FromDays >= 1 && file.PaceTarget.ToDays >= file.PaceTarget.FromDays, "report.paceTarget must run from a day at or after 1 to the same day or a later one");
            Require(
                file.BalanceAtDays.Count > 0 && file.BalanceAtDays[0] >= 1 && IsRising(file.BalanceAtDays) && file.BalanceAtDays[^1] <= horizonDays,
                $"report.balanceAtDays must be days in rising order within players.horizonDays ({horizonDays})");
            return new ReportSettings
            {
                Milestones = file.Milestones.Select(level => new LevelId(level)).ToArray(),
                PaceTarget = new PaceTarget(new LevelId(file.PaceTarget.Level), new DayNumber(file.PaceTarget.FromDays), new DayNumber(file.PaceTarget.ToDays)),
                BalanceAtDays = file.BalanceAtDays.Select(day => new DayNumber(day)).ToArray(),
            };
        }

        /// <summary>The values of an object that must state exactly <paramref name="expected"/>, by booster.</summary>
        private Dictionary<BoosterKind, TValue> ByBooster<TValue>(Dictionary<string, TValue> stated, IReadOnlyList<BoosterKind> expected, string field)
        {
            var values = new Dictionary<BoosterKind, TValue>();
            foreach ((string name, TValue value) in stated)
            {
                BoosterKind kind = BoosterKinds.Find(name) is BoosterKind found && expected.Contains(found)
                    ? found
                    : throw Invalid($"{field} names '{name}'; it holds {BoosterNamesListed(expected)}");
                values[kind] = value;
            }

            Require(values.Count == expected.Count, $"{field} must state every one of {BoosterNamesListed(expected)}");
            return values;
        }

        /// <summary>A share of the goals a rule waits for: 0 (always) or a mark the measurement keeps, since nothing else can be read from a stored game.</summary>
        private Percent MarkOf(int value, string field, MeasurementPlan measurement)
        {
            Percent mark = PercentOf(value, field);
            Require(
                mark.Value == 0 || measurement.ProgressMarks.Contains(mark),
                $"{field} {mark} must be 0 or one of measurement.progressMarksPercent ({string.Join(", ", measurement.ProgressMarks)})");
            return mark;
        }

        private int? LossesOf(int? value, string field) => value is null ? null : AtLeast(value.Value, 0, field);

        private Percent PercentOf(int value, string field)
        {
            Require(value is >= 0 and <= 100, $"{field} {value} must be a percentage from 0 to 100");
            return new Percent(value);
        }

        private Coins CoinsOf(long amount, string field)
        {
            Require(amount is >= 0 and <= LargestAmount, $"{field} {amount} must be an amount of coins from 0 to {LargestAmount}");
            return new Coins(amount);
        }

        private T AtLeast<T>(T value, T least, string field)
            where T : IComparable<T>
        {
            Require(value.CompareTo(least) >= 0, $"{field} {value} must be at least {least}");
            return value;
        }

        private static bool IsRising(IEnumerable<int> numbers)
        {
            int[] list = [.. numbers];
            return list.Zip(list.Skip(1)).All(pair => pair.Second > pair.First);
        }

        private static string BoosterNamesListed(IEnumerable<BoosterKind> boosters) => string.Join(", ", boosters.Select(BoosterKinds.Name));

        private void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw Invalid(message);
            }
        }

        private EconomySettingsFormatException Invalid(string message) => new($"Invalid economy settings '{path}': {message}.");
    }

    private sealed class SettingsFile
    {
        public required string Name { get; init; }

        public required PlayersFile Players { get; init; }

        public required HabitFile Habit { get; init; }

        public required LivesFile Lives { get; init; }

        public required CoinsFile Coins { get; init; }

        public required RewardsFile Rewards { get; init; }

        public required ExtraMovesOfferFile ExtraMovesOffer { get; init; }

        public required PricesFile Prices { get; init; }

        public required PlayerFile Player { get; init; }

        public required MeasurementBlock Measurement { get; init; }

        public required ReportFile Report { get; init; }

        public required ObservedFile Observed { get; init; }
    }

    private sealed class PlayersFile
    {
        public required int Count { get; init; }

        public required ulong Seed { get; init; }

        public required Dictionary<string, int> SkillMixPercent { get; init; }

        public required string AverageRung { get; init; }

        public required int HorizonDays { get; init; }
    }

    private sealed class HabitFile
    {
        public required List<string> SessionStarts { get; init; }

        public required int SessionCeilingMinutes { get; init; }

        public required int MillisecondsPerMove { get; init; }

        public required int SecondsBetweenAttempts { get; init; }

        public required int SecondsPerAd { get; init; }
    }

    private sealed class LivesFile
    {
        public required int Max { get; init; }

        public required int MinutesPerLife { get; init; }
    }

    private sealed class CoinsFile
    {
        public required long PerMoveLeftInFinale { get; init; }

        public required long PerLevelWon { get; init; }
    }

    private sealed class RewardsFile
    {
        public required PaintingRewardFile PaintingCompleted { get; init; }

        public required DailyRewardFile Daily { get; init; }
    }

    private sealed class PaintingRewardFile
    {
        public required long Coins { get; init; }

        public required int BoostersEachTime { get; init; }

        public required List<string> BoosterRotation { get; init; }
    }

    private sealed class DailyRewardFile
    {
        public required long Coins { get; init; }

        public required int EveryDays { get; init; }

        public required int BoostersEachTime { get; init; }

        public required List<string> BoosterRotation { get; init; }
    }

    private sealed class ExtraMovesOfferFile
    {
        public required int FreeAdsPerDay { get; init; }

        public required long PriceCoins { get; init; }
    }

    private sealed class PricesFile
    {
        public required Dictionary<string, long> Boosters { get; init; }

        public required long SideCollectionPiece { get; init; }
    }

    private sealed class PlayerFile
    {
        public required ExtraMovesFile ExtraMoves { get; init; }

        public required StartBoostersFile StartBoosters { get; init; }

        public required InLevelBoostersFile InLevelBoosters { get; init; }
    }

    private sealed class ExtraMovesFile
    {
        public required int TakeWhenGoalsDonePercent { get; init; }

        public required bool WatchesAds { get; init; }

        public required bool PaysCoins { get; init; }
    }

    private sealed class StartBoostersFile
    {
        public required int? UseAfterLossesOnLevel { get; init; }

        public required int? BuyAfterLossesOnLevel { get; init; }

        public required List<string> Preference { get; init; }

        public required int PerAttempt { get; init; }
    }

    private sealed class InLevelBoostersFile
    {
        public required Dictionary<string, int> AssumedMovesWorth { get; init; }

        public required int UseWhenGoalsDonePercent { get; init; }

        public required int PerAttempt { get; init; }
    }

    private sealed class MeasurementBlock
    {
        public required int MoveCap { get; init; }

        public required int FirstRepeat { get; init; }

        public required List<int> ProgressMarksPercent { get; init; }

        public required List<int> PlainGamesBySeedPosition { get; init; }

        public required int BoostedGamesPerSeed { get; init; }

        public required List<string> BoardStarts { get; init; }
    }

    private sealed class ReportFile
    {
        public required List<int> Milestones { get; init; }

        public required PaceTargetFile PaceTarget { get; init; }

        public required List<int> BalanceAtDays { get; init; }
    }

    private sealed class PaceTargetFile
    {
        public required int Level { get; init; }

        public required int FromDays { get; init; }

        public required int ToDays { get; init; }
    }

    private sealed class ObservedFile
    {
        public required int Players { get; init; }

        public required int MedianAttemptSeconds { get; init; }

        public required int MedianAttemptsPerSession { get; init; }
    }
}
