using Puzzle.Bot;
using Puzzle.Client.Flow;
using Puzzle.Client.Play;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using Puzzle.Museum;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.MetaSupport;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// The booster intro (#7, the owner's decisions of 2026-10-19), as far as it is plain code (the Unity project's source
/// files, compiled into these tests): where its steps come, where a booster used by hand is aimed
/// (<c>BoosterLessonAim</c>), what the board takes while it is (<c>TutorialWatch</c>), that the use is free
/// (<c>AttemptLedger</c>) and how the record names it, what the pills say, and what the card of a long press plays on
/// its tiny board (<c>BoosterShowcase</c>). The boards are the tests' filler with a setup in purple.
/// </summary>
public class BoosterIntroTests
{
    private const ulong AttemptSeed = 20261019;

    private static readonly EconomyRules TheRules = Rules();

    private static readonly (TutorialStep Step, BoosterKind Booster, int Level)[] Openings =
    {
        (TutorialStep.BoosterHitCell, BoosterKind.HitCell, 7),
        (TutorialStep.BoosterHitRow, BoosterKind.HitRow, 12),
        (TutorialStep.BoosterHitColumn, BoosterKind.HitColumn, 16),
        (TutorialStep.BoosterShuffle, BoosterKind.Shuffle, 20),
    };

    // ---------- where the steps come ----------

    [Fact]
    public void Each_booster_used_during_play_is_used_by_hand_on_the_level_it_opens_on_and_on_no_other()
    {
        foreach ((TutorialStep step, BoosterKind booster, int level) in Openings)
        {
            Assert.Equal(level, TheRules.Unlocks.LevelOf(booster));
            Assert.Equal(new[] { level }, Enumerable.Range(1, 60).Where(each => TutorialPlan.MayComeOn(each, TutorialSeen.None, TheRules.Unlocks).Contains(step)));
            Assert.DoesNotContain(step, TutorialPlan.MayComeOn(level, TutorialSeen.None.With(step), TheRules.Unlocks));
        }
    }

    [Fact]
    public void A_booster_moved_to_another_level_by_the_economy_is_used_by_hand_there()
    {
        EconomyRules moved = Rules(file => file.boosterUnlocks.levels[0] = Opens("hit-cell", 9));

        Assert.Contains(TutorialStep.BoosterHitCell, TutorialPlan.MayComeOn(9, TutorialSeen.None, moved.Unlocks));
        Assert.DoesNotContain(TutorialStep.BoosterHitCell, TutorialPlan.MayComeOn(7, TutorialSeen.None, moved.Unlocks));
    }

    [Fact]
    public void The_start_boosters_are_shown_on_the_card_of_a_level_tried_before_and_not_won_once_and_never_on_a_board()
    {
        Assert.False(TutorialPlan.IntroducesTheStartBoosters(TutorialSeen.None, attemptsOnLevel: 0));
        Assert.True(TutorialPlan.IntroducesTheStartBoosters(TutorialSeen.None, attemptsOnLevel: 1));
        Assert.True(TutorialPlan.IntroducesTheStartBoosters(TutorialSeen.None, attemptsOnLevel: 5));
        Assert.False(TutorialPlan.IntroducesTheStartBoosters(TutorialSeen.None.With(TutorialStep.StartBoosters), attemptsOnLevel: 1));
        Assert.False(TutorialStep.StartBoosters.IsOnTheBoard());
        Assert.All(Enumerable.Range(1, 60), level => Assert.DoesNotContain(TutorialStep.StartBoosters, TutorialPlan.MayComeOn(level, TutorialSeen.None, TheRules.Unlocks)));
    }

    [Fact]
    public void A_save_from_before_the_tutorial_has_seen_none_of_the_booster_intro()
    {
        TutorialSeen older = TutorialPlan.OfAnOlderSave(new MuseumProgress(levelsWon: 400, PlacedPieces.None));

        Assert.All(Openings, opening => Assert.False(older.Has(opening.Step)));
        Assert.False(older.Has(TutorialStep.StartBoosters));
        Assert.True(older.Has(TutorialStep.Item));
    }

