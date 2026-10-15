using System.IO;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestBoards;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

/// <summary>
/// The attempt's ordered actions (docs/GAME_DESIGN.md §18): the engine keeps what the player did when asked to, and
/// changes nothing about the game for it; an attempt with every kind of booster, a refused shuffle and the extra moves
/// is played again from its record to the same board, status, moves used and goals.
/// </summary>
public class AttemptActionTests
{
    private static readonly GameOptions Recording = new() { RecordActions = true };

    [Fact]
    public void Recording_the_actions_changes_nothing_about_the_game_and_keeps_every_move_in_order()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        Game plain = Game.Start(level, 11);
        Game recorded = Game.Start(level, 11, Recording);
        var rng = new Rng(4);
        var played = new List<Move>();

        while (plain.Status == GameStatus.Playing)
        {
            IReadOnlyList<Move> moves = plain.FindLegalMoves();
            Move move = moves[rng.NextInt(moves.Count)];
            Assert.True(plain.TryPlay(move));
            Assert.True(recorded.TryPlay(move));
            played.Add(move);

            Assert.Equal(plain.Board.ComputeHash(), recorded.Board.ComputeHash());
            Assert.Equal(plain.Status, recorded.Status);
        }

        Assert.Empty(plain.Actions);
        Assert.Equal(played.Select(move => new AttemptAction.MovePlayed(move)), recorded.Actions);
    }

    [Fact]
    public void An_illegal_move_and_a_strike_with_nothing_to_hit_are_no_actions()
    {
        string[] cells = Blank(7, 7, '#').With(6, 0, '.');
        Game game = Game.Start(Level(Filler(7, 7).With(0, 6, 'p').With(1, 6, 'p').With(2, 5, 'p').With(6, 0, '.'), cells: cells), 1, Recording);

        Assert.False(game.TryPlay(Move.Swap(P(3, 3), P(3, 4))));
        Assert.False(game.TryUseBooster(new BoosterUse.HitCell(P(6, 0))));

        Assert.Empty(game.Actions);
    }

    [Fact]
    public void A_copy_of_the_game_keeps_no_actions_and_adds_none_to_the_game_it_was_copied_from()
    {
        Game game = Game.Start(GameTests.OpenLevel(), 3, Recording);
        Move first = game.FindLegalMoves()[0];
        Assert.True(game.TryPlay(first));

        foreach (Game copy in new[] { game.CreateReplica(), game.CreateLookahead(9), game.CreateImagined(9) })
        {
            Assert.True(copy.TryPlay(copy.FindLegalMoves()[0]));

            Assert.Empty(copy.Actions);
        }

        Assert.Equal(new AttemptAction[] { new AttemptAction.MovePlayed(first) }, game.Actions);
    }

    [Theory]
    [InlineData(ShuffleBoosterRule.RandomOrders, 5UL)]
    [InlineData(ShuffleBoosterRule.LaidWithoutAMatch, 6UL)]
    public void An_attempt_with_every_booster_and_the_extra_moves_is_replayed_from_its_record_to_the_same_end(ShuffleBoosterRule rule, ulong playSeed)
    {
        LevelDefinition level = GameTests.OpenLevel().WithMoveLimit(8);
        StartBooster[] startBoosters = { StartBooster.RocketAndBomb, StartBooster.ColorBomb, StartBooster.ExtraMoves };
        AttemptSeeds seeds = AttemptSeeds.WithFixedBoard(7, 21);
        Game game = Game.Start(level, seeds, new GameOptions { StartBoosters = startBoosters, RecordActions = true, ShuffleBoosterRule = rule });
        var rng = new Rng(playSeed);

        Assert.True(game.TryUseBooster(new BoosterUse.HitCell(P(3, 3))));
        PlayRandomly(game, rng, moves: 1);
        Assert.True(game.TryUseBooster(new BoosterUse.HitRow(P(0, 5))));
        Assert.True(game.TryUseBooster(new BoosterUse.HitColumn(P(9, 0))));
        PlayRandomly(game, rng, moves: 1);
        int refusals = AskForAShuffleUntilOneIsAccepted(game);
        PlayRandomly(game, rng, moves: int.MaxValue);
        Assert.Equal(GameStatus.LostOutOfMoves, game.Status);
        Assert.True(game.TryTakeExtraMoves());
        PlayRandomly(game, rng, moves: int.MaxValue);

        AttemptRecord record = AttemptRecordJson.Parse(AttemptRecordJson.Serialize(RecordOf(game, seeds)));
        Game replayed = AttemptReplay.Of(record, level);

        string[] expected = new[] { "hit-cell", "move", "hit-row", "hit-column", "move" }.Concat(Enumerable.Repeat("shuffle-refused", refusals)).Append("shuffle").ToArray();
        Assert.Equal(expected, game.Actions.Take(expected.Length).Select(action => AttemptActionNames.Of(action)));
        Assert.Contains(new AttemptAction.ExtraMovesTaken(), game.Actions);
        Assert.Equal(rule, record.Start.ShuffleRule);
        AssertTheSameEnd(game, replayed);
    }

    [Fact]
    public void A_refused_shuffle_of_the_first_rule_drew_from_the_shuffle_stream_so_the_replay_asks_for_it_too()
    {
        // Seven rockets and two red gems: every order of the gems is the order on the board, and each try draws.
        LevelDefinition level = Level(Blank(3, 3, 'H').With(0, 0, 'r').With(2, 2, 'r'));
        AttemptSeeds seeds = AttemptSeeds.From(1);
        Game game = Game.Start(level, seeds, new GameOptions { RecordActions = true, ShuffleBoosterRule = ShuffleBoosterRule.RandomOrders });

        Assert.False(game.TryUseBooster(new BoosterUse.Shuffle()));
        Assert.True(game.TryPlay(Move.Tap(P(1, 1))));
        Assert.True(game.TryUseBooster(new BoosterUse.Shuffle()));

        AttemptAction[] expected =
        {
            new AttemptAction.ShuffleRefused(),
            new AttemptAction.MovePlayed(Move.Tap(P(1, 1))),
            new AttemptAction.BoosterUsed(new BoosterUse.Shuffle()),
        };
        Assert.Equal(expected, game.Actions);
        AssertTheSameEnd(game, AttemptReplay.Of(RecordOf(game, seeds), level));

        // Without the refusal the accepted shuffle draws other numbers and leaves another board.
        Game withoutTheRefusal = AttemptReplay.Play(level, seeds, Array.Empty<StartBooster>(), ShuffleBoosterRule.RandomOrders, expected.Skip(1).ToArray());
        Assert.NotEqual(game.Board.ComputeHash(), withoutTheRefusal.Board.ComputeHash());
    }

    [Fact]
    public void The_extra_moves_after_a_forced_shuffle_are_replayed_to_the_same_end()
    {
        // A small board whose last move may leave no move at all: the extra moves then begin with a shuffle.
        LevelDefinition level = Level(Blank(3, 3), moves: 1);
        (Game game, AttemptSeeds seeds) = FirstDeadAfterItsMove(level);
        ulong deadBoard = game.Board.ComputeHash();

        Assert.True(game.TryTakeExtraMoves());
        Assert.NotEqual(deadBoard, game.Board.ComputeHash());
        PlayRandomly(game, new Rng(8), moves: int.MaxValue);

        Assert.Equal(new AttemptAction.ExtraMovesTaken(), game.Actions[1]);
        AssertTheSameEnd(game, AttemptReplay.Of(RecordOf(game, seeds), level));
    }

    [Fact]
    public void Extra_moves_refused_for_a_board_no_shuffle_brings_back_are_kept_and_replayed()
    {
        // Boxes all around one rocket: after it has fired there is nothing to move and nothing to shuffle.
        LevelDefinition level = Level(Blank(3, 3, '3').With(1, 1, 'H'), moves: 1);
        AttemptSeeds seeds = AttemptSeeds.From(1);
        Game game = Game.Start(level, seeds, Recording);
        Assert.True(game.TryPlay(Move.Tap(P(1, 1))));

        Assert.False(game.TryTakeExtraMoves());
        Assert.False(game.TryTakeExtraMoves());

        Assert.Equal(new AttemptAction[] { new AttemptAction.MovePlayed(Move.Tap(P(1, 1))), new AttemptAction.ExtraMovesRefused(), new AttemptAction.ExtraMovesRefused() }, game.Actions);
        AssertTheSameEnd(game, AttemptReplay.Of(RecordOf(game, seeds), level));
    }

    [Fact]
    public void An_attempt_left_before_it_was_decided_is_replayed_to_the_game_as_it_was()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptSeeds seeds = AttemptSeeds.From(9);
        Game game = Game.Start(level, seeds, Recording);
        PlayRandomly(game, new Rng(2), moves: 4);

        AttemptRecord record = RecordOf(game, seeds);

        Assert.Equal(AttemptResult.Abandoned, record.Outcome.Result);
        AssertTheSameEnd(game, AttemptReplay.Of(record, level));
    }

    [Fact]
    public void A_record_is_replayed_only_on_its_own_level_and_only_with_its_actions()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptSeeds seeds = AttemptSeeds.From(9);
        Game game = Game.Start(level, seeds, Recording);
        PlayRandomly(game, new Rng(2), moves: 2);
        AttemptRecord record = RecordOf(game, seeds);
        var withoutActions = new AttemptRecord(record.Start, record.Outcome, lab: null);

        Assert.Throws<ArgumentException>(() => AttemptReplay.Of(withoutActions, level));
        Assert.Throws<ArgumentException>(() => AttemptReplay.Of(record, level.WithMoveLimit(level.MoveLimit + 1)));
    }

    [Fact]
    public void A_record_that_is_not_of_its_game_is_told_at_the_action_that_went_elsewhere()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptSeeds seeds = AttemptSeeds.From(9);
        Game game = Game.Start(level, seeds, Recording);
        PlayRandomly(game, new Rng(2), moves: 3);
        AttemptRecord record = RecordOf(game, seeds);
        // A swap beyond the board's edge: no game accepts it.
        AttemptAction[] withAnIllegalMove = record.Actions!.Take(1).Append(new AttemptAction.MovePlayed(Move.Swap(P(20, 20), P(20, 21)))).ToArray();
        var illegal = new AttemptRecord(record.Start, record.Outcome, lab: null, withAnIllegalMove);
        var endingElsewhere = new AttemptRecord(
            record.Start,
            new AttemptOutcome(record.Outcome.Result, record.Outcome.MovesUsed + 1, record.Outcome.Goals, false, Array.Empty<string>(), record.Outcome.EndedUtc),
            lab: null,
            record.Actions);

        Assert.Equal(3, Assert.Throws<AttemptReplayException>(() => AttemptReplay.Of(endingElsewhere, level)).ActionIndex);
        AttemptReplayException refused = Assert.Throws<AttemptReplayException>(() => AttemptReplay.Of(illegal, level));
        Assert.Equal(1, refused.ActionIndex);
    }

    [Fact]
    public void Every_action_is_spelled_with_a_name_of_its_own_and_read_back_from_it()
    {
        AttemptAction[] actions =
        {
            new AttemptAction.MovePlayed(Move.Swap(P(3, 4), P(3, 5))), new AttemptAction.MovePlayed(Move.Tap(P(0, 9))),
            new AttemptAction.BoosterUsed(new BoosterUse.HitCell(P(2, 3))), new AttemptAction.BoosterUsed(new BoosterUse.HitRow(P(0, 5))),
            new AttemptAction.BoosterUsed(new BoosterUse.HitColumn(P(4, 0))), new AttemptAction.BoosterUsed(new BoosterUse.Shuffle()),
            new AttemptAction.ExtraMovesTaken(), new AttemptAction.ShuffleRefused(), new AttemptAction.ExtraMovesRefused(),
        };

        // These are written into attempt records: a name that changes makes old records name another action.
        Assert.Equal(
            new[] { "move", "move", "hit-cell", "hit-row", "hit-column", "shuffle", "extra-moves", "shuffle-refused", "extra-moves-refused" },
            actions.Select(AttemptActionNames.Of));
        Assert.Equal(actions, actions.Select(action => AttemptActionNames.Named(AttemptActionNames.Of(action), AttemptActionNames.CellsOf(action))));
        Assert.Null(AttemptActionNames.Named("magic-wand", new[] { 1, 1 }));
    }

    [Theory]
    [InlineData("move", null)]
    [InlineData("move", new[] { 1, 1, 1 })]
    [InlineData("move", new[] { 1, 1, 3, 3 })]
    [InlineData("hit-row", null)]
    [InlineData("hit-cell", new[] { 1, 1, 1, 2 })]
    [InlineData("shuffle", new[] { 1, 1 })]
    [InlineData("extra-moves", new[] { 0, 0 })]
    public void A_known_action_aimed_at_cells_that_do_not_fit_it_is_refused(string name, int[]? cells)
    {
        Assert.Throws<ArgumentException>(() => AttemptActionNames.Named(name, cells));
    }

    [Fact]
    public void The_start_boosters_are_found_by_their_record_names_and_no_other_name_is_one()
    {
        foreach (StartBooster booster in Enum.GetValues<StartBooster>())
        {
            Assert.Equal(booster, BoosterNames.StartBoosterNamed(BoosterNames.Of(booster)));
        }

        Assert.Null(BoosterNames.StartBoosterNamed("hit-cell"));
        Assert.Null(BoosterNames.StartBoosterNamed("hammer"));
    }

    /// <summary>The record the released game would write of the game: the level's own move limit, the game's actions.</summary>
    internal static AttemptRecord RecordOf(Game game, AttemptSeeds seeds)
    {
        LevelDefinition level = game.Level;
        var start = new AttemptStart(level.Id, level.ContentVersion, level.MoveLimit, seeds, attempt: 0, Phone, "unity", Noon, game.ShuffleBoosterRule);
        return new AttemptRecord(start, AttemptOutcome.Of(game, Noon.AddMinutes(3)), lab: null, game.Actions);
    }

    /// <summary>Plays random legal moves until the game is no longer being played or the moves are played.</summary>
    internal static void PlayRandomly(Game game, Rng rng, int moves)
    {
        for (int played = 0; played < moves && game.Status == GameStatus.Playing; played++)
        {
            IReadOnlyList<Move> legal = game.FindLegalMoves();
            Assert.True(game.TryPlay(legal[rng.NextInt(legal.Count)]));
        }
    }

    internal static void AssertTheSameEnd(Game played, Game replayed)
    {
        Assert.Equal(played.Board.ComputeHash(), replayed.Board.ComputeHash());
        Assert.Equal(played.Status, replayed.Status);
        Assert.Equal(played.MovesUsed, replayed.MovesUsed);
        Assert.Equal(played.MoveLimit, replayed.MoveLimit);
        Assert.Equal(GoalsDone(played), GoalsDone(replayed));
        Assert.Equal(played.BoostersUsed, replayed.BoostersUsed);
        Assert.Equal(played.Actions, replayed.Actions);
    }

    /// <summary>
    /// Asks for the shuffle booster again and again, as a player may: under the first rule a rearrangement without a
    /// match is rare on a large board, and each try draws other numbers; the rule of today is never refused there.
    /// Returns how often it was refused first.
    /// </summary>
    private static int AskForAShuffleUntilOneIsAccepted(Game game)
    {
        int refusals = 0;
        while (!game.TryUseBooster(new BoosterUse.Shuffle()))
        {
            refusals++;
            Assert.True(refusals < 50, "fifty shuffles in a row were refused");
        }

        if (game.ShuffleBoosterRule == ShuffleBoosterRule.LaidWithoutAMatch)
        {
            Assert.Equal(0, refusals);
        }

        return refusals;
    }

    private static int[] GoalsDone(Game game) => Enumerable.Range(0, game.Goals.Count).Select(goal => game.Goals[goal].Done).ToArray();

    /// <summary>The first game of the level whose one move ran it out of moves on a board with no move left on it.</summary>
    private static (Game Game, AttemptSeeds Seeds) FirstDeadAfterItsMove(LevelDefinition level)
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            AttemptSeeds seeds = AttemptSeeds.From(seed);
            if (StartOrNothing(level, seeds) is not Game game)
            {
                continue; // a seed that fills no board of three by three with a move on it
            }

            Assert.True(game.TryPlay(game.FindLegalMoves()[0]));
            if (game.Status == GameStatus.LostOutOfMoves && game.FindLegalMoves().Count == 0)
            {
                return (game, seeds);
            }
        }

        throw new InvalidOperationException("No seed of 200 leaves the board without a move after its one move.");
    }

    private static Game? StartOrNothing(LevelDefinition level, AttemptSeeds seeds)
    {
        try
        {
            return Game.Start(level, seeds, Recording);
        }
        catch (LevelInitializationException)
        {
            return null;
        }
    }
}

