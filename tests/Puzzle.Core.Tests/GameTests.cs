using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Resolution;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

public class GameTests
{
    public static IEnumerable<object[]> Levels() => new[]
    {
        new object[] { "open 10x12", OpenLevel() },
        new object[] { "obstacles 10x12", ObstacleLevel() },
    };

    [Theory]
    [MemberData(nameof(Levels))]
    public void Starting_boards_are_full_stable_and_playable(string name, LevelDefinition level)
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            Game game = Game.Start(level, seed);

            AssertStable(game, $"{name} seed {seed}");
            Assert.True(game.FindLegalMoves().Count >= RuleSet.Default.MinInitialMoves, $"{name} seed {seed}");
        }
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Random_play_keeps_every_invariant(string name, LevelDefinition level)
    {
        var options = new GameOptions { MoveLimitOverride = 80 };
        for (ulong seed = 0; seed < 40; seed++)
        {
            Game game = Game.Start(level, seed, options);
            var rng = new Rng(seed + 1000);
            int[] jellyBefore = JellyLayers(game.Board);
            while (game.Status == GameStatus.Playing)
            {
                IReadOnlyList<Move> moves = game.FindLegalMoves();
                int movesUsed = game.MovesUsed;

                Assert.True(game.TryPlay(moves[rng.NextInt(moves.Count)]));

                string context = $"{name} seed {seed} move {game.MovesUsed}";
                Assert.Equal(movesUsed + 1, game.MovesUsed);
                if (game.Status == GameStatus.Playing)
                {
                    AssertStable(game, context);
                }

                int[] jellyAfter = JellyLayers(game.Board);
                Assert.True(jellyAfter.Zip(jellyBefore).All(pair => pair.First <= pair.Second), $"jelly grew: {context}");
                jellyBefore = jellyAfter;
            }

            Assert.NotEqual(GameStatus.LostNoMovesPossible, game.Status);
        }
    }

    [Fact]
    public void Same_seed_and_moves_replay_identically()
    {
        LevelDefinition level = ObstacleLevel();

        List<ulong> first = PlayFirstMoves(level, seed: 42, moves: 40);
        List<ulong> second = PlayFirstMoves(level, seed: 42, moves: 40);
        List<ulong> other = PlayFirstMoves(level, seed: 43, moves: 1);

        Assert.Equal(first, second);
        Assert.NotEqual(first[0], other[0]);
    }

    [Fact]
    public void Lookahead_never_touches_the_live_game_and_hides_refills()
    {
        Game game = Game.Start(OpenLevel(), seed: 7);
        ulong liveHash = game.Board.ComputeHash();
        Move move = game.FindLegalMoves()[0];

        Game lookahead = game.CreateLookahead(seed: 99);
        Assert.True(lookahead.TryPlay(move));

        Assert.Equal(liveHash, game.Board.ComputeHash());
        Assert.Equal(0, game.MovesUsed);
        Assert.Contains(AllPositions(lookahead.Board), p => lookahead.Board.TileAt(p).Kind == TileKind.Hidden);
    }

    internal static LevelDefinition OpenLevel() => new LevelDefinition(
        new LevelId(1),
        DifficultyTier.Normal,
        LevelLayout.Parse(new LayoutLayers { Cells = Blank(10, 12, '#') }),
        new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 60) },
        moveLimit: 30,
        new SpawnRules { Colors = 5 });

    internal static LevelDefinition ObstacleLevel()
    {
        string[] cells =
        {
            "..######..",
            ".########.",
            "##########",
            "##########",
            "####..####",
            "####..####",
            "##########",
            "##########",
            "##########",
            "##########",
            ".########.",
            "..######..",
        };
        string[] tiles = Blank(10, 12)
            .With(3, 2, '1').With(4, 2, '2').With(5, 2, '2').With(6, 2, '1')
            .With(0, 6, '3').With(9, 6, '3');
        string[] jelly = Blank(10, 12);
        for (int x = 2; x < 8; x++)
        {
            jelly = jelly.With(x, 11, '2').With(x, 10, '1');
        }

        string[] chains = Blank(10, 12).With(2, 8, '1').With(7, 8, '2').With(4, 7, '1');
        LevelLayout layout = LevelLayout.Parse(new LayoutLayers { Cells = cells, Tiles = tiles, Jelly = jelly, Chains = chains });
        return new LevelDefinition(
            new LevelId(2),
            DifficultyTier.Hard,
            layout,
            new GoalSpec[] { new GoalSpec.BreakBoxes(6), new GoalSpec.ClearJelly(layout.JellyCellCount), new GoalSpec.DropItems(3) },
            moveLimit: 40,
            new SpawnRules
            {
                Colors = 5,
                Items = new ItemSpawnRules { Total = 3, MaxOnBoard = 2, ChancePercent = 8 },
            });
    }

    private static void AssertStable(Game game, string context)
    {
        Board board = game.Board;
        foreach (Position p in AllPositions(board).Where(board.Shape.IsPlayable))
        {
            Tile tile = board.TileAt(p);
            Assert.False(tile.IsEmpty && !IsSealed(board, p), $"fillable empty cell {p}: {context}");
            Assert.NotEqual(TileKind.Hidden, tile.Kind);
            Assert.InRange(board.ChainAt(p), 0, LevelLayout.MaxChainHp);
        }

        Assert.False(new MatchFinder(board).AnyMatch(), $"unresolved match: {context}");
        Assert.NotEmpty(game.FindLegalMoves());
    }

    /// <summary>
    /// An empty cell is legitimate only when nothing can reach it: blocked from above by a box or chained
    /// tile (or a spawner-less top), and no resting movable tile on an upper diagonal.
    /// </summary>
    private static bool IsSealed(Board board, Position cell)
    {
        for (int y = cell.Y - 1; y >= 0; y--)
        {
            var above = P(cell.X, y);
            if (!board.Shape.IsPlayable(above) || board.TileAt(above).IsEmpty)
            {
                continue;
            }

            if (CanFall(board, above))
            {
                return false;
            }

            return !CanSlideInto(board, cell);
        }

        return !board.Shape.HasSpawner(cell.X) && !CanSlideInto(board, cell);
    }

    private static bool CanSlideInto(Board board, Position cell) =>
        new[] { P(cell.X - 1, cell.Y - 1), P(cell.X + 1, cell.Y - 1) }.Any(source =>
            board.Shape.IsPlayable(source) && CanFall(board, source) && IsResting(board, source));

    private static bool CanFall(Board board, Position p) => board.ChainAt(p) == 0 && board.TileAt(p).Kind.IsMovable();

    private static bool IsResting(Board board, Position p)
    {
        for (int y = p.Y + 1; y < board.Height; y++)
        {
            if (board.Shape.IsPlayable(P(p.X, y)))
            {
                return !board.TileAt(P(p.X, y)).IsEmpty;
            }
        }

        return true;
    }

    private static List<ulong> PlayFirstMoves(LevelDefinition level, ulong seed, int moves)
    {
        Game game = Game.Start(level, seed, new GameOptions { MoveLimitOverride = moves + 1 });
        var hashes = new List<ulong> { game.Board.ComputeHash() };
        for (int i = 0; i < moves && game.Status == GameStatus.Playing; i++)
        {
            game.TryPlay(game.FindLegalMoves()[0]);
            hashes.Add(game.Board.ComputeHash());
        }

        return hashes;
    }

    private static int[] JellyLayers(Board board) =>
        AllPositions(board).Select(board.JellyAt).ToArray();

    private static IEnumerable<Position> AllPositions(Board board) =>
        from y in Enumerable.Range(0, board.Height)
        from x in Enumerable.Range(0, board.Width)
        select P(x, y);
}
