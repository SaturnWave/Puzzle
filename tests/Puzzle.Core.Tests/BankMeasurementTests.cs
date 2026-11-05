using System.IO;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Tests;

public class BankMeasurementTests
{
    private static readonly BankCandidate Measured = BankSupport.AsBankCandidate(BankSupport.Drawn(BankSupport.CollectSpec(), seed: 3), seeds: 16);
    private static readonly Lazy<RungGames[]> MeasuredGames = new(() => LadderMeasurer.Measure(BankSupport.BreadthOf(Measured, 40, 55, 100)));

    [Fact]
    public void Stored_games_are_the_games_the_simulation_runner_plays_on_the_seed_and_repeat()
    {
        RungGames games = MeasuredGames.Value[1];
        var policy = new GreedyPolicy(BankSupport.Rung(55).ToProfile());

        for (int game = 0; game < games.Header.GameCount; game++)
        {
            uint seed = Measured.Seeds[game / 2];
            RunResult direct = SimulationRunner.Play(Measured.Content.Level, policy, AttemptSeeds.From(seed), Rng.Derive(seed, (ulong)(game % 2)), 150);

            Assert.Equal(direct.CompletedAtMove ?? RungGames.StillPlaying, games.Finishes()[game]);
            Assert.Equal(FirstMoveAt(direct, 0.8f), games.ReachedAt(game, mark: 1));
        }
    }

    [Fact]
    public void Every_rung_plays_the_same_seeds_and_the_stronger_rung_finishes_sooner_on_the_whole()
    {
        RungGames[] games = MeasuredGames.Value;

        Assert.Equal(["skill-40", "skill-55", "skill-100"], games.Select(batch => batch.Header.Player.Name));
        Assert.All(games, batch => Assert.Equal(Enumerable.Range(0, 16), batch.Header.SeedIndices));
        Assert.All(games, batch => Assert.Equal(32, batch.Header.GameCount));
        Assert.True(MeanFinish(games[2]) < MeanFinish(games[0]), "the expert needs fewer moves than skill-40");
    }

    [Fact]
    public void A_rungs_limit_is_the_limit_the_level_report_reads_from_the_same_games()
    {
        var policy = new GreedyPolicy(BankSupport.Rung(55).ToProfile());
        RunResult[] runs = Enumerable.Range(0, 32)
            .Select(game => LadderMeasurer.Play(Measured.Content.Level, policy, Measured.Seeds[game / 2], repeat: game % 2, moveCap: 150))
            .ToArray();
        LevelReport report = LevelReport.From(runs, 150);
        WinCurve curve = WinCurve.Of(MeasuredGames.Value[1].Finishes(), 150);

        foreach (double share in new[] { 0.25, 0.40, 0.55, 0.75, 0.90, 0.95, 1.0 })
        {
            Assert.Equal(report.MoveLimitFor(share), curve.LimitFor(share));
        }

        Assert.All(Enumerable.Range(1, 150), limit => Assert.Equal(report.WinRateAt(limit), curve.ShareAt(limit)));
    }

    [Fact]
    public void A_curve_counts_unfinished_and_stuck_games_as_lost_under_every_limit()
    {
        WinCurve curve = WinCurve.Of([10, 12, 12, RungGames.StillPlaying, RungGames.Stuck, 20, 30, 149], moveCap: 150);

        Assert.Equal((8, 1), (curve.Games, curve.Stuck));
        Assert.Equal(0, curve.ShareAt(9));
        Assert.Equal(3 / 8.0, curve.ShareAt(12));
        Assert.Equal(6 / 8.0, curve.ShareAt(150));
        Assert.Equal(12, curve.LimitFor(0.375));
        Assert.Equal(149, curve.LimitFor(0.75));
        Assert.Null(curve.LimitFor(0.80));
    }

