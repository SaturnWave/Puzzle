using System.Globalization;
using System.Text;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Bank.Candidates;

/// <summary>
/// The text a candidate's identity is hashed from. It is the bank's own form, not the level file's: a field added to
/// the level file later, left at its default, changes no identity. Whoever changes a line here starts a new scheme
/// (the first line names it) and must not recompute stored ids; <c>CandidateIdentityTests</c> pins the text of a
/// fixed level so a change cannot pass unnoticed.
/// </summary>
internal static class CanonicalText
{
    private const string CandidateScheme = "c1";
    private const string BoardScheme = "b1";

    /// <summary>Everything that decides how a candidate plays: layout, colour count, spawn rules, goals with their amounts.</summary>
    public static string OfCandidate(LevelDefinition level) =>
        Compose(CandidateScheme, level, ItemsWithAmount(level.Spawn.Items), level.Goals.Select(GoalWithAmount));

    /// <summary>The same without the amounts: collect targets, the items to drop, and how many of them spawn.</summary>
    public static string OfBoard(LevelDefinition level) =>
        Compose(BoardScheme, level, Number(level.Spawn.Items.ChancePercent), level.Goals.Select(GoalKind));

    private static string Compose(string scheme, LevelDefinition level, string items, IEnumerable<string> goals)
    {
        LayoutLayers layers = level.Layout.ToLayers();
        SpawnRules spawn = level.Spawn;
        var text = new StringBuilder();
        text.Append(scheme).Append('\n');
        text.Append(Number(level.Layout.Shape.Width)).Append('x').Append(Number(level.Layout.Shape.Height)).Append('\n');
        text.Append("cells:").AppendJoin('|', layers.Cells).Append('\n');
        text.Append("tiles:").AppendJoin('|', layers.Tiles ?? []).Append('\n');
        text.Append("jelly:").AppendJoin('|', layers.Jelly ?? []).Append('\n');
        text.Append("chains:").AppendJoin('|', layers.Chains ?? []).Append('\n');
        text.Append("colors:").Append(Number(spawn.Colors)).Append('\n');
        text.Append("spawn:").AppendJoin(',', Number(spawn.SmoothingPermille), Number(spawn.GoalBiasPercent), Number(spawn.CascadeBiasPercent)).Append('\n');
        text.Append("items:").Append(items).Append('\n');
        text.Append("goals:").AppendJoin(';', goals).Append('\n');
        return text.ToString();
    }

    private static string ItemsWithAmount(ItemSpawnRules items) =>
        string.Join(',', Number(items.Total), Number(items.MaxOnBoard), Number(items.ChancePercent));

    private static string GoalWithAmount(GoalSpec goal) => $"{GoalKind(goal)}={Number(goal.Target)}";

    /// <summary>A collect goal is named with the palette slot of its colour: a candidate has slots, not colour names.</summary>
    private static string GoalKind(GoalSpec goal) => goal switch
    {
        GoalSpec.CollectColor collect => $"CollectColor/{Number(SlotOf(collect.Color))}",
        GoalSpec.BreakBoxes _ => "BreakBoxes",
        GoalSpec.ClearJelly _ => "ClearJelly",
        GoalSpec.BreakChains _ => "BreakChains",
        GoalSpec.DropItems _ => "DropItems",
        _ => throw new InvalidOperationException($"Unhandled goal {goal.GetType().Name}."),
    };

    private static int SlotOf(GemColor color) => (int)color;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
