using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Puzzle.Bot
{
    /// <summary>Creates the named player bots shared by the CLI, the web test UI and the level generator.</summary>
    public static class BotPolicies
    {
        public const string Random = "random";

        /// <summary>The fair search bot (<see cref="UstaPolicy"/>); "usta-&lt;n&gt;" names it with n simulations per move.</summary>
        public const string Usta = "usta";

        /// <summary>
        /// Where the random bot stands among the player models: below every greedy player, who notices at least 1 % of
        /// the legal moves and stands at that share.
        /// </summary>
        private const int RandomStrength = 0;

        /// <summary>
        /// The named player models: the random floor and the named greedy skill profiles, weakest first. These are what
        /// "sim --bot all" plays. The rungs of <see cref="SkillLadder"/> are player models too (<see cref="IsPlayerModel"/>)
        /// and stand between and below the named profiles (<see cref="NamesByStrength"/>); which of two player models is
        /// the weaker is told by <see cref="TryCompareStrength"/>, not by a place in this list.
        /// </summary>
        public static IReadOnlyList<string> Names { get; } =
            new[] { Random }.Concat(SkillProfile.All.Select(profile => profile.Name)).ToArray();

        /// <summary>
        /// Every player model once, weakest first: the random bot, then the <see cref="SkillLadder"/> with each named
        /// profile standing in for the rung that plays like it (novice for skill-50). This is what the web lab offers.
        /// </summary>
        public static IReadOnlyList<string> NamesByStrength { get; } =
            new[] { Random }.Concat(SkillLadder.Rungs.Select(NameStandingOn)).ToArray();

        /// <summary>
        /// Bots that measure how well a level can be played rather than model a player. They are much slower and are
        /// never used to set move limits; <see cref="TryCreate"/> also accepts "usta-&lt;n&gt;".
        /// </summary>
        public static IReadOnlyList<string> MeasurementNames { get; } = new[] { Usta };

        /// <summary>
        /// Creates the bot <paramref name="name"/> names (case-insensitive): random, a named skill profile, a rung of
        /// <see cref="SkillLadder"/> ("skill-55") or usta; false for any other name, a missing one included.
        /// </summary>
        /// <param name="policy">The named bot; a random bot when the name is not one, so it is never null.</param>
        public static bool TryCreate(string? name, out IBotPolicy policy)
        {
            if (name is null)
            {
                policy = new RandomPolicy();
                return false;
            }

            if (string.Equals(name, Random, StringComparison.OrdinalIgnoreCase))
            {
                policy = new RandomPolicy();
                return true;
            }

            if (GreedyProfileNamed(name) is SkillProfile profile)
            {
                policy = new GreedyPolicy(profile);
                return true;
            }

            if (UstaSettingsNamed(name) is UstaSettings settings)
            {
                policy = new UstaPolicy(settings);
                return true;
            }

            policy = new RandomPolicy();
            return false;
        }

        /// <summary>
        /// Whether <paramref name="name"/> names a player model (case-insensitive): the random bot, a named skill profile
        /// or a rung of <see cref="SkillLadder"/>. A measurement bot is not one, nor is a missing name.
        /// </summary>
        public static bool IsPlayerModel(string? name) => StrengthOf(name) is int;

        /// <summary>
        /// Compares how strong two player models are, by name (case-insensitive). The random bot is the weakest; the
        /// greedy players stand by the share of moves they notice, so a named profile is as strong as the ladder rung
        /// with its settings (novice and skill-50).
        /// </summary>
        /// <param name="order">
        /// Negative when <paramref name="first"/> is the weaker, zero when both are equally strong, positive when it is
        /// the stronger; zero too when the names cannot be compared.
        /// </param>
        /// <returns>False when either name is not a player model (<see cref="IsPlayerModel"/>).</returns>
        public static bool TryCompareStrength(string? first, string? second, out int order)
        {
            if (StrengthOf(first) is int firstStrength && StrengthOf(second) is int secondStrength)
            {
                order = firstStrength.CompareTo(secondStrength);
                return true;
            }

            order = 0;
            return false;
        }

        /// <summary>The name a rung goes by among all player models: that of the named profile that plays like it, else its own.</summary>
        private static string NameStandingOn(SkillProfile rung)
        {
            foreach (SkillProfile named in SkillProfile.All)
            {
                if (named.PlaysLike(rung))
                {
                    return named.Name;
                }
            }

            return rung.Name;
        }

        /// <summary>
        /// Where a player model stands, weakest lowest: <see cref="RandomStrength"/> for the random bot, and for a
        /// greedy player the share of moves it notices (on the ladder, which the named profiles are on, the temperature
        /// follows from that share). Absent for any other name.
        /// </summary>
        private static int? StrengthOf(string? name)
        {
            if (name is null)
            {
                return null;
            }

            if (string.Equals(name, Random, StringComparison.OrdinalIgnoreCase))
            {
                return RandomStrength;
            }

            return GreedyProfileNamed(name)?.NoticePercent;
        }

        /// <summary>The greedy player <paramref name="name"/> names: a named profile or a ladder rung; absent for any other name.</summary>
        private static SkillProfile? GreedyProfileNamed(string name)
        {
            if (SkillProfile.TryFind(name, out SkillProfile named))
            {
                return named;
            }

            return SkillLadder.TryFind(name, out SkillProfile? rung) ? rung : null;
        }

        /// <summary>
        /// The settings "usta" or "usta-&lt;simulations&gt;" stands for (case-insensitive; simulations in
        /// <see cref="UstaSettings.MinSimulations"/>..<see cref="UstaSettings.MaxSimulations"/>); absent for any other name.
        /// </summary>
        private static UstaSettings? UstaSettingsNamed(string name)
        {
            if (string.Equals(name, Usta, StringComparison.OrdinalIgnoreCase))
            {
                return UstaSettings.Default;
            }

            string prefix = Usta + "-";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int simulations)
                && simulations >= UstaSettings.MinSimulations
                && simulations <= UstaSettings.MaxSimulations)
            {
                return UstaSettings.Default with { Simulations = simulations };
            }

            return null;
        }
    }
}
