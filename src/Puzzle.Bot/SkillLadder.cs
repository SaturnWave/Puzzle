using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Puzzle.Bot
{
    /// <summary>
    /// Greedy players between and below the named <see cref="SkillProfile"/>s, five points apart: the rung "skill-&lt;n&gt;"
    /// notices n % of the legal moves, and its temperature lies on the straight lines through the named profiles (novice
    /// 50 → 12, casual 70 → 6, average 85 → 3, expert 100 → 0). So skill-50, skill-70, skill-85 and skill-100 play
    /// exactly like novice, casual, average and expert, and the other rungs fill the gaps between them and reach below
    /// the novice. On the ladder both settings improve together, so a rung's skill alone tells how strong it is.
    /// </summary>
    public static class SkillLadder
    {
        public const int WeakestSkill = 30;

        public const int StrongestSkill = 100;

        /// <summary>The skill points between two neighbouring rungs.</summary>
        public const int Step = 5;

        /// <summary>A rung is named by this and its skill: "skill-55".</summary>
        public const string NamePrefix = "skill-";

        /// <summary>Every rung, weakest first.</summary>
        public static IReadOnlyList<SkillProfile> Rungs { get; } = Enumerable
            .Range(0, ((StrongestSkill - WeakestSkill) / Step) + 1)
            .Select(rung => RungAt(WeakestSkill + (rung * Step)))
            .ToArray();

        /// <summary>How the rungs are named, for a message that lists the bots: "skill-&lt;n&gt; (n = 30, 35 .. 100)".</summary>
        public static string NamePattern { get; } = string.Format(
            CultureInfo.InvariantCulture,
            "{0}<n> (n = {1}, {2} .. {3})",
            NamePrefix,
            WeakestSkill,
            WeakestSkill + Step,
            StrongestSkill);

        /// <summary>The name of the rung with <paramref name="skill"/>; only the skills of <see cref="Rungs"/> name a bot.</summary>
        public static string NameOf(int skill) => NamePrefix + skill.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The rung <paramref name="name"/> names (case-insensitive). No other name is one: not a skill between two
        /// rungs ("skill-52"), nor one off either end of the ladder ("skill-25", "skill-105").
        /// </summary>
        public static bool TryFind(string name, [NotNullWhen(true)] out SkillProfile? rung)
        {
            foreach (SkillProfile candidate in Rungs)
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    rung = candidate;
                    return true;
                }
            }

            rung = null;
            return false;
        }

        private static SkillProfile RungAt(int skill) =>
            new SkillProfile { Name = NameOf(skill), NoticePercent = skill, Temperature = TemperatureAt(skill) };

        /// <summary>
        /// The temperature of the player who notices <paramref name="noticePercent"/> of the moves, on the straight line
        /// between the two named profiles around it. Below the weakest named profile the line to the next one goes on.
        /// A named profile's own notice share gives exactly its own temperature: it is the weaker end of its line, where
        /// nothing is added, and the expert is the end of the last line, where the whole difference is.
        /// </summary>
        private static double TemperatureAt(int noticePercent)
        {
            IReadOnlyList<SkillProfile> named = SkillProfile.All;
            int stronger = 1;
            while (stronger < named.Count - 1 && named[stronger].NoticePercent <= noticePercent)
            {
                stronger++;
            }

            SkillProfile from = named[stronger - 1];
            SkillProfile to = named[stronger];
            double difference = to.Temperature - from.Temperature;
            return from.Temperature + (difference * (noticePercent - from.NoticePercent) / (to.NoticePercent - from.NoticePercent));
        }
    }
}
