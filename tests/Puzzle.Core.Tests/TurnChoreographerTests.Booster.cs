using Puzzle.Client.Play;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// A booster used during play, as the Unity game shows it (TurnChoreographer.ComposeBooster): its tool's own cue, what
/// it hits as the tool reaches each cell, the board settling as after a move, no move spent, the goals counted, and
/// the view's record of the board brought to the engine's.
/// </summary>
public partial class TurnChoreographerTests
{
    /// <summary>
    /// A board of every obstacle a booster meets: crates of one and two hits in the fourth row, tape on two gems, a
    /// slab under three cells, and a rocket and a bomb standing in it; the goals count crates, tape and slabs.
    /// </summary>
    private static Table TableWithObstacles(ulong seed = 1)
    {
        string[] tiles = Filler(9, 9).With(1, 4, '1').With(3, 4, '2').With(5, 4, 'H').With(7, 4, 'X').With(4, 6, '2');
        string[] chains = Blank(9, 9).With(2, 4, '1').With(4, 2, '2');
        string[] jelly = Blank(9, 9).With(0, 4, '1').With(4, 4, '2').With(4, 5, '1');
        GoalSpec[] goals = { new GoalSpec.BreakBoxes(3), new GoalSpec.BreakChains(2), new GoalSpec.ClearJelly(3) };
        return TableOf(tiles, seed, goals, jelly, chains);
    }

    public static TheoryData<string> EveryBooster() => new TheoryData<string> { "cell 4 4", "cell 4 2", "cell 1 4", "row 4", "row 2", "column 4", "column 7", "shuffle" };

    private static BoosterUse BoosterNamed(string name)
    {
        string[] words = name.Split(' ');
        return words[0] switch
        {
            "cell" => new BoosterUse.HitCell(P(int.Parse(words[1]), int.Parse(words[2]))),
            "row" => new BoosterUse.HitRow(P(0, int.Parse(words[1]))),
            "column" => new BoosterUse.HitColumn(P(int.Parse(words[1]), 0)),
            "shuffle" => new BoosterUse.Shuffle(),
            _ => throw new ArgumentException(name),
        };
    }

    /// <summary>Uses the booster on the engine and writes its script, as the stage does; also what the goals counted before.</summary>
    private static (TurnScript Script, int[] DoneBefore) UseOn(Table table, BoosterUse booster)
    {
        int[] doneBefore = Enumerable.Range(0, table.Game.Goals.Count).Select(goal => table.Game.Goals[goal].Done).ToArray();
        Assert.True(table.Game.TryUseBooster(booster), $"{booster} was refused");
        return (TurnChoreographer.ComposeBooster(table.Ledger, table.Game.Level.Goals, table.Game.LastEvents, table.Game.Board), doneBefore);
    }

    [Theory]
    [MemberData(nameof(EveryBooster))]
    public void A_booster_on_a_board_of_obstacles_ends_on_the_engines_board_spends_no_move_and_counts_the_goals(string name)
    {
        Table table = TableWithObstacles();

        (TurnScript script, int[] doneBefore) = UseOn(table, BoosterNamed(name));

        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
        Assert.Empty(script.Cues.OfType<Cue.MoveSpent>());
        Assert.Equal(0, table.Game.MovesUsed);
        for (int goal = 0; goal < table.Game.Goals.Count; goal++)
        {
            int ticks = script.Cues.OfType<Cue.GoalTick>().Count(tick => tick.GoalIndex == goal);
            Assert.Equal(table.Game.Goals[goal].Done - doneBefore[goal], ticks);
        }
    }