    [Fact]
    public void A_mixs_limit_is_where_its_weighted_share_reaches_the_target()
    {
        CandidateId candidate = Measured.Content.Id;
        var reading = new LadderReading(
        [
            BankSupport.Games(candidate, 45, [30, 30, 30, 30]),
            BankSupport.Games(candidate, 55, [20, 20, 30, 30]),
            BankSupport.Games(candidate, 65, [10, 10, 20, 20]),
        ]);
        PlayerMix mix = BankSupport.Player("skill-45*0.25+skill-55*0.5+skill-65*0.25");

        Assert.Equal(0.125, reading.ShareAt(mix, 10));        // half of skill-65's games, a quarter of the mix
        Assert.Equal(0.5, reading.ShareAt(mix, 20));          // all of skill-65's and half of skill-55's
        Assert.Equal(20, reading.LimitFor(mix, 0.5));
        Assert.Equal(30, reading.LimitFor(mix, 0.51));
        Assert.Equal(20, reading.LimitFor(BankSupport.Player("skill-55"), 0.5));
        Assert.Null(reading.LimitFor(BankSupport.Player("skill-55*0.5+skill-70*0.5"), 0.5)); // skill-70 was not measured
    }

    [Fact]
    public void A_draft_limit_averages_the_players_limit_with_its_neighbours_on_both_sides()
    {
        CandidateId candidate = Measured.Content.Id;
        var reading = new LadderReading(
        [
            BankSupport.Games(candidate, 45, [40]),
            BankSupport.Games(candidate, 50, [33]),
            BankSupport.Games(candidate, 55, [30]),
            BankSupport.Games(candidate, 60, [24]),
            BankSupport.Games(candidate, 65, [22]),
        ]);

        Assert.Equal(30, reading.DraftLimit(BankSupport.Player("skill-55"), 0.9));  // (30 + 33 + 24 + 40 + 22) / 5 = 29.8
        Assert.Equal(25, reading.DraftLimit(BankSupport.Player("skill-60"), 0.9));  // skill-70 was not measured, so one step either side: (24 + 30 + 22) / 3 = 25.3
        Assert.Equal(40, reading.DraftLimit(BankSupport.Player("skill-45"), 0.9));  // nothing measured below: its own limit
        Assert.Null(reading.DraftLimit(BankSupport.Player("skill-70"), 0.9));
    }

    [Fact]
    public void Seeds_are_judged_by_every_rung_at_its_own_limit_and_a_rung_without_a_limit_takes_no_part()
    {
        CandidateId candidate = Measured.Content.Id;
        var reading = new LadderReading(
        [
            BankSupport.Games(candidate, 55, [20, 22, 30, 31], repeats: 2),                                  // limit for a half: 22; seed 0 wins both, seed 1 none
            BankSupport.Games(candidate, 65, [15, 19, 16, 25], repeats: 2),                                  // limit for a half: 16; one game of each seed
            BankSupport.Games(candidate, 30, [RungGames.StillPlaying, RungGames.StillPlaying, 90, RungGames.Stuck], repeats: 2), // never wins a half
        ]);

        IReadOnlyList<SeedEstimate> seeds = reading.SeedShares(0.5);

        Assert.Equal([new SeedEstimate(0, 0.75, 4), new SeedEstimate(1, 0.25, 4)], seeds);
    }

    [Theory]
    [InlineData(0, 1920, true)]
    [InlineData(2, 1920, true)]   // two in 1,920 can still be one in 5,000 with bad luck
    [InlineData(3, 1920, false)]
    [InlineData(1, 1920, true)]
    [InlineData(1, 128, false)]  // one in 128 is too many to be one in 5,000
    [InlineData(2, 128, false)]
    [InlineData(40, 100_000, false)]
    public void Stuck_games_are_judged_by_their_rate_not_their_number(int stuck, int games, bool tolerated) =>
        Assert.Equal(tolerated, StuckRule.Tolerates(stuck, games));

    [Theory]
    [InlineData("skill-55", "skill-55")]
    [InlineData("novice", "skill-50")]
    [InlineData("EXPERT", "skill-100")]
    [InlineData("skill-65*0.25 + skill-45*0.25 + skill-55*0.5", "skill-45*0.25+skill-55*0.5+skill-65*0.25")]
    public void A_player_is_a_rung_or_a_weighted_mix_of_rungs(string text, string label)
    {
        Assert.True(PlayerMix.TryParse(text, out PlayerMix? mix, out _));
        Assert.Equal(label, mix.Label);
        Assert.Equal(1, mix.Parts.Sum(part => part.Weight), precision: 6);
    }

