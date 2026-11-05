using System.Security.Cryptography;

namespace Puzzle.Tooling.Players;

/// <summary>
/// What a player's sign-in cookie remembers of the account it was made for. The account gets a new stamp when the owner
/// gives it a new password (<c>puzzle players password</c>), and a cookie that remembers another stamp signs nobody in
/// any more. A stamp is no secret: the cookie is sealed, so a stamp only has to differ from the ones before it.
/// </summary>
public sealed record SignInStamp
{
    public const int MaxLength = 64;

    /// <exception cref="ArgumentException"><paramref name="value"/> is not 1..<see cref="MaxLength"/> ASCII letters or digits.</exception>
    public SignInStamp(string value)
    {
        Value = value is { Length: >= 1 and <= MaxLength } && value.All(char.IsAsciiLetterOrDigit)
            ? value
            : throw new ArgumentException($"A sign-in stamp is 1..{MaxLength} ASCII letters or digits.", nameof(value));
    }

    /// <summary>
    /// The stamp of an account whose entry in the store names none: it was written before accounts had stamps. It is
    /// replaced like any other when the password changes.
    /// </summary>
    public static SignInStamp First { get; } = new("first");

    public string Value { get; }

    /// <summary>A stamp no account has had: 128 random bits as hex.</summary>
    public static SignInStamp New() => new(RandomNumberGenerator.GetHexString(32, lowercase: true));

    public override string ToString() => Value;
}
