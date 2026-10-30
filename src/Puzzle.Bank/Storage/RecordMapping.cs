using System.Globalization;
using System.Text.Json;
using Puzzle.Bank.Candidates;
using Puzzle.Core.Levels;
using Puzzle.Generator.Profiles;
using Puzzle.Tooling;

namespace Puzzle.Bank.Storage;

/// <summary>Between the bank's candidates and the records that store them.</summary>
public static class RecordMapping
{
    private const string DrawnKind = "drawn";
    private const string ImportedKind = "imported";

    public static BoardRecord ToRecord(BankCandidate candidate)
    {
        using JsonDocument level = JsonDocument.Parse(LevelJson.ToCanonicalJson(candidate.Content.Level));
        return new BoardRecord(
            candidate.Content.Id.ToString(),
            candidate.Content.Board.ToString(),
            level.RootElement.Clone(),
            [.. candidate.Seeds],
            new AmountsRecord(candidate.AmountStep, [.. candidate.Amounts.CollectTargets], candidate.Amounts.SpawnedItems),
            ToRecord(candidate.Origin),
            candidate.CreatedUtc);
    }

    /// <exception cref="BankFormatException">The record does not hold a candidate: a level that does not load, an id that is none, an unknown origin.</exception>
    public static BankCandidate ToCandidate(BoardRecord record)
    {
        try
        {
            LevelDefinition level = LevelJson.Parse(record.Level.GetRawText(), $"The board record {record.Id}");
            CandidateContent content = CandidateContent.FromStored(level, CandidateId.Parse(record.Id), BoardId.Parse(record.Board));
            var amounts = new AmountBase(record.Amounts.Collect, record.Amounts.SpawnedItems);
            return new BankCandidate(content, record.Seeds, amounts, record.Amounts.Step, ToOrigin(record.Origin), record.CreatedUtc);
        }
        catch (Exception ex) when (ex is LevelFormatException or FormatException)
        {
            throw new BankFormatException($"The board record {record.Id} does not hold a candidate: {ex.Message}", ex);
        }
    }

    public static DrawSpecRecord ToRecord(DrawSpec spec) => new(
        spec.Size.ToString(),
        spec.PlainShape,
        spec.Colors,
        spec.Families.Select(family => string.Create(CultureInfo.InvariantCulture, $"{family.Kind}:{family.Strength}")).ToArray(),
        spec.CollectGoals,
        spec.DensityStep,
        spec.FullJelly);

    /// <exception cref="FormatException">A size or a family does not read.</exception>
    public static DrawSpec ToSpec(DrawSpecRecord record) => new()
    {
        Size = BoardSize.TryParse(record.Size, out BoardSize size) ? size : throw new FormatException($"'{record.Size}' is not a board size."),
        PlainShape = record.Plain,
        Colors = record.Colors,
        Families = record.Families.Select(ToFamily).ToArray(),
        CollectGoals = record.CollectGoals,
        DensityStep = record.DensityStep,
        FullJelly = record.FullJelly,
    };

    private static OriginRecord ToRecord(CandidateOrigin origin) => origin switch
    {
        CandidateOrigin.Drawn drawn => new OriginRecord(DrawnKind, drawn.Draw, drawn.Variant, drawn.Seed, ToRecord(drawn.Spec), drawn.DrawCode, null),
        CandidateOrigin.Imported imported => new OriginRecord(ImportedKind, null, null, null, null, null, imported.From),
        _ => throw new InvalidOperationException($"Unhandled origin {origin.GetType().Name}."),
    };

    private static CandidateOrigin ToOrigin(OriginRecord record) => record switch
    {
        { Kind: DrawnKind, Draw: long draw, Variant: int variant, Seed: ulong seed, Spec: DrawSpecRecord spec, DrawCode: int drawCode }
            => new CandidateOrigin.Drawn(draw, variant, seed, ToSpec(spec), drawCode),
        { Kind: ImportedKind, ImportedFrom: string from } => new CandidateOrigin.Imported(from),
        _ => throw new FormatException($"'{record.Kind}' is not a complete origin."),
    };

    private static FamilyDraw ToFamily(string text)
    {
        string[] kindAndStrength = text.Split(':');
        return kindAndStrength.Length == 2
            && Enum.TryParse(kindAndStrength[0], out ObstacleKind kind)
            && Enum.IsDefined(kind)
            && int.TryParse(kindAndStrength[1], NumberStyles.None, CultureInfo.InvariantCulture, out int strength)
                ? new FamilyDraw(kind, strength)
                : throw new FormatException($"'{text}' is not an obstacle family with its strength.");
    }
}
