using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Core.Levels
{
    /// <summary>
    /// Corrections to the levels of a released game (docs/GAME_DESIGN.md §20). The game carries its levels with it and
    /// plays without a connection; a pack fetched later replaces some of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pack is for every release at once: it holds each level that ever got a content version above the first, whole,
    /// at its latest version. A build lays it over its own levels and takes whatever is newer than what it carries, so
    /// an old build and a new one read the same pack and the newest pack alone is enough.
    /// </para>
    /// <para>
    /// Packs are numbered in the order they were published (<see cref="Sequence"/>). A device keeps the pack with the
    /// highest number it has seen, so a stale copy served late never takes a level back.
    /// </para>
    /// </remarks>
    public sealed class PatchPack
    {
        public const int FirstSequence = 1;

        private readonly LevelDefinition[] _levels;
        private readonly Dictionary<LevelId, LevelDefinition> _byId = new Dictionary<LevelId, LevelDefinition>();

        /// <param name="sequence">Which pack this is, counted from <see cref="FirstSequence"/>.</param>
        /// <param name="levels">The corrected levels, in any order.</param>
        /// <exception cref="LevelFormatException">The number is below the first, two entries correct the same level, or an entry is at the first content version.</exception>
        public PatchPack(int sequence, IReadOnlyList<LevelDefinition> levels)
            : this(levels)
        {
            Sequence = sequence >= FirstSequence
                ? sequence
                : throw new LevelFormatException($"Patch pack: sequence {sequence}; packs are numbered from {FirstSequence}.");
        }

        private PatchPack(IReadOnlyList<LevelDefinition> levels)
        {
            _levels = (levels ?? throw new ArgumentNullException(nameof(levels))).OrderBy(level => level.Id.Value).ToArray();
            foreach (LevelDefinition level in _levels)
            {
                if (level.ContentVersion.IsFirst)
                {
                    throw new LevelFormatException($"Patch pack: level {level.Id} is at the first content version; a correction has a later one.");
                }

                if (!_byId.TryAdd(level.Id, level))
                {
                    throw new LevelFormatException($"Patch pack: level {level.Id} is corrected twice.");
                }
            }
        }

        /// <summary>What a device has before it ever fetched a pack: no correction, and a number every pack is above.</summary>
        public static PatchPack None { get; } = new PatchPack(Array.Empty<LevelDefinition>());

        /// <summary>Which pack this is; 0 only for <see cref="None"/>.</summary>
        public int Sequence { get; }

        /// <summary>The corrected levels, by level number.</summary>
        public IReadOnlyList<LevelDefinition> Levels => _levels;

        /// <summary>Whether a device that holds <paramref name="other"/> should take this pack instead.</summary>
        public bool IsNewerThan(PatchPack other) => Sequence > (other ?? throw new ArgumentNullException(nameof(other))).Sequence;

        /// <exception cref="LevelFormatException">The file is of an unknown format version, has no sequence number, or the pack or one of its levels is not valid.</exception>
        public static PatchPack FromFile(PatchPackFile file)
        {
            if (file is null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (file.formatVersion != PatchPackFile.CurrentFormatVersion)
            {
                throw new LevelFormatException($"Patch pack: unsupported format version {file.formatVersion}.");
            }

            if (file.levels is null || file.levels.Any(level => level is null))
            {
                throw new LevelFormatException("Patch pack: the list of levels is missing or holds an empty entry.");
            }

            return new PatchPack(file.sequence, file.levels.Select(LevelFileMapper.ToDefinition).ToArray());
        }

        public PatchPackFile ToFile() => new PatchPackFile { sequence = Sequence, levels = _levels.Select(LevelFileMapper.ToFile).ToArray() };

        /// <summary>The correction of a level number; absent when the pack leaves that level alone.</summary>
        public LevelDefinition? CorrectionOf(LevelId id) => _byId.TryGetValue(id, out LevelDefinition? correction) ? correction : null;

        /// <summary>
        /// Lays the pack over the levels a build carries. A correction replaces the build's level only when its
        /// content version is higher: a newer build already carries that correction, or a later one. A correction
        /// of a level the build does not have is left out, since a pack never adds levels.
        /// </summary>
        public PatchedLevels ApplyTo(IReadOnlyList<LevelDefinition> shipped)
        {
            if (shipped is null)
            {
                throw new ArgumentNullException(nameof(shipped));
            }

            var levels = new LevelDefinition[shipped.Count];
            var replaced = new List<LevelId>();
            var alreadyCurrent = new List<LevelId>();
            var carried = new HashSet<LevelId>();
            for (int i = 0; i < shipped.Count; i++)
            {
                LevelDefinition level = shipped[i];
                carried.Add(level.Id);
                levels[i] = level;
                if (CorrectionOf(level.Id) is not LevelDefinition correction)
                {
                    continue;
                }

                if (correction.ContentVersion > level.ContentVersion)
                {
                    levels[i] = correction;
                    replaced.Add(level.Id);
                }
                else
                {
                    alreadyCurrent.Add(level.Id);
                }
            }

            LevelId[] unknown = _levels.Select(level => level.Id).Where(id => !carried.Contains(id)).ToArray();
            return new PatchedLevels(levels, replaced, alreadyCurrent, unknown);
        }
    }

    /// <summary>The levels of a build with a patch pack laid over them, and what the pack did.</summary>
    public sealed class PatchedLevels
    {
        internal PatchedLevels(
            IReadOnlyList<LevelDefinition> levels,
            IReadOnlyList<LevelId> replaced,
            IReadOnlyList<LevelId> alreadyCurrent,
            IReadOnlyList<LevelId> unknown)
        {
            Levels = levels;
            Replaced = replaced;
            AlreadyCurrent = alreadyCurrent;
            Unknown = unknown;
        }

        /// <summary>The levels to play, in the order of the build's levels.</summary>
        public IReadOnlyList<LevelDefinition> Levels { get; }

        /// <summary>Levels the pack replaced.</summary>
        public IReadOnlyList<LevelId> Replaced { get; }

        /// <summary>Corrections left aside because the build's level is at that content version or a later one.</summary>
        public IReadOnlyList<LevelId> AlreadyCurrent { get; }

        /// <summary>Corrections of level numbers the build does not have.</summary>
        public IReadOnlyList<LevelId> Unknown { get; }
    }
}
