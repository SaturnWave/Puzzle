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
/// How the Unity game shows the finale of a won game (docs/GAME_DESIGN.md §19): the engine turns every move left
/// over into a rocket and fires it (<c>Game.PlayFinale</c>), and <c>TurnChoreographer.ComposeFinale</c> makes a
/// script of that: moves that fly from the medallion, gems that turn into rockets, strokes, and a board that
/// settles. The tests of a move's script are in the other part of this class.
/// </summary>
public partial class TurnChoreographerTests
{
    /// <summary>The move that wins every board of <see cref="WonWith"/>: the third purple gem comes down into its row.</summary>
    private static readonly Move TheWinningMove = Move.Swap(P(2, 2), P(2, 3));

    /// <summary>
    /// A board of filler that its first move wins, with so many moves left over. The view's ledger stands at the
    /// board the winning move left, as the stage's does when the finale begins.
    /// </summary>
    private static Table WonWith(int movesLeft, ulong seed = 1, int width = 9, int height = 9) =>
        WonOn(Filler(width, height), movesLeft, seed);

    /// <summary>
    /// The same on a board of the test's own: the three purple gems of the winning move are laid into it, so it
    /// must leave their cells and the cell under the third to them. Tape lies where a digit of the map says.
    /// </summary>
    private static Table WonOn(string[] board, int movesLeft, ulong seed = 1, string[]? tape = null)
    {
        string[] tiles = board.With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p');
        var goals = new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) };
        var table = new Table(Start(Level(tiles, goals: goals, chains: tape, moves: movesLeft + 1), seed));
        table.Play(TheWinningMove);
        Assert.Equal((GameStatus.Won, movesLeft), (table.Game.Status, table.Game.MovesLeft));
        return table;
    }

    /// <summary>
    /// A move of the finale as its script tells it: its flight, the gem that bursts where it lands, the rocket
    /// that appears there, and that rocket going off.
    /// </summary>
    private sealed record ThrownMove(Cue.MoveThrown Flight, Cue.Leave Gem, Cue.Appear Rocket, Cue.Leave GoesOff)
    {
        public Position Cell => Flight.To;
    }

    /// <summary>
    /// The moves of a finale's script, wave by wave, each wave in the order its moves leave the medallion. The
    /// moves of one wave leave closer together than one of them takes to fly; between two waves the rockets fire
    /// and the board settles.
    /// </summary>
    private static List<List<ThrownMove>> WavesOf(TurnScript script)
    {
        var waves = new List<List<ThrownMove>>();
        float leftBefore = float.NegativeInfinity;
        foreach (Cue.MoveThrown flight in Of<Cue.MoveThrown>(script))
        {
            if (flight.Start - leftBefore > Tempo.MoveFlies)
            {
                waves.Add(new List<ThrownMove>());
            }

            leftBefore = flight.Start;
            Cue.Leave gem = Of<Cue.Leave>(script).Single(leave => leave.At == flight.To && Math.Abs(leave.Start - flight.End) < Tolerance);
            Cue.Appear rocket = Of<Cue.Appear>(script).Single(appear => appear.At == flight.To && Math.Abs(appear.Start - flight.End) < Tolerance);
            Cue.Leave goesOff = Of<Cue.Leave>(script).Single(leave => leave.PieceId == rocket.PieceId);
            waves[^1].Add(new ThrownMove(flight, gem, rocket, goesOff));
        }

        return waves;
    }

    private static bool IsAHit(Cue cue) =>
        cue is Cue.Leave { Manner: not LeaveManner.Collect } || cue is Cue.Reface || cue is Cue.TapeHit || cue is Cue.SlabHit;

    private static bool IsATravel(Cue cue) => cue is Cue.Fall || cue is Cue.Enter || cue is Cue.Slip;

    private static bool Settles(GameEvent gameEvent) =>
        gameEvent is GameEvent.TileMoved || gameEvent is GameEvent.TileSpawned || gameEvent is GameEvent.ItemCollected;

    /// <summary>The strokes of rockets in a script.</summary>
    private static Cue.Effect[] StrokesOf(TurnScript script) =>
        Of<Cue.Effect>(script).Where(effect => effect.Kind == EffectKind.ClearRow || effect.Kind == EffectKind.ClearColumn).ToArray();

    private static bool RunsThrough(Cue.Effect stroke, Position cell) =>
        stroke.Kind == EffectKind.ClearRow ? cell.Y == stroke.Origin.Y : cell.X == stroke.Origin.X;

    /// <summary>When a rocket's stroke gets to a cell of its line.</summary>
    private static float Reaches(Cue.Effect stroke, Position cell)
    {
        int cells = stroke.Kind == EffectKind.ClearRow ? Math.Abs(cell.X - stroke.Origin.X) : Math.Abs(cell.Y - stroke.Origin.Y);
        return stroke.Start + cells * stroke.SecondsPerCell;
    }

    /// <summary>
    /// Seed after seed, the first won board whose finale is what is looked for, with that finale played and written.
    /// The same search finds the same finale every time: games are decided by their seeds.
    /// </summary>
    private static (Table Table, FinaleOutcome Finale, TurnScript Script) FirstFinaleWith(int movesLeft, Func<IReadOnlyList<GameEvent>, TurnScript, bool> isLookedFor, int boardSide = 9)
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            Table table = WonWith(movesLeft, seed, width: boardSide, height: boardSide);
            (FinaleOutcome finale, TurnScript script) = table.PlayTheFinale();
            if (isLookedFor(table.Game.LastEvents, script))
            {
                return (table, finale, script);
            }
        }

        throw new InvalidOperationException("No seed has such a finale; pick another count of moves or more seeds.");
    }

    [Fact]
    public void Every_move_left_is_thrown_once_and_a_gem_turns_into_a_rocket_where_it_lands()
    {
        Table table = WonWith(movesLeft: 8);

        (FinaleOutcome finale, TurnScript script) = table.PlayTheFinale();

        Assert.IsType<Cue.FinaleOpens>(script.Cues[0]);
        ThrownMove[] moves = Assert.Single(WavesOf(script)).ToArray();
        (Position, TileKind)[] made = table.Game.LastEvents
            .TakeWhile(e => e is GameEvent.SpecialCreated)
            .Cast<GameEvent.SpecialCreated>()
            .Select(created => (created.At, created.Kind))
            .ToArray();
        Assert.Equal(8, finale.RocketsFired);
        Assert.Equal(made, moves.Select(move => (move.Cell, move.Rocket.Tile.Kind)));
        Assert.Equal(8, moves.Select(move => move.Cell).Distinct().Count());
        Assert.All(moves, move =>
        {
            Assert.Equal(Tempo.MoveFlies, move.Flight.Seconds, Tolerance);
            Assert.True(move.Gem.Tile.IsGem);
            Assert.Equal(LeaveManner.Pop, move.Gem.Manner);
            Assert.True(move.Rocket.Tile.Kind.IsRocket());
            Assert.Equal(LeaveManner.Fire, move.GoesOff.Manner);
        });
    }

    [Fact]
    public void The_finale_spends_no_move_and_counts_toward_no_goal()
    {
        // Purple is the level's goal and keeps falling in; the rockets burst it, and the plaque must not count it.
        Table table = WonWith(movesLeft: 20);

        (_, TurnScript script) = table.PlayTheFinale();

        Assert.Contains(Of<Cue.Leave>(script), leave => leave.Tile.IsGemOf(GemColor.Purple));
        Assert.Empty(Of<Cue.MoveSpent>(script));
        Assert.Empty(Of<Cue.GoalTick>(script));
    }

    [Fact]
    public void Moves_leave_the_medallion_one_after_another_and_many_leave_closer_together_so_that_the_last_leaves_in_time()
    {
        float[] five = Of<Cue.MoveThrown>(WonWith(movesLeft: 5).PlayTheFinale().Script).Select(flight => flight.Start).ToArray();
        float[] thirty = Of<Cue.MoveThrown>(WonWith(movesLeft: 30).PlayTheFinale().Script).Select(flight => flight.Start).ToArray();

        Assert.Equal(5, five.Length);
        Assert.Equal(30, thirty.Length);
        for (int move = 0; move < five.Length; move++)
        {
            Assert.Equal(Tempo.FinaleLead + move * Tempo.MovesLeaveEvery, five[move], Tolerance);
        }

        for (int move = 0; move < thirty.Length; move++)
        {
            Assert.Equal(Tempo.FinaleLead + move * Tempo.MovesLeaveWithin / 30, thirty[move], Tolerance);
        }

        Assert.True(Tempo.MovesLeaveWithin / 30 < Tempo.MovesLeaveEvery);
        Assert.True(thirty[^1] <= Tempo.FinaleLead + Tempo.MovesLeaveWithin);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(12)]
    public void No_rocket_of_a_wave_goes_off_before_the_last_move_of_the_wave_has_landed_and_stood_a_moment(int movesLeft)
    {
        (_, TurnScript script) = WonWith(movesLeft).PlayTheFinale();

        List<ThrownMove> wave = Assert.Single(WavesOf(script));
        float allHaveLanded = wave.Max(move => move.Flight.End);
        Assert.All(wave, move => Assert.True(move.GoesOff.Start >= allHaveLanded + Tempo.RocketsRest - Tolerance, $"the rocket in {move.Cell} goes off at {move.GoesOff.Start}"));
        Assert.Equal(allHaveLanded + Tempo.RocketsRest, wave[0].GoesOff.Start, Tolerance);

        // A rocket has popped up by the time its flare is over: nothing the view does with it outlasts it.
        Assert.True(Tempo.RocketsRest >= Tempo.Appear - Tempo.Fire);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(30)]
    public void The_rockets_of_a_wave_go_off_in_the_order_they_were_thrown_or_sooner_when_a_stroke_reaches_them_and_all_within_their_time(int movesLeft)
    {
        (_, TurnScript script) = WonWith(movesLeft).PlayTheFinale();

        List<ThrownMove> wave = Assert.Single(WavesOf(script));
        Cue.Effect[] strokes = StrokesOf(script);
        float firstGoesOff = wave.Max(move => move.Flight.End) + Tempo.RocketsRest;
        float goOffEvery = Math.Min(Tempo.ArmedEvery, Tempo.RocketsFireWithin / movesLeft);
        for (int thrown = 0; thrown < wave.Count; thrown++)
        {
            ThrownMove move = wave[thrown];
            float ownMoment = firstGoesOff + thrown * goOffEvery;
            bool isOnItsOwn = Math.Abs(move.GoesOff.Start - ownMoment) < Tolerance;
            bool isSetOffByAStroke = move.GoesOff.Start < ownMoment && strokes.Any(stroke =>
                stroke.Origin != move.Cell && RunsThrough(stroke, move.Cell) && Math.Abs(Reaches(stroke, move.Cell) - move.GoesOff.Start) < Tolerance);
            Assert.True(isOnItsOwn || isSetOffByAStroke, $"the rocket thrown {thrown + 1}. goes off at {move.GoesOff.Start}; its own moment is {ownMoment}");

            // Each goes off once, and its stroke begins a moment into its flare.
            Cue.Effect own = Assert.Single(strokes, stroke => stroke.Origin == move.Cell);
            Assert.Equal(move.GoesOff.Start + Tempo.FireLead, own.Start, Tolerance);
        }

        Assert.True(wave.Max(move => move.GoesOff.Start) - firstGoesOff <= Tempo.RocketsFireWithin + Tolerance);
    }

    [Fact]
    public void A_rocket_that_anothers_stroke_reaches_goes_off_as_it_is_reached_by_the_first_of_them()
    {
        (_, _, TurnScript script) = FirstFinaleWith(movesLeft: 12, (_, written) => RocketsSetOffByAStroke(written).Any());

        Cue.Effect[] strokes = StrokesOf(script);
        Assert.All(RocketsSetOffByAStroke(script), move =>
        {
            float firstReached = strokes.Where(stroke => stroke.Origin != move.Cell && RunsThrough(stroke, move.Cell)).Min(stroke => Reaches(stroke, move.Cell));
            Assert.Equal(firstReached, move.GoesOff.Start, Tolerance);
            Assert.Equal(LeaveManner.Fire, move.GoesOff.Manner);
        });
    }

    [Fact]
    public void A_rocket_that_stood_on_the_board_goes_off_when_the_first_stroke_of_the_wave_reaches_it_and_stays_when_none_does()
    {
        Position stands = P(6, 5);
        string[] board = Filler(9, 9).With(stands.X, stands.Y, 'H');
        int reachedIn = 0;
        int leftStandingIn = 0;
        foreach (ulong seed in Enumerable.Range(1, 40).Select(seed => (ulong)seed))
        {
            Table table = WonOn(board, movesLeft: 12, seed);
            int rocket = table.Ledger.PieceAt(stands);
            Assert.Equal(TileKind.RocketH, table.Ledger.TileAt(stands).Kind);

            (FinaleOutcome finale, TurnScript script) = table.PlayTheFinale();

            float boardSettles = script.Cues.Where(IsATravel).Min(travel => travel.Start);
            Cue.Effect[] strokes = StrokesOfTheFirstWave(script);
            float[] reaches = strokes.Where(stroke => stroke.Origin != stands && RunsThrough(stroke, stands)).Select(stroke => Reaches(stroke, stands)).ToArray();
            Assert.Equal(12, Of<Cue.MoveThrown>(script).Count());
            if (reaches.Length == 0)
            {
                // No stroke of the wave runs through its cell: it is no part of the wave, and stands when the board comes down.
                leftStandingIn++;
                Assert.Equal(finale.RocketsFired, strokes.Length);
                Assert.DoesNotContain(Of<Cue.Leave>(script), leave => leave.PieceId == rocket && leave.Start < boardSettles);
                continue;
            }

            reachedIn++;
            Cue.Leave goesOff = Assert.Single(Of<Cue.Leave>(script), leave => leave.PieceId == rocket);
            Cue.Effect own = Assert.Single(strokes, stroke => stroke.Origin == stands);
            Assert.Equal((LeaveManner.Fire, EffectKind.ClearRow), (goesOff.Manner, own.Kind));
            Assert.Equal(reaches.Min(), goesOff.Start, Tolerance);
            Assert.Equal(goesOff.Start + Tempo.FireLead, own.Start, Tolerance);

            // It is a rocket of the wave from then on, though no move was thrown for it: its stroke passes over nothing.
            Assert.Equal(finale.RocketsFired + 1, strokes.Length);
            Assert.Empty(PassedOverAndLeftStanding(script));
            Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
            AssertCanBePlayed(script, table.Ledger);
        }

        Assert.True(reachedIn > 0 && leftStandingIn > 0, $"a stroke reached the standing rocket in {reachedIn} finales and none did in {leftStandingIn}: both must be seen");
    }

    [Fact]
    public void A_rocket_under_tape_loses_its_tape_to_the_first_stroke_that_reaches_it_and_goes_off_with_the_second()
    {
        Position stands = P(6, 5);
        string[] board = Filler(9, 9).With(stands.X, stands.Y, 'V');
        string[] tape = Blank(9, 9).With(stands.X, stands.Y, '1');
        int untapedOnlyIn = 0;
        int setOffIn = 0;
        foreach (ulong seed in Enumerable.Range(1, 60).Select(seed => (ulong)seed))
        {
            Table table = WonOn(board, movesLeft: 8, seed, tape);
            int rocket = table.Ledger.PieceAt(stands);
            Assert.Equal((TileKind.RocketV, 1), (table.Ledger.TileAt(stands).Kind, table.Ledger.TapeAt(stands)));

            (_, TurnScript script) = table.PlayTheFinale();

            float boardSettles = script.Cues.Where(IsATravel).Min(travel => travel.Start);
            float[] reaches = StrokesOfTheFirstWave(script)
                .Where(stroke => stroke.Origin != stands && RunsThrough(stroke, stands))
                .Select(stroke => Reaches(stroke, stands))
                .OrderBy(reached => reached)
                .ToArray();
            Cue.Leave[] goesOff = Of<Cue.Leave>(script).Where(leave => leave.PieceId == rocket && leave.Start < boardSettles).ToArray();
            Cue.TapeHit[] tapeHits = Of<Cue.TapeHit>(script).Where(hit => hit.At == stands && hit.Start < boardSettles).ToArray();
            Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
            AssertCanBePlayed(script, table.Ledger);
            if (reaches.Length == 0)
            {
                Assert.Empty(tapeHits);
                Assert.Empty(goesOff);
                continue;
            }

            // The tape takes the first stroke's hit, as it is seen to arrive; the rocket under it does not flare.
            Cue.TapeHit comesOff = Assert.Single(tapeHits);
            Assert.Equal((0, reaches[0]), (comesOff.HitsLeft, comesOff.Start));
            if (reaches.Length == 1)
            {
                untapedOnlyIn++;
                Assert.Empty(goesOff);
                continue;
            }

            // A second stroke finds a rocket like any other piece in its way, and sets it off as it gets there.
            setOffIn++;
            Cue.Leave fires = Assert.Single(goesOff);
            Assert.Equal(LeaveManner.Fire, fires.Manner);
            Assert.Equal(reaches[1], fires.Start, Tolerance);
            Assert.Contains(StrokesOf(script), stroke => stroke.Origin == stands && Math.Abs(stroke.Start - (fires.Start + Tempo.FireLead)) < Tolerance);
        }

        Assert.True(untapedOnlyIn > 0 && setOffIn > 0, $"one stroke reached the taped rocket in {untapedOnlyIn} finales and two or more in {setOffIn}: both must be seen");
    }

    /// <summary>
    /// For every wave of a finale, in the engine's order, whether pieces burst by themselves after it: a round of
    /// matches before the next wave begins. A wave begins with the first rocket the engine names on a board that
    /// has landed; a special piece a match leaves is named in the middle of the match's hits.
    /// </summary>
    private static List<bool> CascadesAfterEachWave(IReadOnlyList<GameEvent> events)
    {
        var cascades = new List<bool>();
        GameEvent? before = null;
        foreach (GameEvent gameEvent in events)
        {
            if (gameEvent is GameEvent.SpecialCreated && (before == null || Settles(before)))
            {
                cascades.Add(false);
            }
            else if (gameEvent is GameEvent.RoundStarted)
            {
                cascades[^1] = true;
            }

            before = gameEvent;
        }

        return cascades;
    }

    [Fact]
    public void What_bursts_between_two_waves_is_part_of_the_show_and_a_finale_whose_last_wave_sets_nothing_off_has_no_aftermath()
    {
        // Sixteen cells and twenty-nine moves left: the rockets come in waves, and what falls in between them matches by luck.
        (Table table, _, TurnScript script) = FirstFinaleWith(
            movesLeft: 29,
            (events, _) => CascadesAfterEachWave(events) is { Count: > 1 } cascades && cascades.SkipLast(1).Contains(true) && !cascades[^1],
            boardSide: 4);

        Assert.Equal(CascadesAfterEachWave(table.Game.LastEvents).Count, WavesOf(script).Count);
        Assert.Null(script.ShowSeconds);
    }

    [Fact]
    public void The_show_of_a_finale_in_waves_ends_when_the_board_has_come_down_after_its_last_wave()
    {
        (Table table, FinaleOutcome finale, TurnScript script) = FirstFinaleWith(
            movesLeft: 29,
            (events, _) => CascadesAfterEachWave(events) is { Count: > 1 } cascades && cascades.SkipLast(1).Contains(true) && cascades[^1],
            boardSide: 4);

        // The first piece that bursts by itself after the last wave: the board landed a quick pause before that.
        List<ThrownMove> lastWave = WavesOf(script)[^1];
        float lastWaveSettles = script.Cues.Where(cue => IsATravel(cue) && cue.Start > lastWave[^1].Flight.End).Min(travel => travel.Start);
        int lastWaveBeginsAt = table.Game.LastEvents.ToList().FindLastIndex(e => e is GameEvent.SpecialCreated created && created.At == lastWave[0].Cell);
        var firstOfTheAftermath = (GameEvent.TileDestroyed)table.Game.LastEvents
            .Skip(lastWaveBeginsAt)
            .SkipWhile(e => e is not GameEvent.RoundStarted)
            .First(e => e is GameEvent.TileDestroyed);
        Cue.Leave burst = Of<Cue.Leave>(script).First(leave => leave.At == firstOfTheAftermath.At && leave.Tile == firstOfTheAftermath.Tile && leave.Start > lastWaveSettles);

        float showSeconds = Assert.NotNull(script.ShowSeconds);
        Assert.Equal(burst.Start - Tempo.PauseBeforeCascade * Tempo.FinaleBurstPace, showSeconds, Tolerance);
        Assert.True(showSeconds > lastWave.Max(move => move.GoesOff.Start), "the show ends before the last wave's rockets have gone off");

        // Only what comes after that is hurried when the finale is long.
        FinalePace pace = FinalePace.Of(script, finale.RocketsFired);
        Assert.Equal(pace.OfTheShow, pace.At(lastWave[^1].GoesOff.Start));
    }

    /// <summary>The rockets of a one-wave finale that go off sooner than their turn among the thrown ones would have them.</summary>
    private static IEnumerable<ThrownMove> RocketsSetOffByAStroke(TurnScript script)
    {
        List<ThrownMove> wave = WavesOf(script)[0];
        float firstGoesOff = wave.Max(move => move.Flight.End) + Tempo.RocketsRest;
        float goOffEvery = Math.Min(Tempo.ArmedEvery, Tempo.RocketsFireWithin / wave.Count);
        return wave.Where((move, thrown) => move.GoesOff.Start < firstGoesOff + thrown * goOffEvery - Tolerance);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(30)]
    public void No_stroke_of_a_wave_passes_over_a_gem_or_a_rocket_and_leaves_it_standing(int movesLeft)
    {
        (_, TurnScript script) = WonWith(movesLeft).PlayTheFinale();

        Assert.NotEmpty(StrokesOfTheFirstWave(script));
        Assert.Empty(PassedOverAndLeftStanding(script));
    }

    /// <summary>The strokes of a finale's first wave: its hits are all shown before the first piece falls; later hits are a cascade's or a later wave's.</summary>
    private static Cue.Effect[] StrokesOfTheFirstWave(TurnScript script)
    {
        float boardSettles = script.Cues.Where(IsATravel).Select(travel => travel.Start).DefaultIfEmpty(float.PositiveInfinity).Min();
        return StrokesOf(script).Where(stroke => stroke.Start < boardSettles).ToArray();
    }

    /// <summary>
    /// Where a stroke of a finale's first wave runs through a cell and the piece that lies there only goes later,
    /// one line for each. Only pieces that one hit takes are looked at: a gem or a special piece that no tape holds.
    /// </summary>
    private static List<string> PassedOverAndLeftStanding(TurnScript script)
    {
        Cue.Effect[] strokes = StrokesOfTheFirstWave(script);
        float boardSettles = script.Cues.Where(IsATravel).Select(travel => travel.Start).DefaultIfEmpty(float.PositiveInfinity).Min();
        Dictionary<int, float> appearedAt = Of<Cue.Appear>(script).ToDictionary(appear => appear.PieceId, appear => appear.Start);
        HashSet<Position> taped = Of<Cue.TapeHit>(script).Select(hit => hit.At).ToHashSet();
        var passedOver = new List<string>();
        foreach (Cue.Leave leave in Of<Cue.Leave>(script).Where(leave => leave.Start < boardSettles && (leave.Tile.IsGem || leave.Tile.IsSpecial) && !taped.Contains(leave.At)))
        {
            float laySince = appearedAt.TryGetValue(leave.PieceId, out float appeared) ? appeared : 0f;
            foreach (Cue.Effect stroke in strokes.Where(stroke => RunsThrough(stroke, leave.At)))
            {
                float reached = Reaches(stroke, leave.At);
                if (reached > laySince + Tolerance && reached < leave.Start - Tolerance)
                {
                    passedOver.Add(FormattableString.Invariant($"the stroke from {stroke.Origin} reaches {leave.At} at {reached:F3}, and the {leave.Tile} there only goes at {leave.Start:F3}"));
                }
            }
        }

        return passedOver;
    }

    /// <summary>
    /// Won boards on which a special piece that is no rocket stands when the finale begins: a bomb, a seeker, a
    /// colour bomb, and all three. A third of the boards players win hold one.
    /// </summary>
    public static TheoryData<string> BoardsWithASpecialPieceStanding() => new TheoryData<string> { "X", "S", "C", "XSC" };

    [Theory]
    [MemberData(nameof(BoardsWithASpecialPieceStanding))]
    public void No_stroke_of_a_wave_passes_over_a_piece_that_a_bomb_a_seeker_or_a_colour_bomb_of_the_wave_takes(string standing)
    {
        // The engine works a wave out effect by effect: a seeker that a stroke set off may take a gem on another
        // stroke's line before the engine fires that stroke. Seen, the stroke gets there first, and must take it.
        string[] board = Filler(9, 9);
        var places = new[] { P(6, 5), P(4, 7), P(7, 2) };
        for (int piece = 0; piece < standing.Length; piece++)
        {
            board = board.With(places[piece].X, places[piece].Y, standing[piece]);
        }

        int setOff = 0;
        foreach (ulong seed in Enumerable.Range(1, 12).Select(seed => (ulong)seed))
        {
            Table table = WonOn(board, movesLeft: 12, seed);

            (_, TurnScript script) = table.PlayTheFinale();

            float boardSettles = script.Cues.Where(IsATravel).Min(travel => travel.Start);
            setOff += Of<Cue.Effect>(script).Count(effect => effect.Start < boardSettles && !StrokesOf(script).Contains(effect));
            Assert.Empty(PassedOverAndLeftStanding(script));
            Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
            AssertCanBePlayed(script, table.Ledger);
            AssertWhatWentHasALeaving(script, table);
        }

        Assert.True(setOff > 0, "no stroke set a special piece off in any of these finales: this row proves nothing");
    }

    /// <summary>
    /// Levels of the game's own pack whose won board held a seeker, a bomb or a colour bomb that took a gem on a
    /// later stroke's line, when the expert bot was given fifteen moves more than the level has.
    /// </summary>
    public static TheoryData<int> PackLevelsWonWithASpecialPieceStanding() => new TheoryData<int> { 15, 23, 27, 31, 69 };

    [Theory]
    [MemberData(nameof(PackLevelsWonWithASpecialPieceStanding))]
    public void In_won_games_of_the_pack_no_stroke_passes_over_a_piece_that_another_special_piece_takes(int levelNumber)
    {
        LevelDefinition level = LevelJson.Load(PackLevelPath(levelNumber));

        (Table table, TurnScript script) = FinaleOfAGameWonWithMovesToSpare(level, movesToSpare: 15, seed: 1)
            ?? throw new InvalidOperationException($"The game of level {levelNumber} was not won with moves left: this row proves nothing.");

        Assert.Contains(Of<Cue.Effect>(script), effect => !StrokesOf(script).Contains(effect));
        Assert.Empty(PassedOverAndLeftStanding(script));
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(30)]
    public void Nothing_falls_or_comes_in_before_the_last_hit_of_its_wave_is_shown(int movesLeft)
    {
        Table table = WonWith(movesLeft);
        Table twin = WonWith(movesLeft);
        (_, TurnScript script) = table.PlayTheFinale();

        // The same finale, told only up to where its board begins to settle: every hit of its first wave.
        twin.Game.PlayFinale();
        List<GameEvent> hitsOfTheWave = twin.Game.LastEvents.TakeWhile(e => !Settles(e)).ToList();
        TurnScript wave = TurnChoreographer.ComposeFinale(twin.Ledger, hitsOfTheWave, twin.Game.Board);
        float lastHit = wave.Cues.Where(IsAHit).Max(hit => hit.Start);

        // The collapse follows the last hit at once, and not a moment sooner.
        float firstFall = script.Cues.Where(IsATravel).Min(travel => travel.Start);
        Assert.Equal(lastHit + Tempo.CellFreeAfterHit * Tempo.FinaleBurstPace, firstFall, Tolerance);
    }

    [Theory]
    [InlineData(30, 9)]
    [InlineData(29, 4)]
    public void No_stroke_of_the_finale_runs_through_a_cell_after_a_piece_has_begun_to_fall_into_it(int movesLeft, int boardSide)
    {
        (_, TurnScript script) = WonWith(movesLeft, width: boardSide, height: boardSide).PlayTheFinale();

        Cue.Effect[] strokes = StrokesOf(script);
        Assert.All(script.Cues.Where(IsATravel), travel =>
        {
            foreach (Position cell in CellsOnTheWayOf(travel))
            {
                // A stroke of a later wave may come once the piece has landed: that piece is then its to hit.
                Assert.All(
                    strokes.Where(stroke => RunsThrough(stroke, cell) && stroke.Start < travel.End - Tolerance),
                    stroke => Assert.True(Reaches(stroke, cell) <= travel.Start + Tolerance, $"the stroke from {stroke.Origin} reaches {cell} at {Reaches(stroke, cell)}; a piece falls into it from {travel.Start} on"));
            }
        });
    }

    /// <summary>The cells of the board a travelling piece comes through, the one it lands in included.</summary>
    private static IEnumerable<Position> CellsOnTheWayOf(Cue travel) => travel switch
    {
        Cue.Fall fall => Enumerable.Range(fall.From.Y + 1, fall.To.Y - fall.From.Y).Select(row => new Position(fall.To.X, row)),
        Cue.Enter enter => Enumerable.Range(0, enter.To.Y + 1).Select(row => new Position(enter.To.X, row)),
        Cue.Slip slip => new[] { slip.To },
        _ => throw new InvalidOperationException($"{travel.GetType().Name} is not a travel."),
    };

    [Fact]
    public void In_the_finale_pieces_burst_at_the_quickest_pace_also_in_the_first_cascade_after_a_wave()
    {
        (Table table, _, TurnScript script) = FirstFinaleWith(movesLeft: 12, (events, _) => events.Any(e => e is GameEvent.RoundStarted));

        Assert.All(Of<Cue.Leave>(script).Where(leave => leave.Manner == LeaveManner.Pop), leave => Assert.Equal(Tempo.Pop * Tempo.FinaleBurstPace, leave.Seconds, Tolerance));
        Assert.All(Of<Cue.Leave>(script).Where(leave => leave.Manner == LeaveManner.Gather), leave => Assert.Equal(Tempo.Gather * Tempo.FinaleBurstPace, leave.Seconds, Tolerance));

        // The engine counts the rounds of every wave from nothing; in a turn round 0 is the player's own match, at an unhurried pace.
        var firstOfTheCascade = (GameEvent.TileDestroyed)table.Game.LastEvents.SkipWhile(e => e is not GameEvent.RoundStarted).First(e => e is GameEvent.TileDestroyed);
        float boardSettles = script.Cues.Where(IsATravel).Min(travel => travel.Start);
        Cue.Leave burst = Of<Cue.Leave>(script).First(leave => leave.At == firstOfTheCascade.At && leave.Tile == firstOfTheCascade.Tile && leave.Start > boardSettles);
        float landedBefore = script.Cues.Where(cue => IsATravel(cue) && cue.Start < burst.Start).Max(cue => cue.End);
        Assert.Equal(landedBefore + Tempo.PauseBeforeCascade * Tempo.FinaleBurstPace, burst.Start, Tolerance);
        Assert.True(Tempo.FinaleBurstPace < Tempo.PaceOfRound(0));
    }

    [Fact]
    public void A_board_with_fewer_free_gems_than_moves_left_gets_them_in_waves_each_on_a_board_that_has_landed()
    {
        // Sixteen cells and twenty-nine moves left: one wave cannot hold them.
        Table table = WonWith(movesLeft: 29, width: 4, height: 4);

        (FinaleOutcome finale, TurnScript script) = table.PlayTheFinale();

        List<List<ThrownMove>> waves = WavesOf(script);
        Assert.True(waves.Count > 1);
        Assert.Equal(29, finale.RocketsFired);
        Assert.Equal(29, waves.Sum(wave => wave.Count));
        Assert.All(waves.Skip(1), wave =>
        {
            float begins = wave[0].Flight.Start;
            float landed = script.Cues.Where(cue => IsATravel(cue) && cue.Start < begins).Max(cue => cue.End);
            Assert.True(begins >= landed + Tempo.PauseBeforeCascade * Tempo.FinaleBurstPace - Tolerance, $"a wave begins at {begins}; the board lands at {landed}");
        });
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
        AssertCanBePlayed(script, table.Ledger);
    }

    [Fact]
    public void A_special_piece_that_a_cascade_of_the_finale_leaves_is_not_a_move_thrown()
    {
        (Table table, FinaleOutcome finale, TurnScript script) = FirstFinaleWith(movesLeft: 12, (events, _) => ACascadeLeavesASpecialPiece(events));

        Assert.Equal(finale.RocketsFired, Of<Cue.MoveThrown>(script).Count());
        Assert.True(Of<Cue.Appear>(script).Count() > finale.RocketsFired);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
        AssertCanBePlayed(script, table.Ledger);
    }

    private static bool ACascadeLeavesASpecialPiece(IReadOnlyList<GameEvent> events)
    {
        bool isInARound = false;
        foreach (GameEvent gameEvent in events)
        {
            isInARound = gameEvent is GameEvent.RoundStarted || (isInARound && !Settles(gameEvent));
            if (isInARound && gameEvent is GameEvent.SpecialCreated)
            {
                return true;
            }
        }

        return false;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(30)]
    public void After_the_finale_the_ledger_says_what_the_engines_board_says_and_its_script_can_be_played_as_written(int movesLeft)
    {
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
        {
            Table table = WonWith(movesLeft, seed);

            (FinaleOutcome finale, TurnScript script) = table.PlayTheFinale();

            Assert.Equal(movesLeft, finale.RocketsFired);
            Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
            AssertCanBePlayed(script, table.Ledger);
            AssertWhatWentHasALeaving(script, table);
        }
    }

    /// <summary>
    /// Levels of the game's own pack with what a rocket's stroke meets on its way: crates (40, with a hole in its
    /// board), crates and slabs (640, 280), slabs in layers (285), tape (644, 426), items that only fall (206).
    /// </summary>
    public static TheoryData<int> PackLevelsWithObstacles() => new TheoryData<int> { 40, 640, 280, 285, 644, 426, 206 };

    [Theory]
    [MemberData(nameof(PackLevelsWithObstacles))]
    public void Through_whole_won_games_of_the_pack_every_finale_can_be_played_and_counts_down_to_what_the_engine_fired(int levelNumber)
    {
        LevelDefinition level = LevelJson.Load(PackLevelPath(levelNumber));
        int finales = 0;
        foreach (ulong seed in new ulong[] { 1, 2 })
        {
            finales += FinaleOfAWholeGame(level, movesToSpare: 15, seed) ? 1 : 0;
        }

        Assert.True(finales > 0, $"no game of level {levelNumber} was won with moves left: this row proves nothing");
    }

    /// <summary>What stands in a stroke's way and takes its hit without going at once, or without going at all.</summary>
    public enum Obstacle
    {
        Crate,
        Slab,
        Tape,
    }

    /// <summary>
    /// The same levels with the obstacles each still holds when it is won with its goals cut to a fifth: the
    /// crates of 40, the crates and slabs of 640 and 280, the slabs of 285, the tape of 644 and 426.
    /// </summary>
    public static TheoryData<int, Obstacle[]> PackLevelsWonWithObstaclesStanding() => new TheoryData<int, Obstacle[]>
    {
        { 40, new[] { Obstacle.Crate } },
        { 640, new[] { Obstacle.Crate, Obstacle.Slab } },
        { 280, new[] { Obstacle.Crate, Obstacle.Slab } },
        { 285, new[] { Obstacle.Slab } },
        { 644, new[] { Obstacle.Tape } },
        { 426, new[] { Obstacle.Tape } },
    };

    [Theory]
    [MemberData(nameof(PackLevelsWonWithObstaclesStanding))]
    public void A_finale_on_a_board_that_still_holds_its_obstacles_hits_them_and_can_be_played_as_written(int levelNumber, Obstacle[] obstacles)
    {
        // No level of the pack is won with a crate, a tape or a slab still standing: its goals ask for every one of
        // them. With its goals cut to a small part the same board is, and the finale's strokes meet them.
        LevelDefinition level = WithGoalsCutTo(0.2f, LevelJson.Load(PackLevelPath(levelNumber)));
        var stood = new Dictionary<Obstacle, int>();
        var hit = new Dictionary<Obstacle, int>();
        foreach (ulong seed in new ulong[] { 1, 2, 3 })
        {
            if (!(WonByTheExpertWithMovesLeft(level, movesToSpare: 0, seed) is Table table))
            {
                continue;
            }

            foreach (Obstacle obstacle in obstacles)
            {
                stood[obstacle] = stood.GetValueOrDefault(obstacle) + StandingOn(table.Ledger, obstacle);
            }

            TurnScript script = FinalePlayedAsWritten(table);
            foreach (Obstacle obstacle in obstacles)
            {
                hit[obstacle] = hit.GetValueOrDefault(obstacle) + HitsOn(obstacle, script);
            }
        }

        Assert.All(obstacles, obstacle =>
        {
            Assert.True(stood.GetValueOrDefault(obstacle) > 0, $"no won board of level {levelNumber} held a {obstacle}: this row proves nothing about one");
            Assert.True(hit.GetValueOrDefault(obstacle) > 0, $"no finale of level {levelNumber} hit a {obstacle}, though {stood[obstacle]} stood");
        });
    }

    /// <summary>How many of an obstacle the view's board holds: crates, cells with a slab, cells under tape.</summary>
    private static int StandingOn(PieceLedger ledger, Obstacle obstacle) => obstacle switch
    {
        Obstacle.Crate => ledger.Pieces.Count(piece => piece.Tile.Kind == TileKind.Box),
        Obstacle.Slab => ledger.Cells().Count(cell => ledger.SlabAt(cell) > 0),
        Obstacle.Tape => ledger.Cells().Count(cell => ledger.TapeAt(cell) > 0),
        _ => throw new ArgumentOutOfRangeException(nameof(obstacle), obstacle, "An obstacle that is not known here."),
    };

    /// <summary>How many hits on an obstacle a script shows: a crate that gives or breaks, a slab layer or a tape that takes one.</summary>
    private static int HitsOn(Obstacle obstacle, TurnScript script) => obstacle switch
    {
        Obstacle.Crate => Of<Cue.Reface>(script).Count() + Of<Cue.Leave>(script).Count(leave => leave.Manner == LeaveManner.Break),
        Obstacle.Slab => Of<Cue.SlabHit>(script).Count(),
        Obstacle.Tape => Of<Cue.TapeHit>(script).Count(),
        _ => throw new ArgumentOutOfRangeException(nameof(obstacle), obstacle, "An obstacle that is not known here."),
    };

    [Fact]
    public void A_stroke_of_the_finale_passes_over_an_item_and_leaves_it_to_drop_out_through_its_exit()
    {
        // Level 206 asks for items to be dropped; with its goal cut to a fifth it is won while items are on the board.
        LevelDefinition level = WithGoalsCutTo(0.2f, LevelJson.Load(PackLevelPath(206)));
        int itemsOnTheBoards = 0;
        int itemsUnderAStroke = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3 })
        {
            if (!(WonByTheExpertWithMovesLeft(level, movesToSpare: 0, seed) is Table table))
            {
                continue;
            }

            PlacedPiece[] items = table.Ledger.Pieces.Where(piece => piece.Tile.Kind == TileKind.Item).ToArray();
            TurnScript script = FinalePlayedAsWritten(table);

            itemsOnTheBoards += items.Length;
            itemsUnderAStroke += items.Count(item => StrokesOfTheFirstWave(script).Any(stroke => RunsThrough(stroke, item.Cell)));
            Assert.All(Of<Cue.Leave>(script).Where(leave => leave.Tile.Kind == TileKind.Item), leave => Assert.Equal(LeaveManner.Collect, leave.Manner));
        }

        Assert.True(itemsOnTheBoards > 0, "no won board of level 206 held an item: this proves nothing");
        Assert.True(itemsUnderAStroke > 0, "no stroke ran through an item's cell: this proves nothing");
    }

    [Fact]
    public void An_item_that_waits_a_moment_for_its_exit_in_a_finale_has_stopped_giving_when_it_drops_on_and_out()
    {
        // Level 316 with its goals cut to a fifth and seventy moves to spare, as the expert bot wins it on seed 2:
        // two items stand in one column when the finale begins. The upper one comes down seven cells behind the
        // lower one, a few milliseconds before the exit is free again, drops into it and out.
        LevelDefinition level = WithGoalsCutTo(0.2f, LevelJson.Load(PackLevelPath(316)));
        Table table = WonByTheExpertWithMovesLeft(level, movesToSpare: 70, seed: 2)
            ?? throw new InvalidOperationException("The game of level 316 was not won with moves left: this proves nothing.");

        TurnScript script = FinalePlayedAsWritten(table);

        // The items that came to rest a cell over their exit before they dropped into it: their last two travels.
        (Cue Landed, Cue DropsOn)[] waited = Of<Cue.Leave>(script)
            .Where(leave => leave.Manner == LeaveManner.Collect)
            .Select(dropsOut => TravelsOf(dropsOut.PieceId, script))
            .Where(travels => travels.Length >= 2 && RestsBefore(travels[^1], travels[^2]) > NoPause)
            .Select(travels => (travels[^2], travels[^1]))
            .ToArray();

        (Cue landed, Cue dropsOn) = Assert.Single(waited);
        float waits = RestsBefore(dropsOn, landed);
        Assert.True(waits + dropsOn.Seconds < Tempo.Land, "a whole landing would not outlast the item's way out: this proves nothing");
        Assert.Equal(waits, LandingSecondsOf(landed), Tolerance);
        Assert.Equal(0f, LandingSecondsOf(dropsOn));
        AssertEveryLandingLastsAsLongAsItsPieceRests(script);
    }

    /// <summary>
    /// Plays a whole game of a level with the expert bot and, if it is won with moves left, its finale; checks
    /// that every script of it, the finale's too, can be played as it is written.
    /// </summary>
    /// <returns>True when the game had a finale.</returns>
    private static bool FinaleOfAWholeGame(LevelDefinition level, int movesToSpare, ulong seed)
    {
        if (!(WonByTheExpertWithMovesLeft(level, movesToSpare, seed) is Table table))
        {
            return false;
        }

        FinalePlayedAsWritten(table);
        return true;
    }

    /// <summary>
    /// Plays the finale of a game that is won with moves left and checks its script: it ends on the engine's
    /// board, can be played as it is written, throws as many moves as the engine made rockets, counts toward no
    /// goal and is shown within the longest time.
    /// </summary>
    private static TurnScript FinalePlayedAsWritten(Table table)
    {
        int[] goalsLeft = Remaining(table.Game);
        (FinaleOutcome finale, TurnScript script) = table.PlayTheFinale();

        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
        AssertCanBePlayed(script, table.Ledger);
        AssertWhatWentHasALeaving(script, table);
        Assert.Equal(finale.RocketsFired, Of<Cue.MoveThrown>(script).Count());
        Assert.Empty(Of<Cue.MoveSpent>(script));
        Assert.Empty(Of<Cue.GoalTick>(script));
        Assert.Equal(goalsLeft, Remaining(table.Game));
        Assert.True(ShownFor(script, finale) <= Tempo.FinaleLongest + 2f * Frame);
        return script;
    }

    /// <summary>
    /// A whole game of a level as the expert bot plays it with so many moves more than the level has, at the
    /// moment its finale begins: won, with moves left, the view's ledger at the board the winning move left.
    /// Nothing when the game was lost or won with its last move.
    /// </summary>
    private static Table? WonByTheExpertWithMovesLeft(LevelDefinition level, int movesToSpare, ulong seed)
    {
        Assert.True(BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot));
        var options = new GameOptions { RecordEvents = true, MoveLimitOverride = level.MoveLimit + movesToSpare };
        var table = new Table(Game.Start(level, seed, options));
        var rng = new Rng(seed, 7);
        while (table.Game.Status == GameStatus.Playing)
        {
            table.Play(bot.ChooseMove(table.Game, table.Game.FindLegalMoves(), rng));
        }

        return table.Game.Status == GameStatus.Won && table.Game.MovesLeft > 0 ? table : null;
    }

    /// <summary>The same game with its finale played and written; nothing when the game has none.</summary>
    private static (Table Table, TurnScript Script)? FinaleOfAGameWonWithMovesToSpare(LevelDefinition level, int movesToSpare, ulong seed)
    {
        if (!(WonByTheExpertWithMovesLeft(level, movesToSpare, seed) is Table table))
        {
            return null;
        }

        return (table, table.PlayTheFinale().Script);
    }

    /// <summary>The same level with every goal's count cut to a part of it, one at least: it is won while most of what it asks for still stands.</summary>
    private static LevelDefinition WithGoalsCutTo(float part, LevelDefinition level)
    {
        int Cut(GoalSpec goal) => Math.Max(1, (int)(goal.Target * part));
        GoalSpec[] goals = level.Goals
            .Select(goal => goal switch
            {
                GoalSpec.CollectColor collect => new GoalSpec.CollectColor(collect.Color, Cut(goal)),
                GoalSpec.BreakBoxes _ => new GoalSpec.BreakBoxes(Cut(goal)),
                GoalSpec.ClearJelly _ => new GoalSpec.ClearJelly(Cut(goal)),
                GoalSpec.BreakChains _ => new GoalSpec.BreakChains(Cut(goal)),
                GoalSpec.DropItems _ => (GoalSpec)new GoalSpec.DropItems(Cut(goal)),
                _ => throw new InvalidOperationException($"A goal of the kind {goal.GetType().Name} is not known here."),
            })
            .ToArray();
        return new LevelDefinition(level.Id, level.Difficulty, level.Layout, goals, level.MoveLimit, level.Spawn, level.Attempts, level.ContentVersion);
    }

    [Theory]
    [InlineData(1, 2f)]
    [InlineData(10, 3.5f)]
    public void A_few_moves_left_are_thrown_fired_and_at_rest_within_seconds_cascades_aside(int movesLeft, float atRestWithin)
    {
        Table table = WonWith(movesLeft);
        table.Game.PlayFinale();

        // Only the wave itself is timed here: what the refill happens to set off afterwards is luck.
        List<GameEvent> wave = table.Game.LastEvents.TakeWhile(e => e is not GameEvent.RoundStarted).ToList();
        TurnScript script = TurnChoreographer.ComposeFinale(table.Ledger, wave, table.Game.Board);

        Assert.InRange(script.Seconds, 1f, atRestWithin);
    }

    [Fact]
    public void A_finale_may_take_four_seconds_for_up_to_ten_moves_a_little_more_for_every_move_beyond_and_never_more_than_its_longest_time()
    {
        Assert.Equal(4f, Tempo.FinaleMayTake(1));
        Assert.Equal(4f, Tempo.FinaleMayTake(10));
        Assert.Equal(4.4f, Tempo.FinaleMayTake(12), Tolerance);
        Assert.Equal(Tempo.FinaleLongest, Tempo.FinaleMayTake(40));
        Assert.Equal(Tempo.FinaleLongest, Tempo.FinaleMayTake(900));
        Assert.InRange(Tempo.FinaleLongest, 5f, 6f);
    }

    /// <summary>A finale's script of a length, of which so much is its show; no cue is needed to tell how fast it is played.</summary>
    private static TurnScript ScriptOf(float seconds, float? showSeconds) => new TurnScript(Array.Empty<Cue>(), seconds, showSeconds);

    /// <summary>The steps of a 60 Hz screen, which the stage's clock is moved in.</summary>
    private const float Frame = 1f / 60f;

    /// <summary>
    /// How long the stage takes to play a finale's script to its end: each step of its clock moves the script on by
    /// the pace at the moment the step begins, which is what GameStage.Advance does with a finale on its timeline.
    /// </summary>
    private static float PlayedOnTheStagesClock(TurnScript script, FinalePace pace)
    {
        float played = 0f;
        int frames = 0;
        while (played < script.Seconds)
        {
            Assert.True(pace.At(played) >= 1f, $"at {played} s the script is played at {pace.At(played)}: slower than written, or not at all");
            played += Frame * pace.At(played);
            frames++;
        }

        return frames * Frame;
    }

    [Fact]
    public void A_finale_that_is_over_in_the_time_its_moves_may_take_is_played_as_it_is_written()
    {
        float mayTake = Tempo.FinaleMayTake(3);

        FinalePace withAnAftermath = FinalePace.Of(ScriptOf(mayTake, showSeconds: 1.8f), moves: 3);
        FinalePace allShow = FinalePace.Of(ScriptOf(1.9f, showSeconds: null), moves: 3);
        FinalePace empty = FinalePace.Of(ScriptOf(0f, showSeconds: null), moves: 0);

        Assert.Equal((1f, 1f), (withAnAftermath.OfTheShow, withAnAftermath.OfTheAftermath));
        Assert.Equal(mayTake, withAnAftermath.ShownSeconds, Tolerance);
        Assert.Equal((1f, 1f, 1.9f), (allShow.OfTheShow, allShow.OfTheAftermath, allShow.ShownSeconds));
        Assert.Equal((1f, 0f), (empty.OfTheShow, empty.ShownSeconds));
        Assert.All(new[] { 0f, 1.79f, 1.8f, 2.5f, mayTake }, moment => Assert.Equal(1f, withAnAftermath.At(moment)));
        Assert.Throws<ArgumentOutOfRangeException>(() => FinalePace.Of(ScriptOf(1f, null), moves: -1));
    }

    [Fact]
    public void A_cascade_after_a_few_moves_is_not_hurried_while_the_finale_is_over_in_four_seconds()
    {
        // Three moves: the rockets are at rest after 2.3 s, and what fell in bursts for another 1.44 s. Hurried to
        // be over in a time that grew with the count of moves, that cascade ran at twice its pace.
        FinalePace pace = FinalePace.Of(ScriptOf(2.3f + 1.44f, showSeconds: 2.3f), moves: 3);

        Assert.Equal((1f, 1f), (pace.OfTheShow, pace.OfTheAftermath));
        Assert.Equal(3.74f, pace.ShownSeconds, Tolerance);
    }

    [Fact]
    public void A_long_aftermath_is_played_faster_so_that_the_finale_is_over_about_in_time_and_its_show_is_not()
    {
        // Three moves may take 4 s. The show takes 2.4 s; what the pieces that fell in set off takes 2 s more.
        TurnScript script = ScriptOf(4.4f, showSeconds: 2.4f);

        FinalePace pace = FinalePace.Of(script, moves: 3);

        Assert.Equal(1f, pace.OfTheShow);
        Assert.Equal(1.25f, pace.OfTheAftermath, Tolerance);

        // Picking the pace up costs a little of what the hurry saves.
        Assert.InRange(pace.ShownSeconds, Tempo.FinaleMayTake(3), Tempo.FinaleMayTake(3) + 0.05f);
        Assert.InRange(PlayedOnTheStagesClock(script, pace), pace.ShownSeconds - Frame, pace.ShownSeconds + 2f * Frame);
    }

    [Fact]
    public void The_aftermath_picks_its_pace_up_evenly_and_is_never_hurried_between_two_frames()
    {
        TurnScript script = ScriptOf(5.6f, showSeconds: 1.6f);
        FinalePace pace = FinalePace.Of(script, moves: 1);
        float fastest = Tempo.FinaleAftermathFastest;

        Assert.Equal(1f, pace.At(0f));
        Assert.Equal(1f, pace.At(1.6f));
        Assert.Equal(1f + (fastest - 1f) / 2f, pace.At(1.6f + Tempo.FinaleHurriesWithin / 2f), Tolerance);
        Assert.Equal(fastest, pace.At(1.6f + Tempo.FinaleHurriesWithin), Tolerance);
        Assert.Equal(fastest, pace.At(5.5f));

        // From one frame of the stage to the next the pace changes by a small part of itself, at the most.
        float played = 0f;
        while (played < script.Seconds)
        {
            float before = pace.At(played);
            played += Frame * before;
            Assert.InRange(pace.At(played) - before, 0f, 0.06f);
        }
    }

    [Fact]
    public void An_aftermath_is_never_rushed_beyond_its_fastest_pace_so_such_a_finale_takes_longer_than_it_should()
    {
        // A cascade of four seconds after one rocket: at its fastest it still takes more than two and a half.
        TurnScript script = ScriptOf(5.6f, showSeconds: 1.6f);
        FinalePace long_ = FinalePace.Of(script, moves: 1);
        FinalePace showAloneIsTooLong = FinalePace.Of(ScriptOf(4.6f, showSeconds: 4.2f), moves: 1);

        Assert.Equal((1f, Tempo.FinaleAftermathFastest), (long_.OfTheShow, long_.OfTheAftermath));
        Assert.InRange(long_.ShownSeconds, 1.6f + 4f / Tempo.FinaleAftermathFastest, 1.6f + 4f / Tempo.FinaleAftermathFastest + 0.05f);
        Assert.True(long_.ShownSeconds > Tempo.FinaleMayTake(1));
        Assert.InRange(PlayedOnTheStagesClock(script, long_), long_.ShownSeconds - Frame, long_.ShownSeconds + 2f * Frame);
        Assert.Equal((1f, Tempo.FinaleAftermathFastest), (showAloneIsTooLong.OfTheShow, showAloneIsTooLong.OfTheAftermath));
        Assert.InRange(Tempo.FinaleAftermathFastest, 1.2f, 1.6f);
    }

    [Fact]
    public void No_finale_is_shown_for_longer_than_the_longest_time_whatever_pace_that_takes()
    {
        // Wave after wave on a small board: the show alone is longer than any finale may be.
        TurnScript wavesOnly = ScriptOf(3f * Tempo.FinaleLongest, showSeconds: null);
        TurnScript wavesAndACascade = ScriptOf(12f, showSeconds: 8f);
        FinalePace allShow = FinalePace.Of(wavesOnly, moves: 300);
        FinalePace withAnAftermath = FinalePace.Of(wavesAndACascade, moves: 300);

        Assert.Equal(3f, allShow.OfTheShow, Tolerance);
        Assert.Equal(Tempo.FinaleLongest, allShow.ShownSeconds, Tolerance);
        Assert.Equal(Tempo.FinaleLongest, withAnAftermath.ShownSeconds, Tolerance);
        Assert.True(withAnAftermath.OfTheShow > 1f);
        Assert.Equal(Tempo.FinaleAftermathFastest * withAnAftermath.OfTheShow, withAnAftermath.OfTheAftermath, Tolerance);
        Assert.InRange(PlayedOnTheStagesClock(wavesOnly, allShow), Tempo.FinaleLongest - Frame, Tempo.FinaleLongest + 2f * Frame);
        Assert.InRange(PlayedOnTheStagesClock(wavesAndACascade, withAnAftermath), Tempo.FinaleLongest - Frame, Tempo.FinaleLongest + 2f * Frame);
    }

    [Fact]
    public void The_show_of_a_finale_ends_when_the_board_has_come_down_after_its_last_rockets_and_begins_to_burst_by_itself()
    {
        (Table table, _, TurnScript script) = FirstFinaleWith(movesLeft: 5, (events, _) => events.Any(e => e is GameEvent.RoundStarted));
        (_, _, TurnScript withoutAnAftermath) = FirstFinaleWith(movesLeft: 5, (events, _) => !events.Any(e => e is GameEvent.RoundStarted));
        TurnScript ofAMove = TableOf(BoardWithAMatchInReach()).Play(Move.Swap(P(2, 2), P(2, 3)));

        // Up to the first match the pieces that fell in make: the board has landed a quick pause before it bursts.
        var firstOfTheAftermath = (GameEvent.TileDestroyed)table.Game.LastEvents.SkipWhile(e => e is not GameEvent.RoundStarted).First(e => e is GameEvent.TileDestroyed);
        float boardSettles = script.Cues.Where(IsATravel).Min(travel => travel.Start);
        Cue.Leave burst = Of<Cue.Leave>(script).First(leave => leave.At == firstOfTheAftermath.At && leave.Tile == firstOfTheAftermath.Tile && leave.Start > boardSettles);
        float showSeconds = Assert.NotNull(script.ShowSeconds);
        Assert.Equal(burst.Start - Tempo.PauseBeforeCascade * Tempo.FinaleBurstPace, showSeconds, Tolerance);
        Assert.InRange(showSeconds, 1.5f, script.Seconds);

        Assert.Null(withoutAnAftermath.ShowSeconds);
        Assert.Null(ofAMove.ShowSeconds);
    }

    [Fact]
    public void However_many_moves_are_left_the_finale_is_shown_within_its_longest_time()
    {
        // Thirty moves on a board with room for them; eighty on one that every gem of is taken; ninety on sixteen cells, wave after wave.
        (FinaleOutcome Outcome, TurnScript Script)[] finales =
        {
            WonWith(movesLeft: 30).PlayTheFinale(),
            WonWith(movesLeft: 80).PlayTheFinale(),
            WonWith(movesLeft: 90, width: 4, height: 4).PlayTheFinale(),
        };

        Assert.Contains(finales, finale => finale.Script.Seconds > Tempo.FinaleLongest);
        Assert.All(finales, finale => Assert.True(ShownFor(finale.Script, finale.Outcome) <= Tempo.FinaleLongest + 2f * Frame));
    }

    /// <summary>
    /// How long a finale is shown: its script played frame by frame at the pace the stage asks for at each frame.
    /// The time the pace says it takes is not trusted here: it is checked against the playing.
    /// </summary>
    private static float ShownFor(TurnScript script, FinaleOutcome finale)
    {
        FinalePace pace = FinalePace.Of(script, finale.RocketsFired);
        float played = PlayedOnTheStagesClock(script, pace);
        Assert.InRange(played, pace.ShownSeconds - Frame, pace.ShownSeconds + 2f * Frame);
        return played;
    }

    [Fact]
    public void The_same_game_has_the_same_script_of_its_finale()
    {
        string one = Told(WonWith(movesLeft: 12, seed: 3).PlayTheFinale().Script);
        string same = Told(WonWith(movesLeft: 12, seed: 3).PlayTheFinale().Script);
        string other = Told(WonWith(movesLeft: 12, seed: 4).PlayTheFinale().Script);

        Assert.Equal(one, same);
        Assert.NotEqual(one, other);
    }

    /// <summary>A script as a text: what each cue is, when it begins and how long it takes.</summary>
    private static string Told(TurnScript script) =>
        string.Join("\n", script.Cues.Select(cue => FormattableString.Invariant($"{cue.GetType().Name} {cue.Start:R} {cue.Seconds:R}")));

    [Fact]
    public void A_finale_with_nothing_in_it_has_an_empty_script()
    {
        Table table = WonWith(movesLeft: 0);

        (FinaleOutcome finale, TurnScript script) = table.PlayTheFinale();

        Assert.Equal(0, finale.RocketsFired);
        Assert.Empty(script.Cues);
        Assert.Equal(0f, script.Seconds);
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void A_special_piece_made_on_a_gem_in_a_move_with_no_colour_bomb_at_work_is_a_fault()
    {
        Table table = TableOf(BoardWithAMatchInReach());
        var events = new GameEvent[] { new GameEvent.SpecialCreated(P(1, 1), TileKind.RocketH) };

        Assert.Throws<InvalidOperationException>(() => TurnChoreographer.Compose(table.Ledger, table.Game.Level.Goals, events, table.Game.Board));
    }

    [Fact]
    public void A_finale_that_makes_anything_but_a_rocket_of_a_gem_is_a_fault()
    {
        Table bombOnAGem = TableOf(BoardWithAMatchInReach());
        Table rocketOnACrate = TableOf(BoardWithAMatchInReach().With(1, 1, '2'));
        var bomb = new GameEvent[] { new GameEvent.SpecialCreated(P(1, 1), TileKind.Bomb) };
        var rocket = new GameEvent[] { new GameEvent.SpecialCreated(P(1, 1), TileKind.RocketV) };

        Assert.Throws<InvalidOperationException>(() => TurnChoreographer.ComposeFinale(bombOnAGem.Ledger, bomb, bombOnAGem.Game.Board));
        Assert.Throws<InvalidOperationException>(() => TurnChoreographer.ComposeFinale(rocketOnACrate.Ledger, rocket, rocketOnACrate.Game.Board));
    }
}
