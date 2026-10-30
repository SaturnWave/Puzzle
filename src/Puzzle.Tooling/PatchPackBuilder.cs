using Puzzle.Core.Levels;

namespace Puzzle.Tooling;

/// <summary>A patch pack as <see cref="PatchPackBuilder"/> made it, and what it found on the way.</summary>
/// <param name="Pack">The pack to publish.</param>
/// <param name="Changed">Levels that differ from their latest published content; each got a new content version.</param>
/// <param name="Kept">Published corrections that still stand, carried over as they were.</param>
/// <param name="MissingNow">Levels of the release that the current levels lack; they stay as published.</param>
/// <param name="NotPublished">Current levels that neither the release nor the previous pack has; a pack never adds levels.</param>
public sealed record PatchPackDraft(
    PatchPack Pack,
    IReadOnlyList<LevelId> Changed,
    IReadOnlyList<LevelId> Kept,
    IReadOnlyList<LevelId> MissingNow,
    IReadOnlyList<LevelId> NotPublished);

/// <summary>
/// Makes the next patch pack: the one that brings every release to the levels as they are now
/// (docs/GAME_DESIGN.md §20).
/// </summary>
/// <remarks>
/// <para>
/// A content version names one content of a level number, for good. A level that differs from its latest published
/// content gets the version after that one, even when the change takes it back to what a release shipped; a
/// correction once published stays in every later pack until a newer one replaces it, because builds from before
/// it still need it.
/// </para>
/// <para>
/// What was published is read from two places: the previous pack, and the levels of the release folder that carry a
/// content version above the first (a later release, made by laying a pack over the one before). Either alone is
/// enough for the levels it names; where both name a version of a level they must agree.
/// </para>
/// </remarks>
public static class PatchPackBuilder
{
    /// <param name="release">The levels of a release, as its build carries them: the first release or a later one.</param>
    /// <param name="current">The levels as they should be played now. Their own content versions are not read.</param>
    /// <param name="previous">The pack published before this one; <see cref="PatchPack.None"/> when none was.</param>
    /// <exception cref="ArgumentException">A level number occurs twice in <paramref name="release"/> or in <paramref name="current"/>.</exception>
    /// <exception cref="LevelFormatException">The release and the previous pack give one content version of a level two contents.</exception>
    public static PatchPackDraft Build(IReadOnlyList<LevelDefinition> release, IReadOnlyList<LevelDefinition> current, PatchPack previous)
    {
        Dictionary<LevelId, LevelDefinition> released = ById(release, nameof(release));
        Dictionary<LevelId, LevelDefinition> now = ById(current, nameof(current));
        IEnumerable<LevelId> published = released.Keys.Union(previous.Levels.Select(level => level.Id)).OrderBy(id => id.Value);

        var corrections = new List<LevelDefinition>();
        var changed = new List<LevelId>();
        var kept = new List<LevelId>();
        var missingNow = new List<LevelId>();
        foreach (LevelId id in published)
        {
            released.TryGetValue(id, out LevelDefinition? shipped);
            LevelDefinition? standing = LatestCorrection(shipped, previous.CorrectionOf(id));
            LevelDefinition latest = standing ?? shipped
                ?? throw new InvalidOperationException($"Level {id} is published but neither the release nor the pack holds it.");
            if (now.TryGetValue(id, out LevelDefinition? wanted) && !LevelJson.HaveSameContent(wanted, latest))
            {
                corrections.Add(wanted.WithContentVersion(latest.ContentVersion.Next()));
                changed.Add(id);
                continue;
            }

            if (wanted is null)
            {
                missingNow.Add(id);
            }

            if (standing is not null)
            {
                corrections.Add(standing);
                kept.Add(id);
            }
        }

        LevelId[] notPublished = now.Keys.Where(id => !released.ContainsKey(id) && previous.CorrectionOf(id) is null).OrderBy(id => id.Value).ToArray();
        return new PatchPackDraft(new PatchPack(previous.Sequence + 1, corrections), changed, kept, missingNow, notPublished);
    }

    /// <summary>
    /// The latest correction of a level that was published: the previous pack's, or the release's own level when the
    /// release was made after that pack and carries a later one. Absent for a level still at its first version.
    /// </summary>
    /// <exception cref="LevelFormatException">Both name the same content version and their contents differ.</exception>
    private static LevelDefinition? LatestCorrection(LevelDefinition? shipped, LevelDefinition? packed)
    {
        LevelDefinition? carried = shipped is { ContentVersion.IsFirst: false } ? shipped : null;
        if (carried is null || packed is null)
        {
            return carried ?? packed;
        }

        if (carried.ContentVersion != packed.ContentVersion)
        {
            return carried.ContentVersion > packed.ContentVersion ? carried : packed;
        }

        return LevelJson.HaveSameContent(carried, packed)
            ? packed
            : throw new LevelFormatException(
                $"Level {carried.Id}: content version {carried.ContentVersion} is one content in the release and another in the previous pack.");
    }

    private static Dictionary<LevelId, LevelDefinition> ById(IReadOnlyList<LevelDefinition> levels, string name)
    {
        var byId = new Dictionary<LevelId, LevelDefinition>();
        foreach (LevelDefinition level in levels)
        {
            if (!byId.TryAdd(level.Id, level))
            {
                throw new ArgumentException($"Level {level.Id} occurs twice.", name);
            }
        }

        return byId;
    }
}
