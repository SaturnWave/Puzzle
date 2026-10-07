#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// What happens on the stage in the course of a turn: acts that each begin at a moment, run for a while and are
    /// then over. Time only reaches the timeline through <see cref="Advance"/>, so the same acts and the same steps
    /// always show the same thing: the running game steps it by the time of a frame, the Editor's storyboard by
    /// whatever moment it wants a picture of. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    /// <remarks>
    /// An act is only shown from its beginning on; before that it does not touch its target. Acts are shown in the
    /// order they begin (acts that begin together, in the order they were added), so where two acts move the same
    /// thing in one step, the one that began later has the last word.
    /// <para>
    /// An act that throws is a mistake of whoever wrote it, and the timeline does not hide it: the exception leaves
    /// <see cref="Advance"/>. But the timeline stays in order behind it. The act that threw is dropped, acts that
    /// were over stay over, and the acts the step had not reached yet are shown by the next step. So one faulty act
    /// costs its own picture and one step, and the timeline still comes to its end.
    /// </para>
    /// </remarks>
    public sealed class Timeline
    {
        private readonly List<Act> _acts = new List<Act>();
        private bool _isAdvancing;

        /// <summary>The seconds this timeline has been advanced by.</summary>
        public float Now { get; private set; }

        /// <summary>True when every act is over.</summary>
        public bool IsDone => _acts.Count == 0;

        /// <summary>Adds an act that lasts a while.</summary>
        /// <param name="startsIn">Seconds from now until the act begins.</param>
        /// <param name="seconds">How long it runs; nothing or less makes it an act of one moment.</param>
        /// <param name="show">Receives how far the act has come, from 0 to 1, in every step from its beginning on; exactly 1 in its last step.</param>
        /// <param name="whenOver">Called once, after the last <paramref name="show"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">The act would begin in the past.</exception>
        /// <exception cref="InvalidOperationException">An act tried to add an act while the timeline was being advanced.</exception>
        public void Add(float startsIn, float seconds, Action<float> show, Action? whenOver = null)
        {
            if (startsIn < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(startsIn), startsIn, "An act cannot begin in the past.");
            }

            if (_isAdvancing)
            {
                throw new InvalidOperationException("Acts are added between steps of the timeline, not by an act while it is shown.");
            }

            var act = new Act(Now + startsIn, seconds, show, whenOver);
            _acts.Insert(PlaceAfterEarlierActs(act.Start), act);
        }

        /// <summary>Adds something that happens at one moment.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The moment lies in the past.</exception>
        public void At(float startsIn, Action happen) => Add(startsIn, 0f, _ => { }, happen);

        /// <summary>Drops every act, shown or not, without telling any of them that it is over: what they were showing is given up.</summary>
        /// <exception cref="InvalidOperationException">An act tried to clear the timeline while it was being advanced.</exception>
        public void Clear()
        {
            if (_isAdvancing)
            {
                throw new InvalidOperationException("The timeline is cleared between its steps, not by an act while it is shown.");
            }

            _acts.Clear();
        }

        /// <exception cref="ArgumentOutOfRangeException">Time was asked to run backwards.</exception>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "The stage's clock only runs forwards.");
            }

            Now += deltaSeconds;
            _isAdvancing = true;
            try
            {
                ShowActsThatHaveBegun();
            }
            finally
            {
                _isAdvancing = false;
            }
        }

        /// <summary>Shows every act that has begun and drops the ones that are over; the others keep their order.</summary>
        private void ShowActsThatHaveBegun()
        {
            int kept = 0;
            int reached = 0;
            try
            {
                for (; reached < _acts.Count; reached++)
                {
                    Act act = _acts[reached];
                    if (act.Start > Now || !IsOverOnceShown(act))
                    {
                        _acts[kept++] = act;
                    }
                }
            }
            finally
            {
                // After a whole step "reached" is the end of the list, and this drops the acts that are over. When
                // an act threw, "reached" is that act: it is dropped with the acts that were over before it, and the
                // acts behind it stay as they are for the next step.
                int firstNotReached = Math.Min(reached + 1, _acts.Count);
                _acts.RemoveRange(kept, firstNotReached - kept);
            }
        }

        /// <summary>Shows an act as far as it has come, and tells it when that was its end.</summary>
        private bool IsOverOnceShown(Act act)
        {
            float progress = act.ProgressAt(Now);
            act.Show(progress);
            if (progress < 1f)
            {
                return false;
            }

            act.WhenOver?.Invoke();
            return true;
        }

        /// <summary>The place in the list behind every act that begins no later: the list stays in the order of beginning.</summary>
        private int PlaceAfterEarlierActs(float start)
        {
            int place = _acts.Count;
            while (place > 0 && _acts[place - 1].Start > start)
            {
                place--;
            }

            return place;
        }

        private readonly struct Act
        {
            public Act(float start, float seconds, Action<float> show, Action? whenOver)
            {
                Start = start;
                Seconds = seconds;
                Show = show;
                WhenOver = whenOver;
            }

            public float Start { get; }

            public float Seconds { get; }

            public Action<float> Show { get; }

            public Action? WhenOver { get; }

            public float ProgressAt(float now) => Seconds <= 0f ? 1f : Math.Min(1f, (now - Start) / Seconds);
        }
    }
}
