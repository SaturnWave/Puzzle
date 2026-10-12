using System;

namespace Puzzle.Core.Levels
{
    // Serialization contract of a patch pack; raw and unvalidated like LevelFile, whose conventions it follows.
#pragma warning disable SA1307, IDE1006, CA1051 // public camelCase fields are the wire format

    [Serializable]
    public sealed class PatchPackFile
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;

        /// <summary>Which pack this is: packs are numbered from 1 in the order they were published. A file without it is not a pack.</summary>
        public int sequence;

        /// <summary>Whole level files, each at the content version it has once the pack is applied.</summary>
        public LevelFile[] levels = Array.Empty<LevelFile>();
    }

#pragma warning restore SA1307, IDE1006, CA1051
}
