using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Model;

namespace Puzzle.Core.Levels
{
    /// <summary>Converts between the raw <see cref="LevelFile"/> wire format and a validated <see cref="LevelDefinition"/>.</summary>
    public static class LevelFileMapper
    {
        /// <exception cref="LevelFormatException">The file is malformed; the message names the level and the problem.</exception>
        public static LevelDefinition ToDefinition(LevelFile file)
        {
            if (file.formatVersion != LevelFile.CurrentFormatVersion)
            {
                throw new LevelFormatException($"Level {file.id}: unsupported format version {file.formatVersion}.");
            }

            LevelId id = WithLevelContext(file.id, () => new LevelId(file.id));
            DifficultyTier difficulty = WithLevelContext(file.id, () => ParseDifficulty(file.difficulty));
            LevelLayout layout = WithLevelContext(file.id, () => ParseLayout(file));
            GoalSpec[] goals = WithLevelContext(file.id, () => file.goals.Select(ParseGoal).ToArray());
            SpawnRules spawn = WithLevelContext(file.id, () => ParseSpawn(file.spawn));
            AttemptPolicy attempts = WithLevelContext(file.id, () => ParseAttempts(file.attempts));

            // LevelDefinition reports cross-part inconsistencies with the level id already in the message.
            return new LevelDefinition(id, difficulty, layout, goals, file.moves, spawn, attempts);
        }

        public static LevelFile ToFile(LevelDefinition level)
        {
            LayoutLayers layers = level.Layout.ToLayers();
            return new LevelFile
            {
                id = level.Id.Value,
                difficulty = level.Difficulty.ToString(),
                width = level.Layout.Shape.Width,
                height = level.Layout.Shape.Height,
                moves = level.MoveLimit,
                cells = layers.Cells.ToArray(),
                tiles = NullIfBlank(layers.Tiles),
                jelly = NullIfBlank(layers.Jelly),
                chains = NullIfBlank(layers.Chains),
                goals = level.Goals.Select(ToGoalFile).ToArray(),
                spawn = new SpawnFile
                {
                    colors = level.Spawn.Colors,
                    palette = IsDefaultPalette(level.Spawn.Palette) ? null : level.Spawn.Palette.Select(c => c.ToString()).ToArray(),
                    smoothingPermille = level.Spawn.SmoothingPermille,
                    goalBias = level.Spawn.GoalBiasPercent,
                    cascadeBias = level.Spawn.CascadeBiasPercent,
                    itemsTotal = level.Spawn.Items.Total,
                    maxItemsOnBoard = level.Spawn.Items.MaxOnBoard,
                    itemChancePercent = level.Spawn.Items.ChancePercent,
                },
                attempts = ToAttemptsFile(level.Attempts),
            };
        }

        /// <summary>Prefixes value-validation failures of one part of the file with the level id.</summary>
        private static T WithLevelContext<T>(int levelId, Func<T> parse)
        {
            try
            {
                return parse();
            }
            catch (LevelFormatException ex)
            {
                throw new LevelFormatException($"Level {levelId}: {ex.Message}", ex);
            }
            catch (ArgumentException ex)
            {
                throw new LevelFormatException($"Level {levelId}: {ex.Message}", ex);
            }
        }

        private static LevelLayout ParseLayout(LevelFile file)
        {
            LevelLayout layout = LevelLayout.Parse(new LayoutLayers
            {
                Cells = file.cells,
                Tiles = file.tiles,
                Jelly = file.jelly,
                Chains = file.chains,
            });
            return layout.Shape.Width == file.width && layout.Shape.Height == file.height
                ? layout
                : throw new LevelFormatException(
                    $"declared size {file.width}x{file.height} but cells are {layout.Shape.Width}x{layout.Shape.Height}");
        }

        private static DifficultyTier ParseDifficulty(string text) =>
            Enum.TryParse(text, ignoreCase: false, out DifficultyTier tier) && Enum.IsDefined(typeof(DifficultyTier), tier)
                ? tier
                : throw new LevelFormatException($"unknown difficulty '{text}'");

