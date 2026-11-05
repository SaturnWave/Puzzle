#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Gallery;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The name of a part of a card or of a plaque, as the design canvas sets it over the goals and the boosters
    /// of a level (LevelStartTR, "HEDEFLER") and over the shop's shelves (ShopTR, "ALTIN"): small capitals between
    /// two gilt rules, each of which ends in a diamond beside the words. Sizes are in points around the middle of
    /// the words; the header is as wide as its words make it.
    /// </summary>
    public sealed class SectionHeader : IDisposable
    {
        /// <summary>The words, as the design sets them: 10 points, their capitals apart by this many hundredths of their size.</summary>
        private const float LabelSize = 10f;
        private const float LabelSpacing = 9f;

        /// <summary>
        /// From an end of the words outwards (LevelStartTR): the middle of the diamond, where the line begins and
        /// where it ends. The canvas' diamond is 8 points across in a dark line of 0.9: its lit face and its edge
        /// as that leaves them.
        /// </summary>
        private const float DiamondFromTheWords = 15f;
        private const float LineBeginsFromTheWords = 22f;
        private const float LineEndsFromTheWords = 54f;
        private const float DiamondAcross = 6.8f;
        private const float DiamondEdge = 1.25f;

        private readonly PaintedShape _rules;
        private readonly TextMeshPro _label;

        /// <param name="label">The words, in the capitals they are to be read in.</param>
        public SectionHeader(string name, Transform parent, ArtCatalogue art, string label, DrawOrder ruleOrder, DrawOrder letteringOrder)
        {
            Transform = new GameObject(name).transform;
            Transform.SetParent(parent, worldPositionStays: false);
            _rules = PaintedShape.Create("Rules", Transform, art.PaintMaterial, ruleOrder);
            _label = Lettering.Write("Words", Transform, art, LetteringStyle.Label, LabelSize, Lettering.LabelInk, letteringOrder);
            _label.characterSpacing = LabelSpacing;
            Say(label);
        }

        /// <summary>The header's place is the middle of its words.</summary>
        public Transform Transform { get; }

        /// <summary>From the outer end of one rule to the outer end of the other.</summary>
        public float Width { get; private set; }

        /// <summary>Changes the words; the rules move out or in with them.</summary>
        public void Say(string label)
        {
            Lettering.Say(_label, label);
            float halfWords = Lettering.WidthOf(_label, label) / 2f;
            Width = 2f * (halfWords + LineEndsFromTheWords);

            var sketch = new MeshSketch();
            foreach (float side in new[] { -1f, 1f })
            {
                float lineBegins = side * (halfWords + LineBeginsFromTheWords);
                float lineEnds = side * (halfWords + LineEndsFromTheWords);
                GiltRule.AddLine(sketch, Mathf.Min(lineBegins, lineEnds), Mathf.Max(lineBegins, lineEnds), height: 0f, isShaded: true);
                GiltRule.AddDiamond(sketch, new Vector2(side * (halfWords + DiamondFromTheWords), 0f), DiamondAcross, DiamondEdge);
            }

            _rules.Show(sketch.ToMesh("Section header rules"));
        }

        public void Dispose()
        {
            _rules.Dispose();
            SceneObjects.DestroyObjectOf(Transform);
        }
    }
}
