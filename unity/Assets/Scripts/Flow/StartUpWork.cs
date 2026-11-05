#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// One piece of the work the game does as it starts, before the hall can be shown: reading the levels, the
    /// museum's catalogue or the economy, opening the player's session in the hall with its pictures. Its weight is
    /// its share of the whole as it was measured, so the entry page's bar moves about as the work does.
    /// </summary>
    public sealed class StartUpStep
    {
        /// <exception cref="ArgumentOutOfRangeException">The weight is not more than nothing.</exception>
        public StartUpStep(string name, float weight, Action work)
        {
            if (!(weight > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(weight), $"The step \"{name}\" weighs {weight}: a step of the start-up weighs more than nothing.");
            }

            Name = name;
            Weight = weight;
            Work = work ?? throw new ArgumentNullException(nameof(work));
        }

        /// <summary>What the step does, for the log that times the start-up.</summary>
        public string Name { get; }

        /// <summary>The step's share of the whole start-up, against the other steps' weights.</summary>
        public float Weight { get; }

        public Action Work { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The work the game does as it starts, done one step at a time: the running game does one step a frame, so a
    /// frame of the entry page is drawn between two steps and its bar follows the work (<see cref="LoadingBar"/>).
    /// The steps are done in their order, each once. No Unity dependency.
    /// </summary>
    /// <remarks>
    /// A step that throws is not done: what it threw goes to whoever asked for it, and the work stops there for good
    /// (<see cref="HasFailed"/>), so a game that cannot start says why once and does not try the same step every frame.
    /// </remarks>
    public sealed class StartUpWork
    {
        private readonly IReadOnlyList<StartUpStep> _steps;
        private readonly float _wholeWeight;
        private int _stepsDone;
        private float _weightDone;

        /// <summary>The step that was begun and never came to its end: it threw.</summary>
        private StartUpStep? _begunAndNotEnded;

        /// <exception cref="ArgumentException">There is no step.</exception>
        public StartUpWork(IReadOnlyList<StartUpStep> steps)
        {
            if (steps.Count == 0)
            {
                throw new ArgumentException("A start-up has at least one step.", nameof(steps));
            }

            _steps = steps;
            foreach (StartUpStep step in steps)
            {
                _wholeWeight += step.Weight;
            }
        }

        /// <summary>How much of the work is done, from 0 to 1 by the steps' weights: exactly 1 once every step is.</summary>
        public float Done => IsDone ? 1f : _weightDone / _wholeWeight;

        public bool IsDone => _stepsDone == _steps.Count;

        /// <summary>True once a step threw: the work goes no further.</summary>
        public bool HasFailed => _begunAndNotEnded != null;

        /// <summary>Does the next step and tells which it was.</summary>
        /// <exception cref="InvalidOperationException">Every step is done, or a step has failed.</exception>
        public StartUpStep DoTheNext()
        {
            if (_begunAndNotEnded is StartUpStep failed)
            {
                throw new InvalidOperationException($"The start-up failed at \"{failed.Name}\" and goes no further.");
            }

            if (IsDone)
            {
                throw new InvalidOperationException("Every step of the start-up is done.");
            }

            StartUpStep step = _steps[_stepsDone];
            _begunAndNotEnded = step;
            step.Work();
            _begunAndNotEnded = null;
            _stepsDone++;
            _weightDone += step.Weight;
            return step;
        }
    }
}
