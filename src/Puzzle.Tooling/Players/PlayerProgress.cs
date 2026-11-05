using Puzzle.Core.Levels;

namespace Puzzle.Tooling.Players;

/// <summary>
/// How far a player has come in the public level set, which is played in order of the level ids. Their place is kept by
/// level id: they stand on the level after <see cref="LastWonLevel"/>, wherever that sits among the files.
/// <see cref="AttemptsStarted"/> counts their attempts on one version of that level, the one
/// <see cref="AttemptedLevel"/> fingerprints: on any other version (the next level, or the same level regenerated) the
/// count starts again, as the level's seed pool does. An attempt counts once a move is played in it; one only opened and
/// looked at uses up no retry number. <see cref="Live"/> is the attempt the player is in.
/// </summary>
public sealed record PlayerProgress
{
    /// <param name="attemptedLevel">Fingerprint of the level version the attempts were started on; absent before the first attempt on a level.</param>
    /// <param name="lastWonLevel">The id of the level won last; absent before the first win.</param>
    /// <param name="live">The attempt the player is in; absent when they are in none.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public PlayerProgress(int levelsWon, string? attemptedLevel, int attemptsStarted, LevelId? lastWonLevel = null, SavedAttempt? live = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(levelsWon);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptsStarted);
        LevelsWon = levelsWon;
        AttemptedLevel = attemptedLevel;
        AttemptsStarted = attemptsStarted;
        LastWonLevel = lastWonLevel;
        Live = live;
    }

    /// <summary>Where every player starts: the first level, never tried.</summary>
    public static PlayerProgress None { get; } = new(levelsWon: 0, attemptedLevel: null, attemptsStarted: 0);

    /// <summary>How many levels the player has won.</summary>
    public int LevelsWon { get; }

    /// <summary>
    /// The id of the level the player won last; absent before the first win. A store written before places were kept
    /// by id names none even after wins: there <see cref="LevelsWon"/> levels of the set, in its order, are behind the
    /// player.
    /// </summary>
    public LevelId? LastWonLevel { get; }

    /// <summary>Fingerprint of the level version <see cref="AttemptsStarted"/> counts on; absent before the first attempt on a level.</summary>
    public string? AttemptedLevel { get; }

    /// <summary>The attempts on <see cref="AttemptedLevel"/> in which a move was played.</summary>
    public int AttemptsStarted { get; }

    /// <summary>The attempt the player is in, being played or just decided; absent when they are in none.</summary>
    public SavedAttempt? Live { get; }

    /// <summary>The attempts played on the level version <paramref name="levelFingerprint"/>; also the 0-based number of the next one.</summary>
    public int AttemptsOn(string levelFingerprint) => AttemptedLevel == levelFingerprint ? AttemptsStarted : 0;

    /// <summary>The first move of an attempt on <paramref name="levelFingerprint"/> was played: the attempt now counts.</summary>
    public PlayerProgress WithAttemptStartedOn(string levelFingerprint) =>
        new(LevelsWon, levelFingerprint, AttemptsOn(levelFingerprint) + 1, LastWonLevel, Live);

    /// <summary>The level <paramref name="level"/> is won: the player stands on the one after it, never tried.</summary>
    public PlayerProgress WithLevelWon(LevelId level) => new(LevelsWon + 1, attemptedLevel: null, attemptsStarted: 0, level, Live);

    /// <summary>The same progress with <paramref name="live"/> as the attempt the player is in, or with none.</summary>
    public PlayerProgress WithLive(SavedAttempt? live) => new(LevelsWon, AttemptedLevel, AttemptsStarted, LastWonLevel, live);
}
