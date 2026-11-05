using System;
using System.Collections.Generic;
using System.Linq;
using MuseumPuzzle.Server.Players;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>A player who was given a receipt's payment, and the number it has in that player's ledger.</summary>
    public sealed class ReceiptClaim
    {
        public ReceiptClaim(PlayerId player, LedgerNumber number)
        {
            Player = player;
            Number = number;
        }

        public PlayerId Player { get; }

        public LedgerNumber Number { get; }
    }

    /// <summary>
    /// The registry's record of one receipt: the Cloud Save custom item "rcpt-…" of the Private class, which no player
    /// can read (docs/research/server-v1.md §3.2, §4.5). It says which store and product the receipt was for, when the
    /// server first verified it, what it is now, and who was given it, so that a pack of coins is given once in all
    /// and a refund found in the store's lists reaches the ledger of whoever holds it.
    /// A pack of coins has at most one claim; the starter pack, which the store account keeps for good, one for each
    /// Unity account it was given back on. When the last player who held it deletes their data the record keeps no
    /// player at all: the receipt's digest, its product, its date and its state stay for 36 months as the trace the
    /// owner decided on (§5.6, decision 5, 2026-10-26), from <see cref="ForgottenOn"/>.
    /// </summary>
    public sealed class ReceiptRecord
    {
        /// <exception cref="ArgumentException">A product that is empty, two claims of one player, or a trace date on a record that still has a player.</exception>
        public ReceiptRecord(PurchaseStore store, string product, DateTimeOffset at, LedgerState state, bool isTest, IEnumerable<ReceiptClaim> claims, DateOnly? forgottenOn)
        {
            Store = store;
            Product = string.IsNullOrEmpty(product) ? throw new ArgumentException("A receipt is for a product.", nameof(product)) : product;
            At = at;
            State = state;
            IsTest = isTest;
            Claims = (claims ?? throw new ArgumentNullException(nameof(claims))).ToArray();
            ForgottenOn = forgottenOn;
            if (Claims.GroupBy(claim => claim.Player).FirstOrDefault(same => same.Count() > 1) is IGrouping<PlayerId, ReceiptClaim> twice)
            {
                throw new ArgumentException($"The receipt names the player {twice.Key} twice.", nameof(claims));
            }

            if (forgottenOn != null && Claims.Count > 0)
            {
                throw new ArgumentException("A receipt that still has a player is no trace yet.", nameof(forgottenOn));
            }
        }

        public PurchaseStore Store { get; }

        public string Product { get; }

        /// <summary>When the server first verified it.</summary>
        public DateTimeOffset At { get; }

        public LedgerState State { get; }

        public bool IsTest { get; }

        public IReadOnlyList<ReceiptClaim> Claims { get; }

        /// <summary>The day its last player's data was deleted; nothing while a player holds it. The trace is deleted 36 months after this day.</summary>
        public DateOnly? ForgottenOn { get; }

        public ReceiptClaim? ClaimOf(PlayerId player) => Claims.FirstOrDefault(claim => claim.Player == player);

        /// <summary>The record with one more player; a receipt that was a trace is no longer one.</summary>
        public ReceiptRecord WithClaim(ReceiptClaim claim) =>
            new ReceiptRecord(Store, Product, At, State, IsTest, Claims.Where(held => held.Player != claim.Player).Append(claim), forgottenOn: null);

        public ReceiptRecord WithState(LedgerState state) => new ReceiptRecord(Store, Product, At, state, IsTest, Claims, ForgottenOn);

        /// <summary>The record without this player: the trace of its payment from <paramref name="today"/> when the player was the last who held it.</summary>
        public ReceiptRecord WithoutPlayer(PlayerId player, DateOnly today)
        {
            ReceiptClaim[] others = Claims.Where(claim => claim.Player != player).ToArray();
            return new ReceiptRecord(Store, Product, At, State, IsTest, others, others.Length == 0 ? ForgottenOn ?? today : null);
        }
    }
}
