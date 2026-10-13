using Puzzle.Core.Levels;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Measuring;

/// <summary>A measurement as a run reads it: the stored games of every level, rung and board start it has, found by what they are for.</summary>
public sealed class Measurement
{
    private readonly Dictionary<LineKey, MeasuredLine> _byKey = [];

    /// <exception cref="ArgumentException">Two lines are for the same level, rung and board start.</exception>
    public Measurement(MeasurementSpec spec, IEnumerable<MeasuredLine> lines)
    {
        Spec = spec;
        foreach (MeasuredLine line in lines)
        {
            if (!_byKey.TryAdd(line.Key, line))
            {
                throw new ArgumentException($"Two lines are for {line.Key}.", nameof(lines));
            }
        }

        Lines = [.. _byKey.Values.OrderBy(line => line.Key)];
    }

    public MeasurementSpec Spec { get; }

    /// <summary>Every line, sorted by level, skill and board start.</summary>
    public IReadOnlyList<MeasuredLine> Lines { get; }

    public long GameCount => Lines.Sum(line => (long)line.GameCount);

    /// <exception cref="MeasurementFormatException">No file is there, it is not a measurement, or it is damaged.</exception>
    public static Measurement Load(string path)
    {
        StoredMeasurement stored = MeasurementFile.Read(path)
            ?? throw new MeasurementFormatException($"No measurement at '{path}'; make it with: puzzle economy measure.");
        try
        {
            return new Measurement(stored.Spec, stored.Lines);
        }
        catch (ArgumentException ex)
        {
            throw new MeasurementFormatException($"'{path}' is damaged: {ex.Message}", ex);
        }
    }

    /// <summary>The games of a level, rung and board start; absent when they were not measured.</summary>
    public MeasuredLine? Find(LevelId level, SkillRung rung, BoardStart start) =>
        _byKey.TryGetValue(LineKey.Of(level, rung, start), out MeasuredLine? line) ? line : null;

    /// <summary>
    /// The last level a run with these rungs and board starts can reach: every level from 1 to it has the games of
    /// every one of them. 0 when level 1 is missing something.
    /// </summary>
    public int LastLevelWithoutGap(IReadOnlyCollection<SkillRung> rungs, IReadOnlyCollection<BoardStart> starts)
    {
        int level = 0;
        while (rungs.Count > 0 && starts.Count > 0 && rungs.All(rung => starts.All(start => Find(new LevelId(level + 1), rung, start) is not null)))
        {
            level++;
        }

        return level;
    }
}
