#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Client.Purchases
{
    /// <summary>
    /// What the player has bought for real money, as their progress keeps it: how many times each product was
    /// delivered (by the end of its id in the stores, <see cref="Puzzle.Meta.StoreProduct.IdSuffix"/>), and the
    /// stores' ids of the latest payments delivered. The counts are what sells the starter pack once per player; the
    /// payments' ids are what delivers a payment once when the store tells of it again, as it does with a payment the
    /// game has not confirmed yet (closed between delivery and confirmation) and with what the player owns.
    /// <para>
    /// Only the latest <see cref="TransactionsKept"/> ids are kept, the oldest giving way first: the store tells again
    /// only of payments not yet confirmed, which are the last few, and of the starter pack, which its count already
    /// answers; the record so stays small however much a player buys.
    /// </para>
    /// <para>
    /// The record also keeps the numbers the game's server gave the payments it verified (<see cref="LedgerDelivered"/>,
    /// docs/research/server-v1.md §3.7), never cut short: they name a payment alike on every device of the player, so
    /// that the cloud's merge counts a payment two devices both delivered once (Cloud/ProgressMerge), and a verified
    /// payment no device delivered can be given back from the server's ledger. A number stands for a payment whose
    /// contents are in the wallet. Until the server is in the game the numbers stay none, and the record is this
    /// device's alone: a player who deletes the game or changes phones starts with none, and the store gives back only
    /// the starter pack (a product the store keeps as the player's), never coins bought before.
    /// </para>
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class PurchasesMade : IEquatable<PurchasesMade>
    {
        /// <summary>How many of the latest payments' ids are kept.</summary>
        public const int TransactionsKept = 64;

        private readonly SortedDictionary<string, int> _times;
        private readonly string[] _transactions;

        /// <summary>What was bought, with no payment numbered by the server.</summary>
        /// <exception cref="ArgumentException">
        /// A product named with no id or twice, a count under 1, a payment's id that is empty or named twice, or more
        /// ids than are kept.
        /// </exception>
        public PurchasesMade(IEnumerable<(string Product, int Times)> bought, IEnumerable<string> transactions)
            : this(bought, transactions, LedgerNumbers.None)
        {
        }

        /// <param name="ledgerDelivered">The ledger numbers of the payments whose contents are in the wallet.</param>
        /// <exception cref="ArgumentException">
        /// A product named with no id or twice, a count under 1, a payment's id that is empty or named twice, or more
        /// ids than are kept.
        /// </exception>
        public PurchasesMade(IEnumerable<(string Product, int Times)> bought, IEnumerable<string> transactions, LedgerNumbers ledgerDelivered)
        {
            LedgerDelivered = ledgerDelivered ?? throw new ArgumentNullException(nameof(ledgerDelivered));
            _times = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach ((string product, int times) in bought ?? throw new ArgumentNullException(nameof(bought)))
            {
                if (string.IsNullOrEmpty(product) || times < 1 || _times.ContainsKey(product))
                {
                    throw new ArgumentException($"A product is named once, with an id and bought at least once: '{product}' ×{times}.", nameof(bought));
                }

                _times[product] = times;
            }

            _transactions = (transactions ?? throw new ArgumentNullException(nameof(transactions))).ToArray();
            if (_transactions.Any(string.IsNullOrEmpty) || _transactions.Distinct(StringComparer.Ordinal).Count() != _transactions.Length)
            {
                throw new ArgumentException("A payment's id is not empty and is kept once.", nameof(transactions));
            }

            if (_transactions.Length > TransactionsKept)
            {
                throw new ArgumentException($"{_transactions.Length} payments' ids are more than the {TransactionsKept} kept.", nameof(transactions));
            }
        }

        /// <summary>A player who has bought nothing.</summary>
        public static PurchasesMade None { get; } = new PurchasesMade(Array.Empty<(string, int)>(), Array.Empty<string>());

        /// <summary>Every product bought, by the end of its store id in ordinal order, with how many times.</summary>
        public IReadOnlyList<(string Product, int Times)> Bought => _times.Select(entry => (entry.Key, entry.Value)).ToArray();

        /// <summary>The ids of the latest payments delivered, the oldest first.</summary>
        public IReadOnlyList<string> Transactions => _transactions;

        /// <summary>The server's ledger numbers of the payments whose contents are in the wallet, every one since the first.</summary>
        public LedgerNumbers LedgerDelivered { get; }

        /// <summary>How many times the product with this end of its store id was delivered.</summary>
        public int TimesBought(string productIdSuffix) => _times.TryGetValue(productIdSuffix, out int times) ? times : 0;

        /// <summary>True when the payment with this id was delivered (or counted as sold out) already.</summary>
        public bool HasSeen(string transactionId) => _transactions.Contains(transactionId, StringComparer.Ordinal);

        /// <summary>The product was delivered once more, for this payment.</summary>
        /// <exception cref="ArgumentException">The payment was seen already: a payment is delivered once.</exception>
        public PurchasesMade WithDelivered(string productIdSuffix, string transactionId)
        {
            var bought = new SortedDictionary<string, int>(_times, StringComparer.Ordinal);
            bought[productIdSuffix] = TimesBought(productIdSuffix) + 1;
            return new PurchasesMade(bought.Select(entry => (entry.Key, entry.Value)), TransactionsWith(transactionId), LedgerDelivered);
        }

        /// <summary>The payment was seen and nothing was given for it: the product is sold out to this player.</summary>
        /// <exception cref="ArgumentException">The payment was seen already.</exception>
        public PurchasesMade WithSeen(string transactionId) => new PurchasesMade(Bought, TransactionsWith(transactionId), LedgerDelivered);

        /// <summary>
        /// The payment with this number in the server's ledger has its contents in the wallet: delivered just now with
        /// its number, or delivered before the server could verify it and numbered now, when no coin comes with the number.
        /// </summary>
        public PurchasesMade WithLedgerNumber(LedgerNumber number) => new PurchasesMade(Bought, Transactions, LedgerDelivered.With(number));

        public bool Equals(PurchasesMade? other) =>
            other is not null && _times.SequenceEqual(other._times) && _transactions.SequenceEqual(other._transactions, StringComparer.Ordinal)
            && LedgerDelivered.Equals(other.LedgerDelivered);

        public override bool Equals(object? obj) => Equals(obj as PurchasesMade);

        public override int GetHashCode() => HashCode.Combine(_times.Count, _transactions.Length, _transactions.LastOrDefault(), LedgerDelivered);

        public override string ToString() =>
            (_times.Count == 0 ? "nothing bought" : $"bought {string.Join(", ", _times.Select(entry => $"{entry.Key} ×{entry.Value}"))}")
            + (LedgerDelivered.IsEmpty ? string.Empty : $", {LedgerDelivered}");

        private IEnumerable<string> TransactionsWith(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId) || HasSeen(transactionId))
            {
                throw new ArgumentException($"The payment '{transactionId}' has an id and is seen once.", nameof(transactionId));
            }

            return _transactions.Append(transactionId).Skip(Math.Max(0, _transactions.Length + 1 - TransactionsKept));
        }
    }
}
