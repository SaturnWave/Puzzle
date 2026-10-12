using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Engine.Resolution;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// Boosters and the "+5 moves" offer (docs/GAME_DESIGN.md §18 and §19): what the player may do besides moving, none
/// of which costs a move, and none of which a bot ever does.
/// </summary>
public class BoosterTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The filler board with three purple gems one swap from a match in the bottom left corner, so it has a move.</summary>
    private static string[] PlayableBoard() => Filler(7, 7).With(0, 6, 'p').With(1, 6, 'p').With(2, 5, 'p');

    private static IEnumerable<Position> Cells(Board board) =>
        from y in Enumerable.Range(0, board.Height)
        from x in Enumerable.Range(0, board.Width)
        select P(x, y);

    /// <summary>What the strike itself destroyed, before anything fell.</summary>
    private static List<GameEvent.TileDestroyed> DestroyedByTheStrike(Game game) =>
        game.LastEvents.TakeWhile(e => e is not GameEvent.RoundStarted).OfType<GameEvent.TileDestroyed>().ToList();

    [Fact]
    public void Hitting_a_cell_destroys_what_lies_on_it_and_costs_no_move()
    {
        Game game = Start(Level(PlayableBoard()));
        Tile struck = game.Board.TileAt(P(3, 3));
        var booster = new BoosterUse.HitCell(P(3, 3));

        Assert.True(game.TryUseBooster(booster));

        Assert.Equal(0, game.MovesUsed);
        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Equal(new GameEvent.BoosterUsed(booster), game.LastEvents[0]);
        Assert.Equal(new GameEvent.TileDestroyed(P(3, 3), struck), Assert.Single(DestroyedByTheStrike(game)));
        Assert.True(game.LastMove.GemsCleared >= 1);
        Assert.False(game.Board.TileAt(P(3, 3)).IsEmpty);
    }

    [Fact]
    public void Hitting_a_row_hits_every_cell_of_it_once()
    {
        string[] tiles = PlayableBoard().With(5, 3, '2');
        string[] chains = Blank(7, 7).With(1, 3, '1');
        Game game = Start(Level(tiles, chains: chains));

        Assert.True(game.TryUseBooster(new BoosterUse.HitRow(P(3, 3))));

        List<GameEvent.TileDestroyed> destroyed = DestroyedByTheStrike(game);
        Assert.Equal(5, destroyed.Count);
        Assert.All(destroyed, e => Assert.Equal(3, e.At.Y));
        Assert.Contains(new GameEvent.BoxDamaged(P(5, 3), 1), game.LastEvents);
        Assert.Contains(new GameEvent.ChainDamaged(P(1, 3), 0), game.LastEvents);
        Assert.Equal(1, game.LastMove.BoxHpRemoved);
        Assert.Equal(0, game.MovesUsed);
    }

    [Fact]
    public void Hitting_a_column_reaches_from_the_top_of_the_board_to_the_bottom()
    {
        Game game = Start(Level(PlayableBoard()));

        Assert.True(game.TryUseBooster(new BoosterUse.HitColumn(P(4, 6))));

        List<GameEvent.TileDestroyed> destroyed = DestroyedByTheStrike(game);
        Assert.Equal(7, destroyed.Count);
        Assert.All(destroyed, e => Assert.Equal(4, e.At.X));
    }

    [Fact]
    public void A_special_a_booster_hits_fires()
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'H')));

        Assert.True(game.TryUseBooster(new BoosterUse.HitCell(P(3, 3))));

        Assert.Contains(new GameEvent.EffectTriggered(P(3, 3), EffectKind.ClearRow, 0), game.LastEvents);
        Assert.Equal(6, DestroyedByTheStrike(game).Count(e => e.Tile.IsGem && e.At.Y == 3));
    }

    [Fact]
    public void A_strike_with_nothing_to_hit_is_rejected_and_changes_nothing()
    {
        string[] cells = Blank(7, 7, '#').With(6, 0, '.');
        Game game = Start(Level(PlayableBoard().With(3, 0, 'I').With(6, 0, '.').With(0, 0, 'H'), cells: cells));
        Assert.True(game.TryPlay(Move.Tap(P(0, 0))));
        Assert.Equal(TileKind.Item, game.Board.TileAt(P(3, 0)).Kind);
        ulong before = game.Board.ComputeHash();
        IReadOnlyList<GameEvent> eventsOfTheMove = game.LastEvents;
        PlayStats statsOfTheMove = game.LastMove;

        Assert.False(game.TryUseBooster(new BoosterUse.HitCell(P(3, 0))), "an item");
        Assert.False(game.TryUseBooster(new BoosterUse.HitCell(P(6, 0))), "a gap in the board");
        Assert.False(game.TryUseBooster(new BoosterUse.HitRow(P(6, 0))), "a row aimed at through a gap");
        Assert.False(game.TryUseBooster(new BoosterUse.HitColumn(P(9, 9))), "outside the board");

        Assert.Equal(before, game.Board.ComputeHash());
        Assert.Empty(game.BoostersUsed);
        Assert.NotEmpty(eventsOfTheMove);
        Assert.Same(eventsOfTheMove, game.LastEvents);
        Assert.Same(statsOfTheMove, game.LastMove);
        Assert.Equal(1, game.MovesUsed);
    }

    [Fact]
    public void A_booster_can_win_the_level_and_a_decided_game_takes_none()
    {
        string[] jelly = Blank(7, 7).With(3, 3, '1');
        Game game = Start(Level(PlayableBoard(), jelly: jelly, goals: new GoalSpec[] { new GoalSpec.ClearJelly(1) }));

        Assert.True(game.TryUseBooster(new BoosterUse.HitCell(P(3, 3))));

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(0, game.MovesUsed);
        Assert.Throws<InvalidOperationException>(() => game.TryUseBooster(new BoosterUse.Shuffle()));
    }

    [Fact]
    public void Shuffling_rearranges_the_free_gems_and_leaves_everything_else_where_it_was()
    {
        string[] tiles = PlayableBoard().With(1, 1, '2').With(3, 3, 'X');
        string[] chains = Blank(7, 7).With(5, 5, '1');
        Game game = Start(Level(tiles, chains: chains));
        Board board = game.Board;
        ulong before = board.ComputeHash();
        Tile chained = board.TileAt(P(5, 5));
        List<GemColor> colorsBefore = FreeGemColors(board);

        Assert.True(game.TryUseBooster(new BoosterUse.Shuffle()));

        Assert.Equal(new GameEvent[] { new GameEvent.BoosterUsed(new BoosterUse.Shuffle()), new GameEvent.Shuffled() }, game.LastEvents);
        Assert.NotEqual(before, board.ComputeHash());
        Assert.Equal(colorsBefore, FreeGemColors(board));
        Assert.Equal(Tile.Box(2), board.TileAt(P(1, 1)));
        Assert.Equal(TileKind.Bomb, board.TileAt(P(3, 3)).Kind);
        Assert.Equal(chained, board.TileAt(P(5, 5)));
        Assert.False(new MatchFinder(board).AnyMatch());
        Assert.NotEmpty(game.FindLegalMoves());
        Assert.Equal(0, game.MovesUsed);
        Assert.Equal(1, game.TotalStats.Shuffles);
    }

    [Fact]
    public void A_shuffle_that_cannot_put_a_gem_anywhere_else_is_rejected_and_changes_nothing()
    {
        // One free gem among rockets: there is a move (any rocket), and nothing to rearrange.
        Game oneGem = Start(Level(Blank(3, 3, 'H').With(1, 1, 'r')));
        ulong before = oneGem.Board.ComputeHash();

        Assert.False(oneGem.TryUseBooster(new BoosterUse.Shuffle()));

        Assert.Equal(before, oneGem.Board.ComputeHash());
        Assert.Empty(oneGem.BoostersUsed);
        Assert.Equal(0, oneGem.TotalStats.Shuffles);

        // Two gems of one colour: every arrangement is the one on the board.
        Game twoAlike = Start(Level(Blank(3, 3, 'H').With(0, 0, 'r').With(2, 2, 'r')));
        Assert.False(twoAlike.TryUseBooster(new BoosterUse.Shuffle()));
    }

    [Fact]
    public void A_shuffle_that_is_accepted_always_moved_a_gem()
    {
        // Three free gems, two of them alike: a third of all orders are the order on the board.
        LevelDefinition level = Level(Blank(3, 3, 'H').With(0, 0, 'r').With(1, 1, 'g').With(2, 2, 'r'));
        int accepted = 0;
        for (ulong seed = 0; seed < 60; seed++)
        {
            Game game = Start(level, seed);
            ulong before = game.Board.ComputeHash();

            if (game.TryUseBooster(new BoosterUse.Shuffle()))
            {
                accepted++;
                Assert.NotEqual(before, game.Board.ComputeHash());
            }
            else
            {
                Assert.Equal(before, game.Board.ComputeHash());
            }
        }

        Assert.True(accepted > 0);
    }

    [Fact]
    public void Boosters_are_named_in_the_attempts_record_in_the_order_they_were_used()
    {
        var options = new GameOptions { StartBoosters = new[] { StartBooster.ExtraMoves } };
        Game game = Game.Start(Level(PlayableBoard()), 1, options);

        Assert.True(game.TryUseBooster(new BoosterUse.HitRow(P(3, 3))));
        Assert.True(game.TryUseBooster(new BoosterUse.HitCell(P(3, 3))));

        string[] expected = { "start-extra-moves", "hit-row", "hit-cell" };
        Assert.Equal(expected, game.BoostersUsed);
        Assert.Equal(expected, AttemptOutcome.Of(game, Noon).BoostersUsed);
    }

    [Fact]
    public void Every_booster_has_a_record_name_of_its_own()
    {
        string[] names =
        {
            BoosterNames.Of(StartBooster.RocketAndBomb), BoosterNames.Of(StartBooster.ColorBomb), BoosterNames.Of(StartBooster.ExtraMoves),
            BoosterNames.Of(new BoosterUse.HitCell(P(0, 0))), BoosterNames.Of(new BoosterUse.HitRow(P(0, 0))),
            BoosterNames.Of(new BoosterUse.HitColumn(P(0, 0))), BoosterNames.Of(new BoosterUse.Shuffle()),
        };

        // These are written into attempt records: a name that changes makes old records name another booster.
        Assert.Equal(
            new[] { "start-rocket-and-bomb", "start-color-bomb", "start-extra-moves", "hit-cell", "hit-row", "hit-column", "shuffle" },
            names);
    }

    [Fact]
    public void A_copy_of_the_game_carries_its_boosters_and_its_longer_limit()
    {
        var options = new GameOptions { StartBoosters = new[] { StartBooster.ExtraMoves } };
        Game game = Game.Start(Level(PlayableBoard()), 1, options);
        Assert.True(game.TryUseBooster(new BoosterUse.HitCell(P(3, 3))));

        foreach (Game copy in new[] { game.CreateReplica(), game.CreateLookahead(9), game.CreateImagined(9) })
        {
            Assert.Equal(game.MoveLimit, copy.MoveLimit);
            Assert.Equal(game.BoostersUsed, copy.BoostersUsed);
            Assert.Equal(game.Board.ComputeHash(), copy.Board.ComputeHash());

            // What the copy does next is its own.
            Assert.True(copy.TryUseBooster(new BoosterUse.HitCell(P(0, 0))));
            Assert.Equal(new[] { "start-extra-moves", "hit-cell" }, game.BoostersUsed);
        }
    }

    [Fact]
    public void A_copy_of_the_game_knows_that_the_extra_moves_were_taken_and_that_the_finale_ran()
    {
        Game outOfMoves = Start(Level(Filler(7, 7).With(3, 3, 'H').With(3, 5, 'V'), moves: 1));
        Assert.True(outOfMoves.TryPlay(Move.Tap(P(3, 3))));
        Assert.True(outOfMoves.TryTakeExtraMoves());

        Game replica = outOfMoves.CreateReplica();

        Assert.True(replica.ExtraMovesTaken);
        Assert.Equal(outOfMoves.MoveLimit, replica.MoveLimit);
        Assert.Equal(GameStatus.Playing, replica.Status);

        string[] jelly = Blank(7, 7).With(3, 3, '1');
        Game won = Start(Level(PlayableBoard(), jelly: jelly, goals: new GoalSpec[] { new GoalSpec.ClearJelly(1) }));
        Assert.True(won.TryUseBooster(new BoosterUse.HitCell(P(3, 3))));
        won.PlayFinale();

        Assert.True(won.CreateReplica().FinalePlayed);
        Assert.Throws<InvalidOperationException>(() => won.CreateReplica().PlayFinale());
    }

    [Fact]
    public void Rocket_and_bomb_start_in_place_of_two_gems_and_the_rest_of_the_board_is_what_it_would_have_been()
    {
        LevelDefinition level = GameTests.OpenLevel();
        Game plain = Start(level, seed: 5);
        Game boosted = Game.Start(level, 5, new GameOptions { RecordEvents = true, StartBoosters = new[] { StartBooster.RocketAndBomb } });

        List<Position> changed = Cells(plain.Board).Where(p => plain.Board.TileAt(p) != boosted.Board.TileAt(p)).ToList();

        Assert.Equal(2, changed.Count);
        Assert.All(changed, p => Assert.True(plain.Board.TileAt(p).IsGem));
        Assert.Single(changed, p => boosted.Board.TileAt(p).Kind.IsRocket());
        Assert.Single(changed, p => boosted.Board.TileAt(p).Kind == TileKind.Bomb);
        Assert.Equal(
            changed.Select(p => new GameEvent.SpecialCreated(p, boosted.Board.TileAt(p).Kind)).ToHashSet<GameEvent>(),
            boosted.LastEvents.ToHashSet());
        Assert.Equal(new[] { "start-rocket-and-bomb" }, boosted.BoostersUsed);
        Assert.Equal(plain.MoveLimit, boosted.MoveLimit);
    }

    [Fact]
    public void A_colour_bomb_starts_in_place_of_one_gem()
    {
        LevelDefinition level = GameTests.OpenLevel();
        Game plain = Start(level, seed: 5);
        Game boosted = Game.Start(level, 5, new GameOptions { StartBoosters = new[] { StartBooster.ColorBomb } });

        Position changed = Assert.Single(Cells(plain.Board), p => plain.Board.TileAt(p) != boosted.Board.TileAt(p));

        Assert.Equal(TileKind.ColorBomb, boosted.Board.TileAt(changed).Kind);
    }

    [Fact]
    public void Extra_moves_lengthen_the_limit_by_three_and_change_nothing_else_about_the_game()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        Game plain = Game.Start(level, 11);
        Game boosted = Game.Start(level, 11, new GameOptions { StartBoosters = new[] { StartBooster.ExtraMoves } });
        var plainRng = new Rng(4);
        var boostedRng = new Rng(4);

        Assert.Equal(level.MoveLimit + 3, boosted.MoveLimit);
        Assert.Equal(plain.Board.ComputeHash(), boosted.Board.ComputeHash());
        for (int move = 0; move < 12; move++)
        {
            IReadOnlyList<Move> plainMoves = plain.FindLegalMoves();
            IReadOnlyList<Move> boostedMoves = boosted.FindLegalMoves();
            Assert.True(plain.TryPlay(plainMoves[plainRng.NextInt(plainMoves.Count)]));
            Assert.True(boosted.TryPlay(boostedMoves[boostedRng.NextInt(boostedMoves.Count)]));

            Assert.Equal(plain.Board.ComputeHash(), boosted.Board.ComputeHash());
        }
    }

    [Fact]
    public void The_same_start_boosters_give_the_same_start_in_whatever_order_they_are_named()
    {
        LevelDefinition level = GameTests.OpenLevel();
        Game one = Game.Start(level, 8, new GameOptions { StartBoosters = new[] { StartBooster.ColorBomb, StartBooster.RocketAndBomb } });
        Game other = Game.Start(level, 8, new GameOptions { StartBoosters = new[] { StartBooster.RocketAndBomb, StartBooster.ColorBomb } });

        Assert.Equal(one.Board.ComputeHash(), other.Board.ComputeHash());
        Assert.Equal(new[] { "start-rocket-and-bomb", "start-color-bomb" }, one.BoostersUsed);
        Assert.Equal(one.BoostersUsed, other.BoostersUsed);
    }

    [Fact]
    public void A_start_booster_is_taken_once_or_not_at_all()
    {
        Assert.Throws<ArgumentException>(() => new GameOptions { StartBoosters = new[] { StartBooster.ColorBomb, StartBooster.ColorBomb } });
        Assert.Throws<ArgumentException>(() => new GameOptions { StartBoosters = new[] { (StartBooster)99 } });
        Assert.Empty(GameOptions.Default.StartBoosters);
    }

    [Fact]
    public void A_board_without_a_free_gem_cannot_take_a_special()
    {
        // Every cell holds a special already; the level itself is fine.
        LevelDefinition level = Level(Blank(3, 3, 'H'));

        Assert.Throws<LevelInitializationException>(() =>
            Game.Start(level, 1, new GameOptions { StartBoosters = new[] { StartBooster.ColorBomb } }));
    }

    [Fact]
    public void Taking_the_extra_moves_lets_a_game_that_ran_out_go_on_for_five_more()
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'H').With(3, 5, 'V'), moves: 1));
        Assert.True(game.TryPlay(Move.Tap(P(3, 3))));
        Assert.Equal(GameStatus.LostOutOfMoves, game.Status);

        Assert.True(game.TryTakeExtraMoves());

        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Equal(1 + ExtraMovesOffer.Moves, game.MoveLimit);
        Assert.Equal(ExtraMovesOffer.Moves, game.MovesLeft);
        Assert.True(game.ExtraMovesTaken);
        Assert.True(game.TryPlay(Move.Tap(P(3, 5))));
        Assert.Equal(2, game.MovesUsed);
        Assert.True(AttemptOutcome.Of(game, Noon).ExtraMovesBought);
    }

    [Fact]
    public void The_extra_moves_are_offered_once_and_only_to_a_game_that_ran_out_of_moves()
    {
        Game fresh = Start(Level(Filler(7, 7).With(3, 3, 'H')));
        Assert.False(fresh.TryTakeExtraMoves());
        Assert.False(fresh.ExtraMovesTaken);
        Assert.False(AttemptOutcome.Of(fresh, Noon).ExtraMovesBought);

        Game game = Start(Level(Filler(7, 7).With(3, 3, 'H'), moves: 1));
        Assert.True(game.TryPlay(Move.Tap(P(3, 3))));
        Assert.True(game.TryTakeExtraMoves());
        var rng = new Rng(2);
        while (game.Status == GameStatus.Playing)
        {
            IReadOnlyList<Move> moves = game.FindLegalMoves();
            Assert.True(game.TryPlay(moves[rng.NextInt(moves.Count)]));
        }

        Assert.Equal(GameStatus.LostOutOfMoves, game.Status);
        Assert.Equal(1 + ExtraMovesOffer.Moves, game.MovesUsed);
        Assert.False(game.TryTakeExtraMoves());
    }

    [Fact]
    public void A_board_that_no_shuffle_can_bring_back_is_not_sold_extra_moves()
    {
        // Boxes all around one rocket: after it has fired there is nothing to move and nothing to shuffle.
        Game game = Start(Level(Blank(3, 3, '3').With(1, 1, 'H'), moves: 1));
        Assert.True(game.TryPlay(Move.Tap(P(1, 1))));
        Assert.Equal(GameStatus.LostOutOfMoves, game.Status);
        ulong before = game.Board.ComputeHash();

        Assert.False(game.TryTakeExtraMoves());

        Assert.Equal(GameStatus.LostOutOfMoves, game.Status);
        Assert.False(game.ExtraMovesTaken);
        Assert.Equal(1, game.MoveLimit);
        Assert.Equal(before, game.Board.ComputeHash());
        Assert.False(AttemptOutcome.Of(game, Noon).ExtraMovesBought);
    }

    private static List<GemColor> FreeGemColors(Board board) =>
        Cells(board)
            .Where(p => board.TileAt(p).IsGem && board.ChainAt(p) == 0)
            .Select(p => board.TileAt(p).Color)
            .OrderBy(color => color)
            .ToList();
}
