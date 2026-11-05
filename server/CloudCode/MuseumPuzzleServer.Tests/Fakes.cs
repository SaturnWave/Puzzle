using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;

namespace MuseumPuzzle.Server.Tests;

/// <summary>A clock the test sets, and that moves on by <see cref="Step"/> each time it is read (zero by default).</summary>
public sealed class HandClock : IClock
{
    public HandClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }

    public TimeSpan Step { get; set; } = TimeSpan.Zero;

    public DateTimeOffset UtcNow
    {
        get
        {
            DateTimeOffset now = Now;
            Now += Step;
            return now;
        }
    }
}

/// <summary>The Secret Manager as a dictionary; can be made unreachable.</summary>
public sealed class FakeSecrets : ISecrets
{
    private readonly Dictionary<SecretName, string> _values = new();

    public bool IsUnreachable { get; set; }

    public FakeSecrets With(SecretName name, string value)
    {
        _values[name] = value;
        return this;
    }

    public Task<string?> ReadAsync(SecretName name, CancellationToken cancellation)
    {
        if (IsUnreachable)
        {
            throw new SecretsUnavailableException($"The fake Secret Manager is unreachable ({name}).");
        }

        return Task.FromResult(_values.TryGetValue(name, out string? value) ? value : null);
    }
}

/// <summary>
/// Cloud Save in memory, with Cloud Save's write locks: every write gives a new lock, a write with a lock that is not
/// the item's is a conflict, a write with no lock writes whatever is there. It can be made unreachable, and a test
/// can slip another writer's change in just before the next write of an item (<see cref="BeforeNextWrite"/>).
/// </summary>
public sealed class FakeServerStore : IServerStore
{
    private readonly Dictionary<string, (string Text, int Lock)> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<DateOnly, SortedSet<string>> _traces = new();
    private int _locks;

    public bool IsUnreachable { get; set; }

    /// <summary>Items whose writes fail (as if Cloud Save refused them), by the item's name: "ledger:p1", "rcpt-…".</summary>
    public HashSet<string> RefusedWrites { get; } = new(StringComparer.Ordinal);

    /// <summary>Runs once, just before the next write of this item, as another call of the module would.</summary>
    public Dictionary<string, Action<FakeServerStore>> BeforeNextWrite { get; } = new(StringComparer.Ordinal);

    public int Writes { get; private set; }

    public HashSet<PlayerId> PlayersDeleted { get; } = new();

    public DateTimeOffset? VoidedSeenUntil { get; set; }

    public static string LedgerItem(PlayerId player) => $"ledger:{player}";

    public string? TextOf(string item) => _items.TryGetValue(item, out var held) ? held.Text : null;

    public Ledger LedgerOf(PlayerId player) => TextOf(LedgerItem(player)) is string text ? ServerDocuments.LedgerFrom(text) : Ledger.Empty;

    public ReceiptRecord? RecordOf(ReceiptId receipt) => TextOf(receipt.Value) is string text ? ServerDocuments.ReceiptFrom(text) : null;

    public bool HasPlayerData(PlayerId player) => _items.Keys.Any(key => key == LedgerItem(player) || key == $"save:{player}");

    /// <summary>Puts a document in place as Cloud Save would hold it, outside the module.</summary>
    public void Put(string item, string text) => _items[item] = (text, ++_locks);

    public void PutLedger(PlayerId player, Ledger ledger) => Put(LedgerItem(player), ServerDocuments.TextOf(ledger));

    public void PutRecord(ReceiptId receipt, ReceiptRecord record) => Put(receipt.Value, ServerDocuments.TextOf(record));

    public IReadOnlyCollection<string> TracesNotedOn(DateOnly day) => _traces.TryGetValue(day, out var noted) ? noted : (IReadOnlyCollection<string>)Array.Empty<string>();

    public Task<StoredText?> ReadLedgerAsync(PlayerId player, CancellationToken cancellation) => Read(LedgerItem(player));

    public Task<LockedWrite> WriteLedgerAsync(PlayerId player, string text, WriteLock? expected, CancellationToken cancellation) => Write(LedgerItem(player), text, expected);

    public Task<StoredText?> ReadReceiptAsync(ReceiptId receipt, CancellationToken cancellation) => Read(receipt.Value);

    public Task<LockedWrite> WriteReceiptAsync(ReceiptId receipt, string text, WriteLock? expected, CancellationToken cancellation) => Write(receipt.Value, text, expected);

    public Task DeleteReceiptAsync(ReceiptId receipt, CancellationToken cancellation)
    {
        Reachable();
        _items.Remove(receipt.Value);
        return Task.CompletedTask;
    }

