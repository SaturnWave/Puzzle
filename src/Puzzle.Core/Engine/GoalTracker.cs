using System;
using System.Collections.Generic;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine
{
    public readonly struct GoalProgress
    {
        internal GoalProgress(GoalSpec goal, int done)
        {
            Goal = goal;
            Done = done;
        }

        public GoalSpec Goal { get; }

        /// <summary>Counted so far; may exceed the target.</summary>
        public int Done { get; }

        public int Remaining => Math.Max(0, Goal.Target - Done);

        public bool IsComplete => Done >= Goal.Target;

        /// <summary>0..1, clamped at the target.</summary>
        public double Fraction => Math.Min(Done, Goal.Target) / (double)Goal.Target;
    }

    /// <summary>Counts progress toward each goal of the level.</summary>
    public sealed class GoalTracker
    {
        private readonly GoalSpec[] _goals;
        private readonly int[] _done;

        internal GoalTracker(IReadOnlyList<GoalSpec> goals)
        {
            _goals = new GoalSpec[goals.Count];
            for (int i = 0; i < goals.Count; i++)
            {
                _goals[i] = goals[i];
            }

            _done = new int[_goals.Length];
        }

        private GoalTracker(GoalTracker source)
        {
            _goals = source._goals;
            _done = (int[])source._done.Clone();
        }

        public int Count => _goals.Length;

        public bool AllComplete
        {
            get
            {
                for (int i = 0; i < _goals.Length; i++)
                {
                    if (_done[i] < _goals[i].Target)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Mean completion fraction over all goals, 0..1.</summary>
        public double OverallProgress
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < _goals.Length; i++)
                {
                    sum += this[i].Fraction;
                }

                return sum / _goals.Length;
            }
        }

        public GoalProgress this[int index] => new GoalProgress(_goals[index], _done[index]);

        internal bool WantsBoxes => Wants<GoalSpec.BreakBoxes>();

        internal bool WantsJelly => Wants<GoalSpec.ClearJelly>();

        internal bool WantsChains => Wants<GoalSpec.BreakChains>();

        internal bool WantsItems => Wants<GoalSpec.DropItems>();

        internal bool WantsColor(GemColor color)
        {
            for (int i = 0; i < _goals.Length; i++)
            {
                if (_goals[i] is GoalSpec.CollectColor collect && collect.Color == color && _done[i] < collect.Target)
                {
                    return true;
                }
            }

            return false;
        }

        internal void RecordGemCleared(GemColor color)
        {
            for (int i = 0; i < _goals.Length; i++)
            {
                if (_goals[i] is GoalSpec.CollectColor collect && collect.Color == color)
                {
                    _done[i]++;
                }
            }
        }

        internal void RecordBoxDestroyed() => Increment<GoalSpec.BreakBoxes>();

        internal void RecordJellyCleared() => Increment<GoalSpec.ClearJelly>();

        internal void RecordChainBroken() => Increment<GoalSpec.BreakChains>();

        internal void RecordItemCollected() => Increment<GoalSpec.DropItems>();

        internal GoalTracker Clone() => new GoalTracker(this);

        /// <summary>Takes the counts of <paramref name="earlier"/>, a clone of this tracker: what was counted since is forgotten.</summary>
        internal void RestoreFrom(GoalTracker earlier) => Array.Copy(earlier._done, _done, _done.Length);

        private bool Wants<TGoal>()
            where TGoal : GoalSpec
        {
            for (int i = 0; i < _goals.Length; i++)
            {
                if (_goals[i] is TGoal && _done[i] < _goals[i].Target)
                {
                    return true;
                }
            }

            return false;
        }

        private void Increment<TGoal>()
            where TGoal : GoalSpec
        {
            for (int i = 0; i < _goals.Length; i++)
            {
                if (_goals[i] is TGoal)
                {
                    _done[i]++;
                }
            }
        }
    }
}
