using System;
using System.Security.Cryptography;
using System.Text;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>
    /// The name of a receipt in the server's registry: "rcpt-" and the first 40 hexadecimal digits of the SHA-256 of the
    /// store's name, a colon and the store's own name for the payment (Google's purchase token, Apple's transaction id;
    /// docs/research/server-v1.md §3.2). It is also the id of the receipt's Cloud Save custom item. The token itself is
    /// never kept: the digest is enough to find a payment again, in a second question and in Google's list of voided
    /// purchases, and it is the trace that stays after a player's data is deleted.
    /// </summary>
    public readonly struct ReceiptId : IEquatable<ReceiptId>
    {
        public const string Prefix = "rcpt-";

        private const int DigestDigits = 40;

        private ReceiptId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        /// <exception cref="ArgumentException">No token.</exception>
        public static ReceiptId Of(PurchaseStore store, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("A receipt is named by the store's token for the payment.", nameof(token));
            }

            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{PurchaseStoreWords.WordOf(store)}:{token}"));
            return new ReceiptId(string.Concat(Prefix, Convert.ToHexString(digest).ToLowerInvariant().AsSpan(0, DigestDigits)));
        }

        /// <summary>The receipt id this text is; nothing for a text that is not one (a line no server of the game writes).</summary>
        public static ReceiptId? Parse(string? text)
        {
            if (text == null || text.Length != Prefix.Length + DigestDigits || !text.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return null;
            }

            for (int index = Prefix.Length; index < text.Length; index++)
            {
                if (!char.IsAsciiHexDigitLower(text[index]) && !char.IsAsciiDigit(text[index]))
                {
                    return null;
                }
            }

            return new ReceiptId(text);
        }

        public bool Equals(ReceiptId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is ReceiptId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(ReceiptId left, ReceiptId right) => left.Equals(right);

        public static bool operator !=(ReceiptId left, ReceiptId right) => !left.Equals(right);
    }
}
