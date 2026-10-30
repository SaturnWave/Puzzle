using System.IO;
using System.Threading.Tasks;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Players;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

/// <summary>The file-backed account store of the public mode, and the deletion a player may ask for.</summary>
public sealed class PlayerStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();

    private string DataFolder => Path.Combine(_folder.Path, "data");

    private string AccountsFile => Path.Combine(DataFolder, "players.json");

    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData("abc")]
    [InlineData("Ayse_42")]
    [InlineData("a-b-c")]
    [InlineData("ABCDEFGHIJ0123456789")]
    public void A_username_is_3_to_20_ascii_letters_digits_underscores_or_dashes(string name)
    {
        Assert.True(Username.TryCreate(name, out Username? username));
        Assert.Equal(name, username.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("ABCDEFGHIJ0123456789x")]
    [InlineData("ay se")]
    [InlineData("ayşe")]
    [InlineData("a@b.c")]
    [InlineData("abc\n")]
    [InlineData("../abc")]
    public void Anything_else_is_not_a_username(string? name)
    {
        Assert.False(Username.TryCreate(name, out _));
    }

    [Fact]
    public void Usernames_that_differ_only_in_case_are_the_same_name()
    {
        Assert.Equal(new Username("Ayse"), new Username("aYSE"));
        Assert.Equal(new Username("Ayse").GetHashCode(), new Username("aYSE").GetHashCode());
        Assert.NotEqual(new Username("Ayse"), new Username("Ayse1"));
    }

    [Fact]
    public void A_new_player_id_is_a_random_device_id_with_nothing_personal_in_it()
    {
        DeviceId first = PlayerAccount.NewPlayerId();
        DeviceId second = PlayerAccount.NewPlayerId();

        Assert.NotEqual(first, second);
        Assert.Matches("^[0-9a-f]{32}$", first.Value);
    }

    [Fact]
    public void An_accounts_text_form_never_spells_its_password_hash()
    {
        PlayerAccount account = Account("Ayse", passwordHash: "pbkdf2-sha256$1000$c2FsdA==$aGFzaA==");

        Assert.Equal("Ayse", account.ToString());
        Assert.DoesNotContain("pbkdf2", $"{account}");
    }

    [Fact]
    public void The_store_reads_back_what_it_wrote()
    {
        PlayerAccount ayse = Account("Ayse");
        PlayerAccount mert = Account("Mert");
        using (PlayerStore store = PlayerStore.Open(DataFolder))
        {
            Assert.True(store.TryAdd(ayse));
            Assert.True(store.TryAdd(mert));
            store.UpdateProgress(mert.PlayerId, progress => progress.WithLevelWon(new LevelId(4)).WithAttemptStartedOn("0123456789abcdef"));
        }

        using PlayerStore reopened = PlayerStore.Open(DataFolder);

        Assert.Equal(new[] { "Ayse", "Mert" }, reopened.Accounts.Select(account => account.Username.Value));
        Assert.True(reopened.TryFind(new Username("ayse"), out PlayerAccount? readAyse));
        Assert.Equal(
            (ayse.PlayerId, ayse.PasswordHash, ayse.SignInStamp, ayse.CreatedUtc, PlayerProgress.None),
            (readAyse.PlayerId, readAyse.PasswordHash, readAyse.SignInStamp, readAyse.CreatedUtc, readAyse.Progress));
        Assert.True(reopened.TryFind(mert.PlayerId, out PlayerAccount? readMert));
        Assert.Equal(new PlayerProgress(1, "0123456789abcdef", 1, new LevelId(4)), readMert.Progress);
    }

    [Fact]
    public void A_live_attempt_is_read_back_with_its_seeds_and_every_move_in_order()
    {
        PlayerAccount ayse = Account("Ayse");
        var live = new SavedAttempt(
            Guid.NewGuid(),
            new LevelId(12),
            levelFingerprint: "0123456789abcdef",
            attempt: 2,
            AttemptSeeds.WithFixedBoard(7, ulong.MaxValue),
            Noon.AddMinutes(5),
            new[] { Move.Swap(TestBoards.P(1, 1), TestBoards.P(1, 2)), Move.Tap(TestBoards.P(3, 4)), Move.Swap(TestBoards.P(5, 0), TestBoards.P(4, 0)) });
        using (PlayerStore store = PlayerStore.Open(DataFolder))
        {
            store.TryAdd(ayse);
            store.UpdateProgress(ayse.PlayerId, progress => progress.WithAttemptStartedOn("0123456789abcdef").WithLive(live));
        }

        PlayerProgress read = Assert.Single(PlayerStore.ReadAccounts(DataFolder)).Progress;

        Assert.Equal(live, read.Live);
        Assert.Equal(live.Moves, read.Live?.Moves);
        Assert.Equal(new PlayerProgress(0, "0123456789abcdef", 1, lastWonLevel: null, live), read);
        Assert.Null(read.WithLive(null).Live);
    }

    [Theory]
    [InlineData("\"live\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"level\":1,\"levelFingerprint\":\"fp\",\"attempt\":0,\"boardSeed\":1,\"playSeed\":1,\"startedUtc\":\"2026-10-03T12:00:00+00:00\",\"moves\":[[1,1,5,5]]}")]
    [InlineData("\"live\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"level\":1,\"levelFingerprint\":\"fp\",\"attempt\":0,\"boardSeed\":1,\"playSeed\":1,\"startedUtc\":\"2026-10-03T12:00:00+00:00\",\"moves\":[[1]]}")]
    [InlineData("\"live\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"level\":0,\"levelFingerprint\":\"fp\",\"attempt\":0,\"boardSeed\":1,\"playSeed\":1,\"startedUtc\":\"2026-10-03T12:00:00+00:00\",\"moves\":[]}")]
    [InlineData("\"live\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"level\":1,\"levelFingerprint\":\"fp\",\"attempt\":0,\"moves\":[]}")]
    [InlineData("\"lastWonLevel\":0")]
    public void A_live_attempt_or_a_last_won_level_the_types_refuse_makes_a_damaged_store(string progressField)
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(
            AccountsFile,
            "{\"schema\":2,\"accounts\":[{\"username\":\"Ayse\",\"playerId\":\"0123456789abcdef\",\"passwordHash\":\"x\",\"signInStamp\":\"abc\",\"createdUtc\":\"2026-10-03T12:00:00+00:00\","
            + "\"progress\":{\"levelsWon\":0,\"attemptsStarted\":0," + progressField + "}}]}");

        Assert.Throws<PlayerStoreFormatException>(() => PlayerStore.ReadAccounts(DataFolder));
    }

    [Fact]
    public void A_store_written_before_stamps_and_live_attempts_reads_as_accounts_on_their_first_stamp_with_no_live_attempt()
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(
            AccountsFile,
            "{\"schema\":1,\"accounts\":[{\"username\":\"Ayse\",\"playerId\":\"0123456789abcdef\",\"passwordHash\":\"x\",\"createdUtc\":\"2026-10-03T12:00:00+00:00\","
            + "\"progress\":{\"levelsWon\":3,\"attemptedLevel\":\"fp\",\"attemptsStarted\":2}}]}");

        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount ayse = Assert.Single(store.Accounts);
        store.UpdateProgress(ayse.PlayerId, progress => progress.WithAttemptStartedOn("fp")); // the next change writes today's form

        Assert.Equal(SignInStamp.First, ayse.SignInStamp);
        Assert.Equal(new PlayerProgress(3, "fp", 2), ayse.Progress);
        PlayerAccount rewritten = Assert.Single(PlayerStore.ReadAccounts(DataFolder));
        Assert.Equal((SignInStamp.First, new PlayerProgress(3, "fp", 3)), (rewritten.SignInStamp, rewritten.Progress));
    }

    [Fact]
    public void A_new_password_brings_a_new_sign_in_stamp_and_keeps_the_progress_and_the_player_id()
    {
        var hasher = new PasswordHasher(iterations: 1_000);
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount before = Assert.IsType<AddPlayerOutcome.Added>(PlayerAddition.Add(store, hasher, new Username("Ayse"), "the old password", Noon)).Account;
        store.UpdateProgress(before.PlayerId, progress => progress.WithLevelWon(new LevelId(1)).WithAttemptStartedOn("0123456789abcdef"));

        PlayerAccount after = Assert.IsType<ChangePasswordOutcome.Changed>(PlayerPasswordChange.Change(store, hasher, new Username("AYSE"), "the new password")).Account;

        Assert.True(PasswordHasher.Verify("the new password", after.PasswordHash));
        Assert.False(PasswordHasher.Verify("the old password", after.PasswordHash));
        Assert.NotEqual(before.SignInStamp, after.SignInStamp);
        Assert.Equal((before.PlayerId, before.CreatedUtc, new PlayerProgress(1, "0123456789abcdef", 1, new LevelId(1))), (after.PlayerId, after.CreatedUtc, after.Progress));
        Assert.Equal(after.SignInStamp, Assert.Single(PlayerStore.ReadAccounts(DataFolder)).SignInStamp);
        Assert.DoesNotContain("the new password", File.ReadAllText(AccountsFile));
    }

    [Fact]
    public void A_new_password_outside_the_rules_or_for_nobody_changes_nothing()
    {
        var hasher = new PasswordHasher(iterations: 1_000);
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount ayse = Assert.IsType<AddPlayerOutcome.Added>(PlayerAddition.Add(store, hasher, new Username("Ayse"), "the old password", Noon)).Account;
        string intact = File.ReadAllText(AccountsFile);

        Assert.IsType<ChangePasswordOutcome.PasswordRefused>(PlayerPasswordChange.Change(store, hasher, ayse.Username, "short"));
        Assert.IsType<ChangePasswordOutcome.PasswordRefused>(PlayerPasswordChange.Change(store, hasher, ayse.Username, null));
        Assert.IsType<ChangePasswordOutcome.NoSuchPlayer>(PlayerPasswordChange.Change(store, hasher, new Username("Mert"), "the new password"));

        Assert.Equal(intact, File.ReadAllText(AccountsFile));
    }

    [Fact]
    public void Hashing_the_same_password_again_keeps_the_sign_in_stamp()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount ayse = Account("Ayse");
        store.TryAdd(ayse);

        PlayerAccount rehashed = store.ReplacePasswordHash(ayse.PlayerId, "pbkdf2-sha256$2000$c2FsdA==$aGFzaA==");

        Assert.Equal(ayse.SignInStamp, rehashed.SignInStamp);
    }

    [Fact]
    public void A_username_is_taken_whatever_its_case()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount ayse = Account("Ayse");
        store.TryAdd(ayse);

        Assert.False(store.TryAdd(Account("AYSE")));
        Assert.True(store.TryAdd(Account("Mert")));
        Assert.Equal(new[] { "Ayse", "Mert" }, PlayerStore.ReadAccounts(DataFolder).Select(account => account.Username.Value));
        Assert.True(store.TryFind(new Username("AYSE"), out PlayerAccount? kept));
        Assert.Equal(ayse.PlayerId, kept.PlayerId); // the refused account replaced nothing
    }

    [Fact]
    public void Concurrent_additions_do_not_lose_accounts()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount[] accounts = Enumerable.Range(0, 60).Select(number => Account($"player-{number}")).ToArray();

        Parallel.ForEach(accounts, new ParallelOptions { MaxDegreeOfParallelism = 8 }, account =>
            Assert.True(store.TryAdd(account)));

        Assert.Equal(60, store.Accounts.Count);
        Assert.Equal(
            accounts.Select(account => account.PlayerId.Value).Order(),
            PlayerStore.ReadAccounts(DataFolder).Select(account => account.PlayerId.Value).Order());
    }

    [Fact]
    public void Concurrent_additions_of_one_username_let_exactly_one_through()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        var results = new System.Collections.Concurrent.ConcurrentBag<bool>();

        Parallel.For(0, 40, _ => results.Add(store.TryAdd(Account("Ayse"))));

        Assert.Single(results, added => added);
        Assert.Single(store.Accounts);
        Assert.Single(PlayerStore.ReadAccounts(DataFolder));
    }

    [Fact]
    public void A_crash_between_the_temp_write_and_the_replace_leaves_the_old_file_intact()
    {
        PlayerAccount ayse = Account("Ayse");
        using (PlayerStore beforeCrash = PlayerStore.Open(DataFolder))
        {
            beforeCrash.TryAdd(ayse);
        }

        // The writer died after writing its temporary file (here half of one) and before moving it over the real file.
        string intact = File.ReadAllText(AccountsFile);
        File.WriteAllText(AccountsFile + ".tmp", intact[..(intact.Length / 2)].Replace("Ayse", "Mert"));

        using PlayerStore afterCrash = PlayerStore.Open(DataFolder);

        Assert.Equal(intact, File.ReadAllText(AccountsFile));
        Assert.Equal(ayse.PlayerId, Assert.Single(afterCrash.Accounts).PlayerId);

        afterCrash.TryAdd(Account("Zeynep")); // the next change simply writes over what the crash left
        Assert.Equal(new[] { "Ayse", "Zeynep" }, PlayerStore.ReadAccounts(DataFolder).Select(account => account.Username.Value));
        Assert.False(File.Exists(AccountsFile + ".tmp"));
    }

    [Fact]
    public void A_change_that_cannot_be_written_changes_neither_the_file_nor_what_the_store_answers()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount ayse = Account("Ayse");
        store.TryAdd(ayse);
        string intact = File.ReadAllText(AccountsFile);
        Directory.CreateDirectory(AccountsFile + ".tmp"); // a folder where the temporary file must go: no write can succeed

        Assert.ThrowsAny<Exception>(() => store.TryAdd(Account("Mert")));
        Assert.ThrowsAny<Exception>(() => store.UpdateProgress(ayse.PlayerId, progress => progress.WithLevelWon(new LevelId(1))));
        Assert.ThrowsAny<Exception>(() => store.Remove(ayse.Username));

        Assert.Equal(intact, File.ReadAllText(AccountsFile));
        Assert.False(store.TryFind(new Username("Mert"), out _));
        Assert.True(store.TryFind(ayse.Username, out PlayerAccount? kept));
        Assert.Equal(PlayerProgress.None, kept.Progress);

        Directory.Delete(AccountsFile + ".tmp");
        Assert.True(store.TryAdd(Account("Mert")));
    }

    [Fact]
    public void A_write_failure_is_an_io_failure_the_caller_can_name()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        Directory.CreateDirectory(AccountsFile + ".tmp");

        Exception failure = Assert.ThrowsAny<Exception>(() => store.TryAdd(Account("Ayse")));

        Assert.True(failure is IOException or UnauthorizedAccessException, failure.GetType().Name);
    }

    [Fact]
    public void An_open_store_keeps_every_other_writer_out_until_it_is_closed()
    {
        PlayerStore first = PlayerStore.Open(DataFolder);
        first.TryAdd(Account("Ayse"));

        Assert.Throws<PlayerStoreInUseException>(() => PlayerStore.Open(DataFolder));
        Assert.Single(PlayerStore.ReadAccounts(DataFolder)); // reading needs no lock

        first.Dispose();
        using PlayerStore second = PlayerStore.Open(DataFolder);
        Assert.Single(second.Accounts);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"schema\":1}")]
    [InlineData("{\"schema\":99,\"accounts\":[]}")]
    [InlineData("{\"schema\":1,\"accounts\":[{\"username\":\"a b\",\"playerId\":\"0123456789abcdef\",\"passwordHash\":\"x\",\"createdUtc\":\"2026-10-03T12:00:00+00:00\",\"progress\":{\"levelsWon\":0,\"attemptsStarted\":0}}]}")]
    [InlineData("{\"schema\":1,\"accounts\":[{\"username\":\"Ayse\",\"playerId\":\"0123456789abcdef\",\"passwordHash\":\"x\",\"createdUtc\":\"2026-10-03T12:00:00+00:00\",\"progress\":{\"levelsWon\":-1,\"attemptsStarted\":0}}]}")]
    public void A_damaged_store_is_refused_and_left_as_it_is(string content)
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(AccountsFile, content);

        Assert.Throws<PlayerStoreFormatException>(() => PlayerStore.Open(DataFolder));
        Assert.Throws<PlayerStoreFormatException>(() => PlayerStore.ReadAccounts(DataFolder));

        Assert.Equal(content, File.ReadAllText(AccountsFile));
        File.WriteAllText(AccountsFile, "{\"schema\":1,\"accounts\":[]}");
        using PlayerStore repaired = PlayerStore.Open(DataFolder); // the refused open did not keep the lock
        Assert.Empty(repaired.Accounts);
    }

    [Fact]
    public void Two_accounts_with_one_username_make_a_damaged_store()
    {
        Directory.CreateDirectory(DataFolder);
        const string Line = "{\"username\":\"NAME\",\"playerId\":\"0123456789abcdef\",\"passwordHash\":\"x\",\"createdUtc\":\"2026-10-03T12:00:00+00:00\",\"progress\":{\"levelsWon\":0,\"attemptsStarted\":0}}";
        File.WriteAllText(AccountsFile, $"{{\"schema\":1,\"accounts\":[{Line.Replace("NAME", "Ayse")},{Line.Replace("NAME", "AYSE").Replace("0123456789abcdef", "fedcba9876543210")}]}}");

        Assert.Throws<PlayerStoreFormatException>(() => PlayerStore.ReadAccounts(DataFolder));
    }

    [Fact]
    public void A_store_never_written_holds_no_accounts_and_reading_one_creates_nothing()
    {
        Assert.Empty(PlayerStore.ReadAccounts(DataFolder));
        Assert.False(Directory.Exists(DataFolder));
    }

    [Fact]
    public void The_store_defaults_to_the_data_folder_of_the_repository_root_wherever_the_server_starts()
    {
        Directory.CreateDirectory(Path.Combine(_folder.Path, "levels", "generated"));
        string project = Directory.CreateDirectory(Path.Combine(_folder.Path, "src", "Puzzle.Web")).FullName;

        Assert.Equal(Path.Combine(_folder.Path, "data"), PlayerStore.FindDefaultFolder(project));
        Assert.Equal(Path.Combine(_folder.Path, "data"), PlayerStore.FindDefaultFolder(_folder.Path));
    }

    [Fact]
    public void Attempts_are_counted_per_version_of_the_level_and_a_win_starts_the_count_again()
    {
        PlayerProgress progress = PlayerProgress.None
            .WithAttemptStartedOn("version-a")
            .WithAttemptStartedOn("version-a");

        Assert.Equal(2, progress.AttemptsOn("version-a"));
        Assert.Equal(0, progress.AttemptsOn("version-b")); // the level was regenerated: its seed pool starts again
        Assert.Equal(new PlayerProgress(0, "version-b", 1), progress.WithAttemptStartedOn("version-b"));
        Assert.Equal(new PlayerProgress(1, null, 0, new LevelId(7)), progress.WithLevelWon(new LevelId(7)));
    }

    [Fact]
    public void An_attempt_counts_once_its_first_move_is_played_and_a_win_keeps_the_won_attempt_as_the_live_one()
    {
        var opened = new SavedAttempt(Guid.NewGuid(), new LevelId(7), "version-a", attempt: 0, AttemptSeeds.From(11), Noon, Array.Empty<Move>());

        PlayerProgress looking = PlayerProgress.None.WithLive(opened);
        PlayerProgress won = looking.WithAttemptStartedOn("version-a").WithLevelWon(new LevelId(7));

        Assert.Equal(0, looking.AttemptsOn("version-a")); // opened and looked at: not an attempt yet
        Assert.Equal((1, new LevelId(7), opened), (won.LevelsWon, won.LastWonLevel, won.Live));
    }

    [Fact]
    public void Deleting_a_player_removes_the_account_the_progress_and_the_records_and_nothing_of_anyone_else()
    {
        LevelDefinition level = GameTests.OpenLevel();
        PlayerAccount ayse = Account("Ayse");
        PlayerAccount mert = Account("Mert");
        var log = new AttemptLog(Path.Combine(_folder.Path, "recordings", "attempts.jsonl"));
        log.Append(Of(level, AttemptResult.Won, 18, attempt: 0, lab: Lab(level), device: ayse.PlayerId));
        log.Append(Of(level, AttemptResult.OutOfMoves, 30, attempt: 0, lab: Lab(level), device: mert.PlayerId));
        log.Append(Of(level, AttemptResult.Abandoned, 4, attempt: 1, lab: Lab(level), device: ayse.PlayerId));
        log.Append(Of(level, AttemptResult.Won, 20, attempt: 0, lab: Lab(level))); // the owner's own lab attempt
        File.AppendAllText(log.Path, $"{{\"schema\":1,\"deviceId\":\"{ayse.PlayerId.Value}\",\"cut short\n"); // unreadable, but hers
        File.AppendAllText(log.Path, "{\"schema\":1,\"cut short and nobody's\n");

        using PlayerStore store = PlayerStore.Open(DataFolder);
        store.TryAdd(ayse);
        store.TryAdd(mert);
        store.UpdateProgress(ayse.PlayerId, progress => progress.WithLevelWon(new LevelId(1)));

        Assert.True(PlayerDeletion.TryDelete(store, log, new Username("AYSE"), out DeletedPlayer? deleted));

        Assert.Equal((ayse.PlayerId, 3), (deleted.PlayerId, deleted.RecordsRemoved));
        Assert.False(store.TryFind(ayse.Username, out _));
        Assert.False(store.TryFind(ayse.PlayerId, out _));
        Assert.Equal("Mert", Assert.Single(PlayerStore.ReadAccounts(DataFolder)).Username.Value);
        AttemptLogContents left = log.ReadAll();
        Assert.Equal(new[] { mert.PlayerId, Phone }, left.Records.Select(record => record.Start.Device));
        Assert.Single(left.Problems);
        Assert.DoesNotContain(ayse.PlayerId.Value, File.ReadAllText(log.Path));
        Assert.DoesNotContain("Ayse", File.ReadAllText(AccountsFile));
        Assert.False(File.Exists(log.Path + ".kept"));
    }

    [Fact]
    public void Deleting_a_player_nobody_knows_changes_nothing()
    {
        LevelDefinition level = GameTests.OpenLevel();
        var log = new AttemptLog(Path.Combine(_folder.Path, "recordings", "attempts.jsonl"));
        log.Append(Of(level, AttemptResult.Won, 18, lab: Lab(level)));
        string before = File.ReadAllText(log.Path);
        using PlayerStore store = PlayerStore.Open(DataFolder);
        store.TryAdd(Account("Mert"));

        Assert.False(PlayerDeletion.TryDelete(store, log, new Username("Ayse"), out _));

        Assert.Equal(before, File.ReadAllText(log.Path));
        Assert.Single(store.Accounts);
    }

    [Fact]
    public void A_player_without_records_or_without_a_log_is_still_deleted()
    {
        var log = new AttemptLog(Path.Combine(_folder.Path, "recordings", "attempts.jsonl"));
        using PlayerStore store = PlayerStore.Open(DataFolder);
        store.TryAdd(Account("Ayse"));

        Assert.True(PlayerDeletion.TryDelete(store, log, new Username("Ayse"), out DeletedPlayer? deleted));

        Assert.Equal(0, deleted.RecordsRemoved);
        Assert.Empty(store.Accounts);
        Assert.False(File.Exists(log.Path));
    }

    [Fact]
    public void When_the_records_cannot_be_removed_the_account_stays_so_the_deletion_can_be_run_again()
    {
        LevelDefinition level = GameTests.OpenLevel();
        PlayerAccount ayse = Account("Ayse");
        var log = new AttemptLog(Path.Combine(_folder.Path, "recordings", "attempts.jsonl"));
        log.Append(Of(level, AttemptResult.Won, 18, lab: Lab(level), device: ayse.PlayerId));
        using PlayerStore store = PlayerStore.Open(DataFolder);
        store.TryAdd(ayse);

        using (new FileStream(log.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) // a spreadsheet keeps the log open
        {
            Assert.Throws<IOException>(() => PlayerDeletion.TryDelete(store, log, ayse.Username, out _));
        }

        Assert.True(store.TryFind(ayse.Username, out _));
        Assert.Single(log.ReadAll().Records);
        Assert.True(PlayerDeletion.TryDelete(store, log, ayse.Username, out DeletedPlayer? deleted));
        Assert.Equal(1, deleted.RecordsRemoved);
    }

    [Fact]
    public void A_removal_that_was_cut_short_is_never_overwritten_by_the_next_one()
    {
        LevelDefinition level = GameTests.OpenLevel();
        var log = new AttemptLog(Path.Combine(_folder.Path, "recordings", "attempts.jsonl"));
        log.Append(Of(level, AttemptResult.Won, 18, lab: Lab(level)));
        File.WriteAllText(log.Path + ".kept", "the lines an interrupted removal kept\n");

        Assert.Throws<IOException>(() => log.RemoveRecordsOf(Phone));

        Assert.Equal("the lines an interrupted removal kept\n", File.ReadAllText(log.Path + ".kept"));
        Assert.Single(log.ReadAll().Records);
    }

    private static PlayerAccount Account(string username, string passwordHash = "pbkdf2-sha256$1000$c2FsdA==$aGFzaA==") =>
        new(new Username(username), PlayerAccount.NewPlayerId(), passwordHash, SignInStamp.New(), Noon, PlayerProgress.None);
}
