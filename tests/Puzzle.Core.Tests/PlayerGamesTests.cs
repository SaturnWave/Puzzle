using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Players;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Api;
using Puzzle.Web.Players;
using Puzzle.Web.Recording;
using Puzzle.Web.Sessions;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

/// <summary>
/// The public mode's progression: levels in order of their ids, one live attempt that is the player's until they go on,
/// attempts numbered and recorded by the server.
/// </summary>
public sealed class PlayerGamesTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly ManualClock _clock = new();
    private readonly ListLogger<PlayerGames> _logger = new();
    private readonly AttemptLog _log;
    private readonly AttemptRecorder _recorder;
    private readonly PlayerStore _store;

    public PlayerGamesTests()
    {
        _log = new AttemptLog(Path.Combine(_folder.Path, "recordings", "attempts.jsonl"));
        _recorder = new AttemptRecorder(_log, new ListLogger<AttemptRecorder>());
        _store = PlayerStore.Open(DataFolder);
    }

    private string DataFolder => Path.Combine(_folder.Path, "data");

    public void Dispose()
    {
        _store.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public void A_new_player_stands_before_level_1_with_no_live_attempt()
    {
        PlayerGames games = Games(PublicLevels.Easy(1), PublicLevels.Easy(2));
        DeviceId ayse = Register("Ayse");

        PlayerGameDto game = games.Current(ayse);

        Assert.Equal(new PlayerProgressDto(LevelNumber: 1, LevelCount: 2, AttemptsOnLevel: 0, Finished: false), game.Progress);
        Assert.Null(game.Attempt);
        Assert.IsType<MoveOutcome.NoLiveAttempt>(games.Play(ayse, new PlayerMove(Guid.NewGuid(), MovesSeen: 0, Move.Tap(TestBoards.P(0, 0)))));
    }

    [Fact]
    public async Task The_levels_come_in_the_order_of_their_ids_whatever_the_files_are_called()
    {
        // By file name the level with id 7 is first; by id it is the one with id 3.
        PlayerGames games = Games(PublicLevels.WriteNamed(NewLevelsRoot(), ("a_first", PublicLevels.Hopeless(7)), ("z_last", PublicLevels.Easy(3))));
        DeviceId ayse = Register("Ayse");

        PlayerGameDto first = Start(games, ayse);
        await PlayToTheEndAsync(games, ayse);
        PlayerGameDto second = Start(games, ayse);

        Assert.NotNull(first.Attempt);
        Assert.Equal((1, 30), (first.Attempt.LevelNumber, first.Attempt.State.MoveLimit)); // the easy level, id 3
        Assert.NotNull(second.Attempt);
        Assert.Equal((2, 2), (second.Attempt.LevelNumber, second.Attempt.State.MoveLimit)); // the hopeless one, id 7
    }

    [Fact]
    public async Task A_win_unlocks_the_next_level()
    {
        PlayerGames games = Games(PublicLevels.Easy(1), PublicLevels.Hopeless(2));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);

        PlayerMoveResultDto last = await PlayToTheEndAsync(games, ayse);

        Assert.Equal("Won", last.State.Status);
        Assert.Equal(new PlayerProgressDto(LevelNumber: 2, LevelCount: 2, AttemptsOnLevel: 0, Finished: false), last.Progress);
        Assert.Equal((1, new LevelId(1)), (StoredProgress(ayse).LevelsWon, StoredProgress(ayse).LastWonLevel));

        PlayerGameDto afterWin = games.Current(ayse); // a reload still shows the won attempt, on the level it was won on
        Assert.NotNull(afterWin.Attempt);
        Assert.Equal((1, "Won"), (afterWin.Attempt.LevelNumber, afterWin.Attempt.State.Status));

        PlayerGameDto next = Start(games, ayse);
        Assert.NotNull(next.Attempt);
        Assert.Equal((2, 1, "Playing", 2), (next.Attempt.LevelNumber, next.Attempt.AttemptNumber, next.Attempt.State.Status, next.Attempt.State.MoveLimit));
        Assert.Equal(0, next.Progress.AttemptsOnLevel); // opened, not played yet
    }

    [Fact]
    public async Task A_loss_keeps_the_level_and_the_retry_takes_the_levels_next_seed()
    {
        PlayerGames games = Games(PublicLevels.Hopeless(1, seedPool: new uint[] { 11, 12, 13 }), PublicLevels.Easy(2));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);

        PlayerMoveResultDto last = await PlayToTheEndAsync(games, ayse);
        PlayerGameDto retry = Start(games, ayse);

        Assert.Equal("LostOutOfMoves", last.State.Status);
        Assert.Equal(new PlayerProgressDto(LevelNumber: 1, LevelCount: 2, AttemptsOnLevel: 1, Finished: false), last.Progress);
        Assert.NotNull(retry.Attempt);
        Assert.Equal((1, 2, "Playing"), (retry.Attempt.LevelNumber, retry.Attempt.AttemptNumber, retry.Attempt.State.Status));
        Assert.Equal(1, retry.Progress.AttemptsOnLevel);
        Assert.Equal(0, StoredProgress(ayse).LevelsWon);

        await PlayToTheEndAsync(games, ayse);
        AttemptRecord[] records = _log.ReadAll().Records.ToArray();
        Assert.Equal(new int?[] { 0, 1 }, records.Select(record => record.Start.Attempt));
        Assert.Equal(new[] { AttemptSeeds.From(11), AttemptSeeds.From(12) }, records.Select(record => record.Start.Seeds));
        Assert.All(records, record => Assert.Equal(AttemptStanding.Comparable, AttemptCounting.Standing(record)));
    }

    [Fact]
    public async Task Attempt_numbers_advance_for_each_player_apart()
    {
        PlayerGames games = Games(PublicLevels.Hopeless(1, seedPool: new uint[] { 11, 12, 13 }));
        DeviceId ayse = Register("Ayse");
        DeviceId mert = Register("Mert");

        for (int attempt = 0; attempt < 2; attempt++)
        {
            Start(games, ayse);
            await PlayToTheEndAsync(games, ayse);
        }

        Start(games, mert);
        await PlayToTheEndAsync(games, mert);

        Assert.Equal(new int?[] { 0, 1 }, RecordsOf(ayse).Select(record => record.Start.Attempt));
        Assert.Equal(new int?[] { 0 }, RecordsOf(mert).Select(record => record.Start.Attempt));
        Assert.Equal(AttemptSeeds.From(11), RecordsOf(mert).Single().Start.Seeds); // his first try is his own, on the first seed
        Assert.Equal(3, Start(games, ayse).Attempt?.AttemptNumber);
        Assert.Equal(2, Start(games, mert).Attempt?.AttemptNumber);
    }

    [Fact]
    public async Task An_attempt_nobody_moved_in_uses_up_no_retry_number_and_is_dealt_again_after_a_restart()
    {
        LevelDefinition level = PublicLevels.Hopeless(1, seedPool: new uint[] { 11, 12, 13 });
        PlayerGames beforeRestart = Games(level);
        DeviceId ayse = Register("Ayse");
        Start(beforeRestart, ayse);
        await PlayToTheEndAsync(beforeRestart, ayse);
        PlayerGameDto lookedAt = Start(beforeRestart, ayse); // opened, never played

        PlayerGames afterRestart = Games(level);
        PlayerGameDto reloaded = afterRestart.Current(ayse);
        PlayerGameDto started = Start(afterRestart, ayse);
        await PlayToTheEndAsync(afterRestart, ayse);

        Assert.Equal(2, lookedAt.Attempt?.AttemptNumber);
        Assert.Equal(1, reloaded.Progress.AttemptsOnLevel); // only the attempt that was played counts
        AssertSameAttempt(lookedAt.Attempt, reloaded.Attempt);
        AssertSameAttempt(lookedAt.Attempt, started.Attempt);
        AttemptRecord[] records = _log.ReadAll().Records.ToArray();
        Assert.Equal(new int?[] { 0, 1 }, records.Select(record => record.Start.Attempt)); // the record's number is the player's retry count
        Assert.Equal(new[] { AttemptSeeds.From(11), AttemptSeeds.From(12) }, records.Select(record => record.Start.Seeds)); // no pool seed was skipped
    }

    [Fact]
    public void A_start_the_store_cannot_save_opens_nothing_and_uses_up_no_retry_number()
    {
        PlayerGames games = Games(PublicLevels.Easy(1, seedPool: new uint[] { 11, 12 }));
        DeviceId ayse = Register("Ayse");
        string obstacle = BlockTheStore();

        Exception failure = Assert.ThrowsAny<Exception>(() => Start(games, ayse));
        PlayerGameDto afterFailure = games.Current(ayse);
        Directory.Delete(obstacle);
        PlayerGameDto started = Start(games, ayse);

        Assert.True(failure is IOException or UnauthorizedAccessException, failure.GetType().Name);
        Assert.Null(afterFailure.Attempt);
        Assert.Equal((1, 0), (started.Attempt?.AttemptNumber, started.Progress.AttemptsOnLevel));
        Assert.True(games.TryGetLiveSession(ayse, out GameSession? live));
        Assert.Equal(AttemptSeeds.From(11), live.Seeds);
    }

    [Fact]
    public async Task Past_the_levels_seed_pool_an_attempt_is_recorded_as_a_replay_that_is_not_comparable()
    {
        PlayerGames games = Games(PublicLevels.Hopeless(1, seedPool: new uint[] { 11 }));
        DeviceId ayse = Register("Ayse");

        for (int attempt = 0; attempt < 2; attempt++)
        {
            Start(games, ayse);
            await PlayToTheEndAsync(games, ayse);
        }

        AttemptRecord[] records = _log.ReadAll().Records.ToArray();
        Assert.Equal(new int?[] { 0, null }, records.Select(record => record.Start.Attempt));
        Assert.Equal(new[] { AttemptSeeds.From(11), AttemptSeeds.From(11) }, records.Select(record => record.Start.Seeds));
        Assert.Equal(
            new[] { AttemptStanding.Comparable, AttemptStanding.ChosenSeeds },
            records.Select(AttemptCounting.Standing));
    }

    [Fact]
    public void A_second_start_that_names_the_first_attempt_abandons_it_and_records_it_when_moves_were_played()
    {
        PlayerGames games = Games(PublicLevels.Long(1, seedPool: new uint[] { 11, 12 }));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);
        PlayOneMove(games, ayse);
        _clock.Advance(TimeSpan.FromMinutes(2));

        PlayerGameDto second = Start(games, ayse, leaving: LiveAttemptId(games, ayse));

        AttemptRecord abandoned = Assert.Single(_log.ReadAll().Records);
        Assert.Equal((AttemptResult.Abandoned, 1, 0), (abandoned.Outcome.Result, abandoned.Outcome.MovesUsed, abandoned.Start.Attempt));
        Assert.Equal(Noon.AddMinutes(2), abandoned.Outcome.EndedUtc);
        Assert.NotNull(second.Attempt);
        Assert.Equal((2, 0, "Playing"), (second.Attempt.AttemptNumber, second.Attempt.State.MovesUsed, second.Attempt.State.Status));
        Assert.Equal(1, second.Progress.AttemptsOnLevel);
        Assert.True(games.TryGetLiveSession(ayse, out GameSession? live));
        Assert.Equal(AttemptSeeds.From(12), live.Seeds); // the one live attempt is the new one
    }

    [Fact]
    public void A_start_that_does_not_name_the_attempt_being_played_leaves_it_as_it_is()
    {
        PlayerGames games = Games(PublicLevels.Long(1, seedPool: new uint[] { 11, 12 }));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);
        PlayOneMove(games, ayse);
        PlayerGameDto beingPlayed = games.Current(ayse);

        PlayerGameDto fromAPageThatShowedNoAttempt = Start(games, ayse);
        PlayerGameDto fromAPageThatShowedAnother = Start(games, ayse, leaving: Guid.NewGuid());

        AssertSameAttempt(beingPlayed.Attempt, fromAPageThatShowedNoAttempt.Attempt);
        AssertSameAttempt(beingPlayed.Attempt, fromAPageThatShowedAnother.Attempt);
        Assert.Empty(_log.ReadAll().Records); // nothing was abandoned behind the player's back
    }

    [Fact]
    public void Starting_again_before_any_move_goes_on_with_the_same_attempt_and_uses_up_no_seed()
    {
        PlayerGames games = Games(PublicLevels.Easy(1, seedPool: new uint[] { 11, 12 }));
        DeviceId ayse = Register("Ayse");

        PlayerGameDto first = Start(games, ayse);
        PlayerGameDto again = Start(games, ayse);

        Assert.Equal(1, again.Attempt?.AttemptNumber);
        Assert.Equal(0, again.Progress.AttemptsOnLevel);
        AssertSameAttempt(first.Attempt, again.Attempt);
        Assert.True(games.TryGetLiveSession(ayse, out GameSession? live));
        Assert.Equal(AttemptSeeds.From(11), live.Seeds);
        Assert.Empty(_log.ReadAll().Records);
    }

    [Fact]
    public void A_reload_resumes_the_live_attempt_as_it_stands()
    {
        PlayerGames games = Games(PublicLevels.Long(1));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);
        PlayerMoveResultDto played = PlayOneMove(games, ayse);

        PlayerGameDto resumed = games.Current(ayse);

        Assert.NotNull(resumed.Attempt);
        Assert.Equal((1, 1, 1), (resumed.Attempt.LevelNumber, resumed.Attempt.AttemptNumber, resumed.Attempt.State.MovesUsed));
        Assert.Equal(played.State.Tiles, resumed.Attempt.State.Tiles);
        Assert.Equal(1, resumed.Progress.AttemptsOnLevel); // its first move made it count
        Assert.Empty(_log.ReadAll().Records); // looking is not leaving
    }

    [Fact]
    public async Task A_move_after_the_attempt_ended_is_refused_and_a_refused_move_costs_nothing()
    {
        PlayerGames games = Games(PublicLevels.Hopeless(1));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);
        Move tapOnAPlainGem = Move.Tap(TestBoards.P(0, 0)); // nothing a plain gem does when tapped

        MoveOutcome.Played refused = Assert.IsType<MoveOutcome.Played>(games.Play(ayse, PublicLevels.OnTheLiveAttempt(games, ayse, tapOnAPlainGem)));
        await PlayToTheEndAsync(games, ayse);

        Assert.False(refused.Result.Accepted);
        Assert.Empty(refused.Result.Events);
        Assert.Equal(0, refused.Result.State.MovesUsed);
        Assert.Equal(0, refused.Result.Progress.AttemptsOnLevel); // a move the engine refused starts no attempt
        Assert.IsType<MoveOutcome.AttemptOver>(games.Play(ayse, PublicLevels.OnTheLiveAttempt(games, ayse, tapOnAPlainGem)));
    }

    [Fact]
    public void A_move_made_on_an_earlier_board_or_on_another_attempt_is_refused_and_nothing_is_played()
    {
        PlayerGames games = Games(PublicLevels.Long(1, seedPool: new uint[] { 11, 12 }));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);
        PlayerMove asASecondTabSawIt = PublicLevels.OnTheLiveAttempt(games, ayse, PublicLevels.FirstLegalMove(games, ayse));
        PlayOneMove(games, ayse); // the first tab plays on: the second tab's board is one move old

        MoveOutcome onAnEarlierBoard = games.Play(ayse, asASecondTabSawIt with { Move = PublicLevels.FirstLegalMove(games, ayse) });
        int movesAfterTheStaleMove = MovesUsed(games, ayse);
        Start(games, ayse, leaving: LiveAttemptId(games, ayse)); // the first tab goes on to the next attempt: the second tab now shows an attempt that is gone
        MoveOutcome onAnotherAttempt = games.Play(ayse, asASecondTabSawIt with { Move = PublicLevels.FirstLegalMove(games, ayse) });

        Assert.IsType<MoveOutcome.AttemptChanged>(onAnEarlierBoard);
        Assert.Equal(1, movesAfterTheStaleMove);
        Assert.IsType<MoveOutcome.AttemptChanged>(onAnotherAttempt);
        Assert.Equal(0, MovesUsed(games, ayse)); // legal on the new board as it was, and still not played
        Assert.Equal(1, Assert.Single(_log.ReadAll().Records).Outcome.MovesUsed); // the abandoned attempt holds the one move its player saw
    }

    [Fact]
    public async Task With_every_level_won_there_is_nothing_left_to_start()
    {
        PlayerGames games = Games(PublicLevels.Easy(1));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);

        PlayerMoveResultDto last = await PlayToTheEndAsync(games, ayse);
        PlayerGameDto afterAll = Start(games, ayse);

        var finished = new PlayerProgressDto(LevelNumber: 1, LevelCount: 1, AttemptsOnLevel: 0, Finished: true);
        Assert.Equal(finished, last.Progress);
        Assert.Equal(finished, afterAll.Progress);
        Assert.Null(afterAll.Attempt);
        Assert.Null(StoredProgress(ayse).Live);
        Assert.Single(_log.ReadAll().Records);
    }

    [Fact]
    public async Task The_record_carries_the_player_id_never_the_username_and_replays_exactly()
    {
        LevelDefinition level = PublicLevels.Easy(1, seedPool: new uint[] { 11, 12 });
        PlayerGames games = Games(level);
        DeviceId zeynep = Register("Zeynep_K");
        Start(games, zeynep);

        await PlayToTheEndAsync(games, zeynep);

        AttemptRecord record = Assert.Single(_log.ReadAll().Records);
        Assert.Equal(zeynep, record.Start.Device);
        Assert.Equal("web-public", record.Start.Client);
        Assert.Equal((AttemptResult.Won, 0), (record.Outcome.Result, record.Start.Attempt));
        Assert.DoesNotContain("zeynep", File.ReadAllText(_log.Path), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(record.Lab);
        Assert.Equal(($"{PublicLevels.Set}/level_0001", LevelJson.Fingerprint(level)), (record.Lab.LevelKey, record.Lab.LevelFingerprint));
        Assert.False(record.Lab.IsAssisted);
        Assert.Equal(AttemptStanding.Comparable, AttemptCounting.Standing(record));
        AssertReplays(record, level);
    }

    [Fact]
    public void An_attempt_left_for_days_is_still_the_players_as_they_left_it()
    {
        PlayerGames games = Games(PublicLevels.Long(1, seedPool: new uint[] { 11, 12 }));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);
        PlayOneMove(games, ayse);
        PlayerGameDto left = games.Current(ayse);

        _clock.Advance(TimeSpan.FromDays(3)); // far past the idle limit that puts a lab session away
        PlayerGameDto back = games.Current(ayse);
        PlayerMoveResultDto next = PlayOneMove(games, ayse);

        AssertSameAttempt(left.Attempt, back.Attempt);
        Assert.Equal((true, 2), (next.Accepted, next.State.MovesUsed));
        Assert.Empty(_log.ReadAll().Records); // a pause, however long, is not an abandoned attempt
    }

    [Fact]
    public async Task A_live_attempt_is_rebuilt_after_a_restart_move_for_move_and_goes_on_to_its_one_record()
    {
        LevelDefinition level = PublicLevels.Long(1); // no seed pool: only the saved seeds can bring this board back
        PlayerGames beforeRestart = Games(level);
        DeviceId ayse = Register("Ayse");
        Start(beforeRestart, ayse);
        for (int move = 0; move < 4; move++)
        {
            PlayOneMove(beforeRestart, ayse);
        }

        PlayerGameDto left = beforeRestart.Current(ayse);
        _clock.Advance(TimeSpan.FromHours(5));

        PlayerGames afterRestart = Games(level); // a new server over the same store
        PlayerGameDto rebuilt = afterRestart.Current(ayse);
        PlayerMoveResultDto last = await PlayToTheEndAsync(afterRestart, ayse);

        Assert.Equal(4, left.Attempt?.State.MovesUsed);
        AssertSameAttempt(left.Attempt, rebuilt.Attempt); // the same attempt, and its board cell for cell
        Assert.Equal(left.Progress, rebuilt.Progress);
        AttemptRecord record = Assert.Single(_log.ReadAll().Records); // the restart recorded nothing as abandoned
        Assert.Equal((AttemptResult.OutOfMoves, last.State.MovesUsed, 0), (record.Outcome.Result, record.Outcome.MovesUsed, record.Start.Attempt));
        Assert.Equal(Noon, record.Start.StartedUtc); // the attempt began before the restart
        Assert.True(record.Outcome.EndedUtc >= Noon.AddHours(5));
        AssertReplays(record, level); // every move is in it, those before the restart too
    }

    [Fact]
    public async Task A_decided_attempt_is_still_shown_after_a_restart_and_going_on_records_nothing_more()
    {
        LevelDefinition[] levels = { PublicLevels.Easy(1), PublicLevels.Hopeless(2) };
        PlayerGames beforeRestart = Games(levels);
        DeviceId ayse = Register("Ayse");
        Start(beforeRestart, ayse);
        await PlayToTheEndAsync(beforeRestart, ayse);
        PlayerGameDto left = beforeRestart.Current(ayse);

        PlayerGames afterRestart = Games(levels);
        PlayerGameDto rebuilt = afterRestart.Current(ayse);
        PlayerGameDto next = Start(afterRestart, ayse);

        Assert.Equal("Won", rebuilt.Attempt?.State.Status);
        AssertSameAttempt(left.Attempt, rebuilt.Attempt);
        Assert.Equal(2, rebuilt.Progress.LevelNumber);
        Assert.Equal((2, 1, "Playing"), (next.Attempt?.LevelNumber, next.Attempt?.AttemptNumber, next.Attempt?.State.Status));
        Assert.Equal(AttemptResult.Won, Assert.Single(_log.ReadAll().Records).Outcome.Result);
    }

    [Fact]
    public void A_saved_attempt_on_a_level_that_changed_since_is_dropped_and_the_level_starts_afresh()
    {
        PlayerGames beforeRestart = Games(PublicLevels.Long(1));
        DeviceId ayse = Register("Ayse");
        Start(beforeRestart, ayse);
        PlayOneMove(beforeRestart, ayse);

        PlayerGames afterRestart = Games(PublicLevels.Hopeless(1)); // the same id, other content: the saved moves are not this level's
        PlayerGameDto reloaded = afterRestart.Current(ayse);
        PlayerGameDto started = Start(afterRestart, ayse);

        Assert.Null(reloaded.Attempt);
        Assert.Equal(0, reloaded.Progress.AttemptsOnLevel); // attempts are counted per version of a level
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal((1, 1, 2), (started.Attempt?.LevelNumber, started.Attempt?.AttemptNumber, started.Attempt?.State.MoveLimit));
        Assert.Empty(_log.ReadAll().Records);
    }

    [Fact]
    public async Task A_players_place_is_the_level_after_the_one_they_won_last_whatever_joins_the_set()
    {
        PlayerGames beforeRestart = Games(PublicLevels.Easy(2), PublicLevels.Hopeless(5));
        DeviceId ayse = Register("Ayse");
        Start(beforeRestart, ayse);
        await PlayToTheEndAsync(beforeRestart, ayse); // level id 2 is won

        // A level joins the set before the one the player won: counted by position, the player would be sent back to id 2.
        PlayerGames afterRestart = Games(PublicLevels.Easy(1), PublicLevels.Easy(2), PublicLevels.Hopeless(5));
        PlayerGameDto reloaded = afterRestart.Current(ayse);
        PlayerGameDto next = Start(afterRestart, ayse);

        Assert.Equal(new PlayerProgressDto(LevelNumber: 3, LevelCount: 3, AttemptsOnLevel: 0, Finished: false), reloaded.Progress);
        Assert.Equal((3, 2), (next.Attempt?.LevelNumber, next.Attempt?.State.MoveLimit)); // the hopeless level, id 5
    }

    [Fact]
    public void Progress_from_before_places_were_kept_by_id_stands_where_its_count_of_wins_leads()
    {
        PlayerGames games = Games(PublicLevels.Easy(1), PublicLevels.Hopeless(2), PublicLevels.Easy(3));
        DeviceId ayse = Register("Ayse");
        _store.UpdateProgress(ayse, _ => new PlayerProgress(levelsWon: 1, attemptedLevel: null, attemptsStarted: 0));

        PlayerGameDto game = Start(games, ayse);

        Assert.Equal((2, 2), (game.Attempt?.LevelNumber, game.Attempt?.State.MoveLimit));
    }

    [Fact]
    public async Task A_win_the_store_could_not_save_is_kept_and_going_on_waits_until_it_is_saved()
    {
        PlayerGames games = Games(PublicLevels.Easy(1), PublicLevels.Easy(2));
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);
        string obstacle = BlockTheStore();

        PlayerMoveResultDto last = await PlayToTheEndAsync(games, ayse);

        Assert.Equal("Won", last.State.Status); // the store never fails a move
        Assert.Equal(2, last.Progress.LevelNumber); // and the player is told the truth, not what the store still holds
        Assert.Equal(0, StoredProgress(ayse).LevelsWon);
        Assert.All(_logger.Entries, entry => Assert.True(entry is { Level: LogLevel.Error, Exception: IOException or UnauthorizedAccessException }));
        Assert.NotEmpty(_logger.Entries);
        Assert.ThrowsAny<Exception>(() => Start(games, ayse)); // going on needs the win saved first
        Assert.Equal(1, games.SaveUnsavedProgress()); // still unsaved, still kept

        Directory.Delete(obstacle);
        PlayerGameDto next = Start(games, ayse);

        Assert.Equal(2, next.Attempt?.LevelNumber);
        Assert.Equal(1, StoredProgress(ayse).LevelsWon);
    }

    [Fact]
    public async Task What_the_store_could_not_save_is_saved_by_the_next_try_without_the_player()
    {
        LevelDefinition[] levels = { PublicLevels.Easy(1), PublicLevels.Easy(2) };
        PlayerGames games = Games(levels);
        DeviceId ayse = Register("Ayse");
        Start(games, ayse);
        string obstacle = BlockTheStore();
        PlayerMoveResultDto last = await PlayToTheEndAsync(games, ayse);
        Directory.Delete(obstacle);

        int stillUnsaved = games.SaveUnsavedProgress(); // what the server does once a minute, and when it stops

        Assert.Equal(0, stillUnsaved);
        PlayerProgress stored = StoredProgress(ayse);
        Assert.Equal((1, new LevelId(1)), (stored.LevelsWon, stored.LastWonLevel));
        Assert.Equal(last.State.MovesUsed, stored.Live?.Moves.Count); // every move of the attempt, none lost

        // So a restart after it finds the win and the won attempt, although the player never came back in between.
        PlayerGameDto afterRestart = Games(levels).Current(ayse);
        Assert.Equal((2, "Won"), (afterRestart.Progress.LevelNumber, afterRestart.Attempt?.State.Status));
    }

    [Fact]
    public void Leaving_attempt_after_attempt_uses_up_the_budget_of_new_attempts_and_the_one_being_played_stays()
    {
        PlayerGames games = Games(PublicLevels.Long(1));
        DeviceId ayse = Register("Ayse");
        PlayAMoveInEachOfTheNextAttempts(games, ayse, AttemptBudget.Attempts);
        PlayerGameDto beingPlayed = games.Current(ayse);

        StartOutcome oneTooMany = games.Start(ayse, leaving: LiveAttemptId(games, ayse));

        Assert.Equal(AttemptBudget.Window, Assert.IsType<StartOutcome.TooManyAttempts>(oneTooMany).RetryAfter);
        AssertSameAttempt(beingPlayed.Attempt, games.Current(ayse).Attempt);
        Assert.Equal((true, 2), (PlayOneMove(games, ayse).Accepted, MovesUsed(games, ayse))); // and it plays on
        AttemptRecord[] records = _log.ReadAll().Records.ToArray(); // one for each attempt left, none for the one that stayed
        Assert.Equal(AttemptBudget.Attempts - 1, records.Length);
        Assert.All(records, record => Assert.Equal(AttemptResult.Abandoned, record.Outcome.Result));
    }

    [Fact]
    public async Task Attempts_played_to_their_end_count_against_the_same_budget()
    {
        PlayerGames games = Games(PublicLevels.Hopeless(1));
        DeviceId ayse = Register("Ayse");
        for (int attempt = 0; attempt < AttemptBudget.Attempts; attempt++)
        {
            Start(games, ayse);
            await PlayToTheEndAsync(games, ayse);
        }

        StartOutcome oneTooMany = games.Start(ayse);

        Assert.IsType<StartOutcome.TooManyAttempts>(oneTooMany);
        Assert.Equal("LostOutOfMoves", games.Current(ayse).Attempt?.State.Status); // the lost attempt is still on the page, its retry waits
        Assert.Equal(AttemptBudget.Attempts, _log.ReadAll().Records.Count);
    }

    [Fact]
    public void The_budget_gives_an_attempt_back_when_the_oldest_one_was_opened_ten_minutes_ago()
    {
        PlayerGames games = Games(PublicLevels.Long(1));
        DeviceId ayse = Register("Ayse");
        PlayAMoveInEachOfTheNextAttempts(games, ayse, count: 1);
        _clock.Advance(TimeSpan.FromMinutes(1));
        PlayAMoveInEachOfTheNextAttempts(games, ayse, AttemptBudget.Attempts - 1);

        StartOutcome atOnce = games.Start(ayse, leaving: LiveAttemptId(games, ayse));
        _clock.Advance(TimeSpan.FromMinutes(9) - TimeSpan.FromSeconds(1));
        StartOutcome aSecondEarly = games.Start(ayse, leaving: LiveAttemptId(games, ayse));
        _clock.Advance(TimeSpan.FromSeconds(1));
        StartOutcome onTime = games.Start(ayse, leaving: LiveAttemptId(games, ayse));
        PlayOneMove(games, ayse);
        StartOutcome rightAfter = games.Start(ayse, leaving: LiveAttemptId(games, ayse));

        Assert.Equal(TimeSpan.FromMinutes(9), Assert.IsType<StartOutcome.TooManyAttempts>(atOnce).RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.IsType<StartOutcome.TooManyAttempts>(aSecondEarly).RetryAfter);
        Assert.IsType<StartOutcome.Started>(onTime);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.IsType<StartOutcome.TooManyAttempts>(rightAfter).RetryAfter); // only the oldest had left the window
    }

    [Fact]
    public void A_start_that_opens_no_new_attempt_uses_up_nothing_of_the_budget()
    {
        PlayerGames games = Games(PublicLevels.Long(1));
        DeviceId ayse = Register("Ayse");
        PlayerGameDto opened = Start(games, ayse);

        for (int again = 0; again < AttemptBudget.Attempts; again++)
        {
            AssertSameAttempt(opened.Attempt, Start(games, ayse).Attempt); // nobody moved in it: the same game is dealt
        }

        PlayOneMove(games, ayse);
        PlayerGameDto beingPlayed = games.Current(ayse);
        for (int again = 0; again < AttemptBudget.Attempts; again++)
        {
            AssertSameAttempt(beingPlayed.Attempt, Start(games, ayse).Attempt); // not named: it goes on
        }

        // Only the one attempt that was opened counts: the rest of the budget is still there, and no more than that.
        PlayAMoveInEachOfTheNextAttempts(games, ayse, AttemptBudget.Attempts - 1);
        Assert.IsType<StartOutcome.TooManyAttempts>(games.Start(ayse, leaving: LiveAttemptId(games, ayse)));
    }

    [Fact]
    public void Each_player_has_a_budget_of_their_own()
    {
        PlayerGames games = Games(PublicLevels.Long(1));
        DeviceId ayse = Register("Ayse");
        DeviceId mert = Register("Mert");
        PlayAMoveInEachOfTheNextAttempts(games, ayse, AttemptBudget.Attempts);

        StartOutcome forAyse = games.Start(ayse, leaving: LiveAttemptId(games, ayse));
        StartOutcome forMert = games.Start(mert);

        Assert.IsType<StartOutcome.TooManyAttempts>(forAyse);
        Assert.IsType<StartOutcome.Started>(forMert);
    }

    [Fact]
    public void A_start_the_store_could_not_save_uses_up_nothing_of_the_budget()
    {
        PlayerGames games = Games(PublicLevels.Long(1));
        DeviceId ayse = Register("Ayse");
        string obstacle = BlockTheStore();
        for (int failed = 0; failed < AttemptBudget.Attempts; failed++)
        {
            Assert.ThrowsAny<Exception>(() => games.Start(ayse));
        }

        Directory.Delete(obstacle);
        PlayAMoveInEachOfTheNextAttempts(games, ayse, AttemptBudget.Attempts);

        Assert.Equal(AttemptBudget.Attempts, games.Current(ayse).Progress.AttemptsOnLevel);
    }

    [Fact]
    public void A_level_file_that_does_not_load_makes_the_public_set_unplayable_and_the_message_names_the_file()
    {
        string root = NewLevelsRoot();
        PublicLevels.Write(root, PublicLevels.Easy(1), PublicLevels.Easy(3));
        File.WriteAllText(Path.Combine(root, PublicLevels.Set, "level_0002.json"), "{ \"id\": 2, cut short");

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => new PublicLevelSet(LevelLibrary.Load(root), PublicLevels.Set));

        Assert.Contains("level_0002.json", refused.Message);
        Assert.Contains("Puzzle:PublicLevels", refused.Message);
    }

    [Fact]
    public void A_broken_file_outside_the_public_set_does_not_stop_it()
    {
        string root = NewLevelsRoot();
        PublicLevels.Write(root, PublicLevels.Easy(1));
        Directory.CreateDirectory(Path.Combine(root, "drafts"));
        File.WriteAllText(Path.Combine(root, "drafts", "broken.json"), "not a level");

        var set = new PublicLevelSet(LevelLibrary.Load(root), PublicLevels.Set);

        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Two_levels_with_one_id_make_the_public_set_unplayable_and_the_message_names_both()
    {
        LevelLibrary library = PublicLevels.WriteNamed(NewLevelsRoot(), ("one", PublicLevels.Easy(4)), ("another", PublicLevels.Hopeless(4)));

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => new PublicLevelSet(library, PublicLevels.Set));

        Assert.Contains($"{PublicLevels.Set}/one", refused.Message);
        Assert.Contains($"{PublicLevels.Set}/another", refused.Message);
    }

    /// <summary>Games over a level set of its own, on this test's store and log: a second call is a server restarted.</summary>
    private PlayerGames Games(params LevelDefinition[] levels) => Games(PublicLevels.Write(NewLevelsRoot(), levels));

    private PlayerGames Games(LevelLibrary library) =>
        new(new PublicLevelSet(library, PublicLevels.Set), _store, _recorder, _clock, _logger);

    private string NewLevelsRoot() => Path.Combine(_folder.Path, $"levels-{Guid.NewGuid():N}");

    /// <summary>Puts a folder where the store's temporary file must go, so no write to the store can succeed; returns it for the test to remove.</summary>
    private string BlockTheStore() => Directory.CreateDirectory(_store.Path + ".tmp").FullName;

    private DeviceId Register(string username)
    {
        var account = new PlayerAccount(
            new Username(username),
            PlayerAccount.NewPlayerId(),
            "pbkdf2-sha256$1000$c2FsdA==$aGFzaA==",
            SignInStamp.New(),
            Noon,
            PlayerProgress.None);
        Assert.True(_store.TryAdd(account));
        return account.PlayerId;
    }

    private PlayerProgress StoredProgress(DeviceId playerId) =>
        PlayerStore.ReadAccounts(DataFolder).Single(account => account.PlayerId == playerId).Progress;

    private IEnumerable<AttemptRecord> RecordsOf(DeviceId playerId) =>
        _log.ReadAll().Records.Where(record => record.Start.Device == playerId);

    private static Guid LiveAttemptId(PlayerGames games, DeviceId playerId)
    {
        PlayerAttemptDto? attempt = games.Current(playerId).Attempt;
        Assert.NotNull(attempt);
        return PublicLevels.IdOf(attempt);
    }

    private static int MovesUsed(PlayerGames games, DeviceId playerId)
    {
        PlayerAttemptDto? attempt = games.Current(playerId).Attempt;
        Assert.NotNull(attempt);
        return attempt.State.MovesUsed;
    }

    /// <summary>The player's game after a start the server took, as it takes every start at the pace people play at.</summary>
    private static PlayerGameDto Start(PlayerGames games, DeviceId playerId, Guid? leaving = null) =>
        Assert.IsType<StartOutcome.Started>(games.Start(playerId, leaving)).Game;

    /// <summary>
    /// Opens the player's next <paramref name="count"/> attempts one after the other, each in place of the one before
    /// it, and plays a move in each; the last one is left being played.
    /// </summary>
    private static void PlayAMoveInEachOfTheNextAttempts(PlayerGames games, DeviceId playerId, int count)
    {
        for (int opened = 0; opened < count; opened++)
        {
            Guid? leaving = games.Current(playerId).Attempt is { } live ? PublicLevels.IdOf(live) : null;
            Start(games, playerId, leaving);
            PlayOneMove(games, playerId);
        }
    }

    private static PlayerMoveResultDto PlayOneMove(PlayerGames games, DeviceId playerId)
    {
        PlayerMove move = PublicLevels.OnTheLiveAttempt(games, playerId, PublicLevels.FirstLegalMove(games, playerId));
        return Assert.IsType<MoveOutcome.Played>(games.Play(playerId, move)).Result;
    }

    /// <summary>Plays the live attempt to its end and returns the answer to the move that decided it.</summary>
    private static async Task<PlayerMoveResultDto> PlayToTheEndAsync(PlayerGames games, DeviceId playerId)
    {
        PlayerMoveResultDto? last = null;
        await PublicLevels.PlayToTheEndAsync(games, playerId, move =>
        {
            last = Assert.IsType<MoveOutcome.Played>(games.Play(playerId, move)).Result;
            Assert.True(last.Accepted);
            return Task.CompletedTask;
        });
        return last ?? throw new InvalidOperationException("The attempt was over before a move was played.");
    }

    /// <summary>The same attempt, under the same id, with the same board: every cell, the goals, the moves used and the status.</summary>
    private static void AssertSameAttempt(PlayerAttemptDto? expected, PlayerAttemptDto? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal((expected.Id, expected.LevelNumber, expected.AttemptNumber), (actual.Id, actual.LevelNumber, actual.AttemptNumber));
        Assert.Equal(JsonSerializer.Serialize(expected.State), JsonSerializer.Serialize(actual.State));
    }

    /// <summary>The record's level, seeds and moves, played again, end as the record says the attempt ended.</summary>
    private static void AssertReplays(AttemptRecord record, LevelDefinition level)
    {
        Assert.NotNull(record.Lab);
        Game replay = Game.Start(level, record.Start.Seeds);
        Assert.All(record.Lab.Moves, move => Assert.True(replay.TryPlay(move)));
        Assert.Equal(record.Outcome.MovesUsed, replay.MovesUsed);
        Assert.Equal(record.Outcome.Result == AttemptResult.Won, replay.Status == GameStatus.Won);
        Assert.True(replay.Status.IsOver());
    }
}
