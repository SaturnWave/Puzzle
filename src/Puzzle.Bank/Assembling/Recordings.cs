using Puzzle.Core.Engine;
using Puzzle.Core.Recording;
using Puzzle.Tooling;

namespace Puzzle.Bank.Assembling;

/// <summary>
/// The levels of a folder that people have recorded attempts on, each with the pool seeds those attempts were played
/// on. A recording names the exact file it was played on (<see cref="LevelJson.Fingerprint"/>), and only attempts on
/// the very file the folder holds count here: a recording of an older version of a level number, or of the level
/// another folder has under that number, says nothing about the board that stands in this one. Protecting by the
/// number alone would protect whatever board happens to carry it.
/// </summary>
public sealed class RecordedLevels
{
    private readonly IReadOnlyDictionary<int, IReadOnlyList<uint>> _seedsByLevel;

    /// <param name="seedsByLevel">Per recorded level, the pool seeds its attempts were played on, most played first.</param>
    /// <param name="onOtherVersions">Level numbers with recordings none of which is of the folder's file.</param>
    public RecordedLevels(IReadOnlyDictionary<int, IReadOnlyList<uint>> seedsByLevel, IReadOnlyList<int> onOtherVersions)
    {
        _seedsByLevel = seedsByLevel;
        OnOtherVersions = onOtherVersions;
    }

    public static RecordedLevels None { get; } = new(new Dictionary<int, IReadOnlyList<uint>>(), []);

    /// <summary>The recorded levels, ascending.</summary>
    public IReadOnlyList<int> Numbers => _seedsByLevel.Keys.Order().ToArray();

    /// <summary>
    /// Level numbers people recorded attempts on, but on another file than the folder holds: an older version, or
    /// the level of another folder. They are not protected here; the files they were played on must be kept where
    /// the comparison of people and bots finds them (levels/archive), which taking a set live sees to.
    /// </summary>
    public IReadOnlyList<int> OnOtherVersions { get; }

    public bool Contains(int level) => _seedsByLevel.ContainsKey(level);

    /// <summary>The pool seeds people played the level on, most played first; empty for a level nobody recorded.</summary>
    public IReadOnlyList<uint> SeedsOf(int level) => _seedsByLevel.GetValueOrDefault(level, []);

    /// <summary>Sorts <paramref name="records"/> by whether they were played on the file <paramref name="folder"/> has under their level number.</summary>
    /// <exception cref="Puzzle.Core.Levels.LevelFormatException">A level file a record names is not a valid level.</exception>
    public static RecordedLevels Of(IEnumerable<AttemptRecord> records, LevelFolder folder)
    {
        var played = new Dictionary<int, Dictionary<uint, int>>();
        var elsewhere = new HashSet<int>();
        var fingerprints = new Dictionary<int, string?>();
        foreach (AttemptRecord record in records.Where(record => record.Lab is not null))
        {
            int number = record.Start.Level.Value;
            if (!fingerprints.TryGetValue(number, out string? fingerprint))
            {
                fingerprints[number] = fingerprint = folder.Find(number) is FolderLevel held ? LevelJson.Fingerprint(held.Level) : null;
            }

            if (fingerprint != record.Lab!.LevelFingerprint)
            {
                elsewhere.Add(number);
                continue;
            }

            if (!played.TryGetValue(number, out Dictionary<uint, int>? attemptsBySeed))
            {
                played[number] = attemptsBySeed = [];
            }

            // An attempt on a seed outside the pool (a seed chosen by hand in the lab) still shows the level was played; it names no pool seed to keep.
            foreach (uint seed in folder.Find(number)!.Level.Attempts.SeedPool.Where(seed => AttemptSeeds.From(seed) == record.Start.Seeds))
            {
                attemptsBySeed[seed] = attemptsBySeed.GetValueOrDefault(seed) + 1;
            }
        }

        return new RecordedLevels(
            played.ToDictionary(
                level => level.Key,
                level => (IReadOnlyList<uint>)level.Value.OrderByDescending(seed => seed.Value).ThenBy(seed => seed.Key).Select(seed => seed.Key).ToArray()),
            elsewhere.Where(number => !played.ContainsKey(number)).Order().ToArray());
    }

    /// <summary>The fingerprints of every level file <paramref name="records"/> were played on.</summary>
    public static IReadOnlySet<string> FingerprintsIn(IEnumerable<AttemptRecord> records) =>
        records.Select(record => record.Lab?.LevelFingerprint).OfType<string>().ToHashSet();
}
