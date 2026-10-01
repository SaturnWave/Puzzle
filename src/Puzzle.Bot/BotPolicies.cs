using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Bot
{
    /// <summary>Creates the named player bots shared by the CLI, the web test UI and the level generator.</summary>
    public static class BotPolicies
    {
        public const string Random = "random";

        public static IReadOnlyList<string> Names { get; } =
            new[] { Random }.Concat(SkillProfile.All.Select(profile => profile.Name)).ToArray();

        public static bool TryCreate(string name, out IBotPolicy policy)
        {
            if (string.Equals(name, Random, StringComparison.OrdinalIgnoreCase))
            {
                policy = new RandomPolicy();
                return true;
            }

            if (SkillProfile.TryFind(name, out SkillProfile profile))
            {
                policy = new GreedyPolicy(profile);
                return true;
            }

            policy = new RandomPolicy();
            return false;
        }
    }
}
