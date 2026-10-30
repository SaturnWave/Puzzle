using System.IO;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Resolution;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// The shuffle booster's two rules (the owner's decision of 2026-10-15): today it lays the free gems so that none
/// completes a match and is refused only on a board no laying can change; the first rule tried random orders and was
/// refused when none of them came out without a match. Records name their rule, so a record of the first rule, every
/// record written before the change, still replays to its end.
/// </summary>
public class ShuffleBoosterRuleTests
{
    private const string FirstRuleRecords = "attempt-records.first-shuffle-rule.jsonl";

    [Fact]
    public void On_a_large_open_board_the_rule_of_today_shuffles_where_the_first_rule_refused()
    {
        LevelDefinition level = GameTests.OpenLevel();
        ulong seed = Enumerable.Range(0, 500).Select(n => (ulong)n)
            .First(n => !Game.Start(level, n, Under(ShuffleBoosterRule.RandomOrders)).TryUseBooster(new BoosterUse.Shuffle()));
        Game game = Game.Start(level, seed, Under(ShuffleBoosterRule.LaidWithoutAMatch));
        ulong before = game.Board.ComputeHash();
        List<Tile> gemsBefore = FreeGems(game.Board);

        Assert.True(game.TryUseBooster(new BoosterUse.Shuffle()));

        Assert.NotEqual(before, game.Board.ComputeHash());
        Assert.Equal(gemsBefore, FreeGems(game.Board));
        Assert.False(new MatchFinder(game.Board).AnyMatch());
        Assert.NotEmpty(game.FindLegalMoves());
        Assert.Equal(0, game.MovesUsed);
        Assert.Equal(new[] { "shuffle" }, game.BoostersUsed);
    }

    [Fact]
    public void The_rule_of_today_shuffles_every_levels_first_board_and_its_board_after_a_few_moves()
    {
        var refused = new List<string>();
        foreach (LevelDefinition level in LiveLevels())
        {
            Game game = LevelStart.Attempt(level, attempt: 0);
            ShuffleAndCheck(game, level, "first board", refused);
            AttemptActionTests.PlayRandomly(game, new Rng((ulong)level.Id.Value), moves: 3);
            if (game.Status == GameStatus.Playing)
            {
                ShuffleAndCheck(game, level, "after three moves", refused);
            }
        }

        Assert.Empty(refused);
    }

    [Fact]
    public void The_rule_of_today_keeps_every_tile_that_is_not_a_free_gem_where_it_was()
    {
        string[] tiles = Filler(7, 7).With(1, 1, '2').With(3, 3, 'X').With(5, 1, 'H');
        string[] chains = Blank(7, 7).With(5, 5, '1').With(0, 6, '2');
        Game game = Game.Start(Level(tiles, chains: chains), 3, Under(ShuffleBoosterRule.LaidWithoutAMatch));
        Board board = game.Board;
        Position[] fixedCells = { P(1, 1), P(3, 3), P(5, 1), P(5, 5), P(0, 6) };
        Tile[] fixedBefore = fixedCells.Select(board.TileAt).ToArray();
        List<Tile> gemsBefore = FreeGems(board);

        Assert.True(game.TryUseBooster(new BoosterUse.Shuffle()));

        Assert.Equal(fixedBefore, fixedCells.Select(board.TileAt));
        Assert.Equal(gemsBefore, FreeGems(board));
        Assert.False(new MatchFinder(board).AnyMatch());
        Assert.Equal(1, game.TotalStats.Shuffles);
    }

