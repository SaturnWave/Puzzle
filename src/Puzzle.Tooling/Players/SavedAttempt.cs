using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Tooling.Players;

/// <summary>
/// A player's live attempt as the store keeps it, so it outlasts a pause of any length and a restart of the server: the
/// level, the seeds and every move played, in order. The engine is deterministic, so starting that level with those
/// seeds and playing those moves again gives the board the player left.
/// </summary>
public sealed record SavedAttempt
{
    /// <param name="id">Names the attempt to the player's page; a move says which attempt it was made on.</param>
    /// <param name="level">The id of the level the attempt is on.</param>
    /// <param name="levelFingerprint">The version of that level's content the attempt was started on.</param>
    /// <param name="attempt">The player's 0-based retry number on that level version.</param>
    /// <param name="startedUtc">When the attempt was opened; stored in UTC.</param>
    /// <param name="moves">Every accepted move so far, in order.</param>
    /// <exception cref="ArgumentException">A part is missing or out of range.</exception>
    public SavedAttempt(Guid id, LevelId level, string levelFingerprint, int attempt, AttemptSeeds seeds, DateTimeOffset startedUtc, IReadOnlyList<Move> moves)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        ArgumentNullException.ThrowIfNull(moves);
        Id = id;
        Level = level;
        LevelFingerprint = string.IsNullOrWhiteSpace(levelFingerprint)
            ? throw new ArgumentException("A level fingerprint is required.", nameof(levelFingerprint))
            : levelFingerprint;
        Attempt = attempt;
        Seeds = seeds;
        StartedUtc = startedUtc.ToUniversalTime();
        Moves = Array.AsReadOnly(moves.ToArray());
    }

    public Guid Id { get; }

    public LevelId Level { get; }

    public string LevelFingerprint { get; }

    /// <summary>The player's 0-based retry number on the level version; it is used up only once a move is played.</summary>
    public int Attempt { get; }

    public AttemptSeeds Seeds { get; }

    public DateTimeOffset StartedUtc { get; }

    /// <summary>Every accepted move so far, in order.</summary>
    public IReadOnlyList<Move> Moves { get; }

    /// <summary>Two saved attempts are equal when every part is, the moves one by one.</summary>
    public bool Equals(SavedAttempt? other) =>
        other is not null
        && Id == other.Id
        && Level == other.Level
        && LevelFingerprint == other.LevelFingerprint
        && Attempt == other.Attempt
        && Seeds == other.Seeds
        && StartedUtc == other.StartedUtc
        && Moves.SequenceEqual(other.Moves);

    public override int GetHashCode() => HashCode.Combine(Id, Level, Attempt, Seeds, Moves.Count);
}
