using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>The choice of a board for a level number, on hand-made candidates: no game is played and no file read.</summary>
public class AssemblyChoiceTests
{
    /// <summary>A Normal level after the hook levels of its room: boxes, jelly, chains and items are unlocked, one family to a board.</summary>
    private static readonly LevelSlot Level = AssemblySupport.Slot(57);

    [Fact]
    public void A_level_keeps_its_own_board_while_it_still_fits_whatever_the_others_would_cost()
    {
        ChoiceCandidate own = AssemblySupport.CandidateFor(Level, 1);
        ChoiceCandidate centred = AssemblySupport.CandidateFor(Level, 2);
        MoveRange moves = Level.Difficulty!.Moves;
        Chooser chooser = AssemblySupport.ChooserOver([own, centred], limits: new Dictionary<CandidateId, int> { [own.Id] = moves.Max, [centred.Id] = moves.Mid });

        Choice choice = chooser.Best(Level, incumbent: own)!;

        Assert.Equal((own.Id, true, moves.Max), (choice.Candidate.Id, choice.IsIncumbent, choice.DraftLimit));
    }

    [Fact]
    public void A_board_whose_limit_left_the_range_gives_way_to_a_candidate_that_fits()
    {
        ChoiceCandidate own = AssemblySupport.CandidateFor(Level, 1);
        ChoiceCandidate other = AssemblySupport.CandidateFor(Level, 2);
        Chooser chooser = AssemblySupport.ChooserOver([own, other], limits: new Dictionary<CandidateId, int> { [own.Id] = Level.Difficulty!.Moves.Max + 2 });

        Choice choice = chooser.Best(Level, incumbent: own)!;

        Assert.Equal((other.Id, false), (choice.Candidate.Id, choice.IsIncumbent));
    }

    [Fact]
    public void A_levels_own_board_drafted_one_move_outside_the_range_is_still_tried_and_a_new_board_is_not()
    {
        ChoiceCandidate board = AssemblySupport.CandidateFor(Level, 1);
        Chooser chooser = AssemblySupport.ChooserOver([board], limits: new Dictionary<CandidateId, int> { [board.Id] = Level.Difficulty!.Moves.Max + 1 });

        Assert.True(chooser.Best(Level, incumbent: board)!.IsIncumbent);
        Assert.Null(chooser.Best(Level, incumbent: null));
    }

    [Fact]
    public void Among_the_candidates_that_fit_the_one_nearest_the_middle_of_the_move_range_is_chosen()
    {
        MoveRange moves = Level.Difficulty!.Moves;
        ChoiceCandidate atTheEdge = AssemblySupport.CandidateFor(Level, 1);
        ChoiceCandidate nearTheMiddle = AssemblySupport.CandidateFor(Level, 2);
        Chooser chooser = AssemblySupport.ChooserOver(
            [atTheEdge, nearTheMiddle],
            limits: new Dictionary<CandidateId, int> { [atTheEdge.Id] = moves.Min, [nearTheMiddle.Id] = moves.Mid + 1 });

        Assert.Equal(nearTheMiddle.Id, chooser.Best(Level, incumbent: null)!.Candidate.Id);
    }

    [Fact]
    public void A_board_stands_at_one_level_number_only_whatever_its_amounts()
    {
        ChoiceCandidate first = AssemblySupport.CandidateFor(Level, 1);
        ChoiceCandidate sibling = first with { Id = new CandidateId(99) }; // the same board at another amount
        Chooser chooser = AssemblySupport.ChooserOver([first, sibling]);
        LevelSlot next = AssemblySupport.Slot(58);

        chooser.Occupy(Level.Number, Occupant.Of(first), first.Board, steered: true);

        Assert.Null(chooser.Best(next, incumbent: null));
        Assert.Equal(UnfilledCause.BoardsInUse, chooser.ShortageOf(next).Cause);
    }

