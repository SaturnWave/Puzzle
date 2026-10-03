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
        /// The player models: the random floor and the greedy skill profiles, weakest first. These are what calibration,
        /// "sim --bot all" and the web lab's bot list use; the level generator relies on the order (a floor bot must be
        /// weaker than the calibration bot).
        /// </summary>
        public static IReadOnlyList<string> Names { get; } =
            new[] { Random }.Concat(SkillProfile.All.Select(profile => profile.Name)).ToArray();

        /// <summary>
        /// Bots that measure how well a level can be played rather than model a player. They are much slower and are
        /// never used to set move limits; <see cref="TryCreate"/> also accepts "usta-&lt;n&gt;".
        /// </summary>
        public static IReadOnlyList<string> MeasurementNames { get; } = new[] { Usta };

        /// <summary>Creates the bot <paramref name="name"/> names (case-insensitive); false for any other name, a missing one included.</summary>
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

            if (SkillProfile.TryFind(name, out SkillProfile profile))
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
                && int.TryParse(name.AsSpan(prefix.Length),NumberStyles.None, CultureInfo.InvariantCulture, out int simulations)
                && simulations >= UstaSettings.MinSimulations
                && simulations <= UstaSettings.MaxSimulations)
            {
                return UstaSettings.Default with { Simulations = simulations };
            }

            return null;
        }
    }
}
