using System.Diagnostics.CodeAnalysis;
using Puzzle.Core.Levels;
using Puzzle.Tooling;
using Puzzle.Tooling.Players;

namespace Puzzle.Web.Players;

/// <summary>One level of the public set: the file, the version of its content, and its 1-based place in the set.</summary>
public sealed record PublicLevel(LibraryLevel Entry, string Fingerprint, int Number)
{
    public LevelId Id => Entry.Level.Id;
}

/// <summary>
/// The levels the public mode's players work through: every level file in one folder of the level library
/// ("generated" by default), in the order of the level ids, whatever the files are called. A player's place in the set
/// is kept by level id, so the set must be whole: it is refused when a file of the folder did not load, or two levels
/// share an id.
/// </summary>
public sealed class PublicLevelSet
{
    private readonly PublicLevel[] _levels;

    /// <param name="folder">The folder's name under the library root, e.g. "generated".</param>
    /// <exception cref="InvalidOperationException">
    /// The folder cannot be played through: it holds no level, a level file in it did not load, or two of its levels
    /// share an id. The message names the files.
    /// </exception>
    public PublicLevelSet(LevelLibrary library, string folder)
    {
        Folder = folder;
        string prefix = folder.Trim('/') + "/";
        RefuseUnloadedFiles(library, folder, prefix);
        _levels = library.Levels
            .Where(entry => entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Level.Id.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select((entry, index) => new PublicLevel(entry, LevelJson.Fingerprint(entry.Level), index + 1))
            .ToArray();
        if (_levels.Length == 0)
        {
            throw new InvalidOperationException($"No levels under '{folder}' in {library.Root}; set Puzzle:PublicLevels.");
        }

        RefuseSharedIds(_levels, folder);
    }

    public string Folder { get; }

    public int Count => _levels.Length;

    /// <summary>
    /// The level a player with <paramref name="progress"/> stands on: the one after the level they won last; false
    /// when they have won them all. A progress that names no last level (no win yet, or written before places were
    /// kept by id) stands on the level its count of wins leads to.
    /// </summary>
    public bool TryGetNext(PlayerProgress progress, [NotNullWhen(true)] out PublicLevel? level)
    {
        int place = progress.LastWonLevel is { } lastWon
            ? _levels.Count(each => each.Id.Value <= lastWon.Value)
            : progress.LevelsWon;
        level = place < _levels.Length ? _levels[place] : null;
        return level is not null;
    }

    /// <summary>The level of the set with the id <paramref name="id"/>; false when the set has none.</summary>
    public bool TryFind(LevelId id, [NotNullWhen(true)] out PublicLevel? level)
    {
        level = Array.Find(_levels, each => each.Id == id);
        return level is not null;
    }

    /// <summary>
    /// A file that did not load would silently move every player behind it one level on, and back again once it is
    /// repaired; a public server does not start like that.
    /// </summary>
    private static void RefuseUnloadedFiles(LevelLibrary library, string folder, string prefix)
    {
        LibraryProblem[] unloaded = library.Problems
            .Where(problem => Path.GetRelativePath(library.Root, problem.Path).Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (unloaded.Length > 0)
        {
            throw new InvalidOperationException(
                $"{unloaded.Length} level file(s) of the public set '{folder}' (Puzzle:PublicLevels) cannot be loaded; the first is '{unloaded[0].Path}': {unloaded[0].Message} "
                + "Repair or remove them: a missing level would move every player behind it.");
        }
    }

    private static void RefuseSharedIds(PublicLevel[] levelsById, string folder)
    {
        for (int index = 1; index < levelsById.Length; index++)
        {
            if (levelsById[index].Id == levelsById[index - 1].Id)
            {
                throw new InvalidOperationException(
                    $"The levels '{levelsById[index - 1].Entry.Key}' and '{levelsById[index].Entry.Key}' of the public set '{folder}' (Puzzle:PublicLevels) "
                    + $"share the id {levelsById[index].Id}; a player's place is kept by level id, so each level needs its own.");
            }
        }
    }
}
