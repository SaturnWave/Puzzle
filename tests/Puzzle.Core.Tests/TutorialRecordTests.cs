using Puzzle.Client.Flow;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;

namespace Puzzle.Core.Tests;

/// <summary>
/// How the moves the released game's tutorial shows by hand reach an attempt's record: a real move of the attempt,
/// counted in an optional field written only when there was one, by both writers alike, and help like a hint.
/// </summary>
public class TutorialRecordTests
{
    [Fact]
    public void An_attempt_with_a_move_shown_by_hand_counts_it_in_its_line_and_both_writers_write_it_alike()
    {
        AttemptRecord played = TestRecords.Of(GameTests.OpenLevel(), AttemptResult.Won, 12);
        AttemptRecord guided = new(played.Start, played.Outcome, lab: null, played.Actions, hintsShown: 1, guidedMoves: 1);

        string counted = AttemptLine.Of(guided);

        Assert.Equal(AttemptRecordJson.Serialize(guided), counted);
        Assert.EndsWith(",\"hintsShown\":1,\"guidedMoves\":1}", counted, StringComparison.Ordinal);
        Assert.DoesNotContain("guidedMoves", AttemptLine.Of(played));
        Assert.Equal((1, 1), (AttemptRecordJson.Parse(counted).HintsShown, AttemptRecordJson.Parse(counted).GuidedMoves));
        Assert.Equal(0, AttemptRecordJson.Parse(AttemptLine.Of(played)).GuidedMoves);
        Assert.Equal(1, AttemptRecord.SchemaVersion); // the field is optional: schema 1 stays
    }

    [Fact]
    public void A_count_of_fewer_than_no_guided_moves_makes_no_record()
    {
        AttemptRecord played = TestRecords.Of(GameTests.OpenLevel(), AttemptResult.Won, 12);
        string line = AttemptLine.Of(played).Replace("\"endedUtc\"", "\"guidedMoves\":-1,\"endedUtc\"", StringComparison.Ordinal);

        Assert.Throws<AttemptRecordFormatException>(() => AttemptRecordJson.Parse(line));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttemptRecord(played.Start, played.Outcome, lab: null, guidedMoves: -1));
    }

    [Fact]
    public void An_attempt_with_a_move_shown_by_hand_is_help_and_is_left_out_of_the_comparison_with_the_bots()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord comparable = TestRecords.Of(level, AttemptResult.Won, 20);
        AttemptRecord guided = new(comparable.Start, comparable.Outcome, lab: null, actions: null, guidedMoves: 1);

        HumanBotReport report = HumanBotComparison.Compare(new[] { comparable, guided }, new[] { level }, botRuns: 1);

        Assert.Equal(AttemptStanding.Assisted, AttemptCounting.Standing(guided));
        Assert.Equal(new RecordTally(Compared: 1, Assisted: 1, ChosenSeeds: 0, UnknownLevel: 0, TesterMode: 0), report.Records);
    }
}