    [Theory]
    [InlineData("random")]
    [InlineData("usta")]
    [InlineData("skill-52")]
    [InlineData("skill-45*0.5+skill-55*0.4")]
    [InlineData("skill-55*0.5+skill-55*0.5")]
    [InlineData("skill-55*-1+skill-60*2")]
    public void What_is_not_on_the_ladder_or_does_not_sum_to_one_is_no_player(string text)
    {
        Assert.False(PlayerMix.TryParse(text, out _, out string? problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void A_player_moves_along_the_ladder_until_a_rung_would_leave_it()
    {
        PlayerMix mix = BankSupport.Player("skill-35*0.5+skill-95*0.5");

        Assert.Equal("skill-40*0.5+skill-100*0.5", mix.Shifted(5)?.Label);
        Assert.Null(mix.Shifted(10));
        Assert.Null(mix.Shifted(-10));
    }

    [Fact]
    public void Each_purpose_plays_its_own_repeats_and_a_request_outside_them_is_refused()
    {
        Assert.Equal([0, 1000, 2000, 3000], Enum.GetValues<GamePurpose>().Select(purpose => purpose.FirstRepeat()));

        MeasurementRequest confirmWithBreadthRepeats = BankSupport.BreadthOf(Measured, 55) with { Purpose = GamePurpose.Confirm };

        Assert.Throws<ArgumentOutOfRangeException>(() => LadderMeasurer.Measure(confirmWithBreadthRepeats));
    }

    [Fact]
    public void The_fingerprint_of_a_build_is_the_same_whenever_it_is_played()
    {
        Assert.Equal(MeasurementFingerprint.OfThisBuild, MeasurementFingerprint.Compute());
        Assert.Equal(64, MeasurementFingerprint.OfThisBuild.Length);
        Assert.NotEqual(MeasurementFingerprint.EpochOf("a", 0), MeasurementFingerprint.EpochOf("a", 1));
    }

    [Fact]
    public void An_empty_bank_opens_its_first_epoch_and_a_later_tool_of_the_same_build_works_in_it()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        var catalog = new BankCatalog();
        EpochId opened;
        using (BankWriter writer = store.OpenWriter(catalog))
        {
            opened = Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "first");
            Assert.Equal(opened, Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "again"));
        }

        BankCatalog read = store.Read().Catalog;

        Assert.Equal(opened, Epochs.Current(read));
        Assert.Equal(MeasurementFingerprint.OfThisBuild, Assert.Single(read.Epochs).Fingerprint);
    }

    [Fact]
    public void A_bank_played_with_other_rules_is_not_written_to_until_a_new_epoch_is_asked_for()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        var catalog = new BankCatalog();
        using BankWriter writer = store.OpenWriter(catalog);
        writer.Open(new EpochRecord("0011223344556677", "the fingerprint of another build", 0, DateTime.UtcNow, "runtime", "core", "bot", "another build"));
        long bytesBefore = new FileInfo(folder.OnlyShard).Length;

