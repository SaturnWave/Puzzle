using System.Diagnostics;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>bench &lt;level.json&gt; [--runs 200]</c>: single-thread throughput of the engine (random moves), of the
/// calibration bot and of the usta search bot, the numbers that decide how long generating levels and measuring them takes.
/// </summary>
internal static class BenchCommand
{
    private const int MoveCap = 150;

    public static int Run(CliArgs args)
    {
        LevelDefinition level = LevelJson.Load(args.Required(0, "level.json"));
        int runs = args.Int("runs", 200);
        Console.WriteLine(SimCommand.Describe(level));

        BenchEngine(level, runs);
        BenchGreedy(level, Math.Max(1, runs / 4));
        BenchUsta(level, Math.Max(1, runs / 10));
        return 0;
    }

    private static void BenchEngine(LevelDefinition level, int games)
    {
        var clock = Stopwatch.StartNew();
        long moves = 0;
        for (int i = 0; i < games; i++)
        {
            moves += PlayRandom(level, (ulong)i);
        }

        clock.Stop();
        Console.WriteLine($"engine (random moves): {moves / clock.Elapsed.TotalSeconds:0} moves/s, {clock.Elapsed.TotalMilliseconds / games:0.00} ms/game (single thread)");
    }

    private static void BenchGreedy(LevelDefinition level, int games)
    {
        var clock = Stopwatch.StartNew();
        IBotPolicy policy = SimCommand.CreatePolicy("average");
        long moves = 0;
        for (int i = 0; i < games; i++)
        {
            moves += SimulationRunner.Play(level, policy, (ulong)i, MoveCap).MovesUsed;
        }

        clock.Stop();
        Console.WriteLine($"greedy-average bot  : {moves / clock.Elapsed.TotalSeconds:0} moves/s, {clock.Elapsed.TotalMilliseconds / games:0.0} ms/game (single thread)");
    }

    /// <summary>
    /// The search bot at its default budget. Plays per decision is the engine work behind one move; on all cores each
    /// thread runs slower than alone, so the all-core cost per game is higher than this single-thread figure.
    /// </summary>
    private static void BenchUsta(LevelDefinition level, int games)
    {
        var clock = Stopwatch.StartNew();
        var usta = new PlayTally(new UstaPolicy());
        for (int i = 0; i < games; i++)
        {
            SimulationRunner.Play(level, usta, (ulong)i, MoveCap);
        }

        clock.Stop();
        double seconds = clock.Elapsed.TotalSeconds;
        Console.WriteLine(
            $"usta search bot     : {usta.Decisions / seconds:0} decisions/s, {usta.Plays / (double)usta.Decisions:0} plays/decision, "
            + $"{seconds / games:0.000} thread-s/game (single thread)");
    }

    private static int PlayRandom(LevelDefinition level, ulong seed)
    {
        Game game = Game.Start(level, seed, new GameOptions { MoveLimitOverride = 60 });
        var rng = new Rng(seed);
        var buffer = new List<Move>();
        while (game.Status == GameStatus.Playing)
        {
            buffer.Clear();
            game.CollectLegalMoves(buffer);
            game.TryPlay(buffer[rng.NextInt(buffer.Count)]);
        }

        return game.MovesUsed;
    }

    /// <summary>Lets usta play unchanged while adding up its decisions and the plays behind them.</summary>
    private sealed class PlayTally(UstaPolicy usta) : IBotPolicy
    {
        public string Name => usta.Name;

        public long Decisions { get; private set; }

        public long Plays { get; private set; }

        public Move ChooseMove(Game game, IReadOnlyList<Move> legalMoves, Rng rng)
        {
            Move move = usta.ChooseMove(game, legalMoves, rng);
            Decisions++;
            Plays += usta.LastDecisionPlays;
            return move;
        }
    }
}
