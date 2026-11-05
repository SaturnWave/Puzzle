using System.Buffers.Binary;
using System.Security.Cryptography;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Bank.Measuring;

/// <summary>
/// A fingerprint of how the engine and the greedy bot behave, not of their version: a few fixed levels that use every
/// mechanic are played by fixed players on fixed seeds, and every board they pass through is hashed. Two builds with
/// one fingerprint very likely play every stored game alike; the bank adds a second guard that needs no such luck
/// (<see cref="SelfCheck"/> replays stored games). The players are fixed here and not taken from the skill ladder:
/// a rung whose settings change makes that rung's games stale, not the engine's.
/// </summary>
public static class MeasurementFingerprint
{
    private const int MoveCap = 150;
    private const int SeedsPerPairing = 12;
    private const ulong SeedRoot = 0x50_5A_42_31; // "PZB1"

    /// <summary>From a player who misses most moves and judges loosely to one who sees all and always takes the best.</summary>
    private static readonly SkillProfile[] Players =
    [
        new() { Name = "probe-30", NoticePercent = 30, Temperature = 18 },
        new() { Name = "probe-55", NoticePercent = 55, Temperature = 10.5 },
        new() { Name = "probe-85", NoticePercent = 85, Temperature = 3 },
        new() { Name = "probe-100", NoticePercent = 100, Temperature = 0 },
    ];

    private static readonly Lazy<string> Computed = new(Compute);

    /// <summary>The fingerprint of the running build, 64 hex digits; played once per process (well under a second).</summary>
    public static string OfThisBuild => Computed.Value;

    /// <summary>The identity of the epoch a fingerprint opens. <paramref name="generation"/> tells apart epochs the probe cannot (one opened by hand after a change it does not see).</summary>
    public static EpochId EpochOf(string fingerprint, int generation)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes($"{fingerprint}/{generation}"));
        return new EpochId(BinaryPrimitives.ReadUInt64BigEndian(hash));
    }

    internal static string Compute()
    {
        using var hash = new RunningHash();
        LevelDefinition[] levels = ProbeLevels();
        for (int level = 0; level < levels.Length; level++)
        {
            foreach (SkillProfile player in Players)
            {
                var policy = new GreedyPolicy(player);
                for (int game = 0; game < SeedsPerPairing; game++)
                {
                    ulong seed = Rng.Derive(Rng.Derive(SeedRoot, (ulong)level), (ulong)game);
                    PlayHashing(levels[level], policy, seed, hash);
                }
            }
        }

        return hash.Finish();
    }

    /// <summary>One game to its end or the cap, adding the board after every move, then how it ended and what happened in it.</summary>
    private static void PlayHashing(LevelDefinition level, GreedyPolicy policy, ulong seed, RunningHash hash)
    {
        Game game = Game.Start(level, AttemptSeeds.From(seed), new GameOptions { MoveLimitOverride = MoveCap });
        var rng = new Rng(Rng.Derive(seed, 2));
        var legalMoves = new List<Move>();
        hash.Add(game.Board.ComputeHash());
        while (game.Status == GameStatus.Playing)
        {
            legalMoves.Clear();
            game.CollectLegalMoves(legalMoves);
            if (!game.TryPlay(policy.ChooseMove(game, legalMoves, rng)))
            {
                throw new InvalidOperationException($"{policy.Name} chose an illegal move on a probe level.");
            }

            hash.Add(game.Board.ComputeHash());
            hash.Add(BitConverter.DoubleToUInt64Bits(game.Goals.OverallProgress));
        }

        PlayStats totals = game.TotalStats;
        foreach (int count in new[]
        {
            (int)game.Status, game.MovesUsed, totals.GemsCleared, totals.SpecialsCreated, totals.SpecialsActivated, totals.Cascades,
            totals.BoxHpRemoved, totals.JellyLayersRemoved, totals.ChainHpRemoved, totals.ItemsCollected, totals.Shuffles,
        })
        {
            hash.Add((ulong)count);
        }
    }

    /// <summary>
    /// The probe levels. Between them: four to eight colours, boxes of one to three hit points, jelly of one and two
    /// layers, chains of one and two hit points, items lying on the board and spawning, a cut-out outline, every
    /// special tile preset, and a board so small and colourful that it runs out of moves and shuffles. A mechanic
    /// added to the game belongs in a level here.
    /// </summary>
    private static LevelDefinition[] ProbeLevels() =>
    [
        Probe(
            4,
            ["########", "########", "########", "########", "########", "########", "########", "########"],
            goals: [new GoalSpec.CollectColor(GemColor.Red, 40), new GoalSpec.CollectColor(GemColor.Blue, 40)]),
        Probe(
            6,
            ["#########", "#########", "#########", "#########", "#########", "#########", "#########", "#########", "#########"],
            tiles: [".........", ".........", ".........", ".........", ".........", "..1...1..", ".12...21.", "122.3.221", "333333333"],
            chains: [".........", ".1.....1.", "..2...2..", "...1.1...", ".........", ".........", ".........", ".........", "........."],
            goals: [new GoalSpec.BreakBoxes(22), new GoalSpec.BreakChains(6)]),
        Probe(
            5,
            ["..#####..", ".#######.", "#########", "#########", "####.####", "####.####", "#########", "#########", ".#######.", "..#####.."],
            jelly: [".........", ".........", ".........", ".........", "1111.1111", "1221.1221", "122212221", "111111111", ".2222222.", "..11111.."],
            goals: [new GoalSpec.ClearJelly(46), new GoalSpec.CollectColor(GemColor.Green, 30)]),
        Probe(
            8,
            ["##########", "##########", "##########", "##########", "##########", "##########", "##########", "##########", "##########", "##########"],
            tiles: ["..I....I..", "..........", "..........", "..........", "..........", "..........", "..........", "..........", "..........", ".........."],
            items: new ItemSpawnRules { Total = 4, MaxOnBoard = 2, ChancePercent = 7 },
            goals: [new GoalSpec.DropItems(6)]),
        Probe(
            7,
            ["######", "######", "######", "######", "######", "######"],
            tiles: ["......", ".H..V.", "..XM..", "..SC..", "......", "......"],
            goals: [new GoalSpec.CollectColor(GemColor.Red, 25)]),
    ];

    private static LevelDefinition Probe(
        int colors,
        string[] cells,
        GoalSpec[] goals,
        string[]? tiles = null,
        string[]? jelly = null,
        string[]? chains = null,
        ItemSpawnRules? items = null) => new(
            new LevelId(1),
            DifficultyTier.Normal,
            LevelLayout.Parse(new LayoutLayers { Cells = cells, Tiles = tiles, Jelly = jelly, Chains = chains }),
            goals,
            MoveCap,
            new SpawnRules { Palette = GemColors.First(colors), Items = items ?? ItemSpawnRules.None });
}

/// <summary>A SHA-256 over a stream of numbers.</summary>
internal sealed class RunningHash : IDisposable
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly byte[] _number = new byte[sizeof(ulong)];

    public void Add(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_number, value);
        _hash.AppendData(_number);
    }

    /// <summary>The hash of everything added, as 64 hex digits.</summary>
    public string Finish() => Convert.ToHexStringLower(_hash.GetHashAndReset());

    public void Dispose() => _hash.Dispose();
}
