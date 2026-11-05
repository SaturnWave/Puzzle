using System.Globalization;
using Puzzle.Bank.Measuring;
using Puzzle.Bot;
using Puzzle.Core.Levels;
using Puzzle.Economy.Units;
using Puzzle.Tooling;

namespace Puzzle.Economy.Measuring;

/// <summary>What the guard found right: how many lines and how many replayed games.</summary>
public sealed record GuardReport(int Lines, int GamesReplayed);

/// <summary>
/// The check before a run reads a measurement: the engine that played it is this build's and adds the moves the file
/// says, every line was played on the level file and by the rung that exist today, and a sample of its stored games
/// plays again to the same end. The engine fingerprint plays no start booster, so only the replay sees a change of
/// how a booster starts a board; and nothing plays the booster's or the offer's added moves, so those two numbers
/// are compared outright (<see cref="MeasurementSpec.RequireTheAddedMovesOfThisBuild"/>).
/// </summary>
public static class MeasurementGuard
{
    /// <summary>Stored games played again; spread over the lines and every board start among them.</summary>
    public const int GamesReplayed = 48;

    private const int LevelsNamed = 12;

    /// <param name="lines">The lines the run will read.</param>
    /// <param name="levelFiles">The level files of today, by level number.</param>
    /// <exception cref="MeasurementMismatchException">The measurement is of another engine, of other levels or rungs, or its games no longer replay; the message says what to measure again.</exception>
    public static GuardReport Check(MeasurementSpec spec, IReadOnlyList<MeasuredLine> lines, IReadOnlyDictionary<int, LevelDefinition> levelFiles)
    {
        RequireThisEngine(spec);
        spec.RequireTheAddedMovesOfThisBuild();
        RequireTheLevelsAndRungsOfToday(lines, levelFiles);
        RequireSameGames(spec, lines, levelFiles);
        return new GuardReport(lines.Count, lines.Count == 0 ? 0 : GamesReplayed);
    }

    private static void RequireThisEngine(MeasurementSpec spec)
    {
        string today = MeasurementFingerprint.OfThisBuild;
        if (spec.Engine != today)
        {
            throw new MeasurementMismatchException(
                $"The measurement was played by another engine or bot than this build has (fingerprint {MeasurementSpec.Abbreviated(spec.Engine)}, this build {MeasurementSpec.Abbreviated(today)}). "
                + "Every level has to be measured again: puzzle economy measure --restart.");
        }
    }

    private static void RequireTheLevelsAndRungsOfToday(IReadOnlyList<MeasuredLine> lines, IReadOnlyDictionary<int, LevelDefinition> levelFiles)
    {
        Dictionary<int, string> today = levelFiles.ToDictionary(level => level.Key, level => LevelJson.Fingerprint(level.Value));
        int[] changed = lines
            .Where(line => !line.Player.IsOnLadder || !today.TryGetValue(line.Level.Value, out string? fingerprint) || fingerprint != line.Fingerprint)
            .Select(line => line.Level.Value)
            .Distinct()
            .Order()
            .ToArray();
        if (changed.Length > 0)
        {
            throw new MeasurementMismatchException(
                $"{changed.Length} levels were measured on another content than their level file has today, by a rung whose settings changed since, or have no level file: "
                + $"{Listed(changed)}. Measure them again (puzzle economy measure keeps every line that still stands).");
        }
    }

    private static void RequireSameGames(MeasurementSpec spec, IReadOnlyList<MeasuredLine> lines, IReadOnlyDictionary<int, LevelDefinition> levelFiles)
    {
        var different = new List<string>();
        foreach (StoredGameRef game in Sample(lines))
        {
            MeasuredLine line = game.Line;
            var bot = new GreedyPolicy(line.Player.ToProfile());
            MeasuredGame replayed = EconomyGame.Play(
                levelFiles[line.Level.Value],
                bot,
                line.Start,
                line.Pool[game.Position],
                spec.FirstRepeat + game.Game,
                spec.MoveCap,
                spec.ProgressMarks);
            if (!replayed.IsSameGameAs(line.Seeds[game.Position][game.Game]))
            {
                different.Add($"{line.Key} seed {game.Position} game {game.Game}");
            }
        }

        if (different.Count > 0)
        {
            throw new MeasurementMismatchException(
                $"{different.Count} of {GamesReplayed} stored games no longer replay to the same end (first: {different[0]}). The engine fingerprint matches, "
                + "so its probe levels do not see what changed. Every level has to be measured again: puzzle economy measure --restart.");
        }
    }

    /// <summary>
    /// The games to replay, by a fixed rule: the board starts in turn, within a start the lines at even distances, and
    /// in a line a seed and a game that move on with every sample.
    /// </summary>
    private static IEnumerable<StoredGameRef> Sample(IReadOnlyList<MeasuredLine> lines)
    {
        MeasuredLine[][] byStart = lines.GroupBy(line => line.Start).OrderBy(group => group.Key).Select(group => group.OrderBy(line => line.Key).ToArray()).ToArray();
        for (int sample = 0; sample < GamesReplayed && byStart.Length > 0; sample++)
        {
            MeasuredLine[] ofStart = byStart[sample % byStart.Length];
            MeasuredLine line = ofStart[(int)((long)sample * ofStart.Length / GamesReplayed)];
            int position = sample % line.Pool.Count;
            yield return new StoredGameRef(line, position, sample % line.Seeds[position].Count);
        }
    }

    private static string Listed(int[] levels) => levels.Length <= LevelsNamed
        ? string.Join(", ", levels.Select(level => level.ToString(CultureInfo.InvariantCulture)))
        : string.Join(", ", levels.Take(LevelsNamed).Select(level => level.ToString(CultureInfo.InvariantCulture))) + $" and {levels.Length - LevelsNamed} more";

    private readonly record struct StoredGameRef(MeasuredLine Line, int Position, int Game);
}