        var refused = Assert.Throws<EpochMismatchException>(() => Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "a test"));

        Assert.Contains("--new-epoch", refused.Message);
        Assert.Null(Epochs.Current(catalog));
        Assert.Equal(bytesBefore, new FileInfo(folder.OnlyShard).Length);

        EpochId own = Epochs.ForWriting(catalog, writer, new EpochChoice.OpenAnother(Leaving: null), "the engine changed on purpose");

        Assert.Equal(own, Epochs.Current(catalog));
        Assert.Equal("0011223344556677", catalog.EpochOf("the fingerprint of another build")?.Epoch); // the other build still finds its own
    }

    [Fact]
    public void A_build_that_has_an_epoch_opens_another_only_when_the_epoch_it_leaves_is_named()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        var catalog = new BankCatalog();
        using BankWriter writer = store.OpenWriter(catalog);
        EpochId own = Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "first");

        var refused = Assert.Throws<EpochMismatchException>(() => Epochs.ForWriting(catalog, writer, new EpochChoice.OpenAnother(Leaving: null), "by mistake"));
        Assert.Throws<EpochMismatchException>(() => Epochs.ForWriting(catalog, writer, new EpochChoice.OpenAnother("0011223344556677"), "the wrong epoch named"));

        Assert.Contains($"--new-epoch {own}", refused.Message);
        Assert.Equal(own, Epochs.Current(catalog));
        Assert.Single(catalog.Epochs);

        EpochId next = Epochs.ForWriting(catalog, writer, new EpochChoice.OpenAnother(own.ToString()), "a change the probe does not see");

        Assert.NotEqual(own, next);
        Assert.Equal(next, Epochs.Current(catalog));
    }

    [Fact]
    public void After_a_rule_change_a_held_board_gets_its_ladder_games_again_in_the_new_epoch_and_only_once()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        var catalog = new BankCatalog();
        using BankWriter writer = store.OpenWriter(catalog);
        BankCandidate candidate = BankSupport.AsBankCandidate(Measured.Content, seeds: 8);
        writer.Adopt(candidate, LadderMeasurer.Measure(BankSupport.BreadthOf(candidate, 45, 55, 65)));
        var afterTheChange = new EpochId(0x99);
        var remeasurer = new Remeasurer(writer, afterTheChange, BankSupport.QuickGrowth);

        Assert.False(candidate.ReadingIn(afterTheChange).IsUsable);
        Assert.Equal(3, remeasurer.RungsLacking(candidate).Count);
        Assert.Equal(RemeasureOutcome.Measured, remeasurer.Remeasure(candidate));
        Assert.Equal(RemeasureOutcome.AlreadyMeasured, remeasurer.Remeasure(candidate));

        BankCandidate read = Assert.Single(store.Read().Catalog.Candidates);
        Assert.True(read.ReadingIn(afterTheChange).IsUsable);
        Assert.Equal(["skill-45", "skill-55", "skill-65"], read.ReadingIn(afterTheChange).Rungs.Order());
        Assert.Equal(read.ReadingIn(BankSupport.SomeEpoch).Games, read.ReadingIn(afterTheChange).Games); // the same seeds and repeats as before
        Assert.Equal(1, Epochs.MeasuredIn(catalog, afterTheChange));
    }

    [Fact]
    public void A_single_rung_that_was_never_measured_is_filled_in_without_playing_the_others_again()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        var catalog = new BankCatalog();
        using BankWriter writer = store.OpenWriter(catalog);
        BankCandidate candidate = BankSupport.AsBankCandidate(Measured.Content, seeds: 8);
        writer.Adopt(candidate, LadderMeasurer.Measure(BankSupport.BreadthOf(candidate, 45, 65)));
        var remeasurer = new Remeasurer(writer, BankSupport.SomeEpoch, BankSupport.QuickGrowth);

        Assert.Equal(["skill-55"], remeasurer.RungsLacking(candidate).Select(rung => rung.Name));
        Assert.Equal(RemeasureOutcome.Measured, remeasurer.Remeasure(candidate));

        Assert.Equal(3, candidate.Games.Count);
    }

    [Fact]
    public void Boards_are_measured_again_in_the_order_of_their_use_levels_in_use_first_then_imported_ones_then_the_rest()
    {
        var catalog = new BankCatalog();
        BankCandidate drawn = Drawn(seed: 1);
        BankCandidate drawnAndInUse = Drawn(seed: 2);
        BankCandidate imported = BankSupport.AsBankCandidate(CandidateContent.Of(BankSupport.FixedLevel()));
        foreach (BankCandidate candidate in new[] { drawn, drawnAndInUse, imported })
        {
            catalog.Add(candidate);
        }

        IReadOnlyList<BankCandidate> order = Remeasurer.InOrder(catalog, new HashSet<CandidateId> { drawnAndInUse.Content.Id });

        Assert.Equal([drawnAndInUse.Content.Id, imported.Content.Id, drawn.Content.Id], order.Select(candidate => candidate.Content.Id));

        static BankCandidate Drawn(ulong seed)
        {
            CandidateContent content = BankSupport.Drawn(BankSupport.CollectSpec(), seed);
            return new BankCandidate(content, Grower.PlaySeedsOf(content.Id, 4), AmountGrid.BaseOf(content), amountStep: 0, new CandidateOrigin.Drawn(0, 0, seed, BankSupport.CollectSpec(), 1), DateTime.UnixEpoch);
        }
    }

    [Fact]
    public void A_bank_whose_shards_went_away_fails_a_read_as_a_read_failure_and_not_as_a_stack_trace_of_the_file_system()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        var catalog = new BankCatalog();
        BankCandidate candidate = BankSupport.AsBankCandidate(Measured.Content, seeds: 8);
        using (BankWriter writer = store.OpenWriter(catalog))
        {
            writer.Adopt(candidate, LadderMeasurer.Measure(BankSupport.BreadthOf(candidate, 55)));
        }

        RecordLocation location = candidate.Games[0].Location!;
        Directory.Delete(folder.ShardFolder, recursive: true); // the disk was unplugged

        var failed = Assert.Throws<BankReadException>(() => store.ReadGames(location));

        Assert.Contains(folder.Path, failed.Message);
        Assert.IsAssignableFrom<IOException>(failed.InnerException);
    }

    [Fact]
    public void Stored_games_replay_to_the_same_bytes_and_a_game_that_does_not_is_found()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        var catalog = new BankCatalog();
        RungGames[] games = LadderMeasurer.Measure(BankSupport.BreadthOf(Measured, 40, 55, 100));
        byte[] tampered = games[2].Bytes.ToArray();
        tampered[0] = (byte)(tampered[0] + 1); // as if the expert's first game had taken one move more under the old rules
        using (BankWriter writer = store.OpenWriter(catalog))
        {
            writer.Store(BankSupport.AsBankCandidate(Measured.Content, seeds: 16), [games[0], games[1], new RungGames(games[2].Header, tampered)], draw: SomeDraw());
        }

        BankCandidate stored = Assert.Single(catalog.Candidates);
        ReplayReport honest = SelfCheck.Replay(store, stored.Games.Take(2).Select(batch => (stored, batch)), seedsPerBatch: 4);
        ReplayReport all = SelfCheck.Replay(store, SelfCheck.Sample(catalog, BankSupport.SomeEpoch, candidates: 3, new Rng(1)), seedsPerBatch: 4);

        Assert.True(honest.Agrees);
        Assert.Equal((2, 16), (honest.Batches, honest.Games));
        Assert.Equal((3, 24, 1), (all.Batches, all.Games, all.DifferentGames));
        Assert.Equal(Measured.Content.Id, all.FirstDifferent);
        Assert.True(all.MeanFinishStored > all.MeanFinishReplayed);
        Assert.Throws<EpochMismatchException>(() => Epochs.RequireSameGames(store, catalog, BankSupport.SomeEpoch, new Rng(1)));
    }

    [Fact]
    public void Games_of_a_rung_whose_settings_changed_since_are_not_read_as_that_rungs()
    {
        BankCandidate candidate = BankSupport.AsBankCandidate(CandidateContent.Of(BankSupport.FixedLevel()));
        StoredGames current = BankSupport.Games(candidate.Content.Id, 55, [20, 21]);
        StoredGames stale = current with { Header = current.Header with { Player = new PlayerModel("skill-60", 60, 99.0) }, Finishes = [50, 51] };
        var catalog = new BankCatalog();
        catalog.Add(candidate);
        catalog.Add(current);
        catalog.Add(stale);

        LadderReading reading = candidate.ReadingIn(BankSupport.SomeEpoch);

        Assert.Equal(["skill-55"], reading.Rungs);
        Assert.Empty(candidate.ReadingIn(new EpochId(1)).Rungs); // nor are games of another epoch
    }

    private static DrawRecord SomeDraw() => new(
        0, 0, "a class|collect only", "skill-55", RecordMapping.ToRecord(BankSupport.CollectSpec()), 3, 0, DrawOutcome.Stored, Measured.Content.Id.ToString(), null, null, false, 0, null);

    private static byte FirstMoveAt(RunResult run, float share)
    {
        for (int move = 0; move < run.ProgressAfterMove.Count; move++)
        {
            if (run.ProgressAfterMove[move] >= share)
            {
                return (byte)move;
            }
        }

        return RungGames.NeverReached;
    }

    private static double MeanFinish(RungGames games) =>
        games.Finishes().Average(finish => finish is RungGames.StillPlaying or RungGames.Stuck ? 151 : finish);
}