    [Fact]
    public void A_board_held_back_for_its_own_level_is_not_given_to_another_until_it_is_released()
    {
        ChoiceCandidate own = AssemblySupport.CandidateFor(Level, 1);
        Chooser chooser = AssemblySupport.ChooserOver([own]);
        LevelSlot other = AssemblySupport.Slot(58);

        chooser.Reserve(own.Board, Level.Number);
        Choice? whileReserved = chooser.Best(other, incumbent: null);
        chooser.Release(own.Board, Level.Number);

        Assert.Null(whileReserved);
        Assert.Equal(own.Id, chooser.Best(other, incumbent: null)!.Candidate.Id);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void No_level_looks_like_a_level_within_the_novelty_window(int numbersApart, bool allowed)
    {
        ChoiceCandidate standing = AssemblySupport.CandidateFor(Level, 1, outline: "hourglass");
        ChoiceCandidate lookalike = AssemblySupport.CandidateFor(Level, 2, outline: "hourglass");
        Chooser chooser = AssemblySupport.ChooserOver([lookalike]);

        chooser.Occupy(Level.Number - numbersApart, Occupant.Of(standing), standing.Board, steered: true);

        Assert.Equal(allowed, chooser.Best(Level, incumbent: null) is not null);
        Assert.Equal(allowed ? 1 : 0, chooser.ShortageOf(Level).Fresh);
    }

    [Fact]
    public void A_candidate_the_fresh_games_turned_down_is_not_offered_again_to_any_level_that_asks_the_same()
    {
        ChoiceCandidate candidate = AssemblySupport.CandidateFor(Level, 1);
        Chooser chooser = AssemblySupport.ChooserOver([candidate]);
        LevelSlot asksTheSame = Level with { Number = 58 };
        LevelSlot asksForMore = Level with { Number = 59, Difficulty = Level.Difficulty! with { Share = 0.8 } };

        chooser.Reject(Level, candidate);

        Assert.Null(chooser.Best(Level, incumbent: candidate));
        Assert.Null(chooser.Best(asksTheSame, incumbent: null));
        Assert.Equal(candidate.Id, chooser.Best(asksForMore, incumbent: null)!.Candidate.Id);
    }

    [Theory]
    [InlineData(0.5, 10)]
    [InlineData(0.8, 4)]
    [InlineData(0.2, 16)]
    public void The_kinds_of_the_chosen_levels_approach_the_policys_target_shares(double collectOnlyShare, int expectedBoxLevels)
    {
        // Twenty Normal levels in a row would be the same slot twenty times; here the same slot is asked twenty times, far enough apart to be no neighbours.
        var candidates = new List<ChoiceCandidate>();
        for (int id = 1; id <= 40; id++)
        {
            candidates.Add(id % 2 == 0 ? AssemblySupport.CandidateFor(Level, id) : AssemblySupport.CandidateFor(Level, id, null, ObstacleKind.Boxes));
        }

        var targets = new Dictionary<LevelKind, double> { [LevelKind.CollectOnly] = collectOnlyShare, [LevelKind.Boxes] = 1 - collectOnlyShare };
        Chooser chooser = AssemblySupport.ChooserOver(candidates, targets);

        for (int level = 0; level < 20; level++)
        {
            LevelSlot slot = Level with { Number = 2000 + (level * 10) };
            ChoiceCandidate chosen = chooser.Best(slot, incumbent: null)!.Candidate;
            chooser.Occupy(slot.Number, Occupant.Of(chosen), chosen.Board, steered: true);
        }

        (int levels, IReadOnlyDictionary<LevelKind, int> byKind) = chooser.KindMix;
        Assert.Equal(20, levels);
        Assert.InRange(byKind.GetValueOrDefault(LevelKind.Boxes), expectedBoxLevels - 1, expectedBoxLevels + 1);
    }

    [Fact]
    public void A_rare_kind_is_brought_to_its_target_although_each_board_of_it_lies_at_the_edge_of_the_move_range()
    {
        // Item boards need many moves: their limits sit at the top of the range, where a choice by the middle alone never takes them.
        var candidates = new List<ChoiceCandidate>();
        var limits = new Dictionary<CandidateId, int>();
        for (int id = 1; id <= 110; id++)
        {
            bool dropsItems = id > 100;
            ChoiceCandidate candidate = dropsItems
                ? AssemblySupport.CandidateFor(Level, id, null, ObstacleKind.Items)
                : id % 2 == 0 ? AssemblySupport.CandidateFor(Level, id) : AssemblySupport.CandidateFor(Level, id, null, ObstacleKind.Boxes);
            candidates.Add(candidate);
            if (dropsItems)
            {
                limits[candidate.Id] = Level.Difficulty!.Moves.Max;
            }
        }

        var targets = new Dictionary<LevelKind, double> { [LevelKind.CollectOnly] = 0.5, [LevelKind.Boxes] = 0.44, [LevelKind.Items] = 0.06 };
        Chooser chooser = AssemblySupport.ChooserOver(candidates, targets, limits);

        for (int level = 0; level < 100; level++)
        {
            LevelSlot slot = Level with { Number = 2000 + (level * 10) };
            ChoiceCandidate chosen = chooser.Best(slot, incumbent: null)!.Candidate;
            chooser.Occupy(slot.Number, Occupant.Of(chosen), chosen.Board, steered: true);
        }

        Assert.InRange(chooser.KindMix.ByKind.GetValueOrDefault(LevelKind.Items), 5, 7);
        Assert.InRange(chooser.KindMix.ByKind.GetValueOrDefault(LevelKind.Boxes), 42, 46);
    }

    [Fact]
    public void A_pinned_level_is_a_neighbour_but_does_not_count_for_the_kind_mix()
    {
        ChoiceCandidate pinned = AssemblySupport.CandidateFor(Level, 1, null, ObstacleKind.Chains);
        Chooser chooser = AssemblySupport.ChooserOver([]);

        chooser.Occupy(50, Occupant.Of(pinned), pinned.Board, steered: false);

        Assert.Equal(0, chooser.KindMix.Levels);
        Assert.Empty(chooser.KindMix.ByKind);
    }

    [Fact]
    public void A_thin_kind_is_named_for_what_the_bank_is_short_of_and_never_filled_with_a_board_that_does_not_fit()
    {
        LevelSlot hook = AssemblySupport.Slot(71); // introduces two-layer jelly: its board must show it
        ChoiceCandidate oneLayerJelly = AssemblySupport.CandidateFor(AssemblySupport.Slot(74), 1, null, ObstacleKind.Jelly) with { Features = JellyOf(hook, layers: 1) };
        ChoiceCandidate tooLong = AssemblySupport.CandidateFor(hook, 2, null, ObstacleKind.Jelly);
        Chooser none = AssemblySupport.ChooserOver([]);
        Chooser wrongStrength = AssemblySupport.ChooserOver([oneLayerJelly]);
        Chooser outOfRange = AssemblySupport.ChooserOver([tooLong], limits: new Dictionary<CandidateId, int> { [tooLong.Id] = 99 });

        Assert.Equal(UnfilledCause.NoStructure, none.ShortageOf(hook).Cause);
        Assert.Null(wrongStrength.Best(hook, incumbent: null));
        Assert.Equal(UnfilledCause.NoStructure, wrongStrength.ShortageOf(hook).Cause);
        Assert.Null(outOfRange.Best(hook, incumbent: null));
        Shortage shortage = outOfRange.ShortageOf(hook);
        Assert.Equal((1, 0, UnfilledCause.NoLimitInRange), (shortage.WithStructure, shortage.InRange, shortage.Cause));
        Assert.Contains("shows J2", shortage.Describe(hook));
        Assert.Contains("skill-55", shortage.Describe(hook));
    }

    private static CandidateFeatures JellyOf(LevelSlot slot, int layers) =>
        AssemblySupport.CandidateFor(slot, 1, null, ObstacleKind.Jelly).Features with { MaxJellyLayers = layers };
}
