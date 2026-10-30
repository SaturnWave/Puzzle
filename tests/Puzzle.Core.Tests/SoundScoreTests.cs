using Puzzle.Client.Flow;
using Puzzle.Client.Play;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Meta;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// Which sound falls at which moment of play, and what of it reaches the player under the settings: the score of a
/// move, of each booster, of a won game's finale and of a card. <c>SoundScore</c> and <c>SoundCue</c> are the Unity
/// project's source files, compiled into these tests; the scripts are the ones the game shows
/// (<c>TurnChoreographer</c>). How the sounds sound and how a tick feels is not here: the owner's ear and hand judge
/// that on the phone.
/// </summary>
public class SoundScoreTests
{
    private const float Tolerance = 0.0005f;

    /// <summary>Three purple gems in the fourth row once the one above the third column is pulled down.</summary>
    private static readonly Move TheMatchingMove = Move.Swap(P(2, 2), P(2, 3));

    private static readonly WinReward WinPays = MetaSupport.Rules().Win;

    private static string[] BoardWithAMatchInReach() =>
        Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p');

    /// <summary>Plays a move on a game and writes its script, as the board's stage does.</summary>
    private static TurnScript ScriptOf(Game game, Move move)
    {
        PieceLedger ledger = PieceLedger.Of(game.Board);
        Assert.True(game.TryPlay(move), $"{move} is not legal");
        return TurnChoreographer.Compose(ledger, game.Level.Goals, game.LastEvents, game.Board);
    }

    private static float[] MomentsOf(IEnumerable<SoundNote> notes, SoundCue cue) =>
        notes.Where(note => note.Cue == cue).Select(note => note.At).ToArray();

    private static void AssertNoCueComesSoonerThanTheGapAfterItself(IReadOnlyList<SoundNote> notes)
    {
        foreach (IGrouping<SoundCue, SoundNote> ofOneCue in notes.GroupBy(note => note.Cue))
        {
            float[] moments = ofOneCue.Select(note => note.At).ToArray();
            for (int note = 1; note < moments.Length; note++)
            {
                Assert.True(moments[note] - moments[note - 1] >= SoundScore.SameCueApart - Tolerance, $"{ofOneCue.Key} at {moments[note - 1]} and again at {moments[note]}");
            }
        }
    }

    [Fact]
    public void A_match_is_heard_as_the_trade_when_the_pieces_start_to_slide_then_one_pop_for_its_three_pieces()
    {
        TurnScript script = ScriptOf(Start(Level(BoardWithAMatchInReach())), TheMatchingMove);

        IReadOnlyList<SoundNote> notes = SoundScore.OfATurn(script);

        float popped = script.Cues.OfType<Cue.Leave>().Min(leave => leave.Start);
        Assert.Equal(new SoundNote(0f, SoundCue.Swap), notes[0]);
        Assert.Equal(new SoundNote(popped, SoundCue.Match), notes[1]);
        Assert.Single(MomentsOf(notes, SoundCue.Swap));
        Assert.DoesNotContain(notes, note => note.Cue == SoundCue.Blast);
        AssertNoCueComesSoonerThanTheGapAfterItself(notes);
    }

    [Fact]
    public void A_rocket_traded_with_a_gem_is_heard_as_the_trade_then_its_blast_then_the_pops_of_its_stroke_thinned()
    {
        TurnScript script = ScriptOf(Start(Level(Filler(7, 7).With(3, 3, 'H'))), Move.Swap(P(3, 3), P(3, 4)));

        IReadOnlyList<SoundNote> notes = SoundScore.OfATurn(script);

        Cue.Effect stroke = script.Cues.OfType<Cue.Effect>().First();
        Cue.Leave[] pops = script.Cues.OfType<Cue.Leave>().Where(leave => leave.Manner == LeaveManner.Pop).ToArray();
        Assert.Equal(new SoundNote(0f, SoundCue.Swap), notes[0]);
        Assert.Contains(new SoundNote(stroke.Start, SoundCue.Blast), notes);
        float[] heardPops = MomentsOf(notes, SoundCue.Match);
        Assert.Equal(pops.Min(pop => pop.Start), heardPops[0], Tolerance);
        Assert.InRange(heardPops.Length, 1, pops.Length - 1); // the stroke pops a gem a cell, one after another: heard as a few, not each
        AssertNoCueComesSoonerThanTheGapAfterItself(notes);
    }

