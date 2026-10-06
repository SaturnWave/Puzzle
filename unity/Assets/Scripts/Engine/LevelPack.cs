#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Core.Levels;
using UnityEngine;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// Every level of the game in one JSON document, as tools/unity/sync-engine.js packs them from levels/generated.
    /// The level entries are the engine's own level file model, which is laid out for Unity's JSON reader.
    /// </summary>
    [Serializable]
    public sealed class LevelPack
    {
        public LevelFile[] levels = Array.Empty<LevelFile>();

        /// <exception cref="LevelFormatException">The document holds no level, or one of its levels is not a valid level.</exception>
        public static IReadOnlyList<LevelDefinition> Read(string json)
        {
            LevelPack pack = JsonUtility.FromJson<LevelPack>(json);
            if (pack == null || pack.levels.Length == 0)
            {
                throw new LevelFormatException("The level pack holds no level.");
            }

            var levels = new List<LevelDefinition>(pack.levels.Length);
            foreach (LevelFile file in pack.levels)
            {
                levels.Add(LevelFileMapper.ToDefinition(WithAbsentLayersAbsent(file)));
            }

            return levels;
        }

        /// <summary>One level file, as the tools write them.</summary>
        /// <exception cref="LevelFormatException">The document is not a valid level.</exception>
        public static LevelDefinition ReadLevel(string json) =>
            LevelFileMapper.ToDefinition(WithAbsentLayersAbsent(JsonUtility.FromJson<LevelFile>(json)));

        /// <summary>
        /// Unity's reader fills a field the document leaves out with an empty value, where the level format means
        /// "absent": no preset tiles, no jelly, no chains, no explicit palette, no colour on a goal.
        /// </summary>
        private static LevelFile WithAbsentLayersAbsent(LevelFile file)
        {
            file.tiles = NullIfEmpty(file.tiles);
            file.jelly = NullIfEmpty(file.jelly);
            file.chains = NullIfEmpty(file.chains);
            file.spawn.palette = NullIfEmpty(file.spawn.palette);
            foreach (GoalFile goal in file.goals)
            {
                goal.color = string.IsNullOrEmpty(goal.color) ? null : goal.color;
            }

            return file;
        }

        private static string[]? NullIfEmpty(string[]? layer) => layer == null || layer.Length == 0 ? null : layer;
    }
}
