using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using MuseumPuzzle.Server.Endpoints;
using MuseumPuzzle.Server.Purchases;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Services.CloudCode.Core;
using static MuseumPuzzle.Server.Tests.TestWorld;

namespace MuseumPuzzle.Server.Tests;

/// <summary>The endpoints: who may call what, what the game sends, the answers' words, and the layout Unity's Deployment window deploys.</summary>
public sealed class EndpointTests : IDisposable
{
    private readonly TestGoogle _google = TestGoogle.Create();
    private readonly TestApple _apple = TestApple.Create();
    private readonly HandClock _clock = new(Day);
    private readonly FakeServerStore _cloud = new();

    public void Dispose()
    {
        _google.Dispose();
        _apple.Dispose();
    }

    [Fact]
    public async Task Only_a_player_has_payments_refunds_and_data()
    {
        ModuleCalls calls = Calls(new ScriptedNetwork());

        Assert.Equal(UnavailableReason.NotAPlayer, Assert.IsType<PurchaseVerdict.Unavailable>(await calls.ValidatePurchaseAsync(string.Empty, "GooglePlay", Pouch, "t", "t", false, _cloud, new FakeSecrets())).Reason);
        Assert.Equal(UnavailableReason.NotAPlayer, Assert.IsType<RefundOutcome.Unavailable>(await calls.CheckRefundsAsync(null, _cloud, new FakeSecrets())).Reason);
        Assert.Equal(UnavailableReason.NotAPlayer, Assert.IsType<DeletionOutcome.Unavailable>(await calls.DeleteMyDataAsync(string.Empty, _cloud)).Reason);
        Assert.Empty(_cloud.PlayersDeleted);
    }

    [Fact]
    public async Task Only_the_scheduler_runs_the_daily_jobs()
    {
        ModuleCalls calls = Calls(new ScriptedNetwork());

        Assert.IsType<ChoreOutcome.Refused>(await calls.PollGoogleVoidedAsync(Ada.Value, _cloud, _google.Secrets()));
        Assert.IsType<ChoreOutcome.Refused>(await calls.ForgetOldTracesAsync(Ada.Value, _cloud));
        Assert.IsType<ChoreOutcome.Done>(await calls.ForgetOldTracesAsync(null, _cloud));
    }

    [Fact]
    public async Task A_store_the_game_does_not_sell_through_is_refused()
    {
        PurchaseVerdict verdict = await Calls(new ScriptedNetwork()).ValidatePurchaseAsync(Ada.Value, "Steam", Pouch, "t", "t", false, _cloud, new FakeSecrets());

        Assert.Equal(InvalidReason.UnknownStore, Assert.IsType<PurchaseVerdict.Invalid>(verdict).Reason);
    }

    [Fact]
    public async Task Google_is_asked_by_the_purchase_token_the_game_sends_as_the_payload()
    {
        ScriptedNetwork network = TestGoogle.Network().On(TestGoogle.PurchaseUrl("the-purchase-token"), HttpStatusCode.OK, TestGoogle.Purchase(StoreIdOf(Pouch)));

        PurchaseVerdict verdict = await Calls(network).ValidatePurchaseAsync(Ada.Value, "GooglePlay", Pouch, "GPA.ignored", "the-purchase-token", false, _cloud, _google.Secrets());

        Assert.Equal(1, Assert.IsType<PurchaseVerdict.Valid>(verdict).Number.Value);
        Assert.Equal(ReceiptId.Of(PurchaseStore.GooglePlay, "the-purchase-token"), _cloud.LedgerOf(Ada).Entries.Single().Receipt);
    }

    [Fact]
    public async Task Apple_is_asked_by_the_transaction_id_with_the_signed_transaction_as_the_payload()
    {
        string signed = _apple.Transaction("2000000912345678", StoreIdOf(Pouch));
        ScriptedNetwork network = new ScriptedNetwork().On(TestApple.TransactionUrl(MuseumPuzzle.Server.AppStore.AppleEnvironment.Production, "2000000912345678"), HttpStatusCode.OK, TestApple.TransactionInfo(signed));

        PurchaseVerdict verdict = await Calls(network).ValidatePurchaseAsync(Ada.Value, "AppleAppStore", Pouch, "2000000912345678", signed, false, _cloud, _apple.Secrets());

        Assert.IsType<PurchaseVerdict.Valid>(verdict);
        Assert.Equal("2000000912345678", _cloud.LedgerOf(Ada).Entries.Single().Order);
    }

