#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The small cream card that stands over the board's shelf and says what the shelf has to say (<see cref="PillWords"/>):
    /// how to play on a first try, which try this is, the booster that is armed and what to pick for it, why a use
    /// was refused. The design canvas draws none of it; it is made of the canvas'
    /// card (its cream, its gilt rim, its soft shadow) drawn small, with the lettering of its labels. It is centred
    /// on the screen just over the plank, as wide as its words, and drawn with the shelf's size, but its words are
    /// never smaller on the screen than can be read. The tutorial's steps are said by a pill of the same make over the
    /// dimmed stage (<see cref="CreateForLessons"/>): in three lines, what to look at or do in the larger type, why
    /// under it, and a small line (a touch goes on, a booster used by hand is free); it stands over the shelf, under
    /// the plaques, over a jigsaw's tray, or under the card before a level (<see cref="Stand"/>).
    /// </summary>
    public sealed class ShelfPill : IDisposable
    {
        /// <summary>The canvas' card is drawn at this share of its size: its rim and corners are then those of a label.</summary>
        private const float CardScale = 0.4f;

        /// <summary>The card's picture is its body and 60 points around it for its rim and glow (HangingCard).</summary>
        private const float CardPictureMargin = 60f;

        /// <summary>The words keep this far from the card's sides, and the card is at least this wide.</summary>
        private const float WordsInFromSides = 14f;
        private const float NarrowestBody = 120f;

        /// <summary>The card's body for one line, for two and for three, and how far two lines stand from each other.</summary>
        private const float OneLineHigh = 30f;
        private const float TwoLinesHigh = 44f;
        private const float ThreeLinesHigh = 60f;
        private const float LinesApart = 15.5f;

        /// <summary>The type: a single line and the first of two at 12.5; what to pick at 14, the larger; the honest aside at 11.</summary>
        private const float LineSize = 12.5f;
        private const float PickSize = 14f;
        private const float AsideSize = 11f;

        /// <summary>No word is set smaller than this on the screen, however small the shelf is drawn (the shelf's own rule).</summary>
        private const float SmallestOnScreen = 9f;

        /// <summary>The card stands this far over the plank's top edge.</summary>
        private const float OverThePlank = 5f;

        /// <summary>While a finger aims on the board the pill is drawn this faint: where it stands over the lowest cells, they show through it.</summary>
        private const float FaintWhileAiming = 0.3f;

        private readonly Transform _root;
        private readonly SpriteRenderer _card;
        private readonly TextMeshPro _first;
        private readonly TextMeshPro _second;
        private readonly TextMeshPro _aside;

        /// <summary>The second half of a step's one sentence where it is too wide for one line of the large type.</summary>
        private readonly TextMeshPro _secondHalf;
        private PillWords _words = PillWords.None;

        /// <summary>How many lines the pill says now: its body is as high as they need.</summary>
        private int _lines = 1;
        private bool _isFaint;
        private float _scale = 1f;
        private float _room = ScreenLayout.WidestShelf;

        /// <summary>The line the pill stands on (its foot) or hangs from (its head), in the layout's points.</summary>
        private float _edgeAt;
        private PillEdge _edge = PillEdge.Foot;

        /// <summary>The middle of the pill's width, in the layout's points: the screen's middle but where a page stands it in a column of its own.</summary>
        private float _across;

        private ShelfPill(Transform parent, ArtCatalogue art, string name, DrawOrder card, DrawOrder lettering)
        {
            _root = new GameObject(name).transform;
            _root.SetParent(parent, worldPositionStays: false);
            _card = SceneObjects.AddSprite("Card", _root, art.Of(GalleryArt.Card), art.SpriteMaterial, card);
            _card.drawMode = SpriteDrawMode.Sliced; // the corners keep their shape at every size
            _card.transform.localScale = new Vector3(CardScale, CardScale, 1f);
            _first = Lettering.Write("Line", _root, art, LetteringStyle.Label, LineSize, Lettering.Ink, lettering);
            _second = Lettering.Write("What to pick", _root, art, LetteringStyle.Figure, PickSize, Lettering.Ink, lettering);
            _aside = Lettering.Write("Aside", _root, art, LetteringStyle.Soft, AsideSize, Lettering.SoftInk, lettering);
            _secondHalf = Lettering.Write("Second half", _root, art, LetteringStyle.Figure, PickSize, Lettering.Ink, lettering);
            Show(PillWords.None);
        }

        /// <summary>Which edge of the pill keeps to the line it is stood on.</summary>
        public enum PillEdge
        {
            /// <summary>The pill stands on the line: a pill of more lines grows upwards.</summary>
            Foot,

            /// <summary>The pill hangs from the line: a pill of more lines grows downwards.</summary>
            Head,

            /// <summary>The pill's middle lies on the line: a pill of more lines grows both ways.</summary>
            Middle,
        }

        /// <summary>What the pill says now; <see cref="PillWords.None"/> while it is out of sight.</summary>
        public PillWords Words => _words;

        public static ShelfPill Create(Transform parent, ArtCatalogue art) => new ShelfPill(parent, art, "Shelf pill", DrawOrder.ShelfPill, DrawOrder.ShelfPillLettering);

        /// <summary>The pill that says the tutorial's steps, over the dark of its spotlight.</summary>
        public static ShelfPill CreateForLessons(Transform parent, ArtCatalogue art) => new ShelfPill(parent, art, "Lesson pill", DrawOrder.LessonPill, DrawOrder.LessonPillLettering);

        /// <summary>The pill that says a step of the tutorial over a card that hangs in front of the dark (the card before a level), over the card's own spotlight.</summary>
        public static ShelfPill CreateForLessonsOnACard(Transform parent, ArtCatalogue art) =>
            new ShelfPill(parent, art, "Lesson pill on a card", DrawOrder.CardLessonPill, DrawOrder.CardLessonPillLettering);

        /// <summary>Where the pill's top edge lies now, in the layout's points.</summary>
        public float Top => _root.localPosition.y + BodyHeight / 2f * _scale;

        /// <summary>Puts the pill over the shelf as the layout has it, at the shelf's size, and lays its words out for it.</summary>
        public void Place(ScreenLayout layout) => StandOverTheShelf(layout, layout.ShelfScale);

        /// <summary>Puts the pill over the shelf as the layout has it, drawn at a size of its own, as wide as the shelf.</summary>
        public void StandOverTheShelf(ScreenLayout layout, float scale) => Stand(layout.ShelfTop + OverThePlank * scale, PillEdge.Foot, scale, layout.ShelfWidth);

        /// <summary>
        /// Puts the pill centred on the screen, or on a column of it, with one edge on a line, drawn at a size, its
        /// words as wide as the room allows, and lays its words out for it.
        /// </summary>
        /// <param name="at">The line, in the layout's points.</param>
        /// <param name="widest">How wide the pill may be at most, in the layout's points.</param>
        /// <param name="across">The middle of the pill's width: the screen's middle unless a page has a column for it (the jigsaw held sideways).</param>
        public void Stand(float at, PillEdge edge, float scale, float widest, float across = 0f)
        {
            _across = across;
            _scale = scale;
            _room = widest / _scale - 2f * WordsInFromSides;
            _edgeAt = at;
            _edge = edge;
            _root.localScale = new Vector3(_scale, _scale, 1f);
            Show(_words);
        }

        /// <summary>Changes what the pill says; <see cref="PillWords.None"/> takes it out of sight.</summary>
        public void Show(PillWords words)
        {
            _words = words;
            bool isShown = !(words is PillWords.Silent);
            _root.gameObject.SetActive(isShown);
            if (!isShown)
            {
                return;
            }

            bool isSplit = words is PillWords.Lesson { Why: null } alone && !FitsOnOneLine(alone.Says);
            _lines = words switch
            {
                PillWords.Line _ => 1,
                PillWords.Armed _ => 2,
                PillWords.Lesson lesson => 1 + (lesson.Why != null || isSplit ? 1 : 0) + (lesson.Aside != null ? 1 : 0),
                _ => throw new InvalidOperationException($"The pill does not know how to say {words.GetType().Name}."),
            };
            float widest = words switch
            {
                PillWords.Line line => Write(_first, line.Words, LineSize, 0f),
                PillWords.Armed armed => Mathf.Max(Write(_first, armed.About, LineSize, LinesApart / 2f), Write(_second, armed.WhatToPick, PickSize, -LinesApart / 2f)),
                PillWords.Lesson { Why: string why } lesson => Mathf.Max(
                    Write(_second, lesson.Says, PickSize, LineAbove(0)), Mathf.Max(Write(_first, why, LineSize, LineAbove(1)), WriteTheAside(lesson.Aside))),
                PillWords.Lesson lesson when isSplit => SaySplit(lesson),
                PillWords.Lesson lesson => Mathf.Max(Write(_second, lesson.Says, PickSize, LineAbove(0)), WriteTheAside(lesson.Aside)),
                _ => throw new InvalidOperationException($"The pill does not know how to say {words.GetType().Name}."),
            };
            _first.gameObject.SetActive(!(words is PillWords.Lesson { Why: null }));
            _second.gameObject.SetActive(words is PillWords.Armed || words is PillWords.Lesson);
            _secondHalf.gameObject.SetActive(isSplit);
            _aside.gameObject.SetActive(words is PillWords.Lesson { Aside: string _ });

            var body = new Vector2(Mathf.Max(NarrowestBody, widest + 2f * WordsInFromSides), BodyHeight);
            _card.size = body / CardScale + new Vector2(CardPictureMargin, CardPictureMargin);

            // A pill of more lines is higher: the edge it keeps to stays where it was (over the shelf, its foot just over the plank).
            float toTheMiddle = BodyHeight / 2f * _scale;
            float middle = _edge switch
            {
                PillEdge.Foot => _edgeAt + toTheMiddle,
                PillEdge.Head => _edgeAt - toTheMiddle,
                _ => _edgeAt,
            };
            _root.localPosition = new Vector3(_across, middle, 0f);
        }

        /// <summary>
        /// Draws the pill faint while a finger aims on the board, and whole again after: on a screen with little wall
        /// under the board the pill stands over the lowest cells, and the cell that is aimed at may be one of them.
        /// </summary>
        public void LetTheBoardShowThrough(bool isFaint)
        {
            if (_isFaint == isFaint)
            {
                return;
            }

            _isFaint = isFaint;
            float strength = isFaint ? FaintWhileAiming : 1f;
            _card.color = new Color(1f, 1f, 1f, strength);
            Lettering.DrawIn(_first, WithStrength(Lettering.Ink, strength));
            Lettering.DrawIn(_second, WithStrength(Lettering.Ink, strength));
            Lettering.DrawIn(_aside, WithStrength(Lettering.SoftInk, strength));
        }

        public void Dispose() => SceneObjects.DestroyObjectOf(_root);

        private static Color WithStrength(Color colour, float strength) => new Color(colour.r, colour.g, colour.b, colour.a * strength);

        private float BodyHeight => _lines switch
        {
            1 => OneLineHigh,
            2 => TwoLinesHigh,
            _ => ThreeLinesHigh,
        };

        /// <summary>How far over the pill's middle its line of this place (0 the first) stands, its lines <see cref="LinesApart"/> apart.</summary>
        private float LineAbove(int place) => (_lines - 1) * LinesApart / 2f - place * LinesApart;

        /// <summary>The small last line of a step, where it has one; how wide it is.</summary>
        private float WriteTheAside(string? aside) => aside is string words ? Write(_aside, words, AsideSize, LineAbove(_lines - 1)) : 0f;

        /// <summary>True when a step's one sentence fits on one line of the large type, at its size.</summary>
        private bool FitsOnOneLine(string says)
        {
            _second.fontSize = Mathf.Max(PickSize, SmallestOnScreen / _scale);
            return Lettering.WidthOf(_second, says) <= _room;
        }

        /// <summary>A step's one sentence on two lines of the large type, broken where the longer is the shortest, and its small last line.</summary>
        private float SaySplit(PillWords.Lesson lesson)
        {
            (string first, string second) = BalancedLines.SplitInTwo(lesson.Says, part => Lettering.WidthOf(_second, part));
            return Mathf.Max(
                Mathf.Max(Write(_second, first, PickSize, LineAbove(0)), Write(_secondHalf, second, PickSize, LineAbove(1))),
                WriteTheAside(lesson.Aside));
        }

        /// <summary>Sets a line at its size, or smaller where it would not fit the shelf, never under the smallest size on the screen; returns how wide it is.</summary>
        private float Write(TextMeshPro line, string words, float size, float aboveMiddle)
        {
            float atLeast = SmallestOnScreen / _scale;
            Lettering.SayWithin(line, words, Mathf.Max(size, atLeast), _room);
            line.transform.localPosition = new Vector3(0f, aboveMiddle, 0f);
            return Mathf.Min(_room, Lettering.WidthOf(line, words));
        }
    }
}