    [Fact]
    public void Free_gems_all_of_one_kind_are_refused_by_the_rule_of_today_before_it_draws_anything()
    {
        // Seven rockets and two red gems: no order of the gems is another board.
        LevelDefinition level = Level(Blank(3, 3, 'H').With(0, 0, 'r').With(2, 2, 'r'));
        AttemptSeeds seeds = AttemptSeeds.From(1);
        Game game = Game.Start(level, seeds, new GameOptions { RecordActions = true });
        ulong before = game.Board.ComputeHash();

        Assert.False(game.TryUseBooster(new BoosterUse.Shuffle()));
        Assert.Equal(before, game.Board.ComputeHash());
        Assert.True(game.TryPlay(Move.Tap(P(1, 1))));
        Assert.True(game.TryUseBooster(new BoosterUse.Shuffle()));

        Assert.Equal(new AttemptAction.ShuffleRefused(), game.Actions[0]);
        AttemptActionTests.AssertTheSameEnd(game, AttemptReplay.Of(AttemptActionTests.RecordOf(game, seeds), level));

        // The refusal drew nothing: without it the accepted shuffle lays the same board.
        Game withoutTheRefusal = AttemptReplay.Play(level, seeds, Array.Empty<StartBooster>(), ShuffleBoosterRule.LaidWithoutAMatch, game.Actions.Skip(1).ToArray());
        Assert.Equal(game.Board.ComputeHash(), withoutTheRefusal.Board.ComputeHash());
    }

    [Fact]
    public void A_board_whose_only_arrangement_without_a_match_is_its_own_is_refused_and_left_as_it_was()
    {
        // Four red gems and a blue one in a row of five: the blue one has to stand in the middle, where it stands.
        Game game = Game.Start(Level(new[] { "rrbrr" }), 1, new GameOptions { RecordActions = true });
        ulong before = game.Board.ComputeHash();

        Assert.False(game.TryUseBooster(new BoosterUse.Shuffle()));

        Assert.Equal(before, game.Board.ComputeHash());
        Assert.Empty(game.BoostersUsed);
        Assert.Equal(new AttemptAction[] { new AttemptAction.ShuffleRefused() }, game.Actions);
    }

    [Fact]
    public void Records_written_before_the_rule_changed_name_none_and_replay_by_the_first_rule_to_their_end()
    {
        string[] lines = File.ReadAllLines(AttemptRecordJsonTests.FixturePath(FirstRuleRecords));
        Dictionary<int, LevelDefinition> levels = LiveLevels().ToDictionary(level => level.Id.Value);

        Assert.Equal(4, lines.Length);
        foreach (string line in lines)
        {
            AttemptRecord record = AttemptRecordJson.Parse(line);
            LevelDefinition level = levels[record.Start.Level.Value];

            Assert.DoesNotContain("shuffleRule", line);
            Assert.Equal(ShuffleBoosterRule.RandomOrders, record.Start.ShuffleRule);
            Assert.Contains("shuffle", record.Outcome.BoostersUsed);
            Game replayed = AttemptReplay.Of(record, level);
            Assert.Equal(record.Actions, replayed.Actions);
            Assert.Equal(line, AttemptLine.Of(record));
        }
    }

    [Fact]
    public void A_record_of_the_first_rule_with_a_refused_shuffle_does_not_replay_under_the_rule_of_today()
    {
        string line = File.ReadAllLines(AttemptRecordJsonTests.FixturePath(FirstRuleRecords)).First(text => text.Contains("shuffle-refused"));
        AttemptRecord record = AttemptRecordJson.Parse(line);
        LevelDefinition level = LiveLevels().Single(candidate => candidate.Id == record.Start.Level);
        AttemptRecord renamed = new(Renamed(record.Start, ShuffleBoosterRule.LaidWithoutAMatch), record.Outcome, lab: null, record.Actions);

        Assert.NotNull(AttemptReplay.Of(record, level));
        Assert.Throws<AttemptReplayException>(() => AttemptReplay.Of(renamed, level));
    }

    [Fact]
    public void A_record_of_the_rule_of_today_names_it_just_before_its_actions_and_is_read_back_with_it()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptSeeds seeds = AttemptSeeds.From(5);
        Game game = Game.Start(level, seeds, new GameOptions { RecordActions = true });
        Assert.True(game.TryUseBooster(new BoosterUse.Shuffle()));
        AttemptRecord record = AttemptActionTests.RecordOf(game, seeds);

        string line = AttemptRecordJson.Serialize(record);
        AttemptRecord read = AttemptRecordJson.Parse(line);

