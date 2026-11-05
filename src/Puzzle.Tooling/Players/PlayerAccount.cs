using System.Security.Cryptography;
using Puzzle.Core.Recording;

namespace Puzzle.Tooling.Players;

/// <summary>
/// One player of the public mode. The username is what the player and the owner see; the attempt records carry only
/// <see cref="PlayerId"/>, a random id with nothing personal in it, in their device field. This account is the only
/// place that ties the two together.
/// </summary>
public sealed record PlayerAccount
{
    /// <param name="passwordHash">The password as <see cref="PasswordHasher"/> encodes it; never the password itself.</param>
    /// <param name="signInStamp">What the player's sign-in cookies must remember to stay valid.</param>
    /// <exception cref="ArgumentException">A part is missing.</exception>
    public PlayerAccount(Username username, DeviceId playerId, string passwordHash, SignInStamp signInStamp, DateTimeOffset createdUtc, PlayerProgress progress)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash)
            ? throw new ArgumentException("A password hash is required.", nameof(passwordHash))
            : passwordHash;
        SignInStamp = signInStamp ?? throw new ArgumentNullException(nameof(signInStamp));
        CreatedUtc = createdUtc.ToUniversalTime();
        Progress = progress ?? throw new ArgumentNullException(nameof(progress));
    }

    public Username Username { get; }

    /// <summary>The pseudonymous id the player's attempt records carry as their device id.</summary>
    public DeviceId PlayerId { get; }

    public string PasswordHash { get; }

    /// <summary>What a sign-in cookie of this player must remember; a new password brings a new stamp, which ends every sign-in before it.</summary>
    public SignInStamp SignInStamp { get; }

    public DateTimeOffset CreatedUtc { get; }

    public PlayerProgress Progress { get; }

    /// <summary>A new pseudonymous player id: 128 random bits as hex, the shape of the lab's device ids.</summary>
    public static DeviceId NewPlayerId() => new(RandomNumberGenerator.GetHexString(32, lowercase: true));

    public PlayerAccount WithProgress(PlayerProgress progress) => new(Username, PlayerId, PasswordHash, SignInStamp, CreatedUtc, progress);

    /// <summary>The same password hashed again (at a higher cost): the player's sign-ins stay as they are.</summary>
    public PlayerAccount WithPasswordHash(string passwordHash) => new(Username, PlayerId, passwordHash, SignInStamp, CreatedUtc, Progress);

    /// <summary>Another password: its hash, and a new stamp, so no sign-in made with the old password is valid any more.</summary>
    public PlayerAccount WithNewPassword(string passwordHash) => new(Username, PlayerId, passwordHash, SignInStamp.New(), CreatedUtc, Progress);

    /// <summary>Only the username: an account written to a log by accident must not spell out its password hash.</summary>
    public override string ToString() => Username.Value;
}
