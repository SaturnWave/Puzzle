using System;
using System.Diagnostics.CodeAnalysis;

namespace Puzzle.Core.Recording
{
    /// <summary>
    /// A random id a device keeps for itself, the only identity in an attempt record: no name, e-mail or advertising
    /// id (docs/GAME_DESIGN.md §20). The allowed characters (letters, digits, '-' and '_') fit hex, UUID and base64url
    /// tokens and leave no room for an e-mail address or a name with spaces.
    /// </summary>
    public sealed record DeviceId
    {
        public const int MinLength = 8;
        public const int MaxLength = 64;

        /// <exception cref="ArgumentException"><paramref name="value"/> is not a well-formed device id.</exception>
        public DeviceId(string value)
        {
            Value = IsWellFormed(value)
                ? value
                : throw new ArgumentException($"A device id is {MinLength}..{MaxLength} letters, digits, '-' or '_'.", nameof(value));
        }

        public string Value { get; }

        /// <summary>The device id <paramref name="text"/> spells; false when it is missing or malformed.</summary>
        public static bool TryCreate(string? text, [NotNullWhen(true)] out DeviceId? id)
        {
            id = IsWellFormed(text) ? new DeviceId(text) : null;
            return id is not null;
        }

        public override string ToString() => Value;

        private static bool IsWellFormed([NotNullWhen(true)] string? text)
        {
            if (text is null || text.Length < MinLength || text.Length > MaxLength)
            {
                return false;
            }

            foreach (char c in text)
            {
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
