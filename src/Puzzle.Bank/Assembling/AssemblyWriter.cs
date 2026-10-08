using System.Globalization;
using System.Text.Json;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Tooling;

namespace Puzzle.Bank.Assembling;

/// <summary>
/// Writes an assembly into a folder: the level files and <c>report.csv</c> in the form the game, the test server and
/// the other tools read, and beside them what only the assembler knows: <c>assembly.csv</c> (one row per level: what
/// was decided, from which candidate, why, what its fresh games said, and the stamp it stands under) and
/// <c>assembly.manifest</c> (<see cref="AssemblyManifest"/>). Nothing written depends on a clock, so the same
/// assembly is the same bytes. The folder is written beside its place and moved into it at the end: it holds a whole
/// assembly or what it held before, never a mixture, and never a file of an earlier assembly.
/// </summary>
public static class AssemblyWriter
{
    public const string TableName = "assembly.csv";

    /// <summary>The rungs beside a level's player whose win share the table states, in skill points: how the level plays for someone weaker or stronger.</summary>
    public static readonly IReadOnlyList<int> NeighbourRungs = [-10, -5, 5, 10];

    private const string WritingSuffix = ".writing";
    private const string ReplacedSuffix = ".replaced";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly string[] IdentityColumns = ["level", "tier", "decision", "candidate", "board", "kinds", "key", "player", "share", "moveRange"];

    /// <summary>
    /// What the fresh games and the bank said of a level when it was decided. A level that is kept later was not
    /// decided again, so these cells travel with it from table to table: where a shipped level came from stays
    /// readable in the folder that ships it.
    /// </summary>
    private static readonly string[] CarriedColumns =
    [
        "origin", "record", "draftLimit", "confirmLimit", "calibratedLimit", "limit", "audits", "auditGames", "auditTarget", "auditWin", "firstTryWin", "firstTryGames",
        "checkWin", "checkGames", "poolSize", "winAtMinus10", "winAtMinus5", "winAtPlus5", "winAtPlus10", "tried",
    ];

    private static readonly string[] ClosingColumns = ["rules", "checks", "epoch", "notKept", "why", "turnedDown"];

    private static readonly string[] TableColumns = [.. IdentityColumns, .. CarriedColumns, .. ClosingColumns];

    /// <exception cref="IOException">The folder cannot be written, or the finished assembly could not be moved into its place (it is then whole beside it, under the name the message gives).</exception>
    public static void Write(Assembly assembly, string folder)
    {
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        string writing = target + WritingSuffix;
        DeleteIfThere(writing); // what an interrupted run left
        Directory.CreateDirectory(writing);
        foreach (LevelDecision decision in assembly.Decisions)
        {
            WriteLevelFile(decision, writing);
        }

        ReportTable baseReport = assembly.Base.Report;
        string[] bots = assembly.Policy.Plan.ReportBots.Order(StringComparer.Ordinal).ToArray();
        IReadOnlyList<string> columns = baseReport.Columns.Count > 0 ? baseReport.Columns : GenerationReport.ColumnsFor(bots);
        WriteLines(
            Path.Combine(writing, GenerationReport.FileName),
            assembly.Decisions.Select(decision => ReportRowOf(decision, assembly, columns, bots)).OfType<string>().Prepend(string.Join(',', columns)),
            baseReport.Newline);
        WriteLines(
            Path.Combine(writing, TableName),
            assembly.Decisions.Select(decision => string.Join(',', TableCells(decision, assembly).Select(Cell))).Prepend(string.Join(',', TableColumns)),
            Environment.NewLine);
        File.WriteAllText(Path.Combine(writing, AssemblyManifest.FileName), AssemblyManifest.Of(assembly, writing).ToJson());
        MoveIntoPlace(writing, target);
    }

    /// <summary>The written folder takes the place of what was there; the earlier content goes only once the new one stands.</summary>
    private static void MoveIntoPlace(string writing, string target)
    {
        string replaced = target + ReplacedSuffix;
        DeleteIfThere(replaced);
        try
        {
            if (Directory.Exists(target))
            {
                Directory.Move(target, replaced);
            }

            Directory.Move(writing, target);
        }
        catch (IOException ex)
        {
            throw new IOException($"The assembly is whole in '{writing}', but could not be moved to '{target}' (is a file of that folder open in another program?): {ex.Message}", ex);
        }

        DeleteIfThere(replaced);
    }