    [Fact]
    public void The_steps_of_the_booster_intro_come_after_the_older_steps_with_names_of_their_own_and_teach_their_booster()
    {
        Assert.Equal(
            new[] { "booster-hit-cell", "booster-hit-row", "booster-hit-column", "booster-shuffle", "start-boosters" },
            TutorialSteps.All.Skip(11).Select(TutorialSteps.NameOf));
        Assert.Equal(TutorialStep.FirstPainting, TutorialSteps.All[10]);
        Assert.All(Openings, opening =>
        {
            Assert.Equal(opening.Booster, opening.Step.BoosterOf());
            Assert.Equal(opening.Step, TutorialSteps.Teaching(opening.Booster));
            Assert.Equal(opening.Step, TutorialSteps.Find(opening.Step.NameOf()));
        });
        Assert.Null(TutorialStep.Match.BoosterOf());
        Assert.Throws<ArgumentException>(() => TutorialSteps.Teaching(BoosterKind.StartColorBomb));
        Assert.True(TutorialSeen.All.Has(TutorialStep.StartBoosters));
    }

    // ---------- where a booster used by hand is aimed ----------

    [Fact]
    public void The_spatula_is_aimed_at_the_gem_a_goal_collects_and_where_a_goal_breaks_crates_at_the_crate()
    {
        string[] onePurple = Playable().With(4, 1, 'p').With(1, 4, '1');
        Game collecting = Start(Level(onePurple, goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 10) }, colors: 6));
        Game breaking = Start(Level(onePurple, goals: new GoalSpec[] { new GoalSpec.BreakBoxes(1) }, colors: 6));

        Assert.Equal(new BoosterUse.HitCell(P(4, 1)), BoosterLessonAim.For(collecting, BoosterKind.HitCell, AttemptSeed));
        Assert.Equal(new BoosterUse.HitCell(P(1, 4)), BoosterLessonAim.For(breaking, BoosterKind.HitCell, AttemptSeed));
    }

    [Fact]
    public void Where_nothing_is_worth_more_the_spatula_goes_for_the_obstacle_before_a_gem()
    {
        Game game = Start(Level(Playable().With(0, 5, '1'), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 10) }, colors: 6));

        Assert.Equal(new BoosterUse.HitCell(P(0, 5)), BoosterLessonAim.For(game, BoosterKind.HitCell, AttemptSeed));
    }

    [Fact]
    public void A_brush_is_aimed_at_the_row_or_the_column_worth_the_most_to_the_goals()
    {
        GoalSpec[] purple = { new GoalSpec.CollectColor(GemColor.Purple, 10) };
        Game row = Start(Level(Playable().With(0, 4, 'p').With(2, 4, 'p').With(4, 4, 'p'), goals: purple, colors: 6));
        Game column = Start(Level(Playable().With(4, 0, 'p').With(4, 2, 'p').With(4, 5, 'p'), goals: purple, colors: 6));

        var toTheRow = Assert.IsType<BoosterUse.HitRow>(BoosterLessonAim.For(row, BoosterKind.HitRow, AttemptSeed));
        var toTheColumn = Assert.IsType<BoosterUse.HitColumn>(BoosterLessonAim.For(column, BoosterKind.HitColumn, AttemptSeed));

        Assert.Equal(4, toTheRow.At.Y);
        Assert.Equal(4, toTheColumn.At.X);
    }

    [Fact]
    public void Where_every_line_is_worth_as_much_a_brush_goes_for_the_one_nearest_the_middle_and_of_two_the_upper_or_the_left()
    {
        Game game = Start(Level(Playable(), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 10) }, colors: 6));

        Assert.Equal(new BoosterUse.HitRow(P(2, 2)), BoosterLessonAim.For(game, BoosterKind.HitRow, AttemptSeed));
        Assert.Equal(new BoosterUse.HitColumn(P(2, 2)), BoosterLessonAim.For(game, BoosterKind.HitColumn, AttemptSeed));
    }

    [Fact]
    public void The_shuffle_is_shown_as_it_is_and_a_start_booster_has_no_aim()
    {
        Game game = Start(Level(Playable()));

        Assert.IsType<BoosterUse.Shuffle>(BoosterLessonAim.For(game, BoosterKind.Shuffle, AttemptSeed));
        Assert.Throws<ArgumentException>(() => BoosterLessonAim.For(game, BoosterKind.StartColorBomb, AttemptSeed));
    }

    [Fact]
    public void Aiming_leaves_the_game_and_its_random_streams_as_they_were()
    {
        LevelDefinition level = Level(Playable().With(0, 4, 'p').With(2, 4, 'p'), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 10) }, colors: 6);
        Game asked = Start(level);
        Game untouched = Start(level);

        foreach (BoosterKind booster in BoosterKinds.InLevel)
        {
            BoosterLessonAim.For(asked, booster, AttemptSeed);
        }

        Assert.True(asked.TryUseBooster(new BoosterUse.HitRow(P(0, 4))));
        Assert.True(untouched.TryUseBooster(new BoosterUse.HitRow(P(0, 4))));
        Assert.Equal(TilesOf(untouched.Board), TilesOf(asked.Board));
        Assert.Equal(untouched.BoostersUsed, asked.BoostersUsed);
    }

    // ---------- what the board takes while a booster is used by hand ----------

    [Fact]
    public void While_a_brush_is_used_by_hand_the_board_takes_its_press_and_a_use_anywhere_on_its_row_and_nothing_else()
    {
        Game game = Start(Level(Playable().With(0, 4, 'p').With(2, 4, 'p').With(4, 4, 'p'), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 10) }, colors: 6));
        TutorialWatch watch = WatchOf(TutorialStep.BoosterHitRow);

        var lesson = Assert.IsType<Lesson.Booster>(watch.BeginWhatIsDue(game));
        Assert.Equal((TutorialStep.BoosterHitRow, BoosterKind.HitRow, 4), (lesson.Step, lesson.Kind, ((BoosterUse.HitRow)lesson.Use).At.Y));

        Assert.True(watch.TakesThePress(BoosterKind.HitRow));
        Assert.False(watch.TakesThePress(BoosterKind.HitCell));
        Assert.False(watch.Takes(Move.Swap(P(0, 0), P(1, 0))));
        Assert.True(watch.TakesTheUse(new BoosterUse.HitRow(P(5, 4))));
        Assert.False(watch.TakesTheUse(new BoosterUse.HitRow(P(5, 3))));
        Assert.False(watch.TakesTheUse(new BoosterUse.HitColumn(P(2, 4))));
        Assert.True(watch.IsFree(new BoosterUse.HitRow(P(1, 4))));
        Assert.Null(watch.Touched());
        Assert.Null(watch.Used(new BoosterUse.HitRow(P(1, 3))));
        Assert.Empty(watch.GuidedBoosters);

        Assert.True(game.TryUseBooster(new BoosterUse.HitRow(P(1, 4))));
        Assert.Equal(TutorialStep.BoosterHitRow, watch.Used(new BoosterUse.HitRow(P(1, 4))));
        Assert.Equal(new[] { "hit-row" }, watch.GuidedBoosters);
        Assert.Null(watch.Showing);
        Assert.False(watch.IsFree(new BoosterUse.HitRow(P(1, 4))));
        Assert.Equal(new[] { TutorialStep.BoosterHitRow }, watch.TakeWhatWasDone());
        Assert.Equal(0, watch.GuidedMoves);
    }

    [Fact]
    public void While_the_spatula_is_used_by_hand_only_its_cell_is_taken_and_while_nothing_shows_every_press_and_use_is()
    {
        Game game = Start(Level(Playable().With(4, 1, 'p'), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 10) }, colors: 6));
        TutorialWatch watch = WatchOf(TutorialStep.BoosterHitCell);
        TutorialWatch nothing = WatchOf();

        Assert.IsType<Lesson.Booster>(watch.BeginWhatIsDue(game));

        Assert.True(watch.TakesTheUse(new BoosterUse.HitCell(P(4, 1))));
        Assert.False(watch.TakesTheUse(new BoosterUse.HitCell(P(4, 2))));
        Assert.True(nothing.TakesThePress(BoosterKind.Shuffle));
        Assert.True(nothing.TakesTheUse(new BoosterUse.HitCell(P(4, 2))));
        Assert.False(nothing.IsFree(new BoosterUse.HitCell(P(4, 2))));
    }

    [Fact]
    public void A_lesson_for_a_booster_is_only_made_for_the_booster_its_step_teaches()
    {
        Assert.Throws<ArgumentException>(() => new Lesson.Booster(TutorialStep.BoosterHitRow, new BoosterUse.HitCell(P(0, 0))));
        Assert.Throws<ArgumentException>(() => new Lesson.Booster(TutorialStep.Match, new BoosterUse.Shuffle()));
    }

    // ---------- the use is free, and the record names it ----------

    [Fact]
    public void A_booster_used_by_hand_leaves_the_stock_as_it_was_and_one_used_otherwise_takes_one_from_it()
    {
        var ledger = new AttemptLedger(TheRules, 1000);
        PlayerProgress onSeven = ledger.WithTheOpeningGiftsDue(new PlayerProgress(levelsWon: 6, attemptsOnLevel: 0).WithWallet(TheRules.StartingWallet));

        PlayerProgress free = ledger.Apply(onSeven, new AttemptEvent.GuidedBoosterUsed(BoosterKind.HitCell), Evening);
        PlayerProgress paid = ledger.Apply(onSeven, new AttemptEvent.BoosterAccepted(BoosterKind.HitCell), Evening);

        Assert.Equal(3, onSeven.Wallet.Shelf.Count(BoosterKind.HitCell));
        Assert.Equal(onSeven, free);
        Assert.Equal(2, paid.Wallet.Shelf.Count(BoosterKind.HitCell));
        Assert.Throws<ArgumentException>(() => new AttemptEvent.GuidedBoosterUsed(BoosterKind.StartExtraMoves));
        Assert.False(new AttemptEvent.GuidedBoosterUsed(BoosterKind.Shuffle).EndsTheAttempt);
    }

    [Fact]
    public void A_record_names_the_booster_used_by_hand_after_its_guided_moves_both_writers_alike_and_reads_it_back()
    {
        AttemptRecord used = RecordWithBoosters("hit-cell", "hit-row");
        var guided = new AttemptRecord(used.Start, used.Outcome, lab: null, used.Actions, guidedMoves: 1, guidedBoosters: new[] { "hit-cell" });

        string line = AttemptLine.Of(guided);

        Assert.Equal(AttemptRecordJson.Serialize(guided), line);
        Assert.EndsWith(",\"guidedMoves\":1,\"guidedBoosters\":[\"hit-cell\"]}", line, StringComparison.Ordinal);
        Assert.Contains("\"boostersUsed\":[\"hit-cell\",\"hit-row\"]", line, StringComparison.Ordinal);
        Assert.Equal(new[] { "hit-cell" }, AttemptRecordJson.Parse(line).GuidedBoosters);
        Assert.DoesNotContain("guidedBoosters", AttemptLine.Of(used), StringComparison.Ordinal);
        Assert.Empty(AttemptRecordJson.Parse(AttemptLine.Of(used)).GuidedBoosters);
        Assert.Equal(1, AttemptRecord.SchemaVersion); // the field is optional and additive: schema 1 stays
        Assert.Equal(AttemptStanding.Assisted, AttemptCounting.Standing(guided));
    }

    [Fact]
    public void A_booster_named_as_used_by_hand_must_be_among_the_boosters_used()
    {
        AttemptRecord used = RecordWithBoosters("hit-cell");
        string line = AttemptLine.Of(new AttemptRecord(used.Start, used.Outcome, lab: null, guidedBoosters: new[] { "hit-cell" }));

        Assert.Throws<ArgumentException>(() => new AttemptRecord(used.Start, used.Outcome, lab: null, guidedBoosters: new[] { "shuffle" }));
        Assert.Throws<ArgumentException>(() => new AttemptRecord(used.Start, used.Outcome, lab: null, guidedBoosters: new[] { "hit-cell", "hit-cell" }));
        Assert.Throws<AttemptRecordFormatException>(() => AttemptRecordJson.Parse(line.Replace("\"guidedBoosters\":[\"hit-cell\"]", "\"guidedBoosters\":[\"hit-row\"]", StringComparison.Ordinal)));
    }

    // ---------- what the pills say ----------

    [Fact]
    public void The_pill_of_a_booster_used_by_hand_asks_for_its_disc_then_for_its_lit_target_says_what_it_does_and_that_it_is_free()
    {
        Assert.Equal(new PillWords.Lesson("Yeni: Spatula! Diske dokun", "Bir taşı ya da bir katı kaldırır", "Bu seferlik bedava"), TutorialWords.OfTheBooster(BoosterKind.HitCell, isArmed: false));
        Assert.Equal(
            new PillWords.Lesson("Şimdi parlayan taşa dokun", "Bir taşı ya da bir katı kaldırır", "Her bölüm booster'sız geçilebilir"),
            TutorialWords.OfTheBooster(BoosterKind.HitCell, isArmed: true));
        Assert.Equal(new PillWords.Lesson("Şimdi parlayan satıra dokun", "Bir satırı boyar", "Bu seferlik bedava"), TutorialWords.OfTheBooster(BoosterKind.HitRow, isArmed: true));
        Assert.Equal(new PillWords.Lesson("Yeni: Paleti karıştır! Diske dokun", "Taşları yeniden dizer", "Bu seferlik bedava"), TutorialWords.OfTheBooster(BoosterKind.Shuffle, isArmed: false));
        Assert.Throws<ArgumentException>(() => TutorialWords.OfTheBooster(BoosterKind.Shuffle, isArmed: true));
        Assert.Throws<ArgumentException>(() => TutorialWords.OfTheBooster(BoosterKind.StartColorBomb, isArmed: false));
    }

    [Fact]
    public void The_pill_of_a_start_booster_names_it_says_what_choosing_it_does_and_that_a_touch_goes_on()
    {
        Assert.Equal(
            new PillWords.Lesson("+3 hamle", "Seçersen bölüme 3 hamle fazlayla başlarsın", "Devam etmek için dokun"),
            TutorialWords.OfTheStartBooster(BoosterKind.StartExtraMoves, 3));
        Assert.Equal(
            new PillWords.Lesson("Fırça + sıçrama", "Seçersen bölüme fırça ve sıçramayla başlarsın", "Devam etmek için dokun"),
            TutorialWords.OfTheStartBooster(BoosterKind.StartRocketAndBomb, 3));
        Assert.Throws<ArgumentException>(() => TutorialWords.OfTheStartBooster(BoosterKind.HitRow, 3));
        Assert.Equal("Kapatmak için dokun", BoosterWords.TouchToClose);
    }

    // ---------- the card of a long press ----------

    [Theory]
    [MemberData(nameof(EveryBoosterKind))]
    public void The_card_of_every_booster_plays_its_effect_with_the_engine_on_a_tiny_board_that_is_the_same_every_time(BoosterKind booster)
    {
        BoosterShowcase showcase = BoosterShowcase.Of(booster);
        Game game = showcase.Start();

        Assert.Equal((BoosterShowcase.Cells, BoosterShowcase.Cells), (game.Board.Width, game.Board.Height));
        Assert.Equal(TilesOf(game.Board), TilesOf(showcase.Start().Board));

        int acts = 0;
        for (ShowcaseAct? act = showcase.NextAct(game, 0); act != null; act = showcase.NextAct(game, ++acts))
        {
            bool isDone = act switch
            {
                ShowcaseAct.UseTheBooster use => game.TryUseBooster(use.Use),
                ShowcaseAct.Play play => game.TryPlay(play.Move),
                ShowcaseAct.CountTheMovesUp count => count.From == game.Level.MoveLimit && game.MoveLimit == count.From + count.Moves,
                _ => throw new ArgumentOutOfRangeException(nameof(booster), act, "an act the card does not know"),
            };
            Assert.True(isDone, $"the card of {booster.Name()} asked for \"{act}\", which was not done");
            Assert.True(game.Status == GameStatus.Playing, $"the card of {booster.Name()} ended its game");
        }

        Assert.InRange(acts, 1, 2);
        Assert.Equal(new[] { booster.Name() }, game.BoostersUsed.ToArray());
    }

    [Fact]
    public void The_spatulas_card_takes_the_crate_away_and_the_start_boosters_cards_open_with_their_specials()
    {
        Game spatula = BoosterShowcase.Of(BoosterKind.HitCell).Start();
        Assert.Equal(TileKind.Box, spatula.Board.TileAt(P(2, 2)).Kind);
        Assert.True(spatula.TryUseBooster(Assert.IsType<ShowcaseAct.UseTheBooster>(BoosterShowcase.Of(BoosterKind.HitCell).NextAct(spatula, 0)).Use));
        Assert.DoesNotContain(TilesOf(spatula.Board), tile => tile.Kind == TileKind.Box);

        IReadOnlyList<Tile> rocketAndBomb = TilesOf(BoosterShowcase.Of(BoosterKind.StartRocketAndBomb).Start().Board);
        Assert.Contains(rocketAndBomb, tile => tile.Kind == TileKind.RocketH || tile.Kind == TileKind.RocketV);
        Assert.Contains(rocketAndBomb, tile => tile.Kind == TileKind.Bomb);
        Assert.Contains(TilesOf(BoosterShowcase.Of(BoosterKind.StartColorBomb).Start().Board), tile => tile.Kind == TileKind.ColorBomb);
        Assert.Equal(
            3,
            Assert.IsType<ShowcaseAct.CountTheMovesUp>(BoosterShowcase.Of(BoosterKind.StartExtraMoves).NextAct(BoosterShowcase.Of(BoosterKind.StartExtraMoves).Start(), 0)).Moves);
    }

    public static TheoryData<BoosterKind> EveryBoosterKind()
    {
        var kinds = new TheoryData<BoosterKind>();
        foreach (BoosterKind booster in BoosterKinds.All)
        {
            kinds.Add(booster);
        }

        return kinds;
    }

    /// <summary>The tests' filler with a move on it, three oranges in the upper left corner: a board needs one to start.</summary>
    private static string[] Playable() => Filler(6, 6).With(0, 0, 'o').With(1, 0, 'o').With(2, 1, 'o');

    private static IReadOnlyList<Tile> TilesOf(Board board) =>
        Enumerable.Range(0, board.Height).SelectMany(y => Enumerable.Range(0, board.Width).Select(x => board.TileAt(new Position(x, y)))).ToArray();

    private static AttemptRecord RecordWithBoosters(params string[] boosters)
    {
        AttemptRecord played = TestRecords.Of(GameTests.OpenLevel(), AttemptResult.Won, 12);
        AttemptOutcome outcome = played.Outcome;
        return new AttemptRecord(
            played.Start, new AttemptOutcome(outcome.Result, outcome.MovesUsed, outcome.Goals, outcome.ExtraMovesBought, boosters, outcome.EndedUtc), lab: null);
    }

    private static TutorialWatch WatchOf(params TutorialStep[] steps) =>
        new(steps, HintAdvisor.TryCreate(TutorialWatch.ShownBy, out HintAdvisor? advisor) ? advisor : throw new InvalidOperationException("no expert"), AttemptSeed);
}
