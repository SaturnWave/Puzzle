using System;

namespace Puzzle.Core.Levels
{
    // Serialization contract of a level file. Field names are camelCase on purpose: Unity's JsonUtility
    // maps public fields verbatim, and System.Text.Json reads them with IncludeFields. Every value here is
    // raw and unvalidated; LevelFileMapper turns it into a LevelDefinition or reports what is wrong.
#pragma warning disable SA1307, IDE1006, CA1051 // public camelCase fields are the wire format

    [Serializable]
    public sealed class LevelFile
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public int id;
        public string difficulty = string.Empty;
        public int width;
        public int height;
        public int moves;
        public string[] cells = Array.Empty<string>();
        public string[]? tiles;
        public string[]? jelly;
        public string[]? chains;
        public GoalFile[] goals = Array.Empty<GoalFile>();
        public SpawnFile spawn = new SpawnFile();

        /// <summary>Optional; omitted means varied boards and no calibrated seed pool.</summary>
        public AttemptsFile? attempts;
    }

    [Serializable]
    public sealed class GoalFile
    {
        public string type = string.Empty;

        /// <summary>Only for CollectColor goals.</summary>
        public string? color;
        public int count;
    }

    [Serializable]
    public sealed class SpawnFile
    {
        /// <summary>Color count; the palette defaults to the first N colors.</summary>
        public int colors = 5;

        /// <summary>Explicit color names; when present it wins and <see cref="colors"/> is ignored.</summary>
        public string[]? palette;
        public int smoothingPermille = 60;
        public int goalBias;
        public int cascadeBias;
        public int itemsTotal;
        public int maxItemsOnBoard;
        public int itemChancePercent;
    }

    [Serializable]
    public sealed class AttemptsFile
    {
        /// <summary>"varied" (new starting board each attempt) or "fixed" (always <see cref="boardSeed"/>).</summary>
        public string board = "varied";

        /// <summary>Required when <see cref="board"/> is "fixed"; absent otherwise.</summary>
        public uint? boardSeed;

        /// <summary>Play seeds calibrated to the level's typical difficulty; attempt k uses entry k mod length.</summary>
        public uint[] seedPool = Array.Empty<uint>();
    }

#pragma warning restore SA1307, IDE1006, CA1051
}
