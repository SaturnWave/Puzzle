using Puzzle.Client.Purchases;
using static Puzzle.Core.Tests.CloudSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The server's numbers of the verified payments (docs/research/server-v1.md §3.7): a set never cut short, written as
/// runs ("1-12,14") that a damaged text cannot blow up; and the server's ledger as the game reads it, line by line.
/// </summary>
public class LedgerTests
{
    private static LedgerNumbers Numbers(params long[] numbers) => LedgerNumbers.Of(numbers.Select(number => new LedgerNumber(number)));

    [Theory]
    [InlineData(new long[] { }, "")]
    [InlineData(new long[] { 14 }, "14")]
    [InlineData(new long[] { 3, 1, 2, 14, 12, 11, 2 }, "1-3,11-12,14")]
    [InlineData(new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14 }, "1-12,14")]
    public void Numbers_are_written_as_ascending_runs_each_number_once(long[] numbers, string text)
    {
        LedgerNumbers set = Numbers(numbers);

        Assert.Equal(text, set.Text);
        Assert.Equal(set, LedgerNumbers.Read(text).Numbers);
        Assert.Equal(numbers.Distinct().Count(), set.Count);
        Assert.All(numbers, number => Assert.True(set.Contains(new LedgerNumber(number))));
        Assert.False(set.Contains(new LedgerNumber(13)));
    }

    [Fact]
    public void Union_intersection_and_difference_hold_every_number_where_it_belongs()
    {
        LedgerNumbers one = LedgerNumbers.Read("1-10,20-30").Numbers;
        LedgerNumbers other = LedgerNumbers.Read("5-22,30,40").Numbers;

        Assert.Equal("1-30,40", one.Union(other).Text);
        Assert.Equal("5-10,20-22,30", one.Intersect(other).Text);
        Assert.Equal("1-4,23-29", one.Except(other).Text);
        Assert.Equal("11-19,40", other.Except(one).Text);
        Assert.Equal("1-10,12,20-30", one.With(new LedgerNumber(12)).Text);
        Assert.Same(one, one.With(new LedgerNumber(7)));
    }

    [Theory]
    [InlineData("1-3,,5", "1-3,5", 1)] // an empty part
    [InlineData("0,2", "2", 1)] // the ledger counts from 1
    [InlineData("-4,2", "2", 1)]
    [InlineData("9-7,8", "8", 1)] // a run backwards
    [InlineData("a,b-c,3", "3", 2)]
    [InlineData("99999999999999999999,3", "3", 1)] // more than a long holds
    [InlineData(" 4 , 5 ", "4-5", 0)] // a space a hand put in is no harm
    public void A_part_no_game_writes_is_set_aside_alone(string text, string kept, int setAside)
    {
        LedgerNumbersReading reading = LedgerNumbers.Read(text);

        Assert.Equal(kept, reading.Numbers.Text);
        Assert.Equal(setAside, reading.SetAside.Count);
    }

    [Fact]
    public void A_run_of_billions_costs_no_more_than_a_run_of_two()
    {
        LedgerNumbers huge = LedgerNumbers.Read("1-9000000000000").Numbers;

        Assert.Equal(9_000_000_000_000, huge.Count);
        Assert.True(huge.Contains(new LedgerNumber(8_999_999_999_999)));
        Assert.Equal("1-9000000000000", huge.Union(Numbers(5)).Text);
        Assert.Equal("1-4,6-9000000000000", huge.Except(Numbers(5)).Text);
    }

    [Fact]
    public void The_ledger_reads_line_by_line_and_sets_aside_what_no_server_writes()
    {
        string text = """
            {"next":6,"items":[
             {"seq":1,"store":"GooglePlay","product":"coins.small","order":"GPA.1","at":"2026-11-01T10:02:11Z","state":"valid","test":false},
             {"seq":2,"store":"GooglePlay","product":"coins.large","order":"GPA.2","at":"2026-11-02T10:02:11Z","state":"voided","test":true},
             {"seq":3,"store":"GooglePlay","product":"coins.small","order":"GPA.3","at":"2026-11-02T11:00:00Z","state":"lost","test":false},
             {"seq":0,"store":"GooglePlay","product":"coins.small","order":"GPA.4","at":"2026-11-02T11:00:00Z","state":"valid","test":false},
             {"seq":1,"store":"GooglePlay","product":"coins.large","order":"GPA.5","at":"2026-11-02T11:00:00Z","state":"valid","test":false},
             {"seq":5,"store":"AppleAppStore","product":"","order":"2000001","at":"2026-11-02T11:00:00Z","state":"refunded","test":false},
             null
            ]}
            """;

        PurchaseLedgerReading reading = PurchaseLedger.Read(Json.Read<PurchaseLedgerDocument>(text));

        Assert.Equal(new[] { 1L, 2L }, reading.Ledger.Entries.Select(entry => entry.Number.Value));
        Assert.Equal(5, reading.SetAside.Count); // a state no server writes, number 0, number 1 again, no product, no line
        LedgerEntry voided = reading.Ledger.Find(new LedgerNumber(2))!;
        Assert.Equal((LedgerState.Voided, true, "coins.large"), (voided.State, voided.IsTest, voided.Product));
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 10, 2, 11, TimeSpan.Zero), voided.At);
        Assert.Null(reading.Ledger.Find(new LedgerNumber(3)));
    }

    [Fact]
    public void A_ledger_that_is_no_document_reads_as_none_with_a_word_on_why()
    {
        PurchaseLedgerReading reading = PurchaseLedger.Read(Json.Read<PurchaseLedgerDocument>("not a ledger"));

        Assert.Empty(reading.Ledger.Entries);
        Assert.Single(reading.SetAside);
    }

    /// <summary>The verified payments no device delivered: the purchases' stage gives them back. One refunded or cancelled never is.</summary>
    [Fact]
    public void The_payments_not_delivered_are_the_valid_ones_whose_number_the_progress_lacks()
    {
        PurchaseLedgerDocument document = Json.Read<PurchaseLedgerDocument>(LedgerText((1, Small, "GPA.1"), (2, Large, "GPA.2"), (3, Small, "GPA.3")))!;
        document.items[2].state = "refunded";
        PurchaseLedger ledger = PurchaseLedger.Read(document).Ledger;
        var made = new PurchasesMade(Array.Empty<(string, int)>(), Array.Empty<string>(), Numbers(1));

        Assert.Equal(new[] { 2L }, ledger.NotDelivered(made).Select(entry => entry.Number.Value));
    }
}
