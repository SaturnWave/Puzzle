using System.IO;
using Puzzle.Client.Flow;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

/// <summary>
/// The attempt record's line as the Unity game writes it by hand (<c>AttemptLine</c>, the Unity project's source file,
/// compiled into these tests): it is the tools' line, character for character, so the tools read it as the same record;
/// an optional field that is absent is left out, never written as a zero.
/// </summary>
public class AttemptLineTests
{
    [Fact]
    public void The_games_line_is_the_tools_line_for_every_pinned_record_of_the_game()
    {
        string[] withActions = File.ReadAllLines(AttemptRecordJsonTests.FixturePath("attempt-records.actions.jsonl"));
        string gameLineOfSchemaOne = File.ReadAllLines(AttemptRecordJsonTests.FixturePath("attempt-records.schema-1.jsonl"))[1];

        Assert.Equal(withActions, AttemptRecordJsonTests.ActionWireExamples().Select(AttemptLine.Of));
        Assert.Equal(gameLineOfSchemaOne, AttemptLine.Of(AttemptRecordTests.WireFormatExamples()[1]));
    }

    [Fact]
    public void The_tools_read_the_games_line_of_a_played_attempt_as_the_same_record_and_replay_it()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptSeeds seeds = AttemptSeeds.From(17);
        Game game = Game.Start(level, seeds, new GameOptions { StartBoosters = new[] { StartBooster.ColorBomb }, RecordActions = true });
        Assert.True(game.TryUseBooster(new BoosterUse.HitRow(new Position(4, 4))));
        AttemptActionTests.PlayRandomly(game, new Rng(3), moves: 6);
        AttemptRecord record = AttemptActionTests.RecordOf(game, seeds);

        string line = AttemptLine.Of(record);
        AttemptRecord read = AttemptRecordJson.Parse(line);

        Assert.Equal(AttemptRecordJson.Serialize(record), line);
        Assert.Equal(record.Actions, read.Actions);
        Assert.Equal(record.Outcome.Goals, read.Outcome.Goals);
        Assert.Equal(record.Start.Seeds, read.Start.Seeds);
        AttemptActionTests.AssertTheSameEnd(game, AttemptReplay.Of(read, level));
    }

    [Fact]
    public void Optional_fields_that_are_absent_are_left_out_and_not_written_as_zero()
    {
        AttemptRecord bare = Of(GameTests.OpenLevel(), AttemptResult.OutOfMoves, 30, attempt: null);

        string line = AttemptLine.Of(bare);
        AttemptRecord read = AttemptRecordJson.Parse(line);

        Assert.Equal(AttemptRecordJson.Serialize(bare), line);
        Assert.DoesNotContain("\"contentVersion\"", line);
        Assert.DoesNotContain("\"attempt\"", line);
        Assert.DoesNotContain("\"actions\"", line);
        Assert.DoesNotContain("\"lab\"", line);
        Assert.Null(read.Start.Attempt);
        Assert.Null(read.Actions);
    }

    [Fact]
    public void A_name_with_a_quote_or_a_backslash_is_escaped_as_json_has_it()
    {
        AttemptRecord plain = Of(GameTests.OpenLevel(), AttemptResult.Won, 18);
        var outcome = new AttemptOutcome(AttemptResult.Won, 18, plain.Outcome.Goals, false, new[] { "odd \"one\"", "back\\slash" }, plain.Outcome.EndedUtc);
        var record = new AttemptRecord(plain.Start, outcome, lab: null);

        AttemptRecord read = AttemptRecordJson.Parse(AttemptLine.Of(record));

        Assert.Equal(outcome.BoostersUsed, read.Outcome.BoostersUsed);
    }

    [Fact]
    public void A_record_with_a_lab_part_is_not_the_games_to_write()
    {
        LevelDefinition level = GameTests.OpenLevel();

        Assert.Throws<ArgumentException>(() => AttemptLine.Of(Of(level, AttemptResult.Won, 18, lab: Lab(level))));
    }
}

/// <summary>
/// The phone's attempt log (<c>AttemptLogFile</c>, the Unity project's source file, compiled into these tests): one line
/// per attempt, in the order the attempts ended; a record that cannot be written waits and goes before the next one;
/// a line cut short is left, and the next record starts a line of its own.
/// </summary>
public class AttemptLogFileTests
{
    private static readonly DeviceId GamePhone = new("6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a");