        private static GoalSpec ParseGoal(GoalFile goal) => goal.type switch
        {
            "CollectColor" => new GoalSpec.CollectColor(ParseColor(goal.color), goal.count),
            "BreakBoxes" => new GoalSpec.BreakBoxes(goal.count),
            "ClearJelly" => new GoalSpec.ClearJelly(goal.count),
            "BreakChains" => new GoalSpec.BreakChains(goal.count),
            "DropItems" => new GoalSpec.DropItems(goal.count),
            _ => throw new LevelFormatException($"unknown goal type '{goal.type}'"),
        };

        private static GemColor ParseColor(string? text) =>
            text is not null && Enum.TryParse(text, ignoreCase: false, out GemColor color) && Enum.IsDefined(typeof(GemColor), color)
                ? color
                : throw new LevelFormatException($"unknown gem color '{text}'");

        private static SpawnRules ParseSpawn(SpawnFile spawn) => new SpawnRules
        {
            Palette = spawn.palette is { Length: > 0 } names ? names.Select(ParseColor).ToArray() : GemColors.First(spawn.colors),
            SmoothingPermille = spawn.smoothingPermille,
            GoalBiasPercent = spawn.goalBias,
            CascadeBiasPercent = spawn.cascadeBias,
            Items = new ItemSpawnRules
            {
                Total = spawn.itemsTotal,
                MaxOnBoard = spawn.maxItemsOnBoard,
                ChancePercent = spawn.itemChancePercent,
            },
        };

        private static AttemptPolicy ParseAttempts(AttemptsFile? attempts)
        {
            if (attempts is null)
            {
                return AttemptPolicy.Default;
            }

            StartBoard board = attempts.board switch
            {
                "varied" => new StartBoard.Varied(),
                "fixed" => new StartBoard.Fixed(attempts.boardSeed),
                _ => throw new LevelFormatException($"unknown start board '{attempts.board}' (use varied or fixed)"),
            };
            return new AttemptPolicy { Board = board, SeedPool = attempts.seedPool };
        }

        /// <summary>The default policy is omitted so plain levels stay short.</summary>
        private static AttemptsFile? ToAttemptsFile(AttemptPolicy attempts) => attempts.Board switch
        {
            StartBoard.Varied _ when attempts.SeedPool.Count == 0 => null,
            StartBoard.Varied _ => new AttemptsFile { board = "varied", seedPool = attempts.SeedPool.ToArray() },
            StartBoard.Fixed fixedBoard => new AttemptsFile { board = "fixed", boardSeed = fixedBoard.Seed, seedPool = attempts.SeedPool.ToArray() },
            _ => throw new InvalidOperationException($"Unhandled start board {attempts.Board.GetType().Name}."),
        };

        private static GoalFile ToGoalFile(GoalSpec goal) => goal switch
        {
            GoalSpec.CollectColor collect => new GoalFile { type = "CollectColor", color = collect.Color.ToString(), count = goal.Target },
            GoalSpec.BreakBoxes _ => new GoalFile { type = "BreakBoxes", count = goal.Target },
            GoalSpec.ClearJelly _ => new GoalFile { type = "ClearJelly", count = goal.Target },
            GoalSpec.BreakChains _ => new GoalFile { type = "BreakChains", count = goal.Target },
            GoalSpec.DropItems _ => new GoalFile { type = "DropItems", count = goal.Target },
            _ => throw new InvalidOperationException($"Unhandled goal {goal.GetType().Name}."),
        };

        private static bool IsDefaultPalette(IReadOnlyList<GemColor> palette) =>
            palette.SequenceEqual(GemColors.First(palette.Count));

        /// <summary>Optional layers that carry no information are omitted from the file.</summary>
        private static string[]? NullIfBlank(IReadOnlyList<string>? rows) =>
            rows is null || rows.All(row => row.All(glyph => glyph == TileGlyphs.NoLayer)) ? null : rows.ToArray();
    }
}
