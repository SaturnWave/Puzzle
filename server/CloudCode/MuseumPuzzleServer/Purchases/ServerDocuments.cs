using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MuseumPuzzle.Server.Players;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>A ledger or a receipt record the server reads is not one it writes; the message says what is wrong. Nothing is written over such a document.</summary>
    public sealed class ServerDocumentException : Exception
    {
        public ServerDocumentException(string message)
            : base(message)
        {
        }

        public ServerDocumentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// The texts of the documents this module keeps in Cloud Save, and the only place that knows their layout:
    /// the player's ledger (the Protected item "purchases", which the game reads with Purchases/PurchaseLedger, its
    /// layout pinned for both sides in tests/fixtures/cloud-code/ledger.json) and a receipt's record (the Private
    /// custom item "rcpt-…", which only this module reads). Times are UTC to the second ("2026-11-01T10:02:11Z"),
    /// days ISO dates.
    /// </summary>
    public static class ServerDocuments
    {
        private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        private const string DayFormat = "yyyy-MM-dd";

        private static readonly JsonSerializerOptions Layout = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string TextOf(Ledger ledger) => JsonSerializer.Serialize(
            new LedgerDocument
            {
                Next = ledger.Next.Value,
                Items = ledger.Entries.Select(LineOf).ToList(),
                RefundsCheckedAt = ledger.RefundsCheckedAt is DateTimeOffset at ? InstantText(at) : null,
            },
            Layout);

        /// <exception cref="ServerDocumentException">The text is no ledger this module writes.</exception>
        public static Ledger LedgerFrom(string text)
        {
            LedgerDocument document = Parsed<LedgerDocument>(text, "ledger");
            try
            {
                return new Ledger(
                    (document.Items ?? new List<LedgerLine>()).Select(EntryOf),
                    new LedgerNumber(document.Next),
                    document.RefundsCheckedAt is string at ? InstantOf(at, "refundsCheckedAt") : null);
            }
            catch (ArgumentException contradiction)
            {
                throw new ServerDocumentException($"The ledger contradicts itself: {contradiction.Message}", contradiction);
            }
        }

        public static string TextOf(ReceiptRecord record) => JsonSerializer.Serialize(
            new ReceiptDocument
            {
                Store = PurchaseStoreWords.WordOf(record.Store),
                Product = record.Product,
                At = InstantText(record.At),
                State = WordOf(record.State),
                Test = record.IsTest,
                Claims = record.Claims.Select(claim => new ClaimLine { Player = claim.Player.Value, Seq = claim.Number.Value }).ToList(),
                ForgottenOn = record.ForgottenOn?.ToString(DayFormat, CultureInfo.InvariantCulture),
            },
            Layout);

        /// <exception cref="ServerDocumentException">The text is no receipt record this module writes.</exception>
        public static ReceiptRecord ReceiptFrom(string text)
        {
            ReceiptDocument document = Parsed<ReceiptDocument>(text, "receipt record");
            try
            {
                return new ReceiptRecord(
                    PurchaseStoreWords.StoreNamed(document.Store) ?? throw new ServerDocumentException($"A receipt record names the store '{document.Store}', which the game does not sell through."),
                    document.Product ?? string.Empty,
                    InstantOf(document.At, "at"),
                    StateOf(document.State),
                    document.Test,
                    (document.Claims ?? new List<ClaimLine>()).Select(line => new ReceiptClaim(new PlayerId(line.Player ?? string.Empty), new LedgerNumber(line.Seq))),
                    document.ForgottenOn is string day ? DayOf(day) : null);
            }
            catch (ArgumentException contradiction)
            {
                throw new ServerDocumentException($"The receipt record contradicts itself: {contradiction.Message}", contradiction);
            }
        }

        public static string InstantText(DateTimeOffset at) => at.ToUniversalTime().ToString(InstantFormat, CultureInfo.InvariantCulture);

        public static string WordOf(LedgerState state) => state switch
        {
            LedgerState.Valid => "valid",
            LedgerState.Voided => "voided",
            LedgerState.Refunded => "refunded",
            LedgerState.Invalid => "invalid",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "A state the ledger does not write."),
        };

        private static LedgerState StateOf(string? word) => word switch
        {
            "valid" => LedgerState.Valid,
            "voided" => LedgerState.Voided,
            "refunded" => LedgerState.Refunded,
            "invalid" => LedgerState.Invalid,
            _ => throw new ServerDocumentException($"'{word}' is no state the ledger writes."),
        };

        private static LedgerLine LineOf(LedgerEntry entry) => new LedgerLine
        {
            Seq = entry.Number.Value,
            Store = PurchaseStoreWords.WordOf(entry.Store),
            Product = entry.Product,
            Order = entry.Order,
            At = InstantText(entry.At),
            State = WordOf(entry.State),
            Test = entry.IsTest,
            Receipt = entry.Receipt.Value,
        };

        private static LedgerEntry EntryOf(LedgerLine line) => new LedgerEntry(
            new LedgerNumber(line.Seq),
            PurchaseStoreWords.StoreNamed(line.Store) ?? throw new ServerDocumentException($"The payment #{line.Seq} names the store '{line.Store}', which the game does not sell through."),
            line.Product ?? string.Empty,
            line.Order ?? string.Empty,
            InstantOf(line.At, $"items #{line.Seq}.at"),
            StateOf(line.State),
            line.Test,
            ReceiptId.Parse(line.Receipt) ?? throw new ServerDocumentException($"The payment #{line.Seq} names no receipt the server writes ('{line.Receipt}')."));

        private static T Parsed<T>(string text, string what)
            where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(text, Layout) ?? throw new ServerDocumentException($"The {what} is empty.");
            }
            catch (JsonException unreadable)
            {
                throw new ServerDocumentException($"The {what} is not the JSON this module writes: {unreadable.Message}", unreadable);
            }
        }

        private static DateTimeOffset InstantOf(string? text, string field) =>
            DateTimeOffset.TryParseExact(text, InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset at)
                ? at
                : throw new ServerDocumentException($"{field} is '{text}', not a UTC time to the second.");

        private static DateOnly DayOf(string text) =>
            DateOnly.TryParseExact(text, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day)
                ? day
                : throw new ServerDocumentException($"forgottenOn is '{text}', not a day.");

        private sealed class LedgerDocument
        {
            [JsonPropertyName("next")]
            public long Next { get; set; }

            [JsonPropertyName("items")]
            public List<LedgerLine>? Items { get; set; }

            /// <summary>When the server last asked Apple for refunds; the game does not read it.</summary>
            [JsonPropertyName("refundsCheckedAt")]
            public string? RefundsCheckedAt { get; set; }
        }

        private sealed class LedgerLine
        {
            [JsonPropertyName("seq")]
            public long Seq { get; set; }

            [JsonPropertyName("store")]
            public string? Store { get; set; }

            [JsonPropertyName("product")]
            public string? Product { get; set; }

            [JsonPropertyName("order")]
            public string? Order { get; set; }

            [JsonPropertyName("at")]
            public string? At { get; set; }

            [JsonPropertyName("state")]
            public string? State { get; set; }

            [JsonPropertyName("test")]
            public bool Test { get; set; }

            /// <summary>The receipt's id in the registry; the game does not read it.</summary>
            [JsonPropertyName("receipt")]
            public string? Receipt { get; set; }
        }

        private sealed class ReceiptDocument
        {
            [JsonPropertyName("store")]
            public string? Store { get; set; }

            [JsonPropertyName("product")]
            public string? Product { get; set; }

            [JsonPropertyName("at")]
            public string? At { get; set; }

            [JsonPropertyName("state")]
            public string? State { get; set; }

            [JsonPropertyName("test")]
            public bool Test { get; set; }

            [JsonPropertyName("claims")]
            public List<ClaimLine>? Claims { get; set; }

            [JsonPropertyName("forgottenOn")]
            public string? ForgottenOn { get; set; }
        }

        private sealed class ClaimLine
        {
            [JsonPropertyName("player")]
            public string? Player { get; set; }

            [JsonPropertyName("seq")]
            public long Seq { get; set; }
        }
    }
}