    private static void DeleteIfThere(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A copied level is copied as bytes, never read and written again: a pinned file stays the very file.</summary>
    private static void WriteLevelFile(LevelDecision decision, string folder)
    {
        string path = Path.Combine(folder, LevelFolder.FileNameOf(decision.Number));
        switch (decision)
        {
            case LevelDecision.Assembled made:
                LevelJson.Save(made.Level, path);
                break;
            case var copied when CopiedLevel(copied) is FolderLevel source:
                File.Copy(source.Path, path, overwrite: true);
                break;
            case LevelDecision.Unfilled:
                break; // the number had no level before and has none now; the table says why
            default:
                throw new InvalidOperationException($"Unhandled decision {decision.GetType().Name}.");
        }
    }

    /// <summary>The level of the base folder a decision carries over unchanged; absent for an assembled level and for a number without a level.</summary>
    internal static FolderLevel? CopiedLevel(LevelDecision decision) => decision switch
    {
        LevelDecision.Pinned pinned => pinned.Level,
        LevelDecision.Kept kept => kept.Level,
        LevelDecision.Unfilled unfilled => unfilled.Stale,
        LevelDecision.Assembled => null,
        _ => throw new InvalidOperationException($"Unhandled decision {decision.GetType().Name}."),
    };

    /// <summary>
    /// The level's report row: the base folder's row as it is for a pinned or kept level, a new one for an assembled
    /// level; absent when a copied level had no row. The row of a level that stays for want of a candidate says
    /// <see cref="FolderLevel.StaleMark"/> where a generated row says whether its range was relaxed, so no reader of
    /// the report takes it for a level of this policy.
    /// </summary>
    private static string? ReportRowOf(LevelDecision decision, Assembly assembly, IReadOnlyList<string> columns, string[] bots)
    {
        switch (decision)
        {
            case LevelDecision.Assembled made:
                string[] ownColumns = GenerationReport.ColumnsFor(bots);
                string[] cells = GenerationReport.CellsOf(MetricsOf(made, assembly, bots), bots);
                return string.Join(',', columns.Select(column => Array.IndexOf(ownColumns, column) is >= 0 and int at ? cells[at] : string.Empty));
            case LevelDecision.Unfilled { Stale.Row: ReportLine staleRow }:
                return staleRow.With("relaxed", FolderLevel.StaleMark);
            default:
                return CopiedLevel(decision)?.Row?.Text;
        }
    }

    /// <summary>
    /// The report row of an assembled level. The win share is read from the games played after the audit had
    /// passed, the other shares of the level's player from the passed audit; the report bots' shares are read from
    /// the candidate's stored games on all of its seeds, as the generator's were played on any seeds. An assembly
    /// never relaxes a move range and takes no time worth a column, so those two are constant.
    /// </summary>
    internal static LevelMetrics MetricsOf(LevelDecision.Assembled made, Assembly assembly, IReadOnlyList<string> bots)
    {
        SlotDifficulty ask = made.Slot.Difficulty!.For(made.Candidate.Content.Features);
        FinishedLevel finished = made.Finished;
        LadderReading reading = made.Candidate.ReadingIn(assembly.Bank!.Epoch);
        var botWinRates = new Dictionary<string, double>();
        foreach (string bot in bots)
        {
            if (PlayerMix.TryParse(bot, out PlayerMix? player, out _) && reading.ShareAt(player, finished.Limit) is double share)
            {
                botWinRates[bot] = share;
            }
        }

        return new LevelMetrics
        {
            Number = made.Number,
            Tier = made.Slot.Tier,
            Size = made.Slot.Plan.Size.ToString(),
            Playable = made.Level.Layout.Shape.PlayableCount,
            Colors = made.Level.Spawn.Colors,
            Style = StyleOf(made.Candidate.Content.Features),
            Goals = string.Join(' ', made.Level.Goals.Select(LevelGenerator.Describe)),
            MoveLimit = finished.Limit,
            CalibratedMoves = finished.CalibratedLimit,
            MoveMargin = ask.Margin,
            MoveRange = ask.Moves,
            CalibrationBot = ask.Player.Label,
            TargetWinRate = ask.Share,
            WinRate = finished.ReportedWin,
            WinRateWithExtraMoves = finished.WinPlus5,
            Floor = null,
            FloorSetLimit = false,
            MovesP50 = finished.MovesP50,
            MovesP90 = finished.MovesP90,
            NearMiss = finished.NearMiss,
            BotWinRates = botWinRates,
            PoolSize = finished.PoolSeeds.Count,
            PoolMinWinRate = finished.PoolMinWin,
            PoolMaxWinRate = finished.PoolMaxWin,
            CandidatesTried = made.TurnedDown.Count + 1,
            Relaxed = false,
            Seconds = 0,
        };
    }

    /// <summary>The outline and what lies on the board, in the manner of the generator's style column: <c>Shaped+Boxes+Chains</c>, <c>Full+Collect2</c>.</summary>
    private static string StyleOf(CandidateFeatures features)
    {
        IEnumerable<string> parts = features.Families.Select(family => family.ToString());
        if (features.CollectGoals > 0)
        {
            parts = parts.Append(string.Create(Invariant, $"Collect{features.CollectGoals}"));
        }

        return string.Join('+', parts.Prepend(features.IsPlain ? "Full" : "Shaped"));
    }

    private static IEnumerable<string> TableCells(LevelDecision decision, Assembly assembly)
    {
        CandidateContent? content = ContentOf(decision);
        SlotDifficulty? ask = content is null ? decision.Slot.Difficulty : decision.Slot.Difficulty?.For(content.Features);
        string[] identity =
        [
            Text(decision.Number),
            decision.Slot.Tier.ToString(),
            DecisionName(decision),
            content?.Id.ToString() ?? string.Empty,
            content?.Board.ToString() ?? string.Empty,
            content is null ? string.Empty : string.Join('+', LevelKinds.Of(content.Features)),
            KeyOf(decision),
            ask?.Player.Label ?? string.Empty,
            ask is null ? string.Empty : DecisionKey.ShareText(ask.Share),
            ask?.Moves.ToString() ?? string.Empty,
        ];
        IEnumerable<string> carried = decision is LevelDecision.Assembled made
            ? MeasuredCells(made, assembly)
            : CarriedColumns.Select(column => CopiedLevel(decision)?.Provenance?.Cell(column) ?? string.Empty);
        DecisionStamp? stamp = StampOf(decision, assembly);
        return identity.Concat(carried).Concat(
        [
            stamp?.Rules ?? string.Empty,
            stamp?.Checks ?? string.Empty,
            stamp?.Epoch ?? string.Empty,
            NotKeptOf(decision),
            WhyOf(decision),
            string.Join(" | ", TurnedDownOf(decision)),
        ]);
    }

    /// <summary>The cells of <see cref="CarriedColumns"/> for a level decided in this assembly, in their order.</summary>
    private static string[] MeasuredCells(LevelDecision.Assembled made, Assembly assembly)
    {
        FinishedLevel finished = made.Finished;
        return
        [
            OriginOf(made.Candidate.Origin),
            RecordOf(made.Candidate, assembly.Bank!.Epoch),
            Text(made.Choice.DraftLimit),
            Text(finished.ConfirmLimit),
            Text(finished.CalibratedLimit),
            Text(finished.Limit),
            Text(finished.Audits),
            Text(finished.AuditGames),
            Share(finished.AuditTarget),
            Share(finished.Win),
            Share(finished.FirstTryWin),
            Text(finished.FirstTryGames),
            finished.CheckWin is double checkWin ? Share(checkWin) : string.Empty,
            Text(finished.CheckGames),
            Text(finished.PoolSeeds.Count),
            .. NeighbourRungs.Select(points => NeighbourShare(made, assembly, points) is double share ? Share(share) : string.Empty),
            Text(made.TurnedDown.Count + 1),
        ];
    }

    /// <summary>The candidate behind the level a decision puts at its number; absent for a number without a level and for a level that is no candidate.</summary>
    internal static CandidateContent? ContentOf(LevelDecision decision) =>
        (decision as LevelDecision.Assembled)?.Candidate.Content ?? CopiedLevel(decision)?.Content;

    /// <summary>
    /// The stamp the level stands under in this set: the assembly's own for a level it decided or kept (a kept level
    /// is one whose stamp is the assembly's, or that it takes over), the stamp it came with for a pinned or stale
    /// level, or the assembly's when a pinned level came with none. An assembly without a bank knows no epoch, so a
    /// level it keeps goes on carrying the epoch it was decided in.
    /// </summary>
    private static DecisionStamp? StampOf(LevelDecision decision, Assembly assembly) => decision switch
    {
        LevelDecision.Kept kept when assembly.Stamp.Epoch.Length == 0 => assembly.Stamp with { Epoch = kept.Level.Stamp?.Epoch ?? string.Empty },
        LevelDecision.Assembled or LevelDecision.Kept => assembly.Stamp,
        LevelDecision.Pinned pinned => pinned.Level.Stamp ?? assembly.Stamp,
        LevelDecision.Unfilled unfilled => unfilled.Stale?.Stamp,
        _ => throw new InvalidOperationException($"Unhandled decision {decision.GetType().Name}."),
    };

    /// <summary>
    /// The win share at an assembled level's limit of the player <paramref name="points"/> skill points from its own,
    /// in the candidate's stored ladder games on all of its seeds; absent off the ladder's ends. How the level plays
    /// for someone weaker or stronger than the one it was made for.
    /// </summary>
    public static double? NeighbourShare(LevelDecision.Assembled made, Assembly assembly, int points) =>
        made.Slot.Difficulty!.Player.Shifted(points) is PlayerMix neighbour && assembly.Bank is BankAccess bank
            ? made.Candidate.ReadingIn(bank.Epoch).ShareAt(neighbour, made.Finished.Limit)
            : null;

    public static string DecisionName(LevelDecision decision) => decision switch
    {
        LevelDecision.Pinned => "pinned",
        LevelDecision.Kept => "kept",
        LevelDecision.Assembled { SameBoard: true } => "same-board",
        LevelDecision.Assembled => "new-board",
        LevelDecision.Unfilled => "unfilled",
        _ => throw new InvalidOperationException($"Unhandled decision {decision.GetType().Name}."),
    };

    internal static string KeyOf(LevelDecision decision) => decision switch
    {
        LevelDecision.Kept kept => kept.Key,
        LevelDecision.Assembled made => made.Key,
        _ => string.Empty,
    };

    private static string NotKeptOf(LevelDecision decision) => decision switch
    {
        LevelDecision.Assembled made => made.NotKept,
        LevelDecision.Unfilled unfilled => unfilled.NotKept,
        _ => string.Empty,
    };

    private static string WhyOf(LevelDecision decision) => decision switch
    {
        LevelDecision.Pinned pinned => pinned.Slot.PinnedBecause ?? string.Empty,
        LevelDecision.Kept => "decided under the inputs the policy still gives it",
        LevelDecision.Assembled made => made.Choice.Why,
        LevelDecision.Unfilled unfilled => unfilled.Stale is null ? unfilled.Reason : $"{unfilled.Reason}; the level it had is kept and no longer meets the policy",
        _ => throw new InvalidOperationException($"Unhandled decision {decision.GetType().Name}."),
    };

    private static IReadOnlyList<string> TurnedDownOf(LevelDecision decision) => decision switch
    {
        LevelDecision.Assembled made => made.TurnedDown,
        LevelDecision.Unfilled unfilled => unfilled.TurnedDown,
        _ => [],
    };

    private static string OriginOf(CandidateOrigin origin) => origin switch
    {
        CandidateOrigin.Drawn drawn => string.Create(Invariant, $"draw {drawn.Draw}.{drawn.Variant}"),
        CandidateOrigin.Imported imported => $"imported {imported.From}",
        _ => throw new InvalidOperationException($"Unhandled origin {origin.GetType().Name}."),
    };

    /// <summary>Where the candidate's first batch of ladder games lies in the bank: the record a reader starts from.</summary>
    private static string RecordOf(BankCandidate candidate, EpochId epoch) => candidate.Games
        .Where(batch => batch.Header.Epoch == epoch && batch.Header.Purpose == GamePurpose.Breadth)
        .Select(batch => batch.Location)
        .OfType<RecordLocation>()
        .Select(location => string.Create(Invariant, $"{location.File}@{location.Offset}"))
        .FirstOrDefault() ?? string.Empty;

    private static void WriteLines(string path, IEnumerable<string> lines, string newline) =>
        File.WriteAllText(path, string.Concat(lines.Select(line => line + newline)));

    /// <summary>A cell of free text: the table has no quoting, so a comma inside becomes a semicolon.</summary>
    private static string Cell(string text) => text.Replace(',', ';');

    private static string Text(int number) => number.ToString(Invariant);

    private static string Share(double share) => share.ToString("0.000", Invariant);
}