    [Fact]
    public void A_match_of_four_is_heard_with_the_special_piece_it_makes_under_which_the_phone_ticks_lightly()
    {
        string[] tiles = Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(3, 3, 'p').With(2, 2, 'p');
        TurnScript script = ScriptOf(Start(Level(tiles)), TheMatchingMove);

        IReadOnlyList<SoundNote> notes = SoundScore.OfATurn(script);

        Cue.Appear rocket = script.Cues.OfType<Cue.Appear>().First();
        Assert.Contains(new SoundNote(rocket.Start, SoundCue.SpecialMade), notes);
        Assert.Equal(Tick.Light, SoundScore.TickOf(SoundCue.SpecialMade));
        Assert.Null(SoundScore.TickOf(SoundCue.Match));
    }

    public static TheoryData<string> EveryBooster() => new TheoryData<string> { "cell", "row", "column", "shuffle" };

    private static BoosterUse BoosterNamed(string name) => name switch
    {
        "cell" => new BoosterUse.HitCell(P(4, 4)),
        "row" => new BoosterUse.HitRow(P(0, 4)),
        "column" => new BoosterUse.HitColumn(P(4, 0)),
        "shuffle" => new BoosterUse.Shuffle(),
        _ => throw new ArgumentException(name),
    };

    [Theory]
    [MemberData(nameof(EveryBooster))]
    public void Each_booster_is_heard_first_as_its_tool_strikes_and_the_shuffle_as_it_deals_with_no_sound_of_a_trade(string name)
    {
        Game game = Start(Level(Filler(9, 9).With(5, 4, 'H').With(1, 4, '1')));
        PieceLedger ledger = PieceLedger.Of(game.Board);
        Assert.True(game.TryUseBooster(BoosterNamed(name)), $"{name} was refused");
        TurnScript script = TurnChoreographer.ComposeBooster(ledger, game.Level.Goals, game.LastEvents, game.Board);

        IReadOnlyList<SoundNote> notes = SoundScore.OfABooster(script);

        float strikes = script.Cues.OfType<Cue.ToolUsed>().Select(tool => tool.StrikesAt).DefaultIfEmpty(0f).First();
        Assert.Equal(new SoundNote(strikes, SoundCue.Booster), notes[0]);
        Assert.Single(MomentsOf(notes, SoundCue.Booster));
        Assert.DoesNotContain(notes, note => note.Cue == SoundCue.Swap);
        Assert.Equal(Tick.Light, SoundScore.TickOf(SoundCue.Booster));
        AssertNoCueComesSoonerThanTheGapAfterItself(notes);
    }

