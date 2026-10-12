using System.IO;
using Puzzle.Bot;
using Puzzle.Client.Play;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// How the Unity game turns the events of a move into a timed script for its view, and keeps its own record of the
/// board while doing so. <c>TurnChoreographer</c> and <c>PieceLedger</c> are the Unity project's source files,
/// compiled into these tests: they have no Unity dependency.
/// </summary>
public partial class TurnChoreographerTests
{
    private const float Tolerance = 0.0005f;

    /// <summary>A game together with the view's record of its board, as the Unity stage keeps them.</summary>
    private sealed class Table
    {
        public Table(Game game)
        {
            Game = game;
            Ledger = PieceLedger.Of(game.Board);
        }

        public Game Game { get; }

        public PieceLedger Ledger { get; }

        /// <summary>The numbers of the pieces the ledger held before the script that was written last.</summary>
        public IReadOnlyCollection<int> HeldBefore { get; private set; } = Array.Empty<int>();

        public TurnScript Play(Move move)
        {
            Assert.True(Game.TryPlay(move), $"{move} is not legal");
            HeldBefore = Ledger.Pieces.Select(piece => piece.Id).ToArray();
            return TurnChoreographer.Compose(Ledger, Game.Level.Goals, Game.LastEvents, Game.Board);
        }

        /// <summary>Plays the won game's finale on the engine and writes its script, as the stage does.</summary>
        public (FinaleOutcome Outcome, TurnScript Script) PlayTheFinale()
        {
            FinaleOutcome outcome = Game.PlayFinale();
            HeldBefore = Ledger.Pieces.Select(piece => piece.Id).ToArray();
            return (outcome, TurnChoreographer.ComposeFinale(Ledger, Game.LastEvents, Game.Board));
        }
    }

    private static Table TableOf(string[] tiles, ulong seed = 1, GoalSpec[]? goals = null, string[]? jelly = null, string[]? chains = null) =>
        new Table(Start(Level(tiles, goals: goals, jelly: jelly, chains: chains), seed));

    /// <summary>Three purple gems in the fourth row once the one above the third column is pulled down.</summary>
    private static string[] BoardWithAMatchInReach() =>
        Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p');

    private static IEnumerable<TCue> Of<TCue>(TurnScript script)
        where TCue : Cue => script.Cues.OfType<TCue>();

