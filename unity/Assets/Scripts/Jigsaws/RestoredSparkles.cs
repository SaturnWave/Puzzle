#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using UnityEngine;

namespace Puzzle.Client.Jigsaws
{
    /// <summary>
    /// The sparkles around a painting that is whole again: they burst out around its frame one after another when
    /// the last piece is in, and go on twinkling for as long as the painting is looked at. They have no clock of
    /// their own: the stage says how long the painting has been whole and what moment its clock is at.
    /// </summary>
    public sealed class RestoredSparkles : IDisposable
    {
        /// <summary>
        /// Where each sparkle stands, in shares of the frame's width and height from its lower left corner (so
        /// less than 0 and more than 1 lie over or under the frame), and how large it is, as a share of its
        /// drawing. None stands beside the frame, which on a phone reaches the screen's sides, and none under its
        /// middle, where the brass plate hangs.
        /// </summary>
        private static readonly (float Across, float Up, float Size)[] Spots =
        {
            (0.075f, 1.07f, 1.3f), (0.925f, 1.06f, 1.45f), (0.2f, 1.11f, 0.9f), (0.8f, 1.12f, 1.05f), (0.11f, 0.87f, 0.8f),
            (0.9f, 0.2f, 0.9f), (0.075f, -0.06f, 1f), (0.925f, -0.07f, 1.2f), (0.71f, 0.9f, 0.6f), (0.3f, 0.12f, 0.55f),
        };

        /// <summary>A sparkle that has arrived swells and shrinks by this share of its size, this many times a second, and turns slowly.</summary>
        private const float TwinkleShare = 0.22f;
        private const float TwinklesPerSecond = 0.55f;
        private const float TurnsDegreesPerSecond = 14f;

        private readonly Transform _root;
        private readonly Transform[] _sparkles = new Transform[Spots.Length];

        private RestoredSparkles(Transform parent, ArtCatalogue art)
        {
            _root = new GameObject("Sparkles").transform;
            _root.SetParent(parent, worldPositionStays: false);
            for (int sparkle = 0; sparkle < Spots.Length; sparkle++)
            {
                _sparkles[sparkle] = SceneObjects.AddSprite($"Sparkle {sparkle}", _root, art.Of(GalleryArt.Sparkle), art.SpriteMaterial, DrawOrder.Celebration).transform;
            }

            Show(secondsWhole: 0f, clockSeconds: 0f);
        }

        /// <summary>How long the sparkles take to burst out, from the first one's beginning to the last one's end.</summary>
        public static float BurstSeconds => (Spots.Length - 1) * Tempo.SparkleEvery + Tempo.SparkleBursts;

        public static RestoredSparkles Create(Transform parent, ArtCatalogue art) => new RestoredSparkles(parent, art);

        /// <summary>Sets the sparkles around a frame; call again whenever the screen changes.</summary>
        public void Place(Box frame)
        {
            for (int sparkle = 0; sparkle < Spots.Length; sparkle++)
            {
                (float across, float up, _) = Spots[sparkle];
                _sparkles[sparkle].localPosition = new Vector3(frame.Left + across * frame.Width, frame.Bottom + up * frame.Height, 0f);
            }
        }

        /// <summary>Shows the sparkles as they are when the burst began a while ago; before it began, none is seen.</summary>
        /// <param name="secondsWhole">How long ago the burst began; nothing or less shows no sparkle.</param>
        /// <param name="clockSeconds">The moment of the stage's clock, which the twinkling follows.</param>
        public void Show(float secondsWhole, float clockSeconds)
        {
            for (int sparkle = 0; sparkle < Spots.Length; sparkle++)
            {
                float arrived = Mathf.Clamp01((secondsWhole - sparkle * Tempo.SparkleEvery) / Tempo.SparkleBursts);
                float twinkle = 1f + TwinkleShare * Mathf.Sin(2f * Mathf.PI * (TwinklesPerSecond * clockSeconds + sparkle * 0.37f));
                float size = arrived <= 0f ? 0f : Spots[sparkle].Size * Easing.Overshoot(arrived) * twinkle;
                _sparkles[sparkle].localScale = new Vector3(size, size, 1f);
                _sparkles[sparkle].localRotation = Quaternion.Euler(0f, 0f, TurnsDegreesPerSecond * clockSeconds + sparkle * 31f);
            }
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_root);
    }
}
