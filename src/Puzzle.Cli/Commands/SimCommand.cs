using System.Diagnostics;
using System.Globalization;
using Puzzle.Bot.Simulation;
using Puzzle.Bot;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>sim &lt;level.json&gt; [--bot average|all] [--runs 300] [--cap 150] [--seed 1]</c>:
/// plays the level many times and reports how many moves it really needs.
/// </summary>
internal static class SimCommand
{
    private static readonly double[] SuggestedWinRates = { 0.95, 0.85, 0.75, 0.60, 0.45, 0.30 };
    private static readonly double[] ReportedShares = { 0.10, 0.25, 0.50, 0.75, 0.90 };

    public static int Run(CliArgs args)
    {
        string path = args.Required(0, "level.json");
        int runs = args.Int("runs", 300);
        int cap = args.Int("cap", 150);
        ulong seed = args.ULong("seed", 1);
        string bot = args.Text("bot", "average");

        LevelDefinition level = LevelJson.Load(path);
        Console.WriteLine(Describe(level));
        IEnumerable<string> policies = bot.Equals("all", StringComparison.OrdinalIgnoreCase) ? BotPolicies.Names : new[] { bot };
        foreach (string policyName in policies)
        {
            Console.WriteLine();
            PrintReport(level, policyName, runs, cap, seed);
        }

        return 0;
    }

    /// <exception cref="UsageException">Unknown policy name.</exception>
    public static IBotPolicy CreatePolicy(string name) => BotPolicies.TryCreate(name, out IBotPolicy policy)
        ? policy
        : throw new UsageException(
            $"Unknown bot '{name}'. Use {string.Join(", ", BotPolicies.Names)} or all; a rung of the skill ladder: {SkillLadder.NamePattern}; "
            + $"measurement bots: {string.Join(", ", BotPolicies.MeasurementNames)}, "
            + $"usta-<n> (n simulations per move, {UstaSettings.MinSimulations}..{UstaSettings.MaxSimulations}).");

    public static string Describe(LevelDefinition level)
    {
        BoardShape shape = level.Layout.Shape;
        string goals = string.Join(", ", level.Goals);
        return $"Level {level.Id} ({level.Difficulty}, {shape.Width}x{shape.Height}, {shape.PlayableCount} cells, {level.Spawn.Colors} colors, limit {level.MoveLimit})\n  goals: {goals}";
    }

    public static string Percent(double value) => (value * 100).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(5) + "%";

    public static string Moves(int? moves) => moves?.ToString(CultureInfo.InvariantCulture) ?? ">cap";

    private static void PrintReport(LevelDefinition level, string policyName, int runs, int cap, ulong seed)
    {
        IBotPolicy probe = CreatePolicy(policyName);
        var clock = Stopwatch.StartNew();
        LevelReport report = BatchSimulator.Run(level, () => CreatePolicy(policyName), runs, cap, seed);
        clock.Stop();

        Console.WriteLine($"bot={probe.Name}  runs={runs}  cap={cap}  seed={seed}");
        Console.WriteLine($"  completed within cap : {Percent(report.CompletionRate)}   stuck: {Percent(report.StuckRate)}   shuffles/run: {report.AverageShuffles:0.00}");
        Console.WriteLine($"  moves needed         : {string.Join(" | ", ReportedShares.Select(q => $"p{q * 100:0} {Moves(report.MovesNeeded(q))}"))}");
        Console.WriteLine($"  at level limit ({level.MoveLimit,2})  : win {Percent(report.WinRateAt(level.MoveLimit))}   near-miss {Percent(report.NearMissRateAt(level.MoveLimit))}");
        Console.WriteLine($"  suggested limits     : {string.Join("  ", SuggestedWinRates.Select(w => $"{w * 100:0}%->{Moves(report.MoveLimitFor(w))}"))}");
        Console.WriteLine($"  specials/run {report.AverageSpecialsCreated:0.0}   cascades/move {report.AverageCascadesPerMove:0.00}");
        Console.WriteLine($"  time {clock.Elapsed.TotalSeconds:0.00} s  ({clock.Elapsed.TotalMilliseconds / runs:0.0} ms/run on {Environment.ProcessorCount} threads)");
    }
}
