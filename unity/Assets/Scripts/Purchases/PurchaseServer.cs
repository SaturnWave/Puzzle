#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Meta;

namespace Puzzle.Client.Purchases
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the reply's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// What the game's server said of a payment, as its ValidatePurchase answers (docs/research/server-v1.md §4.8;
    /// tests/fixtures/cloud-code/replies.json). A closed set: the private constructor keeps every kind inside this class.
    /// </summary>
    public abstract class ServerVerdict
    {
        private ServerVerdict()
        {
        }

        /// <summary>
        /// The module's answer as the game receives it. An answer the module does not write (no verdict it gives, a
        /// valid one without its number) is no answer: the payment waits, as for a server out of reach.
        /// </summary>
        /// <param name="reply">Nothing when the answer's text held no document.</param>
        public static ServerVerdict Of(PurchaseReplyDocument? reply)
        {
            if (reply == null)
            {
                return new Unavailable("an answer that is no document");
            }

            return reply.verdict switch
            {
                "valid" when reply.seq >= 1 => new Valid(new LedgerNumber(reply.seq), reply.test),
                "alreadyValidated" when reply.seq >= 1 => new AlreadyValidated(new LedgerNumber(reply.seq), reply.test),
                "pending" => new Pending(),
                "invalid" => new Invalid(WordOr(reply.reason, "no reason")),
                "unavailable" => new Unavailable(WordOr(reply.reason, "no reason")),
                _ => new Unavailable($"an answer the module does not write: verdict '{reply.verdict}', seq {reply.seq}"),
            };
        }

        private static string WordOr(string? word, string none) => string.IsNullOrEmpty(word) ? none : word!;

        /// <summary>The store confirmed the payment; the server entered it in the player's ledger under this number now.</summary>
        public sealed class Valid : ServerVerdict
        {
            public Valid(LedgerNumber number, bool isTest)
            {
                Number = number;
                IsTest = isTest;
            }

            public LedgerNumber Number { get; }

            /// <summary>A test payment (Google's licence testers, Apple's sandbox).</summary>
            public bool IsTest { get; }

            public override string ToString() => $"valid #{Number}{(IsTest ? " (test)" : string.Empty)}";
        }

        /// <summary>The server verified the payment before, under this number (another phone asked first, or the game closed before it gave it).</summary>
        public sealed class AlreadyValidated : ServerVerdict
        {
            public AlreadyValidated(LedgerNumber number, bool isTest)
            {
                Number = number;
                IsTest = isTest;
            }

            public LedgerNumber Number { get; }

            public bool IsTest { get; }

            public override string ToString() => $"validated before as #{Number}{(IsTest ? " (test)" : string.Empty)}";
        }

        /// <summary>The store holds the payment as not made yet (cash at a shop): nothing is given; the store tells of it again once made.</summary>
        public sealed class Pending : ServerVerdict
        {
            public override string ToString() => "pending with the store";
        }

        /// <summary>The store did not confirm the payment, or it is another player's, or refunded: nothing is given, nothing confirmed (owner's decision 4).</summary>
        public sealed class Invalid : ServerVerdict
        {
            public Invalid(string reason)
            {
                Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            }

            /// <summary>The module's word for why ("anotherPlayers"), for the log only.</summary>
            public string Reason { get; }

            public override string ToString() => $"invalid: {Reason}";
        }

        /// <summary>
        /// No verdict: the module could not ask the store, or the module could not be reached (no network, not deployed,
        /// no account signed in). The payment waits, unconfirmed, and is asked about again later.
        /// </summary>
        public sealed class Unavailable : ServerVerdict
        {
            public Unavailable(string reason)
            {
                Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            }

            /// <summary>Why, for the log only: the module's word, or what kept the game from asking it.</summary>
            public string Reason { get; }

            public override string ToString() => $"unavailable: {Reason}";
        }
    }

    /// <summary>
    /// ValidatePurchase's answer as the module writes it (server/CloudCode, Endpoints/ModuleReplies):
    /// <c>{"verdict":"valid","seq":3,"test":false}</c>, <c>{"verdict":"invalid","reason":"anotherPlayers"}</c>. A value
    /// the answer leaves out reads as its default: no number is 0, which no ledger gives.
    /// </summary>
    [Serializable]
    public sealed class PurchaseReplyDocument
    {
        public string verdict = string.Empty;
        public long seq;
        public bool test;
        public string reason = string.Empty;
    }

    /// <summary>A payment the server is asked about: the store's payment, with its proof, and the product it is for.</summary>
    public sealed class PaymentQuestion
    {
        public PaymentQuestion(StorePayment payment, StoreProduct product)
        {
            Payment = payment ?? throw new ArgumentNullException(nameof(payment));
            Product = product ?? throw new ArgumentNullException(nameof(product));
        }

        public StorePayment Payment { get; }

        public StoreProduct Product { get; }

        public override string ToString() => $"{Payment} ({Product.IdSuffix})";
    }

    /// <summary>The server's verdict on a payment it was asked about.</summary>
    public sealed class ServerAnswer
    {
        public ServerAnswer(StorePayment payment, ServerVerdict verdict)
        {
            Payment = payment ?? throw new ArgumentNullException(nameof(payment));
            Verdict = verdict ?? throw new ArgumentNullException(nameof(verdict));
        }

        public StorePayment Payment { get; }

        public ServerVerdict Verdict { get; }

        public override string ToString() => $"{Payment}: {Verdict}";
    }

    /// <summary>
    /// The game's server as the purchase's course asks it (docs/research/server-v1.md §4): whether payments wait for its
    /// word, the player a payment sheet names to the store, and its verdicts. What it answers is gathered and taken by
    /// the course at its next step, in the order it came, as the store's news is. It is a seam: the game's is
    /// Cloud/CloudSaving (the Cloud Code module behind the account), the tests and the Editor's walks have fakes, and
    /// a session without a cloud has <see cref="NoPurchaseServer"/>.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public interface IPurchaseServer
    {
        /// <summary>
        /// True while every payment waits for the server's verdict before it is given; false while the game gives payments
        /// on the phone as before the server: the "Bulut kaydı" switch off, a build without a cloud project, Remote
        /// Config's purchase_validation_enabled off (docs/research/server-v1.md §3.8).
        /// </summary>
        bool Verifies { get; }

        /// <summary>The player a payment sheet names to the store now; nothing while no account is signed in.</summary>
        StoreBuyer? Buyer { get; }

        /// <summary>Asks the server about a payment, in the background: its verdict comes with <see cref="TakeAnswers"/>, once.</summary>
        void Ask(PaymentQuestion question);

        /// <summary>The verdicts that came since this was asked last, in the order they came.</summary>
        IReadOnlyList<ServerAnswer> TakeAnswers();
    }

    /// <summary>No server: payments are given on the phone, as before the server (the Editor's tools, a session without a cloud).</summary>
    public sealed class NoPurchaseServer : IPurchaseServer
    {
        private NoPurchaseServer()
        {
        }

        public static NoPurchaseServer Instance { get; } = new NoPurchaseServer();

        public bool Verifies => false;

        public StoreBuyer? Buyer => null;

        /// <exception cref="InvalidOperationException">Always: a course asks only a server that verifies.</exception>
        public void Ask(PaymentQuestion question) => throw new InvalidOperationException($"No server verifies payments here, yet it was asked about the {question}.");

        public IReadOnlyList<ServerAnswer> TakeAnswers() => Array.Empty<ServerAnswer>();
    }
}
