using System.IO;
using Puzzle.Client.Engine;
using Puzzle.Tooling;

namespace Puzzle.Core.Tests;

/// <summary>
/// The engine's fingerprint on the tools' runtime. The Unity project plays the same games inside the Editor and
/// compares itself with the same file (unity/Assets/Editor/EngineSmokeTest.cs), so a difference between the two
/// runtimes shows up there. <c>EngineTrace</c> itself is the Unity project's source file, compiled into these tests.
/// </summary>
public sealed class EngineTraceTests
{
    /// <summary>Set to 1 to write the trace anew after a deliberate change of an engine rule, then run this test once.</summary>
    private const string PinAnewVariable = "PUZZLE_PIN_ENGINE_TRACE";

    [Fact]
    public void The_sample_levels_play_as_the_pinned_trace_says()
    {
        string[] played = EngineTrace.OfSamples(RepoFiles.SampleLevelPaths.Select(LevelJson.Load));
        if (Environment.GetEnvironmentVariable(PinAnewVariable) == "1")
        {
            File.WriteAllLines(RepoFiles.EngineTracePath, played);
        }

        Assert.Equal(File.ReadAllLines(RepoFiles.EngineTracePath), played);
    }

    [Fact]
    public void The_hints_on_the_sample_games_are_the_pinned_ones_and_leave_the_games_as_the_engine_trace_has_them()
    {
        Levels.LevelDefinition[] samples = RepoFiles.SampleLevelPaths.Select(LevelJson.Load).ToArray();
        string[] hinted = EngineTrace.HintsOfSamples(samples, "casual");
        if (Environment.GetEnvironmentVariable(PinAnewVariable) == "1")
        {
            File.WriteAllLines(RepoFiles.HintTracePath, hinted);
        }

        Assert.Equal(File.ReadAllLines(RepoFiles.HintTracePath), hinted);
        Assert.Equal(File.ReadAllLines(RepoFiles.EngineTracePath), EngineTrace.OfSamples(samples)); // asked after the hints: they left nothing behind
    }

    [Fact]
    public void A_game_played_twice_with_one_seed_leaves_the_same_trace()
    {
        Levels.LevelDefinition level = LevelJson.Load(RepoFiles.SampleLevelPaths[0]);

        Assert.Equal(EngineTrace.Of(level, seed: 42), EngineTrace.Of(level, seed: 42));
        Assert.NotEqual(EngineTrace.Of(level, seed: 42), EngineTrace.Of(level, seed: 43));
    }
}
