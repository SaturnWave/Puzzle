using Puzzle.Bank.Measuring;
using Puzzle.Bot;

namespace Puzzle.Economy.Units;

/// <summary>
/// A rung of the skill ladder (docs/GAME_DESIGN.md section 7) as the kind of player it stands for: "skill-55" notices
/// 55 % of the moves. Only rungs the ladder has can be made, so a value of this type always names a bot that exists.
/// </summary>
public readonly record struct SkillRung : IComparable<SkillRung>
{
    private SkillRung(int skill) => Skill = skill;

    /// <summary>The share of the legal moves the rung notices; a rung's skill alone tells how strong it is.</summary>
    public int Skill { get; }

    public string Name => SkillLadder.NameOf(Skill);

    /// <summary>The rung as stored games name their player: the name with the settings it plays with today.</summary>
    public PlayerModel Model => SkillLadder.TryFind(Name, out SkillProfile? rung)
        ? PlayerModel.Of(rung)
        : throw new InvalidOperationException($"'{Name}' is no rung of the skill ladder.");

    /// <summary>The rung <paramref name="name"/> names; absent for any other name (<see cref="SkillLadder.TryFind"/>).</summary>
    public static SkillRung? Find(string name) =>
        SkillLadder.TryFind(name, out SkillProfile? found) ? new SkillRung(found.NoticePercent) : null;

    /// <exception cref="ArgumentException">The name is no rung of the ladder.</exception>
    public static SkillRung Parse(string name) => Find(name)
        ?? throw new ArgumentException($"'{name}' is no rung of the skill ladder ({SkillLadder.NamePattern}).", nameof(name));

    public int CompareTo(SkillRung other) => Skill.CompareTo(other.Skill);

    public override string ToString() => Name;

    public static bool operator <(SkillRung left, SkillRung right) => left.Skill < right.Skill;

    public static bool operator >(SkillRung left, SkillRung right) => left.Skill > right.Skill;

    public static bool operator <=(SkillRung left, SkillRung right) => left.Skill <= right.Skill;

    public static bool operator >=(SkillRung left, SkillRung right) => left.Skill >= right.Skill;
}