    public Task DeletePlayerDataAsync(PlayerId player, CancellationToken cancellation)
    {
        Reachable();
        _items.Remove(LedgerItem(player));
        _items.Remove($"save:{player}");
        PlayersDeleted.Add(player);
        return Task.CompletedTask;
    }

    public Task NoteTraceAsync(DateOnly day, ReceiptId receipt, CancellationToken cancellation)
    {
        Reachable();
        if (!_traces.TryGetValue(day, out var noted))
        {
            _traces[day] = noted = new SortedSet<string>(StringComparer.Ordinal);
        }

        noted.Add(receipt.Value);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReceiptId>> TracesOfAsync(DateOnly day, CancellationToken cancellation)
    {
        Reachable();
        IReadOnlyList<ReceiptId> noted = TracesNotedOn(day).Select(text => ReceiptId.Parse(text)!.Value).ToArray();
        return Task.FromResult(noted);
    }

    public Task DeleteTracesOfAsync(DateOnly day, CancellationToken cancellation)
    {
        Reachable();
        _traces.Remove(day);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> ReadVoidedSeenUntilAsync(CancellationToken cancellation)
    {
        Reachable();
        return Task.FromResult(VoidedSeenUntil);
    }

    public Task WriteVoidedSeenUntilAsync(DateTimeOffset until, CancellationToken cancellation)
    {
        Reachable();
        VoidedSeenUntil = until;
        return Task.CompletedTask;
    }

    private Task<StoredText?> Read(string item)
    {
        Reachable();
        return Task.FromResult(_items.TryGetValue(item, out var held) ? new StoredText(held.Text, new WriteLock(held.Lock.ToString(CultureInfo.InvariantCulture))) : null);
    }

    private Task<LockedWrite> Write(string item, string text, WriteLock? expected)
    {
        Reachable();
        if (BeforeNextWrite.Remove(item, out Action<FakeServerStore>? other))
        {
            other(this);
        }

        if (RefusedWrites.Contains(item))
        {
            throw new ServerStoreUnavailableException($"The fake Cloud Save refuses {item}.");
        }

        bool stale = expected is WriteLock given && (!_items.TryGetValue(item, out var held) || held.Lock.ToString(CultureInfo.InvariantCulture) != given.Value);
        if (stale)
        {
            return Task.FromResult<LockedWrite>(new LockedWrite.Conflict());
        }

        Writes++;
        _items[item] = (text, ++_locks);
        return Task.FromResult<LockedWrite>(new LockedWrite.Written(new WriteLock(_locks.ToString(CultureInfo.InvariantCulture))));
    }

    private void Reachable()
    {
        if (IsUnreachable)
        {
            throw new ServerStoreUnavailableException("The fake Cloud Save is unreachable.");
        }
    }
}

/// <summary>A request the scripted network saw: its method, its address, its authorization and its body.</summary>
public sealed record SeenRequest(HttpMethod Method, string Url, string? Authorization, string Body);

/// <summary>
/// The network as the tests script it: each address (by its start) answers what the test says, or is unreachable;
/// an address nobody scripted fails the test. Nothing leaves the machine.
/// </summary>
public sealed class ScriptedNetwork : HttpMessageHandler
{
    private readonly List<(string Start, Func<SeenRequest, (HttpStatusCode, string)> Answer)> _routes = new();

    public List<SeenRequest> Seen { get; } = new();

    public HashSet<string> Unreachable { get; } = new(StringComparer.Ordinal);

    public HttpClient Client() => new(this, disposeHandler: false);

    /// <summary>Answers requests to addresses starting with <paramref name="start"/>; a later route for a longer start wins.</summary>
    public ScriptedNetwork On(string start, Func<SeenRequest, (HttpStatusCode, string)> answer)
    {
        _routes.Add((start, answer));
        return this;
    }

    public ScriptedNetwork On(string start, HttpStatusCode status, string body) => On(start, _ => (status, body));

    public int CountTo(string start) => Seen.Count(request => request.Url.StartsWith(start, StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string url = request.RequestUri!.ToString();
        string body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var seen = new SeenRequest(request.Method, url, request.Headers.Authorization?.ToString(), body);
        Seen.Add(seen);
        if (Unreachable.Any(start => url.StartsWith(start, StringComparison.Ordinal)))
        {
            throw new HttpRequestException($"The scripted network does not reach {url}.");
        }

        var route = _routes.Where(candidate => url.StartsWith(candidate.Start, StringComparison.Ordinal)).OrderByDescending(candidate => candidate.Start.Length).FirstOrDefault();
        Assert.True(route.Answer != null, $"Nobody scripted an answer for {request.Method} {url}.");
        (HttpStatusCode status, string answer) = route.Answer!(seen);
        return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
    }
}
