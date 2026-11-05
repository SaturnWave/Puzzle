using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Recording;
using Puzzle.Web.Sessions;
using static Puzzle.Core.Tests.TestBoards;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

public sealed class AttemptRecordingTests : IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private readonly TempFolder _folder = new();
    private readonly ManualClock _clock = new();
    private readonly MemoryCache _cache;
    private readonly ListLogger<AttemptRecorder> _logger = new();

    public AttemptRecordingTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10, Clock = _clock });
    }

    public void Dispose()
    {
        _cache.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public void Concurrent_appends_each_land_as_one_whole_line()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "recordings", "attempts.jsonl"));
        AttemptRecord[] records = Enumerable.Range(0, 200)
            .Select(attempt => Of(GameTests.OpenLevel(), AttemptResult.Won, 18, attempt))
            .ToArray();

        Parallel.ForEach(records, new ParallelOptions { MaxDegreeOfParallelism = 8 }, log.Append);

        AttemptLogContents contents = log.ReadAll();
        Assert.Empty(contents.Problems);
        Assert.Equal(200, File.ReadAllLines(log.Path).Length);
        Assert.Equal(Enumerable.Range(0, 200), contents.Records.Select(record => record.Start.Attempt ?? -1).Order());
    }

    [Fact]
    public void A_broken_line_is_reported_by_number_and_the_rest_still_read()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "attempts.jsonl"));
        log.Append(Of(GameTests.OpenLevel(), AttemptResult.Won, 18));
        File.AppendAllText(log.Path, "{\"schema\":1,\"level\":\n");
        log.Append(Of(GameTests.OpenLevel(), AttemptResult.OutOfMoves, 30));

        AttemptLogContents contents = log.ReadAll();

        Assert.Equal(2, contents.Records.Count);
        Assert.Equal(2, Assert.Single(contents.Problems).Line);
    }

    [Fact]
    public void A_final_line_without_a_line_break_is_still_being_written_and_is_not_read_yet()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "attempts.jsonl"));
        log.Append(Of(GameTests.OpenLevel(), AttemptResult.Won, 18));
        string next = AttemptRecordJson.Serialize(Of(GameTests.OpenLevel(), AttemptResult.OutOfMoves, 30));

        File.AppendAllText(log.Path, next[..40]);
        AttemptLogContents halfway = log.ReadAll();
        File.AppendAllText(log.Path, next[40..] + "\n");
        AttemptLogContents done = log.ReadAll();

        Assert.Single(halfway.Records);
        Assert.Empty(halfway.Problems);
        Assert.Equal(2, done.Records.Count);
        Assert.Empty(done.Problems);
    }

    [Fact]
    public void An_append_after_a_line_cut_short_starts_a_line_of_its_own_and_both_are_accounted_for()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "attempts.jsonl"));
        log.Append(Of(GameTests.OpenLevel(), AttemptResult.Won, 18, attempt: 0));
        string cut = AttemptRecordJson.Serialize(Of(GameTests.OpenLevel(), AttemptResult.OutOfMoves, 30, attempt: 1));
        File.AppendAllText(log.Path, cut[..40]); // the writer died mid-line

        log.Append(Of(GameTests.OpenLevel(), AttemptResult.Won, 20, attempt: 2));

        AttemptLogContents contents = log.ReadAll();
        Assert.Equal(new int?[] { 0, 2 }, contents.Records.Select(record => record.Start.Attempt));
        Assert.Equal(2, Assert.Single(contents.Problems).Line);
    }

    [Fact]
    public void A_record_left_without_its_final_line_break_is_kept_by_the_next_append()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "attempts.jsonl"));
        string handEdited = AttemptRecordJson.Serialize(Of(GameTests.OpenLevel(), AttemptResult.Won, 18, attempt: 0));
        File.WriteAllText(log.Path, handEdited); // an editor saved it without a final newline

        log.Append(Of(GameTests.OpenLevel(), AttemptResult.Won, 20, attempt: 1));

        AttemptLogContents contents = log.ReadAll();
        Assert.Empty(contents.Problems);
        Assert.Equal(new int?[] { 0, 1 }, contents.Records.Select(record => record.Start.Attempt));
    }

    [Fact]
    public void The_log_can_be_read_while_a_writer_holds_it_open()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "attempts.jsonl"));
        log.Append(Of(GameTests.OpenLevel(), AttemptResult.Won, 18));

        using var writer = new FileStream(log.Path, FileMode.Append, FileAccess.Write, FileShare.Read);

        Assert.Single(log.ReadAll().Records);
    }

    [Fact]
    public void An_append_waits_out_a_brief_lock_by_another_process()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "attempts.jsonl"));
        log.Append(Of(GameTests.OpenLevel(), AttemptResult.Won, 18));
        var locked = new FileStream(log.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var release = new Thread(() =>
        {
            Thread.Sleep(30);
            locked.Dispose();
        });

        release.Start();
        log.Append(Of(GameTests.OpenLevel(), AttemptResult.OutOfMoves, 30));
        release.Join();

        Assert.Equal(2, log.ReadAll().Records.Count);
    }

    [Fact]
    public void A_log_never_written_holds_no_records()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "recordings", "attempts.jsonl"));

        AttemptLogContents contents = log.ReadAll();

        Assert.Empty(contents.Records);
        Assert.Empty(contents.Problems);
    }

    [Fact]
    public void Recordings_default_to_the_repository_root_wherever_the_lab_starts()
    {
        Directory.CreateDirectory(Path.Combine(_folder.Path, "levels", "generated"));
        string project = Directory.CreateDirectory(Path.Combine(_folder.Path, "src", "Puzzle.Web")).FullName;
        string expected = Path.Combine(_folder.Path, "recordings", "attempts.jsonl");

        Assert.Equal(expected, AttemptLog.FindDefaultPath(project));
        Assert.Equal(expected, AttemptLog.FindDefaultPath(_folder.Path));
    }

    [Fact]
    public void A_decided_lab_attempt_is_recorded_once_and_replays_exactly()
    {
        LevelDefinition level = EasyLevel();
        LabParts lab = StartLab("attempts.jsonl");
        GameSession session = lab.Store.Create(Library(level), AttemptSeeds.From(5), NextRetry(level), 0, Phone);

        PlayToTheEnd(session, lab.Recorder);
        lab.Store.Leave(session.Id); // the player starts the next attempt after the win

        AttemptRecord record = Assert.Single(lab.Log.ReadAll().Records);
        Assert.Equal(AttemptResult.Won, record.Outcome.Result);
        Assert.Equal(0, record.Start.Attempt);
        Assert.Equal(AttemptRecorder.Client, record.Start.Client);
        Assert.NotNull(record.Lab);
        Assert.Equal(LevelJson.Fingerprint(level), record.Lab.LevelFingerprint);
        Assert.False(record.Lab.IsAssisted);
        Assert.Equal(record.Outcome.MovesUsed, record.Lab.Moves.Count);

        Game replay = Game.Start(level, record.Start.Seeds);
        Assert.All(record.Lab.Moves, move => Assert.True(replay.TryPlay(move)));
        Assert.Equal(GameStatus.Won, replay.Status);
        Assert.Equal(record.Outcome.MovesUsed, replay.MovesUsed);
    }

    [Fact]
    public void Leaving_an_undecided_attempt_records_it_as_abandoned_unless_no_move_was_played()
    {
        LevelDefinition level = GameTests.OpenLevel();
        LabParts lab = StartLab("attempts.jsonl");
        GameSession untouched = lab.Store.Create(Library(level), AttemptSeeds.From(5), NextRetry(level), 0, Phone);
        GameSession played = lab.Store.Create(Library(level), AttemptSeeds.From(6), NextRetry(level), 0, Phone);

        lab.Store.Leave(untouched.Id);
        Play(played, MoveSource.Player, lab.Recorder);
        Play(played, MoveSource.Bot, lab.Recorder);
        played.HintShown();
        _clock.Advance(TimeSpan.FromMinutes(5));
        lab.Store.Leave(played.Id);

        AttemptRecord record = Assert.Single(lab.Log.ReadAll().Records);
        Assert.Equal(AttemptResult.Abandoned, record.Outcome.Result);
        Assert.Equal(2, record.Outcome.MovesUsed);
        Assert.Equal(Noon.AddMinutes(5), record.Outcome.EndedUtc);
        Assert.NotNull(record.Lab);
        Assert.Equal(1, record.Lab.BotMoves);
        Assert.Equal(1, record.Lab.HintsShown);
        Assert.True(record.Lab.IsAssisted);
        Assert.False(lab.Store.TryGet(played.Id, out _));
    }

    [Fact]
    public void Only_a_session_idle_past_the_timeout_is_swept_from_the_store_and_recorded_as_abandoned_at_its_last_activity()
    {
        LevelDefinition level = GameTests.OpenLevel();
        LabParts lab = StartLab("attempts.jsonl");
        GameSession idle = lab.Store.Create(Library(level), AttemptSeeds.From(6), NextRetry(level), 0, Phone);
        _clock.Advance(TimeSpan.FromMinutes(5));
        Play(idle, MoveSource.Player, lab.Recorder);
        _clock.Advance(IdleTimeout); // no request touches the idle session meanwhile
        GameSession active = lab.Store.Create(Library(level), AttemptSeeds.From(7), NextRetry(level), 0, Phone);
        Play(active, MoveSource.Player, lab.Recorder);
        _clock.Advance(TimeSpan.FromMinutes(10)); // well within the timeout for the active session

        lab.Store.RemoveIdleSessions();

        Assert.False(lab.Store.TryGet(idle.Id, out _));
        Assert.True(lab.Store.TryGet(active.Id, out _));
        AttemptRecord record = SingleRecordOnceWritten(lab.Log);
        Assert.Equal(AttemptSeeds.From(6), record.Start.Seeds);
        Assert.Equal(AttemptResult.Abandoned, record.Outcome.Result);
        Assert.Equal(1, record.Outcome.MovesUsed);
        Assert.Equal(Noon.AddMinutes(5), record.Outcome.EndedUtc);
        Assert.Equal(RecordProgress.Open, active.Record);
    }

    [Fact]
    public void Attempts_still_played_when_the_lab_stops_are_recorded_as_abandoned_at_their_last_activity()
    {
        LabParts lab = StartLab("attempts.jsonl");
        GameSession won = lab.Store.Create(Library(EasyLevel()), AttemptSeeds.From(5), NextRetry(EasyLevel()), 0, Phone);
        PlayToTheEnd(won, lab.Recorder);
        LevelDefinition level = GameTests.OpenLevel();
        GameSession played = lab.Store.Create(Library(level), AttemptSeeds.From(6), NextRetry(level), 0, Phone);
        lab.Store.Create(Library(level), AttemptSeeds.From(7), NextRetry(level), 0, Phone); // looked at, never played
        _clock.Advance(TimeSpan.FromMinutes(5));
        Play(played, MoveSource.Player, lab.Recorder);
        _clock.Advance(TimeSpan.FromMinutes(5));

        lab.Store.RecordUnfinishedAttempts();

        AttemptRecord[] records = lab.Log.ReadAll().Records.ToArray();
        Assert.Equal(2, records.Length);
        Assert.Single(records, record => record.Outcome.Result == AttemptResult.Won);
        AttemptRecord abandoned = Assert.Single(records, record => record.Outcome.Result == AttemptResult.Abandoned);
        Assert.Equal(AttemptSeeds.From(6), abandoned.Start.Seeds);
        Assert.Equal(Noon.AddMinutes(5), abandoned.Outcome.EndedUtc);
    }

    [Fact]
    public void A_wall_clock_stepping_back_cannot_end_an_attempt_before_it_starts()
    {
        LevelDefinition level = EasyLevel();
        LabParts lab = StartLab("attempts.jsonl");
        GameSession session = lab.Store.Create(Library(level), AttemptSeeds.From(5), NextRetry(level), 0, Phone);
        _clock.StepWallClock(TimeSpan.FromHours(-1)); // a time sync after the host woke from sleep
        _clock.Advance(TimeSpan.FromMinutes(2));

        PlayToTheEnd(session, lab.Recorder);

        AttemptRecord record = Assert.Single(lab.Log.ReadAll().Records);
        Assert.Equal(Noon, record.Start.StartedUtc);
        Assert.Equal(Noon.AddMinutes(2), record.Outcome.EndedUtc);
        Assert.DoesNotContain(_logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public void A_record_the_format_refuses_is_logged_and_the_game_goes_on()
    {
        LevelDefinition level = EasyLevel();
        LabParts lab = StartLab("attempts.jsonl");
        var unnamed = new LibraryLevel(" ", "level_0001.json", level); // a lab record needs a level key
        GameSession session = lab.Store.Create(unnamed, AttemptSeeds.From(5), NextRetry(level), 0, Phone);

        PlayToTheEnd(session, lab.Recorder);

        Assert.Equal(GameStatus.Won, session.Game.Status);
        Assert.Empty(lab.Log.ReadAll().Records);
        LogEntry failure = Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.IsAssignableFrom<ArgumentException>(failure.Exception);
    }

    [Fact]
    public void A_failed_write_is_logged_the_attempt_is_not_called_recorded_and_the_game_goes_on()
    {
        File.WriteAllText(Path.Combine(_folder.Path, "not-a-folder"), "a file where the recordings folder should be");
        LevelDefinition level = EasyLevel();
        LabParts lab = StartLab(Path.Combine("not-a-folder", "attempts.jsonl"));
        GameSession session = lab.Store.Create(Library(level), AttemptSeeds.From(5), NextRetry(level), 0, Phone);

        PlayToTheEnd(session, lab.Recorder);
        lab.Store.Leave(session.Id); // the player moves on; the record is not tried twice for that

        Assert.Equal(GameStatus.Won, session.Game.Status);
        Assert.Equal(RecordProgress.Waiting, session.Record);
        LogEntry failure = Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.IsAssignableFrom<IOException>(failure.Exception);
    }

    [Fact]
    public void A_waiting_record_is_written_once_the_log_can_be_written_again()
    {
        LevelDefinition level = EasyLevel();
        LabParts lab = StartLab("attempts.jsonl");
        GameSession session = lab.Store.Create(Library(level), AttemptSeeds.From(5), NextRetry(level), 0, Phone);
        File.WriteAllText(lab.Log.Path, string.Empty);

        RecordProgress whileLocked;
        using (new FileStream(lab.Log.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) // a spreadsheet keeps it open
        {
            PlayToTheEnd(session, lab.Recorder);
            lab.Recorder.RetryWaiting();
            whileLocked = session.Record;
        }

        lab.Recorder.RetryWaiting();
        lab.Recorder.RetryWaiting(); // nothing waits any more: no second copy

        Assert.Equal(RecordProgress.Waiting, whileLocked);
        Assert.Equal(RecordProgress.Written, session.Record);
        AttemptRecord record = Assert.Single(lab.Log.ReadAll().Records);
        Assert.Equal(AttemptResult.Won, record.Outcome.Result);
    }

    [Fact]
    public void Past_the_bound_the_record_that_waited_longest_gives_way_and_the_rest_is_lost_when_the_lab_stops()
    {
        File.WriteAllText(Path.Combine(_folder.Path, "not-a-folder"), "a file where the recordings folder should be");
        LevelDefinition level = EasyLevel();
        LabParts lab = StartLab(Path.Combine("not-a-folder", "attempts.jsonl"), maxWaiting: 1);
        GameSession first = lab.Store.Create(Library(level), AttemptSeeds.From(5), NextRetry(level), 0, Phone);
        GameSession second = lab.Store.Create(Library(level), AttemptSeeds.From(6), NextRetry(level), 0, Phone);

        PlayToTheEnd(first, lab.Recorder);
        PlayToTheEnd(second, lab.Recorder);
        RecordProgress secondBeforeStop = second.Record;
        lab.Recorder.RetryWaitingBeforeStop();

        Assert.Equal(RecordProgress.Lost, first.Record);
        Assert.Equal(RecordProgress.Waiting, secondBeforeStop);
        Assert.Equal(RecordProgress.Lost, second.Record);
    }

    [Fact]
    public void Help_asked_after_the_attempt_ended_leaves_it_as_it_was_recorded()
    {
        LevelDefinition level = EasyLevel();
        LabParts lab = StartLab("attempts.jsonl");
        GameSession session = lab.Store.Create(Library(level), AttemptSeeds.From(5), NextRetry(level), 0, Phone);
        PlayToTheEnd(session, lab.Recorder);

        session.HintShown(); // the solver, run on the finished game to see what was possible

        Assert.Equal(0, session.HintsShown);
        Assert.Equal(AttemptStanding.Comparable, session.Standing);
        Assert.Equal(AttemptStanding.Comparable, AttemptCounting.Standing(Assert.Single(lab.Log.ReadAll().Records)));
    }

    [Fact]
    public void Only_the_devices_next_retry_under_the_levels_own_rules_keeps_its_number_and_any_other_says_why_not()
    {
        LevelDefinition level = GameTests.OpenLevel().WithAttempts(new AttemptPolicy { Board = new StartBoard.Varied(), SeedPool = new uint[] { 11, 12 } });
        LabParts lab = StartLab("attempts.jsonl");
        var origins = new Dictionary<string, AttemptOrigin>
        {
            ["next"] = new(1, true, level.Attempts.Board, InSequence: true, DeviceKept: true),
            ["asked for"] = new(1, true, level.Attempts.Board, InSequence: false, DeviceKept: true),
            ["seed given"] = new(null, false, level.Attempts.Board, InSequence: false, DeviceKept: true),
            ["other board"] = new(1, true, new StartBoard.Fixed(77), InSequence: true, DeviceKept: true),
            ["id not kept"] = new(1, true, level.Attempts.Board, InSequence: true, DeviceKept: false),
            ["pool used up"] = new(2, true, level.Attempts.Board, InSequence: true, DeviceKept: true), // pool[0] again
        };

        Dictionary<string, GameSession> sessions = origins.ToDictionary(
            pair => pair.Key,
            pair => lab.Store.Create(Library(level), level.Attempts.SeedsFor(pair.Value.Attempt ?? 0, 0), pair.Value, 0, Phone));

        Assert.Equal(1, sessions["next"].Start.Attempt);
        Assert.Null(sessions["next"].SequenceBreak);
        Assert.Equal(AttemptStanding.Comparable, sessions["next"].Standing);
        Assert.All(sessions.Where(pair => pair.Key != "next"), pair =>
        {
            Assert.Null(pair.Value.Start.Attempt);
            Assert.Equal(AttemptStanding.ChosenSeeds, pair.Value.Standing);
        });
        Assert.Equal(
            new SequenceBreak?[] { SequenceBreak.ChosenByHand, SequenceBreak.ChosenByHand, SequenceBreak.StartBoardOverride, SequenceBreak.DeviceNotKept, SequenceBreak.PoolReplay },
            new[] { "asked for", "seed given", "other board", "id not kept", "pool used up" }.Select(name => sessions[name].SequenceBreak));
    }

    [Fact]
    public void The_standing_a_session_shows_is_how_the_comparison_counts_its_record()
    {
        LevelDefinition level = GameTests.OpenLevel();
        LabParts lab = StartLab("attempts.jsonl");
        var handPicked = new AttemptOrigin(null, false, level.Attempts.Board, InSequence: false, DeviceKept: true);
        GameSession own = lab.Store.Create(Library(level), AttemptSeeds.From(1), NextRetry(level), 0, Phone);
        GameSession withAssist = lab.Store.Create(Library(level), AttemptSeeds.From(2), NextRetry(level), 20, Phone);
        GameSession hinted = lab.Store.Create(Library(level), AttemptSeeds.From(3), NextRetry(level), 0, Phone);
        GameSession botHelped = lab.Store.Create(Library(level), AttemptSeeds.From(4), NextRetry(level), 0, Phone);
        GameSession chosen = lab.Store.Create(Library(level), AttemptSeeds.From(5), handPicked, 0, Phone);
        GameSession chosenAndHinted = lab.Store.Create(Library(level), AttemptSeeds.From(6), handPicked, 0, Phone);
        GameSession[] sessions = { own, withAssist, hinted, botHelped, chosen, chosenAndHinted };

        hinted.HintShown();
        chosenAndHinted.HintShown();
        Play(botHelped, MoveSource.Bot, lab.Recorder);
        foreach (GameSession session in sessions)
        {
            Play(session, MoveSource.Player, lab.Recorder);
        }

        Dictionary<ulong, AttemptStanding> shown = sessions.ToDictionary(session => session.Seeds.Play, session => session.Standing);
        foreach (GameSession session in sessions)
        {
            lab.Store.Leave(session.Id);
        }

        AttemptRecord[] records = lab.Log.ReadAll().Records.ToArray();
        Assert.Equal(sessions.Length, records.Length);
        Assert.All(records, record => Assert.Equal(shown[record.Start.Seeds.Play], AttemptCounting.Standing(record)));
        Assert.Equal(
            new[] { AttemptStanding.Comparable, AttemptStanding.Assisted, AttemptStanding.Assisted, AttemptStanding.Assisted, AttemptStanding.ChosenSeeds, AttemptStanding.Assisted },
            sessions.Select(session => shown[session.Seeds.Play]));
    }

    [Fact]
    public void Retry_numbers_continue_after_the_recordings_for_each_device_and_level_version()
    {
        LevelDefinition level = GameTests.OpenLevel();
        LevelDefinition changed = level.WithMoveLimit(25);
        var tablet = new DeviceId("tablet-0002");
        AttemptRecord[] recorded =
        {
            Of(level, AttemptResult.Won, 18, attempt: 0, lab: Lab(level)),
            Of(level, AttemptResult.OutOfMoves, 30, attempt: 3, lab: Lab(level)),
            Of(level, AttemptResult.Won, 18, attempt: 1, lab: Lab(level)),
            Of(level, AttemptResult.Won, 18, attempt: 9, lab: null), // the released game numbers its own retries
            Of(level, AttemptResult.Won, 18, attempt: null, lab: Lab(level)), // hand-chosen seeds
        };

        var retries = new RetryNumbers(recorded);

        Assert.Equal(4, retries.Take(Phone, level));
        Assert.Equal(5, retries.Take(Phone, level)); // a reload or a second tab gets the next retry, never the same one
        Assert.Equal(0, retries.Take(tablet, level));
        Assert.Equal(0, retries.Take(Phone, changed));
    }

    [Fact]
    public void Retry_numbers_are_bounded_and_forget_the_device_and_level_used_least_recently()
    {
        LevelDefinition level = GameTests.OpenLevel();
        var tablet = new DeviceId("tablet-0002");
        var laptop = new DeviceId("laptop-0003");
        var retries = new RetryNumbers(new[] { Of(level, AttemptResult.Won, 18, attempt: 6, lab: Lab(level)) }, capacity: 2);

        retries.Take(tablet, level); // the phone's entry is now the one used least recently, but still kept
        Assert.Equal(7, retries.Take(Phone, level));
        retries.Take(laptop, level); // a third entry: the tablet's goes

        Assert.Equal(8, retries.Take(Phone, level));
        Assert.Equal(0, retries.Take(tablet, level));
    }

    private static LevelDefinition EasyLevel() =>
        Level(Blank(10, 12), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 3) }, moves: 30, colors: 5);

    private static LibraryLevel Library(LevelDefinition level) => new("generated/level_0001", "level_0001.json", level);

    /// <summary>The first retry, numbered by the server under the level's own start-board rule.</summary>
    private static AttemptOrigin NextRetry(LevelDefinition level) => new(0, false, level.Attempts.Board, InSequence: true, DeviceKept: true);

    /// <summary>What the lab's move endpoints do while holding the gate: play, keep the move, record if it decided the game.</summary>
    private static void Play(GameSession session, Move move, MoveSource source, AttemptRecorder recorder)
    {
        lock (session.Gate)
        {
            Assert.True(session.Game.TryPlay(move));
            session.MovePlayed(move, source);
            recorder.RecordIfDecided(session);
        }
    }

    private static void Play(GameSession session, MoveSource source, AttemptRecorder recorder) =>
        Play(session, session.Game.FindLegalMoves()[0], source, recorder);

    private static void PlayToTheEnd(GameSession session, AttemptRecorder recorder)
    {
        var bot = new GreedyPolicy(SkillProfile.Expert);
        while (!session.Game.Status.IsOver())
        {
            Play(session, bot.ChooseMove(session.Game, session.Game.FindLegalMoves(), session.BotRng), MoveSource.Player, recorder);
        }
    }

    /// <summary>The cache runs eviction callbacks on the thread pool: waits until the one record they write is in the log.</summary>
    private static AttemptRecord SingleRecordOnceWritten(AttemptLog log)
    {
        Assert.True(SpinWait.SpinUntil(() => log.ReadAll().Records.Count > 0, TimeSpan.FromSeconds(10)), "Nothing was recorded.");
        return Assert.Single(log.ReadAll().Records);
    }

    private LabParts StartLab(string logFile, int maxWaiting = AttemptRecorder.DefaultMaxWaiting)
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, logFile));
        var recorder = new AttemptRecorder(log, _logger, maxWaiting);
        return new LabParts(new GameSessionStore(_cache, IdleTimeout, recorder, _clock), recorder, log);
    }

    private sealed record LabParts(GameSessionStore Store, AttemptRecorder Recorder, AttemptLog Log);
}
