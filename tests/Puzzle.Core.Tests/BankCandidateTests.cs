using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Core.Tests;

public class BankCandidateTests
{
    /// <summary>
    /// The text a candidate's identity is hashed from, for <see cref="BankSupport.FixedLevel"/>. A change here means
    /// stored ids would no longer be found by content: start a new scheme in <c>CanonicalText</c> instead.
    /// </summary>
    private const string FixedLevelText =
        "c1\n6x5\ncells:######|######|######|######|######\ntiles:......|......|......|.1..2.|......\n"
        + "jelly:......|......|......|......|11..11\nchains:......|.1..1.|......|......|......\n"
        + "colors:5\nspawn:60,0,0\nitems:0,0,0\ngoals:BreakBoxes=2;CollectColor/1=30\n";

    [Fact]
    public void The_text_and_the_ids_of_a_fixed_level_are_pinned()
    {
        CandidateContent candidate = CandidateContent.Of(BankSupport.FixedLevel());

        Assert.Equal(FixedLevelText, CanonicalText.OfCandidate(candidate.Level));
        Assert.Equal("f96dba46716bff0b106affd8d636c675", candidate.Id.ToString());
        Assert.Equal("d40c2e27c1ae1ac958d9355bba9a89be", candidate.Board.ToString());
    }

    [Fact]
    public void A_candidates_identity_ignores_number_tier_limit_pool_and_colour_names()
    {
        CandidateContent candidate = CandidateContent.Of(BankSupport.FixedLevel());
        CandidateContent sameContent = CandidateContent.Of(BankSupport.FixedLevel(
            number: 412, tier: DifficultyTier.Easy, moves: 19, palette: BankSupport.OtherPalette, pool: [5u, 6u, 7u]));

        Assert.Equal(candidate.Id, sameContent.Id);
        Assert.Equal(candidate.Board, sameContent.Board);
        Assert.Equal(GemColors.First(5), sameContent.Level.Spawn.Palette);
        Assert.Empty(sameContent.Level.Attempts.SeedPool);
        Assert.Equal(candidate.Id, CandidateContent.Of(candidate.Level).Id);
    }

    [Fact]
    public void Another_amount_is_another_candidate_on_the_same_board_and_another_layout_is_another_board()
    {
        CandidateContent candidate = CandidateContent.Of(BankSupport.FixedLevel());
        CandidateContent otherAmount = CandidateContent.Of(BankSupport.FixedLevel(collectTarget: 38));
        CandidateContent otherLayout = CandidateContent.Of(BankSupport.FixedLevel(boxRow: ".2..1."));

        Assert.NotEqual(candidate.Id, otherAmount.Id);
        Assert.Equal(candidate.Board, otherAmount.Board);
        Assert.NotEqual(candidate.Board, otherLayout.Board);
    }

    [Fact]
    public void A_level_whose_palette_is_out_of_colour_order_is_no_candidate()
    {
        GemColor[] outOfOrder = [GemColor.Blue, GemColor.Red, GemColor.Green, GemColor.Yellow, GemColor.Purple];

        var refused = Assert.Throws<CandidateFormatException>(() => CandidateContent.Of(BankSupport.FixedLevel(palette: outOfOrder)));

        Assert.Contains("out of colour order", refused.Message);
    }

    [Fact]
    public void A_candidates_features_are_read_from_its_content()
    {
        CandidateFeatures features = CandidateContent.Of(BankSupport.FixedLevel()).Features;

        Assert.Equal((6, 5, 30, 5), (features.Width, features.Height, features.PlayableCells, features.Colors));
        Assert.Equal((2, 1, 1, false), (features.MaxBoxHp, features.MaxJellyLayers, features.MaxChainHp, features.HasItems));
        Assert.Equal([ObstacleKind.Boxes, ObstacleKind.Jelly, ObstacleKind.Chains], features.Families);
        Assert.True(features.IsPlain);
        Assert.True(features.IsScalable);
        Assert.Equal("6x5/plain/5c/B2+J1+C1/collect1", features.Kind);
    }

