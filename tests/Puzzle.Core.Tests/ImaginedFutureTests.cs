using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Resolution;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// <see cref="Game.CreateImagined"/>: a sampled future for a fair bot. It may use everything a player can know,
/// and nothing of the live game's random streams.
/// </summary>
public class ImaginedFutureTests
{
    [Fact]
    public void Imagined_copy_starts_from_the_present_state()
    {
        Game live = Game.Start(GameTests.ObstacleLevel(), seed: 7);
        PlayFirstLegalMoves(live, 3);

        Game imagined = live.CreateImagined(seed: 99);

        Assert.Equal(live.Board.ComputeHash(), imagined.Board.ComputeHash());
        Assert.Equal(GoalsDone(live), GoalsDone(imagined));
        Assert.Equal(live.MovesUsed, imagined.MovesUsed);
        Assert.Equal(live.Status, imagined.Status);
        Assert.Equal(live.TotalStats.GemsCleared, imagined.TotalStats.GemsCleared);
        Assert.Same(PlayStats.None, imagined.LastMove);
    }

    [Fact]
    public void Imagined_copy_played_forward_leaves_the_live_game_unchanged()
    {
        Game live = Game.Start(GameTests.ObstacleLevel(), seed: 7);
        PlayFirstLegalMoves(live, 3);
        ulong hash = live.Board.ComputeHash();
        int[] goals = GoalsDone(live);
        int movesUsed = live.MovesUsed;
        GameStatus status = live.Status;

        Game imagined = live.CreateImagined(seed: 99);
        PlayFirstLegalMoves(imagined, 20);

        Assert.NotEqual(hash, imagined.Board.ComputeHash());
        Assert.Equal(hash, live.Board.ComputeHash());
        Assert.Equal(goals, GoalsDone(live));
        Assert.Equal(movesUsed, live.MovesUsed);
        Assert.Equal(status, live.Status);
    }

    [Fact]
    public void Thinking_on_an_imagined_copy_does_not_advance_the_live_future()
    {
        Game live = Game.Start(GameTests.ObstacleLevel(), seed: 7);
        PlayFirstLegalMoves(live, 3);
        Game control = live.CreateReplica();

        PlayFirstLegalMoves(live.CreateImagined(seed: 99), 20);

        // Same refills, effect choices and shuffles as the untouched replica: thinking moved no live stream,
        // dueness weight or item count.
        Assert.Equal(PlayFirstLegalMoves(control, 20), PlayFirstLegalMoves(live, 20));
    }

    [Fact]
    public void Same_seed_twice_imagines_the_same_future()
    {
        Game live = Game.Start(GameTests.ObstacleLevel(), seed: 7);

        List<ulong> first = PlayFirstLegalMoves(live.CreateImagined(seed: 99), 15);
        List<ulong> second = PlayFirstLegalMoves(live.CreateImagined(seed: 99), 15);

        Assert.Equal(15, first.Count);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_seeds_imagine_different_refills_within_a_few_moves()
    {
        Game live = Game.Start(GameTests.OpenLevel(), seed: 7);
        Game first = live.CreateImagined(seed: 99);
        Game second = live.CreateImagined(seed: 100);

        bool diverged = false;
        for (int i = 0; i < 5 && !diverged; i++)
        {
            Move move = first.FindLegalMoves()[0];
            Assert.True(first.TryPlay(move));
            Assert.True(second.TryPlay(move));
            diverged = first.Board.ComputeHash() != second.Board.ComputeHash();
        }

        Assert.True(diverged, "different imagination seeds must bring different refills");
    }

    [Fact]
    public void Imagined_future_depends_on_the_board_and_not_on_the_live_play_seed()
    {
        // Small and many-colored, so 60 moves include specials and shuffles: all three random streams get used.
        LevelDefinition level = Level(Blank(6, 6), colors: 8);
        Game liveA = Game.Start(level, AttemptSeeds.WithFixedBoard(5, 100));
        Game liveB = Game.Start(level, AttemptSeeds.WithFixedBoard(5, 200));
        Game imaginedA = liveA.CreateImagined(seed: 99);
        Game imaginedB = liveB.CreateImagined(seed: 99);

        List<ulong> futureA = PlayFirstLegalMoves(imaginedA, 60);
        List<ulong> futureB = PlayFirstLegalMoves(imaginedB, 60);
        List<ulong> replicaA = PlayFirstLegalMoves(liveA.CreateReplica(), 10);
        List<ulong> replicaB = PlayFirstLegalMoves(liveB.CreateReplica(), 10);

        Assert.True(imaginedA.TotalStats.SpecialsActivated > 0 && imaginedA.TotalStats.Shuffles > 0, "every stream must be exercised");
        Assert.Equal(futureA, futureB);
        Assert.NotEqual(replicaA, replicaB); // replicas know each live future, and those differ
    }

    [Fact]
    public void Imagined_copy_of_a_game_on_its_last_move_keeps_playing()
    {
        Game live = Game.Start(GameTests.OpenLevel(), seed: 7, new GameOptions { MoveLimitOverride = 1 });
        Game imagined = live.CreateImagined(seed: 99);
        Move move = live.FindLegalMoves()[0];

        Assert.True(live.TryPlay(move));
        Assert.True(imagined.TryPlay(move));

        Assert.Equal(GameStatus.LostOutOfMoves, live.Status);
        Assert.Equal(GameStatus.Playing, imagined.Status);
        Game reply = imagined.CreateLookahead(seed: 5);
        Assert.True(reply.TryPlay(reply.FindLegalMoves()[0]));

        PlayFirstLegalMoves(imagined, 10);
        Assert.NotEqual(GameStatus.LostOutOfMoves, imagined.Status);
    }

    [Fact]
    public void Imagined_refills_never_exceed_the_level_item_total()
    {
        LevelDefinition level = new LevelDefinition(
            new LevelId(3),
            DifficultyTier.Normal,
            LevelLayout.Parse(new LayoutLayers { Cells = Blank(8, 8, '#') }),
            new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 999) },
            moveLimit: 30,
            new SpawnRules
            {
                Palette = GemColors.First(5),
                Items = new ItemSpawnRules { Total = 2, MaxOnBoard = 10, ChancePercent = 100 },
            });
        int itemTotal = level.Layout.PresetItemCount + level.Spawn.Items.Total;
        Game live = Game.Start(level, seed: 7);
        PlayFirstLegalMoves(live, 1);
        Assert.Equal(itemTotal, ItemsSpawned(live)); // all items are out: only a forgotten count could spawn more

