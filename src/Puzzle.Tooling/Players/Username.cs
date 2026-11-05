using System.Diagnostics.CodeAnalysis;

namespace Puzzle.Tooling.Players;

/// <summary>
/// The name a player signs in with: 3..20 ASCII letters, digits, '_' or '-'. Names that differ only in case are the
/// same name; ASCII alone keeps that comparison free of locale rules (the Turkish dotted and dotless i).
/// </summary>
public sealed record Username
{
    public const int MinLength = 3;
    public const int MaxLength = 20;

    /// <exception cref="ArgumentException"><paramref name="value"/> is not a well-formed username.</exception>
    public Username(string value)
    {
        Value = IsWellFormed(value)
            ? value
            : throw new ArgumentException($"A username is {MinLength}..{MaxLength} ASCII letters, digits, '_' or '-'.", nameof(value));
    }

    /// <summary>The name as the owner wrote it when adding the player.</summary>
    public string Value { get; }

    /// <summary>The username <paramref name="text"/> spells; false when it is missing or malformed.</summary>
    public static bool TryCreate(string? text, [NotNullWhen(true)] out Username? username)
    {
        username = IsWellFormed(text) ? new Username(text) : null;
        return username is not null;
    }

    public bool Equals(Username? other) => other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;

    private static bool IsWellFormed([NotNullWhen(true)] string? text) =>
        text is { Length: >= MinLength and <= MaxLength } && text.All(IsAllowed);

    private static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-';
}
