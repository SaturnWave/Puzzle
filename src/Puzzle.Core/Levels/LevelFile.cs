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
        public int colors = 5;
        public int smoothingPermille = 60;
        public int itemsTotal;
        public int maxItemsOnBoard;
        public int itemChancePercent;
    }

#pragma warning restore SA1307, IDE1006, CA1051
}
