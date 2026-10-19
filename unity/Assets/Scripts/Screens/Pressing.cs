#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>Something on a screen that a finger can press: a button, a place on the navigation bar.</summary>
    public interface IPressable
    {
        /// <summary>True when a point of the screen, in the layout's points, lies on the control.</summary>
        bool IsUnder(Vector2 point);

        /// <summary>Shows the control held down, or let go.</summary>
        void ShowPressed(bool isPressed);
    }

    /// <summary>
    /// What can be pressed on a screen, each control with what pressing it asks for, and the finger that presses.
    /// A control is pressed when the finger comes down on it and lifts on it; a finger that slides off lets it go,
    /// one that slides back holds it down again, as buttons behave everywhere. One finger, one control at a time.
    /// A control that stops being pressable while the finger holds it (it is dimmed, set aside or taken out of
    /// sight) is not pressed by the lift: it is asked once more as the finger lifts.
    /// </summary>
    /// <typeparam name="TAction">What a press asks for.</typeparam>
    public sealed class PressSet<TAction>
    {
        private readonly List<(IPressable Control, TAction Action)> _controls = new List<(IPressable, TAction)>();

        /// <summary>The control the finger came down on and has not lifted from; nothing while no control is held.</summary>
        private (IPressable Control, TAction Action)? _held;

        /// <summary>Where the finger last was while it held a control.</summary>
        private Vector2 _fingerAt;

        /// <summary>True while the finger is down and came down on a control.</summary>
        public bool IsHolding => _held.HasValue;

        /// <summary>A control added later lies over the ones before it where they overlap: it is asked first.</summary>
        public void Add(IPressable control, TAction action) => _controls.Add((control, action));

        /// <summary>Forgets every control; a finger that held one holds nothing.</summary>
        public void Clear()
        {
            FingerGone();
            _controls.Clear();
        }

        public void FingerDown(Vector2 point)
        {
            FingerGone();
            for (int control = _controls.Count - 1; control >= 0; control--)
            {
                if (_controls[control].Control.IsUnder(point))
                {
                    _held = _controls[control];
                    _fingerAt = point;
                    _controls[control].Control.ShowPressed(true);
                    return;
                }
            }
        }

        public void FingerMovedTo(Vector2 point)
        {
            if (_held.HasValue)
            {
                IPressable control = _held.Value.Control;
                _fingerAt = point;
                control.ShowPressed(control.IsUnder(point));
            }
        }

        /// <summary>
        /// What the control the finger holds asks for, while the finger is still on it: a press that has lasted long
        /// enough is a long press, which its owner may take instead of the press (<see cref="FingerGone"/> then, so
        /// the lift presses nothing).
        /// </summary>
        /// <returns>False while no control is held, or the finger has slid off the one it holds.</returns>
        public bool TryGetHeld(out TAction action)
        {
            if (_held.HasValue && _held.Value.Control.IsUnder(_fingerAt))
            {
                action = _held.Value.Action;
                return true;
            }

            action = default!;
            return false;
        }

        /// <summary>The finger lifted.</summary>
        /// <returns>True when that pressed a control; <paramref name="action"/> is then what the press asks for.</returns>
        public bool FingerUp(out TAction action)
        {
            // The control answers anew: what it was when the finger came down or last moved may no longer be so.
            bool isPressed = _held.HasValue && _held.Value.Control.IsUnder(_fingerAt);
            action = _held.HasValue ? _held.Value.Action : default!;
            FingerGone();
            return isPressed;
        }

        /// <summary>The touch ended without a lift the game saw: nothing is pressed.</summary>
        public void FingerGone()
        {
            _held?.Control.ShowPressed(false);
            _held = null;
        }
    }
}
