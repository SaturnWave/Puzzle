using System.Diagnostics;
using System.Globalization;
using Puzzle.Bank.Assembling;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>assemble</c> and <c>compare</c>: the levels as a difficulty policy makes them from the level bank.
/// <list type="bullet">
/// <item><c>assemble --policy policies/default.json --base levels/generated --out out/assemble/&lt;policy name&gt;
/// [--bank &lt;folder&gt;] [--from 1] [--to &lt;last level of the base&gt;] [--recordings recordings/attempts.jsonl | none]
/// [--accept-damage]</c>: writes the level files, report.csv, assembly.csv and assembly.manifest into the staging
/// folder, which is replaced whole. It never writes into the base folder. Without a bank only pinned levels and levels
/// that still stand under the policy can be made. A damaged bank is refused unless --accept-damage says to go on.</item>
/// <item><c>compare &lt;folder&gt; &lt;other folder&gt; [--from N] [--to N] [--out changes.csv]</c>: per tier the limits and
/// shares of both, the mix of level kinds, and how many numbers changed limit, amounts or board.</item>
/// </list>
/// Exit codes of assemble: 0 every level made, 1 some levels unfilled, a kind missed where the policy makes that
/// count, or the bank is damaged, 2 usage, 3 the build plays other games than the bank holds, 4 a disk failed (the
/// bank's, or the staging folder's).
/// </summary>
internal static class AssembleCommand
{
    private const int Incomplete = 1;
    private const int OtherRules = 3;
    private const int DiskFailed = 4;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly TimeSpan ProgressEvery = TimeSpan.FromSeconds(30);

