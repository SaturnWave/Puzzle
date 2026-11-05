using System.Threading;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Bot;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

public class BankGrowthTests
{
    private static readonly Demand WholeGame = Demand.FromPlan(RepoFiles.DefaultProfile, Enumerable.Range(51, 950), []);

    [Fact]
    public void The_planned_levels_fall_into_classes_and_every_level_is_in_exactly_one()
    {
        int[] levels = WholeGame.Classes.SelectMany(planClass => planClass.Levels).ToArray();

        Assert.Equal(Enumerable.Range(51, 950), levels.Order());
        Assert.Equal(["skill-55"], WholeGame.Aims.Select(aim => aim.Label));
        Assert.All(WholeGame.Classes, planClass =>
        {
            ProfileBand band = RepoFiles.DefaultProfile.BandFor(planClass.Levels[0]);
            Assert.Equal(band.WinRate.For(planClass.Tier), planClass.Share);
            Assert.Equal(band.Moves.For(planClass.Tier), planClass.Moves);
        });
        Assert.Equal(WholeGame.Classes.Count, WholeGame.Classes.Select(planClass => planClass.Label).Distinct().Count());
    }

    [Fact]
    public void Further_aims_join_the_plans_own_player_and_none_is_listed_twice()
    {
        Demand demand = Demand.FromPlan(RepoFiles.DefaultProfile, Enumerable.Range(51, 20), [BankSupport.Player("skill-45"), BankSupport.Player("skill-55")]);

        Assert.Equal(["skill-55", "skill-45"], demand.Aims.Select(aim => aim.Label));
    }

    [Fact]
    public void Levels_calibrated_by_a_bot_off_the_ladder_cannot_be_grown_for()
    {
        var refused = Assert.Throws<DemandException>(() => Demand.FromPlan(RepoFiles.DefaultProfile, Enumerable.Range(1, 10), []));

        Assert.Contains("'random'", refused.Message);
        Assert.Contains("Level 1 ", refused.Message);
    }

    [Fact]
    public void A_hook_level_asks_for_its_mechanic_at_the_strength_it_introduces()
    {
        var planner = new LevelPlanner(RepoFiles.DefaultProfile);

        StructureNeeds introducesBox3 = StructureNeeds.Of(planner.Plan(91));
        StructureNeeds afterwards = StructureNeeds.Of(planner.Plan(94));

        Assert.Equal((3, 3), (introducesBox3.MaxBoxHp, introducesBox3.RequiredBoxHp));
        Assert.Equal((3, 0), (afterwards.MaxBoxHp, afterwards.RequiredBoxHp));
        Assert.Contains("shows B3", introducesBox3.Label);
    }

    [Fact]
    public void A_structure_admits_a_candidate_that_is_neither_stronger_nor_busier_than_allowed_and_shows_what_is_featured()
    {
        CandidateFeatures candidate = CandidateContent.Of(BankSupport.FixedLevel()).Features; // 6x5, plain, 5 colours, B2 + J1 + C1
        var fitting = new StructureNeeds
        {
            Size = BankSupport.SizeOf("6x5"),
            PlainShape = true,
            Colors = 5,
            MaxBoxHp = 2,
            MaxJellyLayers = 1,
            MaxChainHp = 2,
            ItemsAllowed = true,
            MaxFamilies = 3,
            RequiredBoxHp = 0,
            RequiredJellyLayers = 0,
            RequiredChainHp = 0,
            ItemsRequired = false,
        };

        Assert.True(fitting.Admits(candidate));
        Assert.True((fitting with { RequiredBoxHp = 2 }).Admits(candidate));
        Assert.True((fitting with { PlainShape = false }).Admits(candidate));
        Assert.False((fitting with { MaxBoxHp = 1 }).Admits(candidate));                       // a two-hit box before it is unlocked
        Assert.False((fitting with { MaxBoxHp = 3, RequiredBoxHp = 3 }).Admits(candidate));    // the featured strength is not on the board
        Assert.False((fitting with { ItemsRequired = true }).Admits(candidate));
        Assert.False((fitting with { MaxFamilies = 2 }).Admits(candidate));
        Assert.False((fitting with { Colors = 6 }).Admits(candidate));
        Assert.False((fitting with { Size = BankSupport.SizeOf("6x6") }).Admits(candidate));
    }

