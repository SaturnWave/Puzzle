using Puzzle.Economy.Reporting;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// Whole runs of the economy simulator on a small hand-made game: a run is a function of its settings, its measurement
/// and its seed, and which stored game a player meets is a function of the player alone.
/// </summary>
public class EconomyRunTests
{
    private const string TakesEverything =
        """{ "player": { "extraMoves": { "watchesAds": true, "paysCoins": true }, "startBoosters": { "useAfterLossesOnLevel": 1, "buyAfterLossesOnLevel": 1 } }, "rewards": { "daily": { "boostersEachTime": 1 } }, "coins": { "perLevelWon": 150 }, "extraMovesOffer": { "freeAdsPerDay": 0 } }""";

    private static Dictionary<(int Level, int Attempt), GameMet> GamesMet(JourneyList journey) =>
        journey.Attempts.ToDictionary(attempt => (attempt.Level.Value, attempt.Attempt.Value), attempt => attempt.Met);

    [Fact]
    public void The_same_settings_measurement_and_seed_give_the_same_reports_byte_for_byte()
    {
        IReadOnlyDictionary<string, string> first = SmallWorld.Report(SmallWorld.Settings()).Files();
        IReadOnlyDictionary<string, string> again = SmallWorld.Report(SmallWorld.Settings()).Files();
        IReadOnlyDictionary<string, string> otherSeed = SmallWorld.Report(SmallWorld.Settings("""{ "players": { "seed": 2 } }""")).Files();

        Assert.Equal(
            ["boosters.csv", "coins.csv", "lives.csv", "offers.csv", "pace.csv", "purchases.csv", "settings.csv", "spending.csv", "stalls.csv", "summary.txt"],
            first.Keys);
        Assert.All(first, file => Assert.Equal(file.Value, again[file.Key]));
        Assert.All(first.Values, text => Assert.DoesNotContain('\r', text));
        Assert.NotEqual(first["stalls.csv"], otherSeed["stalls.csv"]);

        // The run really ran: everyone finished the thirty levels, and coins and boosters moved.
        EconomyReport report = SmallWorld.Report(SmallWorld.Settings("""{ "player": { "startBoosters": { "useAfterLossesOnLevel": 1 } } }"""));
        Assert.All(report.Pace.Where(row => row.Level.Value == 30), row => Assert.Equal(1.0, row.ReachedShare));
        Assert.Contains(report.Spending, row => row.ExtraMovesByAd > 0);
        Assert.Contains(report.Boosters, row => row.Used > 0);
    }

    [Fact]
    public void A_player_meets_the_same_games_under_two_settings_files()
    {
        EconomySettings longSessions = OnePlayer();
        EconomySettings shortSessions = OnePlayer("""{ "habit": { "sessionCeilingMinutes": 5 } }""");

        JourneyList one = Journey(longSessions, SmallWorld.Levels());
        JourneyList other = Journey(shortSessions, SmallWorld.Levels());

        Assert.Equal(
            one.Attempts.Select(attempt => (attempt.Level, attempt.Attempt, attempt.Met, attempt.IsWon)),
            other.Attempts.Select(attempt => (attempt.Level, attempt.Attempt, attempt.Met, attempt.IsWon)));
        Assert.True(other.Sessions.Count() > one.Sessions.Count());
        Assert.True(other.Attempts.Last().DecidedAt > one.Attempts.Last().DecidedAt);
    }

    [Fact]
    public void What_a_player_owns_or_spends_never_changes_the_game_they_meet()
    {
        var frugalAttempts = new List<AttemptPlayed>();
        var spendingAttempts = new List<AttemptPlayed>();
        foreach (SimulatedPlayer player in Enumerable.Range(1, 12).Select(seed => Player(seed: (ulong)seed)))
        {
            JourneyList frugal = Journey(OnePlayer(), SmallWorld.Levels(), player);
            JourneyList spending = Journey(OnePlayer(TakesEverything), SmallWorld.Levels(), player);
            Dictionary<(int Level, int Attempt), GameMet> metFrugal = GamesMet(frugal);
            Dictionary<(int Level, int Attempt), GameMet> metSpending = GamesMet(spending);
            (int Level, int Attempt)[] inBoth = metFrugal.Keys.Intersect(metSpending.Keys).ToArray();

            Assert.True(inBoth.Length >= SmallWorld.LevelCount);
            Assert.All(inBoth, attempt => Assert.Equal(metFrugal[attempt], metSpending[attempt]));
            frugalAttempts.AddRange(frugal.Attempts);
            spendingAttempts.AddRange(spending.Attempts);
        }

        // The journeys are not the same journeys: coins and boosters were used, and they won attempts.
        Assert.Contains(spendingAttempts, attempt => attempt.Reading.ExtraMoves is ExtraMovesDecision.TakenForCoins);
        Assert.Contains(spendingAttempts, attempt => attempt.StartBooster is StartBoosterDecision.Buy);
        Assert.Contains(spendingAttempts, attempt => attempt.StartBooster is StartBoosterDecision.UseOwned);
        Assert.True(spendingAttempts.Count < frugalAttempts.Count);

        // The game is picked from the player's seed, the level and the attempt's number: nothing else is asked for.
        Assert.Equal(
            ["attempt", "half", "level", "line", "player"],
            typeof(GameMet).GetMethod(nameof(GameMet.By))!.GetParameters().Select(parameter => parameter.Name).Order());
    }

