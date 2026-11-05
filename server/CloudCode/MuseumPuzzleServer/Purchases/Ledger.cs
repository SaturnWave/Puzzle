using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>What the ledger says of a payment now (docs/research/server-v1.md §3.2). A closed set; the game reads the same four words.</summary>
    public enum LedgerState
    {
        /// <summary>The store confirmed it, and neither refunded nor cancelled it since.</summary>
        Valid,

        /// <summary>Google Play cancelled or refunded it (the daily look at Google's list of voided purchases).</summary>
        Voided,

        /// <summary>Apple refunded it (the player's refund history, looked at once a day).</summary>
        Refunded,

        /// <summary>The store did not confirm a payment the game had already delivered before it could ask (§4.8): kept for the record, never delivered.</summary>
        Invalid,
    }

    /// <summary>
    /// The number the server gives a payment in the player's ledger: 1 for the first, one more for each after it, never
    /// given twice. The game keeps the numbers of the payments it delivered (Purchases/LedgerNumbers on the client),
    /// which is how two devices deliver one payment once.
    /// </summary>
    public readonly struct LedgerNumber : IEquatable<LedgerNumber>, IComparable<LedgerNumber>
    {
        /// <exception cref="ArgumentOutOfRangeException">Below 1: the ledger counts from 1.</exception>
        public LedgerNumber(long value)
        {
            Value = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The ledger numbers its payments from 1.");
        }

        public static LedgerNumber First { get; } = new LedgerNumber(1);

        public long Value { get; }

        public LedgerNumber Following => new LedgerNumber(Value + 1);

        public int CompareTo(LedgerNumber other) => Value.CompareTo(other.Value);

        public bool Equals(LedgerNumber other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is LedgerNumber other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

        public static bool operator ==(LedgerNumber left, LedgerNumber right) => left.Equals(right);

        public static bool operator !=(LedgerNumber left, LedgerNumber right) => !left.Equals(right);

        public static bool operator <(LedgerNumber left, LedgerNumber right) => left.Value < right.Value;

        public static bool operator >(LedgerNumber left, LedgerNumber right) => left.Value > right.Value;

        public static bool operator <=(LedgerNumber left, LedgerNumber right) => left.Value <= right.Value;

        public static bool operator >=(LedgerNumber left, LedgerNumber right) => left.Value >= right.Value;
    }

    /// <summary>
    /// One payment of a player's ledger: its number, the store, the product, the store's order, when the server
    /// verified it, what the ledger says of it now, whether it was a test, and the receipt it was verified from.
    /// </summary>
    public sealed class LedgerEntry
    {
        /// <exception cref="ArgumentException">No product.</exception>
        public LedgerEntry(LedgerNumber number, PurchaseStore store, string product, string order, DateTimeOffset at, LedgerState state, bool isTest, ReceiptId receipt)
        {
            Number = number;
            Store = store;
            Product = string.IsNullOrEmpty(product) ? throw new ArgumentException("A payment is for a product.", nameof(product)) : product;
            Order = order ?? string.Empty;
            At = at;
            State = state;
            IsTest = isTest;
            Receipt = receipt;
        }

        public LedgerNumber Number { get; }

        public PurchaseStore Store { get; }

        /// <summary>The end of the product's id ("coins.pouch").</summary>
        public string Product { get; }

        /// <summary>
        /// The store's own name for the payment, for support and for the refund checks: Google's order id ("GPA.3312-…";
        /// empty for a licence tester's payment, which has none), Apple's transaction id.
        /// </summary>
        public string Order { get; }

        /// <summary>When the server verified it.</summary>
        public DateTimeOffset At { get; }

        public LedgerState State { get; }

        /// <summary>A test payment: Google's licence testers, Apple's sandbox.</summary>
        public bool IsTest { get; }

        /// <summary>The receipt it was verified from: how a second question about the same payment finds it.</summary>
        public ReceiptId Receipt { get; }

        public LedgerEntry WithState(LedgerState state) => new LedgerEntry(Number, Store, Product, Order, At, state, IsTest, Receipt);

        public override string ToString() => $"#{Number} {Product} ({State}{(IsTest ? ", test" : string.Empty)})";
    }

    /// <summary>
    /// A player's ledger of verified payments: the Cloud Save item "purchases" of the Protected class, which the player
    /// reads and only this module writes (docs/research/server-v1.md §3.2, §4.5). An immutable value: every change
    /// gives a new ledger, written back under the write lock of the one it was made from.
    /// </summary>
    public sealed class Ledger
    {
        /// <exception cref="ArgumentException">Two entries with one number or one receipt, or a next number not above them all.</exception>
        public Ledger(IEnumerable<LedgerEntry> entries, LedgerNumber next, DateTimeOffset? refundsCheckedAt)
        {
            Entries = (entries ?? throw new ArgumentNullException(nameof(entries))).OrderBy(entry => entry.Number).ToArray();
            Next = next;
            RefundsCheckedAt = refundsCheckedAt;
            if (Entries.GroupBy(entry => entry.Number).FirstOrDefault(same => same.Count() > 1) is IGrouping<LedgerNumber, LedgerEntry> twice)
            {
                throw new ArgumentException($"The ledger names the payment #{twice.Key} twice.", nameof(entries));
            }

            if (Entries.GroupBy(entry => entry.Receipt).FirstOrDefault(same => same.Count() > 1) is IGrouping<ReceiptId, LedgerEntry> sameReceipt)
            {
                throw new ArgumentException($"The ledger holds the receipt {sameReceipt.Key} twice.", nameof(entries));
            }

            if (Entries.Count > 0 && Entries[Entries.Count - 1].Number >= next)
            {
                throw new ArgumentException($"The ledger's next number {next} is not above its last payment #{Entries[Entries.Count - 1].Number}.", nameof(next));
            }
        }

        /// <summary>A player with no payment yet.</summary>
        public static Ledger Empty { get; } = new Ledger(Array.Empty<LedgerEntry>(), LedgerNumber.First, refundsCheckedAt: null);

        public IReadOnlyList<LedgerEntry> Entries { get; }

        /// <summary>The number the next payment gets.</summary>
        public LedgerNumber Next { get; }

        /// <summary>When the server last asked Apple for this player's refunds; nothing before the first time.</summary>
        public DateTimeOffset? RefundsCheckedAt { get; }

        /// <summary>The payment verified from this receipt; nothing when the ledger holds none.</summary>
        public LedgerEntry? FindByReceipt(ReceiptId receipt) => Entries.FirstOrDefault(entry => entry.Receipt == receipt);

        /// <summary>The ledger with one more payment, under the next number.</summary>
        /// <exception cref="ArgumentException">The ledger holds the receipt already.</exception>
        public Ledger WithPayment(PurchaseStore store, string product, string order, DateTimeOffset at, LedgerState state, bool isTest, ReceiptId receipt) =>
            new Ledger(Entries.Append(new LedgerEntry(Next, store, product, order, at, state, isTest, receipt)), Next.Following, RefundsCheckedAt);

        /// <summary>The ledger with these payments in a new state; numbers the ledger does not hold are passed over.</summary>
        public Ledger WithStates(IReadOnlyDictionary<LedgerNumber, LedgerState> states) =>
            new Ledger(Entries.Select(entry => states.TryGetValue(entry.Number, out LedgerState state) ? entry.WithState(state) : entry), Next, RefundsCheckedAt);

        public Ledger WithRefundsCheckedAt(DateTimeOffset at) => new Ledger(Entries, Next, at);
    }
}
