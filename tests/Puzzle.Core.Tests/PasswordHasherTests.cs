using Puzzle.Tooling.Players;

namespace Puzzle.Core.Tests;

public class PasswordHasherTests
{
    /// <summary>Cheap hashes for the tests that are not about the cost.</summary>
    private static readonly PasswordHasher Quick = new(iterations: 1_000);

    [Fact]
    public void The_right_password_verifies_and_a_wrong_one_does_not()
    {
        string hash = Quick.Hash("correct horse");

        Assert.True(PasswordHasher.Verify("correct horse", hash));
        Assert.False(PasswordHasher.Verify("correct horsf", hash));
        Assert.False(PasswordHasher.Verify("Correct horse", hash));
        Assert.False(PasswordHasher.Verify(string.Empty, hash));
    }

    [Fact]
    public void The_hash_never_holds_the_password()
    {
        const string Password = "kedi-kopek-2026";

        string hash = Quick.Hash(Password);

        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash);
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ_and_both_verify()
    {
        string first = Quick.Hash("same password");
        string second = Quick.Hash("same password");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("same password", first));
        Assert.True(PasswordHasher.Verify("same password", second));
    }

    [Fact]
    public void The_servers_hasher_spends_at_least_600_000_iterations_and_the_hash_says_so()
    {
        var server = new PasswordHasher();

        string hash = server.Hash("correct horse");

        Assert.True(PasswordHasher.DefaultIterations >= 600_000);
        Assert.StartsWith($"pbkdf2-sha256${PasswordHasher.DefaultIterations}$", hash);
        Assert.True(PasswordHasher.Verify("correct horse", hash));
        Assert.False(server.NeedsRehash(hash));
    }

    [Fact]
    public void A_hash_made_at_a_lower_cost_still_verifies_and_asks_to_be_replaced()
    {
        var raised = new PasswordHasher(iterations: 2_000);
        string old = Quick.Hash("correct horse");

        Assert.True(PasswordHasher.Verify("correct horse", old));
        Assert.False(PasswordHasher.Verify("wrong", old));
        Assert.True(raised.NeedsRehash(old));
        Assert.False(raised.NeedsRehash(raised.Hash("correct horse")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("correct horse")]
    [InlineData("pbkdf2-sha256$1000$c2FsdA==")]
    [InlineData("md5$1000$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$0$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$999999999$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$1000$not base64$aGFzaA==")]
    [InlineData("pbkdf2-sha256$1000$c2FsdA==$")]
    public void A_malformed_hash_verifies_nothing(string malformed)
    {
        Assert.False(PasswordHasher.Verify("correct horse", malformed));
        Assert.True(Quick.NeedsRehash(malformed));
    }

    [Fact]
    public void The_decoy_hash_costs_what_a_real_hash_of_its_hasher_costs_and_is_each_hashers_own()
    {
        var server = new PasswordHasher(iterations: 3_000);

        Assert.StartsWith("pbkdf2-sha256$1000$", Quick.DecoyHash);
        Assert.StartsWith("pbkdf2-sha256$3000$", server.DecoyHash);
        Assert.False(server.NeedsRehash(server.DecoyHash)); // well-formed, at the hasher's own cost: verifying against it is a full verification
        Assert.False(PasswordHasher.Verify("whatever was typed", server.DecoyHash));
        Assert.False(PasswordHasher.Verify(string.Empty, server.DecoyHash));
        Assert.NotEqual(server.DecoyHash, new PasswordHasher(iterations: 3_000).DecoyHash);
    }
}
