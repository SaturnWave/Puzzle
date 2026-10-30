using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling;

namespace Puzzle.Web.Recording;

/// <summary>
/// Numbers each device's retries of each level version in sequence (docs/GAME_DESIGN.md §17), on the server, so a page
/// reload or a second tab never draws a retry the device has already been given. The numbering continues after the
/// highest retry the recordings hold. A retry left without a single move is not recorded, so after a restart its
/// number, and the board the player only looked at, may come again. Retries past a level's seed pool replay its games;
/// the session says so (<see cref="Sessions.SequenceBreak.PoolReplay"/>).
/// </summary>
/// <remarks>
/// One small entry per device and level version, at most <see cref="DefaultCapacity"/> of them: past that, the entry
/// used least recently is forgotten, and should its device come back to that level, the numbering starts again at 0.
/// The bound is far above the lab's real use (a handful of testers), so only a flood of made-up device ids reaches it.
/// </remarks>
public sealed class RetryNumbers
{
    /// <summary>Device and level versions numbered at once by default: some hundred kilobytes.</summary>
    public const int DefaultCapacity = 10_000;

    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<RetryKey, LinkedListNode<NextRetry>> _entries = new();

    /// <summary>The entries, the one used most recently first.</summary>
    private readonly LinkedList<NextRetry> _byUse = new();

    /// <param name="recorded">The lab's records so far; records of the released game, or with hand-chosen seeds, hold no lab retry.</param>
    /// <param name="capacity">Device and level versions numbered at once; at least 1.</param>
    public RetryNumbers(IEnumerable<AttemptRecord> recorded, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        foreach (AttemptRecord record in recorded)
        {
            if (record is { Start.Attempt: int attempt, Lab: { } lab })
            {
                NextRetry entry = Touch(new RetryKey(record.Start.Device, lab.LevelFingerprint));
                entry.Attempt = Math.Max(entry.Attempt, attempt + 1);
            }
        }
    }

    /// <summary>The next retry number of <paramref name="device"/> on <paramref name="level"/> as it is now; taking it uses it up.</summary>
    public int Take(DeviceId device, LevelDefinition level)
    {
        var key = new RetryKey(device, LevelJson.Fingerprint(level));
        lock (_gate)
        {
            NextRetry entry = Touch(key);
            return entry.Attempt++;
        }
    }

    /// <summary>
    /// The entry of <paramref name="key"/>, now the one used most recently; a new entry starts at retry 0, and past the
    /// capacity the entry used least recently is forgotten.
    /// </summary>
    private NextRetry Touch(RetryKey key)
    {
        if (_entries.TryGetValue(key, out LinkedListNode<NextRetry>? node))
        {
            _byUse.Remove(node);
            _byUse.AddFirst(node);
            return node.Value;
        }

        var entry = new NextRetry(key);
        _entries[key] = _byUse.AddFirst(entry);
        if (_entries.Count > _capacity && _byUse.Last is { } leastUsed)
        {
            _byUse.Remove(leastUsed);
            _entries.Remove(leastUsed.Value.Key);
        }

        return entry;
    }

    private readonly record struct RetryKey(DeviceId Device, string LevelFingerprint);

    /// <summary>The retry number a device and level version get next.</summary>
    private sealed class NextRetry
    {
        public NextRetry(RetryKey key) => Key = key;

        public RetryKey Key { get; }

        public int Attempt { get; set; }
    }
}