        Assert.Contains(",\"shuffleRule\":2,\"actions\":[", line);
        Assert.Equal(line, AttemptLine.Of(record));
        Assert.Equal(ShuffleBoosterRule.LaidWithoutAMatch, read.Start.ShuffleRule);
        AttemptActionTests.AssertTheSameEnd(game, AttemptReplay.Of(read, level));
    }

    [Fact]
    public void A_rule_this_build_does_not_know_reads_as_a_record_that_keeps_no_actions()
    {
        string today = AttemptRecordJson.Serialize(TodaysRecord());

        AttemptRecord later = AttemptRecordJson.Parse(today.Replace("\"shuffleRule\":2", "\"shuffleRule\":9", StringComparison.Ordinal));

        Assert.Null(later.Actions);
        Assert.Equal(new[] { "shuffle" }, later.Outcome.BoostersUsed);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"two\"")]
    public void A_rule_number_that_is_no_rule_number_is_a_format_error(string number)
    {
        string today = AttemptRecordJson.Serialize(TodaysRecord());

        Assert.Throws<AttemptRecordFormatException>(() => AttemptRecordJson.Parse(today.Replace("\"shuffleRule\":2", "\"shuffleRule\":" + number, StringComparison.Ordinal)));
    }

    [Fact]
    public void The_rules_keep_their_numbers_because_records_name_them_by_number()
    {
        Assert.Equal(1, (int)ShuffleBoosterRule.RandomOrders);
        Assert.Equal(2, (int)ShuffleBoosterRule.LaidWithoutAMatch);
        Assert.Equal(ShuffleBoosterRule.LaidWithoutAMatch, GameOptions.Default.ShuffleBoosterRule);
    }

    private static GameOptions Under(ShuffleBoosterRule rule) => new() { ShuffleBoosterRule = rule };

    private static AttemptRecord TodaysRecord()
    {
        AttemptSeeds seeds = AttemptSeeds.From(5);
        Game game = Game.Start(GameTests.OpenLevel(), seeds, new GameOptions { RecordActions = true });
        Assert.True(game.TryUseBooster(new BoosterUse.Shuffle()));
        return AttemptActionTests.RecordOf(game, seeds);
    }

    private static AttemptStart Renamed(AttemptStart start, ShuffleBoosterRule rule) =>
        new(start.Level, start.ContentVersion, start.MoveLimit, start.Seeds, start.Attempt, start.Device, start.Client, start.StartedUtc, rule);

    /// <summary>Shuffles a board that is being played, and notes where it was refused or left a board that breaks the rule.</summary>
    private static void ShuffleAndCheck(Game game, LevelDefinition level, string when, List<string> refused)
    {
        List<Tile> gemsBefore = FreeGems(game.Board);
        if (gemsBefore.Distinct().Count() < 2)
        {
            return;
        }

        if (!game.TryUseBooster(new BoosterUse.Shuffle()))
        {
            refused.Add($"level {level.Id.Value}, {when}: refused");
        }
        else if (new MatchFinder(game.Board).AnyMatch() || !FreeGems(game.Board).SequenceEqual(gemsBefore) || game.FindLegalMoves().Count == 0)
        {
            refused.Add($"level {level.Id.Value}, {when}: a match, other gems or no move");
        }
    }

    /// <summary>The free gems of the board (gems under no chain), sorted, so two boards compare by what they hold and not where.</summary>
    private static List<Tile> FreeGems(Board board) =>
        (from y in Enumerable.Range(0, board.Height)
         from x in Enumerable.Range(0, board.Width)
         let p = P(x, y)
         where board.Shape.IsPlayable(p) && board.TileAt(p).IsGem && board.ChainAt(p) == 0
         select board.TileAt(p))
        .OrderBy(tile => tile.Color)
        .ToList();

    private static IEnumerable<LevelDefinition> LiveLevels() =>
        Directory.GetFiles(RepoFiles.LiveLevelsFolder, "level_*.json").OrderBy(path => path, StringComparer.Ordinal).Select(LevelJson.Load);
}