    [Fact]
    public void A_finales_volley_is_heard_as_a_run_of_throws_no_closer_than_the_gap_and_its_rockets_are_heard_in_their_throws()
    {
        const int movesLeft = 12;
        string[] tiles = Filler(9, 9).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p');
        var goals = new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) };
        Game game = Start(Level(tiles, goals: goals, moves: movesLeft + 1));
        PieceLedger ledger = PieceLedger.Of(game.Board);
        Assert.True(game.TryPlay(TheMatchingMove));
        TurnChoreographer.Compose(ledger, game.Level.Goals, game.LastEvents, game.Board);
        Assert.Equal((GameStatus.Won, movesLeft), (game.Status, game.MovesLeft));
        game.PlayFinale();
        TurnScript finale = TurnChoreographer.ComposeFinale(ledger, game.LastEvents, game.Board);

        IReadOnlyList<SoundNote> notes = SoundScore.OfTheFinale(finale);

        float[] thrown = finale.Cues.OfType<Cue.MoveThrown>().Select(throwOf => throwOf.Start).ToArray();
        float[] heardThrows = MomentsOf(notes, SoundCue.MoveThrown);
        Assert.Equal(movesLeft, thrown.Length);
        Assert.Equal(thrown[0], heardThrows[0], Tolerance);
        Assert.All(heardThrows, heard => Assert.Contains(thrown, moment => Math.Abs(moment - heard) < Tolerance));
        Assert.InRange(heardThrows.Length, 2, movesLeft / 2 + 1); // twelve moves leave within 0.9 s: about every other one is heard
        Assert.InRange(MomentsOf(notes, SoundCue.Blast).Length, 1, finale.Cues.OfType<Cue.Effect>().Count());
        Assert.DoesNotContain(notes, note => note.Cue == SoundCue.SpecialMade);
        AssertNoCueComesSoonerThanTheGapAfterItself(notes);
    }

    [Fact]
    public void A_card_is_heard_as_the_win_the_loss_for_good_or_a_card()
    {
        Game won = Start(Level(BoardWithAMatchInReach(), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5));
        Assert.True(won.TryPlay(TheMatchingMove));
        Game outOfMoves = Start(Level(BoardWithAMatchInReach(), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3), new GoalSpec.CollectColor(GemColor.Red, 999) }, moves: 1));
        Assert.True(outOfMoves.TryPlay(TheMatchingMove));
        Game inPlay = Start(Level(BoardWithAMatchInReach()));
        AttemptBrief firstTry = AttemptBrief.FirstTry(WinPays);

        Assert.Equal(SoundCue.Win, SoundScore.OfACard(LevelResult.Of(won, firstTry)));
        Assert.Equal(SoundCue.CardDown, SoundScore.OfACard(LevelResult.OfferTo(outOfMoves, firstTry)));
        Assert.Equal(SoundCue.CardDown, SoundScore.OfACard(LevelResult.ForLeaving(inPlay, firstTry)));
        AttemptEvent.LostForGood gaveUp = OutOfMovesOffer.GiveUp(outOfMoves);
        Assert.Equal(SoundCue.Lose, SoundScore.OfACard(LevelResult.LostForGood(outOfMoves, firstTry, gaveUp.Reason)));
        Assert.Equal((Tick.Firm, (Tick?)null), (SoundScore.TickOf(SoundCue.Win)!.Value, SoundScore.TickOf(SoundCue.Lose)));
    }

    [Fact]
    public void With_every_setting_on_a_cue_is_heard_and_one_that_ticks_is_felt_too()
    {
        Assert.Equal((SoundCue.Win, Tick.Firm), SoundScore.Reaching(SoundCue.Win, DeviceSettings.Default));
        Assert.Equal((SoundCue.Press, (Tick?)null), SoundScore.Reaching(SoundCue.Press, DeviceSettings.Default));
    }

    [Fact]
    public void With_sound_off_no_cue_is_heard_and_with_vibration_off_none_is_felt()
    {
        DeviceSettings quiet = DeviceSettings.Default.With(DeviceSetting.Sound, isOn: false);
        DeviceSettings still = DeviceSettings.Default.With(DeviceSetting.Vibration, isOn: false);
        DeviceSettings neither = new DeviceSettings(sound: false, vibration: false);

        foreach (SoundCue cue in Enum.GetValues<SoundCue>())
        {
            Assert.Equal(((SoundCue?)null, SoundScore.TickOf(cue)), SoundScore.Reaching(cue, quiet));
            Assert.Equal((SoundScore.IsHeard(cue) ? cue : (SoundCue?)null, (Tick?)null), SoundScore.Reaching(cue, still));
            Assert.Equal(((SoundCue?)null, (Tick?)null), SoundScore.Reaching(cue, neither));
        }
    }

    [Fact]
    public void The_phone_ticks_under_five_cues_the_moments_the_player_made_happen_and_a_hint()
    {
        SoundCue[] ticking = Enum.GetValues<SoundCue>().Where(cue => SoundScore.TickOf(cue) != null).ToArray();

        Assert.Equal(new[] { SoundCue.SpecialMade, SoundCue.Booster, SoundCue.Win, SoundCue.PaintingWhole, SoundCue.Hint }, ticking);
    }

    [Fact]
    public void A_hint_is_felt_and_never_heard_and_the_vibration_switch_stills_it()
    {
        DeviceSettings still = DeviceSettings.Default.With(DeviceSetting.Vibration, isOn: false);

        Assert.Equal(((SoundCue?)null, (Tick?)Tick.Light), SoundScore.Reaching(SoundCue.Hint, DeviceSettings.Default));
        Assert.Equal(((SoundCue?)null, (Tick?)null), SoundScore.Reaching(SoundCue.Hint, still));
        Assert.Equal(new[] { SoundCue.Hint }, Enum.GetValues<SoundCue>().Where(cue => !SoundScore.IsHeard(cue)));
    }

    [Fact]
    public void A_stage_nobody_takes_its_cues_from_keeps_only_the_latest_and_a_take_empties_it()
    {
        var sounded = new CuesSounded();
        for (int cue = 0; cue < CuesSounded.Kept + 5; cue++)
        {
            sounded.Add(cue < 5 ? SoundCue.Press : SoundCue.Match);
        }

        IReadOnlyList<SoundCue> taken = sounded.TakeAll();

        Assert.Equal(CuesSounded.Kept, taken.Count);
        Assert.All(taken, cue => Assert.Equal(SoundCue.Match, cue));
        Assert.Empty(sounded.TakeAll());
    }
}
