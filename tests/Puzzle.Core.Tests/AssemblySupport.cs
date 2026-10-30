using System.IO;
using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>A folder that lives as long as one test.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"puzzle-assembly-test-{Guid.NewGuid():N}")).FullName;

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

/// <summary>Policies, slots, candidates and banks shaped for the assembler's tests.</summary>
internal static class AssemblySupport
{
    private static readonly Lazy<DifficultyPolicy> LoadedDefaultPolicy = new(() => PolicyLoader.Load(RepoFiles.DefaultPolicyPath));

    /// <summary>policies/default.json, loaded once.</summary>
    public static DifficultyPolicy DefaultPolicy => LoadedDefaultPolicy.Value;

    /// <summary>Few games everywhere, so a test can confirm and audit whole levels quickly.</summary>
    public static CheckSettings QuickChecks => new()
    {
        ConfirmSeeds = 8,
        ConfirmGamesPerSeed = 6,
        AuditGamesPerSeed = 4,
        FirstSeedGames = 12,
        AuditRounds = 2,
        FirstTryTolerance = 0.5,
        CandidatesPerLevel = 3,
        CheckGamesPerSeed = 4,
    };

    /// <summary>Move ranges wide enough for any limit, and an audit that a handful of games can pass: for tests that are about what an assembly does, not about how hard a level is.</summary>
    /// <param name="moves">The move range of every tier; any limit at all when absent.</param>
    public static DifficultyPolicy Lenient(DifficultyPolicy policy, MoveRange? moves = null) => policy with
    {
        Plan = policy.Plan with { Calibration = policy.Plan.Calibration with { WinRateTolerance = 0.5 }, SeedPool = new Bot.Simulation.SeedPoolSettings { PoolSize = 6, TolerancePoints = 30 }, MinPoolSize = 3 },
        Bands = [policy.Bands[0] with { Moves = EveryTier(moves ?? new MoveRange(3, 120)) }],
        Checks = QuickChecks,
    };

    private static TierValues<MoveRange> EveryTier(MoveRange moves) => new(moves, moves, moves, moves);

    /// <summary>The default policy made easier and lenient: every live level it is asked about keeps its board and gets another limit.</summary>
    public static DifficultyPolicy EasierAndLenient => Lenient(DefaultPolicy.WithDifficulty("skill-55", 0.95, 0.85, 0.7, 0.6));

    /// <summary>Copies live levels and their report rows into <paramref name="folder"/>: a small stand-in for the live folder that a test may write to.</summary>
    public static LevelFolder CopyOfLive(TemporaryFolder folder, IEnumerable<int> numbers)
    {
        var live = new LevelFolder(RepoFiles.LiveLevelsFolder);
        var rows = new List<string> { string.Join(',', live.Report.Columns) };
        foreach (FolderLevel level in numbers.Select(number => live.Find(number)!))
        {
            File.Copy(level.Path, folder.File(LevelFolder.FileNameOf(level.Number)));
            rows.Add(level.Row!.Text);
        }

        File.WriteAllText(folder.File(GenerationReport.FileName), string.Concat(rows.Select(row => row + live.Report.Newline)));
        return new LevelFolder(folder.Path);
    }

    /// <summary>Loads a policy from JSON text; <c>PLAN</c> in the text stands for the path of profiles/default.json.</summary>
    public static DifficultyPolicy PolicyFrom(string json)
    {
        using var folder = new TemporaryFolder();
        string path = folder.File("policy.json");
        File.WriteAllText(path, json.Replace("PLAN", RepoFiles.DefaultProfilePath.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal));
        return PolicyLoader.Load(path);
    }

    /// <summary>The default policy with another player and other shares from level 51 on.</summary>
    public static DifficultyPolicy WithDifficulty(this DifficultyPolicy policy, string player, double easy, double normal, double hard, double superHard) => policy with
    {
        Bands = [policy.Bands[0] with { Player = BankSupport.Player(player), WinShare = new TierValues<double>(easy, normal, hard, superHard) }],
    };

