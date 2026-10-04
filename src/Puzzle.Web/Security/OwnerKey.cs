using System.Security.Cryptography;
using System.Text;

namespace Puzzle.Web.Security;

/// <summary>
/// The key that opens the lab of a public server to its owner: a long random string from configuration, never from the
/// repository.
/// </summary>
internal sealed class OwnerKey
{
    /// <summary>Shorter keys are refused at startup: the key is all that stands between the internet and the lab.</summary>
    public const int MinLength = 16;

    private readonly string _key;

    /// <exception cref="ArgumentException"><paramref name="key"/> is shorter than <see cref="MinLength"/>.</exception>
    public OwnerKey(string key)
    {
        _key = key.Length >= MinLength
            ? key
            : throw new ArgumentException($"An owner key has at least {MinLength} characters.", nameof(key));
        CookieStamp = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("puzzle-owner-cookie:" + key)));
    }

    /// <summary>
    /// What an owner cookie remembers of the key it was given for: a hash, not the key. A cookie whose stamp is not this
    /// one was made for an earlier key and no longer opens anything.
    /// </summary>
    public string CookieStamp { get; }

    public bool Matches(string? given) => given is not null && Secrets.AreSame(_key, given.Trim());
}