        Game imagined = live.CreateImagined(seed: 99);
        for (int i = 0; i < 40 && imagined.Status == GameStatus.Playing; i++)
        {
            Assert.True(imagined.TryPlay(imagined.FindLegalMoves()[0]));
            Assert.InRange(ItemsSpawned(imagined), 0, itemTotal);
        }
    }

    [Fact]
    public void Imagined_refills_are_real_gems_of_the_level_palette()
    {
        LevelDefinition level = GameTests.OpenLevel();
        Game imagined = Game.Start(level, seed: 7).CreateImagined(seed: 99);

        for (int i = 0; i < 40 && imagined.Status == GameStatus.Playing; i++)
        {
            Assert.True(imagined.TryPlay(imagined.FindLegalMoves()[0]));
            List<Tile> tiles = Tiles(imagined.Board);
            Assert.DoesNotContain(tiles, tile => tile.Kind == TileKind.Hidden);
            Assert.All(tiles.Where(tile => tile.IsGem), tile => Assert.Contains(tile.Color, level.Spawn.Palette));
        }
    }

    [Fact]
    public void Imagined_copy_records_no_events()
    {
        Game live = Game.Start(GameTests.OpenLevel(), seed: 7, new GameOptions { RecordEvents = true });
        Game imagined = live.CreateImagined(seed: 99);
        Move move = live.FindLegalMoves()[0];

        Assert.True(live.TryPlay(move));
        Assert.True(imagined.TryPlay(move));

        Assert.NotEmpty(live.LastEvents);
        Assert.Empty(imagined.LastEvents);
    }

    [Fact]
    public void Imagining_works_from_a_replica_and_is_refused_from_a_lookahead()
    {
        Game live = Game.Start(GameTests.OpenLevel(), seed: 7);

        Assert.Throws<InvalidOperationException>(() => live.CreateLookahead(seed: 1).CreateImagined(seed: 99));
        Assert.Equal(
            PlayFirstLegalMoves(live.CreateImagined(seed: 99), 10),
            PlayFirstLegalMoves(live.CreateReplica().CreateImagined(seed: 99), 10));
    }

    [Fact]
    public void Reseeded_director_remembers_the_dueness_weights_without_sharing_them()
    {
        // Strong smoothing, so the weights a director has built up visibly steer its next draws.
        var rules = new SpawnRules { Palette = GemColors.First(5), SmoothingPermille = 300 };
        Board board = BoardOf(".");
        var context = new SpawnContext(
            board, new MatchFinder(board), new GoalTracker(new GoalSpec[] { new GoalSpec.BreakBoxes(1) }), new ItemLedger(), 0);
        SpawnDirector played = SpawnDirector.Create(rules, assistPercent: 0, new Rng(1));
        Draw(played, context, 30);

        List<Tile> reseeded = Draw(played.Reseed(new Rng(5)), context, 30);
        List<Tile> reseededAgain = Draw(played.Reseed(new Rng(5)), context, 30);
        List<Tile> fresh = Draw(SpawnDirector.Create(rules, assistPercent: 0, new Rng(5)), context, 30);

        Assert.Equal(reseeded, reseededAgain);
        Assert.NotEqual(fresh, reseeded);
    }

    /// <summary>
    /// Plays the first legal move up to <paramref name="count"/> times (fewer if the game ends) and returns the
    /// board hash after each move. Two equal games give equal lists; once they differ, so do the lists.
    /// </summary>
    private static List<ulong> PlayFirstLegalMoves(Game game, int count)
    {
        var hashes = new List<ulong>();
        for (int i = 0; i < count && game.Status == GameStatus.Playing; i++)
        {
            Assert.True(game.TryPlay(game.FindLegalMoves()[0]));
            hashes.Add(game.Board.ComputeHash());
        }

        return hashes;
    }

    private static int[] GoalsDone(Game game) =>
        Enumerable.Range(0, game.Goals.Count).Select(i => game.Goals[i].Done).ToArray();

    /// <summary>Items are indestructible, so every item ever spawned is either on the board or collected.</summary>
    private static int ItemsSpawned(Game game) =>
        Tiles(game.Board).Count(tile => tile.Kind == TileKind.Item) + game.TotalStats.ItemsCollected;

    private static List<Tile> Tiles(Board board) =>
        (from y in Enumerable.Range(0, board.Height)
         from x in Enumerable.Range(0, board.Width)
         select board.TileAt(P(x, y))).ToList();

    private static List<Tile> Draw(ITileSource source, SpawnContext context, int count)
    {
        var tiles = new List<Tile>();
        for (int i = 0; i < count; i++)
        {
            tiles.Add(source.Next(context));
        }

        return tiles;
    }
}
