using System;
using System.Linq;
using MuseumPuzzle.Server.Purchases;
using Newtonsoft.Json;

namespace MuseumPuzzle.Server.Endpoints
{
    // The endpoints' answers as the game receives them. Cloud Code writes a function's answer with Newtonsoft.Json
    // (docs.unity.com, "Custom serialization"), so the names are set here with its attribute, and a value the answer
    // does not carry is left out. tests/fixtures/cloud-code/replies.json pins every kind for both sides: the module's
    // tests write them, the game's tests read them.

    /// <summary>ValidatePurchase's answer: <c>{"verdict":"valid","seq":3,"test":false}</c>, <c>{"verdict":"invalid","reason":"anotherPlayers"}</c>.</summary>
    public sealed class PurchaseReply
    {
        /// <summary>"valid", "alreadyValidated", "pending", "invalid" or "unavailable".</summary>
        [JsonProperty("verdict")]
        public string Verdict { get; set; } = string.Empty;

        /// <summary>The ledger number, for "valid" and "alreadyValidated".</summary>
        [JsonProperty("seq", NullValueHandling = NullValueHandling.Ignore)]
        public long? Seq { get; set; }

        /// <summary>A test payment (Google's licence testers, Apple's sandbox), for "valid" and "alreadyValidated".</summary>
        [JsonProperty("test", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Test { get; set; }

        /// <summary>Why, for "invalid" and "unavailable": for the game's log, never for the player.</summary>
        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
        public string? Reason { get; set; }
    }

    /// <summary>CheckRefunds' answer: <c>{"outcome":"checked","refunded":[4]}</c>, <c>{"outcome":"notDue"}</c>.</summary>
    public sealed class RefundReply
    {
        /// <summary>"checked", "notDue", "nothingToCheck" or "unavailable".</summary>
        [JsonProperty("outcome")]
        public string Outcome { get; set; } = string.Empty;

        /// <summary>The ledger numbers found refunded now, for "checked".</summary>
        [JsonProperty("refunded", NullValueHandling = NullValueHandling.Ignore)]
        public long[]? Refunded { get; set; }

        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
        public string? Reason { get; set; }
    }

    /// <summary>DeleteMyData's answer: <c>{"outcome":"deleted"}</c>, or "unavailable" with a reason; only after "deleted" does the game delete the account.</summary>
    public sealed class DeletionReply
    {
        [JsonProperty("outcome")]
        public string Outcome { get; set; } = string.Empty;

        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
        public string? Reason { get; set; }
    }

    /// <summary>A scheduled job's answer, for Cloud Code's log: <c>{"outcome":"done","looked":12,"changed":1}</c>.</summary>
    public sealed class ChoreReply
    {
        /// <summary>"done", "unfinished", "unavailable" or "refused".</summary>
        [JsonProperty("outcome")]
        public string Outcome { get; set; } = string.Empty;

        [JsonProperty("looked", NullValueHandling = NullValueHandling.Ignore)]
        public int? Looked { get; set; }

        [JsonProperty("changed", NullValueHandling = NullValueHandling.Ignore)]
        public int? Changed { get; set; }

        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
        public string? Reason { get; set; }
    }

    /// <summary>The words of the answers: each closed set of the module written out by hand, so that renaming a member never changes what the game reads.</summary>
    public static class ModuleReplies
    {
        public static PurchaseReply Of(PurchaseVerdict verdict) => verdict switch
        {
            PurchaseVerdict.Valid valid => new PurchaseReply { Verdict = "valid", Seq = valid.Number.Value, Test = valid.IsTest },
            PurchaseVerdict.AlreadyValidated known => new PurchaseReply { Verdict = "alreadyValidated", Seq = known.Number.Value, Test = known.IsTest },
            PurchaseVerdict.Pending => new PurchaseReply { Verdict = "pending" },
            PurchaseVerdict.Invalid invalid => new PurchaseReply { Verdict = "invalid", Reason = WordOf(invalid.Reason) },
            PurchaseVerdict.Unavailable unavailable => new PurchaseReply { Verdict = "unavailable", Reason = WordOf(unavailable.Reason) },
            _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "A verdict the module does not give."),
        };

        public static RefundReply Of(RefundOutcome outcome) => outcome switch
        {
            RefundOutcome.Checked done => new RefundReply { Outcome = "checked", Refunded = done.Refunded.Select(number => number.Value).ToArray() },
            RefundOutcome.NotDue => new RefundReply { Outcome = "notDue" },
            RefundOutcome.NothingToCheck => new RefundReply { Outcome = "nothingToCheck" },
            RefundOutcome.Unavailable unavailable => new RefundReply { Outcome = "unavailable", Reason = WordOf(unavailable.Reason) },
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "An outcome the module does not give."),
        };

        public static DeletionReply Of(DeletionOutcome outcome) => outcome switch
        {
            DeletionOutcome.Deleted => new DeletionReply { Outcome = "deleted" },
            DeletionOutcome.Unavailable unavailable => new DeletionReply { Outcome = "unavailable", Reason = WordOf(unavailable.Reason) },
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "An outcome the module does not give."),
        };

        public static ChoreReply Of(ChoreOutcome outcome) => outcome switch
        {
            ChoreOutcome.Done done => new ChoreReply { Outcome = "done", Looked = done.Looked, Changed = done.Changed },
            ChoreOutcome.Unfinished unfinished => new ChoreReply { Outcome = "unfinished", Looked = unfinished.Looked, Changed = unfinished.Changed },
            ChoreOutcome.Unavailable unavailable => new ChoreReply { Outcome = "unavailable", Reason = WordOf(unavailable.Reason) },
            ChoreOutcome.Refused => new ChoreReply { Outcome = "refused" },
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "An outcome the module does not give."),
        };

        public static string WordOf(InvalidReason reason) => reason switch
        {
            InvalidReason.UnknownStore => "unknownStore",
            InvalidReason.UnknownProduct => "unknownProduct",
            InvalidReason.NotFromTheStore => "notFromTheStore",
            InvalidReason.WrongApp => "wrongApp",
            InvalidReason.ProductMismatch => "productMismatch",
            InvalidReason.Cancelled => "cancelled",
            InvalidReason.Refunded => "refunded",
            InvalidReason.AnotherPlayers => "anotherPlayers",
            InvalidReason.AlreadyRefused => "alreadyRefused",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A reason the module does not give."),
        };

        public static string WordOf(UnavailableReason reason) => reason switch
        {
            UnavailableReason.StoreUnreachable => "storeUnreachable",
            UnavailableReason.CredentialsMissing => "credentialsMissing",
            UnavailableReason.CredentialsRefused => "credentialsRefused",
            UnavailableReason.NoSignedTransaction => "noSignedTransaction",
            UnavailableReason.StoreAnswerUnreadable => "storeAnswerUnreadable",
            UnavailableReason.CloudSaveUnreachable => "cloudSaveUnreachable",
            UnavailableReason.LedgerUnreadable => "ledgerUnreadable",
            UnavailableReason.NotAPlayer => "notAPlayer",
            UnavailableReason.OutOfTime => "outOfTime",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A reason the module does not give."),
        };
    }
}
