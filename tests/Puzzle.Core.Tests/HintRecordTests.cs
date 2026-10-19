using Puzzle.Client.Flow;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;

namespace Puzzle.Core.Tests;

/// <summary>
/// How the hints the released game shows by itself reach an attempt's record and what they make of it: the count is
/// written only when there was a hint, both writers write it alike, and a hinted attempt is help like the lab's hints.
/// </summary>
public class HintRecordTests
{
    [Fact]
    public void An_attempt_the_game_showed_hints_in_counts_them_in_its_line_and_both_writers_write_it_alike()
    {
        AttemptRecord played = TestRecords.Of(GameTests.OpenLevel(), AttemptResult.Won, 12);
        AttemptRecord hinted = new(played.Start, played.Outcome, lab: null, played.Actions, hintsShown: 2);

        string counted = AttemptLine.Of(hinted);
        string plain = AttemptLine.Of(played);

        Assert.Equal(AttemptRecordJson.Serialize(hinted), counted);
        Assert.Equal(AttemptRecordJson.Serialize(played), plain);
        Assert.Contains(",\"hintsShown\":2", counted);
        Assert.DoesNotContain("hintsShown", plain);
        Assert.Equal(2, AttemptRecordJson.Parse(counted).HintsShown);
        Assert.Equal(0, AttemptRecordJson.Parse(plain).HintsShown);
        Assert.Equal(1, AttemptRecord.SchemaVersion); // the field is optional: schema 1 stays
    }

    [Fact]
    public void A_count_of_fewer_than_no_hints_makes_no_record()
    {
        AttemptRecord played = TestRecords.Of(GameTests.OpenLevel(), AttemptResult.Won, 12);
        string line = AttemptLine.Of(played).Replace("\"endedUtc\"", "\"hintsShown\":-1,\"endedUtc\"", StringComparison.Ordinal);

        Assert.Throws<AttemptRecordFormatException>(() => AttemptRecordJson.Parse(line));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttemptRecord(played.Start, played.Outcome, lab: null, hintsShown: -1));
    }

    [Fact]
    public void An_attempt_the_game_showed_a_hint_in_is_help_and_is_left_out_of_the_comparison_with_the_bots()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord comparable = TestRecords.Of(level, AttemptResult.Won, 20);
        AttemptRecord hinted = new(comparable.Start, comparable.Outcome, lab: null, actions: null, hintsShown: 1);
        AttemptRecord hintedInTheTesterMode = new(comparable.Start, comparable.Outcome, lab: null, actions: null, testerMode: true, hintsShown: 3);

        HumanBotReport report = HumanBotComparison.Compare(new[] { comparable, hinted, hinted }, new[] { level }, botRuns: 1);

        Assert.Equal(AttemptStanding.Comparable, AttemptCounting.Standing(comparable));
        Assert.Equal(AttemptStanding.Assisted, AttemptCounting.Standing(hinted));
        Assert.Equal(AttemptStanding.TesterMode, AttemptCounting.Standing(hintedInTheTesterMode));
        Assert.Equal(new RecordTally(Compared: 1, Assisted: 2, ChosenSeeds: 0, UnknownLevel: 0, TesterMode: 0), report.Records);
    }
}
