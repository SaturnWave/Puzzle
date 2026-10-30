using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Generator.Profiles;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>bank grow|status|verify|import|remeasure</c>: the level bank, a folder outside the repository (<c>--bank</c>, or the environment
/// variable PUZZLE_BANK) that holds candidate boards with the games the skill ladder played on them.
/// <list type="bullet">
/// <item><c>grow (--policy policies/default.json | --plan profiles/default.json) --levels 51-1000 (--minutes N | --candidates N) [--also-aim skill-45,skill-65]
/// [--spare 0.25] [--siblings 0] [--new-epoch [&lt;epoch left&gt;]] [--seed N]</c>: draws and measures candidates where the
/// bank is thinnest for the planned levels; creates the bank when the folder has none. How deep the bank gets is set
/// by --spare (spare boards per level of a class); a run that ends by itself before its budget is spent has made the
/// bank as deep as that asks, or as deep as draws can make it, and says which. Stop it with Ctrl+C or let the budget
/// run out; start it again to go on.</item>
/// <item><c>status [--plan ... --levels ... --also-aim ... --spare ...] [--out file.csv] [--draws file.csv]</c>: what the bank holds,
/// with --draws every variant the grower tried (its target, probe limit and outcome), and
/// with a plan, what each class of levels still lacks.</item>
/// <item><c>verify [--candidates 24]</c>: reads every record's checksum and replays stored games.</item>
/// <item><c>import &lt;dir|file&gt; [--from N] [--to N]</c>: takes existing levels into the bank, each measured on the whole
/// ladder like a drawn candidate; a level the bank already holds is passed over, so a stopped import is resumed by
/// starting it again. A level the bank holds from before a rule change gets its ladder games again.</item>
/// <item><c>remeasure [--in-use levels/generated[,&lt;folder&gt;]] [--minutes N] [--candidates N] [--new-epoch [&lt;epoch left&gt;]]</c>:
/// after a change of the engine or the bot, plays the ladder games of the boards the bank holds again under the new
/// rules: first the boards behind the levels of the --in-use folders, then the other imported levels, then the rest.
/// A board that has its games is passed over, so a stopped run is resumed by starting it again.</item>
/// </list>
/// grow, import and remeasure refuse a damaged bank unless <c>--accept-damage</c> says to go on with what is left.
/// Exit codes: 0 done, 1 the bank is damaged or not a bank, 2 usage, 3 the build plays other games than the bank
/// holds, 4 the bank's disk failed, 5 another grower is running.
/// </summary>
internal static class BankCommand
{
    private const int Damaged = 1;
    private const int OtherRules = 3;
    private const int DiskFailed = 4;
    private const int Busy = 5;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly TimeSpan ProgressEvery = TimeSpan.FromSeconds(30);

    public static int Run(CliArgs args)
    {
        string action = args.Required(0, "grow|status|verify|import|remeasure");
        string root = args.OptionalText("bank") ?? Environment.GetEnvironmentVariable(BankStore.RootVariable)
            ?? throw new UsageException($"Name the bank's folder with --bank or the environment variable {BankStore.RootVariable}.");
        try
        {
            return action switch
            {
                "grow" => Grow(args, root),
                "status" => Status(args, root),
                "verify" => Verify(args, root),
                "import" => Import(args, root),
                "remeasure" => Remeasure(args, root),
                _ => throw new UsageException($"Unknown bank action '{action}' (grow, status, verify, import or remeasure)."),
            };
        }
        catch (BankFormatException ex)
        {
            Console.Error.WriteLine($"Not a readable bank: {ex.Message}");
            return Damaged;
        }
        catch (EpochMismatchException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return OtherRules;
        }
        catch (BankDamagedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Damaged;
        }
        catch (BankReadException ex)
        {
            Console.Error.WriteLine($"{ex.Message} Nothing was changed; start again when the disk is back.");
            return DiskFailed;
        }
        catch (BankWriteException ex)
        {
            Console.Error.WriteLine($"{ex.Message} Everything stored before this is in the bank; start again when the disk is back.");
            return DiskFailed;
        }
        catch (BankBusyException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Busy;
        }
        catch (DemandException ex)
        {
            throw new UsageException(ex.Message);
        }
    }

    private static int Grow(CliArgs args, string root)
    {
        Demand demand = DemandFrom(args) ?? throw new UsageException("grow needs --levels <from>-<to> (and --plan, default profiles/default.json).");
        GrowthBudget budget = BudgetFrom(args);
        var settings = new GrowthSettings { Spare = SpareFrom(args), Siblings = NonNegative(args, "siblings", 0) };
        BankStore store = BankStore.Exists(root) ? BankStore.Open(root) : BankStore.Create(root, args.ULong("seed", NewBankSeed()));
        using GrowLock growing = store.LockForGrowth();
        BankScan scan = store.Read();
        BankHealth.RequireSound(scan, store, args, "Growing would go on beside the gap, and what the gap held would be drawn again as if it had never been");
        using BankWriter writer = store.OpenWriter(scan.Catalog);
        EpochId epoch = Epochs.ForWriting(scan.Catalog, writer, EpochChoiceFrom(args), note: $"opened by bank grow on {Environment.MachineName}");
        Console.Error.WriteLine(Epochs.Describe(Epochs.RequireSameGames(store, scan.Catalog, epoch, new Rng((ulong)Stopwatch.GetTimestamp())), epoch));

        // The machine also runs the live test server and an editor; the grower takes what they leave.
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, pressed) =>
        {
            pressed.Cancel = true; // finish the candidate in hand, then end
            stop.Cancel();
        };

        Console.WriteLine($"bank {store.Root}: {scan.Catalog.Candidates.Count} candidates, epoch {epoch}; growing for {demand.Classes.Count} classes of {demand.Classes.Sum(c => c.Levels.Count)} levels, aims {string.Join(", ", demand.Aims.Select(aim => aim.Label))}");
        var grower = new Grower(scan.Catalog, writer, demand, store.Identity, epoch, settings);
        var progress = new GrowthProgress();
        GrowthSummary summary = grower.Run(budget, stop.Token, progress.Note);
        PrintSummary(summary, writer.BytesWritten, scan.Catalog);
        return 0;
    }

    private static int Status(CliArgs args, string root)
    {
        BankStore store = BankStore.Open(root);
        BankScan scan = store.Read();
        BankCatalog catalog = scan.Catalog;
        PrintInventory(store, scan);
        if (args.OptionalText("draws") is string drawsPath)
        {
            File.WriteAllLines(drawsPath, DrawsCsv(catalog.Draws));
            Console.WriteLine($"wrote {drawsPath}: {catalog.Draws.Count} rows");
        }

        if (DemandFrom(args) is not Demand demand)
        {
            return 0;
        }

        if (Epochs.Current(catalog) is not EpochId epoch)
        {
            Console.WriteLine("no epoch of this build in the bank: nothing in it counts for this build's rules (bank remeasure --new-epoch opens one and plays the boards' ladder games again)");
            return 0;
        }

        IReadOnlyList<ClassSupply> supply = SupplyTable.Of(catalog, demand, SpareFrom(args), new LimitBook(epoch));
        PrintSupply(supply, demand);
        if (args.OptionalText("out") is string outPath)
        {
            File.WriteAllLines(outPath, SupplyCsv(supply));
            Console.WriteLine($"wrote {outPath}: {supply.Count} rows");
        }

        return 0;
    }

    private static int Verify(CliArgs args, string root)
    {
        BankStore store = BankStore.Open(root);
        BankScan scan = store.Read();
        foreach (ShardScan shard in scan.Shards)
        {
            string damage = string.Join("; ", shard.Unreadable.Select(range => string.Create(Invariant, $"{range.Length} unreadable bytes at offset {range.Offset}")));
            string tail = shard.TornTailBytes > 0 ? string.Create(Invariant, $"torn tail of {shard.TornTailBytes} bytes (a record cut short; harmless)") : string.Empty;
            string state = string.Join("; ", new[] { damage, tail }.Where(text => text.Length > 0));
            Console.WriteLine(string.Create(Invariant, $"{shard.File}: {shard.Records.Count} records, {shard.Length} bytes{(state.Length > 0 ? " - " + state : string.Empty)}"));
        }

        if (scan.GamesWithoutCandidate > 0)
        {
            Console.WriteLine(string.Create(Invariant, $"{scan.GamesWithoutCandidate} batches of games have their candidate in no shard (a shard file is missing)"));
        }

        if (Epochs.Current(scan.Catalog) is not EpochId epoch)
        {
            Console.WriteLine("no epoch of this build in the bank; its games cannot be replayed for comparison");
            return scan.IsSound ? OtherRules : Damaged;
        }

        int candidates = NonNegative(args, "candidates", 24);
        ReplayReport replay = SelfCheck.Replay(store, SelfCheck.Sample(scan.Catalog, epoch, candidates, new Rng((ulong)Stopwatch.GetTimestamp())), seedsPerBatch: 8);
        Console.WriteLine(Epochs.Describe(replay, epoch));
        return !scan.IsSound ? Damaged : replay.Agrees ? 0 : OtherRules;
    }

    private static int Import(CliArgs args, string root)
    {
        string target = args.Required(1, "dir|file");
        (int from, int to) = (args.Int("from", 1), args.Int("to", int.MaxValue));
        LevelDefinition[] levels = LevelFiles.Under(target)
            .Select(LevelJson.Load)
            .Where(level => level.Id.Value >= from && level.Id.Value <= to)
            .OrderBy(level => level.Id.Value)
            .ToArray();
        BankStore store = BankStore.Exists(root) ? BankStore.Open(root) : BankStore.Create(root, args.ULong("seed", NewBankSeed()));
        BankScan scan = store.Read();
        BankHealth.RequireSound(scan, store, args, "A level whose record is in the gap would be measured and stored a second time");
        using BankWriter writer = store.OpenWriter(scan.Catalog);
        EpochId epoch = Epochs.ForWriting(scan.Catalog, writer, EpochChoiceFrom(args), note: $"opened by bank import on {Environment.MachineName}");
        Console.Error.WriteLine(Epochs.Describe(Epochs.RequireSameGames(store, scan.Catalog, epoch, new Rng((ulong)Stopwatch.GetTimestamp())), epoch));

        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, pressed) =>
        {
            pressed.Cancel = true; // finish the level in hand, then end
            stop.Cancel();
        };

        var importer = new Importer(scan.Catalog, writer, epoch, new GrowthSettings());
        var outcomes = new Dictionary<ImportOutcome, int>();
        var clock = Stopwatch.StartNew();
        TimeSpan lastLine = TimeSpan.Zero;
        foreach (LevelDefinition level in levels.TakeWhile(_ => !stop.IsCancellationRequested))
        {
            ImportResult result = importer.Import(level, $"{target}#{level.Id.Value}");
            outcomes[result.Outcome] = outcomes.GetValueOrDefault(result.Outcome) + 1;
            if (result.Outcome is ImportOutcome.NotACandidate or ImportOutcome.Unplayable)
            {
                Console.WriteLine($"level {level.Id.Value}: {result.Outcome}: {result.Detail}");
            }

            if (clock.Elapsed - lastLine >= ProgressEvery)
            {
                lastLine = clock.Elapsed;
                Console.Error.WriteLine(string.Create(Invariant, $"{clock.Elapsed.TotalMinutes,6:0.0} min  level {level.Id.Value}: {DescribeOutcomes(outcomes)}"));
            }
        }

        Console.WriteLine(string.Create(
            Invariant,
            $"imported from {target}: {levels.Length} levels named, {DescribeOutcomes(outcomes)} in {clock.Elapsed.TotalMinutes:0.0} min; the bank now holds {scan.Catalog.Candidates.Count} candidates"));
        return 0;
    }

    private static string DescribeOutcomes(Dictionary<ImportOutcome, int> outcomes) =>
        string.Join(", ", Enum.GetValues<ImportOutcome>().Select(outcome => string.Create(Invariant, $"{outcome} {outcomes.GetValueOrDefault(outcome)}")));

    private static int Remeasure(CliArgs args, string root)
    {
        var budget = new GrowthBudget(args.OptionalInt("minutes") is int minutes ? TimeSpan.FromMinutes(minutes) : null, args.OptionalInt("candidates"));
        BankStore store = BankStore.Open(root);
        BankScan scan = store.Read();
        BankHealth.RequireSound(scan, store, args, "The boards in the gap would be missing from the new epoch for good");
        BankCatalog catalog = scan.Catalog;
        using BankWriter writer = store.OpenWriter(catalog);
        EpochId epoch = Epochs.ForWriting(catalog, writer, EpochChoiceFrom(args), note: $"opened by bank remeasure on {Environment.MachineName}");
        Console.Error.WriteLine(Epochs.Describe(Epochs.RequireSameGames(store, catalog, epoch, new Rng((ulong)Stopwatch.GetTimestamp())), epoch));

        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, pressed) =>
        {
            pressed.Cancel = true; // finish the candidate in hand, then end
            stop.Cancel();
        };

        IReadOnlyList<BankCandidate> inOrder = Remeasurer.InOrder(catalog, CandidatesInUse(args));
        Console.WriteLine($"bank {store.Root}: {catalog.Candidates.Count} candidates, {Epochs.MeasuredIn(catalog, epoch)} of them with ladder games of epoch {epoch}");
        var remeasurer = new Remeasurer(writer, epoch, new GrowthSettings());
        var outcomes = new Dictionary<RemeasureOutcome, int>();
        var clock = Stopwatch.StartNew();
        TimeSpan lastLine = TimeSpan.Zero;
        int visited = 0;
        foreach (BankCandidate candidate in inOrder)
        {
            if (stop.IsCancellationRequested || budget.IsSpent(clock.Elapsed, outcomes.GetValueOrDefault(RemeasureOutcome.Measured)))
            {
                break;
            }

            RemeasureOutcome outcome = remeasurer.Remeasure(candidate);
            outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + 1;
            visited++;
            if (clock.Elapsed - lastLine >= ProgressEvery)
            {
                lastLine = clock.Elapsed;
                Console.Error.WriteLine(string.Create(Invariant, $"{clock.Elapsed.TotalMinutes,6:0.0} min  {visited} of {inOrder.Count}: {DescribeOutcomes(outcomes)}"));
            }
        }

        int left = inOrder.Count - visited;
        Console.WriteLine(string.Create(
            Invariant,
            $"epoch {epoch}: {DescribeOutcomes(outcomes)} in {clock.Elapsed.TotalMinutes:0.0} min; {left} candidates not looked at yet{(left > 0 ? " (start it again to go on)" : string.Empty)}; "
            + $"{Epochs.MeasuredIn(catalog, epoch)} of {catalog.Candidates.Count} candidates now have ladder games of this epoch"));
        return 0;
    }

    private static string DescribeOutcomes(Dictionary<RemeasureOutcome, int> outcomes) =>
        string.Join(", ", Enum.GetValues<RemeasureOutcome>().Select(outcome => string.Create(Invariant, $"{outcome} {outcomes.GetValueOrDefault(outcome)}")));

    /// <summary>The candidates behind the levels of the --in-use folders: the boards an assembly must find measured to keep them.</summary>
    private static HashSet<CandidateId> CandidatesInUse(CliArgs args)
    {
        var inUse = new HashSet<CandidateId>();
        foreach (string folder in (args.OptionalText("in-use") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var levels = new LevelFolder(folder);
            inUse.UnionWith(levels.Numbers().Select(number => levels.Find(number)?.Content?.Id).OfType<CandidateId>());
        }

        return inUse;
    }

    /// <summary>
    /// --new-epoch alone opens an epoch for a build the bank has none for; where the build already works in one,
    /// the id of that epoch must follow, because every game of it stops counting for this build.
    /// </summary>
    private static EpochChoice EpochChoiceFrom(CliArgs args) => args.OptionalText("new-epoch") switch
    {
        null => new EpochChoice.UseCurrent(),
        "true" => new EpochChoice.OpenAnother(Leaving: null),
        string leaving => new EpochChoice.OpenAnother(leaving),
    };

    /// <summary>The demand the options describe; absent when no levels are named (status then shows the inventory alone).</summary>
    /// <exception cref="UsageException">The level range or an aim does not read.</exception>
    private static Demand? DemandFrom(CliArgs args)
    {
        if (args.OptionalText("levels") is not string levels)
        {
            return null;
        }

        string[] ends = levels.Split('-');
        if (ends.Length != 2 || !int.TryParse(ends[0], NumberStyles.None, Invariant, out int from) || !int.TryParse(ends[1], NumberStyles.None, Invariant, out int to) || from < 1 || to < from)
        {
            throw new UsageException($"--levels expects <from>-<to>, got '{levels}'.");
        }

        string[] alsoAims = args.OptionalText("also-aim")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        IEnumerable<int> numbers = Enumerable.Range(from, to - from + 1);
        return args.OptionalText("policy") is string policy
            ? Demand.FromPolicy(PolicyLoader.Load(policy), numbers, alsoAims.Select(AimNamed))
            : Demand.FromPlan(ProfileLoader.Load(args.Text("plan", "profiles/default.json")), numbers, alsoAims.Select(AimNamed));
    }

    private static PlayerMix AimNamed(string name) => PlayerMix.TryParse(name, out PlayerMix? aim, out string? problem)
        ? aim
        : throw new UsageException($"--also-aim: {problem}");

    private static GrowthBudget BudgetFrom(CliArgs args)
    {
        int? minutes = args.OptionalInt("minutes");
        int? candidates = args.OptionalInt("candidates");
        if ((minutes is null && candidates is null) || minutes < 1 || candidates < 1)
        {
            throw new UsageException("grow needs a budget: --minutes N, --candidates N, or both (whichever is spent first), each at least 1.");
        }

        return new GrowthBudget(minutes is int m ? TimeSpan.FromMinutes(m) : null, candidates);
    }

    private static SpareRule SpareFrom(CliArgs args)
    {
        string text = args.Text("spare", SpareRule.Default.Share.ToString(Invariant));
        return double.TryParse(text, NumberStyles.Float, Invariant, out double share) && share >= 0
            ? SpareRule.Default with { Share = share }
            : throw new UsageException($"--spare expects a share of a class's levels (0.25 = a quarter more boards than levels), got '{text}'.");
    }

    private static int NonNegative(CliArgs args, string option, int fallback)
    {
        int value = args.Int(option, fallback);
        return value >= 0 ? value : throw new UsageException($"--{option} cannot be negative.");
    }

    /// <summary>A new bank's seed is its own: two banks should not draw the same boards.</summary>
    private static ulong NewBankSeed() => BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));

    private static void PrintInventory(BankStore store, BankScan scan)
    {
        BankCatalog catalog = scan.Catalog;
        long games = catalog.Candidates.Sum(candidate => candidate.Games.Sum(batch => (long)batch.Finishes.Length));
        int boards = catalog.Candidates.Select(candidate => candidate.Content.Board).Distinct().Count();
        Console.WriteLine($"bank {store.Root}  id {store.Identity.BankId}  created {store.Identity.CreatedUtc:u}");
        Console.WriteLine(string.Create(
            Invariant,
            $"shards {scan.Shards.Count}, {scan.Bytes / 1024.0 / 1024.0:0.0} MB, {scan.Records} records; torn tails {scan.TornTails}, damaged shards {scan.Shards.Count(shard => !shard.IsSound)}"));
        Console.WriteLine(string.Create(
            Invariant,
            $"candidates {catalog.Candidates.Count} on {boards} boards ({catalog.Candidates.Count(c => c.Content.Features.IsScalable)} with an amount goal), retracted {catalog.RetractedCount}; games {games}"));
        if (catalog.Candidates.Count > 0)
        {
            Console.WriteLine(string.Create(Invariant, $"bytes per candidate {scan.Bytes / catalog.Candidates.Count}, games per candidate {games / catalog.Candidates.Count}"));
        }

        foreach (EpochRecord epoch in catalog.Epochs)
        {
            bool current = epoch.Fingerprint == MeasurementFingerprint.OfThisBuild && Epochs.Current(catalog)?.ToString() == epoch.Epoch;
            Console.WriteLine($"epoch {epoch.Epoch}  opened {epoch.OpenedUtc:u}  {epoch.Runtime}  core {epoch.CoreAssembly} bot {epoch.BotAssembly}{(current ? "  <- this build" : string.Empty)}");
        }

        if (Epochs.Current(catalog) is EpochId own)
        {
            int measured = Epochs.MeasuredIn(catalog, own);
            Console.WriteLine(string.Create(
                Invariant,
                $"candidates with ladder games of this build's epoch: {measured} of {catalog.Candidates.Count}{(measured < catalog.Candidates.Count ? " (bank remeasure plays the rest)" : string.Empty)}"));
        }

        if (!scan.IsSound)
        {
            Console.WriteLine($"DAMAGED: {BankHealth.Describe(scan)}");
        }

        PrintDraws(catalog.DrawTotals);
        PrintKinds(catalog);
    }

    private static void PrintDraws(DrawTotals draws)
    {
        int stored = draws.Variants.GetValueOrDefault(DrawOutcome.Stored);
        string outcomes = string.Join(", ", Enum.GetValues<DrawOutcome>().Select(outcome => string.Create(Invariant, $"{outcome} {draws.Variants.GetValueOrDefault(outcome)}")));
        Console.WriteLine(string.Create(Invariant, $"draws {draws.Draws}; variants: {outcomes}"));
        if (stored > 0)
        {
            Console.WriteLine(string.Create(
                Invariant,
                $"measuring a stored candidate took {draws.MeasuredSeconds / stored:0.00} s on average; {draws.Variants.Values.Sum() / (double)stored:0.00} variants per stored candidate"));
        }
    }

    /// <summary>Candidates by size, colours and families: where the bank is thick and where it has nothing.</summary>
    private static void PrintKinds(BankCatalog catalog)
    {
        var kinds = catalog.Candidates
            .GroupBy(candidate => (candidate.Content.Features.Width, candidate.Content.Features.Height, candidate.Content.Features.Colors, candidate.Content.Features.FamilyLabel))
            .OrderBy(kind => kind.Key);
        Console.WriteLine("candidates by size, colours and families:");
        foreach (var kind in kinds)
        {
            Console.WriteLine(string.Create(Invariant, $"  {kind.Key.Width}x{kind.Key.Height} {kind.Key.Colors}c {kind.Key.FamilyLabel,-12} {kind.Count(),6}"));
        }
    }

    private static void PrintSupply(IReadOnlyList<ClassSupply> supply, Demand demand)
    {
        const int ThinnestShown = 12;
        foreach (PlayerMix aim in demand.Aims)
        {
            ClassSupply[] rows = supply.Where(row => row.Aim.Label == aim.Label).ToArray();
            Console.WriteLine(string.Create(
                Invariant,
                $"aim {aim.Label}: {rows.Length} classes, {rows.Sum(row => row.Class.Levels.Count)} levels; boards needed {rows.Sum(row => row.Needed)}, allotted {rows.Sum(row => row.Allotted)}, missing {rows.Sum(row => row.Deficit)}; classes short of boards {rows.Count(row => row.Deficit > 0)}"));
            foreach (ClassSupply row in rows.Where(row => row.Deficit > 0).OrderByDescending(row => row.Thinness).ThenBy(row => row.Class.Label, StringComparer.Ordinal).Take(ThinnestShown))
            {
                Console.WriteLine(string.Create(Invariant, $"  missing {row.Deficit,4} of {row.Needed,4} (fitting {row.Fitting.Count,4})  {row.Class.Label}"));
            }

            PrintFamilyMix(rows, demand.KindTargets);
        }
    }

    /// <summary>The boards that fit any class of the aim, by family set, against the mix the grower aims at.</summary>
    private static void PrintFamilyMix(ClassSupply[] rows, IReadOnlyDictionary<LevelKind, double> kindTargets)
    {
        BankCandidate[] fitting = rows.SelectMany(row => row.Fitting).DistinctBy(candidate => candidate.Content.Board).ToArray();
        if (fitting.Length == 0)
        {
            return;
        }

        double needed = rows.Sum(row => row.Needed);
        var targetBySet = rows
            .SelectMany(row => KindMix.SetsFor(row.Class.Structure).Select(set => (Label: KindMix.LabelOf(set), Boards: KindMix.TargetShare(row.Class.Structure, set, kindTargets) * row.Needed)))
            .GroupBy(target => target.Label)
            .ToDictionary(set => set.Key, set => set.Sum(target => target.Boards) / needed);
        Console.WriteLine("  fitting boards by families (share now, share aimed at):");
        foreach ((string label, double target) in targetBySet.OrderByDescending(set => set.Value))
        {
            int have = fitting.Count(candidate => KindMix.LabelOf(candidate.Content.Features.Families) == label);
            Console.WriteLine(string.Create(Invariant, $"    {label,-24} {have,5}  {have / (double)fitting.Length,6:P0}  {target,6:P0}"));
        }
    }

    private static IEnumerable<string> SupplyCsv(IReadOnlyList<ClassSupply> supply)
    {
        yield return "aim,class,tier,share,moves,levels,needed,fitting,allotted,missing";
        foreach (ClassSupply row in supply)
        {
            yield return string.Create(
                Invariant,
                $"{row.Aim.Label},{row.Class.Structure.Label},{row.Class.Tier},{row.Class.Share:0.###},{row.Class.Moves},{row.Class.Levels.Count},{row.Needed},{row.Fitting.Count},{row.Allotted},{row.Deficit}");
        }
    }

    /// <summary>Every variant the grower ever tried, one row each: what it aimed at and what came of it, to see which kinds a range cannot hold.</summary>
    private static IEnumerable<string> DrawsCsv(IReadOnlyList<DrawRecord> draws)
    {
        yield return "draw,variant,aim,target,amountStep,densityStep,outcome,candidate,probeLimit,limit,fit,seconds";
        foreach (DrawRecord draw in draws)
        {
            yield return string.Create(
                Invariant,
                $"{draw.Draw},{draw.Variant},{draw.Aim},{draw.Target.Replace(',', ';')},{draw.AmountStep},{draw.Spec.DensityStep},{draw.Outcome},{draw.Candidate},{draw.ProbeLimit},{draw.Limit},{(draw.Fit ? "true" : "false")},{draw.Seconds:0.00}");
        }
    }

    private static void PrintSummary(GrowthSummary summary, long bytesWritten, BankCatalog catalog)
    {
        string why = summary.End switch
        {
            GrowthEnd.BudgetSpent => "the budget is spent",
            GrowthEnd.DeepEnough => "every class has the boards it needs for every aim: the bank is as deep as --spare asks (raise --spare for more boards per level)",
            GrowthEnd.OutOfReach => "the bank is as deep as draws can make it: the classes still short of boards ask only for kinds that draws do not give them (listed below; a longer move range for the kind, not more time, would help)",
            GrowthEnd.Stopped => "stopped",
            _ => throw new InvalidOperationException($"Unhandled end {summary.End}."),
        };
        double minutes = Math.Max(summary.Elapsed.TotalMinutes, 1e-9);
        Console.WriteLine(string.Create(
            Invariant,
            $"{why}: {summary.Stored} candidates stored in {summary.Elapsed.TotalMinutes:0.0} min ({summary.Stored / minutes:0.0} per minute), {summary.Fits} of {summary.Visits} draws gave their target a board, {summary.Probes} variants probed out"));
        if (summary.Stored > 0)
        {
            Console.WriteLine(string.Create(Invariant, $"{bytesWritten} bytes written, {bytesWritten / summary.Stored} per stored candidate; the bank now holds {catalog.Candidates.Count} candidates"));
        }

        const int Shown = 12;
        foreach (string outOfReach in summary.OutOfReach.Take(Shown))
        {
            Console.WriteLine($"  out of reach: {outOfReach}");
        }

        if (summary.OutOfReach.Count > Shown)
        {
            Console.WriteLine(string.Create(Invariant, $"  ... and {summary.OutOfReach.Count - Shown} more classes (bank status --draws shows every variant tried)"));
        }
    }

    /// <summary>A line on stderr every half minute while the bank grows.</summary>
    private sealed class GrowthProgress
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private TimeSpan _lastLine = TimeSpan.Zero;
        private int _visits;
        private int _stored;
        private int _fits;

        public void Note(VisitResult visit)
        {
            _visits++;
            _stored += visit.Stored;
            _fits += visit.Fit ? 1 : 0;
            if (_clock.Elapsed - _lastLine < ProgressEvery)
            {
                return;
            }

            _lastLine = _clock.Elapsed;
            Console.Error.WriteLine(string.Create(
                Invariant,
                $"{_clock.Elapsed.TotalMinutes,6:0.0} min  stored {_stored}, fits {_fits}/{_visits} draws, {_stored / Math.Max(_clock.Elapsed.TotalMinutes, 1e-9):0.0} per minute; last: {visit.Target} for {visit.Aim}: {visit.Last}"));
        }
    }
}
