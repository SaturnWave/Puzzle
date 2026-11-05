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
        /// <summary>The pack inside a built game: Resources/Levels.json, named without its extension.</summary>
        private const string PackResource = "Levels";

        public LevelFile[] levels = Array.Empty<LevelFile>();

        /// <summary>The levels the game carries with it.</summary>
        /// <exception cref="LevelFormatException">The pack is missing, or one of its levels is not a valid level.</exception>
        public static IReadOnlyList<LevelDefinition> LoadPacked()
        {
            TextAsset? pack = Resources.Load<TextAsset>(PackResource);
            if (pack == null)
            {
                throw new LevelFormatException("The game carries no level pack; run node tools/unity/sync-engine.js from the repository root.");
            }

            return Read(pack.text);
        }

        /// <summary>The level a player knows by this number.</summary>
        /// <exception cref="LevelFormatException">No level has the number.</exception>
        public static LevelDefinition Numbered(IReadOnlyList<LevelDefinition> levels, int number)
        {
            foreach (LevelDefinition level in levels)
            {
                if (level.Id.Value == number)
                {
                    return level;
                }
            }

            throw new LevelFormatException($"The level pack has no level {number}.");
        }

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
        /// "absent": no preset tiles, no jelly, no chains, no explicit palette, no colour on a goal, and a content
        /// version nobody stated, which is the first.
        /// </summary>
        private static LevelFile WithAbsentLayersAbsent(LevelFile file)
        {
            if (file.contentVersion == 0)
            {
                file.contentVersion = ContentVersion.FirstValue;
            }

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
