using System.IO;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestBoards;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

public class AttemptRecordTests
{
    [Fact]
    public void Record_survives_its_json_line()
    {
        var start = new AttemptStart(new LevelId(12), 22, AttemptSeeds.WithFixedBoard(7, 99), attempt: 3, Phone, "web-lab", Noon);
        GoalOutcome[] goals =
        {
            new GoalOutcome(new GoalSpec.CollectColor(GemColor.Red, 40), done: 41),
            new GoalOutcome(new GoalSpec.ClearJelly(12), done: 5),
        };
        var outcome = new AttemptOutcome(AttemptResult.OutOfMoves, 27, goals, extraMovesBought: true, boostersUsed: new[] { "hammer" }, Noon.AddMinutes(4));
        Move[] moves = { Move.Swap(new Position(3, 4), new Position(3, 5)), Move.Tap(new Position(0, 9)) };
        var lab = new LabDetails("generated/level_0012", "00ff00ff00ff00ff", assistPercent: 20, hintsShown: 1, botMoves: 2, moves);
        var record = new AttemptRecord(start, outcome, lab);

        string line = AttemptRecordJson.Serialize(record);
        AttemptRecord restored = AttemptRecordJson.Parse(line);

        Assert.StartsWith("{\"schema\":1,", line);
        Assert.DoesNotContain('\n', line);
        Assert.Equal(line, AttemptRecordJson.Serialize(restored));
        Assert.Equal(AttemptSeeds.WithFixedBoard(7, 99), restored.Start.Seeds);
        Assert.Equal(3, restored.Start.Attempt);
        Assert.Equal(Noon, restored.Start.StartedUtc);
        Assert.Equal(goals, restored.Outcome.Goals);
        Assert.Equal(new[] { "hammer" }, restored.Outcome.BoostersUsed);
        Assert.NotNull(restored.Lab);
        Assert.Equal(moves, restored.Lab.Moves);
        Assert.True(restored.Lab.IsAssisted);
    }

