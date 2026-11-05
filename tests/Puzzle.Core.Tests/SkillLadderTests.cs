using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;

namespace Puzzle.Core.Tests;

/// <summary>
/// The skill ladder (<see cref="SkillLadder"/>): greedy players five points apart between and below the named profiles,
/// which keep their names and settings and each play exactly like one rung.
/// </summary>
public class SkillLadderTests
{
    [Theory]
    [InlineData("skill-30", 30, 18.0)]
    [InlineData("skill-35", 35, 16.5)]
    [InlineData("skill-40", 40, 15.0)]
    [InlineData("skill-45", 45, 13.5)]
    [InlineData("skill-50", 50, 12.0)]
    [InlineData("skill-55", 55, 10.5)]
    [InlineData("skill-60", 60, 9.0)]
    [InlineData("skill-65", 65, 7.5)]
    [InlineData("skill-70", 70, 6.0)]
    [InlineData("skill-75", 75, 5.0)]
    [InlineData("skill-80", 80, 4.0)]
    [InlineData("skill-85", 85, 3.0)]
    [InlineData("skill-90", 90, 2.0)]
    [InlineData("skill-95", 95, 1.0)]
    [InlineData("skill-100", 100, 0.0)]
    public void A_rung_notices_its_skill_and_takes_its_temperature_from_the_lines_through_the_named_profiles(string name, int noticePercent, double temperature)
    {
        Assert.True(SkillLadder.TryFind(name, out SkillProfile? rung));

        Assert.Equal((name, noticePercent, temperature), (rung.Name, rung.NoticePercent, rung.Temperature));
        Assert.Equal(name, SkillLadder.NameOf(noticePercent));
    }

    [Fact]
    public void The_ladder_has_fifteen_rungs_from_30_to_100_weakest_first()
    {
        Assert.Equal(Enumerable.Range(0, 15).Select(rung => 30 + (5 * rung)), SkillLadder.Rungs.Select(rung => rung.NoticePercent));
        Assert.Equal(SkillLadder.Rungs.Select(rung => rung.Temperature).OrderByDescending(temperature => temperature), SkillLadder.Rungs.Select(rung => rung.Temperature));
        Assert.Equal("skill-<n> (n = 30, 35 .. 100)", SkillLadder.NamePattern);
    }

    [Fact]
    public void The_named_profiles_keep_their_names_settings_and_order()
    {
        Assert.Equal(
            new[] { ("novice", 50, 12.0), ("casual", 70, 6.0), ("average", 85, 3.0), ("expert", 100, 0.0) },
            SkillProfile.All.Select(profile => (profile.Name, profile.NoticePercent, profile.Temperature)));
    }

    [Theory]
    [InlineData("novice", "skill-50")]
    [InlineData("casual", "skill-70")]
    [InlineData("average", "skill-85")]
    [InlineData("expert", "skill-100")]
    public void A_named_profile_and_the_rung_with_its_settings_play_the_same_games(string named, string rungName)
    {
        Assert.True(SkillProfile.TryFind(named, out SkillProfile profile));
        Assert.True(SkillLadder.TryFind(rungName, out SkillProfile? rung));
        LevelDefinition level = GameTests.ObstacleLevel();

        Assert.True(profile.PlaysLike(rung));
        Assert.Equal((profile.NoticePercent, profile.Temperature), (rung.NoticePercent, rung.Temperature));
        for (ulong seed = 0; seed < 3; seed++)
        {
            RunResult asNamed = SimulationRunner.Play(level, new GreedyPolicy(profile), seed, moveCap: 150);
            RunResult asRung = SimulationRunner.Play(level, Create(rungName), seed, moveCap: 150);

            Assert.Equal(asNamed.CompletedAtMove, asRung.CompletedAtMove);
            Assert.Equal(asNamed.ProgressAfterMove, asRung.ProgressAfterMove);
        }
    }

    [Fact]
    public void Only_the_rungs_between_the_named_profiles_play_like_none_of_them()
    {
        string[] likeANamedProfile = SkillLadder.Rungs
            .Where(rung => SkillProfile.All.Any(named => named.PlaysLike(rung)))
            .Select(rung => rung.Name)
            .ToArray();

        Assert.Equal(new[] { "skill-50", "skill-70", "skill-85", "skill-100" }, likeANamedProfile);
    }

