using System;

namespace MuseumPuzzle.Server.Players
{
    /// <summary>
    /// A player's Unity Authentication id, as Cloud Code hands it to a function the player calls
    /// (<c>IExecutionContext.PlayerId</c>). A type of its own, so that a player id never changes places with a receipt
    /// id, a store's token or a product id: they are all strings on the wire.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        /// <exception cref="ArgumentException">An empty id: Unity never hands one out.</exception>
        public PlayerId(string value)
        {
            Value = string.IsNullOrEmpty(value) ? throw new ArgumentException("A player id is not empty.", nameof(value)) : value;
        }

        public string Value { get; }

        public bool Equals(PlayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is PlayerId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value;

        public static bool operator ==(PlayerId left, PlayerId right) => left.Equals(right);

        public static bool operator !=(PlayerId left, PlayerId right) => !left.Equals(right);
    }
}
