using System.Reflection;
using Puzzle.Core.Levels;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

public class LevelFormatTests
{
    [Fact]
    public void Level_file_round_trips()
    {
        LevelDefinition original = GameTests.ObstacleLevel();

        LevelFile file = LevelFileMapper.ToFile(original);
        LevelDefinition restored = LevelFileMapper.ToDefinition(file);
        LevelFile again = LevelFileMapper.ToFile(restored);

        Assert.Equal(file.cells, again.cells);
        Assert.Equal(file.tiles, again.tiles);
        Assert.Equal(file.jelly, again.jelly);
        Assert.Equal(file.chains, again.chains);
        Assert.Equal(original.Goals, restored.Goals);
        Assert.Equal(original.MoveLimit, restored.MoveLimit);
        Assert.Equal(original.Spawn.Items.Total, restored.Spawn.Items.Total);
    }

    [Fact]
    public void Unknown_glyph_is_reported_with_level_and_cell()
    {
        LevelFile file = LevelFileMapper.ToFile(GameTests.OpenLevel());
        file.id = 7;
        file.tiles = Blank(10, 12).With(3, 4, 'Z');

        var error = Assert.Throws<LevelFormatException>(() => LevelFileMapper.ToDefinition(file));

        Assert.Contains("Level 7", error.Message);
        Assert.Contains("(3,4)", error.Message);
    }

    [Fact]
    public void Goal_that_the_layout_cannot_satisfy_is_rejected()
    {
        LevelFile file = LevelFileMapper.ToFile(GameTests.OpenLevel());
        file.goals = new[] { new GoalFile { type = "BreakBoxes", count = 3 } };

        var error = Assert.Throws<LevelFormatException>(() => LevelFileMapper.ToDefinition(file));

        Assert.Contains("only provides 0", error.Message);
    }

    [Fact]
    public void Goal_variants_are_a_closed_set()
    {
        // C# cannot prove switch exhaustiveness: when a goal type is added, every switch over GoalSpec
        // (LevelDefinition, LevelFileMapper, GoalTracker, the bot evaluator) must be revisited.
        string[] variants = typeof(GoalSpec)
            .GetNestedTypes(BindingFlags.Public)
            .Where(t => t.IsSubclassOf(typeof(GoalSpec)))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(new[] { "BreakBoxes", "BreakChains", "ClearJelly", "CollectColor", "DropItems" }, variants);
    }
}