    [Fact]
    public void The_family_sets_of_a_structure_share_out_all_of_its_boards_and_hold_what_is_featured()
    {
        Assert.All(WholeGame.Classes.Select(planClass => planClass.Structure).Distinct(), structure =>
        {
            IReadOnlyList<IReadOnlyList<ObstacleKind>> sets = KindMix.SetsFor(structure);

            Assert.NotEmpty(sets);
            Assert.Equal(1, sets.Sum(set => KindMix.TargetShare(structure, set)), precision: 9);
            Assert.All(sets, set => Assert.True(set.Count <= structure.MaxFamilies));
            Assert.All(sets, set => Assert.All(Enum.GetValues<ObstacleKind>().Where(kind => structure.Required(kind) > 0), kind => Assert.Contains(kind, set)));
        });
    }

    [Fact]
    public void Boards_of_collect_goals_alone_are_the_largest_single_kind_where_nothing_is_featured()
    {
        StructureNeeds late = StructureNeeds.Of(new LevelPlanner(RepoFiles.DefaultProfile).Plan(500));

        Assert.Equal(0.35, KindMix.TargetShare(late, []), precision: 9);
        Assert.Equal(0.10, KindMix.TargetShare(late, [ObstacleKind.Jelly]), precision: 9);
        Assert.Equal("collect only", KindMix.LabelOf([]));
    }

    [Fact]
    public void Every_family_set_a_class_can_ask_for_is_drawn_without_a_fault()
    {
        foreach (PlanClass planClass in WholeGame.Classes.DistinctBy(planClass => planClass.Structure))
        {
            StructureNeeds structure = planClass.Structure;
            foreach (IReadOnlyList<ObstacleKind> set in KindMix.SetsFor(structure))
            {
                var spec = new DrawSpec
                {
                    Size = structure.Size,
                    PlainShape = structure.PlainShape,
                    Colors = structure.Colors,
                    Families = set.Select(kind => new FamilyDraw(kind, structure.Unlocked(kind))).ToArray(),
                    CollectGoals = set.Count == 0 ? 2 : 0,
                    DensityStep = 0,
                    FullJelly = true,
                };

                CandidateContent?[] drawn = Enumerable.Range(1, 3).Select(seed => BoardDrawer.TryDraw(spec, (ulong)seed)).ToArray();

                Assert.Contains(drawn, board => board is not null && board.Features.Families.SequenceEqual(set));
            }
        }
    }

    [Fact]
    public void A_board_that_fits_two_classes_is_counted_for_one_of_them()
    {
        Demand demand = Demand.FromPlan(RepoFiles.DefaultProfile, Enumerable.Range(601, 100), []);
        PlanClass[] sameStructure = demand.Classes.GroupBy(planClass => planClass.Structure).First(classes => classes.Count() > 1).ToArray();
        StructureNeeds structure = sameStructure[0].Structure;
        BankCandidate candidate = BankSupport.AsBankCandidate(BankSupport.Drawn(BankSupport.CollectSpec(structure.Size.ToString(), structure.Colors), seed: 2));
        var catalog = new BankCatalog();
        catalog.Add(candidate);
        foreach (SkillProfile rung in SkillLadder.Rungs)
        {
            catalog.Add(BankSupport.Games(candidate.Content.Id, rung.NoticePercent, [25, 25, 25, 25])); // every limit is 25: inside both classes' ranges
        }

        ClassSupply[] rows = SupplyTable.Of(catalog, demand, new SpareRule(0, 1), new LimitBook(BankSupport.SomeEpoch))
            .Where(row => row.Class.Structure == structure)
            .ToArray();

        Assert.Equal(sameStructure.Length, rows.Length);
        Assert.All(rows, row => Assert.Equal(candidate, Assert.Single(row.Fitting)));
        Assert.Equal(1, rows.Sum(row => row.Allotted));
        Assert.Equal(rows.Sum(row => row.Needed) - 1, rows.Sum(row => row.Deficit));
    }

    [Fact]
    public void A_class_needs_a_board_for_each_level_and_spares_on_top()
    {
        Assert.Equal(100, new SpareRule(0.25, 4).NeededFor(80));  // a quarter more
        Assert.Equal(9, new SpareRule(0.25, 4).NeededFor(5));     // never fewer than four spares
        Assert.Equal(5, new SpareRule(0, 0).NeededFor(5));
    }

