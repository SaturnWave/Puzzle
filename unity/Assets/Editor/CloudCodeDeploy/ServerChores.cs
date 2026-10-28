#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.Core.Editor;
using Unity.Services.Core.Editor.Environments;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Sets up the Cloud Code module's daily chores (docs/CLOUD_CODE_DEPLOY.md §4) in environments of the linked Unity
    /// Cloud project through Unity's Scheduler and Triggers admin APIs, as the Dashboard's forms would, without anyone at
    /// the screen (2026-10-28, the owner's "sunucuyu kur"): the schedules of server/CloudCode/schedules/daily-chores.sched
    /// and the triggers of daily-chores.tr, the files being the one source of what is set up. A schedule or a trigger of
    /// the same name already there is left as it is, so the tool can run again. It calls as the Editor's signed-in Unity
    /// user, with the services gateway token Unity's own Editor packages use; no service account and no secret.
    /// <para>
    /// usage: Unity.exe -projectPath unity -executeMethod Puzzle.Client.EditorTools.ServerChores.Run
    /// [-choresEnvironment development|production] -logFile out/unity/server-chores.log
    /// Without the argument both environments are set up, development first. WITHOUT -batchmode and -quit: the Editor's
    /// sign-in and the answers come on later Editor updates; the tool ends the Editor. Each step is written on a
    /// "Server chores:" line; the last line is the verdict.
    /// </para>
    /// </summary>
    public static class ServerChores
    {
        private const string EnvironmentArgument = "-choresEnvironment";
        private const string SchedulerApi = "https://services.api.unity.com/scheduler/v1";
        private const string TriggersApi = "https://services.api.unity.com/triggers/v1";

        /// <summary>The environments the module is deployed to, in the order they are set up: development first. A closed set.</summary>
        private static readonly IReadOnlyList<string> Environments = new[] { "development", "production" };

        private static readonly TimeSpan PatienceForTheSignIn = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan Patience = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan PatiencePerRequest = TimeSpan.FromSeconds(30);

        private static Task<bool>? _setUp;
        private static DateTime _deadline;
        private static IReadOnlyList<string> _environments = Environments;

        public static void Run()
        {
            if (EnvironmentsNamed(Environment.GetCommandLineArgs()) is not IReadOnlyList<string> environments)
            {
                Finish(passed: false, $"{EnvironmentArgument} must name one of: {string.Join(", ", Environments)}");
                return;
            }

            _environments = environments;
            _deadline = DateTime.UtcNow + PatienceForTheSignIn;
            Say($"environments {string.Join(", ", environments)}; waiting for the Editor's Unity sign-in (at most {PatienceForTheSignIn.TotalMinutes:F0} min)");
            EditorApplication.update += Watch;
        }

        private static bool IsSignedIn => !string.IsNullOrEmpty(CloudProjectSettings.userId) && !string.IsNullOrEmpty(CloudProjectSettings.projectId);

        private static void Watch()
        {
            if (_setUp == null)
            {
                if (IsSignedIn)
                {
                    Say($"project {CloudProjectSettings.projectId}, the Editor's user signed in");
                    _deadline = DateTime.UtcNow + Patience;
                    _setUp = SetUpAsync(CloudProjectSettings.projectId);
                }
                else if (DateTime.UtcNow > _deadline)
                {
                    EditorApplication.update -= Watch;
                    Finish(passed: false, $"the Editor did not sign in to Unity within {PatienceForTheSignIn.TotalMinutes:F0} min (is the Hub signed in?)");
                }

                return;
            }

            if (_setUp.IsCompleted)
            {
                EditorApplication.update -= Watch;
                bool passed = _setUp.Status == TaskStatus.RanToCompletion && _setUp.Result;
                string fault = _setUp.Exception?.GetBaseException() is Exception thrown ? $"; the set-up failed: {thrown.GetType().Name}: {thrown.Message}" : string.Empty;
                Finish(passed, $"environments {string.Join(", ", _environments)}{fault}");
                return;
            }

            if (DateTime.UtcNow > _deadline)
            {
                EditorApplication.update -= Watch;
                Finish(passed: false, $"the set-up did not finish within {Patience.TotalMinutes:F0} min");
            }
        }

        /// <summary>Every environment named, each with its schedules and then its triggers; true when all of them are there at the end.</summary>
        private static async Task<bool> SetUpAsync(string projectId)
        {
            IReadOnlyList<ScheduleConfig> schedules = ScheduleConfig.ReadAll(ChoresFile("daily-chores.sched"));
            IReadOnlyList<TriggerConfig> triggers = TriggerConfig.ReadAll(ChoresFile("daily-chores.tr"));
            Say($"to set up: schedules {string.Join(", ", schedules.Select(schedule => schedule.Name))}; triggers {string.Join(", ", triggers.Select(trigger => trigger.Name))}");

            IEnvironmentsApi environments = EnvironmentsApi.Instance;
            await environments.RefreshAsync();
            string token = await new AccessTokens().GetServicesGatewayTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Say("Unity gave the Editor no services token");
                return false;
            }

            using var http = new HttpClient { Timeout = PatiencePerRequest };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            foreach (string name in _environments)
            {
                if (environments.Environments.Where(known => known.Name == name).Select(known => (Guid?)known.Id).FirstOrDefault() is not Guid environmentId)
                {
                    Say($"the project has no environment \"{name}\"");
                    return false;
                }

                Say($"environment \"{name}\" ({environmentId})");
                string schedulerConfigs = $"{SchedulerApi}/projects/{projectId}/environments/{environmentId}/configs";
                if (!await EnsureAllAsync(http, schedulerConfigs + "?state=active", schedulerConfigs, schedules.Select(schedule => (schedule.Name, schedule.Body))))
                {
                    return false;
                }

                string triggerConfigs = $"{TriggersApi}/projects/{projectId}/environments/{environmentId}/configs";
                if (!await EnsureAllAsync(http, triggerConfigs, triggerConfigs, triggers.Select(trigger => (trigger.Name, trigger.Body))))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Creates each config the list does not hold by name; true when every one is there afterwards.</summary>
        private static async Task<bool> EnsureAllAsync(HttpClient http, string listUrl, string createUrl, IEnumerable<(string Name, JObject Body)> wanted)
        {
            HttpResponseMessage listed = await http.GetAsync(listUrl);
            string listedText = await listed.Content.ReadAsStringAsync();
            if (!listed.IsSuccessStatusCode)
            {
                Say($"GET {Shorten(listUrl)}: {(int)listed.StatusCode} {listed.ReasonPhrase}: {listedText}");
                return false;
            }

            HashSet<string> there = NamesIn(listedText);
            foreach ((string name, JObject body) in wanted)
            {
                if (there.Contains(name))
                {
                    Say($"\"{name}\" is there already: left as it is");
                    continue;
                }

                using var content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                HttpResponseMessage created = await http.PostAsync(createUrl, content);
                string createdText = await created.Content.ReadAsStringAsync();
                if (!created.IsSuccessStatusCode)
                {
                    Say($"POST {Shorten(createUrl)} \"{name}\": {(int)created.StatusCode} {created.ReasonPhrase}: {createdText}");
                    return false;
                }

                Say($"\"{name}\" created: {createdText}");
            }

            return true;
        }

        /// <summary>The names of the configs a list answer holds, whether it is an array or an object with a "configs" array.</summary>
        private static HashSet<string> NamesIn(string listed)
        {
            JToken parsed = JToken.Parse(listed);
            IEnumerable<JToken> configs = parsed is JArray array ? array : parsed["configs"] as JArray ?? Enumerable.Empty<JToken>();
            return new HashSet<string>(configs.Select(config => (string?)config["name"]).OfType<string>(), StringComparer.Ordinal);
        }

        private static string ChoresFile(string fileName) =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.Parent!.FullName, "server", "CloudCode", "schedules", fileName);

        private static string Shorten(string url) => url.Replace("https://services.api.unity.com", string.Empty);

        /// <summary>The environments the command line names, or both when it names none; nothing when it names one the module does not have.</summary>
        private static IReadOnlyList<string>? EnvironmentsNamed(string[] arguments)
        {
            int at = Array.IndexOf(arguments, EnvironmentArgument);
            if (at < 0)
            {
                return Environments;
            }

            string? named = at + 1 < arguments.Length ? arguments[at + 1] : null;
            return named != null && Environments.Contains(named) ? new[] { named } : null;
        }

        private static void Say(string line) => Debug.Log($"Server chores: {line}");

        private static void Finish(bool passed, string report)
        {
            Debug.Log($"Server chores: {(passed ? "PASSED" : "FAILED")}: {report}");
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary>A schedule of the .sched file, as the Scheduler admin API creates it.</summary>
        private sealed class ScheduleConfig
        {
            private ScheduleConfig(string name, JObject body)
            {
                Name = name;
                Body = body;
            }

            public string Name { get; }

            public JObject Body { get; }

            /// <exception cref="IOException">The file cannot be read.</exception>
            /// <exception cref="JsonException">The file is not the schedules' JSON.</exception>
            public static IReadOnlyList<ScheduleConfig> ReadAll(string path)
            {
                JObject configs = JObject.Parse(File.ReadAllText(path))["Configs"] as JObject
                    ?? throw new JsonSerializationException($"{path} has no \"Configs\" object");
                return configs.Properties().Select(config => new ScheduleConfig(config.Name, new JObject(
                    new JProperty("name", config.Name),
                    new JProperty("eventName", (string?)config.Value["EventName"]),
                    new JProperty("type", (string?)config.Value["Type"]),
                    new JProperty("schedule", (string?)config.Value["Schedule"]),
                    new JProperty("payloadVersion", (int?)config.Value["PayloadVersion"]),
                    new JProperty("payload", (string?)config.Value["Payload"])))).ToList();
            }
        }

        /// <summary>A trigger of the .tr file, as the Triggers admin API creates it.</summary>
        private sealed class TriggerConfig
        {
            private TriggerConfig(string name, JObject body)
            {
                Name = name;
                Body = body;
            }

            public string Name { get; }

            public JObject Body { get; }

            /// <exception cref="IOException">The file cannot be read.</exception>
            /// <exception cref="JsonException">The file is not the triggers' JSON.</exception>
            public static IReadOnlyList<TriggerConfig> ReadAll(string path)
            {
                JArray configs = JObject.Parse(File.ReadAllText(path))["Configs"] as JArray
                    ?? throw new JsonSerializationException($"{path} has no \"Configs\" array");
                return configs.OfType<JObject>().Select(config =>
                {
                    string name = (string?)config["Name"] ?? throw new JsonSerializationException($"{path}: a trigger without a Name");
                    return new TriggerConfig(name, new JObject(
                        new JProperty("name", name),
                        new JProperty("eventType", (string?)config["EventType"]),
                        new JProperty("actionType", (string?)config["ActionType"]),
                        new JProperty("actionUrn", (string?)config["ActionUrn"])));
                }).ToList();
            }
        }
    }
}
