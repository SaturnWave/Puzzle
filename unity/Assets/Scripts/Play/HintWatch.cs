#nullable enable
using System;
using Puzzle.Meta;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// When the game's hint shows itself on a board (the settings' hints, Puzzle.Meta's HintRules): the board has lain
    /// quiet and untouched for the rules' wait, the attempt has hints left, and none is showing. Quiet is the stage's
    /// to tell: at rest and taking a finger, with no card over it, no booster armed, no finale and moves left. Every
    /// step that is not quiet, and every touch, takes a hint that shows away at once and starts the wait again, so the
    /// wait always counts from the latest of them. A hint that is due and finds no move is not counted, and the wait
    /// starts again. It keeps time by the stage's clock, in seconds, and knows nothing of Unity: the repository's tests
    /// compile it too.
    /// </summary>
    public sealed class HintWatch
    {
        private readonly float _waitSeconds;
        private float _quietSince;

        /// <param name="clockSeconds">The stage's clock now: the wait counts from here.</param>
        public HintWatch(HintRules rules, float clockSeconds)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _waitSeconds = (float)rules.Idle.TotalSeconds;
            PerAttempt = rules.PerAttempt;
            _quietSince = clockSeconds;
        }

        /// <summary>Hints the attempt shows at most.</summary>
        public int PerAttempt { get; }

        /// <summary>Hints shown so far in the attempt; what its record counts.</summary>
        public int Shown { get; private set; }

        /// <summary>True from the moment a hint is shown until a touch, or a step that is not quiet, takes it away.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>When on the stage's clock the hint that shows was shown; meaningless while none shows.</summary>
        public float ShownAt { get; private set; }

        /// <summary>
        /// One step of the stage's clock. True when a hint is due now: the caller thinks out its move and calls
        /// <see cref="Show"/>, or <see cref="FoundNoMove"/> when there is none.
        /// </summary>
        /// <param name="isQuiet">True when the board lies at rest and takes a finger, and nothing over it or on it waits.</param>
        public bool IsDue(float clockSeconds, bool isQuiet)
        {
            if (!isQuiet)
            {
                WaitAgainFrom(clockSeconds);
                return false;
            }

            return !IsShowing && Shown < PerAttempt && clockSeconds - _quietSince >= _waitSeconds;
        }

        /// <summary>A finger came down anywhere on the screen: the hint goes, and the wait starts again.</summary>
        public void Touched(float clockSeconds) => WaitAgainFrom(clockSeconds);

        /// <summary>The hint that was due is shown now, and counts.</summary>
        /// <exception cref="InvalidOperationException">The attempt has shown all its hints, or one shows already.</exception>
        public void Show(float clockSeconds)
        {
            if (IsShowing || Shown >= PerAttempt)
            {
                throw new InvalidOperationException($"No hint can be shown now: {Shown} of {PerAttempt} shown, one showing: {IsShowing}.");
            }

            Shown++;
            IsShowing = true;
            ShownAt = clockSeconds;
        }

        /// <summary>The hint that was due found no move to show: nothing counts, and the wait starts again.</summary>
        public void FoundNoMove(float clockSeconds) => WaitAgainFrom(clockSeconds);

        private void WaitAgainFrom(float clockSeconds)
        {
            IsShowing = false;
            _quietSince = clockSeconds;
        }
    }
}
