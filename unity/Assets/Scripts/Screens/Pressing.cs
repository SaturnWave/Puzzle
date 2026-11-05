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
    /// A control is pressed by a tap: the finger comes down on it and lifts on it without having gone further than
    /// the slop on the way (<see cref="TapSlop"/>). A finger that slides off lets the control go, and one that slides
    /// back within the slop holds it down again, as buttons behave everywhere; a finger that goes further than the
    /// slop drags, and presses nothing however it lifts, so a sideways swipe across a card's row presses no row.
    /// One finger, one control at a time. A control that stops being pressable while the finger holds it (it is
    /// dimmed, set aside or taken out of sight) is not pressed by the lift: it is asked once more as the finger lifts.
    /// </summary>
    /// <typeparam name="TAction">What a press asks for.</typeparam>
    public sealed class PressSet<TAction>
    {
        private readonly List<(IPressable Control, TAction Action)> _controls = new List<(IPressable, TAction)>();

        /// <summary>The control the finger came down on and has not lifted from; nothing while no control is held.</summary>
        private (IPressable Control, TAction Action)? _held;

        /// <summary>Where the finger last was while it held a control.</summary>
        private Vector2 _fingerAt;

        /// <summary>Whether the finger that holds a control still taps.</summary>
        private TapSlop _touch;

        /// <summary>True while the finger is down and came down on a control, even one it has since dragged too far to press.</summary>
        public bool IsHolding => _held.HasValue;

        /// <summary>True while the finger would press the control it holds by lifting now: it still taps, and is on the control.</summary>
        private bool IsPressing => _held.HasValue && _touch.IsATap && _held.Value.Control.IsUnder(_fingerAt);

        /// <summary>A control added later lies over the ones before it where they overlap: it is asked first.</summary>
        public void Add(IPressable control, TAction action) => _controls.Add((control, action));

        /// <summary>Forgets every control; a finger that held one holds nothing.</summary>
        public void Clear()
        {
            FingerGone();
            _controls.Clear();
        }

        /// <param name="slop">How far the finger may go and still press, in the points its places are told in: the layout's slop unless the controls stand in points of their own.</param>
        public void FingerDown(Vector2 point, float slop = TapSlop.Points)
        {
            FingerGone();
            for (int control = _controls.Count - 1; control >= 0; control--)
            {
                if (_controls[control].Control.IsUnder(point))
                {
                    _held = _controls[control];
                    _fingerAt = point;
                    _touch = TapSlop.Down(point.x, point.y, slop);
                    _controls[control].Control.ShowPressed(true);
                    return;
                }
            }
        }

        public void FingerMovedTo(Vector2 point)
        {
            if (_held.HasValue)
            {
                _fingerAt = point;
                _touch = _touch.MovedTo(point.x, point.y);
                _held.Value.Control.ShowPressed(IsPressing);
            }
        }

        /// <summary>
        /// What the control the finger holds asks for, while the finger still taps on it: a press that has lasted long
        /// enough is a long press, which its owner may take instead of the press (<see cref="FingerGone"/> then, so
        /// the lift presses nothing).
        /// </summary>
        /// <returns>False while no control is held, or the finger has slid off the one it holds or dragged.</returns>
        public bool TryGetHeld(out TAction action)
        {
            if (IsPressing)
            {
                action = _held!.Value.Action;
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
            bool isPressed = IsPressing;
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