    public static int Run(CliArgs args)
    {
        DifficultyPolicy policy = PolicyLoader.Load(args.Text("policy", "policies/default.json"));
        var baseLevels = new LevelFolder(args.Text("base", "levels/generated"));
        string outFolder = StagingFolder(args.Text("out", Path.Combine("out", "assemble", policy.Name)), baseLevels);
        IReadOnlyList<int> inBase = baseLevels.Numbers();
        int from = args.Int("from", 1);
        int to = args.Int("to", inBase.Count > 0 ? inBase[^1] : 0);
        if (from < 1 || to < from)
        {
            throw new UsageException("--from must be >= 1 and --to >= --from (name --to when the base folder holds no levels).");
        }

        try
        {
            return Assemble(args, policy, baseLevels, outFolder, Enumerable.Range(from, to - from + 1).ToArray());
        }
        catch (BankFormatException ex)
        {
            Console.Error.WriteLine($"Not a readable bank: {ex.Message}");
            return Incomplete;
        }
        catch (EpochMismatchException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return OtherRules;
        }
        catch (BankDamagedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Incomplete;
        }
        catch (BankReadException ex)
        {
            Console.Error.WriteLine($"{ex.Message} Nothing was written to '{outFolder}'; the games played before this are in the bank. Start again when the disk is back.");
            return DiskFailed;
        }
        catch (BankWriteException ex)
        {
            Console.Error.WriteLine($"{ex.Message} Nothing was written to '{outFolder}'; the games played so far are in the bank, so the next run reads them.");
            return DiskFailed;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"The assembly could not be written: {ex.Message}");
            return DiskFailed;
        }
        catch (AssemblyException ex)
        {
            throw new UsageException(ex.Message);
        }
    }

    public static int Compare(CliArgs args)
    {
        var first = new LevelFolder(args.Required(0, "folder"));
        var second = new LevelFolder(args.Required(1, "other folder"));
        int[] numbers = first.Numbers().Union(second.Numbers()).Where(number => number >= args.Int("from", 1) && number <= args.Int("to", int.MaxValue)).Order().ToArray();
        LevelSetComparison comparison = LevelSetComparison.Of(first, second, numbers);
        Print(comparison, first.Root, second.Root, kindTargets: null);
        if (args.OptionalText("out") is string outPath)
        {
            File.WriteAllLines(outPath, ChangesCsv(comparison));
            Console.WriteLine($"wrote {outPath}: {comparison.Levels.Count} rows");
        }

        return 0;
    }

    private static int Assemble(CliArgs args, DifficultyPolicy policy, LevelFolder baseLevels, string outFolder, int[] numbers)
    {
        var clock = Stopwatch.StartNew();
        using OpenBank? bank = OpenBank.At(args.OptionalText("bank") ?? Environment.GetEnvironmentVariable(BankStore.RootVariable), args);
        RecordedLevels recorded = RecordedOn(baseLevels, args);
        Console.WriteLine(
            $"policy '{policy.Name}' on plan '{policy.Plan.Name}', levels {numbers[0]}..{numbers[^1]} from {baseLevels.Root} -> {outFolder}; "
            + (bank is null ? "no bank" : $"bank {bank.Store.Root}: {bank.Access.Catalog.Candidates.Count} candidates, epoch {bank.Access.Epoch}"));

        // The machine also runs the live test server; an assembly takes what it leaves.
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        var progress = new Progress(clock);
        Assembly assembly = new Assembler(policy, baseLevels, bank?.Access, recorded).Run(numbers, progress.Note);
        AssemblyWriter.Write(assembly, outFolder);

        PrintDecisions(assembly);
        PrintRecorded(recorded, policy, numbers);
        Print(LevelSetComparison.Of(baseLevels, new LevelFolder(outFolder), numbers), baseLevels.Root, outFolder, policy.KindShares);
        if (bank is not null)
        {
            Console.WriteLine(string.Create(
                Invariant,
                $"games read from the bank {bank.Games.GamesRead}, played and stored {bank.Games.GamesPlayed}; boards measured at another amount and stored {bank.Siblings.Measured}"));
        }

        PrintShortOfThePolicy(assembly);
        Console.WriteLine(string.Create(Invariant, $"assembled {assembly.Decisions.Count} levels in {clock.Elapsed.TotalSeconds:0.0} s: {outFolder}"));
        return assembly.IsComplete ? 0 : Incomplete;
    }

    /// <summary>
    /// What a reader must not miss: the levels that are in the set without meeting the policy, and the kinds whose
    /// share missed its target. Both are also in the manifest, and taking the set live asks for the levels by number.
    /// </summary>
    private static void PrintShortOfThePolicy(Assembly assembly)
    {
        foreach (IGrouping<ShortfallKind, LevelShortfall> kind in assembly.Shortfalls.GroupBy(shortfall => shortfall.Kind).OrderBy(kind => kind.Key))
        {
            Console.WriteLine($"NOT MEETING THE POLICY: {kind.Count()} levels {DescribeShortfall(kind.Key)}: {Listed(kind.Select(shortfall => shortfall.Level))}");
        }

        if (assembly.MissedKinds.Count > 0)
        {
            string missed = string.Join(", ", assembly.MissedKinds.Select(kind => string.Create(Invariant, $"{kind.Kind} {kind.Share:P1} (target {kind.Target:P0})")));
            Console.WriteLine(string.Create(
                Invariant,
                $"KINDS OFF TARGET by more than {assembly.Policy.KindTolerance:P0}: {missed}. Kept levels keep their kind, and a kind the move ranges cannot hold is not made by a larger bank (bank status --draws shows what draws give)."));
        }
    }

    private static string DescribeShortfall(ShortfallKind kind) => kind switch
    {
        ShortfallKind.Missing => "have no level at all",
        ShortfallKind.Stale => "keep the level they had, which was made for another policy or lies outside its move range (marked 'stale' in report.csv)",
        ShortfallKind.PinnedOutsideItsRange => "are pinned although their own report row says they miss their move range",
        ShortfallKind.PinnedUnderOtherRules => "are pinned and were decided under other rules of the engine or the bot",
        _ => throw new InvalidOperationException($"Unhandled shortfall {kind}."),
    };

    private static void PrintRecorded(RecordedLevels recorded, DifficultyPolicy policy, int[] numbers)
    {
        int[] here = recorded.Numbers.Where(number => number >= numbers[0] && number <= numbers[^1]).ToArray();
        int[] elsewhere = recorded.OnOtherVersions.Where(number => number >= numbers[0] && number <= numbers[^1]).ToArray();
        if (here.Length > 0)
        {
            Console.WriteLine($"recordings: people have recorded attempts on {here.Length} levels of the base folder ({Listed(here)}); policy: recorded = {policy.Recorded} (pinned levels stay as they are whatever it says)");
        }

        if (elsewhere.Length > 0)
        {
            Console.WriteLine($"recordings: {elsewhere.Length} levels ({Listed(elsewhere)}) have recordings of another file than the base folder holds under their number; nothing here is protected by them");
        }
    }

    /// <summary>
    /// The folder to assemble into. It is never the folder the assembly starts from, and never a folder that holds
    /// anything but an earlier assembly: the live levels are replaced by a person, with the server stopped.
    /// </summary>
    /// <exception cref="UsageException">The folder is the base folder, or holds files that no assembly wrote.</exception>
    private static string StagingFolder(string outFolder, LevelFolder baseLevels)
    {
        if (string.Equals(Path.GetFullPath(outFolder), Path.GetFullPath(baseLevels.Root), StringComparison.OrdinalIgnoreCase))
        {
            throw new UsageException($"--out is the folder the assembly starts from ('{baseLevels.Root}'); an assembly is written to a staging folder of its own.");
        }

        bool holdsOtherFiles = Directory.Exists(outFolder)
            && Directory.EnumerateFileSystemEntries(outFolder).Any()
            && !File.Exists(Path.Combine(outFolder, AssemblyManifest.FileName));
        return holdsOtherFiles
            ? throw new UsageException($"'{outFolder}' holds files and no {AssemblyManifest.FileName}: it is not an earlier assembly. Name an empty folder.")
            : outFolder;
    }

    /// <summary>The levels of the base folder people have recorded attempts on; none when the recordings file is not there or --recordings none says to ignore it.</summary>
    private static RecordedLevels RecordedOn(LevelFolder baseLevels, CliArgs args) =>
        RecordsFrom(args) is IReadOnlyList<AttemptRecord> records ? RecordedLevels.Of(records, baseLevels) : RecordedLevels.None;

    /// <summary>The attempt records --recordings names (default: the repository's log); absent for "none" or outside a repository.</summary>
    internal static IReadOnlyList<AttemptRecord>? RecordsFrom(CliArgs args)
    {
        string? path = args.OptionalText("recordings") ?? AttemptLog.FindDefaultPath(Directory.GetCurrentDirectory());
        return path is null || path == "none" ? null : new AttemptLog(path).ReadAll().Records;
    }

    private static void PrintDecisions(Assembly assembly)
    {
        string counts = string.Join(", ", assembly.Decisions.GroupBy(AssemblyWriter.DecisionName).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => $"{group.Key} {group.Count()}"));
        Console.WriteLine($"decisions: {counts}");
        foreach (IGrouping<string, string> reason in assembly.Decisions.Select(NotKeptOf).OfType<string>().GroupBy(GeneralReason).OrderByDescending(reason => reason.Count()))
        {
            Console.WriteLine($"  decided anew, {reason.Count(),4}: {reason.Key}");
        }

        LevelDecision.Assembled[] made = assembly.Decisions.OfType<LevelDecision.Assembled>().ToArray();
        if (made.Length > 0)
        {
            Console.WriteLine("assembled levels, by tier (shares of the level's player: 'audit' on the games that accepted the level, 'after' on games played after that, which a report states):");
            Console.WriteLine("tier       levels  avg moves  avg target  audit win  after win  avg |audit-target|  first try  avg audits  avg tried");
            foreach (IGrouping<DifficultyTier, LevelDecision.Assembled> tier in made.GroupBy(level => level.Slot.Tier).OrderBy(tier => tier.Key))
            {
                Console.WriteLine(string.Create(
                    Invariant,
                    $"{tier.Key,-10} {tier.Count(),6}  {tier.Average(level => level.Finished.Limit),9:0.0}  {tier.Average(level => level.Finished.AuditTarget),10:P0}  {tier.Average(level => level.Finished.Win),9:P1}  {tier.Average(level => level.Finished.ReportedWin),9:P1}  {tier.Average(level => Math.Abs(level.Finished.Win - level.Finished.AuditTarget)),17:P1}  {tier.Average(level => level.Finished.FirstTryWin),9:P1}  {tier.Average(level => level.Finished.Audits),10:0.00}  {tier.Average(level => level.TurnedDown.Count + 1),9:0.00}"));
            }
        }

        PrintNeighbours(made, assembly);
        LevelDecision.Unfilled[] unfilled = assembly.Decisions.OfType<LevelDecision.Unfilled>().ToArray();
        if (unfilled.Length == 0)
        {
            return;
        }

        Console.WriteLine($"UNFILLED: {unfilled.Length} levels have no candidate that fits; the bank is too thin for them ({unfilled.Count(level => level.Stale is not null)} keep the level they had, which no longer meets the policy):");
        foreach (IGrouping<string, LevelDecision.Unfilled> kind in unfilled.GroupBy(level => $"{level.Slot.Tier} {level.Slot.Structure.Label}: {level.Cause}").OrderByDescending(kind => kind.Count()))
        {
            Console.WriteLine($"  {kind.Count(),4}  {kind.Key}  (levels {Listed(kind.Select(level => level.Number))})");
        }
    }

    /// <summary>What the assembled levels are for players beside the one they were made for: mean win shares at the limit, on all seeds of the stored ladder games.</summary>
    private static void PrintNeighbours(LevelDecision.Assembled[] made, Assembly assembly)
    {
        if (made.Length == 0)
        {
            return;
        }

        int[] rungs = AssemblyWriter.NeighbourRungs.Append(0).Order().ToArray();
        Console.WriteLine("assembled levels for weaker and stronger players (skill points from the level's player; all seeds, stored ladder games):");
        Console.WriteLine("tier       " + string.Concat(rungs.Select(points => $"{points.ToString("+0;-0;0", Invariant),8}")));
        foreach (IGrouping<DifficultyTier, LevelDecision.Assembled> tier in made.GroupBy(level => level.Slot.Tier).OrderBy(tier => tier.Key))
        {
            IEnumerable<string> shares = rungs.Select(points =>
            {
                double[] measured = tier.Select(level => AssemblyWriter.NeighbourShare(level, assembly, points)).OfType<double>().ToArray();
                return $"{(measured.Length == 0 ? "-" : measured.Average().ToString("P1", Invariant)),8}";
            });
            Console.WriteLine($"{tier.Key,-10} {string.Concat(shares)}");
        }
    }

    private static string? NotKeptOf(LevelDecision decision) => decision switch
    {
        LevelDecision.Assembled made => made.NotKept,
        LevelDecision.Unfilled unfilled => unfilled.NotKept,
        _ => null,
    };

    /// <summary>A reason without what is particular to one level (its numbers in brackets, its limit), so equal reasons count together.</summary>
    private static string GeneralReason(string reason)
    {
        int detail = reason.IndexOf(" (", StringComparison.Ordinal);
        string general = detail < 0 ? reason : reason[..detail];
        return general.StartsWith("its limit of ", StringComparison.Ordinal) ? "its limit lies outside its move range" : general;
    }

    private static string Listed(IEnumerable<int> numbers)
    {
        const int Shown = 12;
        int[] all = numbers.Order().ToArray();
        return string.Join(' ', all.Take(Shown)) + (all.Length > Shown ? " ..." : string.Empty);
    }

    /// <param name="kindTargets">The policy's target shares, shown beside the kind mix; absent when no policy is at hand.</param>
    private static void Print(LevelSetComparison comparison, string first, string second, IReadOnlyDictionary<LevelKind, double>? kindTargets)
    {
        Console.WriteLine($"A = {first} ({comparison.First.Levels} levels), B = {second} ({comparison.Second.Levels} levels)");
        Console.WriteLine("tier       levels A  levels B  moves A  moves B  target A  target B   win A   win B");
        foreach (DifficultyTier tier in Enum.GetValues<DifficultyTier>())
        {
            TierFigures? a = comparison.First.Tiers.FirstOrDefault(figures => figures.Tier == tier);
            TierFigures? b = comparison.Second.Tiers.FirstOrDefault(figures => figures.Tier == tier);
            if (a is not null || b is not null)
            {
                Console.WriteLine(string.Create(
                    Invariant,
                    $"{tier,-10} {a?.Levels ?? 0,8}  {b?.Levels ?? 0,8}  {Moves(a),7}  {Moves(b),7}  {Percent(a?.MeanTarget),8}  {Percent(b?.MeanTarget),8}  {Percent(a?.MeanWin),6}  {Percent(b?.MeanWin),6}"));
            }
        }

        Console.WriteLine("kind of level     A  share A      B  share B" + (kindTargets is null ? string.Empty : "   target"));
        foreach (LevelKind kind in Enum.GetValues<LevelKind>())
        {
            (int inA, int inB) = (comparison.First.Kinds.GetValueOrDefault(kind), comparison.Second.Kinds.GetValueOrDefault(kind));
            string target = kindTargets is null ? string.Empty : $"  {(kindTargets.TryGetValue(kind, out double share) ? Percent(share) : "-"),7}";
            Console.WriteLine(string.Create(
                Invariant,
                $"{kind,-13} {inA,5}  {Percent(Ratio(inA, comparison.First.Levels)),7}  {inB,5}  {Percent(Ratio(inB, comparison.Second.Levels)),7}{target}"));
        }

        LevelDifference[] sameCandidate = comparison.Levels.Where(level => level.Change == LevelChange.SameCandidate).ToArray();
        Console.WriteLine(string.Create(
            Invariant,
            $"numbers: identical files {comparison.Count(LevelChange.Identical)}; same board and amounts {sameCandidate.Length} (limit changed {sameCandidate.Count(level => level.MovesFirst != level.MovesSecond)}, pool changed {sameCandidate.Count(level => level.PoolChanged)}); same board with other amounts {comparison.Count(LevelChange.SameBoard)}; other board {comparison.Count(LevelChange.OtherBoard)}; only in A {comparison.Count(LevelChange.OnlyInFirst)}; only in B {comparison.Count(LevelChange.OnlyInSecond)}"));
    }

    private static IEnumerable<string> ChangesCsv(LevelSetComparison comparison)
    {
        yield return "level,tier,change,movesA,movesB,poolChanged";
        foreach (LevelDifference level in comparison.Levels)
        {
            yield return string.Create(Invariant, $"{level.Number},{level.Tier},{level.Change},{level.MovesFirst},{level.MovesSecond},{(level.PoolChanged ? "true" : "false")}");
        }
    }

    private static string Moves(TierFigures? figures) => figures is null ? "-" : figures.MeanMoves.ToString("0.0", Invariant);

    private static double? Ratio(int count, int levels) => levels == 0 ? null : count / (double)levels;

    private static string Percent(double? share) => share is double value ? value.ToString("P1", Invariant) : "-";

    /// <summary>The bank an assembly reads and adds its confirm and audit games to, open for as long as the assembly runs.</summary>
    private sealed class OpenBank(BankStore store, BankWriter writer, BankAccess access, BankGames games, BankSiblings siblings) : IDisposable
    {
        public BankStore Store => store;

        public BankAccess Access => access;

        public BankGames Games => games;

        public BankSiblings Siblings => siblings;

        /// <summary>Absent when no bank is named.</summary>
        /// <exception cref="BankFormatException">The folder holds no readable bank.</exception>
        /// <exception cref="BankDamagedException">The bank is damaged and nobody said to go on with what is left of it.</exception>
        /// <exception cref="BankReadException">The bank's disk cannot be read.</exception>
        /// <exception cref="EpochMismatchException">The bank has no games of this build's rules, or its stored games do not replay.</exception>
        public static OpenBank? At(string? root, CliArgs args)
        {
            if (root is null)
            {
                return null;
            }

            BankStore store = BankStore.Open(root);
            BankScan scan = store.Read();
            BankHealth.RequireSound(scan, store, args, "An assembly from it would choose other boards than from the whole bank");
            BankCatalog catalog = scan.Catalog;
            EpochId epoch = Epochs.Current(catalog) ?? throw new EpochMismatchException(
                "The bank holds no games played with this build's rules and bot: nothing in it can set a limit. Open an epoch for this build and play the boards' ladder games again first (bank remeasure --new-epoch).");
            Console.Error.WriteLine(Epochs.Describe(Epochs.RequireSameGames(store, catalog, epoch, new Rng((ulong)Stopwatch.GetTimestamp())), epoch));
            BankWriter writer = store.OpenWriter(catalog);
            var games = new BankGames(store, writer, epoch, MeasurementSettings.Default);
            var siblings = new BankSiblings(catalog, writer, epoch, new GrowthSettings());
            ShardLength[] cut = scan.Shards.Select(shard => new ShardLength(shard.File, shard.Length)).ToArray();
            return new OpenBank(store, writer, new BankAccess(store.Identity.BankId, catalog, epoch, games, siblings, cut), games, siblings);
        }

        public void Dispose() => writer.Dispose();
    }

    /// <summary>A line on stderr every half minute while levels are decided anew.</summary>
    private sealed class Progress(Stopwatch clock)
    {
        private TimeSpan _lastLine = TimeSpan.Zero;
        private int _decided;
        private int _unfilled;

        public void Note(LevelDecision decision)
        {
            _decided++;
            _unfilled += decision is LevelDecision.Unfilled ? 1 : 0;
            if (clock.Elapsed - _lastLine < ProgressEvery)
            {
                return;
            }

            _lastLine = clock.Elapsed;
            Console.Error.WriteLine(string.Create(Invariant, $"{clock.Elapsed.TotalMinutes,6:0.0} min  decided {_decided}, unfilled {_unfilled}; last: level {decision.Number} {AssemblyWriter.DecisionName(decision)}"));
        }
    }
}
