using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;
using Unity.Services.CloudSave.Model;

namespace MuseumPuzzle.Server.Platform
{
    /// <summary>
    /// <see cref="IServerStore"/> on Unity's Cloud Save, through the Cloud Code game API client, always with the
    /// module's service token (a player's own token cannot write a Protected item or touch a Private custom item).
    /// A document is kept as a JSON object, not a string holding JSON: the game reads the ledger's value as it reads
    /// any object. The SDK's failures become <see cref="ServerStoreUnavailableException"/>, except the two that say
    /// something: 404 (nothing there) and 409 (written since the lock). This adapter is the module's one place that
    /// knows the SDK's Cloud Save; the tests use a fake in its place, and stage 7 of the plan tries it on a linked
    /// project.
    /// </summary>
    public sealed class UgsServerStore : IServerStore
    {
        public const string LedgerKey = "purchases";

        public const string ReceiptKey = "receipt";

        public const string TracesPrefix = "forgotten-";

        public const string ServerStateItem = "server-state";

        public const string VoidedSeenUntilKey = "googleVoidedSeenUntil";

        private readonly ICloudSaveDataApi _cloudSave;
        private readonly IExecutionContext _context;

        public UgsServerStore(ICloudSaveDataApi cloudSave, IExecutionContext context)
        {
            _cloudSave = cloudSave ?? throw new ArgumentNullException(nameof(cloudSave));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        private string Token => _context.ServiceToken;

        private string Project => _context.ProjectId;

        public static string TracesItemOf(DateOnly day) => TracesPrefix + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public Task<StoredText?> ReadLedgerAsync(PlayerId player, CancellationToken cancellation) =>
            FirstAsync($"the ledger of {player}", () => _cloudSave.GetProtectedItemsAsync(_context, Token, Project, player.Value, new List<string> { LedgerKey }, cancellationToken: cancellation));

        public Task<LockedWrite> WriteLedgerAsync(PlayerId player, string text, WriteLock? expected, CancellationToken cancellation) =>
            WriteAsync($"the ledger of {player}", () => _cloudSave.SetProtectedItemAsync(_context, Token, Project, player.Value, new SetItemBody(LedgerKey, ValueOf(text), LockOf(expected)), cancellation));

        public Task<StoredText?> ReadReceiptAsync(ReceiptId receipt, CancellationToken cancellation) =>
            FirstAsync($"the record {receipt}", () => _cloudSave.GetPrivateCustomItemsAsync(_context, Token, Project, receipt.Value, new List<string> { ReceiptKey }, cancellationToken: cancellation));

        public Task<LockedWrite> WriteReceiptAsync(ReceiptId receipt, string text, WriteLock? expected, CancellationToken cancellation) =>
            WriteAsync($"the record {receipt}", () => _cloudSave.SetPrivateCustomItemAsync(_context, Token, Project, receipt.Value, new SetItemBody(ReceiptKey, ValueOf(text), LockOf(expected)), cancellation));

        public async Task DeleteReceiptAsync(ReceiptId receipt, CancellationToken cancellation) =>
            await DeleteAsync($"the record {receipt}", () => _cloudSave.DeletePrivateCustomItemsAsync(_context, Token, Project, receipt.Value, cancellation)).ConfigureAwait(false);

        public async Task DeletePlayerDataAsync(PlayerId player, CancellationToken cancellation)
        {
            string id = player.Value;
            await DeleteAsync($"the default items of {player}", () => _cloudSave.DeleteItemsAsync(_context, Token, Project, id, cancellation)).ConfigureAwait(false);
            await DeleteAsync($"the protected items of {player}", () => _cloudSave.DeleteProtectedItemsAsync(_context, Token, Project, id, cancellation)).ConfigureAwait(false);
            await DeleteAsync($"the private items of {player}", () => _cloudSave.DeletePrivateItemsAsync(_context, Token, Project, id, cancellation)).ConfigureAwait(false);
            await DeleteAsync($"the public items of {player}", () => _cloudSave.DeletePublicItemsAsync(_context, Token, Project, id, cancellation)).ConfigureAwait(false);
        }

        public async Task NoteTraceAsync(DateOnly day, ReceiptId receipt, CancellationToken cancellation) =>
            await WriteAsync($"the note of {TracesItemOf(day)}", () => _cloudSave.SetPrivateCustomItemAsync(_context, Token, Project, TracesItemOf(day), new SetItemBody(receipt.Value, true, LockOf(null)), cancellation)).ConfigureAwait(false);

        public async Task<IReadOnlyList<ReceiptId>> TracesOfAsync(DateOnly day, CancellationToken cancellation)
        {
            string item = TracesItemOf(day);
            var receipts = new List<ReceiptId>();
            string? after = null;
            do
            {
                GetKeysResponse? page = await CallAsync($"the note of {item}", () => _cloudSave.GetPrivateCustomKeysAsync(_context, Token, Project, item, after!, cancellation), nothingThere: null).ConfigureAwait(false);
                List<KeyMetadata> keys = page?.Results ?? new List<KeyMetadata>();
                receipts.AddRange(keys.Select(key => ReceiptId.Parse(key.Key)).OfType<ReceiptId>());
                after = keys.Count > 0 && !string.IsNullOrEmpty(page?.Links?.Next) ? keys[keys.Count - 1].Key : null;
            }
            while (after != null);
            return receipts;
        }

        public async Task DeleteTracesOfAsync(DateOnly day, CancellationToken cancellation) =>
            await DeleteAsync($"the note of {TracesItemOf(day)}", () => _cloudSave.DeletePrivateCustomItemsAsync(_context, Token, Project, TracesItemOf(day), cancellation)).ConfigureAwait(false);

        /// <summary>A time the module cannot read counts as no time: the next look reads Google's whole 30 days again, which marks nothing twice.</summary>
        public async Task<DateTimeOffset?> ReadVoidedSeenUntilAsync(CancellationToken cancellation)
        {
            StoredText? stored = await FirstAsync("the server's state", () => _cloudSave.GetPrivateCustomItemsAsync(_context, Token, Project, ServerStateItem, new List<string> { VoidedSeenUntilKey }, cancellationToken: cancellation)).ConfigureAwait(false);
            return stored != null && DateTimeOffset.TryParse(stored.Text.Trim('"'), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset until) ? until : null;
        }

        public async Task WriteVoidedSeenUntilAsync(DateTimeOffset until, CancellationToken cancellation) =>
            await WriteAsync("the server's state", () => _cloudSave.SetPrivateCustomItemAsync(_context, Token, Project, ServerStateItem, new SetItemBody(VoidedSeenUntilKey, ServerDocuments.InstantText(until), LockOf(null)), cancellation)).ConfigureAwait(false);

        /// <summary>
        /// The write lock as the SDK takes it: its text, or null for none (an unconditional write, which creates the
        /// item). The SDK's signature calls the parameter non-nullable but defaults it to null, as Cloud Save's
        /// documentation says to omit the lock when creating an item.
        /// </summary>
        private static string LockOf(WriteLock? expected) => expected?.Value!;

        /// <summary>The JSON object Cloud Save keeps for a document's text.</summary>
        private static JToken ValueOf(string text) => JToken.Parse(text);

        /// <summary>A document's text from the value Cloud Save hands back (an object the SDK read; a string only for a value someone stored as one).</summary>
        private static string TextOf(object? value) => value switch
        {
            null => "null",
            string text => text,
            JToken token => token.ToString(Formatting.None),
            _ => JToken.FromObject(value).ToString(Formatting.None),
        };

        private static async Task<StoredText?> FirstAsync(string what, Func<Task<ApiResponse<GetItemsResponse>>> read)
        {
            GetItemsResponse? items = await CallAsync(what, read, nothingThere: null).ConfigureAwait(false);
            Item? item = items?.Results?.FirstOrDefault();
            return item == null ? null : new StoredText(TextOf(item.Value), new WriteLock(item.WriteLock));
        }

        private static async Task<LockedWrite> WriteAsync(string what, Func<Task<ApiResponse<SetItemResponse>>> write)
        {
            try
            {
                ApiResponse<SetItemResponse> response = await write().ConfigureAwait(false);
                return string.IsNullOrEmpty(response.Data?.WriteLock)
                    ? throw new ServerStoreUnavailableException($"Cloud Save wrote {what} but gave no write lock.")
                    : new LockedWrite.Written(new WriteLock(response.Data.WriteLock));
            }
            catch (ApiException failure) when (StatusOf(failure) == HttpStatusCode.Conflict)
            {
                return new LockedWrite.Conflict();
            }
            catch (ApiException failure)
            {
                throw Unavailable(what, failure);
            }
        }

        /// <summary>Deletes; true when there was something to delete, false when Cloud Save had nothing there (404), which is what a delete asks for all the same.</summary>
        private static async Task<bool> DeleteAsync(string what, Func<Task<ApiResponse>> delete)
        {
            try
            {
                await delete().ConfigureAwait(false);
                return true;
            }
            catch (ApiException failure) when (StatusOf(failure) == HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (ApiException failure)
            {
                throw Unavailable(what, failure);
            }
        }

        private static async Task<T?> CallAsync<T>(string what, Func<Task<ApiResponse<T>>> call, T? nothingThere)
            where T : class
        {
            try
            {
                return (await call().ConfigureAwait(false)).Data;
            }
            catch (ApiException failure) when (StatusOf(failure) == HttpStatusCode.NotFound)
            {
                return nothingThere;
            }
            catch (ApiException failure)
            {
                throw Unavailable(what, failure);
            }
        }

        private static HttpStatusCode? StatusOf(ApiException failure) => failure.Type == ApiExceptionType.Http ? failure.Response?.StatusCode : null;

        private static ServerStoreUnavailableException Unavailable(string what, ApiException failure) =>
            new ServerStoreUnavailableException($"Cloud Save refused {what}: {failure.Type} {(int?)StatusOf(failure)}.", failure);
    }
}
