#nullable enable
using System.Collections.Generic;
using Puzzle.Client.Gallery;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// Things drawn together that fade together, each from its own strength: the world map's pins, dots and labels
    /// fade out while the camera moves and in once it has come to rest, as the design's overlay does.
    /// </summary>
    public sealed class Fading
    {
        private readonly List<(SpriteRenderer Sprite, float Alpha)> _sprites = new List<(SpriteRenderer, float)>();
        private readonly List<(TextMeshPro Line, float Alpha)> _lines = new List<(TextMeshPro, float)>();
        private readonly List<(PaintedShape Shape, float Strength)> _shapes = new List<(PaintedShape, float)>();

        public SpriteRenderer Add(SpriteRenderer sprite)
        {
            _sprites.Add((sprite, sprite.color.a));
            return sprite;
        }

        public TextMeshPro Add(TextMeshPro line)
        {
            _lines.Add((line, line.color.a));
            return line;
        }

        /// <param name="strength">How strongly the shape is drawn when the group is drawn in full.</param>
        public PaintedShape Add(PaintedShape shape, float strength = 1f)
        {
            _shapes.Add((shape, strength));
            return shape;
        }

        public void Clear()
        {
            _sprites.Clear();
            _lines.Clear();
            _shapes.Clear();
        }

        /// <summary>Draws everything at a share of its own strength, from 0 (not seen) to 1.</summary>
        public void Show(float share)
        {
            foreach ((SpriteRenderer sprite, float alpha) in _sprites)
            {
                if (sprite != null)
                {
                    Color colour = sprite.color;
                    colour.a = alpha * share;
                    sprite.color = colour;
                }
            }

            foreach ((TextMeshPro line, float alpha) in _lines)
            {
                if (line != null)
                {
                    Color colour = line.color;
                    colour.a = alpha * share;
                    Lettering.DrawIn(line, colour);
                }
            }

            foreach ((PaintedShape shape, float strength) in _shapes)
            {
                shape.Fade(strength * share);
            }
        }
    }
}
