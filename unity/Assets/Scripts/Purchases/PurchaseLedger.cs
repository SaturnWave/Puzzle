#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Puzzle.Client.Purchases
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the document's layout: Unity's JSON reader maps fields, not properties

    /// <summary>What the server's ledger says of a payment now (docs/research/server-v1.md §3.2). A closed set.</summary>
    public enum LedgerState
    {
        /// <summary>Verified with the store, and neither refunded nor cancelled since.</summary>
        Valid,

        /// <summary>Google Play cancelled or refunded it (the daily look at Google's list of voided purchases).</summary>
        Voided,

        /// <summary>Apple refunded it (the player's refund history, looked at once a day).</summary>
        Refunded,

        /// <summary>The store did not confirm it, or it was another player's.</summary>
        Invalid,
    }

    /// <summary>One verified payment in the player's ledger: its number, what was bought, the store's order, when, and what the ledger says of it now.</summary>
    public sealed class LedgerEntry
    {
        /// <exception cref="ArgumentException">No product.</exception>
        public LedgerEntry(LedgerNumber number, string product, string order, DateTimeOffset at, LedgerState state, bool isTest)
        {
            Number = number;
            Product = string.IsNullOrEmpty(product) ? throw new ArgumentException("A payment is for a product.", nameof(product)) : product;
            Order = order ?? string.Empty;
            At = at;
            State = state;
            IsTest = isTest;
        }

        public LedgerNumber Number { get; }

        /// <summary>The end of the product's id in the stores ("coins.pouch", Puzzle.Meta.StoreProduct.IdSuffix).</summary>
        public string Product { get; }

        /// <summary>The store's order id ("GPA.3312-…"), for support; empty when the store gave none.</summary>
        public string Order { get; }

        public DateTimeOffset At { get; }

        public LedgerState State { get; }

        /// <summary>A test purchase: Google's licence testers, Apple's sandbox.</summary>
        public bool IsTest { get; }

        public override string ToString() => $"#{Number} {Product} ({State}{(IsTest ? ", test" : string.Empty)})";
    }

    /// <summary>
    /// The player's ledger of verified payments as the game reads it: the Cloud Save item "purchases" of the
    /// Protected class, which the player can read and only the game's server writes (docs/research/server-v1.md §3.2,
    /// §4.5). The game reads it beside the save, and needs it for two things: to count once a payment that two devices
    /// both delivered (Cloud/ProgressMerge, by the numbers each kept), and to find the verified payments no device
    /// delivered (<see cref="NotDelivered"/>), which the purchases' stage gives back to the player.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class PurchaseLedger
    {
        private readonly Dictionary<LedgerNumber, LedgerEntry> _byNumber;

        /// <exception cref="ArgumentException">A number twice.</exception>
        public PurchaseLedger(IEnumerable<LedgerEntry> entries)
        {
            Entries = (entries ?? throw new ArgumentNullException(nameof(entries))).OrderBy(entry => entry.Number).ToArray();
            _byNumber = new Dictionary<LedgerNumber, LedgerEntry>();
            foreach (LedgerEntry entry in Entries)
            {
                if (_byNumber.ContainsKey(entry.Number))
                {
                    throw new ArgumentException($"The ledger names the payment #{entry.Number} twice.", nameof(entries));
                }

                _byNumber[entry.Number] = entry;
            }
        }

        /// <summary>A ledger with no payment: a player whose payments were never verified, or one whose ledger was not there to read.</summary>
        public static PurchaseLedger None { get; } = new PurchaseLedger(Array.Empty<LedgerEntry>());

        /// <summary>Every payment, by its number.</summary>
        public IReadOnlyList<LedgerEntry> Entries { get; }

        /// <summary>The payment with this number; nothing when the ledger has none such (or set its line aside).</summary>
        public LedgerEntry? Find(LedgerNumber number) => _byNumber.TryGetValue(number, out LedgerEntry? entry) ? entry : null;

        /// <summary>
        /// The valid payments whose number the progress does not hold: verified, but delivered on no device whose
        /// progress reached this one (the device that paid was lost before it saved to the cloud). A payment refunded
        /// or cancelled, or one the store did not confirm, is never among them.
        /// </summary>
        public IReadOnlyList<LedgerEntry> NotDelivered(PurchasesMade made) =>
            Entries.Where(entry => entry.State == LedgerState.Valid && !made.LedgerDelivered.Contains(entry.Number)).ToArray();

        /// <summary>
        /// What a document that a JSON reader filled holds, line by line. The server writes the ledger, so a line it
        /// could not have written is a fault: it is set aside with a word on why and the rest is read, and a merge that
        /// needs that payment says so (Cloud/ProgressMerge) instead of guessing what it held.
        /// </summary>
        /// <param name="document">Nothing when the item's text held no document.</param>
        public static PurchaseLedgerReading Read(PurchaseLedgerDocument? document)
        {
            if (document == null)
            {
                return new PurchaseLedgerReading(None, new[] { "the ledger is not a document" });
            }

            var entries = new List<LedgerEntry>();
            var numbers = new HashSet<LedgerNumber>();
            var setAside = new List<string>();
            foreach (LedgerEntryDocument? line in document.items ?? Array.Empty<LedgerEntryDocument>())
            {
                if (!(line?.Entry() is LedgerEntry entry))
                {
                    setAside.Add($"a ledger line no server writes ({(line == null ? "no line" : $"#{line.seq} {line.product} {line.state} at '{line.at}'")})");
                }
                else if (!numbers.Add(entry.Number))
                {
                    setAside.Add($"the payment #{entry.Number} named again: the first line is kept");
                }
                else
                {
                    entries.Add(entry);
                }
            }

            return new PurchaseLedgerReading(new PurchaseLedger(entries), setAside);
        }
    }

    /// <summary>What a ledger's document was read as: the payments it holds, and the lines set aside on the way, each with a word on why.</summary>
    public sealed class PurchaseLedgerReading
    {
        public PurchaseLedgerReading(PurchaseLedger ledger, IReadOnlyList<string> setAside)
        {
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            SetAside = setAside ?? throw new ArgumentNullException(nameof(setAside));
        }

        public PurchaseLedger Ledger { get; }

        public IReadOnlyList<string> SetAside { get; }
    }

    /// <summary>
    /// The ledger as the server writes it (docs/research/server-v1.md §3.2):
    /// <c>{"next":4,"items":[{"seq":1,"store":"GooglePlay","product":"coins.pouch","order":"GPA.3312-…","at":"2026-11-01T10:02:11Z","state":"valid","test":false}]}</c>.
    /// The game reads what it needs of it; the store and the next number are the server's business.
    /// </summary>
    [Serializable]
    public sealed class PurchaseLedgerDocument
    {
        public long next;
        public LedgerEntryDocument[] items = Array.Empty<LedgerEntryDocument>();
    }

    /// <summary>One payment of the ledger, as the server writes it.</summary>
    [Serializable]
    public sealed class LedgerEntryDocument
    {
        public long seq;
        public string store = string.Empty;
        public string product = string.Empty;
        public string order = string.Empty;

        /// <summary>When the payment was verified, in UTC: "2026-11-01T10:02:11Z".</summary>
        public string at = string.Empty;

        /// <summary>"valid", "voided", "refunded" or "invalid" (<see cref="LedgerState"/>).</summary>
        public string state = string.Empty;

        public bool test;

        /// <summary>The payment this line says; nothing for a line no server writes: no number from 1, no product, a state or a time it does not write.</summary>
        public LedgerEntry? Entry()
        {
            bool isAnInstant = DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset when);
            return seq >= 1 && !string.IsNullOrEmpty(product) && isAnInstant && StateOf(state) is LedgerState known
                ? new LedgerEntry(new LedgerNumber(seq), product, order, when, known, test)
                : null;
        }

        private static LedgerState? StateOf(string? word) => word switch
        {
            "valid" => LedgerState.Valid,
            "voided" => LedgerState.Voided,
            "refunded" => LedgerState.Refunded,
            "invalid" => LedgerState.Invalid,
            _ => null,
        };
    }
}
