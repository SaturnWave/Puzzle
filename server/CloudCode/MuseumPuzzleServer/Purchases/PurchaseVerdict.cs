using System;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>Why a payment is not given. A closed set; the game logs the word (docs/research/server-v1.md §4.8) and shows one card for all.</summary>
    public enum InvalidReason
    {
        /// <summary>The game names a store it does not sell through.</summary>
        UnknownStore,

        /// <summary>The game does not sell the product.</summary>
        UnknownProduct,

        /// <summary>The store does not know the payment, or its signature is not the store's.</summary>
        NotFromTheStore,

        /// <summary>The receipt is for another app.</summary>
        WrongApp,

        /// <summary>The receipt is for another product than the one named.</summary>
        ProductMismatch,

        /// <summary>The store cancelled the payment.</summary>
        Cancelled,

        /// <summary>The payment was refunded, or voided by the store.</summary>
        Refunded,

        /// <summary>A pack of coins whose receipt another player was given, or names another player as its buyer.</summary>
        AnotherPlayers,

        /// <summary>The ledger holds the payment as one the store did not confirm.</summary>
        AlreadyRefused,
    }

    /// <summary>Why the server could not decide now. A closed set: the game delivers as today, keeps the payment to ask again, and logs the word.</summary>
    public enum UnavailableReason
    {
        /// <summary>Google or Apple did not answer, or asked to wait.</summary>
        StoreUnreachable,

        /// <summary>The owner has not entered the store's key in the Secret Manager (yet), or it is not the key it should be.</summary>
        CredentialsMissing,

        /// <summary>The store refuses the module's key.</summary>
        CredentialsRefused,

        /// <summary>The App Store's signed transaction did not come with the payment (StoreKit 1 on an old iOS).</summary>
        NoSignedTransaction,

        /// <summary>The store answered with something the module cannot read.</summary>
        StoreAnswerUnreadable,

        /// <summary>Cloud Save or the Secret Manager could not be reached.</summary>
        CloudSaveUnreachable,

        /// <summary>The player's ledger is not one the module writes; nothing is written over it, and the log says why.</summary>
        LedgerUnreadable,

        /// <summary>No player called (a service or a schedule): payments, refunds and data are only ever a player's.</summary>
        NotAPlayer,

        /// <summary>The call ran out of its time (a Cloud Code call has fifteen seconds); what was done stays, and the next call finishes it.</summary>
        OutOfTime,
    }

    /// <summary>
    /// The server's answer to "is this payment real?" (docs/research/server-v1.md §4.1, §4.8). A closed set: the
    /// private constructor keeps every kind inside this class.
    /// </summary>
    public abstract class PurchaseVerdict
    {
        private PurchaseVerdict()
        {
        }

        /// <summary>Verified now and entered in the ledger under this number: deliver it with the number, then confirm it to the store.</summary>
        public sealed class Valid : PurchaseVerdict
        {
            public Valid(LedgerNumber number, bool isTest)
            {
                Number = number;
                IsTest = isTest;
            }

            public LedgerNumber Number { get; }

            public bool IsTest { get; }

            public override string ToString() => $"valid #{Number}{(IsTest ? " (test)" : string.Empty)}";
        }

        /// <summary>
        /// Verified before, and still valid: the game delivers it if its progress holds neither the payment nor the
        /// number (the game stopped between the answer and its save), and confirms it to the store.
        /// </summary>
        public sealed class AlreadyValidated : PurchaseVerdict
        {
            public AlreadyValidated(LedgerNumber number, bool isTest)
            {
                Number = number;
                IsTest = isTest;
            }

            public LedgerNumber Number { get; }

            public bool IsTest { get; }

            public override string ToString() => $"already validated #{Number}{(IsTest ? " (test)" : string.Empty)}";
        }

        /// <summary>The store has not taken the money yet: nothing is delivered or confirmed.</summary>
        public sealed class Pending : PurchaseVerdict
        {
            public override string ToString() => "pending";
        }

        /// <summary>Not a payment to give: nothing is delivered, and the game does not confirm it (Google refunds an unconfirmed payment in three days).</summary>
        public sealed class Invalid : PurchaseVerdict
        {
            public Invalid(InvalidReason reason)
            {
                Reason = reason;
            }

            public InvalidReason Reason { get; }

            public override string ToString() => $"invalid ({Reason})";
        }

        /// <summary>No decision now: the game delivers as it does today and asks again at its next sync.</summary>
        public sealed class Unavailable : PurchaseVerdict
        {
            public Unavailable(UnavailableReason reason)
            {
                Reason = reason;
            }

            public UnavailableReason Reason { get; }

            public override string ToString() => $"unavailable ({Reason})";
        }
    }

    /// <summary>
    /// What a store's verifier found of a payment. A closed set: the private constructor keeps every kind inside this
    /// class. Only <see cref="Confirmed"/> enters the ledger.
    /// </summary>
    public abstract class StoreCheck
    {
        private StoreCheck()
        {
        }

        /// <summary>The store took the money for this product from this player.</summary>
        public sealed class Confirmed : StoreCheck
        {
            /// <param name="order">The store's own name for the payment: Google's order id (empty for a licence tester's), Apple's transaction id.</param>
            public Confirmed(string order, bool isTest)
            {
                Order = order ?? string.Empty;
                IsTest = isTest;
            }

            public string Order { get; }

            public bool IsTest { get; }
        }

        public sealed class Pending : StoreCheck
        {
        }

        public sealed class Rejected : StoreCheck
        {
            public Rejected(InvalidReason reason)
            {
                Reason = reason;
            }

            public InvalidReason Reason { get; }
        }

        public sealed class Unreachable : StoreCheck
        {
            public Unreachable(UnavailableReason reason)
            {
                Reason = reason;
            }

            public UnavailableReason Reason { get; }
        }
    }

    /// <summary>
    /// What the game says of a payment when it asks: the store, the product, the store's id for it and the store's
    /// proof of it. For Google Play the token is the purchase token (<c>GoogleOrderInfo.PurchaseToken</c>), and the
    /// proof is the same token; for the App Store the token is the transaction id and the proof Apple's signed
    /// transaction (<c>AppleOrderInfo.jwsRepresentation</c>). Nothing of it is trusted until the store confirms it.
    /// </summary>
    public sealed class PaymentClaim
    {
        public PaymentClaim(PurchaseStore store, string productIdSuffix, string token, string proof, bool deliveredUnverified)
        {
            Store = store;
            ProductIdSuffix = productIdSuffix ?? string.Empty;
            Token = token ?? string.Empty;
            Proof = proof ?? string.Empty;
            DeliveredUnverified = deliveredUnverified;
        }

        public PurchaseStore Store { get; }

        public string ProductIdSuffix { get; }

        /// <summary>The store's id for the payment, which names its receipt (<see cref="ReceiptId"/>).</summary>
        public string Token { get; }

        public string Proof { get; }

        /// <summary>
        /// The game gave the payment before the server could be asked (§4.8, the queue of payments to verify): a
        /// payment the store then refuses is entered in the ledger as invalid, for the record.
        /// </summary>
        public bool DeliveredUnverified { get; }
    }
}