    [Theory]
    [MemberData(nameof(EveryBooster))]
    public void A_boosters_tool_is_seen_for_no_more_than_about_a_second_and_the_shuffle_deals_the_gems_anew(string name)
    {
        Table table = TableWithObstacles();
        BoosterUse booster = BoosterNamed(name);

        (TurnScript script, _) = UseOn(table, booster);

        Cue.ToolUsed[] tools = script.Cues.OfType<Cue.ToolUsed>().ToArray();
        if (booster is BoosterUse.Shuffle)
        {
            Assert.Empty(tools);
            Assert.Single(script.Cues.OfType<Cue.Reshuffle>());
            return;
        }

        Cue.ToolUsed tool = Assert.Single(tools);
        Assert.Equal(booster, tool.Booster);
        Assert.Equal(0f, tool.Start);
        Assert.InRange(tool.Seconds, 0.3f, 1.0f);
        Assert.Empty(script.Cues.OfType<Cue.Reshuffle>());
    }

    [Fact]
    public void The_spatula_strikes_its_cell_when_it_comes_down_on_it()
    {
        Table table = TableOf(Filler(7, 7).With(0, 0, 'H'));

        (TurnScript script, _) = UseOn(table, new BoosterUse.HitCell(P(3, 3)));

        Cue.Leave struck = Assert.Single(script.Cues.OfType<Cue.Leave>(), leave => leave.At == P(3, 3));
        Assert.Equal(Tempo.SpatulaStrikes, struck.Start, Tolerance);
        Assert.Equal(LeaveManner.Pop, struck.Manner);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_brush_paints_its_row_or_column_one_cell_after_another_from_its_start(bool isAlongTheRow)
    {
        Table table = TableOf(Filler(9, 8).With(0, 0, 'H'));
        BoosterUse brush = isAlongTheRow ? new BoosterUse.HitRow(P(6, 5)) : new BoosterUse.HitColumn(P(2, 6));

        (TurnScript script, _) = UseOn(table, brush);

        Cue.Leave[] struck = script.Cues.OfType<Cue.Leave>()
            .Where(leave => isAlongTheRow ? leave.At.Y == 5 : leave.At.X == 2)
            .Where(leave => leave.Start < Tempo.BrushLead + 9 * Tempo.BrushPerCell)
            .OrderBy(leave => isAlongTheRow ? leave.At.X : leave.At.Y)
            .ToArray();
        Assert.Equal(isAlongTheRow ? 9 : 8, struck.Length);
        for (int cell = 0; cell < struck.Length; cell++)
        {
            Assert.Equal(Tempo.BrushLead + cell * Tempo.BrushPerCell, struck[cell].Start, Tolerance);
        }
    }

    [Fact]
    public void A_special_piece_the_brush_reaches_goes_off_as_it_is_reached()
    {
        Table table = TableOf(Filler(9, 9).With(6, 4, 'V'));

        (TurnScript script, _) = UseOn(table, new BoosterUse.HitRow(P(0, 4)));

        Cue.Leave rocket = Assert.Single(script.Cues.OfType<Cue.Leave>(), leave => leave.At == P(6, 4) && leave.Manner == LeaveManner.Fire);
        Assert.Equal(Tempo.BrushLead + 6 * Tempo.BrushPerCell, rocket.Start, Tolerance);
        Cue.Effect stroke = Assert.Single(script.Cues.OfType<Cue.Effect>(), effect => effect.Kind == EffectKind.ClearColumn);
        Assert.True(stroke.Start >= rocket.Start, "the rocket's stroke left before the brush reached it");
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void Events_that_are_not_a_boosters_are_refused_by_its_entry()
    {
        Table table = TableOf(BoardWithAMatchInReach());
        Assert.True(table.Game.TryPlay(Move.Swap(P(2, 2), P(2, 3))));

        Assert.Throws<InvalidOperationException>(() => TurnChoreographer.ComposeBooster(table.Ledger, table.Game.Level.Goals, table.Game.LastEvents, table.Game.Board));
        Assert.Throws<InvalidOperationException>(() => TurnChoreographer.ComposeBooster(table.Ledger, table.Game.Level.Goals, Array.Empty<GameEvent>(), table.Game.Board));
    }
}