    /// <summary>
    /// The colour a bomb picks among equally common colours follows the colours' order, so the games only stay the
    /// same under a recolouring that keeps the order. Colour bombs do fire in these games.
    /// </summary>
    [Fact]
    public void A_recoloured_level_plays_the_very_games_of_its_candidate()
    {
        LevelDefinition level = BankSupport.FixedLevel(palette: BankSupport.OtherPalette, collectTarget: 120);
        LevelDefinition candidate = CandidateContent.Of(level).Level;
        int colourBombs = 0;

        foreach (int skill in new[] { 40, 100 })
        {
            for (uint seed = 1; seed <= 12; seed++)
            {
                RunResult original = LadderMeasurer.Play(level, new GreedyPolicy(BankSupport.Rung(skill).ToProfile()), seed, repeat: 0, moveCap: 150);
                RunResult recoloured = LadderMeasurer.Play(candidate, new GreedyPolicy(BankSupport.Rung(skill).ToProfile()), seed, repeat: 0, moveCap: 150);

                Assert.Equal(original.CompletedAtMove, recoloured.CompletedAtMove);
                Assert.Equal(original.ProgressAfterMove, recoloured.ProgressAfterMove);
                Assert.Equal(
                    (original.Totals.GemsCleared, original.Totals.SpecialsCreated, original.Totals.SpecialsActivated, original.Totals.Cascades, original.Totals.Shuffles),
                    (recoloured.Totals.GemsCleared, recoloured.Totals.SpecialsCreated, recoloured.Totals.SpecialsActivated, recoloured.Totals.Cascades, recoloured.Totals.Shuffles));
                colourBombs += original.Totals.ColorBombsCreated;
            }
        }

        Assert.True(colourBombs > 0, "the sample should include games in which a colour bomb was made");
    }

    [Fact]
    public void A_board_is_the_same_board_whenever_it_is_drawn_from_the_same_specification_and_seed()
    {
        DrawSpec spec = BankSupport.SpecWith("10x11", 6, new FamilyDraw(ObstacleKind.Boxes, 3), new FamilyDraw(ObstacleKind.Jelly, 2));

        CandidateContent once = BankSupport.Drawn(spec, seed: 21);
        CandidateContent again = BankSupport.Drawn(spec, seed: 21);
        CandidateContent other = BankSupport.Drawn(spec, seed: 22);

        Assert.Equal(once.Id, again.Id);
        Assert.NotEqual(once.Id, other.Id);
        Assert.Equal((10, 11, 6), (once.Features.Width, once.Features.Height, once.Features.Colors));
        Assert.Equal([ObstacleKind.Boxes, ObstacleKind.Jelly], once.Features.Families);
        Assert.Equal(GemColors.First(6), once.Level.Spawn.Palette);
    }

    /// <summary>
    /// Boards drawn by this version of the drawing code. When this fails because the drawing changed on purpose, raise
    /// <see cref="BoardDrawer.DrawCodeVersion"/> and pin the new ids: stored candidates keep the version they were drawn with.
    /// </summary>
    [Fact]
    public void The_drawing_code_of_this_version_draws_these_boards()
    {
        Assert.Equal(1, BoardDrawer.DrawCodeVersion);
        Assert.Equal("0bf3b8e0c016eeaf4cefd7a3f6b1f4ed", BankSupport.Drawn(BankSupport.CollectSpec("9x10", 5, collectGoals: 2), seed: 1).Id.ToString());
        Assert.Equal(
            "1bdf501df137341fa1ca8a7e37390031",
            BankSupport.Drawn(BankSupport.SpecWith("11x12", 7, new FamilyDraw(ObstacleKind.Chains, 2), new FamilyDraw(ObstacleKind.Items, 1)), seed: 2).Id.ToString());
    }

    [Fact]
    public void A_drawn_board_needs_no_plan_and_carries_the_families_it_was_asked_for()
    {
        foreach (ObstacleKind kind in Enum.GetValues<ObstacleKind>())
        {
            int strength = kind is ObstacleKind.Items ? 1 : 2;
            CandidateContent drawn = BankSupport.Drawn(BankSupport.SpecWith("10x10", 6, new FamilyDraw(kind, strength)), seed: 9);

            Assert.Equal([kind], drawn.Features.Families);
            Assert.InRange(drawn.Features.StrengthOf(kind), 1, strength);
        }
    }

