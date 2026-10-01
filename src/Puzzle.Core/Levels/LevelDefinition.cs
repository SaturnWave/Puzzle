using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Core.Levels
{
    /// <summary>A complete, validated level: layout, objectives, move budget and spawn rules.</summary>
    public sealed class LevelDefinition
    {
        public const int MaxGoals = 3;
        public const int MaxMoveLimit = 999;

        /// <exception cref="LevelFormatException">The parts contradict each other (e.g. a goal that cannot be reached).</exception>
        public LevelDefinition(
            LevelId id,
            DifficultyTier difficulty,
            LevelLayout layout,
            IReadOnlyList<GoalSpec> goals,
            int moveLimit,
            SpawnRules spawn)
        {
            Id = id;
            Difficulty = difficulty;
            Layout = layout;
            Goals = goals.ToArray();
            MoveLimit = moveLimit;
            Spawn = spawn;
            Validate();
        }

        public LevelId Id { get; }

        public DifficultyTier Difficulty { get; }

        public LevelLayout Layout { get; }

        public IReadOnlyList<GoalSpec> Goals { get; }

        public int MoveLimit { get; }

        public SpawnRules Spawn { get; }

        private void Validate()
        {
            if (Goals.Count == 0 || Goals.Count > MaxGoals)
            {
                throw Problem($"needs 1..{MaxGoals} goals, has {Goals.Count}");
            }

            if (MoveLimit < 1 || MoveLimit > MaxMoveLimit)
            {
                throw Problem($"move limit {MoveLimit} is outside 1..{MaxMoveLimit}");
            }

            if (Layout.MaxPresetColorIndex() >= Spawn.Colors)
            {
                throw Problem($"a preset gem uses a color outside the level's {Spawn.Colors} colors");
            }

            foreach (GoalSpec goal in Goals)
            {
                ValidateGoal(goal);
            }

            if (Goals.Distinct().Count() != Goals.Count)
            {
                throw Problem("contains duplicate goals");
            }
        }

        private void ValidateGoal(GoalSpec goal)
        {
            if (goal is GoalSpec.CollectColor collect && (int)collect.Color >= Spawn.Colors)
            {
                throw Problem($"goal {goal} uses a color outside the level's {Spawn.Colors} colors");
            }

            int available = goal switch
            {
                GoalSpec.CollectColor _ => int.MaxValue,
                GoalSpec.BreakBoxes _ => Layout.BoxCount,
                GoalSpec.ClearJelly _ => Layout.JellyCellCount,
                GoalSpec.BreakChains _ => Layout.ChainCount,
                GoalSpec.DropItems _ => Layout.PresetItemCount + Spawn.Items.Total,
                _ => throw new InvalidOperationException($"Unhandled goal {goal.GetType().Name}."),
            };

            if (goal.Target > available)
            {
                throw Problem($"goal {goal} needs {goal.Target} but the level only provides {available}");
            }
        }

        private LevelFormatException Problem(string detail) => new LevelFormatException($"Level {Id}: {detail}.");
    }
}