    [Fact]
    public void What_is_stated_for_the_game_alone_changes_no_figure_and_is_listed_apart()
    {
        const string AnotherStartAndShop =
            """{ "startBoosters": { "perAttempt": 1 }, "shop": { "boosterSets": { "starter": { "priceCoins": 90000 } }, "adForCoins": { "coins": 999, "perDay": 20 } } }""";
        IReadOnlyDictionary<string, string> first = SmallWorld.Report(SmallWorld.Settings()).Files();
        IReadOnlyDictionary<string, string> otherGame = SmallWorld.Report(SmallWorld.Settings(AnotherStartAndShop)).Files();

        string[] differing = first.Keys.Where(file => first[file] != otherGame[file]).ToArray();

        // A player of the game would be far richer with these; a simulated player never meets one of them. (The welcome
        // gift of the start section is simulated: its tests are with the other rewards.)
        Assert.Equal(["settings.csv", EconomyReport.SummaryFile], differing);
        Assert.Contains("shop.boosterSets.starter.priceCoins,90000,coins (the game's; not simulated),Placeholder\n", otherGame["settings.csv"]);
        Assert.Contains("lives.max,5,lives,Decided\n", otherGame["settings.csv"]);

        // The summary lists them after what the numbers rest on, under a heading of their own.
        string summary = otherGame[EconomyReport.SummaryFile];
        int restOn = summary.IndexOf("WHAT THE NUMBERS REST ON", StringComparison.Ordinal);
        int gameOnly = summary.IndexOf("STATED FOR THE GAME, NOT SIMULATED", StringComparison.Ordinal);
        int stated = summary.IndexOf("    shop.boosterSets.starter.priceCoins = 90000  (coins; Placeholder)", StringComparison.Ordinal);
        Assert.True(restOn >= 0 && gameOnly > restOn && stated > gameOnly, $"the headings stand at {restOn} and {gameOnly}, the game's set at {stated}");
        Assert.DoesNotContain("shop.boosterSets", summary[restOn..gameOnly]);
        Assert.Contains("extraMovesOffer.priceCoins = 900  (coins)", summary[restOn..gameOnly]);
    }

    [Fact]
    public void The_observed_figures_change_no_report()
    {
        IReadOnlyDictionary<string, string> first = SmallWorld.Report(SmallWorld.Settings()).Files();
        IReadOnlyDictionary<string, string> otherRecordings = SmallWorld
            .Report(SmallWorld.Settings("""{ "observed": { "players": 40, "medianAttemptSeconds": 300, "medianAttemptsPerSession": 2 } }"""))
            .Files();

        string[] differing = first.Keys.Where(file => first[file] != otherRecordings[file]).ToArray();

        // Only where the recordings are printed beside the model: the list of settings and the summary's line of them.
        Assert.Equal(["settings.csv", EconomyReport.SummaryFile], differing);
        Assert.Contains("40 people", otherRecordings[EconomyReport.SummaryFile]);
    }

    [Fact]
    public void Players_are_split_by_the_mix_exactly_and_keep_their_seed_whatever_else_changes()
    {
        PopulationSettings players = SmallWorld.Settings().Players;
        PopulationSettings otherHabit = SmallWorld.Settings("""{ "habit": { "sessionCeilingMinutes": 10 } }""").Players;

        IReadOnlyList<SimulatedPlayer> simulated = PopulationRun.PlayersOf(players);

        Assert.Equal(40, simulated.Count);
        Assert.Equal(Enumerable.Range(0, 40), simulated.Select(player => player.Index));
        Assert.Equal(
            Enumerable.Repeat("skill-45", 10).Concat(Enumerable.Repeat("skill-55", 20)).Concat(Enumerable.Repeat("skill-65", 10)),
            simulated.Select(player => player.Rung.Name));
        Assert.Equal(40, simulated.Select(player => player.Seed).Distinct().Count());
        Assert.Equal(simulated, PopulationRun.PlayersOf(otherHabit));
    }
}