    [Fact]
    public void Siblings_keep_their_board_and_move_their_amounts_along_the_grid()
    {
        CandidateContent board = BankSupport.Drawn(BankSupport.CollectSpec("9x9", 5, collectGoals: 2), seed: 4);
        AmountBase amounts = AmountGrid.BaseOf(board);

        CandidateContent more = AmountGrid.AtStep(board, amounts, 2);
        CandidateContent less = AmountGrid.AtStep(board, amounts, -3);

        Assert.Equal(board.Id, AmountGrid.AtStep(board, amounts, 0).Id);
        Assert.Equal(board.Board, more.Board);
        Assert.Equal(board.Board, less.Board);
        Assert.All(amounts.CollectTargets.Zip(TargetsOf(more)), pair => Assert.Equal((int)Math.Round(pair.First * 1.5625), pair.Second));
        Assert.All(amounts.CollectTargets.Zip(TargetsOf(less)), pair => Assert.Equal(Math.Max(6, (int)Math.Round(pair.First * 0.512)), pair.Second));
        Assert.Equal(more.Id, AmountGrid.AtStep(less, amounts, 2).Id); // from the stored base, whichever sibling is at hand
        Assert.Throws<ArgumentOutOfRangeException>(() => AmountGrid.AtStep(board, amounts, AmountGrid.LargestStep + 1));
    }

    [Fact]
    public void An_item_board_drops_what_lies_on_it_and_what_the_grid_lets_spawn()
    {
        CandidateContent board = BankSupport.Drawn(BankSupport.SpecWith("10x10", 6, new FamilyDraw(ObstacleKind.Items, 1)), seed: 1);
        int preset = board.Level.Layout.PresetItemCount;

        CandidateContent fewer = AmountGrid.AtStep(board, AmountGrid.BaseOf(board), AmountGrid.SmallestStep);
        CandidateContent more = AmountGrid.AtStep(board, AmountGrid.BaseOf(board), 3);

        Assert.Equal(5, AmountGrid.BaseOf(board).SpawnedItems);
        Assert.Equal((1, preset + 1), (fewer.Level.Spawn.Items.Total, fewer.Level.Goals.Single().Target));
        Assert.Equal((10, preset + 10, 3), (more.Level.Spawn.Items.Total, more.Level.Goals.Single().Target, more.Level.Spawn.Items.MaxOnBoard));
        Assert.Equal(board.Board, more.Board);
    }

    [Fact]
    public void A_board_of_obstacle_goals_alone_has_no_siblings()
    {
        CandidateContent board = BankSupport.Drawn(BankSupport.SpecWith("10x10", 6, new FamilyDraw(ObstacleKind.Boxes, 2)), seed: 6);

        Assert.False(board.Features.IsScalable);
        Assert.Equal(board.Id, AmountGrid.AtStep(board, AmountGrid.BaseOf(board), 4).Id);
    }

    [Theory]
    [InlineData(48, 24, -3)] // twice too long: three steps of 1.25 down
    [InlineData(12, 24, 3)]
    [InlineData(25, 24, -1)] // a miss always moves, if only one step
    [InlineData(23, 24, 1)]
    [InlineData(151, 24, -4)] // never more than a factor 2.5 at once
    public void The_amount_moves_toward_the_wanted_limit_in_grid_steps(int limit, int wanted, int steps) =>
        Assert.Equal(steps, AmountGrid.StepsToward(limit, wanted));

    [Fact]
    public void A_candidates_play_seeds_follow_from_its_identity()
    {
        CandidateContent candidate = CandidateContent.Of(BankSupport.FixedLevel());

        uint[] seeds = Grower.PlaySeedsOf(candidate.Id, 64);

        Assert.Equal(seeds, Grower.PlaySeedsOf(candidate.Id, 64));
        Assert.Equal(64, seeds.Distinct().Count());
        Assert.Equal(seeds[..8], Grower.PlaySeedsOf(candidate.Id, 8));
        Assert.NotEqual(seeds, Grower.PlaySeedsOf(CandidateContent.Of(BankSupport.FixedLevel(collectTarget: 31)).Id, 64));
    }

    private static IEnumerable<int> TargetsOf(CandidateContent candidate) =>
        candidate.Level.Goals.OfType<GoalSpec.CollectColor>().Select(goal => goal.Target);
}
