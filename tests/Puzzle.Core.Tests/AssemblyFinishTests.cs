using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the fresh games make of a chosen candidate, on games written out by hand: a game finishes on the move the
/// test's script gives its purpose, seed and repeat, so every share below can be counted on fingers.
/// </summary>
public class AssemblyFinishTests
{
    private const byte Never = 60;

    /// <summary>A Normal level: rung 55 should win 0.75 of its first attempts within 20 to 33 moves.</summary>
    private static readonly LevelSlot Level = AssemblySupport.Slot(57);

    private static readonly CheckSettings Checks = AssemblySupport.QuickChecks with { ConfirmSeeds = 4, FirstTryTolerance = 1, CheckGamesPerSeed = 0 };

    private static readonly PoolRules Pool = new(Size: 4, MinSize: 2, Tolerance: 0.07);

    /// <summary>Five of six confirm games finish on move 22: the limit is 22 and every seed plays alike.</summary>
    private static byte ConfirmedAt22(int repeat) => repeat % 6 == 0 ? Never : (byte)22;

    [Fact]
    public void A_candidate_whose_fresh_games_hold_the_share_becomes_the_level_with_what_the_audit_measured()
    {
        var games = new ScriptedGames((purpose, _, repeat) => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : repeat % 4 == 0 ? Never : (byte)22);

        FinishedLevel level = Accepted(Finish(games));

        Assert.Equal((22, 22, 22), (level.ConfirmLimit, level.CalibratedLimit, level.Limit));
        Assert.Equal([0, 1, 2, 3], level.PoolSeeds);
        Assert.Equal((1, 24), (level.Audits, level.AuditGames)); // twelve on the first seed, four on each of the other three
        Assert.Equal((0.75, 0.75, 12), (level.Win, level.FirstTryWin, level.FirstTryGames));
        Assert.Equal((0.75, 0.75), (level.PoolMinWin, level.PoolMaxWin));
        Assert.Equal((22, (int?)Never), (level.MovesP50, level.MovesP90));
        Assert.Equal(0.75, level.WinPlus5);
        Assert.Equal(0.75, level.AuditTarget);
    }

    [Fact]
    public void An_audit_is_played_on_repeats_no_confirm_game_and_no_earlier_audit_has_used()
    {
        var games = new ScriptedGames((purpose, _, repeat) => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : repeat < 2012 ? (byte)26 : repeat % 4 == 0 ? Never : (byte)26);

        Finish(games);

        Assert.All(games.Asked.Where(asked => asked.Purpose == GamePurpose.Confirm), asked => Assert.Equal((1000, 6), (asked.RepeatFrom, asked.RepeatCount)));
        Assert.Equal([2000, 2000, 2012, 2012], games.Asked.Where(asked => asked.Purpose == GamePurpose.Audit).Select(asked => asked.RepeatFrom));
    }

    [Fact]
    public void A_failed_audit_becomes_games_the_limit_is_read_from_and_the_next_audit_checks_the_new_limit()
    {
        // The first audit finishes every game on move 26, four moves later than the confirm games promised.
        var games = new ScriptedGames((purpose, _, repeat) => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : repeat < 2012 ? (byte)26 : repeat % 4 == 0 ? Never : (byte)26);

        FinishedLevel level = Accepted(Finish(games));

        Assert.Equal((22, 26, 26), (level.ConfirmLimit, level.CalibratedLimit, level.Limit));
        Assert.Equal((2, 0.75), (level.Audits, level.Win));
    }

    [Fact]
    public void A_candidate_whose_audits_push_its_limit_out_of_the_move_range_is_turned_down()
    {
        var games = new ScriptedGames((purpose, _, repeat) => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : Never);

        FinishOutcome.Rejected rejected = Assert.IsType<FinishOutcome.Rejected>(Finish(games));

        Assert.Contains("audit 1", rejected.Reason);
        Assert.Contains("60", rejected.Reason);
        Assert.Contains("20-33", rejected.Reason);
    }

    [Fact]
    public void A_candidate_whose_confirm_games_put_the_limit_outside_the_range_is_turned_down_before_any_audit()
    {
        var games = new ScriptedGames((_, _, _) => 40);

        FinishOutcome.Rejected rejected = Assert.IsType<FinishOutcome.Rejected>(Finish(games));

        Assert.Contains("confirm", rejected.Reason);
        Assert.DoesNotContain(games.Asked, asked => asked.Purpose == GamePurpose.Audit);
    }