    [Fact]
    public void The_answers_are_the_words_the_game_reads()
    {
        // tests/fixtures/cloud-code/replies.json is the game's side of the contract (stage 4 reads it).
        JsonNode pinned = JsonNode.Parse(RepoFiles.Read("tests", "fixtures", "cloud-code", "replies.json"))!;
        var written = new JsonObject
        {
            ["validatePurchase"] = new JsonObject
            {
                ["valid"] = Written(ModuleReplies.Of(new PurchaseVerdict.Valid(new LedgerNumber(3), isTest: false))),
                ["alreadyValidated"] = Written(ModuleReplies.Of(new PurchaseVerdict.AlreadyValidated(new LedgerNumber(3), isTest: true))),
                ["pending"] = Written(ModuleReplies.Of(new PurchaseVerdict.Pending())),
                ["invalid"] = Written(ModuleReplies.Of(new PurchaseVerdict.Invalid(InvalidReason.AnotherPlayers))),
                ["unavailable"] = Written(ModuleReplies.Of(new PurchaseVerdict.Unavailable(UnavailableReason.StoreUnreachable))),
            },
            ["checkRefunds"] = new JsonObject
            {
                ["checked"] = Written(ModuleReplies.Of(new RefundOutcome.Checked(new[] { new LedgerNumber(4) }))),
                ["notDue"] = Written(ModuleReplies.Of(new RefundOutcome.NotDue())),
                ["nothingToCheck"] = Written(ModuleReplies.Of(new RefundOutcome.NothingToCheck())),
                ["unavailable"] = Written(ModuleReplies.Of(new RefundOutcome.Unavailable(UnavailableReason.CloudSaveUnreachable))),
            },
            ["deleteMyData"] = new JsonObject
            {
                ["deleted"] = Written(ModuleReplies.Of(new DeletionOutcome.Deleted())),
                ["unavailable"] = Written(ModuleReplies.Of(new DeletionOutcome.Unavailable(UnavailableReason.CloudSaveUnreachable))),
            },
            ["invalidReasons"] = new JsonArray(Enum.GetValues<InvalidReason>().Select(reason => (JsonNode?)ModuleReplies.WordOf(reason)).ToArray()),
            ["unavailableReasons"] = new JsonArray(Enum.GetValues<UnavailableReason>().Select(reason => (JsonNode?)ModuleReplies.WordOf(reason)).ToArray()),
        };

        foreach (KeyValuePair<string, JsonNode?> part in written)
        {
            Assert.True(JsonNode.DeepEquals(pinned[part.Key], part.Value), $"{part.Key}: {part.Value?.ToJsonString()}");
        }
    }

    [Fact]
    public void Cloud_codes_camel_case_writer_writes_the_same_words()
    {
        var camel = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        PurchaseReply reply = ModuleReplies.Of(new PurchaseVerdict.Valid(new LedgerNumber(3), isTest: false));

        Assert.Equal(JsonConvert.SerializeObject(reply), JsonConvert.SerializeObject(reply, camel));
    }

    [Fact]
    public void Unity_finds_one_main_project_named_as_the_module_and_publishes_nothing_else()
    {
        string solutionFolder = RepoFiles.PathOf("server", "CloudCode");
        string[] profiles = Directory.GetFiles(solutionFolder, "*.pubxml", SearchOption.AllDirectories);

        string profile = Assert.Single(profiles);
        string mainProject = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(profile)))!, "*.csproj"));
        string moduleName = Path.GetFileNameWithoutExtension(mainProject);
        Assert.Equal(PurchaseModule.ModuleName, moduleName);
        Assert.Matches("^[a-zA-Z][a-zA-Z_0-9]*$", moduleName);
        Assert.Equal(moduleName, typeof(PurchaseModule).Assembly.GetName().Name);
        Assert.Equal("false", XDocument.Load(RepoFiles.PathOf("server", "CloudCode", "MuseumPuzzleServer.Tests", "MuseumPuzzleServer.Tests.csproj")).Descendants("IsPublishable").Single().Value);
        string solution = File.ReadAllText(Path.Combine(solutionFolder, "MuseumPuzzleServer.sln"));
        Assert.Contains("\"MuseumPuzzleServer\\MuseumPuzzleServer.csproj\"", solution, StringComparison.Ordinal);
    }

    [Fact]
    public void The_editors_module_reference_points_at_the_solution()
    {
        string reference = RepoFiles.PathOf("unity", "Assets", "CloudCode", "MuseumPuzzleServer.ccmr");
        string modulePath = JsonNode.Parse(File.ReadAllText(reference))!["modulePath"]!.GetValue<string>();

        Assert.True(File.Exists(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(reference)!, modulePath))), modulePath);
        Assert.True(File.Exists(reference + ".meta"));
    }

    [Fact]
    public void Every_endpoint_is_a_cloud_code_function_and_the_schedules_call_the_jobs_by_name()
    {
        string[] functions = typeof(PurchaseModule).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.GetCustomAttribute<CloudCodeFunctionAttribute>()?.Name)
            .OfType<string>()
            .ToArray();
        Assert.Equal(new[] { "ValidatePurchase", "CheckRefunds", "DeleteMyData", "PollGoogleVoided", "ForgetOldTraces" }, functions);
        Assert.DoesNotContain(
            typeof(PurchaseModule).Assembly.GetTypes().Where(type => type != typeof(PurchaseModule)).SelectMany(type => type.GetMethods()),
            method => method.GetCustomAttribute<CloudCodeFunctionAttribute>() != null);

        JsonNode schedules = JsonNode.Parse(RepoFiles.Read("server", "CloudCode", "schedules", "daily-chores.sched"))!;
        JsonNode triggers = JsonNode.Parse(RepoFiles.Read("server", "CloudCode", "schedules", "daily-chores.tr"))!;
        string[] events = schedules["Configs"]!.AsObject().Select(config => $"com.unity.services.scheduler.{config.Value!["EventName"]!.GetValue<string>()}.v1").ToArray();
        foreach (JsonNode? trigger in triggers["Configs"]!.AsArray())
        {
            Assert.Contains(trigger!["EventType"]!.GetValue<string>(), events);
            Match urn = Regex.Match(trigger["ActionUrn"]!.GetValue<string>(), "^urn:ugs:cloud-code:(?<module>[^/]+)/(?<function>.+)$");
            Assert.Equal(PurchaseModule.ModuleName, urn.Groups["module"].Value);
            Assert.Contains(urn.Groups["function"].Value, functions);
        }

        Assert.Equal(events.Length, triggers["Configs"]!.AsArray().Count);
    }

    private static JsonNode Written(object reply) => JsonNode.Parse(JsonConvert.SerializeObject(reply))!;

    private ModuleCalls Calls(ScriptedNetwork network) =>
        new(new ModuleParts(Shipped, network.Client(), _apple.Signatures, _clock), NullLogger.Instance);
}