    [Fact]
    public void A_plain_move_trades_two_pieces_then_bursts_the_match()
    {
        Table table = TableOf(BoardWithAMatchInReach());

        TurnScript script = table.Play(Move.Swap(P(2, 2), P(2, 3)));

        Assert.IsType<Cue.MoveSpent>(script.Cues[0]);
        Cue.Slide[] slides = Of<Cue.Slide>(script).ToArray();
        Assert.Equal(2, slides.Length);
        Assert.All(slides, slide => Assert.Equal((0f, Tempo.Swap), (slide.Start, slide.Seconds)));
        Assert.Equal((SlideManner.Carried, P(2, 2), P(2, 3)), (slides[0].Manner, slides[0].From, slides[0].To));
        Assert.Equal((SlideManner.Displaced, P(2, 3), P(2, 2)), (slides[1].Manner, slides[1].From, slides[1].To));

        Cue.Leave[] bursts = Of<Cue.Leave>(script).Where(leave => leave.Start < Tempo.Swap + Tolerance).ToArray();
        Assert.Equal(new[] { P(0, 3), P(1, 3), P(2, 3) }, bursts.Select(leave => leave.At).OrderBy(cell => cell.X));
        Assert.All(bursts, leave => Assert.Equal((LeaveManner.Pop, Tempo.Swap), (leave.Manner, leave.Start)));
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_plain_move_is_over_well_within_a_second()
    {
        Table table = TableOf(BoardWithAMatchInReach());
        var events = new List<GameEvent>();

        // Only the player's own match is timed here: what the refill happens to set off afterwards is luck.
        Assert.True(table.Game.TryPlay(Move.Swap(P(2, 2), P(2, 3))));
        events.AddRange(table.Game.LastEvents.TakeWhile(e => e is not GameEvent.RoundStarted { Round: > 0 }));
        TurnScript script = TurnChoreographer.Compose(table.Ledger, table.Game.Level.Goals, events, table.Game.Board);

        Assert.InRange(script.Seconds, 0.3f, 0.75f);
    }

    [Fact]
    public void Pieces_start_to_fall_once_the_cell_under_them_is_free_and_follow_each_other()
    {
        Table table = TableOf(BoardWithAMatchInReach());

        TurnScript script = table.Play(Move.Swap(P(2, 2), P(2, 3)));

        float cellsAreFree = Tempo.Swap + Tempo.CellFreeAfterHit;
        Cue.Fall[] firstFalls = Of<Cue.Fall>(script).Where(fall => fall.To.Y == 3).ToArray();
        Assert.Equal(3, firstFalls.Length);
        Assert.All(firstFalls, fall => Assert.Equal(cellsAreFree, fall.Start, Tolerance));
        Assert.All(firstFalls, fall => Assert.Equal(FallLaw.SecondsFor(1), fall.Seconds, Tolerance));

        // In every column each piece lets go a moment after the one under it.
        foreach (IGrouping<int, Cue.Fall> column in Of<Cue.Fall>(script).Where(fall => fall.Start < cellsAreFree + 0.2f).GroupBy(fall => fall.To.X))
        {
            float[] starts = column.OrderByDescending(fall => fall.To.Y).Select(fall => fall.Start).ToArray();
            for (int piece = 1; piece < starts.Length; piece++)
            {
                Assert.Equal(starts[piece - 1] + Tempo.FollowLag, starts[piece], Tolerance);
            }
        }
    }

    [Fact]
    public void New_pieces_come_in_from_above_their_column_behind_the_pieces_that_fell()
    {
        Table table = TableOf(BoardWithAMatchInReach());

        TurnScript script = table.Play(Move.Swap(P(2, 2), P(2, 3)));

        Cue.Enter first = Of<Cue.Enter>(script).First(enter => enter.To.X == 0);
        Cue.Fall lastOfColumn = Of<Cue.Fall>(script).Where(fall => fall.To.X == 0).OrderBy(fall => fall.To.Y).First();
        Assert.Equal(P(0, 0), first.To);
        Assert.Equal(-1, first.FromRow);
        Assert.Equal(lastOfColumn.Start + Tempo.FollowLag, first.Start, Tolerance);
        Assert.True(first.Lands);
    }

    [Fact]
    public void A_rockets_hits_run_outwards_from_it_along_its_row()
    {
        Table table = TableOf(Filler(7, 7).With(3, 3, 'H'));

        TurnScript script = table.Play(Move.Tap(P(3, 3)));

        Cue.Effect stroke = Assert.Single(Of<Cue.Effect>(script));
        Assert.Equal((EffectKind.ClearRow, P(3, 3), Tempo.FireLead), (stroke.Kind, stroke.Origin, stroke.Start));
        Cue.Leave rocket = Of<Cue.Leave>(script).Single(leave => leave.Manner == LeaveManner.Fire);
        Assert.Equal((P(3, 3), 0f), (rocket.At, rocket.Start));
        Cue.Leave[] hits = Of<Cue.Leave>(script).Where(leave => leave.Manner == LeaveManner.Pop && leave.At.Y == 3 && leave.Start < 0.2f).ToArray();
        Assert.Equal(6, hits.Length);
        Assert.All(hits, hit => Assert.Equal(stroke.Start + Math.Abs(hit.At.X - 3) * Tempo.RocketPerCell, hit.Start, Tolerance));
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_column_hit_by_a_rocket_falls_as_soon_as_the_stroke_has_passed_it()
    {
        Table table = TableOf(Filler(7, 7).With(3, 3, 'H'));

        TurnScript script = table.Play(Move.Tap(P(3, 3)));

        float StartOfColumn(int x) => Of<Cue.Fall>(script).Where(fall => fall.To.X == x).Min(fall => fall.Start);
        Assert.True(StartOfColumn(2) < StartOfColumn(0), "the column next to the rocket waits for the far end of the stroke");
        Assert.Equal(StartOfColumn(0), StartOfColumn(6), Tolerance);
    }

    [Fact]
    public void A_special_piece_that_is_hit_goes_off_when_it_is_hit()
    {
        Table table = TableOf(Filler(7, 7).With(1, 3, 'H').With(5, 3, 'X'));

        TurnScript script = table.Play(Move.Tap(P(1, 3)));

        Cue.Leave bomb = Of<Cue.Leave>(script).Single(leave => leave.Tile.Kind == TileKind.Bomb);
        Cue.Effect blast = Of<Cue.Effect>(script).Single(effect => effect.Kind == EffectKind.Explode);
        Assert.Equal(Tempo.FireLead + 4 * Tempo.RocketPerCell, bomb.Start, Tolerance);
        Assert.Equal(bomb.Start + Tempo.FireLead, blast.Start, Tolerance);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_match_of_four_gathers_into_the_special_piece_it_leaves()
    {
        string[] tiles = Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p').With(3, 3, 'p');
        Table table = TableOf(tiles);

        TurnScript script = table.Play(Move.Swap(P(2, 2), P(2, 3)));

        Cue.Appear rocket = Of<Cue.Appear>(script).First();
        Cue.Leave[] gathered = Of<Cue.Leave>(script).Where(leave => leave.Manner == LeaveManner.Gather).ToArray();
        Assert.Equal(4, gathered.Length);
        Assert.All(gathered, leave => Assert.Equal(rocket.At, leave.Toward));
        Assert.True(rocket.Tile.IsSpecial);
        Assert.Equal(Tempo.Swap + Tempo.AppearAfterGather, rocket.Start, Tolerance);
        Assert.Equal(rocket.Tile, table.Ledger.TileAt(table.Ledger.Pieces.Single(piece => piece.Id == rocket.PieceId).Cell));
    }

    [Fact]
    public void Two_special_pieces_pushed_together_do_not_trade_places()
    {
        Table table = TableOf(Filler(7, 7).With(2, 3, 'H').With(3, 3, 'X'));

        TurnScript script = table.Play(Move.Swap(P(2, 3), P(3, 3)));

        Cue.Slide slide = Assert.Single(Of<Cue.Slide>(script));
        Assert.Equal((SlideManner.Onto, P(2, 3), P(3, 3)), (slide.Manner, slide.From, slide.To));
        Assert.Equal(2, Of<Cue.Leave>(script).Count(leave => leave.Manner == LeaveManner.Fire && leave.Start == Tempo.Swap));
        Assert.Equal(EffectKind.Cross, Of<Cue.Effect>(script).First().Kind);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void What_a_seeker_flies_to_is_hit_when_it_lands()
    {
        Table table = TableOf(Filler(7, 7).With(3, 3, 'S').With(6, 0, '1'), goals: new GoalSpec[] { new GoalSpec.BreakBoxes(1) });

        TurnScript script = table.Play(Move.Tap(P(3, 3)));

        Cue.Flight flight = Assert.Single(Of<Cue.Flight>(script));
        Cue.Leave crate = Of<Cue.Leave>(script).Single(leave => leave.Manner == LeaveManner.Break);
        Assert.Equal((P(3, 3), P(6, 0)), (flight.From, flight.To));
        Assert.Equal(flight.End, crate.Start, Tolerance);
        Assert.Equal(crate.Start, Assert.Single(Of<Cue.GoalTick>(script)).Start, Tolerance);
    }

    [Fact]
    public void A_seeker_pushed_onto_a_rocket_carries_it_to_where_it_lands()
    {
        Table table = TableOf(Filler(7, 7).With(2, 3, 'S').With(3, 3, 'H').With(6, 0, '1'), goals: new GoalSpec[] { new GoalSpec.BreakBoxes(1) });

        TurnScript script = table.Play(Move.Swap(P(2, 3), P(3, 3)));

        Cue.Flight flight = Assert.Single(Of<Cue.Flight>(script));
        Cue.Effect stroke = Of<Cue.Effect>(script).Single(effect => effect.Kind == EffectKind.ClearRow);
        Assert.Equal(flight.To, stroke.Origin);
        Assert.Equal(flight.End, stroke.Start, Tolerance);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_colour_bomb_sends_a_ray_to_each_gem_and_the_gem_bursts_as_the_ray_arrives()
    {
        Table table = TableOf(Filler(7, 7).With(3, 3, 'C'));
        GemColor colour = table.Game.Board.TileAt(P(4, 3)).Color;

        TurnScript script = table.Play(Move.Swap(P(3, 3), P(4, 3)));

        Cue.Zap[] rays = Of<Cue.Zap>(script).ToArray();
        Cue.Leave[] bursts = Of<Cue.Leave>(script).Where(leave => leave.Tile.IsGemOf(colour) && leave.Start < rays.Max(ray => ray.End) + Tolerance).ToArray();
        Assert.Equal(rays.Length, bursts.Length);
        Assert.All(rays, ray => Assert.Equal(P(4, 3), ray.From));
        foreach (Cue.Zap ray in rays)
        {
            Assert.Equal(ray.End, bursts.Single(burst => burst.At == ray.To).Start, Tolerance);
        }

        Assert.True(rays.Max(ray => ray.End) - rays.Min(ray => ray.End) <= Tempo.RaysWithin + Tolerance);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_colour_bomb_pushed_onto_a_rocket_makes_rockets_that_go_off_one_after_another()
    {
        Table table = TableOf(Filler(7, 7).With(3, 3, 'C').With(4, 3, 'H'));

        TurnScript script = table.Play(Move.Swap(P(3, 3), P(4, 3)));

        Cue.Appear[] made = Of<Cue.Appear>(script).Where(appear => appear.Tile.Kind.IsRocket()).ToArray();
        Assert.NotEmpty(made);
        float[] goOff = made
            .Select(rocket => Of<Cue.Leave>(script).Single(leave => leave.PieceId == rocket.PieceId).Start)
            .OrderBy(start => start)
            .ToArray();
        Assert.True(goOff[0] >= made.Max(rocket => rocket.Start), "a made rocket goes off before all of them are made");
        Assert.Equal(goOff.Length, goOff.Distinct().Count());
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_colour_bomb_keeps_its_cell_until_its_last_ray_has_left()
    {
        Table table = TableOf(Filler(7, 7).With(3, 3, 'C'));

        TurnScript script = table.Play(Move.Swap(P(3, 3), P(4, 3)));

        float lastRayLeaves = Of<Cue.Zap>(script).Max(ray => ray.Start);
        IEnumerable<Cue> intoTheBombsCell = script.Cues.Where(cue =>
            (cue is Cue.Fall fall && fall.To == P(4, 3)) || (cue is Cue.Enter enter && enter.To == P(4, 3)));
        Assert.All(intoTheBombsCell, travel => Assert.True(travel.Start >= lastRayLeaves, "a piece falls into the turning bomb"));
    }

    [Fact]
    public void Two_colour_bombs_clear_the_board_in_a_wave_from_where_they_met()
    {
        Table table = TableOf(Filler(7, 7).With(3, 3, 'C').With(4, 3, 'C'));

        TurnScript script = table.Play(Move.Swap(P(3, 3), P(4, 3)));

        Cue.Effect flood = Assert.Single(Of<Cue.Effect>(script));
        Assert.Equal((EffectKind.ClearBoard, P(4, 3)), (flood.Kind, flood.Origin));
        Cue.Leave[] firstWave = Of<Cue.Leave>(script).Where(leave => leave.Manner == LeaveManner.Pop && leave.Start <= flood.Start + 4 * Tempo.WavePerCell + Tolerance).ToArray();
        Assert.Equal(47, firstWave.Length);
        Assert.All(firstWave, leave => Assert.Equal(
            flood.Start + Math.Max(Math.Abs(leave.At.X - 4), Math.Abs(leave.At.Y - 3)) * Tempo.WavePerCell,
            leave.Start,
            Tolerance));
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void Crates_tape_and_slabs_take_their_hits_with_the_match_and_count_for_their_goals()
    {
        string[] tiles = BoardWithAMatchInReach().With(3, 3, '1').With(0, 4, '2');
        string[] jelly = Blank(6, 6).With(1, 3, '1');
        var goals = new GoalSpec[] { new GoalSpec.BreakBoxes(2), new GoalSpec.ClearJelly(1), new GoalSpec.CollectColor(GemColor.Purple, 9) };
        Table table = TableOf(tiles, goals: goals, jelly: jelly);

        TurnScript script = table.Play(Move.Swap(P(2, 2), P(2, 3)));

        Cue.Leave broken = Of<Cue.Leave>(script).Single(leave => leave.Manner == LeaveManner.Break);
        Cue.Reface weakened = Assert.Single(Of<Cue.Reface>(script));
        Cue.SlabHit slab = Assert.Single(Of<Cue.SlabHit>(script));
        Assert.Equal((P(3, 3), Tempo.Swap), (broken.At, broken.Start));
        Assert.Equal((P(0, 4), Tile.Box(1), Tempo.Swap), (weakened.At, weakened.Tile, weakened.Start));
        Assert.Equal((P(1, 3), 0, Tempo.Swap), (slab.At, slab.LayersLeft, slab.Start));

        int[] counted = Of<Cue.GoalTick>(script).Where(tick => tick.Start < Tempo.Swap + Tolerance).Select(tick => tick.GoalIndex).OrderBy(goal => goal).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 2, 2 }, counted);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void Tape_takes_the_hit_and_the_piece_under_it_stays()
    {
        string[] chains = Blank(6, 6).With(1, 3, '2');
        Table table = TableOf(BoardWithAMatchInReach(), chains: chains);

        TurnScript script = table.Play(Move.Swap(P(2, 2), P(2, 3)));

        Cue.TapeHit tape = Of<Cue.TapeHit>(script).First();
        Assert.Equal((P(1, 3), 1, Tempo.Swap, Tempo.Bump), (tape.At, tape.HitsLeft, tape.Start, tape.Seconds));
        Assert.DoesNotContain(Of<Cue.Leave>(script), leave => leave.At == P(1, 3) && leave.Start < Tempo.Swap + Tolerance);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void An_item_that_reaches_the_foot_of_its_column_drops_out_and_counts()
    {
        string[] tiles = Filler(6, 6).With(0, 5, 'p').With(1, 5, 'p').With(3, 5, 'p').With(2, 4, 'I');
        Table table = TableOf(tiles, goals: new GoalSpec[] { new GoalSpec.DropItems(1) });

        TurnScript script = table.Play(Move.Swap(P(3, 5), P(2, 5)));

        Cue.Leave dropped = Of<Cue.Leave>(script).Single(leave => leave.Manner == LeaveManner.Collect);
        Cue travel = script.Cues.Last(cue => cue is Cue.Fall fall && fall.PieceId == dropped.PieceId);
        Assert.Equal(P(2, 5), dropped.At);
        Assert.Equal(travel.End, dropped.Start, Tolerance);
        Assert.Equal(dropped.End, Assert.Single(Of<Cue.GoalTick>(script)).Start, Tolerance);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_cascade_waits_for_the_pieces_to_land_and_then_bursts_quicker()
    {
        (Table table, TurnScript script) = FirstTurnWith(e => e is GameEvent.RoundStarted { Round: 1 }, LevelJson.Load(RepoFiles.SampleLevelPaths[0]));
        int roundOne = table.Game.LastEvents.Select((e, index) => (e, index)).First(pair => pair.e is GameEvent.RoundStarted { Round: 1 }).index;
        var burstInRoundOne = (GameEvent.TileDestroyed)table.Game.LastEvents.Skip(roundOne).First(e => e is GameEvent.TileDestroyed);

        Cue.Leave burst = Of<Cue.Leave>(script).First(leave => leave.At == burstInRoundOne.At && leave.Tile == burstInRoundOne.Tile && leave.Start > Tempo.Swap + Tolerance);
        float landedBefore = script.Cues.Where(cue => cue.Start < burst.Start && (cue is Cue.Fall || cue is Cue.Enter || cue is Cue.Slip)).Max(cue => cue.End);
        float pace = Tempo.PaceOfRound(1);

        Assert.Equal(landedBefore + Tempo.PauseBeforeCascade * pace, burst.Start, Tolerance);
        Assert.True(pace < 1f);
        Assert.Contains(burst.Seconds, new[] { Tempo.Pop * pace, Tempo.Gather * pace });
    }

    [Fact]
    public void A_board_without_a_move_is_dealt_anew_as_the_last_thing_of_the_turn()
    {
        // Small and many-coloured: such a board soon has no move left.
        (Table table, TurnScript script) = FirstTurnWith(e => e is GameEvent.Shuffled, Level(Blank(6, 6), colors: 8, moves: 200));

        Cue.Reshuffle reshuffle = Assert.Single(Of<Cue.Reshuffle>(script));
        Assert.Same(reshuffle, script.Cues[^1]);
        Assert.Equal(script.Seconds, reshuffle.End, Tolerance);
        Assert.NotEmpty(reshuffle.Dealt);
        Assert.All(reshuffle.Dealt, piece => Assert.Equal(table.Game.Board.TileAt(piece.Cell), piece.Tile));
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_piece_that_is_hit_while_it_still_gives_under_its_landing_stops_giving_when_it_leaves()
    {
        // In a cascade the next match bursts a moment after the pieces have landed, sooner than a landing takes.
        (_, TurnScript script) = FirstTurnWith(e => e is GameEvent.RoundStarted { Round: 1 }, LevelJson.Load(RepoFiles.SampleLevelPaths[0]));

        var leavesAt = Of<Cue.Leave>(script).ToDictionary(leave => leave.PieceId, leave => leave.Start);
        (int PieceId, float Arrives, float LandingSeconds)[] landings = script.Cues.Select(LandingOf).OfType<(int, float, float)>().ToArray();
        (int PieceId, float Arrives, float LandingSeconds)[] cutShort = landings.Where(landing => landing.LandingSeconds < Tempo.Land).ToArray();

        Assert.NotEmpty(cutShort);
        Assert.All(cutShort, landing => Assert.Equal(leavesAt[landing.PieceId], landing.Arrives + landing.LandingSeconds, Tolerance));
        Assert.Contains(landings, landing => landing.LandingSeconds == Tempo.Land);
    }

    /// <summary>The piece of a travel that lands, when it arrives and how long it gives; nothing for any other cue.</summary>
    private static (int PieceId, float Arrives, float LandingSeconds)? LandingOf(Cue cue) => cue switch
    {
        Cue.Fall { Lands: true } fall => (fall.PieceId, fall.End, fall.LandingSeconds),
        Cue.Enter { Lands: true } enter => (enter.PieceId, enter.End, enter.LandingSeconds),
        Cue.Slip { Lands: true } slip => (slip.PieceId, slip.End, slip.LandingSeconds),
        _ => null,
    };

    /// <summary>
    /// Levels of the game's own pack that stress the script. On 285 a strong player's fifth move set off a cascade
    /// deep enough for a burst to overtake a landing, which froze the board of the first build. On 206, 239, 644 and
    /// 257 two effects reached a slab, a crate or a tape in the other order than the engine had counted their hits,
    /// which left a layer on the screen that the game no longer had.
    /// </summary>
    public static TheoryData<int, string> PackLevelsAndBots() => new TheoryData<int, string>
    {
        { 285, SkillProfile.Expert.Name },
        { 206, SkillProfile.Expert.Name },
        { 239, SkillProfile.Expert.Name },
        { 644, SkillProfile.Expert.Name },
        { 257, BotPolicies.Random },
        { 40, SkillProfile.Expert.Name },
        { 147, SkillProfile.Expert.Name },
        { 640, SkillProfile.Expert.Name },
        { 999, BotPolicies.Random },
    };

    [Theory]
    [MemberData(nameof(PackLevelsAndBots))]
    public void Through_whole_games_of_the_pack_every_script_can_be_played_as_it_is_written(int levelNumber, string botName)
    {
        LevelDefinition level = LevelJson.Load(PackLevelPath(levelNumber));
        Assert.True(BotPolicies.TryCreate(botName, out IBotPolicy bot));
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4 })
        {
            var table = new Table(Game.Start(level, seed, new GameOptions { RecordEvents = true }));
            var rng = new Rng(seed, 7);
            while (table.Game.Status == GameStatus.Playing)
            {
                TurnScript script = table.Play(bot.ChooseMove(table.Game, table.Game.FindLegalMoves(), rng));

                Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
                AssertCanBePlayed(script, table.Ledger);
                AssertWhatWentHasALeaving(script, table);
            }
        }
    }

    /// <summary>A level of the generated pack, which lies beside the hand-made samples (levels/generated), by its number.</summary>
    private static string PackLevelPath(int levelNumber) =>
        Path.Combine(Path.GetDirectoryName(RepoFiles.SampleLevelPaths[0])!, "..", "generated", $"level_{levelNumber:D4}.json");

    public static TheoryData<string, string> SampleLevelsAndBots()
    {
        var cases = new TheoryData<string, string>();
        foreach (string path in RepoFiles.SampleLevelPaths)
        {
            cases.Add(Path.GetFileName(path), BotPolicies.Random);
            cases.Add(Path.GetFileName(path), SkillProfile.Expert.Name);
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(SampleLevelsAndBots))]
    public void Through_whole_games_the_view_keeps_showing_the_engines_board_and_its_goals(string levelFile, string botName)
    {
        LevelDefinition level = LevelJson.Load(RepoFiles.SampleLevelPaths.Single(path => Path.GetFileName(path) == levelFile));
        Assert.True(BotPolicies.TryCreate(botName, out IBotPolicy bot));
        foreach (ulong seed in new ulong[] { 1, 2, 3 })
        {
            var table = new Table(Game.Start(level, seed, new GameOptions { RecordEvents = true, MoveLimitOverride = 60 }));
            var rng = new Rng(seed, 11);
            while (table.Game.Status == GameStatus.Playing)
            {
                int[] leftBefore = Remaining(table.Game);
                Move move = bot.ChooseMove(table.Game, table.Game.FindLegalMoves(), rng);

                TurnScript script = table.Play(move);

                Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
                AssertCanBePlayed(script, table.Ledger);
                AssertWhatWentHasALeaving(script, table);
                int[] leftOnThePlaque = leftBefore
                    .Select((left, goal) => Math.Max(0, left - Of<Cue.GoalTick>(script).Count(tick => tick.GoalIndex == goal)))
                    .ToArray();
                Assert.Equal(Remaining(table.Game), leftOnThePlaque);
            }
        }
    }

    private static int[] Remaining(Game game) =>
        Enumerable.Range(0, game.Goals.Count).Select(goal => game.Goals[goal].Remaining).ToArray();

    /// <summary>What every script must be for the view to play it without a piece being in two places or used after it has gone.</summary>
    private static void AssertCanBePlayed(TurnScript script, PieceLedger ledgerAfterwards)
    {
        AssertNothingOutlastsItsPiece(script);
        AssertHitsOnOneThingAreSeenInTheOrderTheyCount(script);
        var busyUntil = new Dictionary<int, float>();
        var gone = new HashSet<int>();
        var onTheBoard = new HashSet<int>();
        float before = 0f;
        foreach (Cue cue in script.Cues)
        {
            Assert.True(cue.Start >= before, "cues are in the order they begin");
            Assert.True(cue.Seconds >= 0f);
            before = cue.Start;
            switch (cue)
            {
                case Cue.Fall fall:
                    Assert.True(fall.From.X == fall.To.X && fall.To.Y > fall.From.Y, "a fall goes straight down");
                    Travels(fall.PieceId, fall);
                    break;
                case Cue.Slip slip:
                    Assert.True(Math.Abs(slip.To.X - slip.From.X) == 1 && slip.To.Y == slip.From.Y + 1, "a slip goes one cell diagonally down");
                    Travels(slip.PieceId, slip);
                    break;
                case Cue.Slide slide:
                    Travels(slide.PieceId, slide);
                    break;
                case Cue.Enter enter:
                    Assert.True(enter.FromRow < enter.To.Y);
                    Assert.DoesNotContain(enter.PieceId, busyUntil.Keys);
                    Travels(enter.PieceId, enter);
                    break;
                case Cue.Appear appear:
                    Assert.DoesNotContain(appear.PieceId, busyUntil.Keys);
                    Assert.DoesNotContain(appear.PieceId, gone);
                    break;
                case Cue.Leave leave:
                    Assert.True(gone.Add(leave.PieceId), "a piece leaves once");
                    Assert.True(!busyUntil.TryGetValue(leave.PieceId, out float busy) || leave.Start >= busy - Tolerance, "a piece leaves after it has arrived");
                    break;
                case Cue.Reface reface:
                    Assert.DoesNotContain(reface.PieceId, gone);
                    break;
            }
        }

        foreach (PlacedPiece piece in ledgerAfterwards.Pieces)
        {
            Assert.DoesNotContain(piece.Id, gone);
            Assert.True(onTheBoard.Add(piece.Id), "every piece has a number of its own");
        }

        Assert.Equal(script.Seconds, script.Cues.Max(cue => cue.End), Tolerance);

        void Travels(int pieceId, Cue travel)
        {
            Assert.DoesNotContain(pieceId, gone);
            Assert.True(!busyUntil.TryGetValue(pieceId, out float busy) || travel.Start >= busy - Tolerance, "a piece is in one place at a time");
            busyUntil[pieceId] = travel.End;
        }
    }

    /// <summary>
    /// A piece goes from the view by a leaving and in no other way: every piece the ledger held before the script
    /// and no longer holds has a leaving in it. One without would stay on the screen where the board has none.
    /// </summary>
    private static void AssertWhatWentHasALeaving(TurnScript script, Table table)
    {
        HashSet<int> held = table.Ledger.Pieces.Select(piece => piece.Id).ToHashSet();
        HashSet<int> left = Of<Cue.Leave>(script).Select(leave => leave.PieceId).ToHashSet();
        Assert.All(table.HeldBefore.Where(piece => !held.Contains(piece)), piece => Assert.Contains(piece, left));
    }

    /// <summary>
    /// The view takes a piece off the stage when its leaving is over, so nothing else the script does with that
    /// piece may still be going on then; and the give of a landing, which poses the piece as its leaving does,
    /// must be over when the leaving begins.
    /// </summary>
    private static void AssertNothingOutlastsItsPiece(TurnScript script)
    {
        Dictionary<int, Cue.Leave> leaving = Of<Cue.Leave>(script).ToDictionary(leave => leave.PieceId);
        foreach (Cue cue in script.Cues)
        {
            foreach (int pieceId in PiecesPosedBy(cue))
            {
                if (cue is Cue.Leave || !leaving.TryGetValue(pieceId, out Cue.Leave? leave))
                {
                    continue;
                }

                Assert.True(cue.End <= leave.End + Tolerance, $"a {cue.GetType().Name} of piece {pieceId} ends at {cue.End}, after the piece has gone at {leave.End}");
                if (LandingOf(cue) is (_, float arrives, float landingSeconds))
                {
                    Assert.True(arrives + landingSeconds <= leave.Start + Tolerance, $"piece {pieceId} still gives under its landing until {arrives + landingSeconds}, but leaves at {leave.Start}");
                }
            }
        }
    }

    /// <summary>
    /// A slab, a tape and a crate take more than one hit, and each hit's cue says what is left afterwards. The view
    /// shows the cues in the order of their moments, so what is left must go down in that order: a hit that leaves
    /// one layer, shown after the hit that leaves none, would lay the layer back.
    /// </summary>
    private static void AssertHitsOnOneThingAreSeenInTheOrderTheyCount(TurnScript script)
    {
        AssertGoesDown(Of<Cue.SlabHit>(script).GroupBy(hit => hit.At.ToString(), hit => hit.LayersLeft), "slab layers in");
        AssertGoesDown(Of<Cue.TapeHit>(script).GroupBy(hit => hit.At.ToString(), hit => hit.HitsLeft), "tape hits left in");
        AssertGoesDown(Of<Cue.Reface>(script).GroupBy(hit => hit.PieceId.ToString(), hit => (int)hit.Tile.Hp), "hits left of crate");

        static void AssertGoesDown(IEnumerable<IGrouping<string, int>> leftByThing, string what)
        {
            foreach (IGrouping<string, int> left in leftByThing)
            {
                int[] inTheOrderShown = left.ToArray();
                Assert.True(
                    inTheOrderShown.Zip(inTheOrderShown.Skip(1), (before, after) => after < before).All(goesDown => goesDown),
                    $"the {what} {left.Key} are shown as {string.Join(", ", inTheOrderShown)}");
            }
        }
    }

    /// <summary>The pieces a cue moves, sizes, turns or changes.</summary>
    private static IEnumerable<int> PiecesPosedBy(Cue cue) => cue switch
    {
        Cue.Slide slide => new[] { slide.PieceId },
        Cue.Fall fall => new[] { fall.PieceId },
        Cue.Enter enter => new[] { enter.PieceId },
        Cue.Slip slip => new[] { slip.PieceId },
        Cue.Appear appear => new[] { appear.PieceId },
        Cue.Reface reface => new[] { reface.PieceId },
        Cue.Leave leave => new[] { leave.PieceId },
        Cue.Reshuffle reshuffle => reshuffle.Dealt.Select(dealt => dealt.Id),
        _ => Array.Empty<int>(),
    };

    /// <summary>
    /// Plays a level with the random bot, seed after seed, until a move's events hold the one looked for.
    /// The same search finds the same turn every time: games are decided by their seeds.
    /// </summary>
    private static (Table Table, TurnScript Script) FirstTurnWith(Func<GameEvent, bool> isLookedFor, LevelDefinition level)
    {
        var bot = new RandomPolicy();
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var table = new Table(Game.Start(level, seed, new GameOptions { RecordEvents = true }));
            var rng = new Rng(seed, 11);
            while (table.Game.Status == GameStatus.Playing)
            {
                TurnScript script = table.Play(bot.ChooseMove(table.Game, table.Game.FindLegalMoves(), rng));
                if (table.Game.LastEvents.Any(isLookedFor))
                {
                    return (table, script);
                }
            }
        }

        throw new InvalidOperationException("No game of the level has such a turn; pick another level or more seeds.");
    }
}
