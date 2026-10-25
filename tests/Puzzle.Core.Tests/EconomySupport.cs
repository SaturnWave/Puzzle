using System.IO;
using System.Text.Json.Nodes;
using Puzzle.Bank.Measuring;
using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Reporting;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;

namespace Puzzle.Core.Tests;

/// <summary>A folder for measurement and report files that lives as long as one test.</summary>
internal sealed class TemporaryEconomyFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"puzzle-economy-test-{Guid.NewGuid():N}");

    public TemporaryEconomyFolder() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

/// <summary>Everything a journey told its log, in order.</summary>
internal sealed class JourneyList : IJourneyLog
{
    public List<object> Events { get; } = [];

    public IEnumerable<AttemptPlayed> Attempts => Events.OfType<AttemptPlayed>();

    public IEnumerable<SessionEnded> Sessions => Events.OfType<SessionEnded>();

    public IEnumerable<RewardGiven> Rewards => Events.OfType<RewardGiven>();

    public IEnumerable<DayEnded> Days => Events.OfType<DayEnded>();

    public IEnumerable<PurchaseMade> Purchases => Events.OfType<PurchaseMade>();

    public void AttemptPlayed(AttemptPlayed attempt) => Events.Add(attempt);

    public void SessionEnded(SessionEnded session) => Events.Add(session);

    public void RewardGiven(RewardGiven reward) => Events.Add(reward);

    public void PurchaseMade(PurchaseMade purchase) => Events.Add(purchase);

    public void DayEnded(DayEnded day) => Events.Add(day);
}

/// <summary>
/// Hand-made measurements for the economy's tests: levels whose stored games are written down, so a test says what
/// every attempt meets and no test needs a measured file.
/// </summary>
internal sealed class HandMadeLevels
{
    private readonly List<MeasuredLine> _lines = [];
    private readonly SkillRung[] _rungs;
    private int _levels;

    public HandMadeLevels(params SkillRung[] rungs) => _rungs = rungs.Length > 0 ? rungs : [EconomySupport.Average];

    /// <summary>Adds the next level: <paramref name="seeds"/> are the plain games of its pool seeds, in pool order, the same for every rung.</summary>
    public HandMadeLevels Level(int moveLimit, params MeasuredGame[][] seeds) => Level(moveLimit, DifficultyTier.Normal, seeds);

    public HandMadeLevels Level(int moveLimit, DifficultyTier tier, params MeasuredGame[][] seeds) => Level(moveLimit, tier, _ => seeds);

    /// <summary>Adds the next level with games of its own for each rung: a level one rung passes and another does not.</summary>
    public HandMadeLevels Level(int moveLimit, Func<SkillRung, MeasuredGame[][]> seedsOf) => Level(moveLimit, DifficultyTier.Normal, seedsOf);

    private HandMadeLevels Level(int moveLimit, DifficultyTier tier, Func<SkillRung, MeasuredGame[][]> seedsOf)
    {
        _levels++;
        foreach (SkillRung rung in _rungs)
        {
            _lines.Add(LineOf(_levels, rung, BoardStart.Plain, moveLimit, tier, seedsOf(rung)));
        }

        return this;
    }

    /// <summary>Adds <paramref name="count"/> levels that every attempt wins with <paramref name="movesLeft"/> moves left.</summary>
    public HandMadeLevels EasyLevels(int count, int moveLimit = 20, int movesLeft = 5)
    {
        for (int level = 0; level < count; level++)
        {
            Level(moveLimit, EconomySupport.Always(EconomySupport.Won(moveLimit - movesLeft)));
        }

        return this;
    }

    /// <summary>Gives the level added last its games on a board that starts with a booster's special.</summary>
    public HandMadeLevels Starting(BoardStart start, params MeasuredGame[][] seeds)
    {
        MeasuredLine plain = _lines[^1];
        foreach (SkillRung rung in _rungs)
        {
            _lines.Add(LineOf(_levels, rung, start, plain.MoveLimit.Value, plain.Tier, seeds));
        }

        return this;
    }

    public Measurement Build() => new(EconomySupport.Spec, _lines);

    /// <summary>The levels as a run of <paramref name="settings"/> reads them.</summary>
    public SimulatedLevels For(EconomySettings settings) => SimulatedLevels.Of(settings, Build());

