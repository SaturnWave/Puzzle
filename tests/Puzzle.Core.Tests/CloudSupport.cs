using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Museum;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the cloud's tests share: the documents' JSON as Unity maps it (System.Text.Json with fields), a game that
/// holds a progress, a clock the test moves, a device of the player (its progress, its cloud.json in memory and its
/// sync, against a cloud it shares with the player's other devices), and the store's purchases as the purchases'
/// stage will deliver them, with the server's ledger numbers.
/// </summary>
internal static class CloudSupport
{
    /// <summary>The account the devices of the tests sign in to.</summary>
    public static readonly CloudPlayerId Player = new("player-1");

    /// <summary>Noon on 3 November 2026 in Istanbul, a whole second.</summary>
    public static readonly DateTimeOffset Noon = new(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(3));

    /// <summary>The tests' economy in a build that sells for money: a small pack (500 coins), a large one (2 500), a starter kit (1 500 and a start booster of each).</summary>
    public static readonly EconomyRules Rules = MetaSupport.Rules(MetaSupport.InAStoreBuild);

    public static readonly IDocumentJson Json = new TextJsonDocuments();

    public static StoreProduct Small => Rules.Shop.Store.Find("coins.small")!;

    public static StoreProduct Large => Rules.Shop.Store.Find("coins.large")!;

    /// <summary>A new player of the tests' economy: 100 coins, a rocket-and-bomb and two shuffles.</summary>
    public static PlayerProgress NewPlayer() => PlayerProgress.NewPlayer(Rules.StartingWallet);

    /// <summary>The progress with this many coins, all else as it is.</summary>
    public static PlayerProgress WithCoins(this PlayerProgress progress, long coins) => progress.WithWallet(progress.Wallet.WithCoins(new Coins(coins)));

    /// <summary>The progress with so many more levels won, each paying the win's coins with no move left.</summary>
    public static PlayerProgress Won(this PlayerProgress progress, int levels)
    {
        PlayerProgress won = progress;
        for (int level = 0; level < levels; level++)
        {
            won = won.WithLevelWon(levelCount: 1000).WithCoins(won.Wallet.Coins.Amount + Rules.Win.Of(movesLeft: 0).Amount);
        }

        return won;
    }

    /// <summary>A payment the store took, delivered as the purchases' stage will: its contents and id, and the ledger number the server gave it.</summary>
    public static PlayerProgress Bought(this PlayerProgress progress, StoreProduct product, string payment, long number)
    {
        var given = Assert.IsType<Delivery.Given>(new WalletLedger(Rules).Deliver(progress, product, payment));
        return given.Progress.WithPurchases(given.Progress.Purchases.WithLedgerNumber(new LedgerNumber(number)));
    }

    /// <summary>The server's ledger with these payments, every one valid, verified on the day of the tests.</summary>
    public static string LedgerText(params (long Number, StoreProduct Product, string Order)[] payments) =>
        Json.Write(new PurchaseLedgerDocument
        {
            next = payments.Length + 1,
            items = payments.Select(payment => new LedgerEntryDocument
            {
                seq = payment.Number,
                store = "GooglePlay",
                product = payment.Product.IdSuffix,
                order = payment.Order,
                at = "2026-11-03T09:00:00Z",
                state = "valid",
            }).ToArray(),
        });

    public static PurchaseLedger LedgerOf(params (long Number, StoreProduct Product, string Order)[] payments) =>
        PurchaseLedger.Read(Json.Read<PurchaseLedgerDocument>(LedgerText(payments))).Ledger;

    /// <summary>A save's text as a game of this version writes it.</summary>
    public static string SaveText(PlayerProgress progress, string write = "write-0", long revision = 1) =>
        CloudEnvelope.Write(new CloudSave(progress, "1.1.0", revision, Noon, "other-writer", new[] { write }), Json);

    /// <summary>The progress a save's text holds.</summary>
    public static PlayerProgress ProgressIn(CloudItem? save) =>
        Assert.IsType<CloudSaveReading.Sound>(CloudEnvelope.Read(Assert.IsType<CloudItem>(save).Text, Json, Rules.StartingWallet)).Save.Progress;
}

/// <summary>The documents' JSON as Unity's reader maps them: public fields, every one written.</summary>
internal sealed class TextJsonDocuments : IDocumentJson
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    public string Write(object document) => JsonSerializer.Serialize(document, document.GetType(), AsUnityMapsIt);

    public T? Read<T>(string text)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(text, AsUnityMapsIt);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>A game's session as the sync sees it: the progress it holds, which a test plays on and the sync replaces.</summary>
internal sealed class HeldProgress : IProgressHolder
{
    public HeldProgress(PlayerProgress start)
    {
        Current = start;
    }

    public PlayerProgress Current { get; private set; }

    public ProgressKeeping Keeping { get; set; } = ProgressKeeping.AfterClosing;

    public bool IsTesterMode { get; set; }

    public int TimesAdopted { get; private set; }

    public void Adopt(PlayerProgress progress)
    {
        Current = progress;
        TimesAdopted++;
    }

    /// <summary>The player plays: the progress changes as the game changes it.</summary>
    public void Play(Func<PlayerProgress, PlayerProgress> change) => Current = change(Current);
}

/// <summary>A clock the test moves, a whole second at a time.</summary>
internal sealed class MovableClock : IWallClock
{
    public MovableClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; private set; }

    public void Passes(TimeSpan time) => Now += time;
}

/// <summary>A device of the player: its progress, its cloud.json in memory and its sync, against the cloud all the player's devices share.</summary>
internal sealed class Device
{
    private int _writes;

    public Device(FakeCloudSave cloud, MovableClock clock, PlayerProgress start, string writer = "device-writer", CloudState? state = null)
    {
        Progress = new HeldProgress(start);
        States = new CloudStateInMemory(state ?? CloudState.New(writer));
        Sync = new CloudSync(cloud, States, Progress, CloudSupport.Rules, CloudSupport.Json, clock, "1.1.0", () => $"{writer}-write-{++_writes}");
    }

    public HeldProgress Progress { get; }

    public CloudStateInMemory States { get; }

    public CloudSync Sync { get; }

    public Task<SyncOutcome> SyncsAsync(SyncTrigger trigger) => Sync.SyncAsync(CloudSupport.Player, trigger, CancellationToken.None);

    public Task<SyncOutcome> SyncsAsync(CloudPlayerId player, SyncTrigger trigger) => Sync.SyncAsync(player, trigger, CancellationToken.None);
}

/// <summary>A cloud whose reads wait until the test lets them through: two syncs at once on one device.</summary>
internal sealed class GatedCloud : ICloudSave
{
    private readonly ICloudSave _cloud;
    private TaskCompletionSource<bool> _gate = new();

    public GatedCloud(ICloudSave cloud)
    {
        _cloud = cloud;
    }

    public void Opens()
    {
        TaskCompletionSource<bool> gate = _gate;
        _gate = new TaskCompletionSource<bool>();
        gate.SetResult(true);
    }

    public async Task<CloudRead> ReadSaveAsync(CancellationToken cancel)
    {
        await _gate.Task;
        return await _cloud.ReadSaveAsync(cancel);
    }

    public Task<CloudRead> ReadLedgerAsync(CancellationToken cancel) => _cloud.ReadLedgerAsync(cancel);

    public Task<CloudWrite> WriteSaveAsync(string text, CloudWriteLock? expected, CancellationToken cancel) => _cloud.WriteSaveAsync(text, expected, cancel);
}