    [Fact]
    public void Each_record_is_one_line_appended_in_the_order_the_attempts_ended()
    {
        using var temp = new TempFolder();
        var log = new AttemptLogFile(temp.Path, GamePhone);
        AttemptRecord[] records = AttemptRecordJsonTests.ActionWireExamples();

        foreach (AttemptRecord record in records)
        {
            log.Append(record);
        }

        Assert.Equal(records.Select(AttemptLine.Of), File.ReadAllLines(log.Path));
        AttemptLogContents read = new AttemptLog(log.Path).ReadAll();
        Assert.Equal(3, read.Records.Count);
        Assert.Empty(read.Problems);
        Assert.Equal(0, log.Waiting);
    }

    [Fact]
    public void A_record_that_cannot_be_written_waits_and_is_written_before_the_next_one()
    {
        using var temp = new TempFolder();
        string folder = Path.Combine(temp.Path, "log");
        File.WriteAllText(folder, "a file where the folder should be");
        var log = new AttemptLogFile(folder, GamePhone);

        Assert.Throws<IOException>(() => log.Append(Ending(1)));
        Assert.Throws<IOException>(() => log.WriteWaiting());
        Assert.Equal(1, log.Waiting);

        File.Delete(folder);
        log.Append(Ending(2));

        Assert.Equal(new[] { AttemptLine.Of(Ending(1)), AttemptLine.Of(Ending(2)) }, File.ReadAllLines(log.Path));
        Assert.Equal(0, log.Waiting);
        Assert.Equal(0, log.RecordsLost);
    }

    [Fact]
    public void Past_the_bound_the_record_that_waited_longest_gives_way_and_is_counted()
    {
        using var temp = new TempFolder();
        string folder = Path.Combine(temp.Path, "log");
        File.WriteAllText(folder, "a file where the folder should be");
        var log = new AttemptLogFile(folder, GamePhone);

        for (int ended = 1; ended <= AttemptLogFile.MaxWaiting + 2; ended++)
        {
            Assert.Throws<IOException>(() => log.Append(Ending(ended)));
        }

        Assert.Equal(AttemptLogFile.MaxWaiting, log.Waiting);
        Assert.Equal(2, log.RecordsLost);
        File.Delete(folder);
        log.WriteWaiting();
        string[] lines = File.ReadAllLines(log.Path);
        Assert.Equal(AttemptLogFile.MaxWaiting, lines.Length);
        Assert.Equal(AttemptLine.Of(Ending(3)), lines[0]);
    }

    [Fact]
    public void A_line_cut_short_is_left_and_the_next_record_starts_a_line_of_its_own()
    {
        using var temp = new TempFolder();
        var log = new AttemptLogFile(temp.Path, GamePhone);
        File.WriteAllText(log.Path, "{\"schema\":1,\"level\":7,\"atte");

        log.Append(Ending(1));

        AttemptLogContents read = new AttemptLog(log.Path).ReadAll();
        Assert.Equal(AttemptLine.Of(Ending(1)), AttemptRecordJson.Serialize(Assert.Single(read.Records)));
        Assert.Equal(1, Assert.Single(read.Problems).Line);
    }

    [Fact]
    public void A_record_of_another_phone_is_not_this_logs()
    {
        using var temp = new TempFolder();
        var log = new AttemptLogFile(temp.Path, Phone);

        Assert.Throws<ArgumentException>(() => log.Append(Ending(1)));
        Assert.Equal(0, log.Waiting);
        Assert.False(File.Exists(log.Path));
    }

    /// <summary>The pinned attempt that was left after one move, ending this many seconds after it began.</summary>
    private static AttemptRecord Ending(int seconds)
    {
        AttemptRecord left = AttemptRecordJsonTests.ActionWireExamples()[1];
        AttemptOutcome was = left.Outcome;
        var outcome = new AttemptOutcome(was.Result, was.MovesUsed, was.Goals, was.ExtraMovesBought, was.BoostersUsed, left.Start.StartedUtc.AddSeconds(seconds));
        return new AttemptRecord(left.Start, outcome, lab: null, left.Actions);
    }
}
