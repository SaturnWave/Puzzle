using System.Diagnostics;
using System.Globalization;
using Puzzle.Core.Levels;
using Puzzle.Tooling;
using Puzzle.Tooling.Hints;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>hints &lt;dir|file&gt; [--levels 1-250,260-1000/10] [--player skill-55] [--hint-bots novice,casual,average]
/// [--moment idle|stuck] [--idle-percent 50] [--per-attempt 3] [--attempts 8] [--seed 7]</c>: what the game's hints do to
/// a player model's win rate at the levels' move limits (<see cref="HintEffect"/>). Every attempt is played without hints
/// and once for each hint bot, on the level's own seeds; the hinted player follows each hint it is shown, up to the
/// cap. Prints, for all levels and for the first room, rooms 2 to 5 and the levels after them, each hint bot's win rate
/// change with its standard error, the hints followed per attempt and the share of attempts that were shown any. No
/// level is changed and nothing is written.
/// </summary>
internal static class HintsCommand
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The groups the summary reads: the first room is calibrated for a weaker player than the rooms after it (docs/GAME_DESIGN.md §8).</summary>
    private static readonly (string Name, int From, int To)[] Groups =
    [
        ("levels 1-50", 1, 50),
        ("levels 51-250", 51, 250),
        ("levels 251+", 251, int.MaxValue),
    ];

    public static int Run(CliArgs args)
    {
        string target = args.Required(0, "dir|file");
        HintEffectSettings settings = SettingsFrom(args);
        LevelDefinition[] levels = LevelsFrom(target, args.Text("levels", "1-250,260-1000/10"));

        var clock = Stopwatch.StartNew();
        IReadOnlyList<HintedAttempt> attempts = HintEffect.Measure(levels, settings);
        Console.Error.WriteLine(string.Create(Invariant, $"{attempts.Count} attempts x {settings.HintBots.Count + 1} games in {clock.Elapsed.TotalSeconds:0} s"));

        Console.WriteLine(string.Create(
            Invariant,
            $"player {settings.Player}; hints when {settings.Moment}, at most {settings.HintsPerAttempt} an attempt, each followed; {settings.AttemptsPerLevel} attempts a level on its own seeds; seed {settings.Seed}"));
        Print("all levels", attempts, settings);
        foreach ((string name, int from, int to) in Groups)
        {
            HintedAttempt[] group = attempts.Where(attempt => attempt.Level.Id.Value >= from && attempt.Level.Id.Value <= to).ToArray();
            if (group.Length > 0)
            {
                Print(name, group, settings);
            }
        }

        return 0;
    }

    private static void Print(string groupName, IReadOnlyList<HintedAttempt> attempts, HintEffectSettings settings)
    {
        IReadOnlyList<HintEffectSummary> effects = HintEffect.Summarize(attempts, settings.HintBots);
        Console.WriteLine(string.Create(Invariant, $"{groupName}: {attempts.Count} attempts on {attempts.Select(attempt => attempt.Level.Id).Distinct().Count()} levels, won without hints {SimCommand.Percent(effects[0].PlainWinRate)}"));
        Console.WriteLine($"  {"hint bot",-9} {"won",7} {"change",8} {"+-2 SE",8} {"hints",6} {"hinted",7}");
        foreach (HintEffectSummary effect in effects)
        {
            Console.WriteLine(string.Create(
                Invariant,
                $"  {effect.HintBot,-9} {SimCommand.Percent(effect.HintedWinRate),7} {effect.Change * 100,7:+0.0;-0.0;0.0} {2 * effect.StandardError * 100,7:0.0} {effect.MeanHints,6:0.00} {SimCommand.Percent(effect.ShareWithAHint),7}"));
        }
    }

    /// <exception cref="UsageException">An option names no player model, no moment, or a number out of range.</exception>
    private static HintEffectSettings SettingsFrom(CliArgs args)
    {
        string[] hintBots = args.Text("hint-bots", "novice,casual,average").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int perAttempt = args.Int("per-attempt", 3);
        int attempts = args.Int("attempts", 8);
        if (hintBots.Length == 0 || perAttempt < 1 || attempts < 1)
        {
            throw new UsageException("--hint-bots must name at least one bot, and --per-attempt and --attempts must be at least 1.");
        }

        return new HintEffectSettings
        {
            Player = args.Text("player", "skill-55"),
            HintBots = hintBots,
            Moment = MomentFrom(args),
            HintsPerAttempt = perAttempt,
            AttemptsPerLevel = attempts,
            Seed = args.ULong("seed", 7),
        };
    }

    /// <exception cref="UsageException">The option names no moment, or the chance is not 1 to 100.</exception>
    private static HintMoment MomentFrom(CliArgs args)
    {
        string moment = args.Text("moment", "idle");
        int chance = args.Int("idle-percent", 50);
        return moment switch
        {
            "idle" when chance is >= 1 and <= 100 => new HintMoment.WhenIdle(chance),
            "idle" => throw new UsageException("--idle-percent must be 1 to 100."),
            "stuck" => new HintMoment.WhenStuck(),
            _ => throw new UsageException($"--moment is 'idle' (a chance before every move, --idle-percent) or 'stuck' (the player's own move counts nothing for the goals), not '{moment}'."),
        };
    }

    /// <summary>The levels a list of ranges names, "1-250,260-1000/10" (every tenth from 260), in level order.</summary>
    /// <exception cref="UsageException">The list is malformed, or names no level of the folder.</exception>
    private static LevelDefinition[] LevelsFrom(string target, string ranges)
    {
        HashSet<int> wanted = [.. ranges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).SelectMany(NumbersOf)];
        LevelDefinition[] levels = LevelFiles.Under(target)
            .Select(LevelJson.Load)
            .Where(level => wanted.Contains(level.Id.Value))
            .OrderBy(level => level.Id.Value)
            .ToArray();
        return levels.Length > 0 ? levels : throw new UsageException($"No level of '{target}' is in --levels {ranges}.");
    }

    private static IEnumerable<int> NumbersOf(string range)
    {
        (int from, int to, int step) = RangeOf(range);
        for (int level = from; level <= to; level += step)
        {
            yield return level;
        }
    }

    /// <exception cref="UsageException">The range is not "from-to" or "from-to/step" with 1 &lt;= from &lt;= to and a positive step.</exception>
    private static (int From, int To, int Step) RangeOf(string range)
    {
        string[] stepped = range.Split('/');
        string[] ends = stepped[0].Split('-');
        int step = 1;
        if (stepped.Length <= 2 && ends.Length == 2
            && int.TryParse(ends[0], NumberStyles.None, Invariant, out int from)
            && int.TryParse(ends[1], NumberStyles.None, Invariant, out int to)
            && from >= 1 && to >= from
            && (stepped.Length == 1 || (int.TryParse(stepped[1], NumberStyles.None, Invariant, out step) && step >= 1)))
        {
            return (from, to, step);
        }

        throw new UsageException($"--levels takes ranges like 1-250 or 260-1000/10, not '{range}'.");
    }
}
