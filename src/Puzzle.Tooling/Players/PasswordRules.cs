using System.Diagnostics.CodeAnalysis;

namespace Puzzle.Tooling.Players;

/// <summary>What a player's password must be.</summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    /// <summary>Longer than any password a person types, and it keeps the work of one sign-in bounded.</summary>
    public const int MaxLength = 128;

    /// <summary>What the owner's tool takes as a password, for a new account and for a changed one.</summary>
    public static bool Allows([NotNullWhen(true)] string? password) => password is { Length: >= MinLength and <= MaxLength };

    /// <summary>
    /// What a sign-in takes at all: not empty, and no longer than <see cref="MaxLength"/>. The password of a ready-made
    /// account of the public test (<see cref="ReadyMadePlayer"/>) need be no more than that: the owner chose a short
    /// one for them, so <see cref="MinLength"/> does not hold there — there alone, and nothing else is let go.
    /// </summary>
    public static bool CanBeSignedInWith([NotNullWhen(true)] string? password) => password is { Length: > 0 and <= MaxLength };
}
