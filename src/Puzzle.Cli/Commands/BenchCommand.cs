using System.Diagnostics;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>bench &lt;level.json&gt; [--runs 200]</c>: single-thread throughput of the engine (random moves) and of the
/// calibration bot, the two numbers that decide how long generating 1000 levels takes.
/// </summary>
internal static class BenchCommand
{
    public static int Run(CliArgs args)
    {
        LevelDefinition level = LevelJson.Load(args.Required(0, "level.json"));
        int runs = args.Int("runs", 200);
        Console.WriteLine(SimCommand.Describe(level));

        var clock = Stopwatch.StartNew();
        long moves = 0;
        for (int i = 0; i < runs; i++)
        {
            moves += PlayRandom(level, (ulong)i);
        }

        clock.Stop();
        Console.WriteLine($"engine (random moves): {moves / clock.Elapsed.TotalSeconds:0} moves/s, {clock.Elapsed.TotalMilliseconds / runs:0.00} ms/game (single thread)");

        clock.Restart();
        var policy = SimCommand.CreatePolicy("average");
        long botMoves = 0;
        int botRuns = Math.Max(1, runs / 4);
        for (int i = 0; i < botRuns; i++)
        {
            botMoves += SimulationRunner.Play(level, policy, (ulong)i, 150).MovesUsed;
        }

        clock.Stop();
        Console.WriteLine($"greedy-average bot  : {botMoves / clock.Elapsed.TotalSeconds:0} moves/s, {clock.Elapsed.TotalMilliseconds / botRuns:0.0} ms/game (single thread)");
        return 0;
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
}