    private static MeasuredLine LineOf(int level, SkillRung rung, BoardStart start, int moveLimit, DifficultyTier tier, MeasuredGame[][] seeds) => new()
    {
        Level = new LevelId(level),
        Fingerprint = "hand-made",
        Tier = tier,
        MoveLimit = new MoveCount(moveLimit),
        Pool = Enumerable.Range(1, seeds.Length).Select(seed => (uint)seed).ToArray(),
        Player = rung.Model,
        Start = start,
        Seeds = seeds.Select(games => new SeedGames(games, EconomySupport.Spec.ProgressMarks)).ToArray(),
    };
}

internal static class EconomySupport
{
    /// <summary>The rung the tests' one player has unless a test says otherwise.</summary>
    public static readonly SkillRung Average = SkillRung.Parse("skill-55");

    private static readonly Percent[] Marks = [new(50), new(80), new(90)];

    /// <summary>The spec of a hand-made measurement: the shipped marks and moves, two games a seed.</summary>
    public static MeasurementSpec Spec { get; } = new()
    {
        Engine = "hand-made",
        MoveCap = new MoveCount(150),
        FirstRepeat = 10000,
        ProgressMarks = Marks,
        PlainGamesBySeedPosition = [2],
        BoostedGamesPerSeed = 2,
        StartBoosterExtraMoves = new MoveCount(3),
        ExtraMovesOfferMoves = new MoveCount(5),
    };

    /// <summary>
    /// The settings every test of a rule starts from: a fixture of the tests' own (tests/fixtures/economy-settings.json),
    /// not the shipped economy/default.json, whose prices, amounts and habits are placeholders their owner changes.
    /// </summary>
    public static string FixturePath => RepoFiles.EconomySettingsFixturePath;

    /// <summary>The fixture as a whole document, for a test that takes a field out of it.</summary>
    public static JsonObject FixtureDocument() => SettingsLoader.Resolved(FixturePath);

    /// <summary>The fixture's settings with <paramref name="changes"/> laid over them, each a JSON object of the fields that differ.</summary>
    public static EconomySettings Settings(params string[] changes) => SettingsLoader.Read(Document(changes), "the test's settings");

    /// <summary>
    /// Settings for tests of the rules: one skill-55 player who takes no offer and no booster unless
    /// <paramref name="changes"/> say so, with ten seconds a move and no pause between attempts, so times are easy to
    /// count: a 30-move attempt takes five minutes.
    /// </summary>
    public static EconomySettings OnePlayer(params string[] changes)
    {
        JsonObject document = Document(
        [
            """{ "players": { "count": 1, "horizonDays": 30 } }""",
            """{ "habit": { "millisecondsPerMove": 10000, "secondsBetweenAttempts": 0, "secondsPerAd": 0 } }""",
            """{ "player": { "extraMoves": { "watchesAds": false, "paysCoins": false }, "startBoosters": { "useAfterLossesOnLevel": null, "buyAfterLossesOnLevel": null } } }""",
            """{ "rewards": { "paintingCompleted": { "boostersEachTime": 0 }, "daily": { "boostersEachTime": 0 } } }""",
            """{ "report": { "balanceAtDays": [1, 2, 3] } }""",
            .. changes,
        ]);
        if (!changes.Any(change => change.Contains("skillMixPercent", StringComparison.Ordinal)))
        {
            document["players"]!["skillMixPercent"] = new JsonObject { [Average.Name] = 100 };
        }

        return SettingsLoader.Read(document, "the test's settings");
    }

    public static SimulatedPlayer Player(int index = 0, ulong seed = 7) => new(index, Average, new PlayerSeed(seed));

    /// <summary>Plays one player's whole journey and hands back everything that happened.</summary>
    public static JourneyList Journey(EconomySettings settings, HandMadeLevels levels, SimulatedPlayer? player = null)
    {
        var log = new JourneyList();
        new PlayerSimulator(settings, levels.For(settings)).Play(player ?? Player(), log);
        return log;
    }

    /// <summary>A game won on <paramref name="onMove"/>; 80 % of its goals were done on <paramref name="nearlyDoneOn"/> (by default only with the win).</summary>
    public static MeasuredGame Won(int onMove, int? nearlyDoneOn = null) =>
        Game(new GameEnd.Won(new MoveCount(onMove)), nearlyDoneOn ?? onMove, doneOn: onMove);