    public static LevelSlot Slot(int number, DifficultyPolicy? policy = null) => LevelSlots.Of(policy ?? DefaultPolicy, [number], RecordedLevels.None)[0];

    /// <summary>A candidate made up for the chooser: of the slot's size and colours, with the named families at the strength the slot unlocks.</summary>
    /// <param name="outline">What it looks like; candidates with one outline and the same families look alike to a player.</param>
    public static ChoiceCandidate CandidateFor(LevelSlot slot, int id, string? outline = null, params ObstacleKind[] families)
    {
        StructureNeeds structure = slot.Structure;
        var features = new CandidateFeatures
        {
            Width = structure.Size.Width,
            Height = structure.Size.Height,
            PlayableCells = structure.Size.Width * structure.Size.Height,
            Colors = structure.Colors,
            MaxBoxHp = families.Contains(ObstacleKind.Boxes) ? structure.MaxBoxHp : 0,
            MaxJellyLayers = families.Contains(ObstacleKind.Jelly) ? structure.MaxJellyLayers : 0,
            MaxChainHp = families.Contains(ObstacleKind.Chains) ? structure.MaxChainHp : 0,
            HasItems = families.Contains(ObstacleKind.Items),
            CollectGoals = families.Length == 0 ? 1 : 0,
            IsScalable = families.Length == 0 || families.Contains(ObstacleKind.Items),
        };
        var signature = new LevelSignature(features.Width, features.Height, outline ?? $"outline {id}", string.Join(',', families));
        return new ChoiceCandidate(new CandidateId((UInt128)id), new BoardId((UInt128)id), features, signature);
    }

    /// <summary>A chooser over made-up candidates whose drafted limit is the middle of the level's range unless <paramref name="limits"/> says otherwise.</summary>
    public static Chooser ChooserOver(IEnumerable<ChoiceCandidate> candidates, IReadOnlyDictionary<LevelKind, double>? kindShares = null, IReadOnlyDictionary<CandidateId, int>? limits = null) => new(
        candidates,
        (candidate, ask) => limits is not null && limits.TryGetValue(candidate.Id, out int limit) ? limit : ask.Moves.Mid,
        kindShares ?? new Dictionary<LevelKind, double>(),
        ChoiceWeights.Default,
        noveltyWindow: 5);

    /// <summary>
    /// A bank in <paramref name="folder"/> that holds the live levels named, each measured by three rungs on a few
    /// seeds: enough for an assembly to keep their boards under another policy.
    /// </summary>
    public static OpenTestBank BankOfLiveLevels(TemporaryBankFolder folder, IEnumerable<int> numbers)
    {
        BankStore store = BankStore.Create(folder.Path, bankSeed: 7);
        BankCatalog catalog = store.Read().Catalog;
        BankWriter writer = store.OpenWriter(catalog);
        var importer = new Importer(catalog, writer, BankSupport.SomeEpoch, BankSupport.QuickGrowth with { Seeds = 20 });
        var live = new LevelFolder(RepoFiles.LiveLevelsFolder);
        foreach (int number in numbers)
        {
            importer.Import(live.Find(number)!.Level, $"live level {number}");
        }

        var games = new BankGames(store, writer, BankSupport.SomeEpoch, MeasurementSettings.Default);
        var siblings = new BankSiblings(catalog, writer, BankSupport.SomeEpoch, BankSupport.QuickGrowth with { Seeds = 20 });
        return new OpenTestBank(writer, new BankAccess(store.Identity.BankId, catalog, BankSupport.SomeEpoch, games, siblings), games);
    }
}

/// <summary>A test's bank, open for reading and for the games an assembly adds.</summary>
internal sealed class OpenTestBank(BankWriter writer, BankAccess access, BankGames games) : IDisposable
{
    public BankAccess Access => access;

    public BankGames Games => games;

    public void Dispose() => writer.Dispose();
}
