using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Puzzle.Bot;

namespace Puzzle.Bank.Measuring;

/// <summary>A rung of the skill ladder and its share of a player mix.</summary>
public readonly record struct MixPart(int Skill, double Weight)
{
    public string RungName => SkillLadder.NameOf(Skill);
}

/// <summary>
/// Who a level is made for: one rung of the skill ladder, or several with weights that sum to 1. A mix wins the
/// weighted share of its rungs' wins, so its limit is read from the weighted curve, never from an average of its
/// rungs' limits (the two differ by half a move on real levels).
/// </summary>
public sealed class PlayerMix
{
    private const double WeightTolerance = 1e-6;

    private PlayerMix(IReadOnlyList<MixPart> parts) => Parts = parts;

    /// <summary>Weakest rung first; the weights sum to 1.</summary>
    public IReadOnlyList<MixPart> Parts { get; }

    public bool IsSingleRung => Parts.Count == 1;

    /// <summary><c>skill-55</c>, or <c>skill-45*0.25+skill-55*0.5+skill-65*0.25</c> for a mix (no comma, so it fits a CSV cell).</summary>
    public string Label => IsSingleRung
        ? Parts[0].RungName
        : string.Join('+', Parts.Select(part => string.Create(CultureInfo.InvariantCulture, $"{part.RungName}*{part.Weight:0.####}")));

    public static PlayerMix Of(SkillProfile rung) => new([new MixPart(rung.NoticePercent, 1)]);

    /// <summary>
    /// Reads a label: a rung (<c>skill-55</c>), a named profile standing for the rung that plays like it
    /// (<c>novice</c>), or weighted rungs joined by '+'. The random bot and the search bot are no ladder players.
    /// </summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out PlayerMix? mix, [NotNullWhen(false)] out string? problem)
    {
        mix = null;
        var parts = new List<MixPart>();
        foreach (string term in text.Split('+', StringSplitOptions.TrimEntries))
        {
            string[] nameAndWeight = term.Split('*', StringSplitOptions.TrimEntries);
            double weight = 1;
            bool weightReads = nameAndWeight.Length == 1
                || (nameAndWeight.Length == 2 && double.TryParse(nameAndWeight[1], NumberStyles.Float, CultureInfo.InvariantCulture, out weight) && weight > 0);
            if (!weightReads || RungNamed(nameAndWeight[0]) is not SkillProfile rung)
            {
                problem = $"'{term}' is not a ladder player: use {SkillLadder.NamePattern} or a named profile, with '*<weight>' in a mix.";
                return false;
            }

            parts.Add(new MixPart(rung.NoticePercent, weight));
        }

        if (Math.Abs(parts.Sum(part => part.Weight) - 1) > WeightTolerance || parts.Select(part => part.Skill).Distinct().Count() != parts.Count)
        {
            problem = $"The weights of '{text}' must sum to 1, each rung named once.";
            return false;
        }

        mix = new PlayerMix(parts.OrderBy(part => part.Skill).ToArray());
        problem = null;
        return true;
    }

    /// <summary>The same mix with every rung moved <paramref name="points"/> skill points; absent when a rung would leave the ladder.</summary>
    public PlayerMix? Shifted(int points)
    {
        MixPart[] moved = Parts.Select(part => part with { Skill = part.Skill + points }).ToArray();
        return moved.All(part => SkillLadder.TryFind(part.RungName, out _)) ? new PlayerMix(moved) : null;
    }

    private static SkillProfile? RungNamed(string name)
    {
        if (SkillLadder.TryFind(name, out SkillProfile? rung))
        {
            return rung;
        }

        return SkillProfile.TryFind(name, out SkillProfile named) ? SkillLadder.Rungs.FirstOrDefault(named.PlaysLike) : null;
    }
}