    [Fact]
    public void A_first_seed_that_plays_unlike_the_level_leaves_the_pool_and_the_next_seed_is_audited_in_its_place()
    {
        // Seed 0 never finishes in its audit games; the others play as the confirm games said.
        var games = new ScriptedGames((purpose, seed, repeat) =>
            purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat)
            : seed == 0 ? Never
            : repeat % 4 == 0 ? Never : (byte)22);

        FinishedLevel level = Accepted(Finish(games, Checks with { FirstTryTolerance = 0.5 }));

        Assert.Equal([1, 2, 3], level.PoolSeeds);
        Assert.Equal((2, 22, 0.75, 12), (level.Audits, level.Limit, level.FirstTryWin, level.FirstTryGames));
    }

    [Fact]
    public void A_seed_joins_the_pool_only_when_it_plays_like_the_level_at_its_limit()
    {
        // Seeds 0 and 1 win five of six within 22 moves, seed 2 every game, seed 3 four of six: the level wins 0.83, and only the first two are within seven points of it.
        var games = new ScriptedGames((purpose, seed, repeat) => seed switch
        {
            2 => 22,
            3 => repeat % 3 == 0 ? Never : (byte)22,
            _ => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : repeat % 4 == 0 ? Never : (byte)22,
        });

        FinishedLevel level = Accepted(Finish(games));

        Assert.Equal([0, 1], level.PoolSeeds);
    }

    [Fact]
    public void A_pool_at_its_smallest_size_takes_the_nearest_seed_in_reserve_when_its_first_seed_leaves()
    {
        // As above, seeds 0 and 1 make the pool and seeds 2 and 3 wait in reserve; then seed 0 never finishes in its audit games.
        var games = new ScriptedGames((purpose, seed, repeat) => seed switch
        {
            2 => 22,
            3 => repeat % 3 == 0 ? Never : (byte)22,
            0 when purpose == GamePurpose.Audit => Never,
            _ => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : repeat % 4 == 0 ? Never : (byte)22,
        });

        FinishedLevel level = Accepted(Finish(games, Checks with { FirstTryTolerance = 0.5 }, auditTolerance: 0.15));

        Assert.Equal([1, 2], level.PoolSeeds);
        Assert.Equal((2, 0.75, 1.0), (level.Audits, level.PoolMinWin, level.PoolMaxWin));
    }

    [Fact]
    public void A_candidate_is_turned_down_when_no_seed_is_left_to_take_a_leaving_first_seeds_place()
    {
        // Every seed's first-seed audit never finishes, and a pool of the whole shortlist has nothing in reserve.
        var games = new ScriptedGames((purpose, _, repeat) => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : Never);
        var wholeShortlist = new PoolRules(Size: 4, MinSize: 4, Tolerance: 0.07);

        FinishOutcome outcome = new Finisher(games, Checks with { FirstTryTolerance = 0.5 }, wholeShortlist, auditTolerance: 0.07, BankSupport.SomeEpoch, moveCap: 150)
            .Finish(Level, MeasuredCandidate());

        Assert.Contains("fewer than 4 seeds", Assert.IsType<FinishOutcome.Rejected>(outcome).Reason);
    }

    [Fact]
    public void A_mix_of_rungs_wins_the_weighted_share_of_its_rungs_games()
    {
        // The weaker rung never finishes, the stronger always on move 22: a mix of one to three wins 0.75 at 22.
        var games = new ScriptedGames((_, _, _) => 22, weakRung: "skill-45", weakFinish: Never);
        LevelSlot mixed = Level with { Difficulty = Level.Difficulty! with { Player = BankSupport.Player("skill-45*0.25+skill-65*0.75") } };

        FinishedLevel level = Accepted(new Finisher(games, Checks, Pool, auditTolerance: 0.07, BankSupport.SomeEpoch, moveCap: 150).Finish(mixed, MeasuredCandidate()));

        Assert.Equal((22, 0.75), (level.Limit, level.Win));
    }

    [Fact]
    public void The_win_share_a_report_states_is_read_from_games_played_after_the_audit_had_passed()
    {
        // The audit wins three games of four; the games after it, on repeats past every audit round, win one of two.
        const int FirstRepeatAfterAudits = 2000 + (2 * 12);
        var games = new ScriptedGames((purpose, _, repeat) =>
            purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat)
            : repeat >= FirstRepeatAfterAudits ? (repeat % 2 == 0 ? Never : (byte)22)
            : repeat % 4 == 0 ? Never : (byte)22);

        FinishedLevel level = Accepted(Finish(games, Checks with { CheckGamesPerSeed = 4 }));

        Assert.Equal((0.75, 0.5, 16), (level.Win, level.CheckWin, level.CheckGames));
        Assert.Equal(0.5, level.ReportedWin);
        Assert.Equal((GamePurpose.Audit, FirstRepeatAfterAudits, 4), games.Asked[^1]);
    }

    [Fact]
    public void Without_games_after_the_audit_a_report_states_the_passed_audit()
    {
        var games = new ScriptedGames((purpose, _, repeat) => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : repeat % 4 == 0 ? Never : (byte)22);

        FinishedLevel level = Accepted(Finish(games));

        Assert.Null(level.CheckWin);
        Assert.Equal((0.75, 0), (level.ReportedWin, level.CheckGames));
    }

    [Fact]
    public void A_seed_people_recorded_attempts_on_comes_first_in_the_pool_while_it_plays_like_the_level()
    {
        var games = new ScriptedGames((purpose, _, repeat) => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : repeat % 4 == 0 ? Never : (byte)22);
        BankCandidate candidate = MeasuredCandidate();
        LevelSlot recorded = Level with { RecordedSeeds = [candidate.Seeds[2]] };

        FinishedLevel level = Accepted(new Finisher(games, Checks, Pool, auditTolerance: 0.07, BankSupport.SomeEpoch, moveCap: 150).Finish(recorded, candidate));

        Assert.Equal([2, 0, 1, 3], level.PoolSeeds);
    }

    [Fact]
    public void A_recorded_seed_that_plays_unlike_the_level_is_not_kept_in_the_pool_for_being_recorded()
    {
        // Seed 3 wins four of six within 22 moves where the level wins five of six: more than seven points off.
        var games = new ScriptedGames((purpose, seed, repeat) => seed switch
        {
            2 => 22,
            3 => repeat % 3 == 0 ? Never : (byte)22,
            _ => purpose == GamePurpose.Confirm ? ConfirmedAt22(repeat) : repeat % 4 == 0 ? Never : (byte)22,
        });
        BankCandidate candidate = MeasuredCandidate();
        LevelSlot recorded = Level with { RecordedSeeds = [candidate.Seeds[3]] };

        FinishedLevel level = Accepted(new Finisher(games, Checks, Pool, auditTolerance: 0.07, BankSupport.SomeEpoch, moveCap: 150).Finish(recorded, candidate));

        Assert.Equal([0, 1], level.PoolSeeds);
    }

    private static FinishOutcome Finish(ScriptedGames games, CheckSettings? checks = null, double auditTolerance = 0.07) =>
        new Finisher(games, checks ?? Checks, Pool, auditTolerance, BankSupport.SomeEpoch, moveCap: 150).Finish(Level, MeasuredCandidate());

    private static FinishedLevel Accepted(FinishOutcome outcome) => outcome is FinishOutcome.Accepted accepted
        ? accepted.Level
        : throw new Xunit.Sdk.XunitException($"The candidate was turned down: {((FinishOutcome.Rejected)outcome).Reason}");

    /// <summary>A candidate with four seeds whose stored ladder games all finish on move 22, so the seeds rank alike and are shortlisted in seed order.</summary>
    private static BankCandidate MeasuredCandidate()
    {
        BankCandidate candidate = BankSupport.AsBankCandidate(CandidateContent.Of(BankSupport.FixedLevel()), seeds: 4);
        foreach (int skill in new[] { 45, 55, 65 })
        {
            candidate.Add(BankSupport.Games(candidate.Content.Id, skill, [22, 22, 22, 22]));
        }

        return candidate;
    }

    /// <summary>A game source that plays nothing: every game finishes on the move the script gives it.</summary>
    private sealed class ScriptedGames(Func<GamePurpose, int, int, byte> finishOf, string? weakRung = null, byte weakFinish = 0) : IGameSource
    {
        public List<(GamePurpose Purpose, int RepeatFrom, int RepeatCount)> Asked { get; } = [];

        public SeedGames Games(BankCandidate candidate, PlayerModel rung, GamePurpose purpose, IReadOnlyList<int> seedIndices, int repeatFrom, int repeatCount)
        {
            Asked.Add((purpose, repeatFrom, repeatCount));
            var finishes = new byte[seedIndices.Count * repeatCount];
            for (int game = 0; game < finishes.Length; game++)
            {
                finishes[game] = rung.Name == weakRung ? weakFinish : finishOf(purpose, seedIndices[game / repeatCount], repeatFrom + (game % repeatCount));
            }

            return new SeedGames(seedIndices, repeatCount, finishes, Enumerable.Repeat(RungGames.NeverReached, finishes.Length).ToArray());
        }
    }
}