/// <summary>
/// The action list in the attempt record's JSON line: absent in every line written before it existed, which still
/// reads; with it, the line survives its round trip and is pinned (tests/fixtures/attempt-records.actions.jsonl), so
/// the game's own writer can be held to the same lines.
/// </summary>
public class AttemptRecordJsonTests
{
    private static readonly DeviceId GamePhone = new("6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a");

    [Fact]
    public void Records_with_actions_are_written_exactly_as_the_pinned_lines()
    {
        string[] golden = File.ReadAllLines(FixturePath("attempt-records.actions.jsonl"));

        Assert.Equal(golden, ActionWireExamples().Select(AttemptRecordJson.Serialize));
        Assert.Equal(golden, golden.Select(line => AttemptRecordJson.Serialize(AttemptRecordJson.Parse(line))));
    }

    [Fact]
    public void A_record_with_actions_survives_its_json_line()
    {
        AttemptRecord record = ActionWireExamples()[0];

        AttemptRecord restored = AttemptRecordJson.Parse(AttemptRecordJson.Serialize(record));

        Assert.NotNull(restored.Actions);
        Assert.Equal(record.Actions, restored.Actions);
        Assert.Equal(record.Outcome.BoostersUsed, restored.Outcome.BoostersUsed);
        Assert.Equal(record.Start.ContentVersion, restored.Start.ContentVersion);
    }

