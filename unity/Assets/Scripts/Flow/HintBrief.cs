#nullable enable
using System;
using Puzzle.Bot;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What a board needs to show the game's hints in one attempt: the rules of when and how many (Puzzle.Meta's
    /// HintRules), the advisor that thinks out the move (Puzzle.Bot's HintAdvisor, playing as the bot the rules name),
    /// and the attempt's play seed that seeds it, so that the same attempt at the same point shows the same hint. A
    /// stage opened without one shows none: the Editor's studies, whose pictures must not change with how long they wait.
    /// No Unity dependency.
    /// </summary>
    public sealed class HintBrief
    {
        private HintBrief(HintRules rules, HintAdvisor advisor, ulong attemptSeed)
        {
            Rules = rules;
            Advisor = advisor;
            AttemptSeed = attemptSeed;
        }

        public HintRules Rules { get; }

        /// <summary>Thinks out the move a hint shows; it keeps scratch buffers, so one brief serves one board.</summary>
        public HintAdvisor Advisor { get; }

        /// <summary>The attempt's play seed (AttemptSeeds.Play), which seeds the hints' own choices.</summary>
        public ulong AttemptSeed { get; }

        /// <summary>The hints of an attempt by the rules of the economy's settings.</summary>
        /// <exception cref="InvalidOperationException">The settings name a bot that gives no hints: they are the game's own, and the tests hold them to a player model.</exception>
        public static HintBrief Of(HintRules rules, ulong attemptSeed)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return HintAdvisor.TryCreate(rules.Bot, out HintAdvisor? advisor)
                ? new HintBrief(rules, advisor, attemptSeed)
                : throw new InvalidOperationException($"The economy settings' hints name the bot \"{rules.Bot}\", which is no player model of the bot family and gives no hints.");
        }
    }
}
