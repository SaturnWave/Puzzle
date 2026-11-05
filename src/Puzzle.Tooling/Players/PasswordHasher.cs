using System.Globalization;
using System.Security.Cryptography;

namespace Puzzle.Tooling.Players;

/// <summary>
/// Hashes passwords with PBKDF2-HMAC-SHA256 and a random salt of its own for every hash. The encoded form is
/// <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;salt&gt;$&lt;hash&gt;</c> (salt and hash in base64): it names its own cost, so the cost can
/// be raised later. Hashes made at a lower cost still verify, and <see cref="NeedsRehash"/> picks them out for
/// replacement at the player's next sign-in.
/// </summary>
public sealed class PasswordHasher
{
    /// <summary>The cost new hashes are made at: OWASP's floor for PBKDF2-HMAC-SHA256.</summary>
    public const int DefaultIterations = 600_000;

    private const string Algorithm = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>A stored hash naming a cost above this is refused unverified: a garbled store must not stall the server.</summary>
    private const int MaxIterations = 20_000_000;

    private readonly int _iterations;

    /// <param name="iterations">The cost of new hashes. The server always takes the default; only tests of other things pay less.</param>
    public PasswordHasher(int iterations = DefaultIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(iterations, MaxIterations);
        _iterations = iterations;

        // Made now, not at the first sign-in that needs it: that one must not take twice as long as those after it.
        DecoyHash = Hash(RandomNumberGenerator.GetHexString(32));
    }

    /// <summary>
    /// An encoded hash, at this hasher's cost, of a random password nobody knows. A sign-in that names no account is
    /// verified against it, so it does the work of a real one: how long the answer took must not tell whether the
    /// username exists.
    /// </summary>
    public string DecoyHash { get; }

    /// <summary>A new encoded hash of <paramref name="password"/>; two hashes of one password differ, by their salts.</summary>
    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, HashBytes);
        return string.Join('$', Algorithm, _iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Whether <paramref name="password"/> is the one <paramref name="encoded"/> was made from; false too when the hash
    /// is malformed. The hash names its own cost, so no hasher is needed to check it.
    /// </summary>
    public static bool Verify(string password, string encoded)
    {
        if (!Encoded.TryParse(encoded, out Encoded? stored))
        {
            return false;
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, stored.Salt, stored.Iterations, HashAlgorithmName.SHA256, stored.Hash.Length);
        return CryptographicOperations.FixedTimeEquals(actual, stored.Hash);
    }

    /// <summary>Whether <paramref name="encoded"/> was made at a lower cost than this hasher's, or cannot be read at all.</summary>
    public bool NeedsRehash(string encoded) => !Encoded.TryParse(encoded, out Encoded? stored) || stored.Iterations < _iterations;

    /// <summary>The parts of an encoded hash.</summary>
    private sealed record Encoded(int Iterations, byte[] Salt, byte[] Hash)
    {
        public static bool TryParse(string text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Encoded? encoded)
        {
            encoded = null;
            string[] parts = text.Split('$');
            if (parts.Length != 4 || parts[0] != Algorithm)
            {
                return false;
            }

            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) || iterations < 1 || iterations > MaxIterations)
            {
                return false;
            }

            if (!TryFromBase64(parts[2], out byte[] salt) || !TryFromBase64(parts[3], out byte[] hash))
            {
                return false;
            }

            encoded = new Encoded(iterations, salt, hash);
            return true;
        }

        /// <summary>The bytes <paramref name="text"/> encodes; false when it is not base64 or encodes nothing.</summary>
        private static bool TryFromBase64(string text, out byte[] bytes)
        {
            bytes = new byte[text.Length];
            if (!Convert.TryFromBase64String(text, bytes, out int written) || written == 0)
            {
                return false;
            }

            bytes = bytes[..written];
            return true;
        }
    }
}
