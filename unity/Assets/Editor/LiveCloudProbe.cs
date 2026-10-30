#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Puzzle.Client.Cloud;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.RemoteConfig;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PlayerDeleteOptions = Unity.Services.CloudSave.Models.Data.Player.DeleteOptions;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Asks the linked Unity Cloud project, for real, whether the game's services answer (docs/research/server-v1.md §9
    /// A1–A6), and leaves nothing behind: Unity's services start in the environment named (production unless
    /// <c>-probeEnvironment</c> says otherwise), a new anonymous player signs in, a probe key is written to Cloud Save,
    /// read back and deleted, Remote Config is fetched, the Cloud Code module's DeleteMyData is called (the module answers
    /// "deleted" once the owner deployed it; until then Cloud Code says what is missing), and the player's account is
    /// deleted, so the project keeps no test player. A session an earlier probe left in the Editor's PlayerPrefs (one whose
    /// account could not be deleted) is signed in to and deleted the same way. Each step's answer, or the error Unity
    /// returned (its code and words), is written to the log on a "Live cloud probe:" line; the last line is the verdict:
    /// PASSED when the account and Cloud Save answered and the account was deleted. Remote Config and Cloud Code are
    /// reported, never failed: the owner deploys their content (CLOUD_CODE_DEPLOY.md). No secret is read or written.
    /// Unity's services start only in play mode, so the probe enters an empty scene's play mode and runs there.
    /// usage: Unity.exe -batchmode -projectPath unity -executeMethod Puzzle.Client.EditorTools.LiveCloudProbe.Run [-probeEnvironment development] -logFile out/unity/live-probe.log
    /// WITHOUT -quit: play mode and Unity's services answer on later Editor updates; the probe ends the Editor itself.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveCloudProbe
    {
        private const string EnvironmentArgument = "-probeEnvironment";
        private const string ProbeKey = "probe";

        /// <summary>The Cloud Code module of the game (server/CloudCode).</summary>
        private const string ModuleName = "MuseumPuzzleServer";

        /// <summary>Entering play mode loads the scripts anew and forgets every static field; the Editor's session state survives it.</summary>
        private const string IsRunningKey = "Puzzle.Client.LiveCloudProbe.IsRunning";
        private const string EnvironmentKey = "Puzzle.Client.LiveCloudProbe.Environment";

        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(120);

        private static Task<bool>? _probe;
        private static DateTime _deadline;
        private static string _environment = CloudOfThisBuild.ProductionEnvironment;

        static LiveCloudProbe()
        {
            if (SessionState.GetBool(IsRunningKey, false))
            {
                EditorApplication.update += Watch;
            }
        }

        [MenuItem("Puzzle/Checks/Live cloud probe")]
        public static void Run()
        {
            string environment = EnvironmentNamed(Environment.GetCommandLineArgs());
            // The Editor's own Unity Connect is not signed in to in a batch run, so CloudProjectSettings may say nothing:
            // the player settings' id is what the services read, as in a device build.
            string project = string.IsNullOrEmpty(CloudProjectSettings.projectId) ? Application.cloudProjectId : CloudProjectSettings.projectId;
            if (string.IsNullOrEmpty(project))
            {
                Finish(passed: false, "no Unity Cloud project is linked (ProjectSettings has no cloudProjectId)");
                return;
            }

            Say($"project {project}, environment \"{environment}\"");
            SessionState.SetString(EnvironmentKey, environment);
            SessionState.SetBool(IsRunningKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.update += Watch;
            EditorApplication.EnterPlaymode();
        }

        private static void Watch()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (_probe == null)
            {
                _environment = SessionState.GetString(EnvironmentKey, CloudOfThisBuild.ProductionEnvironment);
                _deadline = DateTime.UtcNow + Patience;
                _probe = ProbeAsync();
                return;
            }

            if (_probe.IsCompleted)
            {
                EditorApplication.update -= Watch;
                bool passed = _probe.Status == TaskStatus.RanToCompletion && _probe.Result;
                string fault = _probe.Exception?.GetBaseException() is Exception thrown ? $"; the probe itself failed: {thrown.GetType().Name}: {thrown.Message}" : string.Empty;
                Finish(passed, $"environment \"{_environment}\"{fault}");
                return;
            }

            if (DateTime.UtcNow > _deadline)
            {
                EditorApplication.update -= Watch;
                Finish(passed: false, $"Unity's services did not finish within {Patience.TotalSeconds:F0} s (environment \"{_environment}\")");
            }
        }

        /// <summary>Every step in turn; true when the account and Cloud Save answered and the account was deleted.</summary>
        private static async Task<bool> ProbeAsync()
        {
            if (!await StepAsync("services start", () => UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(_environment))))
            {
                return false;
            }

            IAuthenticationService authentication = AuthenticationService.Instance;
            if (authentication.SessionTokenExists)
            {
                Say("a session an earlier probe left in the Editor's PlayerPrefs is signed in to, and its account deleted below");
            }

            if (!await StepAsync("anonymous sign-in", () => authentication.SignInAnonymouslyAsync()))
            {
                return false;
            }

            Say($"signed in as player {authentication.PlayerId}");
            bool cloudSaveAnswered = await ProbeCloudSaveAsync();
            await ProbeRemoteConfigAsync();
            await ProbeCloudCodeAsync();
            string probePlayer = authentication.PlayerId;
            bool deleted = await StepAsync("account deletion", () => authentication.DeleteAccountAsync());
            if (deleted)
            {
                Say($"the probe's player {probePlayer} is deleted: the project keeps no test player");
            }
            else
            {
                Say($"the probe's player could NOT be deleted: delete it in the Dashboard (Player Management), or run the probe again, which signs in to its session and deletes it");
            }

            return cloudSaveAnswered && deleted;
        }

        /// <summary>A probe key written, read back and deleted; true when all three answered and the text came back as written.</summary>
        private static async Task<bool> ProbeCloudSaveAsync()
        {
            string written = $"probe {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)} {RandomIds.New()}";
            var save = new Dictionary<string, SaveItem> { { ProbeKey, new SaveItem(written, writeLock: null) } };
            Dictionary<string, string>? locks = null;
            if (!await StepAsync("Cloud Save write", async () => locks = await CloudSaveService.Instance.Data.Player.SaveAsync(save, new PlayerSaveOptions())))
            {
                return false;
            }

            Dictionary<string, Item>? read = null;
            if (!await StepAsync("Cloud Save read", async () => read = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { ProbeKey }, new LoadOptions(new DefaultReadAccessClassOptions()))))
            {
                return false;
            }

            string? came = read != null && read.TryGetValue(ProbeKey, out Item? item) ? item?.Value?.GetAs<string>() : null;
            bool same = came == written;
            Say(same
                ? $"Cloud Save gave back what was written (write lock {locks?.Values.FirstOrDefault()})"
                : $"Cloud Save gave back \"{came ?? "nothing"}\", not \"{written}\"");
            bool deleted = await StepAsync("Cloud Save delete", () => CloudSaveService.Instance.Data.Player.DeleteAsync(ProbeKey, new PlayerDeleteOptions()));
            return same && deleted;
        }

        /// <summary>Remote Config's answer: where the settings came from and how many keys they hold (none until the owner publishes the four keys).</summary>
        private static async Task ProbeRemoteConfigAsync()
        {
            RuntimeConfig? config = null;
            if (await StepAsync("Remote Config fetch", async () => config = await RemoteConfigService.Instance.FetchConfigsAsync(new NoAttributes(), new NoAttributes())))
            {
                string keys = config == null ? "nothing" : string.Join(", ", config.GetKeys() ?? Array.Empty<string>());
                Say($"Remote Config answered from {config?.origin} with the keys: {(keys.Length == 0 ? "none" : keys)}; the game's four ({RemoteConfigKeys.CloudSyncEnabled}, {RemoteConfigKeys.UploadMinMinutes}, {RemoteConfigKeys.ReadMinHours}, {RemoteConfigKeys.PurchaseValidationEnabled}) keep their defaults until published");
            }
        }

        /// <summary>The module's DeleteMyData for the probe's player: "deleted" once the module is deployed, Cloud Code's error until then.</summary>
        private static async Task ProbeCloudCodeAsync()
        {
            string? reply = null;
            if (await StepAsync("Cloud Code DeleteMyData", async () => reply = await CloudCodeService.Instance.CallModuleEndpointAsync(ModuleName, "DeleteMyData", new Dictionary<string, object>())))
            {
                Say($"the Cloud Code module {ModuleName} is deployed and answered {reply}");
            }
        }

        /// <summary>Runs a step, and writes what it came to: done, or the error Unity returned with its code and words.</summary>
        private static async Task<bool> StepAsync(string step, Func<Task> work)
        {
            try
            {
                await work();
                Say($"{step}: done");
                return true;
            }
            catch (CloudSaveException failure)
            {
                Say($"{step}: Cloud Save refused: reason {failure.Reason}, code {failure.ErrorCode}: {failure.Message}");
            }
            catch (CloudCodeException failure)
            {
                Say($"{step}: Cloud Code refused: reason {failure.Reason}, code {failure.ErrorCode}: {failure.Message}");
            }
            catch (AuthenticationException failure)
            {
                Say($"{step}: Authentication refused: code {failure.ErrorCode}: {failure.Message}");
            }
            catch (RequestFailedException failure)
            {
                Say($"{step}: the service refused: code {failure.ErrorCode}: {failure.Message}");
            }
            catch (ServicesInitializationException failure)
            {
                Say($"{step}: Unity's services did not start: {failure.Message}");
            }

            return false;
        }

        private static string EnvironmentNamed(string[] arguments)
        {
            int at = Array.IndexOf(arguments, EnvironmentArgument);
            return at >= 0 && at + 1 < arguments.Length && !string.IsNullOrEmpty(arguments[at + 1]) ? arguments[at + 1] : CloudOfThisBuild.ProductionEnvironment;
        }

        private static void Say(string line) => Debug.Log($"Live cloud probe: {line}");

        private static void Finish(bool passed, string report)
        {
            SessionState.EraseBool(IsRunningKey);
            SessionState.EraseString(EnvironmentKey);
            Debug.Log($"Live cloud probe: {(passed ? "PASSED" : "FAILED")}: {report}");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        private struct NoAttributes
        {
        }
    }
}
