using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// <see cref="UstaPolicy"/>: the fair search bot. It may plan on sampled futures, but must decide from what a player
/// can know, never from the live game's future or the moves left, and must play reproducibly.
/// </summary>
public class UstaPolicyTests
{
    [Fact]
    public void Usta_chooses_the_same_move_on_the_same_board_whatever_the_live_refills()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        for (ulong boardSeed = 1; boardSeed <= 3; boardSeed++)
        {
            Game liveA = Game.Start(level, AttemptSeeds.WithFixedBoard(boardSeed, 100));
            Game liveB = Game.Start(level, AttemptSeeds.WithFixedBoard(boardSeed, 200));

            Move moveA = new UstaPolicy().ChooseMove(liveA, liveA.FindLegalMoves(), new Rng(boardSeed));
            Move moveB = new UstaPolicy().ChooseMove(liveB, liveB.FindLegalMoves(), new Rng(boardSeed));

            Assert.Equal(moveA, moveB);
        }
    }

    [Fact]
    public void Choosing_a_move_leaves_the_live_game_and_its_future_unchanged()
    {
        Game live = Game.Start(GameTests.ObstacleLevel(), seed: 7);
        Game control = live.CreateReplica();

        Move chosen = new UstaPolicy().ChooseMove(live, live.FindLegalMoves(), new Rng(3));

        Assert.Equal(0, live.MovesUsed);
        Assert.Equal(PlayChosenThenFirstLegalMoves(control, chosen, 20), PlayChosenThenFirstLegalMoves(live, chosen, 20));
    }

    [Fact]
    public void A_single_legal_move_is_returned_without_drawing_randomness()
    {
        Game game = Game.Start(GameTests.OpenLevel(), seed: 7);
        Move only = game.FindLegalMoves()[0];
        var rng = new Rng(11);
        Rng untouched = rng.Clone();
        var usta = new UstaPolicy();

        Move chosen = usta.ChooseMove(game, new[] { only }, rng);

        Assert.Equal(only, chosen);
        Assert.Equal(untouched.NextULong(), rng.NextULong());
        Assert.Equal(0, usta.LastDecisionPlays);
    }

    [Fact]
    public void A_winning_move_is_played_at_once()
    {
        // Swapping (2,4) down completes the purple goal; swapping (6,0) down matches orange, which wins nothing.
        string[] tiles = Filler(7, 7).With(0, 5, 'p').With(1, 5, 'p').With(2, 4, 'p').With(4, 1, 'o').With(5, 1, 'o').With(6, 0, 'o');
        Game game = Start(Level(tiles, goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }));
        IReadOnlyList<Move> legalMoves = game.FindLegalMoves();
        var usta = new UstaPolicy();

        Move chosen = usta.ChooseMove(game, legalMoves, new Rng(1));

        Game check = game.CreateReplica();
        Assert.True(check.TryPlay(chosen));
        Assert.Equal(GameStatus.Won, check.Status);
        Assert.True(legalMoves.Count > 1, "the test needs a choice");
        Assert.Equal(legalMoves.Count, usta.LastDecisionPlays); // the prior found the win: no simulation ran
    }

    [Fact]
    public void Usta_searches_beyond_the_prior_on_an_ordinary_move()
    {
        Game game = Game.Start(GameTests.ObstacleLevel(), seed: 7);
        IReadOnlyList<Move> legalMoves = game.FindLegalMoves();
        var usta = new UstaPolicy();

        usta.ChooseMove(game, legalMoves, new Rng(1));

        // The prior, then 24 simulations, each one imagined play plus a sweep over the replies.
        Assert.True(usta.LastDecisionPlays > legalMoves.Count + 2 * UstaSettings.Default.Simulations, $"{usta.LastDecisionPlays} plays");
    }

    [Fact]
    public void Usta_plays_a_legal_move_on_the_last_move_of_a_game()
    {
        Game game = Game.Start(GameTests.ObstacleLevel(), seed: 7, new GameOptions { MoveLimitOverride = 1 });

        Move chosen = new UstaPolicy().ChooseMove(game, game.FindLegalMoves(), new Rng(1));

        Assert.True(game.TryPlay(chosen));
    }

    [Theory]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void Usta_plays_the_same_moves_whatever_the_moves_left(ulong seed)
    {
        LevelDefinition level = GameTests.ObstacleLevel();

        (RunResult open, IReadOnlyList<Move> openMoves) = PlayRecorded(level, seed, moveCap: 150);
        Assert.Equal(GameStatus.Won, open.FinalStatus);

        // Every limit short of the win, down to a single move: each game's last decision is made with one move left.
        for (int cap = 1; cap < openMoves.Count; cap++)
        {
            (RunResult tight, IReadOnlyList<Move> tightMoves) = PlayRecorded(level, seed, cap);
            Assert.Equal(GameStatus.LostOutOfMoves, tight.FinalStatus);
            Assert.Equal(openMoves.Take(cap), tightMoves);
        }
    }

    [Fact]
    public void A_reused_policy_plays_like_a_fresh_one()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        var reused = new UstaPolicy();
        SimulationRunner.Play(level, reused, seed: 1, moveCap: 150);

        RunResult second = SimulationRunner.Play(level, reused, seed: 2, moveCap: 150);
        RunResult fresh = SimulationRunner.Play(level, new UstaPolicy(), seed: 2, moveCap: 150);

        Assert.Equal(fresh.CompletedAtMove, second.CompletedAtMove);
        Assert.Equal(fresh.ProgressAfterMove, second.ProgressAfterMove);
    }

    [Fact]
    public void Usta_games_are_reproducible_from_their_seed()
    {
        LevelDefinition level = GameTests.ObstacleLevel();

        RunResult first = SimulationRunner.Play(level, new UstaPolicy(), seed: 5, moveCap: 150);
        RunResult second = SimulationRunner.Play(level, new UstaPolicy(), seed: 5, moveCap: 150);

        Assert.Equal(first.CompletedAtMove, second.CompletedAtMove);
        Assert.Equal(first.ProgressAfterMove, second.ProgressAfterMove);
        Assert.Equal(first.Totals.GemsCleared, second.Totals.GemsCleared);
    }

    [Fact]
    public void Usta_needs_no_more_moves_than_the_expert_on_paired_seeds()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        var expert = new GreedyPolicy(SkillProfile.Expert);
        var usta = new UstaPolicy();
        int expertMoves = 0;
        int ustaMoves = 0;

        for (ulong seed = 0; seed < 10; seed++)
        {
            expertMoves += MovesToWin(SimulationRunner.Play(level, expert, seed, moveCap: 150));
            ustaMoves += MovesToWin(SimulationRunner.Play(level, usta, seed, moveCap: 150));
        }

        Assert.True(ustaMoves <= expertMoves, $"usta {ustaMoves} moves, expert {expertMoves} moves over 10 games");
    }

    [Fact]
    public void Halving_spends_twenty_four_simulations_on_eight_arms_as_eight_four_two()
    {
        HalvingPlan plan = HalvingPlan.For(candidates: 8, budget: 24);

        Assert.Equal(8, plan.Arms);
        Assert.Equal(new[] { new HalvingRound(8, 1), new HalvingRound(4, 2), new HalvingRound(2, 4) }, plan.Rounds);
        Assert.Equal(24, plan.Simulations);
    }

    [Theory]
    [MemberData(nameof(CandidatesAndBudgets))]
    public void Halving_never_spends_more_than_the_budget_and_ends_with_one_arm(int candidates, int budget)
    {
        HalvingPlan plan = HalvingPlan.For(candidates, budget);

        Assert.InRange(plan.Simulations, 1, budget);
        Assert.InRange(plan.Arms, 2, candidates);
        Assert.All(plan.Rounds, round => Assert.True(round.SamplesPerArm >= 1));
        Assert.Equal(plan.Arms, plan.Rounds[0].Arms);
        Assert.Equal(2, plan.Rounds[^1].Arms); // the last round picks one of two
    }

    public static IEnumerable<object[]> CandidatesAndBudgets() =>
        from candidates in new[] { 2, 3, 5, 8 }
        from budget in new[] { 2, 6, 12, 24, 48 }
        select new object[] { candidates, budget };

    [Theory]
    [InlineData(24, 8)]
    [InlineData(24, 5)]
    [InlineData(48, 7)]
    [InlineData(12, 3)]
    public void One_decision_runs_exactly_the_planned_simulations(int simulations, int rootWidth)
    {
        Game game = Game.Start(GameTests.ObstacleLevel(), seed: 7);
        IReadOnlyList<Move> legalMoves = game.FindLegalMoves();
        var usta = new UstaPolicy(UstaSettings.Default with { Simulations = simulations, RootWidth = rootWidth });

        usta.ChooseMove(game, legalMoves, new Rng(1));

        HalvingPlan plan = HalvingPlan.For(Math.Min(legalMoves.Count, rootWidth), simulations);
        Assert.True(plan.Arms > 2, "with more than two arms, the survivors of one round decide the next round's cost");
        Assert.Equal(plan.Simulations, usta.LastDecisionSimulations);
    }

    [Fact]
    public void A_single_candidate_needs_no_simulation()
    {
        HalvingPlan plan = HalvingPlan.For(candidates: 1, budget: 24);

        Assert.Equal(1, plan.Arms);
        Assert.Empty(plan.Rounds);
    }

    [Fact]
    public void Settings_reject_values_outside_their_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UstaSettings { Simulations = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UstaSettings { Simulations = 4097 });
        Assert.Throws<ArgumentOutOfRangeException>(() => UstaSettings.Default with { RootWidth = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => UstaSettings.Default with { SpecialSlots = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => UstaSettings.Default with { FollowUpWeight = 1.01 });
        Assert.Throws<ArgumentOutOfRangeException>(() => UstaSettings.Default with { FollowUpWeight = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => UstaSettings.Default with { FollowUpWinValue = double.PositiveInfinity });
        Assert.Equal(4096, (UstaSettings.Default with { Simulations = 4096 }).Simulations);
        Assert.Equal(0, (UstaSettings.Default with { SpecialSlots = 0 }).SpecialSlots);
    }

    [Fact]
    public void Usta_is_named_after_its_settings()
    {
        Assert.Equal("usta", new UstaPolicy().Name);
        Assert.Equal("usta-48", new UstaPolicy(UstaSettings.Default with { Simulations = 48 }).Name);
        Assert.Equal("usta-custom", new UstaPolicy(UstaSettings.Default with { RootWidth = 6 }).Name);
        Assert.Equal("usta-custom", new UstaPolicy(UstaSettings.Default with { Simulations = 48, FollowUpWeight = 0.5 }).Name);
    }

    [Theory]
    [InlineData("usta", "usta")]
    [InlineData("USTA-48", "usta-48")]
    [InlineData("usta-2", "usta-2")]
    [InlineData("usta-4096", "usta-4096")]
    [InlineData("usta-24", "usta")]
    public void Bot_names_create_usta_with_the_named_budget(string name, string expectedName)
    {
        Assert.True(BotPolicies.TryCreate(name, out IBotPolicy policy));
        Assert.IsType<UstaPolicy>(policy);
        Assert.Equal(expectedName, policy.Name);
    }

    [Theory]
    [InlineData("usta-0")]
    [InlineData("usta-1")]
    [InlineData("usta-x")]
    [InlineData("usta-99999")]
    [InlineData("usta-4097")]
    [InlineData("usta-")]
    [InlineData("usta-+48")]
    [InlineData("usta48")]
    public void Malformed_usta_names_are_rejected(string name)
    {
        Assert.False(BotPolicies.TryCreate(name, out _));
    }

    [Fact]
    public void A_missing_bot_name_is_rejected_without_throwing()
    {
        Assert.False(BotPolicies.TryCreate(null, out IBotPolicy fallback));
        Assert.NotNull(fallback);
    }

    [Fact]
    public void Usta_is_a_measurement_bot_not_a_player_model()
    {
        Assert.Equal(new[] { "random", "novice", "casual", "average", "expert" }, BotPolicies.Names);
        Assert.Equal(new[] { "usta" }, BotPolicies.MeasurementNames);
    }

    private static List<ulong> PlayChosenThenFirstLegalMoves(Game game, Move chosen, int more)
    {
        Assert.True(game.TryPlay(chosen));
        var hashes = new List<ulong> { game.Board.ComputeHash() };
        for (int i = 0; i < more && game.Status == GameStatus.Playing; i++)
        {
            Assert.True(game.TryPlay(game.FindLegalMoves()[0]));
            hashes.Add(game.Board.ComputeHash());
        }

        return hashes;
    }

    private static int MovesToWin(RunResult run) =>
        run.CompletedAtMove ?? throw new InvalidOperationException($"Run {run.Seed} did not finish within the cap.");

    /// <summary>Plays one usta game with a move cap and returns it with the moves usta chose, in order.</summary>
    private static (RunResult Run, IReadOnlyList<Move> Moves) PlayRecorded(LevelDefinition level, ulong seed, int moveCap)
    {
        var recorder = new MoveRecorder(new UstaPolicy());
        RunResult run = SimulationRunner.Play(level, recorder, seed, moveCap);
        return (run, recorder.Moves);
    }

    /// <summary>Plays like the bot it wraps and remembers every move that bot chose.</summary>
    private sealed class MoveRecorder : IBotPolicy
    {
        private readonly IBotPolicy _bot;
        private readonly List<Move> _moves = new();

        public MoveRecorder(IBotPolicy bot) => _bot = bot;

        public string Name => _bot.Name;

        public IReadOnlyList<Move> Moves => _moves;

        public Move ChooseMove(Game game, IReadOnlyList<Move> legalMoves, Rng rng)
        {
            Move move = _bot.ChooseMove(game, legalMoves, rng);
            _moves.Add(move);
            return move;
        }
    }
}