    [Fact]
    public void A_line_written_before_the_actions_existed_still_reads_as_a_record_that_keeps_none()
    {
        string[] schemaOne = File.ReadAllLines(FixturePath("attempt-records.schema-1.jsonl"));

        AttemptRecord[] read = schemaOne.Select(AttemptRecordJson.Parse).ToArray();

        Assert.All(read, record => Assert.Null(record.Actions));
        Assert.All(schemaOne, line => Assert.DoesNotContain("\"actions\"", AttemptRecordJson.Serialize(AttemptRecordJson.Parse(line))));
    }

    [Fact]
    public void An_action_of_a_later_game_leaves_the_record_readable_without_its_actions()
    {
        string line = AttemptRecordJson.Serialize(ActionWireExamples()[1])
            .Replace("{\"act\":\"move\",\"at\":[5,5]}", "{\"act\":\"move\",\"at\":[5,5]},{\"act\":\"magic-wand\",\"at\":[1,2]}", StringComparison.Ordinal);

        AttemptRecord read = AttemptRecordJson.Parse(line);

        Assert.Null(read.Actions);
        Assert.Equal(AttemptResult.Abandoned, read.Outcome.Result);
    }

    [Theory]
    [InlineData("{\"act\":\"move\",\"at\":[5,5]}", "{\"act\":\"move\",\"at\":[5]}")]
    [InlineData("{\"act\":\"move\",\"at\":[5,5]}", "{\"act\":\"move\"}")]
    [InlineData("{\"act\":\"move\",\"at\":[5,5]}", "{\"at\":[5,5]}")]
    [InlineData("{\"act\":\"move\",\"at\":[5,5]}", "null")]
    [InlineData("\"actions\":[{\"act\":\"move\",\"at\":[5,5]}]", "\"actions\":{\"act\":\"move\"}")]
    public void A_malformed_action_is_a_format_error(string action, string replacement)
    {
        string line = AttemptRecordJson.Serialize(ActionWireExamples()[1]);
        Assert.Contains(action, line);

        Assert.Throws<AttemptRecordFormatException>(() => AttemptRecordJson.Parse(line.Replace(action, replacement, StringComparison.Ordinal)));
    }

