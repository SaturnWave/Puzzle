#nullable enable
using System;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// A control of the world map a finger presses: a round button, a pin, an era's medallion, a card's small button.
    /// It is a real button: it says what it does in the language spoken (<see cref="Label"/>, what a screen reader
    /// would read; the icon-only ones have words nowhere else), and a finger may press it anywhere in a box at least
    /// <see cref="LeastTarget"/> points square around its middle, however small it is drawn. Held, what it draws
    /// goes a little smaller.
    /// </summary>
    public sealed class MapButton : IPressable
    {
        /// <summary>The least box a finger may press a control in, in points (the platforms' 44 pt).</summary>
        public const float LeastTarget = 44f;

        private const float PressedScale = 0.94f;

        private readonly Func<Vector2, bool>? _isCovered;
        private Transform? _drawing;
        private Vector3 _restingScale = Vector3.one;
        private Box _target;

        /// <param name="drawing">What the button draws: it is scaled down while the button is held; nothing until it is attached to something drawn (<see cref="Attach"/>).</param>
        /// <param name="isCovered">True where something lies over the button (the card over a pin): a finger there does not press it.</param>
        public MapButton(string label, Transform? drawing, Func<Vector2, bool>? isCovered = null)
        {
            Label = label;
            _drawing = drawing;
            _isCovered = isCovered;
        }

        /// <summary>What the button does, said in words: its name for a screen reader.</summary>
        public string Label { get; private set; }

        /// <summary>Where a finger presses it, in the page's points.</summary>
        public Box Target => _target;

        /// <summary>False while the button is out of sight or leads nowhere: no finger presses it.</summary>
        public bool IsShown { get; set; } = true;

        /// <summary>Puts the button's target around a middle, as large as it is drawn but never smaller than <see cref="LeastTarget"/>.</summary>
        public void Aim(Vector2 middle, float width, float height)
        {
            _target = Box.Around(middle.x, middle.y, Math.Max(width, LeastTarget), Math.Max(height, LeastTarget));
            _restingScale = _drawing != null ? _drawing.localScale : Vector3.one;
        }

        /// <summary>The button stands for something drawn anew (an era's pin after the camera came to rest): what it draws and what it says.</summary>
        public void Attach(Transform drawing, string label)
        {
            _drawing = drawing;
            Label = label;
        }

        public bool IsUnder(Vector2 point) =>
            IsShown && _target.Contains(point.x, point.y) && !(_isCovered?.Invoke(point) ?? false);

        public void ShowPressed(bool isPressed)
        {
            if (_drawing != null)
            {
                _drawing.localScale = isPressed ? _restingScale * PressedScale : _restingScale;
            }
        }
    }
}
