using System.Diagnostics;
using Puzzle.Bot.Simulation;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>solve &lt;level.json&gt; [--seeds 8] [--beam 8] [--cap 150]</c>: for each seed, the perfect-information
/// search ("always the right move", knows every refill) against the human-like and the random bot on the very
/// same starting board. The gap is the level's margin for error.
/// </summary>
internal static class SolveCommand
{
    public static int Run(CliArgs args)
    {
        LevelDefinition level = LevelJson.Load(args.Required(0, "level.json"));
        int seeds = args.Int("seeds", 8);
        int beam = args.Int("beam", 8);
        int cap = args.Int("cap", 150);
        Console.WriteLine(SimCommand.Describe(level));
        Console.WriteLine($"beam={beam}  cap={cap}");
        Console.WriteLine("seed | perfect play | average bot | random bot | positions searched");

        var solver = new BeamSearchSolver(beam);
        var clock = Stopwatch.StartNew();
        var perfect = new List<int>();
        var average = new List<int>();
        for (ulong seed = 0; seed < (ulong)seeds; seed++)
        {
            Game game = SimulationRunner.StartGame(level, seed, cap);
            SolveResult best = solver.Solve(game, cap);
            int? human = SimulationRunner.Play(level, new GreedyPolicy(SkillProfile.Average), seed, cap).CompletedAtMove;
            int? random = SimulationRunner.Play(level, new RandomPolicy(), seed, cap).CompletedAtMove;
            Console.WriteLine($"{seed,4} | {SimCommand.Moves(best.MovesNeeded),12} | {SimCommand.Moves(human),11} | {SimCommand.Moves(random),10} | {best.PositionsExplored,8}");
            if (best.MovesNeeded is int p && human is int h)
            {
                perfect.Add(p);
                average.Add(h);
            }
        }

        clock.Stop();
        if (perfect.Count > 0)
        {
            Console.WriteLine($"mean perfect {perfect.Average():0.0}  mean average-bot {average.Average():0.0}  "
                + $"=> a human-like player needs {average.Average() / perfect.Average():0.00}x the optimal moves");
            Console.WriteLine($"margin at level limit {level.MoveLimit}: {level.MoveLimit - perfect.Average():0.0} spare moves for a perfect player");
        }

        Console.WriteLine($"time {clock.Elapsed.TotalSeconds:0.0} s");
        return 0;
    }
}
