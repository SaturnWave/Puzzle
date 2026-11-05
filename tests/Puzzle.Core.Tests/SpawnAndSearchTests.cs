using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

public class SpawnAndSearchTests
{
    private static readonly GemColor[] UnusualPalette = { GemColor.Red, GemColor.Cyan, GemColor.Pink, GemColor.Yellow, GemColor.Blue };

    [Fact]
    public void Only_palette_colors_ever_appear()
    {
        LevelDefinition level = OpenLevel(new SpawnRules { Palette = UnusualPalette });
        Game game = Game.Start(level, seed: 3, new GameOptions { MoveLimitOverride = 40 });
        var rng = new Rng(9);

        while (game.Status == GameStatus.Playing)
        {
            Assert.All(Gems(game.Board), color => Assert.Contains(color, UnusualPalette));
            IReadOnlyList<Move> moves = game.FindLegalMoves();
            game.TryPlay(moves[rng.NextInt(moves.Count)]);
        }
    }

    [Fact]
    public void Cascade_bias_turns_free_matches_up_and_down()
    {
        double stingy = CascadesPerMove(new SpawnRules { Palette = GemColors.First(5), CascadeBiasPercent = -100 });
        double generous = CascadesPerMove(new SpawnRules { Palette = GemColors.First(5), CascadeBiasPercent = 150 });

        Assert.True(stingy < generous * 0.6, $"cascades/move: stingy {stingy:0.00}, generous {generous:0.00}");
    }

    [Fact]
    public void Palette_and_biases_survive_the_level_file()
    {
        LevelDefinition level = OpenLevel(new SpawnRules { Palette = UnusualPalette, GoalBiasPercent = 30, CascadeBiasPercent = -40 });

        LevelDefinition restored = LevelFileMapper.ToDefinition(LevelFileMapper.ToFile(level));

        Assert.Equal(UnusualPalette, restored.Spawn.Palette);
        Assert.Equal(30, restored.Spawn.GoalBiasPercent);
        Assert.Equal(-40, restored.Spawn.CascadeBiasPercent);
    }

    [Fact]
    public void Replica_made_mid_game_replays_the_live_future_exactly()
    {
        Game live = Game.Start(GameTests.ObstacleLevel(), seed: 11, new GameOptions { MoveLimitOverride = 60 });
        var rng = new Rng(4);
        for (int i = 0; i < 8; i++)
        {
            IReadOnlyList<Move> opening = live.FindLegalMoves();
            live.TryPlay(opening[rng.NextInt(opening.Count)]);
        }

        Game replica = live.CreateReplica();
        for (int i = 0; i < 25 && live.Status == GameStatus.Playing; i++)
        {
            IReadOnlyList<Move> moves = live.FindLegalMoves();
            Move move = moves[rng.NextInt(moves.Count)];
            live.TryPlay(move);
            replica.TryPlay(move);

            Assert.Equal(live.Board.ComputeHash(), replica.Board.ComputeHash());
        }
    }

    [Fact]
    public void Solver_line_wins_when_replayed_and_beats_the_human_like_bot()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        Game start = SimulationRunner.StartGame(level, seed: 2, moveCap: 150);

        SolveResult best = new BeamSearchSolver(beamWidth: 6).Solve(start, maxMoves: 150);
        Game replay = start.CreateReplica();
        foreach (Move move in best.Path)
        {
            Assert.True(replay.TryPlay(move));
        }

        int? human = SimulationRunner.Play(level, new GreedyPolicy(SkillProfile.Average), seed: 2, moveCap: 150).CompletedAtMove;
        Assert.True(best.Solved);
        Assert.Equal(GameStatus.Won, replay.Status);
        Assert.Equal(0, start.MovesUsed);
        Assert.True(best.MovesNeeded <= human, $"solver {best.MovesNeeded}, average bot {human}");
    }

    private static double CascadesPerMove(SpawnRules spawn)
    {
        LevelDefinition level = OpenLevel(spawn);
        double cascades = 0;
        double moves = 0;
        for (ulong seed = 0; seed < 25; seed++)
        {
            RunResult run = SimulationRunner.Play(level, new RandomPolicy(), seed, moveCap: 30);
            cascades += run.Totals.Cascades;
            moves += run.MovesUsed;
        }

        return cascades / moves;
    }

    private static LevelDefinition OpenLevel(SpawnRules spawn) => new LevelDefinition(
        new LevelId(1),
        DifficultyTier.Normal,
        LevelLayout.Parse(new LayoutLayers { Cells = Blank(10, 10, '#') }),
        new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 500) },
        moveLimit: 30,
        spawn);

    private static IEnumerable<GemColor> Gems(Board board) =>
        from y in Enumerable.Range(0, board.Height)
        from x in Enumerable.Range(0, board.Width)
        let tile = board.TileAt(P(x, y))
        where tile.IsGem
        select tile.Color;
}
