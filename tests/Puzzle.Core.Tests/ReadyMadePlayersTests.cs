using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Puzzle.Core.Levels;
using Puzzle.Tooling.Players;
using Puzzle.Web;
using Puzzle.Web.Accounts;

namespace Puzzle.Core.Tests;

/// <summary>
/// The ready-made accounts of the public test: the one rule they skip, the test settings file that lists them, and
/// what a public server makes of that file when it starts.
/// </summary>
public sealed class ReadyMadePlayersTests : IDisposable
{
    /// <summary>Shorter than the owner's tool takes; its '!' is in no hash, id or date, so it cannot be in a file by chance.</summary>
    private const string ShortPassword = "p!7";

    private readonly TempFolder _folder = new();
    private readonly ManualClock _clock = new();
    private readonly PasswordHasher _cheapHasher = new(iterations: 1_000);

    public ReadyMadePlayersTests()
    {
        PublicLevels.Write(Path.Combine(_folder.Path, "levels"), PublicLevels.Easy(1), PublicLevels.Easy(2));
    }

    private string DataFolder => PuzzleServer.DataFolderIn(_folder.Path);

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void The_ready_made_accounts_that_are_missing_are_made_like_any_other_account()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);

        IReadOnlyList<PlayerAccount> made = PlayerAddition.AddMissing(store, _cheapHasher, [ReadyMade("Deneme1"), ReadyMade("Deneme2")], _clock.GetUtcNow());

        Assert.Equal(new[] { "Deneme1", "Deneme2" }, made.Select(account => account.Username.Value));
        Assert.Equal(made, store.Accounts);
        Assert.All(made, account => Assert.Equal((TestRecords.Noon, PlayerProgress.None), (account.CreatedUtc, account.Progress)));
        Assert.All(made, account => Assert.StartsWith("pbkdf2-sha256$1000$", account.PasswordHash));
        Assert.DoesNotContain(ShortPassword, File.ReadAllText(store.Path));
        Assert.NotEqual(made[0].PasswordHash, made[1].PasswordHash); // the same password, a salt each
        Assert.NotEqual(made[0].PlayerId, made[1].PlayerId);
        Assert.NotEqual(made[0].SignInStamp, made[1].SignInStamp);
    }

    [Fact]
    public void A_ready_made_account_signs_in_with_a_password_shorter_than_the_owners_tool_takes()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAddition.AddMissing(store, _cheapHasher, [ReadyMade("Deneme1")], _clock.GetUtcNow());
        var accounts = new PlayerAccounts(store, _cheapHasher, new SignInLockouts(_clock), new ListLogger<PlayerAccounts>());

        Assert.IsType<SignInOutcome.SignedIn>(accounts.SignIn("deneme1", ShortPassword));
        Assert.IsType<SignInOutcome.WrongCredentials>(accounts.SignIn("Deneme1", ShortPassword + "x"));
    }

    [Fact]
    public void The_owners_tool_still_refuses_the_password_a_ready_made_account_takes()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount readyMade = Assert.Single(PlayerAddition.AddMissing(store, _cheapHasher, [ReadyMade("Deneme1")], _clock.GetUtcNow()));

        Assert.IsType<AddPlayerOutcome.PasswordRefused>(PlayerAddition.Add(store, _cheapHasher, new Username("Mert"), ShortPassword, _clock.GetUtcNow()));
        Assert.IsType<ChangePasswordOutcome.PasswordRefused>(PlayerPasswordChange.Change(store, _cheapHasher, readyMade.Username, ShortPassword));
        Assert.Equal(readyMade, Assert.Single(store.Accounts));
    }

    [Fact]
    public void A_ready_made_account_that_is_there_already_is_left_alone_with_its_progress_also_when_the_listed_password_differs()
    {
        using PlayerStore store = PlayerStore.Open(DataFolder);
        PlayerAccount ayse = Assert.Single(PlayerAddition.AddMissing(store, _cheapHasher, [ReadyMade("Ayse")], _clock.GetUtcNow()));
        PlayerAccount afterAWin = store.UpdateProgress(ayse.PlayerId, progress => progress.WithLevelWon(new LevelId(1)));
        _clock.Advance(TimeSpan.FromDays(1));

        IReadOnlyList<PlayerAccount> made = PlayerAddition.AddMissing(
            store,
            _cheapHasher,
            [new ReadyMadePlayer(new Username("AYSE"), "another one"), ReadyMade("Deneme1")],
            _clock.GetUtcNow());

        var accounts = new PlayerAccounts(store, _cheapHasher, new SignInLockouts(_clock), new ListLogger<PlayerAccounts>());
        Assert.Equal("Deneme1", Assert.Single(made).Username.Value);
        Assert.Equal(new[] { afterAWin, made[0] }, store.Accounts); // the hash, the stamp, the id, the day it was made, the win: all as they were
        Assert.IsType<SignInOutcome.SignedIn>(accounts.SignIn("Ayse", ShortPassword));
        Assert.IsType<SignInOutcome.WrongCredentials>(accounts.SignIn("Ayse", "another one"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(PasswordRules.MinLength - 1, true)]
    [InlineData(PasswordRules.MaxLength, true)]
    [InlineData(0, false)]
    [InlineData(PasswordRules.MaxLength + 1, false)]
    public void A_ready_made_password_skips_the_minimum_length_and_no_other_rule(int length, bool taken)
    {
        string password = new('x', length);

        Assert.Equal(taken, ReadyMadePlayer.TryCreate(new Username("Deneme1"), password, out ReadyMadePlayer? player));
        Assert.Equal(taken ? password : null, player?.Password);
    }

    [Fact]
    public void A_ready_made_player_has_a_password_and_its_text_form_never_spells_it()
    {
        ReadyMadePlayer player = ReadyMade("Deneme1");

        Assert.False(ReadyMadePlayer.TryCreate(player.Username, null, out _));
        Assert.Throws<ArgumentException>(() => new ReadyMadePlayer(player.Username, string.Empty));
        Assert.Equal("Deneme1", player.ToString());
        Assert.DoesNotContain(ShortPassword, $"{player}");
    }

    [Fact]
    public void The_test_settings_file_lists_the_accounts_in_its_order_and_a_file_that_is_not_there_lists_none()
    {
        string file = WriteListing(
            """
            {
              "about": "what the file is for; the server does not read this",
              "players": [
                { "username": "Deneme2", "password": "p!7" },
                { "username": "Deneme1", "password": "another one" }
              ]
            }
            """);

        IReadOnlyList<ReadyMadePlayer> listed = ReadyMadePlayersFile.Read(file);

        Assert.Equal(new[] { ReadyMade("Deneme2"), new ReadyMadePlayer(new Username("Deneme1"), "another one") }, listed);
        Assert.Empty(ReadyMadePlayersFile.Read(Path.Combine(_folder.Path, "no-such-file.json")));
        Assert.Empty(ReadyMadePlayersFile.Read(WriteListing("""{ "about": "nobody yet" }""")));
    }

    [Theory]
    [InlineData("""{ "players": [ { "username": "Deneme1", "password": "s3cret-p!7" }, """, "not the JSON expected")]
    [InlineData("""{ "players": [ { "username": "Deneme1", "password": 1237 } ] }""", "$.players[0].password")]
    [InlineData("null", "no JSON object")]
    [InlineData("""{ "players": [ { "username": "s3cret-p!7", "password": "Deneme1" } ] }""", "account 1 has no usable username")]
    [InlineData("""{ "players": [ { "username": "Deneme1", "password": "s3cret-p!7" }, null ] }""", "account 2 has no usable username")]
    [InlineData("""{ "players": [ { "username": "Deneme1", "password": "" } ] }""", "the password of 'Deneme1'")]
    [InlineData("""{ "players": [ { "username": "Deneme1" } ] }""", "the password of 'Deneme1'")]
    [InlineData("""{ "players": [ { "username": "Deneme1", "password": "s3cret-p!7" }, { "username": "DENEME1", "password": "s3cret-p!7" } ] }""", "'DENEME1' is listed twice")]
    public void A_test_settings_file_that_cannot_be_used_says_what_is_wrong_and_never_spells_what_may_be_a_password(string json, string told)
    {
        string file = WriteListing(json);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => ReadyMadePlayersFile.Read(file));

        Assert.Contains(file, refused.Message);
        string aboutTheFile = refused.Message.Replace(file, string.Empty); // the folder's random name may hold any digits
        Assert.Contains(told, aboutTheFile);
        Assert.Contains("Puzzle:ReadyMadePlayersPath", aboutTheFile);
        Assert.DoesNotContain("s3cret-p!7", aboutTheFile);
        Assert.DoesNotContain("1237", aboutTheFile);
    }

    [Fact]
    public void A_listed_password_longer_than_a_sign_in_takes_is_refused()
    {
        string tooLong = new('x', PasswordRules.MaxLength + 1);
        string file = WriteListing($$"""{ "players": [ { "username": "Deneme1", "password": "{{tooLong}}" } ] }""");

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => ReadyMadePlayersFile.Read(file));

        Assert.Contains("the password of 'Deneme1'", refused.Message);
        Assert.DoesNotContain(tooLong, refused.Message);
    }

    [Fact]
    public void The_repositorys_own_test_settings_file_is_the_one_the_server_reads_by_default_and_says_what_its_accounts_are()
    {
        string file = RepoFiles.ReadyMadePlayersPath;
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "appsettings.json")));
        using JsonDocument listing = JsonDocument.Parse(File.ReadAllText(file));

        IReadOnlyList<ReadyMadePlayer> listed = ReadyMadePlayersFile.Read(file);

        Assert.NotEmpty(listed);
        Assert.Equal(Path.GetFileName(file), settings.RootElement.GetProperty("Puzzle").GetProperty("ReadyMadePlayersPath").GetString());
        Assert.Contains("TEST accounts of the public test", listing.RootElement.GetProperty("about").GetString());
    }

    [Fact]
    public async Task A_public_server_makes_the_listed_accounts_at_its_start_and_they_sign_in_and_play()
    {
        string file = WriteListing(Listing(("Deneme1", ShortPassword), ("Deneme2", ShortPassword)));

        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path, ("ReadyMadePlayersPath", file));
        TestBrowser browser = server.NewBrowser();
        using HttpResponseMessage signedIn = await browser.SignInAsync("Deneme2", ShortPassword);
        JsonElement game = await browser.PostJsonAsync("/api/player/game/attempts");
        using HttpResponseMessage lab = await browser.GetAsync("/api/levels");

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal("Playing", game.GetProperty("attempt").GetProperty("state").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, lab.StatusCode); // a player like any other: the lab stays the owner's
        IReadOnlyList<PlayerAccount> stored = PlayerStore.ReadAccounts(server.DataFolder);
        Assert.Equal(new[] { "Deneme1", "Deneme2" }, stored.Select(account => account.Username.Value));
        Assert.All(stored, account => Assert.StartsWith($"pbkdf2-sha256${PasswordHasher.DefaultIterations}$", account.PasswordHash)); // hashed at the server's cost
        Assert.DoesNotContain(ShortPassword, File.ReadAllText(Path.Combine(server.DataFolder, "players.json")));
    }

    [Fact]
    public async Task A_restart_makes_only_what_is_missing_and_an_account_keeps_the_password_it_has_whatever_the_file_says_now()
    {
        PuzzleServer.AddPlayerTo(_folder.Path, "Ayse"); // the owner's own, made by hand before the file named her
        string file = WriteListing(Listing(("Ayse", ShortPassword), ("Deneme1", ShortPassword)));
        await using (PuzzleServer first = await PuzzleServer.StartPublicAsync(_folder.Path, ("ReadyMadePlayersPath", file)))
        {
            using HttpResponseMessage ayseWithHerOwnPassword = await first.NewBrowser().SignInAsync("Ayse");
            Assert.Equal(HttpStatusCode.OK, ayseWithHerOwnPassword.StatusCode);
        }

        IReadOnlyList<PlayerAccount> afterTheFirstStart = PlayerStore.ReadAccounts(DataFolder);
        WriteListing(Listing(("Ayse", ShortPassword), ("Deneme1", "changed in the file"), ("Deneme2", ShortPassword)));

        await using PuzzleServer restarted = await PuzzleServer.StartPublicAsync(_folder.Path, ("ReadyMadePlayersPath", file));
        IReadOnlyList<PlayerAccount> afterTheRestart = PlayerStore.ReadAccounts(DataFolder);
        using HttpResponseMessage withTheFilesPassword = await restarted.NewBrowser().SignInAsync("Deneme1", "changed in the file");
        using HttpResponseMessage withItsOwnPassword = await restarted.NewBrowser().SignInAsync("Deneme1", ShortPassword);
        using HttpResponseMessage ayseWithTheFilesPassword = await restarted.NewBrowser().SignInAsync("Ayse", ShortPassword);
        using HttpResponseMessage newcomer = await restarted.NewBrowser().SignInAsync("Deneme2", ShortPassword);

        Assert.Equal(new[] { "Ayse", "Deneme1" }, afterTheFirstStart.Select(account => account.Username.Value));
        Assert.Equal(afterTheFirstStart, afterTheRestart.Take(2)); // not a field of either changed
        Assert.Equal("Deneme2", afterTheRestart[2].Username.Value);
        Assert.Equal(HttpStatusCode.Unauthorized, withTheFilesPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withItsOwnPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, ayseWithTheFilesPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newcomer.StatusCode);
    }

    [Fact]
    public async Task A_test_settings_file_that_cannot_be_used_stops_a_public_server_at_the_start_and_the_lab_never_reads_it()
    {
        string file = WriteListing("""{ "players": [ { "username": "Deneme1" } ] }""");

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => PuzzleApp.Build(PuzzleServer.ArgsFor(_folder.Path, "Public", ("ReadyMadePlayersPath", file))));
        await using PuzzleServer lab = await PuzzleServer.StartLabAsync(_folder.Path, ("ReadyMadePlayersPath", file));

        Assert.Contains(file, refused.Message);
        Assert.False(Directory.Exists(lab.DataFolder)); // the lab mode makes no accounts, ready-made or other
    }

    [Fact]
    public async Task A_public_server_whose_settings_name_no_file_or_a_file_that_is_not_there_makes_no_account()
    {
        await using (PuzzleServer withoutTheSetting = await PuzzleServer.StartPublicAsync(_folder.Path))
        {
            Assert.Empty(PlayerStore.ReadAccounts(withoutTheSetting.DataFolder));
        }

        await using PuzzleServer withoutTheFile = await PuzzleServer.StartPublicAsync(
            _folder.Path,
            ("ReadyMadePlayersPath", Path.Combine(_folder.Path, "no-such-file.json")));
        Assert.Empty(PlayerStore.ReadAccounts(withoutTheFile.DataFolder));
    }

    private static ReadyMadePlayer ReadyMade(string username) => new(new Username(username), ShortPassword);

    /// <summary>The test settings file's JSON for <paramref name="players"/>.</summary>
    private static string Listing(params (string Username, string Password)[] players) => JsonSerializer.Serialize(new
    {
        about = "accounts of this test",
        players = players.Select(player => new { username = player.Username, password = player.Password }),
    });

    /// <summary>Writes <paramref name="json"/> as this test's settings file and returns its path.</summary>
    private string WriteListing(string json)
    {
        string path = Path.Combine(_folder.Path, "ready-made-players.json");
        File.WriteAllText(path, json);
        return path;
    }
}
