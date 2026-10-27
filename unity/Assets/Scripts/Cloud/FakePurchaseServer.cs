#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Purchases;
using Puzzle.Meta;

namespace Puzzle.Client.Cloud
{
    /// <summary>Why the fake server gives no verdict. A closed set.</summary>
    public enum FakeServerOutage
    {
        /// <summary>The module is not deployed: Cloud Code answers NotFound, as it does until the owner deploys it.</summary>
        NotDeployed,

        /// <summary>The phone has no network.</summary>
        NoNetwork,

        /// <summary>The module runs, and Google Play or the App Store cannot be reached from it.</summary>
        StoreUnreachable,
    }

    /// <summary>
    /// The game's server in memory, as the Cloud Code module behaves for the players of one game (server/CloudCode;
    /// docs/research/server-v1.md §4): a registry of the receipts it verified, by their proof, each with the players who
    /// claim it; each player's ledger, which it writes into the player's cloud as the module writes "purchases" (so the
    /// sync reads it); proofs no store gave (a forged payment); and outages: the module not deployed, no network, the
    /// store out of reach. A pack of coins is one player's; the starter pack, which the store keeps as the player's
    /// account's, is claimed by every account it comes back to. Each phone asks through its own view
    /// (<see cref="Phone"/>), as the player it is signed in as. For the tests and the Editor's checks: nothing here talks
    /// to a network.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class FakePurchaseServer
    {
        private readonly FakeUnityServices _services;
        private readonly IDocumentJson _json;
        private readonly Dictionary<string, Dictionary<CloudPlayerId, long>> _receipts = new Dictionary<string, Dictionary<CloudPlayerId, long>>(StringComparer.Ordinal);
        private readonly Dictionary<CloudPlayerId, List<LedgerEntryDocument>> _ledgers = new Dictionary<CloudPlayerId, List<LedgerEntryDocument>>();
        private readonly HashSet<string> _forged = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="services">Whose players' clouds the ledgers are written into.</param>
        /// <param name="json">How the ledger is written, as the module writes it.</param>
        public FakePurchaseServer(FakeUnityServices services, IDocumentJson json)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _json = json ?? throw new ArgumentNullException(nameof(json));
        }

        /// <summary>Why no verdict is given now; nothing while the module answers.</summary>
        public FakeServerOutage? Outage { get; private set; }

        /// <summary>The questions about payments that reached the module.</summary>
        public int Validations { get; private set; }

        /// <summary>The deletions the module made.</summary>
        public int Deletions { get; private set; }

        /// <summary>No store gave this proof: the module finds the payment not from the store.</summary>
        public void Forged(string proof) => _forged.Add(proof ?? throw new ArgumentNullException(nameof(proof)));

        public void GoesOut(FakeServerOutage outage) => Outage = outage;

        public void ComesBack() => Outage = null;

        /// <summary>The player's ledger as the module keeps it, its numbers in order; none for a player with no verified payment.</summary>
        public IReadOnlyList<LedgerEntryDocument> LedgerOf(CloudPlayerId player) =>
            _ledgers.TryGetValue(player, out List<LedgerEntryDocument>? entries) ? entries.ToArray() : Array.Empty<LedgerEntryDocument>();

        /// <summary>A phone's view of the module: it asks as the player its accounts are signed in as.</summary>
        public IServerModule Phone(FakePhoneAccounts accounts) => new FakeServerPhone(this, accounts ?? throw new ArgumentNullException(nameof(accounts)));

        private ServerVerdict Validate(CloudPlayerId player, PaymentQuestion question)
        {
            Validations++;
            string proof = question.Payment.Proof ?? throw new InvalidOperationException($"The game asked the server about the {question.Payment} without the store's proof.");
            if (_forged.Contains(proof))
            {
                return new ServerVerdict.Invalid("notFromTheStore");
            }

            if (!_receipts.TryGetValue(proof, out Dictionary<CloudPlayerId, long>? claims))
            {
                claims = new Dictionary<CloudPlayerId, long>();
                _receipts.Add(proof, claims);
            }

            if (claims.TryGetValue(player, out long known))
            {
                return new ServerVerdict.AlreadyValidated(new LedgerNumber(known), isTest: false);
            }

            bool isOnePlayers = question.Product.Kind == StoreProductKind.CoinPack;
            if (isOnePlayers && claims.Count > 0)
            {
                return new ServerVerdict.Invalid("anotherPlayers");
            }

            long number = Enter(player, question);
            claims.Add(player, number);
            return new ServerVerdict.Valid(new LedgerNumber(number), isTest: false);
        }

        /// <summary>The payment enters the player's ledger under its next number, and the ledger is written into the player's cloud.</summary>
        private long Enter(CloudPlayerId player, PaymentQuestion question)
        {
            if (!_ledgers.TryGetValue(player, out List<LedgerEntryDocument>? entries))
            {
                entries = new List<LedgerEntryDocument>();
                _ledgers.Add(player, entries);
            }

            long number = entries.Count + 1;
            entries.Add(new LedgerEntryDocument
            {
                seq = number,
                store = "GooglePlay",
                product = question.Product.IdSuffix,
                order = "GPA.FAKE-" + number.ToString("0000", CultureInfo.InvariantCulture),
                at = "2026-11-03T09:00:00Z",
                state = "valid",
            });
            _services.CloudOf(player).LedgerWrittenByTheServer(_json.Write(new PurchaseLedgerDocument { next = number + 1, items = entries.ToArray() }));
            return number;
        }

        private void DeleteTheDataOf(CloudPlayerId player)
        {
            Deletions++;
            _ledgers.Remove(player);
            foreach (Dictionary<CloudPlayerId, long> claims in _receipts.Values)
            {
                claims.Remove(player);
            }

            _services.CloudOf(player).Deleted();
        }

        /// <summary>One phone's view of the fake module: the player is the one its accounts are signed in as.</summary>
        private sealed class FakeServerPhone : IServerModule
        {
            private readonly FakePurchaseServer _server;
            private readonly FakePhoneAccounts _accounts;

            public FakeServerPhone(FakePurchaseServer server, FakePhoneAccounts accounts)
            {
                _server = server;
                _accounts = accounts;
            }

            public Task<ServerVerdict> ValidatePurchaseAsync(PaymentQuestion question, CancellationToken cancel)
            {
                cancel.ThrowIfCancellationRequested();
                ServerVerdict verdict = _server.Outage switch
                {
                    FakeServerOutage.NotDeployed => new ServerVerdict.Unavailable("the module is not deployed (NotFound)"),
                    FakeServerOutage.NoNetwork => new ServerVerdict.Unavailable("no network"),
                    FakeServerOutage.StoreUnreachable => new ServerVerdict.Unavailable("storeUnreachable"),
                    _ when _accounts.SignedIn is CloudPlayerId player => _server.Validate(player, question),
                    _ => new ServerVerdict.Unavailable("notAPlayer"),
                };
                return Task.FromResult(verdict);
            }

            public Task<DataDeletion> DeleteMyDataAsync(CancellationToken cancel)
            {
                cancel.ThrowIfCancellationRequested();
                if (_server.Outage is FakeServerOutage outage)
                {
                    return Task.FromResult(outage == FakeServerOutage.NoNetwork ? DataDeletion.NoNetwork : DataDeletion.Unavailable);
                }

                if (!(_accounts.SignedIn is CloudPlayerId player))
                {
                    return Task.FromResult(DataDeletion.Unavailable);
                }

                _server.DeleteTheDataOf(player);
                return Task.FromResult(DataDeletion.Deleted);
            }
        }
    }
}
