using System.Diagnostics;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>solve &lt;level.json&gt; [--seeds 8] [--beam 8] [--cap 150] [--prove 4]</c>: for each seed, the
/// perfect-information search ("always the right move", knows every refill) against the human-like and the
/// random bot on the very same starting board. The gap is the level's margin for error. With <c>--prove D</c>,
/// an exhaustive search checks whether any win shorter than the beam's exists (only when that needs ≤ D moves).
/// </summary>
internal static class SolveCommand
{
    public static int Run(CliArgs args)
    {
        LevelDefinition level = LevelJson.Load(args.Required(0, "level.json"));
        int seeds = args.Int("seeds", 8);
        int beam = args.Int("beam", 8);
        int cap = args.Int("cap", 150);
        int prove = args.Int("prove", 0);
        Console.WriteLine(SimCommand.Describe(level));
        Console.WriteLine($"beam={beam}  cap={cap}{(prove > 0 ? $"  prove<={prove}" : string.Empty)}");
        Console.WriteLine("seed | best found | average bot | random bot | positions searched | branching" + (prove > 0 ? " | exhaustive proof" : string.Empty));

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
            string proof = prove > 0 ? " | " + Prove(game, best, prove) : string.Empty;
            Console.WriteLine($"{seed,4} | {SimCommand.Moves(best.MovesNeeded),10} | {SimCommand.Moves(human),11} | {SimCommand.Moves(random),10} | {best.PositionsExplored,18} | {best.BranchingFactor,9:0.0}{proof}");
            if (best.MovesNeeded is int p && human is int h)
            {
                perfect.Add(p);
                average.Add(h);
            }
        }

        clock.Stop();
        if (perfect.Count > 0)
        {
            Console.WriteLine($"mean best-found {perfect.Average():0.0}  mean average-bot {average.Average():0.0}  "
                + $"=> a human-like player needs {average.Average() / perfect.Average():0.00}x the moves of the best line found");
            Console.WriteLine($"margin at level limit {level.MoveLimit}: {level.MoveLimit - perfect.Average():0.0} spare moves for a perfect player");
        }

        Console.WriteLine($"time {clock.Elapsed.TotalSeconds:0.0} s");
        return 0;
    }

    /// <summary>Tries every sequence one move shorter than the beam's line; none winning proves the line optimal.</summary>
    private static string Prove(Game game, SolveResult best, int maxDepth)
    {
        if (best.MovesNeeded is not int found)
        {
            return "-";
        }

        if (found - 1 > maxDepth)
        {
            return $"skipped (needs depth {found - 1})";
        }

        var clock = Stopwatch.StartNew();
        ExhaustiveResult shorter = ExhaustiveSearch.FindWinWithin(game, found - 1);
        string verdict = shorter.WinFound ? "SHORTER WIN EXISTS" : "optimal (proven)";
        return $"{verdict}, {shorter.PositionsExamined:N0} positions, {clock.Elapsed.TotalSeconds:0.0} s";
    }
}
