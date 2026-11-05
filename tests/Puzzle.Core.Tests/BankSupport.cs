using System.IO;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Bot;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>A folder for a bank that lives as long as one test.</summary>
internal sealed class TemporaryBankFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"puzzle-bank-test-{Guid.NewGuid():N}");

    public string ShardFolder => System.IO.Path.Combine(Path, "shards");

    public string OnlyShard => Directory.GetFiles(ShardFolder).Single();

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

/// <summary>Levels, candidates and games shaped for the bank's tests.</summary>
internal static class BankSupport
{
    public static readonly EpochId SomeEpoch = new(0x1122334455667788);

    /// <summary>The second colour of each palette is the one collected, so both levels are the same candidate.</summary>
    public static readonly GemColor[] OtherPalette = [GemColor.Green, GemColor.Blue, GemColor.Purple, GemColor.Orange, GemColor.Pink];

    /// <summary>A small level with every layer in use: two boxes, two chains, four jelly cells, a box goal and a collect goal on the palette's second colour.</summary>
    public static LevelDefinition FixedLevel(
        int number = 7,
        DifficultyTier tier = DifficultyTier.Hard,
        int moves = 30,
        IReadOnlyList<GemColor>? palette = null,
        int collectTarget = 30,
        string boxRow = ".1..2.",
        uint[]? pool = null)
    {
        palette ??= GemColors.First(5);
        var layers = new LayoutLayers
        {
            Cells = ["######", "######", "######", "######", "######"],
            Tiles = ["......", "......", "......", boxRow, "......"],
            Jelly = ["......", "......", "......", "......", "11..11"],
            Chains = ["......", ".1..1.", "......", "......", "......"],
        };
        GoalSpec[] goals = [new GoalSpec.BreakBoxes(2), new GoalSpec.CollectColor(palette[1], collectTarget)];
        return new LevelDefinition(
            new LevelId(number),
            tier,
            LevelLayout.Parse(layers),
            goals,
            moves,
            new SpawnRules { Palette = palette },
            new AttemptPolicy { SeedPool = pool ?? [] });
    }

    /// <summary>A board of collect goals alone, drawn like the grower draws.</summary>
    public static DrawSpec CollectSpec(string size = "9x9", int colors = 5, int collectGoals = 1) => new()
    {
        Size = SizeOf(size),
        PlainShape = false,
        Colors = colors,
        Families = [],
        CollectGoals = collectGoals,
        DensityStep = 0,
        FullJelly = false,
    };

    public static DrawSpec SpecWith(string size, int colors, params FamilyDraw[] families) => CollectSpec(size, colors, collectGoals: 0) with { Families = families };

    public static BoardSize SizeOf(string text) => BoardSize.TryParse(text, out BoardSize size) ? size : throw new ArgumentException($"'{text}' is no board size.", nameof(text));

    public static CandidateContent Drawn(DrawSpec spec, ulong seed) =>
        BoardDrawer.TryDraw(spec, seed) ?? throw new InvalidOperationException($"Seed {seed} drew no board of {spec}.");

    public static BankCandidate AsBankCandidate(CandidateContent content, int seeds = 8) => new(
        content,
        Grower.PlaySeedsOf(content.Id, seeds),
        AmountGrid.BaseOf(content),
        amountStep: 0,
        new CandidateOrigin.Imported("a test"),
        new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc));

    public static PlayerModel Rung(int skill) => SkillLadder.TryFind(SkillLadder.NameOf(skill), out SkillProfile? rung)
        ? PlayerModel.Of(rung)
        : throw new ArgumentOutOfRangeException(nameof(skill), skill, "Not a rung of the ladder.");

    public static PlayerMix Player(string label) => PlayerMix.TryParse(label, out PlayerMix? mix, out string? problem)
        ? mix
        : throw new ArgumentException(problem, nameof(label));

    public static MeasurementRequest BreadthOf(BankCandidate candidate, params int[] skills) => new()
    {
        Candidate = candidate.Content,
        Seeds = candidate.Seeds,
        SeedIndices = Enumerable.Range(0, candidate.Seeds.Count).ToArray(),
        Players = skills.Select(Rung).ToArray(),
        Purpose = GamePurpose.Breadth,
        RepeatFrom = 0,
        RepeatCount = 2,
        Settings = MeasurementSettings.Default,
        Epoch = SomeEpoch,
    };

    /// <summary>Breadth games made up for a test: <paramref name="finishes"/> seed by seed, <paramref name="repeats"/> to a seed.</summary>
    public static StoredGames Games(CandidateId candidate, int skill, byte[] finishes, int repeats = 1, int moveCap = 150) => new(
        new GamesHeader
        {
            Candidate = candidate,
            Epoch = SomeEpoch,
            Player = Rung(skill),
            Purpose = GamePurpose.Breadth,
            SeedIndices = Enumerable.Range(0, finishes.Length / repeats).ToArray(),
            RepeatFrom = 0,
            RepeatCount = repeats,
            MoveCap = moveCap,
            ProgressMarks = [],
        },
        finishes,
        Location: null);

    /// <summary>Quick settings for growing a bank inside a test: three rungs, few seeds.</summary>
    public static GrowthSettings QuickGrowth => new()
    {
        Rungs = [Rung(45), Rung(55), Rung(65)],
        Seeds = 8,
        ProbeSeeds = 4,
        VariantsPerVisit = 4,
        Spare = new SpareRule(0, 1),
    };
}
