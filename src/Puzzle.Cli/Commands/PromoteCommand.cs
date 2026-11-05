using System.Diagnostics;
using System.Globalization;
using Puzzle.Bank.Assembling;
using Puzzle.Core.Recording;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>promote &lt;staging folder&gt; [--live levels/generated] [--accept 52,53] [--archive &lt;folder&gt;] [--backup &lt;folder&gt;]
/// [--recordings recordings/attempts.jsonl | none] [--server-process Puzzle.Web | none] [--dry-run]</c>: takes an
/// assembled set live (<see cref="Promotion"/>). It refuses while the test server runs: the server reads the levels
/// when it starts and matches players' saved attempts to level files, so the files are swapped under a stopped
/// server and go live when it starts again. <c>--dry-run</c> checks everything and says what would change, with the
/// server running or not, and changes nothing.
/// Exit codes: 0 done (or a dry run that found nothing in the way), 1 refused, 2 usage, 4 a file could not be written.
/// </summary>
internal static class PromoteCommand
{
    private const int Refused = 1;
    private const int DiskFailed = 4;
    private const string NoServer = "none";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Run(CliArgs args)
    {
        string staging = args.Required(0, "staging folder");
        string live = args.Text("live", Path.Combine("levels", "generated"));
        if (string.Equals(Path.GetFullPath(staging), Path.GetFullPath(live), StringComparison.OrdinalIgnoreCase))
        {
            throw new UsageException("The staging folder is the live folder.");
        }

        try
        {
            IReadOnlyList<AttemptRecord> records = AssembleCommand.RecordsFrom(args) ?? [];
            PromotionPlan plan = Promotion.Plan(staging, live, RecordedLevels.FingerprintsIn(records), AcceptedFrom(args));
            Print(plan);
            if (args.Flag("dry-run"))
            {
                Console.WriteLine("dry run: nothing was changed");
                return 0;
            }

            RequireStoppedServer(args.Text("server-process", "Puzzle.Web"));
            string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HHmmss'Z'", Invariant);
            string archive = args.Text("archive", Path.Combine(Path.GetDirectoryName(Path.GetFullPath(live))!, "archive", $"before-{PolicyNameOf(plan)}-{stamp[..10]}"));
            string backup = args.Text("backup", Path.Combine("out", "promote", stamp));
            Promotion.CarryOut(plan, archive, backup);
            Console.WriteLine($"live: {plan.Live} now holds the set of {plan.Staging}");
            Console.WriteLine($"backup of every replaced file: {backup} (copying it over {plan.Live} takes this step back)");
            if (plan.Archived.Count > 0)
            {
                Console.WriteLine($"archive of the replaced files people have recordings of: {archive}");
            }

            return 0;
        }
        catch (PromotionRefusedException ex)
        {
            Console.Error.WriteLine($"Not taken live: {ex.Message}");
            return Refused;
        }
        catch (AssemblyException ex)
        {
            Console.Error.WriteLine($"Not taken live: {ex.Message}");
            return Refused;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"A file could not be read or written: {ex.Message} Files copied before this stay; run the command again to finish the step.");
            return DiskFailed;
        }
    }

    /// <exception cref="PromotionRefusedException">A process of that name runs.</exception>
    private static void RequireStoppedServer(string processName)
    {
        if (processName == NoServer)
        {
            return;
        }

        Process[] running = Process.GetProcessesByName(processName);
        try
        {
            if (running.Length > 0)
            {
                throw new PromotionRefusedException(
                    $"the test server runs (process {processName}, id {running[0].Id}). Stop it first (tools\\public\\Stop-PublicServer.ps1), run this again, then start it; "
                    + $"for a live folder no server reads, say --server-process {NoServer}.");
            }
        }
        finally
        {
            foreach (Process process in running)
            {
                process.Dispose();
            }
        }
    }

    private static HashSet<int> AcceptedFrom(CliArgs args)
    {
        var accepted = new HashSet<int>();
        foreach (string number in (args.OptionalText("accept") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            accepted.Add(int.TryParse(number, NumberStyles.None, Invariant, out int level)
                ? level
                : throw new UsageException($"--accept expects level numbers separated by commas, got '{number}'."));
        }

        return accepted;
    }

    private static string PolicyNameOf(PromotionPlan plan) =>
        plan.Manifest.Policy.TryGetProperty("name", out System.Text.Json.JsonElement name) && name.GetString() is { Length: > 0 } text ? text : "assembly";

    private static void Print(PromotionPlan plan)
    {
        Console.WriteLine($"staged set {plan.Staging} -> {plan.Live}: {plan.Unchanged.Count} level files stay as they are, {plan.Replaced.Count} are replaced, {plan.Added.Count} are new");
        Console.WriteLine($"decisions of the set: {string.Join(", ", plan.Manifest.Decisions.Select(decision => $"{decision.Key} {decision.Value}"))}");
        if (plan.Manifest.Shortfalls.Count > 0)
        {
            Console.WriteLine($"accepted although they do not meet the policy: {string.Join(' ', plan.Manifest.Shortfalls.Select(shortfall => shortfall.Level))}");
        }

        if (plan.Archived.Count > 0)
        {
            Console.WriteLine($"replaced files people have recorded attempts on (they go to the archive first): {string.Join(' ', plan.Archived)}");
        }
    }
}
