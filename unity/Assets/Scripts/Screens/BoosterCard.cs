#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Client.Art;
using Puzzle.Client.Boards;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card a long press on a booster opens (#7, the owner's decision of 2026-10-19), over the board's shelf or in
    /// front of the card before a level: the booster's name on the nameplate, a tiny board in the game's own frame on
    /// which the booster's effect plays over and over (<see cref="BoosterShowcase"/>: the engine plays it, and the
    /// board's own pieces, frame, aim and effects show it, as a board's turn is shown), what the booster does, and a
    /// small line that a touch closes the card. No video, no picture made beforehand. The card has nothing to press:
    /// whoever hangs it closes it on a touch or the way back.
    /// </summary>
    /// <remarks>
    /// The tiny board is the game's board, frame and effects, which draw at the board's places in the drawing order,
    /// far under a card's. Each of its parts is drawn the same number of places higher than the board would draw
    /// it, also every piece and effect added while it plays (<see cref="Lift"/>), so all of it keeps its order within
    /// itself and lies over the card's cream; its mask cuts only what is lifted so, and the board under the card is
    /// not seen through it. (A sorting group would do the same, but the game's render pipeline does not honour one.)
    /// The design canvas draws no such card; it is built in the language of the cards it does draw and is the
    /// owner's to look at.
    /// </remarks>
    /// <typeparam name="TPress">What a press on the card asks for: nothing does, but the card hangs where its kind of card hangs.</typeparam>
    public sealed class BoosterCard<TPress> : IDisposable
    {
        /// <summary>The body; the nameplate as wide as the offer card's, with one line.</summary>
        private static readonly Vector2 Body = new Vector2(300f, 300f);
        private const float NameplateWidth = 212f;
        private const float TitleSize = 22f;

        /// <summary>The tiny board's cells, and how far its top edge lies under the body's.</summary>
        private const float CellSize = 34f;
        private const float BoardDown = 38f;

        /// <summary>The board's frame is drawn for this many pixels a point: about a phone's.</summary>
        private const float FramePixelsPerPoint = 3f;

        /// <summary>What the booster does, and the small line under it, under the board.</summary>
        private const float SentenceDown = 244f;
        private const float CloseLineDown = 270f;
        private const float SentenceSize = 15f;
        private const float CloseLineSize = 12f;
        private const float LinesInFromSides = 22f;

        /// <summary>The moves medallion of the start booster of extra moves, over the middle of the board, and its figure.</summary>
        private const float MedallionAcross = 84f;
        private const float MovesSize = 34f;

        /// <summary>The beats of the play: the board comes in, rests, is aimed at, waits between two acts, rests at the end.</summary>
        private const float RestAfterTheEntrance = 1f;
        private const float AimSeconds = 0.75f;
        private const float BetweenActs = 0.35f;
        private const float RestAtTheEnd = 1.3f;
        private const float MovesCountedApart = 0.32f;
        private const float MovesSwell = 0.25f;

        /// <summary>The tiny board's parts are drawn this many places over the card's body: the frame's shadow, its lowest, just over the card's art.</summary>
        private const int OverTheCard = (int)DrawOrder.CardArt - (int)DrawOrder.FrameShadow + 10;

        private readonly HangingCard<TPress> _card;
        private readonly BoosterShowcase _showcase;

        /// <summary>What every part of the tiny board is drawn higher by, and the place each part there is was last drawn at by the card.</summary>
        private readonly int _liftedBy;
        private Dictionary<Renderer, int> _lifted = new Dictionary<Renderer, int>();

        /// <summary>The parts found at the last lift, which become <see cref="_lifted"/>: kept to be filled anew at every step.</summary>
        private Dictionary<Renderer, int> _stillThere = new Dictionary<Renderer, int>();
        private readonly Transform _showcaseRoot;
        private readonly BoardFrame _frame;
        private readonly BoardView _board;
        private readonly BoardEffects _effects;
        private readonly TurnPlayer _turnPlayer;
        private readonly Timeline _timeline = new Timeline();
        private readonly TweenSet _tweens = new TweenSet();
        private readonly TextMeshPro? _moves;
        private Game _game;
        private PieceLedger _ledger;
        private int _actsDone;
        private Beat _beat;
        private float _beatUntil;
        private float _clock;

        /// <summary>The use aimed at while the play aims; nothing otherwise.</summary>
        private BoosterUse? _aimed;

        /// <param name="booster">Any of the seven boosters: used during play, or taken before a level.</param>
        /// <param name="extraMoves">The moves the start booster of extra moves adds, which its name and sentence say.</param>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> for a card in front of another card.</param>
        public BoosterCard(Transform parent, ArtCatalogue art, EffectArt effects, BoosterKind booster, int extraMoves, int raisedBy)
        {
            Booster = booster;
            _card = new HangingCard<TPress>(
                "Booster card", parent, art, Body, new CardNameplate(NameOf(booster, extraMoves), NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);

            _showcase = BoosterShowcase.Of(booster);
            _game = _showcase.Start();
            _ledger = PieceLedger.Of(_game.Board);

            _showcaseRoot = new GameObject("Showcase").transform;
            _showcaseRoot.SetParent(_card.Content, worldPositionStays: false);
            _liftedBy = OverTheCard + raisedBy;

            float across = BoosterShowcase.Cells * CellSize;
            var topLeft = new Vector2(-across / 2f, _card.At(BoardDown).y);
            _frame = BoardFrame.Create(_showcaseRoot, art, _game.Board.Shape);
            _frame.Place(topLeft, CellSize, FramePixelsPerPoint);
            _board = BoardView.Create(_showcaseRoot, art, _ledger, showExits: false);
            _board.Place(topLeft, CellSize);
            _effects = new BoardEffects(_board.Root, _game.Board.Shape, effects, art, _timeline);
            _turnPlayer = new TurnPlayer(_board, _effects, _timeline, () => { }, _ => { }, () => { });

            float room = Body.x - 2f * LinesInFromSides;
            TextMeshPro sentence = Lettering.Write("What it does", _card.Content, art, LetteringStyle.Label, SentenceSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(sentence, WhatItDoes(booster, extraMoves), SentenceSize, room);
            sentence.transform.localPosition = _card.At(SentenceDown);
            TextMeshPro close = Lettering.Write("Touch to close", _card.Content, art, LetteringStyle.Soft, CloseLineSize, Lettering.SoftInk, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(close, BoosterWords.TouchToClose, CloseLineSize, room);
            close.transform.localPosition = _card.At(CloseLineDown);

            if (booster == BoosterKind.StartExtraMoves)
            {
                Vector3 middle = _card.At(BoardDown + across / 2f);
                SpriteRenderer medallion = SceneObjects.AddSprite("Moves medallion", _card.Content, art.Of(GalleryArt.Medallion), art.SpriteMaterial, DrawOrder.Shelf);
                medallion.sortingOrder += _liftedBy; // over every part of the tiny board
                medallion.transform.localPosition = middle;
                float drawn = medallion.sprite.bounds.size.x;
                medallion.transform.localScale = Vector3.one * (MedallionAcross / drawn);
                _moves = Lettering.Write("Moves", _card.Content, art, LetteringStyle.Figure, MovesSize, Lettering.Ink, DrawOrder.Shelf + 1);
                _moves.sortingOrder += _liftedBy;
                _moves.transform.localPosition = middle - new Vector3(0f, Lettering.FigureDrop(LetteringStyle.Figure) * MovesSize, 0f);
            }

            Begin();
            Lift();
        }

        /// <summary>The beats of the play, over and over. A closed set.</summary>
        private enum Beat
        {
            /// <summary>The board comes in, as a level's does, and rests a moment.</summary>
            ComingIn,

            /// <summary>A booster that strikes is aimed: its cell, row or column is lit where a finger would aim it.</summary>
            Aiming,

            /// <summary>What the engine did is shown, as a turn is.</summary>
            Acting,

            /// <summary>A moment between two acts.</summary>
            Waiting,

            /// <summary>The booster has been shown: the board rests before it comes in again.</summary>
            Resting,
        }

        /// <summary>The booster the card shows.</summary>
        public BoosterKind Booster { get; }

        /// <summary>The card itself: where it hangs and how far it has come down.</summary>
        public HangingCard<TPress> Card => _card;

        /// <summary>How many times the booster's effect has been shown since the card was built: the walks wait for one.</summary>
        public int TimesShown { get; private set; }

        /// <summary>Moves the card's play on: the board comes in, the booster is aimed and used, the board rests, and it all comes again.</summary>
        public void Advance(float deltaSeconds)
        {
            _clock += deltaSeconds;
            _tweens.Advance(deltaSeconds);
            _timeline.Advance(deltaSeconds);
            switch (_beat)
            {
                case Beat.ComingIn:
                case Beat.Waiting:
                    if (_clock >= _beatUntil && _tweens.IsIdle && _timeline.IsDone)
                    {
                        TakeTheNextAct();
                    }

                    break;
                case Beat.Aiming:
                    _effects.ShowTheAim(_aimed, _clock);
                    if (_clock >= _beatUntil && _aimed is BoosterUse aimed)
                    {
                        Strike(aimed);
                    }

                    break;
                case Beat.Acting:
                    if (_timeline.IsDone)
                    {
                        _board.ClearAwayWhatWasTaken();
                        _effects.ClearAway();
                        Wait(Beat.Waiting, BetweenActs);
                    }

                    break;
                case Beat.Resting:
                    if (_clock >= _beatUntil)
                    {
                        Begin();
                    }

                    break;
                default:
                    throw new InvalidOperationException($"The booster card does not know the beat {_beat}.");
            }

            Lift();
        }

        /// <summary>
        /// Draws every part of the tiny board over the card: one added since (a piece that fell in, an effect) or one
        /// the board drew at another place since (a piece lifted while it moves) is drawn so many places higher; one
        /// the card lifted and nothing changed since is left as it is. The board's mask is moved with them. Only the
        /// parts there are now are remembered: what the board took away is forgotten.
        /// </summary>
        private void Lift()
        {
            _stillThere.Clear();
            foreach (Renderer part in _showcaseRoot.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                bool wasLifted = _lifted.TryGetValue(part, out int liftedTo);
                if (part is SpriteMask mask)
                {
                    if (!wasLifted)
                    {
                        mask.backSortingOrder += _liftedBy;
                        mask.frontSortingOrder += _liftedBy;
                    }
                }
                else if (!wasLifted || part.sortingOrder != liftedTo)
                {
                    part.sortingOrder += _liftedBy;
                }

                _stillThere[part] = part.sortingOrder;
            }

            (_lifted, _stillThere) = (_stillThere, _lifted);
        }

        public void Dispose()
        {
            _frame.Dispose();
            _board.Dispose();
            _card.Dispose();
        }

        private static string NameOf(BoosterKind booster, int extraMoves) =>
            booster.IsStart() ? LevelStartWords.NameOf(booster, extraMoves) : BoosterWords.NameOf(booster);

        private static string WhatItDoes(BoosterKind booster, int extraMoves) =>
            booster.IsStart() ? LevelStartWords.WhatItDoes(booster, extraMoves) : BoosterWords.OnTheCard(booster);

        /// <summary>The board as it opens, coming in as a level's board does, the start booster's specials with it.</summary>
        private void Begin()
        {
            _timeline.Clear();
            _effects.ClearAway();
            _effects.ShowTheAim(null, null);
            _game = _showcase.Start();
            _ledger = PieceLedger.Of(_game.Board);
            _board.ShowAnew(_ledger);
            BoardEntrance.Play(_board, _tweens, _effects, _game.LastEvents);
            _actsDone = 0;
            ShowTheMoves(_game.Level.MoveLimit, swell: 0f);
            Wait(Beat.ComingIn, RestAfterTheEntrance);
        }

        private void TakeTheNextAct()
        {
            ShowcaseAct? act = _showcase.NextAct(_game, _actsDone);
            _actsDone++;
            switch (act)
            {
                case null:
                    TimesShown++;
                    Wait(Beat.Resting, RestAtTheEnd);
                    break;
                case ShowcaseAct.UseTheBooster { Use: BoosterUse.Shuffle shuffle }:
                    Strike(shuffle);
                    break;
                case ShowcaseAct.UseTheBooster aimed:
                    _aimed = aimed.Use;
                    Wait(Beat.Aiming, AimSeconds);
                    break;
                case ShowcaseAct.Play play:
                    Show(_game.TryPlay(play.Move), () => TurnChoreographer.Compose(_ledger, _game.Level.Goals, _game.LastEvents, _game.Board));
                    break;
                case ShowcaseAct.CountTheMovesUp count:
                    CountUp(count);
                    break;
                default:
                    throw new InvalidOperationException($"The booster card does not know how to show \"{act}\".");
            }
        }

        /// <summary>The booster is used where it was aimed, and what the engine did is shown as the board shows a booster.</summary>
        private void Strike(BoosterUse use)
        {
            _aimed = null;
            _effects.ShowTheAim(null, null);
            Show(_game.TryUseBooster(use), () => TurnChoreographer.ComposeBooster(_ledger, _game.Level.Goals, _game.LastEvents, _game.Board));
        }

        /// <summary>
        /// Plays what the engine did as a turn is played. A tiny board whose act the engine refused, or whose turn cannot
        /// be shown, is a fault of the card's script: the board comes in anew rather than show something else.
        /// </summary>
        private void Show(bool isDone, Func<TurnScript> compose)
        {
            if (!isDone)
            {
                Debug.LogError($"The card of {Booster.Name()} asked the engine for something it refused; its board comes in anew.");
                Begin();
                return;
            }

            try
            {
                _turnPlayer.Play(compose());
                _beat = Beat.Acting;
            }
            catch (InvalidOperationException fault)
            {
                Debug.LogError($"The card of {Booster.Name()} could not show its board's turn: {fault.Message}; its board comes in anew.");
                Begin();
            }
        }

        /// <summary>The medallion counts the extra moves up one after another, each with a swell of its figure.</summary>
        private void CountUp(ShowcaseAct.CountTheMovesUp count)
        {
            for (int added = 1; added <= count.Moves; added++)
            {
                int shown = count.From + added;
                _timeline.Add(added * MovesCountedApart, MovesSwell, progress => ShowTheMoves(shown, swell: 1f - progress));
            }

            _beat = Beat.Acting;
        }

        private void ShowTheMoves(int moves, float swell)
        {
            if (_moves == null)
            {
                return;
            }

            Lettering.Say(_moves, moves.ToString(CultureInfo.InvariantCulture));
            _moves.transform.localScale = Vector3.one * (1f + 0.25f * Easing.Out(swell));
        }

        private void Wait(Beat beat, float seconds)
        {
            _beat = beat;
            _beatUntil = _clock + seconds;
        }
    }
}