    [Fact]
    public void Records_are_written_exactly_as_the_shared_schema_1_lines()
    {
        // The fixture pins the wire format for every writer of it: renaming a field breaks files already written.
        string[] golden = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "attempt-records.schema-1.jsonl"));

        string[] written = WireFormatExamples().Select(AttemptRecordJson.Serialize).ToArray();

        Assert.Equal(golden, written);
        Assert.Equal(golden, golden.Select(line => AttemptRecordJson.Serialize(AttemptRecordJson.Parse(line))));
    }

    [Fact]
    public void Record_of_the_released_game_has_no_lab_part_and_no_attempt_for_chosen_seeds()
    {
        AttemptRecord record = Of(GameTests.OpenLevel(), AttemptResult.Won, 18, attempt: null, lab: null);

        string line = AttemptRecordJson.Serialize(record);
        AttemptRecord restored = AttemptRecordJson.Parse(line);

        Assert.DoesNotContain("\"lab\"", line);
        Assert.DoesNotContain("\"attempt\"", line);
        Assert.Null(restored.Lab);
        Assert.Null(restored.Start.Attempt);
        Assert.Equal(AttemptResult.Won, restored.Outcome.Result);
    }

    [Fact]
    public void Record_from_a_newer_schema_is_refused_by_version()
    {
        string line = AttemptRecordJson.Serialize(Of(GameTests.OpenLevel(), AttemptResult.Won, 18))
            .Replace("\"schema\":1,", "\"schema\":2,", StringComparison.Ordinal);

        var error = Assert.Throws<AttemptRecordFormatException>(() => AttemptRecordJson.Parse(line));

        Assert.Contains("Schema version 2", error.Message);
    }

    [Theory]
    [InlineData("\"result\":\"won\"", "\"result\":\"draw\"")]
    [InlineData("\"result\":\"won\",", "")]
    [InlineData("\"deviceId\":\"0123456789abcdef0123456789abcdef\"", "\"deviceId\":\"ada@example.com\"")]
    [InlineData("\"moves\":[[1,1,1,2],[3,4]]", "\"moves\":[[1,1,1,2],[3]]")]
    [InlineData("\"type\":\"CollectColor\"", "\"type\":\"CollectGold\"")]
    [InlineData("\"moveLimit\":30", "\"moveLimit\":null")]
    public void Missing_fields_and_bad_values_are_format_errors(string field, string replacement)
    {
        LevelDefinition level = GameTests.OpenLevel();
        string line = AttemptRecordJson.Serialize(Of(level, AttemptResult.Won, 18, lab: Lab(level)));
        Assert.Contains(field, line);

        Assert.Throws<AttemptRecordFormatException>(() => AttemptRecordJson.Parse(line.Replace(field, replacement, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    [InlineData("3f2b8c1e-9d4a-4f6b-8e2c-1a7d5b9c0e3f", true)]
    [InlineData("ada@example.com", false)]
    [InlineData("Ada Lovelace", false)]
    [InlineData("short", false)]
    [InlineData(null, false)]
    public void Device_ids_are_random_tokens_never_personal_text(string? text, bool accepted)
    {
        Assert.Equal(accepted, DeviceId.TryCreate(text, out _));
    }

    [Fact]
    public void Device_id_longer_than_the_limit_is_refused()
    {
        Assert.True(DeviceId.TryCreate(new string('a', DeviceId.MaxLength), out _));
        Assert.False(DeviceId.TryCreate(new string('a', DeviceId.MaxLength + 1), out _));
    }

    [Theory]
    [InlineData(GameStatus.Won, AttemptResult.Won)]
    [InlineData(GameStatus.LostOutOfMoves, AttemptResult.OutOfMoves)]
    [InlineData(GameStatus.LostNoMovesPossible, AttemptResult.NoMovesPossible)]
    [InlineData(GameStatus.Playing, AttemptResult.Abandoned)]
    public void Attempt_result_follows_the_game_status_and_an_undecided_game_was_abandoned(GameStatus status, AttemptResult expected)
    {
        Assert.Equal(expected, AttemptResults.AtEnd(status));
    }

    [Fact]
    public void Game_status_values_are_a_closed_set_for_the_result_mapping()
    {
        // AttemptResults.AtEnd must be revisited when a status is added.
        Assert.Equal(
            new[] { "LostNoMovesPossible", "LostOutOfMoves", "Playing", "Won" },
            Enum.GetNames<GameStatus>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Outcome_of_real_games_classifies_won_out_of_moves_and_abandoned()
    {
        LevelDefinition easy = Level(Blank(10, 12), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 3) }, moves: 30, colors: 5);
        LevelDefinition hard = GameTests.OpenLevel().WithMoveLimit(2);

        Game won = PlayOut(Game.Start(easy, 5));
        Game lost = PlayOut(Game.Start(hard, 5));
        Game left = Game.Start(hard, 5);
        left.TryPlay(left.FindLegalMoves()[0]);

        AttemptOutcome wonOutcome = AttemptOutcome.Of(won, Noon);
        Assert.Equal(AttemptResult.Won, wonOutcome.Result);
        Assert.Equal(won.MovesUsed, wonOutcome.MovesUsed);
        Assert.True(wonOutcome.Goals.All(goal => goal.IsComplete));
        Assert.Equal(AttemptResult.OutOfMoves, AttemptOutcome.Of(lost, Noon).Result);
        Assert.Equal(2, AttemptOutcome.Of(lost, Noon).MovesUsed);
        AttemptOutcome leftOutcome = AttemptOutcome.Of(left, Noon);
        Assert.Equal(AttemptResult.Abandoned, leftOutcome.Result);
        Assert.Equal(left.Goals[0].Done, leftOutcome.Goals[0].Done);
        Assert.False(leftOutcome.ExtraMovesBought);
        Assert.Empty(leftOutcome.BoostersUsed);
    }

    [Fact]
    public void An_attempt_cannot_end_before_it_starts()
    {
        AttemptRecord valid = Of(GameTests.OpenLevel(), AttemptResult.Won, 18);
        var early = new AttemptOutcome(AttemptResult.Won, 18, valid.Outcome.Goals, false, Array.Empty<string>(), Noon.AddSeconds(-1));

        Assert.Throws<ArgumentException>(() => new AttemptRecord(valid.Start, early, lab: null));
    }

    /// <summary>
    /// The records behind the shared fixture's lines, in order: a lab attempt with every field (fixed board, a tap and a
    /// swap, help used, sub-second times) and a released-game attempt (no lab part, hand-chosen seeds, extra moves bought,
    /// a booster).
    /// </summary>
    internal static AttemptRecord[] WireFormatExamples()
    {
        var labStart = new AttemptStart(new LevelId(12), 22, AttemptSeeds.WithFixedBoard(7, 99), attempt: 3, Phone, "web-lab", Noon.AddTicks(5_987_647));
        GoalOutcome[] labGoals =
        {
            new GoalOutcome(new GoalSpec.CollectColor(GemColor.Red, 40), done: 41),
            new GoalOutcome(new GoalSpec.ClearJelly(12), done: 12),
        };
        var labOutcome = new AttemptOutcome(AttemptResult.Won, 18, labGoals, extraMovesBought: false, boostersUsed: Array.Empty<string>(), Noon.AddMinutes(3).AddTicks(2_500_000));
        Move[] moves = { Move.Swap(new Position(3, 4), new Position(3, 5)), Move.Tap(new Position(0, 9)) };
        var lab = new LabDetails("generated/level_0012", "00ff00ff00ff00ff", assistPercent: 0, hintsShown: 1, botMoves: 2, moves);

        var gameStart = new AttemptStart(
            new LevelId(37), 30, AttemptSeeds.From(2_800_535_473), attempt: null, new DeviceId("3f2b8c1e-9d4a-4f6b-8e2c-1a7d5b9c0e3f"), "unity", Noon);
        GoalOutcome[] gameGoals = { new GoalOutcome(new GoalSpec.BreakBoxes(20), done: 7) };
        var gameOutcome = new AttemptOutcome(AttemptResult.Abandoned, 33, gameGoals, extraMovesBought: true, boostersUsed: new[] { "hammer" }, Noon.AddMinutes(5));

        return new[] { new AttemptRecord(labStart, labOutcome, lab), new AttemptRecord(gameStart, gameOutcome, lab: null) };
    }

    private static Game PlayOut(Game game)
    {
        var bot = new GreedyPolicy(SkillProfile.Expert);
        var rng = new Rng(1);
        while (!game.Status.IsOver())
        {
            game.TryPlay(bot.ChooseMove(game, game.FindLegalMoves(), rng));
        }

        return game;
    }
}
