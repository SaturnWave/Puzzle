using System.IO;
using Microsoft.Extensions.Logging;
using Puzzle.Tooling.Players;
using Puzzle.Web.Accounts;

namespace Puzzle.Core.Tests;

/// <summary>The public mode's accounts: how the owner's tool adds a player, and the sign-in with its lockout after repeated failures.</summary>
public sealed class PlayerAccountsTests : IDisposable
{
    private const string Password = "correct horse";

    private readonly TempFolder _folder = new();
    private readonly ManualClock _clock = new();
    private readonly ListLogger<PlayerAccounts> _logger = new();
    private readonly PlayerStore _store;

    public PlayerAccountsTests()
    {
        _store = PlayerStore.Open(Path.Combine(_folder.Path, "data"));
    }

    public void Dispose()
    {
        _store.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public void Adding_a_player_makes_an_account_with_a_hashed_password_a_random_player_id_and_no_progress()
    {
        PlayerAccount ayse = Added(Add("Ayse"));
        PlayerAccount mert = Added(Add("Mert"));

        Assert.True(_store.TryFind(new Username("ayse"), out PlayerAccount? stored));
        Assert.Equal(ayse, stored);
        Assert.Equal((TestRecords.Noon, PlayerProgress.None), (ayse.CreatedUtc, ayse.Progress));
        Assert.DoesNotContain(Password, File.ReadAllText(_store.Path));
        Assert.NotEqual(ayse.PasswordHash, mert.PasswordHash); // the same password, a salt each
        Assert.NotEqual(ayse.PlayerId, mert.PlayerId);
        Assert.DoesNotContain("ayse", ayse.PlayerId.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    public void A_password_shorter_than_eight_characters_is_refused(string? password)
    {
        Assert.IsType<AddPlayerOutcome.PasswordRefused>(Add("Ayse", password));
        Assert.Empty(_store.Accounts);
    }

    [Fact]
    public void A_password_of_eight_characters_is_enough_and_one_past_the_bound_is_refused()
    {
        Added(Add("Ayse", "12345678"));
        Added(Add("Zeynep", new string('x', PasswordRules.MaxLength)));

        Assert.IsType<AddPlayerOutcome.PasswordRefused>(Add("Mert", new string('x', PasswordRules.MaxLength + 1)));
        Assert.Equal(2, _store.Accounts.Count);
    }

    [Fact]
    public void A_username_someone_has_is_taken_whatever_its_case()
    {
        PlayerAccount first = Added(Add("Ayse"));

        Assert.IsType<AddPlayerOutcome.UsernameTaken>(Add("AYSE", "another password"));

        Assert.Equal(first, Assert.Single(_store.Accounts));
    }

    [Fact]
    public void The_right_password_signs_in_whatever_the_usernames_case()
    {
        PlayerAccount added = Added(Add("Ayse"));

        SignInOutcome.SignedIn signedIn = Assert.IsType<SignInOutcome.SignedIn>(Accounts().SignIn(" ayse ", Password));

        Assert.Equal(added.PlayerId, signedIn.Account.PlayerId);
    }

    [Theory]
    [InlineData("Ayse", "wrong password")]
    [InlineData("Nobody", Password)]
    [InlineData("Ayse", "")]
    [InlineData("Ayse", null)]
    [InlineData("not a name", Password)]
    [InlineData(null, Password)]
    public void A_wrong_password_and_a_username_nobody_has_get_the_same_answer(string? username, string? password)
    {
        Added(Add("Ayse"));

        Assert.IsType<SignInOutcome.WrongCredentials>(Accounts().SignIn(username, password));
    }

    [Fact]
    public void A_username_nobody_has_costs_one_password_check_at_the_cost_of_a_real_one()
    {
        PlayerAccount ayse = Added(Add("Ayse"));
        var checkedHashes = new List<string>();
        PlayerAccounts accounts = Accounts(check: (password, encodedHash) =>
        {
            checkedHashes.Add(encodedHash);
            return PasswordHasher.Verify(password, encodedHash);
        });

        accounts.SignIn("Ayse", "wrong password");
        accounts.SignIn("Nobody", "wrong password");

        // One check each. The unknown name's is against a hash that is not Ayse's and names the same cost, so the two
        // answers take the same work: how long a refusal took does not tell whether the username exists.
        Assert.Equal(2, checkedHashes.Count);
        Assert.Equal(ayse.PasswordHash, checkedHashes[0]);
        Assert.NotEqual(ayse.PasswordHash, checkedHashes[1]);
        Assert.Equal(CostOf(checkedHashes[0]), CostOf(checkedHashes[1]));
        Assert.Equal("1000", CostOf(checkedHashes[1]));
    }

    [Theory]
    [InlineData("not a name", Password)]
    [InlineData("Ayse", "")]
    [InlineData("Ayse", null)]
    public void What_cannot_be_a_username_or_a_password_is_refused_without_any_password_check(string? username, string? password)
    {
        Added(Add("Ayse"));
        int checks = 0;
        PlayerAccounts accounts = Accounts(check: (_, _) =>
        {
            checks++;
            return false;
        });

        Assert.IsType<SignInOutcome.WrongCredentials>(accounts.SignIn(username, password));

        Assert.Equal(0, checks);
    }

    [Fact]
    public void Repeated_failures_lock_the_username_for_a_while_even_against_the_right_password()
    {
        Added(Add("Ayse"));
        PlayerAccounts accounts = Accounts();

        for (int failure = 0; failure < SignInLockouts.MaxFailures; failure++)
        {
            Assert.IsType<SignInOutcome.WrongCredentials>(accounts.SignIn("Ayse", "a guess"));
        }

        SignInOutcome.LockedOut locked = Assert.IsType<SignInOutcome.LockedOut>(accounts.SignIn("Ayse", Password));
        Assert.Equal(SignInLockouts.LockoutTime, locked.RetryAfter);
        Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Warning);

        _clock.Advance(SignInLockouts.LockoutTime - TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.IsType<SignInOutcome.LockedOut>(accounts.SignIn("AYSE", Password)).RetryAfter);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<SignInOutcome.SignedIn>(accounts.SignIn("Ayse", Password));
    }

    [Fact]
    public void A_username_nobody_has_is_locked_just_like_a_real_one()
    {
        PlayerAccounts accounts = Accounts();

        for (int failure = 0; failure < SignInLockouts.MaxFailures; failure++)
        {
            Assert.IsType<SignInOutcome.WrongCredentials>(accounts.SignIn("Nobody", "a guess"));
        }

        Assert.IsType<SignInOutcome.LockedOut>(accounts.SignIn("nobody", "a guess"));
    }

    [Fact]
    public void A_sign_in_forgets_the_failures_before_it_and_old_failures_stop_counting()
    {
        Added(Add("Ayse"));
        PlayerAccounts accounts = Accounts();

        FailSignIns(accounts, SignInLockouts.MaxFailures - 1);
        Assert.IsType<SignInOutcome.SignedIn>(accounts.SignIn("Ayse", Password));
        FailSignIns(accounts, SignInLockouts.MaxFailures - 1);
        Assert.IsType<SignInOutcome.SignedIn>(accounts.SignIn("Ayse", Password));

        FailSignIns(accounts, SignInLockouts.MaxFailures - 1);
        _clock.Advance(SignInLockouts.FailureMemory + TimeSpan.FromSeconds(1));
        FailSignIns(accounts, 1); // the first of a new count, not the last of the old one
        Assert.IsType<SignInOutcome.SignedIn>(accounts.SignIn("Ayse", Password));
    }

    [Fact]
    public void The_lockouts_count_a_bounded_number_of_usernames()
    {
        var lockouts = new SignInLockouts(_clock, capacity: 2);
        var first = new Username("first");
        FailTimes(lockouts, first, SignInLockouts.MaxFailures - 1);
        _clock.Advance(TimeSpan.FromSeconds(1));
        FailTimes(lockouts, new Username("second"), 1);
        _clock.Advance(TimeSpan.FromSeconds(1));

        FailTimes(lockouts, new Username("third"), 1); // no room: the one that failed longest ago is forgotten

        Assert.False(lockouts.RecordFailure(first)); // counted from one again, so not locked
        Assert.Null(lockouts.LockedFor(first));
    }

    [Fact]
    public void A_sign_in_replaces_a_hash_made_at_a_lower_cost()
    {
        Added(Add("Ayse", iterations: 1_000));
        PlayerAccounts raised = Accounts(iterations: 2_000);

        SignInOutcome.SignedIn signedIn = Assert.IsType<SignInOutcome.SignedIn>(raised.SignIn("Ayse", Password));

        Assert.StartsWith("pbkdf2-sha256$2000$", signedIn.Account.PasswordHash);
        Assert.StartsWith("pbkdf2-sha256$2000$", Assert.Single(PlayerStore.ReadAccounts(Path.Combine(_folder.Path, "data"))).PasswordHash);
        Assert.IsType<SignInOutcome.SignedIn>(raised.SignIn("Ayse", Password));
    }

    private static PlayerAccount Added(AddPlayerOutcome outcome) => Assert.IsType<AddPlayerOutcome.Added>(outcome).Account;

    private static void FailSignIns(PlayerAccounts accounts, int times)
    {
        for (int failure = 0; failure < times; failure++)
        {
            Assert.IsType<SignInOutcome.WrongCredentials>(accounts.SignIn("Ayse", "a guess"));
        }
    }

    private static void FailTimes(SignInLockouts lockouts, Username username, int times)
    {
        for (int failure = 0; failure < times; failure++)
        {
            lockouts.RecordFailure(username);
        }
    }

    /// <summary>Adds a player to this test's store as the owner's tool does, with cheap hashes: the cost is the hasher's own test.</summary>
    private AddPlayerOutcome Add(string username, string? password = Password, int iterations = 1_000) =>
        PlayerAddition.Add(_store, new PasswordHasher(iterations), new Username(username), password, _clock.GetUtcNow());

    /// <summary>The cost an encoded hash names: its iteration count, as it is spelled.</summary>
    private static string CostOf(string encodedHash) => encodedHash.Split('$')[1];

    /// <summary>The sign-ins over this test's store, with cheap hashes; <paramref name="check"/> replaces the real password check.</summary>
    private PlayerAccounts Accounts(int iterations = 1_000, PasswordCheck? check = null) =>
        new(_store, new PasswordHasher(iterations), new SignInLockouts(_clock), _logger, check);
}