    [Theory]
    [InlineData("skill-55", "greedy-skill-55")]
    [InlineData("SKILL-30", "greedy-skill-30")]
    [InlineData("Skill-100", "greedy-skill-100")]
    public void A_ladder_name_creates_the_greedy_bot_of_that_rung(string name, string expectedName)
    {
        Assert.True(BotPolicies.TryCreate(name, out IBotPolicy policy));

        Assert.IsType<GreedyPolicy>(policy);
        Assert.Equal(expectedName, policy.Name);
        Assert.True(BotPolicies.IsPlayerModel(name));
    }

    [Theory]
    [InlineData("skill-52")]    // between two rungs
    [InlineData("skill-25")]    // below the ladder
    [InlineData("skill-105")]   // above it
    [InlineData("skill-0")]
    [InlineData("skill-x")]
    [InlineData("skill-")]
    [InlineData("skill")]
    [InlineData("skill50")]
    [InlineData("skill-050")]
    [InlineData("skill-+50")]
    [InlineData("skill- 50")]
    [InlineData("skill-50 ")]
    [InlineData("skill-5.5e1")]
    public void A_name_off_the_ladder_is_not_a_bot(string name)
    {
        Assert.False(BotPolicies.TryCreate(name, out _));
        Assert.False(SkillLadder.TryFind(name, out _));
        Assert.False(BotPolicies.IsPlayerModel(name));
        Assert.False(BotPolicies.TryCompareStrength(name, "random", out _));
    }

    [Fact]
    public void Every_player_model_is_listed_once_weakest_first_with_the_named_profiles_on_their_rungs()
    {
        Assert.Equal(
            new[]
            {
                "random", "skill-30", "skill-35", "skill-40", "skill-45", "novice", "skill-55", "skill-60", "skill-65",
                "casual", "skill-75", "skill-80", "average", "skill-90", "skill-95", "expert",
            },
            BotPolicies.NamesByStrength);
        Assert.Equal(new[] { "random", "novice", "casual", "average", "expert" }, BotPolicies.Names);
    }

    [Fact]
    public void Each_player_model_is_weaker_than_every_one_listed_after_it()
    {
        IReadOnlyList<string> weakestFirst = BotPolicies.NamesByStrength;

        for (int weaker = 0; weaker < weakestFirst.Count; weaker++)
        {
            Assert.Equal(0, StrengthOrder(weakestFirst[weaker], weakestFirst[weaker]));
            for (int stronger = weaker + 1; stronger < weakestFirst.Count; stronger++)
            {
                Assert.True(StrengthOrder(weakestFirst[weaker], weakestFirst[stronger]) < 0, $"{weakestFirst[weaker]} is not weaker than {weakestFirst[stronger]}");
                Assert.True(StrengthOrder(weakestFirst[stronger], weakestFirst[weaker]) > 0, $"{weakestFirst[stronger]} is not stronger than {weakestFirst[weaker]}");
            }
        }
    }

    [Theory]
    [InlineData("novice", "skill-50")]
    [InlineData("casual", "skill-70")]
    [InlineData("average", "skill-85")]
    [InlineData("expert", "skill-100")]
    public void A_named_profile_is_as_strong_as_its_rung_and_stands_between_that_rungs_neighbours(string named, string rungName)
    {
        Assert.True(SkillLadder.TryFind(rungName, out SkillProfile? rung));
        string rungBelow = SkillLadder.NameOf(rung.NoticePercent - SkillLadder.Step);

        Assert.Equal(0, StrengthOrder(named, rungName));
        Assert.Equal(0, StrengthOrder(rungName.ToUpperInvariant(), named.ToUpperInvariant()));
        Assert.True(StrengthOrder(rungBelow, named) < 0, $"{rungBelow} is not weaker than {named}");
        Assert.True(StrengthOrder("random", named) < 0);
    }

    [Theory]
    [InlineData("usta")]
    [InlineData("usta-48")]
    [InlineData("beginner")]
    [InlineData(null)]
    public void Strength_is_compared_between_player_models_only(string? notAPlayerModel)
    {
        Assert.False(BotPolicies.IsPlayerModel(notAPlayerModel));
        Assert.False(BotPolicies.TryCompareStrength(notAPlayerModel, "average", out _));
        Assert.False(BotPolicies.TryCompareStrength("skill-60", notAPlayerModel, out _));
    }

    private static IBotPolicy Create(string name) =>
        BotPolicies.TryCreate(name, out IBotPolicy policy) ? policy : throw new ArgumentException($"'{name}' is not a bot.", nameof(name));

    /// <summary>Negative when <paramref name="first"/> is the weaker player model, zero for equals, positive when it is the stronger.</summary>
    private static int StrengthOrder(string first, string second)
    {
        Assert.True(BotPolicies.TryCompareStrength(first, second, out int order), $"{first} and {second} cannot be compared");
        return order;
    }
}
