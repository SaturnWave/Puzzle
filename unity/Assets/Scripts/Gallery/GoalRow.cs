#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The goals on their plaque: each as its icon with what is left of it, or with a green seal once it is met. One
    /// goal alone stands beside its number, as large as the plaque allows; two or three stand side by side, each
    /// over its number. The sizes are the web test page's (gallery.css, .goals). The row is laid out around its own
    /// place, which is the middle of the room the plaque gives it.
    /// </summary>
    public sealed class GoalRow : IDisposable
    {
        /// <summary>A number under its icon is tucked up this far: the lowest tenth of an icon is only its shadow.</summary>
        private const float NumberTuckedUnderIcon = 4f;

        /// <summary>A seal is drawn 20 points across at scale 1, an icon 1 point.</summary>
        private const float SealDrawnSize = 20f;

        /// <summary>From this number on, a count has three digits and is set smaller.</summary>
        private const int FirstLongNumber = 100;

        private static readonly Measures Alone = new Measures(iconSize: 38f, numberSize: 31f, longNumberSize: 25f, gap: 5f, gapWithLongNumbers: 5f, sealSize: 26f);
        private static readonly Measures Paired = new Measures(iconSize: 30f, numberSize: 24f, longNumberSize: 20f, gap: 12f, gapWithLongNumbers: 6f, sealSize: 20f);
        private static readonly Measures Three = new Measures(iconSize: 27f, numberSize: 19f, longNumberSize: 15f, gap: 5f, gapWithLongNumbers: 5f, sealSize: 18f);

        private readonly ArtCatalogue _art;
        private readonly Transform _root;
        private readonly List<Entry> _entries = new List<Entry>();

        public GoalRow(Transform parent, ArtCatalogue art)
        {
            _art = art;
            _root = new GameObject("Goals").transform;
            _root.SetParent(parent, worldPositionStays: false);
        }

        public Transform Transform => _root;

        /// <summary>How tall the row is with the goals last shown, in points: the plaque centres its label and the row together.</summary>
        public float Height { get; private set; }

        public void Show(IReadOnlyList<GoalFigure> goals)
        {
            MatchEntriesTo(goals.Count);
            if (goals.Count == 0)
            {
                Height = 0f;
                return;
            }

            Measures measures = goals.Count switch { 1 => Alone, 2 => Paired, _ => Three };
            bool hasLongNumber = false;
            foreach (GoalFigure goal in goals)
            {
                hasLongNumber |= goal.Remaining >= FirstLongNumber;
            }

            float numberSize = hasLongNumber ? measures.LongNumberSize : measures.NumberSize;
            for (int goal = 0; goal < goals.Count; goal++)
            {
                _entries[goal].Show(goals[goal], _art, measures, numberSize);
            }

            if (goals.Count == 1)
            {
                Height = measures.IconSize;
                _entries[0].PlaceBesideItsNumber(measures);
            }
            else
            {
                Height = measures.IconSize + numberSize - NumberTuckedUnderIcon;
                PlaceSideBySide(hasLongNumber ? measures.GapWithLongNumbers : measures.Gap, measures, numberSize);
            }
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_root);

        private void MatchEntriesTo(int count)
        {
            while (_entries.Count > count)
            {
                _entries[_entries.Count - 1].Remove();
                _entries.RemoveAt(_entries.Count - 1);
            }

            while (_entries.Count < count)
            {
                _entries.Add(new Entry(_root, _art));
            }
        }

        /// <summary>Each goal is a column as wide as its icon or its number, whichever is wider; the columns are centred as a row.</summary>
        private void PlaceSideBySide(float gap, Measures measures, float numberSize)
        {
            float rowWidth = gap * (_entries.Count - 1);
            foreach (Entry entry in _entries)
            {
                rowWidth += entry.ColumnWidth(measures);
            }

            float left = -rowWidth / 2f;
            foreach (Entry entry in _entries)
            {
                float width = entry.ColumnWidth(measures);
                entry.PlaceOverItsNumber(left + width / 2f, rowHeight: Height, measures, numberSize);
                left += width + gap;
            }
        }

        /// <summary>The sizes of a row with a given number of goals, in points.</summary>
        private readonly struct Measures
        {
            public Measures(float iconSize, float numberSize, float longNumberSize, float gap, float gapWithLongNumbers, float sealSize)
            {
                IconSize = iconSize;
                NumberSize = numberSize;
                LongNumberSize = longNumberSize;
                Gap = gap;
                GapWithLongNumbers = gapWithLongNumbers;
                SealSize = sealSize;
            }

            public float IconSize { get; }

            public float NumberSize { get; }

            /// <summary>The size of the numbers when one of them has three digits.</summary>
            public float LongNumberSize { get; }

            /// <summary>Between a lone icon and its number, or between two goals.</summary>
            public float Gap { get; }

            public float GapWithLongNumbers { get; }

            public float SealSize { get; }
        }

        /// <summary>One goal of the row: its icon, its number, and the seal that takes the number's place once the goal is met.</summary>
        private sealed class Entry
        {
            private readonly SpriteRenderer _icon;
            private readonly SpriteRenderer _seal;
            private readonly TextMeshPro _number;
            private float _markWidth;

            public Entry(Transform row, ArtCatalogue art)
            {
                _icon = AddSprite("Icon", row, art);
                _seal = AddSprite("Seal", row, art);
                _seal.sprite = art.Of(GalleryArt.GoalDone);
                _number = Lettering.Write("Left", row, art, LetteringStyle.Figure, size: 1f, Lettering.Ink, DrawOrder.PlaqueLettering);
            }

            /// <summary>The number or the seal, whichever shows.</summary>
            private Transform Mark => _seal.enabled ? _seal.transform : _number.transform;

            /// <summary>A number is set a little low, as its typeface sets it in a line; a seal is not.</summary>
            private float MarkDrop => _seal.enabled ? 0f : Hud.FigureDrop * _number.fontSize;

            public void Show(GoalFigure goal, ArtCatalogue art, Measures measures, float numberSize)
            {
                bool isMet = goal.Remaining == 0;
                _icon.sprite = art.Of(goal.Icon);
                _icon.transform.localScale = new Vector3(measures.IconSize, measures.IconSize, 1f);
                _seal.enabled = isMet;
                _seal.transform.localScale = Vector3.one * (measures.SealSize / SealDrawnSize);
                _number.enabled = !isMet;
                _number.fontSize = numberSize;
                string left = goal.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Lettering.Say(_number, isMet ? string.Empty : left);
                _markWidth = isMet ? measures.SealSize : Lettering.WidthOf(_number, left);
            }

            public float ColumnWidth(Measures measures) => Mathf.Max(measures.IconSize, _markWidth);

            public void PlaceBesideItsNumber(Measures measures)
            {
                float left = -(measures.IconSize + measures.Gap + _markWidth) / 2f;
                _icon.transform.localPosition = new Vector3(left + measures.IconSize / 2f, 0f, 0f);
                Mark.localPosition = new Vector3(left + measures.IconSize + measures.Gap + _markWidth / 2f, -MarkDrop, 0f);
            }

            public void PlaceOverItsNumber(float middle, float rowHeight, Measures measures, float numberSize)
            {
                _icon.transform.localPosition = new Vector3(middle, rowHeight / 2f - measures.IconSize / 2f, 0f);
                Mark.localPosition = new Vector3(middle, -rowHeight / 2f + numberSize / 2f - MarkDrop, 0f);
            }

            public void Remove()
            {
                SceneObjects.DestroyObjectOf(_icon);
                SceneObjects.DestroyObjectOf(_seal);
                SceneObjects.DestroyObjectOf(_number);
            }

            private static SpriteRenderer AddSprite(string name, Transform row, ArtCatalogue art)
            {
                var sprite = new GameObject(name).AddComponent<SpriteRenderer>();
                sprite.transform.SetParent(row, worldPositionStays: false);
                sprite.sharedMaterial = art.SpriteMaterial;
                sprite.sortingOrder = (int)DrawOrder.PlaqueArt;
                return sprite;
            }
        }
    }
}
