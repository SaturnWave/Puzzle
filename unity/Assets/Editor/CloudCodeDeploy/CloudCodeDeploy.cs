#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Deploys the game's Cloud Code module (server/CloudCode, through Assets/CloudCode/MuseumPuzzleServer.ccmr) to an
    /// environment of the linked Unity Cloud project, as docs/CLOUD_CODE_DEPLOY.md §3 does by hand in the Deployment
    /// window, without anyone at the screen (2026-10-27, the owner works remotely). The Editor's environment is set to
    /// the one named, the Deployment window is opened (Unity's deployment API answers only through it), the module is
    /// deployed, and each item's status is written on a "Cloud Code deploy:" line; the last line is the verdict: PASSED
    /// when every item reports success. The daily schedules and their triggers are not deployed here (§4: the Dashboard).
    /// <para>
    /// The deployment signs in as the Editor's Unity user, which a batch run may lack, so the tool runs in the Editor
    /// with its window: usage: Unity.exe -projectPath unity -executeMethod Puzzle.Client.EditorTools.CloudCodeDeploy.Run
    /// -deployEnvironment development -logFile out/unity/cloud-code-deploy.log
    /// WITHOUT -quit and without -batchmode: the deployment answers on later Editor updates; the tool ends the Editor.
    /// No secret is read or written.
    /// </para>
    /// </summary>
    public static class CloudCodeDeploy
    {
        private const string EnvironmentArgument = "-deployEnvironment";
        private const string ModuleReference = "Assets/CloudCode/MuseumPuzzleServer.ccmr";

        /// <summary>The environments the module goes to (docs/research/server-v1.md §3.1). A closed set.</summary>
        private static readonly IReadOnlyList<string> Environments = new[] { "development", "production" };

        /// <summary>The first deployment builds the module with dotnet, which fetches its NuGet packages: minutes, not seconds.</summary>
        private static readonly TimeSpan Patience = TimeSpan.FromMinutes(10);

        /// <summary>How long the Deployment window may take to find the project's deployable files once opened.</summary>
        private static readonly TimeSpan PatienceForTheWindow = TimeSpan.FromSeconds(60);

        /// <summary>How long the Editor may take, once started, to sign its Unity user in from the Hub's session.</summary>
        private static readonly TimeSpan PatienceForTheSignIn = TimeSpan.FromMinutes(2);

        private static Task<bool>? _deployment;
        private static DateTime _deadline;
        private static string _environment = string.Empty;

        public static void Run()
        {
            if (EnvironmentNamed(Environment.GetCommandLineArgs()) is not string environment)
            {
                Finish(passed: false, $"{EnvironmentArgument} must name one of: {string.Join(", ", Environments)}");
                return;
            }

            // The Editor signs its user in a while after the method runs: the deployment starts once it has.
            _environment = environment;
            _deadline = DateTime.UtcNow + PatienceForTheSignIn;
            Say($"environment \"{environment}\"; waiting for the Editor's Unity sign-in (at most {PatienceForTheSignIn.TotalMinutes:F0} min)");
            EditorApplication.update += Watch;
        }

        private static bool IsSignedIn => !string.IsNullOrEmpty(CloudProjectSettings.userId) && !string.IsNullOrEmpty(CloudProjectSettings.projectId);

        private static void Watch()
        {
            if (_deployment == null)
            {
                if (IsSignedIn)
                {
                    Say($"project {CloudProjectSettings.projectId}, the Editor's user signed in");
                    _deadline = DateTime.UtcNow + Patience;
                    _deployment = DeployAsync();
                }
                else if (DateTime.UtcNow > _deadline)
                {
                    EditorApplication.update -= Watch;
                    Finish(passed: false, $"the Editor did not sign in to Unity within {PatienceForTheSignIn.TotalMinutes:F0} min (is the Hub signed in?), or no Unity Cloud project is linked");
                }

                return;
            }

            if (_deployment.IsCompleted)
            {
                EditorApplication.update -= Watch;
                bool passed = _deployment.Status == TaskStatus.RanToCompletion && _deployment.Result;
                string fault = _deployment.Exception?.GetBaseException() is Exception thrown ? $"; the deployment failed: {thrown.GetType().Name}: {thrown.Message}" : string.Empty;
                Finish(passed, $"environment \"{_environment}\"{fault}");
                return;
            }

            if (DateTime.UtcNow > _deadline)
            {
                EditorApplication.update -= Watch;
                Finish(passed: false, $"the deployment did not finish within {Patience.TotalMinutes:F0} min (environment \"{_environment}\")");
            }
        }

        /// <summary>The environment set, the module found and deployed; true when every item says it succeeded.</summary>
        private static async Task<bool> DeployAsync()
        {
            IEnvironmentsApi environments = EnvironmentsApi.Instance;
            await environments.RefreshAsync();
            Say($"the project's environments: {string.Join(", ", environments.Environments.Select(known => known.Name))}");
            if (!environments.Environments.Any(known => known.Name == _environment))
            {
                Say($"the project has no environment \"{_environment}\": create it in the Dashboard (Settings → Environments)");
                return false;
            }

            environments.SetActiveEnvironment(_environment);
            Say($"the Editor's environment is \"{environments.ActiveEnvironmentName}\"");
            if (environments.ActiveEnvironmentName != _environment)
            {
                return false;
            }

            Deployments.Instance.DeploymentWindow.OpenWindow();
            IDeploymentItem? module = await ModuleFoundByTheWindowAsync();
            if (module == null)
            {
                Say($"the Deployment window found no deployable item at {ModuleReference} within {PatienceForTheWindow.TotalSeconds:F0} s");
                return false;
            }

            Say($"deploying {module.Name} ({module.Path})");
            DeploymentResult<IDeploymentItem> result = await Deployments.Instance.DeploymentWindow.Deploy(new[] { module });
            IReadOnlyList<IDeploymentItem> deployed = result.Deployed.SelectMany(ItemAndChildren).ToList();
            foreach (IDeploymentItem item in deployed)
            {
                Say($"{item.Name}: {item.Status.Message} [{item.Status.MessageSeverity}] {item.Status.MessageDetail}".TrimEnd());
                foreach (AssetState state in item.States)
                {
                    Say($"  {state.Level}: {state.Description} {state.Detail}".TrimEnd());
                }
            }

            return deployed.Count > 0 && deployed.All(item => item.Status.MessageSeverity == SeverityLevel.Success);
        }

        /// <summary>The module's item once the opened window has found the project's deployable files; nothing if it never does.</summary>
        private static async Task<IDeploymentItem?> ModuleFoundByTheWindowAsync()
        {
            DateTime giveUp = DateTime.UtcNow + PatienceForTheWindow;
            while (DateTime.UtcNow < giveUp)
            {
                if (Deployments.Instance.DeploymentWindow.GetFromFiles(new[] { ModuleReference }).FirstOrDefault() is IDeploymentItem found)
                {
                    return found;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            return null;
        }

        private static IEnumerable<IDeploymentItem> ItemAndChildren(IDeploymentItem item) =>
            item is ICompositeItem composite ? new[] { item }.Concat(composite.Children.SelectMany(ItemAndChildren)) : new[] { item };

        /// <summary>The environment the command line names, if it is one of the module's.</summary>
        private static string? EnvironmentNamed(string[] arguments)
        {
            int at = Array.IndexOf(arguments, EnvironmentArgument);
            string? named = at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
            return named != null && Environments.Contains(named) ? named : null;
        }

        private static void Say(string line) => Debug.Log($"Cloud Code deploy: {line}");

        private static void Finish(bool passed, string report)
        {
            Debug.Log($"Cloud Code deploy: {(passed ? "PASSED" : "FAILED")}: {report}");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
