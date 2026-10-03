using System;
using System.Collections.Generic;

namespace Puzzle.Bot
{
    /// <summary>One round of sequential halving: the arms still in, and how many sampled futures each of them plays.</summary>
    internal sealed record HalvingRound(int Arms, int SamplesPerArm);

    /// <summary>
    /// How sequential halving spends a budget of simulations: round by round, every arm still in plays the same number
    /// of futures, then the better half goes on. Each round gets an even share of what is left, so budget a round
    /// cannot divide evenly flows to the finalists. Integer arithmetic only; the plan never exceeds the budget.
    /// </summary>
    internal sealed class HalvingPlan
    {
        private HalvingPlan(int arms, IReadOnlyList<HalvingRound> rounds)
        {
            Arms = arms;
            Rounds = rounds;
        }

        /// <summary>Candidates that enter the first round. With a single one there is nothing to compare and no round.</summary>
        public int Arms { get; }

        public IReadOnlyList<HalvingRound> Rounds { get; }

        /// <summary>Simulations the plan runs in total.</summary>
        public int Simulations
        {
            get
            {
                int total = 0;
                foreach (HalvingRound round in Rounds)
                {
                    total += round.Arms * round.SamplesPerArm;
                }

                return total;
            }
        }

        /// <param name="candidates">Moves that could be searched, at least one.</param>
        /// <param name="budget">Simulations allowed, at least two.</param>
        public static HalvingPlan For(int candidates, int budget)
        {
            if (candidates < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(candidates), candidates, "At least one candidate is needed.");
            }

            if (budget < UstaSettings.MinSimulations)
            {
                throw new ArgumentOutOfRangeException(nameof(budget), budget, $"A budget has at least {UstaSettings.MinSimulations} simulations.");
            }

            int arms = Math.Min(candidates, AffordableArms(budget));
            int roundCount = HalvingsToOne(arms);
            var rounds = new List<HalvingRound>(roundCount);
            int remaining = budget;
            int inPlay = arms;
            for (int round = 0; round < roundCount; round++)
            {
                int samples = Math.Max(1, remaining / (roundCount - round) / inPlay);
                rounds.Add(new HalvingRound(inPlay, samples));
                remaining -= samples * inPlay;
                inPlay = (inPlay + 1) / 2;
            }

            return new HalvingPlan(arms, rounds);
        }

        /// <summary>
        /// The most arms a budget can compare: two, or a third of the budget. Then the first round can give every arm a
        /// future of its own, and a round forced up to one future per arm can never outspend the budget.
        /// </summary>
        private static int AffordableArms(int budget) => Math.Max(2, budget / 3);

        private static int HalvingsToOne(int arms)
        {
            int rounds = 0;
            for (int inPlay = arms; inPlay > 1; inPlay = (inPlay + 1) / 2)
            {
                rounds++;
            }

            return rounds;
        }
    }
}