    /// <summary>A game still running at the cap; 80 % of its goals were done on <paramref name="nearlyDoneOn"/>, or never.</summary>
    public static MeasuredGame Unfinished(int? nearlyDoneOn = null) => Game(new GameEnd.Unfinished(), nearlyDoneOn, doneOn: null);

    /// <summary>A game whose board had no move left after <paramref name="afterMove"/>.</summary>
    public static MeasuredGame Stuck(int afterMove, int? nearlyDoneOn = null) => Game(new GameEnd.Stuck(new MoveCount(afterMove)), nearlyDoneOn, doneOn: null);

    /// <summary>A pool seed all of whose stored games are <paramref name="game"/>: whatever a player's luck, this is what they meet.</summary>
    public static MeasuredGame[] Always(MeasuredGame game) => [game, game];

    private static MeasuredGame Game(GameEnd end, int? nearlyDoneOn, int? doneOn)
    {
        MoveCount? nearly = nearlyDoneOn is int move ? new MoveCount(move) : null;
        MoveCount? done = doneOn is int last ? new MoveCount(last) : null;
        return new MeasuredGame(end, Marks, [nearly, nearly, done]);
    }

    private static JsonObject Document(IEnumerable<string> changes)
    {
        JsonObject document = FixtureDocument();
        foreach (string change in changes)
        {
            SettingsLoader.LayOver(document, JsonNode.Parse(change)!.AsObject());
        }

        return document;
    }
}

/// <summary>A small game for tests of whole runs: thirty levels three rungs play, with games that differ by seed, by luck and by board start.</summary>
internal static class SmallWorld
{
    public const int LevelCount = 30;
    public const int Limit = 20;

    private static readonly SkillRung Weak = SkillRung.Parse("skill-45");
    private static readonly SkillRung Strong = SkillRung.Parse("skill-65");

    /// <summary>
    /// Every level has three pool seeds of two stored games each. A typical win needs 12 to 20 moves of the 20, two more
    /// for the weak rung and two fewer for the strong one; the second seed is always winnable, so no rung meets a wall.
    /// </summary>
    public static HandMadeLevels Levels()
    {
        var world = new HandMadeLevels(Weak, EconomySupport.Average, Strong);
        for (int level = 1; level <= LevelCount; level++)
        {
            int needed = 12 + (level * 7 % 9);
            world.Level(Limit, rung => SeedsNeeding(needed - ((rung.Skill - EconomySupport.Average.Skill) / 5)))
                .Starting(BoardStart.RocketAndBomb, SeedsNeeding(needed - 2))
                .Starting(BoardStart.ColorBomb, SeedsNeeding(needed - 5));
        }

        return world;
    }

    /// <summary>The fixture's rules for forty players of its mix, with milestones the small world has.</summary>
    public static EconomySettings Settings(params string[] changes) => EconomySupport.Settings(
    [
        """{ "players": { "count": 40, "horizonDays": 60 } }""",
        """{ "report": { "milestones": [10, 30], "paceTarget": { "level": 10, "fromDays": 1, "toDays": 2 }, "balanceAtDays": [1, 2, 3] } }""",
        .. changes,
    ]);

    /// <summary>Everything a run of <paramref name="settings"/> on <paramref name="world"/> reports.</summary>
    public static EconomyReport Report(EconomySettings settings, HandMadeLevels? world = null)
    {
        Measurement measurement = (world ?? Levels()).Build();
        SimulatedLevels levels = SimulatedLevels.Of(settings, measurement);
        var identity = new MeasurementIdentity("hand-made", measurement.Spec, measurement.GameCount, levels.Count, Checked: null);
        return EconomyReport.Of("the test's settings", PopulationRun.Play(settings, levels), measurement, identity);
    }

    private static MeasuredGame[][] SeedsNeeding(int moves) =>
    [
        [EconomySupport.Won(moves, nearlyDoneOn: moves - 4), EconomySupport.Won(moves + 3, nearlyDoneOn: moves - 2)],
        [EconomySupport.Won(moves - 3, nearlyDoneOn: moves - 6), EconomySupport.Won(moves - 2, nearlyDoneOn: moves - 5)],
        [EconomySupport.Won(moves + 1, nearlyDoneOn: moves - 3), EconomySupport.Unfinished(nearlyDoneOn: moves + 2)],
    ];
}
