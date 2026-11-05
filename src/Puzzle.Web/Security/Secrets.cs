using System.Security.Cryptography;
using System.Text;

namespace Puzzle.Web.Security;

/// <summary>Compares a secret the owner configured (the owner key) with what a request sent.</summary>
internal static class Secrets
{
    /// <summary>
    /// Whether <paramref name="given"/> is the secret <paramref name="expected"/>, compared in constant time. Both are
    /// hashed first, so not even their lengths show in how long the comparison takes.
    /// </summary>
    public static bool AreSame(string expected, string given) => CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
        SHA256.HashData(Encoding.UTF8.GetBytes(given)));
}
