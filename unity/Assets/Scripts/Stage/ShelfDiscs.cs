#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Meta;
using UnityEngine;

namespace Puzzle.Client.Stage
{
    /// <summary>The pictures on the discs of the boosters used during play, as the design canvas draws the board's shelf (BoardTR).</summary>
    public static class PlayBoosterPictures
    {
        /// <exception cref="ArgumentException">The booster is taken before a level.</exception>
        public static BoosterPicture Of(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell => BoosterPicture.Of(GalleryArt.IconSpatula),
            BoosterKind.HitRow => BoosterPicture.Of(PieceArt.RocketHorizontal),
            BoosterKind.HitColumn => BoosterPicture.Of(PieceArt.RocketVertical),
            BoosterKind.Shuffle => BoosterPicture.Of(GalleryArt.IconShuffle),
            _ => throw new ArgumentException($"{booster.Name()} is taken before a level; the board's shelf shows none of it.", nameof(booster)),
        };
    }

    /// <summary>
    /// The four discs of the boosters used during play on the board's shelf, after the pause disc (BoardTR), and what
    /// they show: how many are held, or the plus where none is; the armed one in its green ring with the others set
    /// aside; one that is not open yet dimmed with the level it opens on (<see cref="ShelfBrief"/>); the one shown
    /// for the first time in a ring of light that breathes; a shake for the one whose use the engine refused. The
    /// stage decides what each shows; this only draws it.
    /// </summary>
    internal sealed class ShelfDiscs
    {
        /// <summary>A disc is drawn 60 points across; the shelf's places say how large it is on a screen.</summary>
        private const float DrawnSize = 60f;

        /// <summary>The ring of light around a disc shown for the first time: so many times the disc across, breathing between these strengths.</summary>
        private const float GuideAcross = 1.42f;
        private const float GuideBreathSeconds = 1.2f;
        private const float GuideFaintest = 0.45f;

        /// <summary>The ring of the light's picture is this part of the picture across (EffectArt.Ring).</summary>
        private const float RingShareOfItsPicture = 0.78f;

        /// <summary>A refused disc swings this far to each side, this many times, and comes to rest.</summary>
        private const float ShakeReach = 5f;
        private const int ShakeSwings = 3;

        private readonly ShelfBrief _brief;
        private readonly Dictionary<BoosterKind, BoosterDisc> _discs = new Dictionary<BoosterKind, BoosterDisc>();
        private readonly Dictionary<BoosterKind, Vector2> _places = new Dictionary<BoosterKind, Vector2>();
        private readonly SpriteRenderer _guide;
        private float _discScale = 1f;

        /// <summary>The disc that shakes, and when on the stage's clock its shake began; nothing while none shakes.</summary>
        private (BoosterKind Booster, float Began)? _shake;

        public ShelfDiscs(Transform parent, ArtCatalogue art, EffectArt effects, ShelfBrief brief)
        {
            _brief = brief;
            foreach (BoosterKind booster in ShelfBrief.Boosters)
            {
                _discs[booster] = new BoosterDisc(
                    booster.Name(), parent, art, PlayBoosterPictures.Of(booster), DrawnSize, DrawOrder.ShelfDisc, brief.FaceOf(booster, BoosterShelf.Empty, BoosterChoice.Waiting));
                _places[booster] = Vector2.zero;
            }

            _guide = SceneObjects.AddSprite("First use", parent, effects.Ring, art.SpriteMaterial, DrawOrder.ShelfGuide);
            _guide.enabled = false;
        }

        /// <summary>The booster that is shown for the first time, ringed with light; nothing for none.</summary>
        public BoosterKind? Guided { get; private set; }

        /// <summary>Every disc with its booster, from the shelf's left end: what a finger may press.</summary>
        public IEnumerable<KeyValuePair<BoosterKind, BoosterDisc>> All => _discs;

        public BoosterDisc this[BoosterKind booster] => _discs[booster];

        /// <summary>Puts each disc in its place on the shelf, as large as the place, and lets a finger press it from the layout's reach.</summary>
        public void Place(ScreenLayout layout)
        {
            for (int index = 0; index < ShelfBrief.Boosters.Count; index++)
            {
                BoosterKind booster = ShelfBrief.Boosters[index];
                Box box = layout.ShelfDiscs[1 + index];
                _discScale = box.Width / DrawnSize;
                _places[booster] = new Vector2(box.MiddleX, box.MiddleY);
                _discs[booster].Place(_places[booster], _discScale);
                _discs[booster].TouchFrom(layout.ShelfTouchReach);
            }
        }

        /// <summary>Shows each disc's face: what is held, which is armed, and whether the shelf listens at all; a booster not open yet is locked whatever is held.</summary>
        /// <param name="armed">The booster that is armed; nothing for none. Held: it is never one the player holds none of.</param>
        /// <param name="isListening">False while the shelf takes no finger: the game is decided, or a card hangs.</param>
        public void Show(BoosterShelf owned, BoosterKind? armed, bool isListening)
        {
            foreach (KeyValuePair<BoosterKind, BoosterDisc> each in _discs)
            {
                BoosterChoice choice = !isListening ? BoosterChoice.SetAside
                    : armed is BoosterKind armedOne ? (armedOne == each.Key && owned.Has(armedOne) ? BoosterChoice.Armed : BoosterChoice.SetAside)
                    : BoosterChoice.Waiting;
                BoosterFace face = _brief.FaceOf(each.Key, owned, choice);
                if (face != each.Value.Face)
                {
                    each.Value.Show(face);
                }
            }
        }

        /// <summary>Rings a booster's disc with breathing light, the one shown for the first time; nothing takes the ring away.</summary>
        public void Guide(BoosterKind? booster) => Guided = booster;

        /// <summary>The disc of a booster whose use the engine refused shakes, from this moment of the stage's clock.</summary>
        public void Shake(BoosterKind booster, float now) => _shake = (booster, now);

        /// <summary>Moves what moves on the shelf to a moment of the stage's clock: the ring of light, a shake.</summary>
        public void Advance(float now)
        {
            _guide.enabled = Guided != null;
            if (Guided is BoosterKind guided)
            {
                float breath = (Mathf.Sin(now * 2f * Mathf.PI / GuideBreathSeconds) + 1f) / 2f;
                _guide.transform.localPosition = _places[guided];
                float across = GuideAcross * DrawnSize * _discScale * Mathf.Lerp(0.96f, 1.04f, breath) / RingShareOfItsPicture;
                _guide.transform.localScale = new Vector3(across, across, 1f);
                _guide.color = new Color(1f, 0.93f, 0.69f, Mathf.Lerp(GuideFaintest, 1f, breath));
            }

            if (_shake.HasValue)
            {
                (BoosterKind shaken, float began) = _shake.Value;
                float progress = Mathf.Clamp01((now - began) / Tempo.DiscShakes);
                float swing = Mathf.Sin(progress * ShakeSwings * 2f * Mathf.PI) * (1f - progress) * ShakeReach * _discScale;
                _discs[shaken].Place(_places[shaken] + new Vector2(swing, 0f), _discScale);
                if (progress >= 1f)
                {
                    _shake = null;
                }
            }
        }

        /// <summary>The middle of a booster's disc, in the layout's points.</summary>
        public Vector2 MiddleOf(BoosterKind booster) => _places[booster];
    }
}