    /// <summary>
    /// The records behind tests/fixtures/attempt-records.actions.jsonl, in order: a won attempt with every optional
    /// start field, a start booster, every booster of play, a refused shuffle, the extra moves refused once and then taken;
    /// an attempt left after one move, with no optional start field; a lost attempt with times between two seconds.
    /// </summary>
    internal static AttemptRecord[] ActionWireExamples()
    {
        var wonStart = new AttemptStart(new LevelId(48), new ContentVersion(2), 2, AttemptSeeds.WithFixedBoard(5, 1234), attempt: 2, GamePhone, "unity", Noon);
        var wonOutcome = new AttemptOutcome(
            AttemptResult.Won,
            3,
            new[] { new GoalOutcome(new GoalSpec.ClearJelly(12), done: 12) },
            extraMovesBought: true,
            boostersUsed: new[] { "start-color-bomb", "hit-cell", "hit-row", "hit-column", "shuffle" },
            Noon.AddMinutes(6));
        AttemptAction[] wonActions =
        {
            new AttemptAction.MovePlayed(Move.Swap(P(3, 4), P(3, 5))),
            new AttemptAction.BoosterUsed(new BoosterUse.HitCell(P(2, 3))),
            new AttemptAction.BoosterUsed(new BoosterUse.HitRow(P(0, 5))),
            new AttemptAction.BoosterUsed(new BoosterUse.HitColumn(P(4, 0))),
            new AttemptAction.BoosterUsed(new BoosterUse.Shuffle()),
            new AttemptAction.ShuffleRefused(),
            new AttemptAction.MovePlayed(Move.Tap(P(0, 9))),
            new AttemptAction.ExtraMovesRefused(),
            new AttemptAction.ExtraMovesTaken(),
            new AttemptAction.MovePlayed(Move.Swap(P(1, 1), P(2, 1))),
        };

        var leftStart = new AttemptStart(new LevelId(7), ContentVersion.First, 25, AttemptSeeds.From(3_000_000_000), attempt: 0, GamePhone, "unity", Noon);
        var leftOutcome = new AttemptOutcome(
            AttemptResult.Abandoned,
            1,
            new[] { new GoalOutcome(new GoalSpec.CollectColor(GemColor.Red, 30), done: 4), new GoalOutcome(new GoalSpec.BreakBoxes(10), done: 0) },
            extraMovesBought: false,
            boostersUsed: Array.Empty<string>(),
            Noon.AddSeconds(40));

        var lostStart = new AttemptStart(new LevelId(1000), ContentVersion.First, 1, AttemptSeeds.From(42), attempt: 5, GamePhone, "unity", Noon.AddTicks(5_987_647));
        var lostOutcome = new AttemptOutcome(
            AttemptResult.OutOfMoves,
            1,
            new[] { new GoalOutcome(new GoalSpec.DropItems(2), done: 1) },
            extraMovesBought: false,
            boostersUsed: Array.Empty<string>(),
            Noon.AddMinutes(3).AddTicks(2_500_000));

        return new[]
        {
            new AttemptRecord(wonStart, wonOutcome, lab: null, wonActions),
            new AttemptRecord(leftStart, leftOutcome, lab: null, new AttemptAction[] { new AttemptAction.MovePlayed(Move.Tap(P(5, 5))) }),
            new AttemptRecord(lostStart, lostOutcome, lab: null, new AttemptAction[] { new AttemptAction.MovePlayed(Move.Swap(P(0, 0), P(1, 0))), new AttemptAction.ExtraMovesRefused() }),
        };
    }

    internal static string FixturePath(string file) => Path.Combine(AppContext.BaseDirectory, "fixtures", file);
}