    [Fact]
    public void A_grown_bank_holds_measured_candidates_of_the_kinds_it_grew_for()
    {
        using var folder = new TemporaryBankFolder();
        Demand demand = Demand.FromPlan(RepoFiles.DefaultProfile, Enumerable.Range(121, 10), []);
        BankStore store = BankStore.Create(folder.Path, bankSeed: 9);
        var catalog = new BankCatalog();
        GrowthSummary summary;
        EpochId epoch;
        using (BankWriter writer = store.OpenWriter(catalog))
        {
            epoch = Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "a test");
            summary = new Grower(catalog, writer, demand, store.Identity, epoch, BankSupport.QuickGrowth).Run(new GrowthBudget(TimeSpan.FromMinutes(2), Candidates: 4), CancellationToken.None);
        }

        Assert.Equal(GrowthEnd.BudgetSpent, summary.End);
        Assert.True(summary.Stored >= 4);
        Assert.Equal(summary.Stored, catalog.Candidates.Count);
        Assert.All(catalog.Candidates, candidate =>
        {
            Assert.Contains(demand.Classes, planClass => planClass.Structure.Admits(candidate.Content.Features));
            Assert.Equal(["skill-45", "skill-55", "skill-65"], candidate.Games.Select(batch => batch.Header.Player.Name));
            Assert.All(candidate.Games, batch => Assert.Equal((GamePurpose.Breadth, 16, epoch), (batch.Header.Purpose, batch.Finishes.Length, batch.Header.Epoch)));
            Assert.Equal(Grower.PlaySeedsOf(candidate.Content.Id, 8), candidate.Seeds);
            Assert.IsType<CandidateOrigin.Drawn>(candidate.Origin);
        });
        Assert.True(Epochs.RequireSameGames(store, catalog, epoch, new Rng(3)).Agrees);
    }

    [Fact]
    public void A_stopped_and_restarted_grower_repeats_no_draw_and_the_bank_reads_back_what_both_runs_stored()
    {
        using var folder = new TemporaryBankFolder();
        Demand demand = Demand.FromPlan(RepoFiles.DefaultProfile, Enumerable.Range(121, 10), []);
        BankStore store = BankStore.Create(folder.Path, bankSeed: 9);

        BankCatalog first = GrowSome(store, demand, candidates: 3);
        long drawsOfFirstRun = first.NextDrawIndex;
        BankCatalog second = GrowSome(store, demand, candidates: 3);
        BankScan read = store.Read();

        Assert.True(second.Candidates.Count >= first.Candidates.Count + 3);
        Assert.Equal(second.Draws.Count, second.Draws.Select(draw => (draw.Draw, draw.Variant)).Distinct().Count());
        Assert.All(second.Draws.Skip(first.Draws.Count), draw => Assert.True(draw.Draw >= drawsOfFirstRun));
        Assert.True(read.IsSound);
        Assert.Equal(second.Candidates.Select(candidate => candidate.Content.Id.Value).Order(), read.Catalog.Candidates.Select(candidate => candidate.Content.Id.Value).Order());
        Assert.Equal(second.Draws, read.Catalog.Draws.Select((draw, index) => draw with { Spec = second.Draws[index].Spec }));
        Assert.Equal(read.Catalog.Candidates.Count, read.Shards.Sum(shard => shard.Records.Count(record => record.Type == RecordType.Board)));
        Assert.All(read.Catalog.Candidates, candidate => Assert.Equal(3, candidate.Games.Count));
    }

    [Fact]
    public void With_siblings_asked_for_a_board_with_an_amount_goal_is_stored_at_its_neighbouring_amounts_too()
    {
        using var folder = new TemporaryBankFolder();
        Demand demand = Demand.FromPlan(RepoFiles.DefaultProfile, Enumerable.Range(121, 10), []);
        BankStore store = BankStore.Create(folder.Path, bankSeed: 9);
        var catalog = new BankCatalog();
        using BankWriter writer = store.OpenWriter(catalog);
        EpochId epoch = Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "a test");
        GrowthSettings withSiblings = BankSupport.QuickGrowth with { Siblings = 1 };

        new Grower(catalog, writer, demand, store.Identity, epoch, withSiblings).Run(new GrowthBudget(TimeSpan.FromMinutes(2), Candidates: 6), CancellationToken.None);

        BankCandidate[][] families = catalog.Candidates.GroupBy(candidate => candidate.Content.Board).Select(board => board.ToArray()).ToArray();
        Assert.Contains(families, siblings => siblings.Length > 1);
        Assert.All(families.Where(siblings => siblings.Length > 1), siblings =>
        {
            Assert.All(siblings, sibling => Assert.True(sibling.Content.Features.IsScalable));
            Assert.Equal(siblings.Length, siblings.Select(sibling => sibling.AmountStep).Distinct().Count());
            Assert.True(siblings.Max(sibling => sibling.AmountStep) - siblings.Min(sibling => sibling.AmountStep) <= 2);
            Assert.All(siblings, sibling => Assert.Equal(siblings[0].Amounts.CollectTargets, sibling.Amounts.CollectTargets));
            Assert.All(siblings, sibling => Assert.Equal(sibling.Content.Id, AmountGrid.AtStep(siblings[0].Content, siblings[0].Amounts, sibling.AmountStep).Id));
        });
    }

    [Fact]
    public void A_grower_with_nothing_missing_draws_nothing()
    {
        using var folder = new TemporaryBankFolder();
        Demand nobody = Demand.FromPlan(RepoFiles.DefaultProfile, [], [BankSupport.Player("skill-55")]);
        BankStore store = BankStore.Create(folder.Path, bankSeed: 9);
        var catalog = new BankCatalog();
        using BankWriter writer = store.OpenWriter(catalog);
        EpochId epoch = Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "a test");

        GrowthSummary summary = new Grower(catalog, writer, nobody, store.Identity, epoch, BankSupport.QuickGrowth).Run(new GrowthBudget(Time: null, Candidates: 5), CancellationToken.None);

        Assert.Equal((GrowthEnd.DeepEnough, 0, 0), (summary.End, summary.Visits, summary.Stored));
        Assert.Empty(catalog.Draws);
    }

    [Fact]
    public void A_stop_from_outside_ends_the_run_before_the_next_draw()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 9);
        var catalog = new BankCatalog();
        using BankWriter writer = store.OpenWriter(catalog);
        EpochId epoch = Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "a test");
        using var stop = new CancellationTokenSource();
        var grower = new Grower(catalog, writer, WholeGame, store.Identity, epoch, BankSupport.QuickGrowth);

        GrowthSummary summary = grower.Run(new GrowthBudget(TimeSpan.FromMinutes(2), Candidates: 50), stop.Token, onVisit: _ => stop.Cancel());

        Assert.Equal((GrowthEnd.Stopped, 1), (summary.End, summary.Visits));
    }

    [Fact]
    public void A_target_counts_its_misses_in_a_row_and_starts_again_after_a_fit()
    {
        string target = VisitTrail.TargetLabel("a class", "Boxes+Jelly");
        DrawRecord Draw(long draw, int variant, bool fit) => new(
            draw, variant, target, "skill-55", RecordMapping.ToRecord(BankSupport.CollectSpec()), 1, 0, fit ? DrawOutcome.Stored : DrawOutcome.ProbedOut, null, null, null, fit, 0, null);

        VisitTrail missedTwice = VisitTrail.Of([Draw(0, 0, false), Draw(0, 1, false), Draw(1, 0, false)]);
        VisitTrail fitAtLast = VisitTrail.Of([Draw(0, 0, false), Draw(1, 0, false), Draw(1, 1, true)]);

        Assert.Equal(2, missedTwice.MissesInARow(VisitTrail.KeyOf("skill-55", target)));    // two visits, whatever their variants
        Assert.Equal(2, missedTwice.MissesInARow(VisitTrail.KeyOf("skill-55", "a class"))); // the class is counted with its family set
        Assert.Equal(0, missedTwice.MissesInARow(VisitTrail.KeyOf("skill-45", target)));
        Assert.Equal(0, fitAtLast.MissesInARow(VisitTrail.KeyOf("skill-55", target)));
    }

    [Theory]
    [InlineData(19, 0, false)] // too few visits to judge
    [InlineData(20, 0, true)]
    [InlineData(20, 1, false)] // one board in twenty is still worth asking for
    [InlineData(41, 2, true)]
    [InlineData(120, 20, false)] // a kind that fits one time in six, however long its runs of misses
    public void A_target_gives_too_little_once_twenty_visits_or_more_gave_it_fewer_than_one_board_in_twenty(int visits, int fits, bool tooLittle)
    {
        string target = VisitTrail.TargetLabel("a class", "Items");
        string otherSet = VisitTrail.TargetLabel("a class", "Boxes");
        DrawRecord Draw(long draw, string aimedAt, bool fit) => new(
            draw, 0, aimedAt, "skill-55", RecordMapping.ToRecord(BankSupport.CollectSpec()), 1, 0, fit ? DrawOutcome.Stored : DrawOutcome.ProbedOut, null, null, null, fit, 0, null);

        // The fits come first, so the misses are one long run: the rate decides, not the run.
        VisitTrail trail = VisitTrail.Of(Enumerable.Range(0, visits).Select(visit => Draw(visit, target, fit: visit < fits)).Append(Draw(visits, otherSet, fit: true)));

        Assert.Equal(tooLittle, trail.GivesTooLittle("skill-55", target));
        Assert.False(trail.GivesTooLittle("skill-55", otherSet));
        Assert.False(trail.GivesTooLittle("skill-45", target)); // another aim has its own record
    }

    [Fact]
    public void Classes_that_ask_the_same_colours_share_and_move_range_learn_together_what_a_family_set_gives()
    {
        const string Small = "10x10/6c/shaped/B2 J1 C1 I/max1/Normal 0.75 in 20-33";
        const string LargerAndStronger = "10x11/6c/plain/B3 J2 C2 I/max3/Normal 0.75 in 20-33";
        const string LongerRange = "10x10/6c/shaped/B2 J1 C1 I/max1/Normal 0.75 in 22-35";
        const string FewerColours = "10x10/5c/shaped/B2 J1 C1 I/max1/Normal 0.75 in 20-33";
        DrawRecord Missed(long draw) => new(
            draw, 0, VisitTrail.TargetLabel(Small, "Items"), "skill-55", RecordMapping.ToRecord(BankSupport.CollectSpec()), 1, 0, DrawOutcome.ProbedOut, null, null, null, false, 0, null);

        VisitTrail trail = VisitTrail.Of(Enumerable.Range(0, 20).Select(visit => Missed(visit)));

        Assert.Equal("6c/Normal 0.75 in 20-33", PlanClass.AskOf(Small));
        Assert.True(trail.GivesTooLittle("skill-55", VisitTrail.TargetLabel(LargerAndStronger, "Items")));
        Assert.False(trail.GivesTooLittle("skill-55", VisitTrail.TargetLabel(LargerAndStronger, "Boxes+Items")));
        Assert.False(trail.GivesTooLittle("skill-55", VisitTrail.TargetLabel(LongerRange, "Items")));
        Assert.False(trail.GivesTooLittle("skill-55", VisitTrail.TargetLabel(FewerColours, "Items")));
        Assert.Equal(0, trail.MissesInARow(VisitTrail.KeyOf("skill-55", LargerAndStronger))); // misses in a row stay each class's own
    }

    [Fact]
    public void A_grower_whose_short_classes_ask_only_for_kinds_no_draw_gives_ends_instead_of_drawing_on()
    {
        using var folder = new TemporaryBankFolder();
        Demand oneClass = Demand.FromPlan(RepoFiles.DefaultProfile, [60], []);
        PlanClass planClass = Assert.Single(oneClass.Classes);
        BankStore store = BankStore.Create(folder.Path, bankSeed: 9);
        var catalog = new BankCatalog();
        using BankWriter writer = store.OpenWriter(catalog);
        EpochId epoch = Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "a test");
        long draw = 0;
        foreach (IReadOnlyList<ObstacleKind> set in KindMix.SetsFor(planClass.Structure))
        {
            for (int visit = 0; visit < 20; visit++)
            {
                writer.Note(new DrawRecord(
                    draw++, 0, VisitTrail.TargetLabel(planClass.Label, KindMix.LabelOf(set)), "skill-55", RecordMapping.ToRecord(BankSupport.CollectSpec()), 1, 0, DrawOutcome.ProbedOut, null, null, null, false, 0, null));
            }
        }

        GrowthSummary summary = new Grower(catalog, writer, oneClass, store.Identity, epoch, BankSupport.QuickGrowth).Run(new GrowthBudget(Time: null, Candidates: 5), CancellationToken.None);

        Assert.Equal((GrowthEnd.OutOfReach, 0, 0), (summary.End, summary.Visits, summary.Stored));
        string named = Assert.Single(summary.OutOfReach);
        Assert.StartsWith($"{planClass.Label} for skill-55: ", named);
        Assert.Contains("draws do not give it collect only", named);
    }

    private static BankCatalog GrowSome(BankStore store, Demand demand, int candidates)
    {
        BankCatalog catalog = store.Read().Catalog;
        using BankWriter writer = store.OpenWriter(catalog);
        EpochId epoch = Epochs.ForWriting(catalog, writer, new EpochChoice.UseCurrent(), "a test");
        new Grower(catalog, writer, demand, store.Identity, epoch, BankSupport.QuickGrowth).Run(new GrowthBudget(TimeSpan.FromMinutes(2), candidates), CancellationToken.None);
        return catalog;
    }
}
