#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// Turns the events of one move into a script for the view: every event gets the moment it is seen. The engine
    /// reports what happened in the order it worked it out, all at once; here that becomes a trade, then bursts that
    /// follow a rocket's stroke outwards, then columns that start to fall as soon as the cell under them is free.
    /// The ledger is brought forward with every event, so at the end it says what the engine's board says.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    /// <remarks>
    /// The rules of timing, in short. A turn is hits and falls in turns. Hits of one match come together; hits of
    /// an effect come as the effect reaches their cell (<see cref="Strike"/>); a special piece that is hit goes off
    /// when it is hit, not when the engine got round to it. A piece falls when every cell on its way is free and it
    /// has finished what it did before; a cell is free a moment after its piece was hit or let go. The next round
    /// of matches waits until everything has landed. A landed piece gives where it lies, and no longer than it lies
    /// there: the give ends the moment the piece falls on or is hit, and a piece that falls on without a pause
    /// does not give at all. So a piece that leaves the board is done with everything else by then, and nothing in
    /// the script reaches for a piece that has gone. And what takes several hits (a crate, a tape, a slab) is hit
    /// in the script in the order the engine counted its hits, even when the effect of a later hit reaches the
    /// cell first: each cue says what is left after it, and the view lays exactly that.
    /// <para>
    /// The finale of a won game is written by the same rules, with a few of its own (<see cref="ComposeFinale"/>).
    /// The engine turns gems into rockets wave by wave and fires each wave on the board as it lay. So the moves of
    /// a wave leave the medallion one after another, each gem turns as its move lands, the rockets stand for a
    /// moment and then go off in the order they were thrown, or sooner when another's stroke reaches them, which
    /// is not the order the engine fired them in (<see cref="Volley"/> says why). Nothing falls before the last
    /// hit of the wave is shown: a piece that fell earlier would lie in the way of a stroke that the engine never
    /// sent through it. The finale spends no move and counts toward no goal, and all of it runs at the quickest
    /// pace.
    /// </para>
    /// </remarks>
    public sealed class TurnChoreographer
    {
        /// <summary>Two cues closer than this follow each other without a pause: the piece does not come to rest between them.</summary>
        private const float NoPause = 0.001f;

        private readonly PieceLedger _ledger;
        private readonly IReadOnlyList<GoalSpec> _goals;
        private readonly IReadOnlyList<GameEvent> _events;
        private readonly Board _boardAfterwards;
        private readonly List<Cue> _cues = new List<Cue>();

        /// <summary>When each piece has finished what it last did, by its number.</summary>
        private readonly Dictionary<int, float> _readyAt = new Dictionary<int, float>();

        /// <summary>
        /// Where in <see cref="_cues"/> the travel is that each piece last landed from, by its number. It is kept
        /// from one fall through the next round of hits to the fall after it: a piece that falls on or is hit while
        /// it still gives under its landing has that landing cut off there (<see cref="Travel"/>,
        /// <see cref="TakeFromTheBoard"/>).
        /// </summary>
        private readonly Dictionary<int, int> _landing = new Dictionary<int, int>();

        /// <summary>When the latest hit on a cell's crate, tape or slab is shown, for <see cref="HitTimeOf"/>.</summary>
        private readonly Dictionary<(Layered What, Position Cell), float> _latestHitOn = new Dictionary<(Layered, Position), float>();

        /// <summary>When the special piece that lay in a cell goes off there; several when several are due in one cell.</summary>
        private readonly Dictionary<Position, Queue<float>> _goesOffAt = new Dictionary<Position, Queue<float>>();

        /// <summary>
        /// When the special pieces go off that do so at a moment of their own and not when the engine destroyed
        /// them, by their cell: the ones a colour bomb made, and the rockets of a wave of the finale.
        /// </summary>
        private readonly Dictionary<Position, float> _armedAt = new Dictionary<Position, float>();

        /// <summary>From when on each cell is empty, by row and column.</summary>
        private readonly float[,] _freeAt;

        private readonly ColumnEntry[] _entries;

        /// <summary>True when the script is a won game's finale and not a move's.</summary>
        private readonly bool _isTheFinale;

        private Strike _strike;
        private Gathering? _gathering;

        /// <summary>The wave of the finale that is on: its moves are thrown and its rockets fire. Nothing in a turn, and once the hits of the wave are over.</summary>
        private Volley? _volley;

        /// <summary>The wave, while the hits that are worked through are those of one of its rockets' strokes; nothing for any other hit.</summary>
        private Volley? _strokeOfTheVolley;
        private float _phaseStart;
        private float _pace;
        private float _motionEnd;

        /// <summary>When the latest hit since the hits began is shown: a piece that goes, a crate, a tape or a slab that takes one.</summary>
        private float _lastHitAt;

        /// <summary>In the finale nothing falls before this: the last hit of the wave has been shown by then. Never set in a turn.</summary>
        private float _fallsNotBefore;

        /// <summary>
        /// In the finale, when the board has come down after the latest wave and its pieces begin to burst by
        /// themselves: where the finale's aftermath begins. Nothing while the latest wave has had none.
        /// </summary>
        private float? _aftermathBegins;
        private bool _isSettling;
        private bool _nextSeekerCarriesASpecial;

        private TurnChoreographer(PieceLedger ledger, IReadOnlyList<GoalSpec> goals, IReadOnlyList<GameEvent> events, Board boardAfterwards, bool isTheFinale)
        {
            _ledger = ledger;
            _goals = goals;
            _events = events;
            _boardAfterwards = boardAfterwards;
            _freeAt = new float[ledger.Shape.Height, ledger.Shape.Width];
            _entries = new ColumnEntry[ledger.Shape.Width];
            _isTheFinale = isTheFinale;
            _phaseStart = isTheFinale ? Tempo.FinaleLead : 0f;
            _pace = isTheFinale ? Tempo.FinaleBurstPace : 1f;
            _lastHitAt = _phaseStart;
            _strike = Strike.AllAtOnce(_phaseStart);
        }

        /// <summary>
        /// The script of one move, and the ledger brought forward to the board after it.
        /// </summary>
        /// <param name="ledger">The board as the view shows it before the move; changed to what it shows after it.</param>
        /// <param name="goals">The level's goals, in the order the plaque shows them.</param>
        /// <param name="events">The engine's events of the move, in the engine's order.</param>
        /// <param name="boardAfterwards">The engine's board after the move: a reshuffle is only told as "it happened".</param>
        /// <exception cref="InvalidOperationException">An event does not fit the ledger: the view and the engine have come apart.</exception>
        public static TurnScript Compose(PieceLedger ledger, IReadOnlyList<GoalSpec> goals, IReadOnlyList<GameEvent> events, Board boardAfterwards)
        {
            var choreographer = new TurnChoreographer(ledger, goals, events, boardAfterwards, isTheFinale: false);
            choreographer.Emit(new Cue.MoveSpent(0f));
            choreographer.TimeEveryEvent();
            return choreographer.ToScript();
        }

        /// <summary>
        /// The script of a won game's finale (Game.PlayFinale), and the ledger brought forward to the board after
        /// it: every move left over is thrown from the medallion onto a gem, which turns into a rocket; the
        /// rockets fire, and the board settles. No move is spent in it and no goal is counted, as the engine
        /// counts none.
        /// </summary>
        /// <param name="ledger">The board as the view shows it when the winning move has come to rest; changed to what it shows after the finale.</param>
        /// <param name="events">The engine's events of the finale, in the engine's order. None, when no move was left: the script is then empty.</param>
        /// <param name="boardAfterwards">The engine's board after the finale.</param>
        /// <exception cref="InvalidOperationException">An event does not fit the ledger: the view and the engine have come apart.</exception>
        public static TurnScript ComposeFinale(PieceLedger ledger, IReadOnlyList<GameEvent> events, Board boardAfterwards)
        {
            var choreographer = new TurnChoreographer(ledger, Array.Empty<GoalSpec>(), events, boardAfterwards, isTheFinale: true);
            if (events.Count > 0)
            {
                choreographer.Emit(new Cue.FinaleOpens(0f, Tempo.MedallionLights));
                choreographer.TimeEveryEvent();
            }

            return choreographer.ToScript();
        }

        private void TimeEveryEvent()
        {
            for (int index = 0; index < _events.Count; index++)
            {
                Time(_events[index], index);
            }
        }

        private TurnScript ToScript()
        {
            // OrderBy keeps cues that begin together in the order they were made, which is the engine's order.
            Cue[] inOrder = _cues.OrderBy(cue => cue.Start).ToArray();
            float seconds = inOrder.Length == 0 ? 0f : inOrder.Max(cue => cue.End);
            return new TurnScript(inOrder, seconds, showSeconds: _aftermathBegins);
        }

        private void Time(GameEvent gameEvent, int index)
        {
            switch (gameEvent)
            {
                case GameEvent.Swapped swapped:
                    Trade(swapped);
                    break;
                case GameEvent.RoundStarted round:
                    StartRound(round.Round);
                    break;
                case GameEvent.MatchCleared match:
                    _gathering = Gathering.Of(match, SpecialsMadeBeforeTheStrikeEnds(index + 1));
                    break;
                case GameEvent.EffectTriggered effect:
                    StartEffect(effect, index);
                    break;
                case GameEvent.SeekerLaunched launch:
                    Launch(launch);
                    break;
                case GameEvent.SpecialCreated created:
                    MakeSpecial(created, index);
                    break;
                case GameEvent.TileDestroyed destroyed:
                    Destroy(destroyed);
                    break;
                case GameEvent.BoxDamaged box:
                    HitCrate(box);
                    break;
                case GameEvent.ChainDamaged chain:
                    HitTape(chain);
                    break;
                case GameEvent.JellyDamaged jelly:
                    HitSlab(jelly);
                    break;
                case GameEvent.TileMoved moved:
                    MoveDown(moved);
                    break;
                case GameEvent.TileSpawned spawned:
                    LetIn(spawned);
                    break;
                case GameEvent.ItemCollected collected:
                    Collect(collected);
                    break;
                case GameEvent.Shuffled _:
                    Reshuffle();
                    break;
                default:
                    throw new InvalidOperationException($"The view has no timing for the engine's event {gameEvent}.");
            }
        }

        // ---------- the player's trade ----------

        private void Trade(GameEvent.Swapped swapped)
        {
            int carried = _ledger.PieceAt(swapped.From);
            Tile carriedTile = _ledger.TileAt(swapped.From);
            Tile otherTile = _ledger.TileAt(swapped.To);
            if (carriedTile.IsSpecial && otherTile.IsSpecial)
            {
                // Two special pieces do not trade places: one is pushed onto the other and both go off from there.
                Emit(new Cue.Slide(0f, Tempo.Swap, carried, swapped.From, swapped.To, SlideManner.Onto));
                _nextSeekerCarriesASpecial = IsASeekerWithALoad(carriedTile.Kind, otherTile.Kind);
            }
            else
            {
                Emit(new Cue.Slide(0f, Tempo.Swap, carried, swapped.From, swapped.To, SlideManner.Carried));
                Emit(new Cue.Slide(0f, Tempo.Swap, _ledger.PieceAt(swapped.To), swapped.To, swapped.From, SlideManner.Displaced));
                _ledger.Trade(swapped.From, swapped.To);
                _readyAt[_ledger.PieceAt(swapped.From)] = Tempo.Swap;
            }

            _readyAt[carried] = Tempo.Swap;
            _phaseStart = Tempo.Swap;
            _strike = Strike.AllAtOnce(_phaseStart);
        }

        /// <summary>
        /// A seeker pushed onto a rocket or a bomb carries it to its target, where it goes off as an effect of its
        /// own; a seeker alone hits its target itself. The events do not say which, the two pieces do.
        /// </summary>
        private static bool IsASeekerWithALoad(TileKind one, TileKind other)
        {
            bool isOneSeeker = (one == TileKind.Seeker) != (other == TileKind.Seeker);
            return isOneSeeker && one != TileKind.ColorBomb && other != TileKind.ColorBomb;
        }

        // ---------- hits ----------

        private void StartRound(int round)
        {
            // The first round of matches after a wave of the finale: the board has come down, and what bursts now is luck.
            if (_volley != null)
            {
                _aftermathBegins = _motionEnd;
            }

            _pace = _isTheFinale ? Tempo.FinaleBurstPace : Tempo.PaceOfRound(round);
            StartHitting(Math.Max(_phaseStart, _motionEnd));
        }

        /// <summary>From here on events are hits again: what fell has landed, and nothing is left over from the hits before.</summary>
        private void StartHitting(float notBefore)
        {
            _phaseStart = _isSettling ? _motionEnd + Tempo.PauseBeforeCascade * _pace : notBefore;
            _isSettling = false;
            _goesOffAt.Clear();
            _armedAt.Clear();
            _gathering = null;
            _volley = null;
            _strokeOfTheVolley = null;
            _lastHitAt = _phaseStart;
            _strike = Strike.AllAtOnce(_phaseStart);
        }

        private void StartEffect(GameEvent.EffectTriggered effect, int index)
        {
            if (_isSettling)
            {
                StartHitting(_phaseStart);
            }

            float start = TakeGoingOffAt(effect.Origin) ?? _phaseStart;
            _strike = Strike.Of(effect, start, _ledger.Shape, _nextSeekerCarriesASpecial, CellsReachedBeforeTheStrikeEnds(index + 1));
            _strokeOfTheVolley = _volley != null && _volley.Fires(effect.Origin) ? _volley : null;
            _nextSeekerCarriesASpecial = false;
            _gathering = null;
            Free(effect.Origin, Math.Max(FreeAt(effect.Origin), _strike.OriginIsTakenUntil));
            Emit(new Cue.Effect(start, _strike.Seconds, effect.Kind, effect.Origin, effect.Radius, _strike.SecondsPerCell));
        }

        private void Launch(GameEvent.SeekerLaunched launch)
        {
            (float departs, float lands) = _strike.Launch(launch.From, launch.Target);
            Emit(new Cue.Flight(departs, lands - departs, launch.From, launch.Target));
            if (_strike.CarriesASpecial)
            {
                GoOffAt(launch.Target, lands);
            }
        }

        /// <summary>
        /// A special piece comes onto the board. What the view has in its cell says how: a colour bomb at work
        /// turns the gem that lies there, a match leaves its special piece in a cell it has just emptied, and in
        /// the finale a move left over lands on the gem that lies there.
        /// </summary>
        /// <exception cref="InvalidOperationException">The cell holds a piece that nothing turns into a special piece.</exception>
        private void MakeSpecial(GameEvent.SpecialCreated created, int index)
        {
            if (_strike.MakesSpecials)
            {
                TurnTheGemInto(created, HitTimeAt(created.At), _strike.NextArmedTime());
            }
            else if (_ledger.PieceAt(created.At) == PieceLedger.NoPiece)
            {
                LetAppear(created, _strike.Start + Tempo.AppearAfterGather * _pace);
            }
            else if (_isTheFinale && created.Kind.IsRocket() && _ledger.TileAt(created.At).IsGem)
            {
                LandAMove(created, index);
            }
            else
            {
                throw new InvalidOperationException($"The engine made a {created.Kind} in {created.At}, where the view has {_ledger.TileAt(created.At)} and nothing that turns it.");
            }
        }

        /// <summary>
        /// A gem turns into a special piece where it lies: the gem goes as the special piece comes, and the special
        /// piece goes off at a moment of its own, whenever the engine got round to it or another hit reached it.
        /// </summary>
        private void TurnTheGemInto(GameEvent.SpecialCreated created, float start, float goesOffAt)
        {
            PlacedPiece gem = TakeFromTheBoard(created.At, start);
            Emit(new Cue.Leave(start, Tempo.Pop * _pace, gem.Id, gem.Tile, created.At, LeaveManner.Pop, created.At));
            _armedAt[created.At] = goesOffAt;
            LetAppear(created, start);
        }

        private void LetAppear(GameEvent.SpecialCreated created, float start)
        {
            Tile special = Tile.Special(created.Kind);
            int id = _ledger.NewId();
            _ledger.Put(created.At, id, special);
            _readyAt[id] = start + Tempo.AppearBeforeFalling;
            Emit(new Cue.Appear(start, Tempo.Appear, id, special, created.At));
        }

        /// <summary>
        /// A move of the finale: it leaves the medallion, the next of its wave, and the gem turns into a rocket as
        /// it lands. From there on the rocket is a special piece that goes off at a moment of its own.
        /// </summary>
        private void LandAMove(GameEvent.SpecialCreated created, int index)
        {
            if (_volley == null || _volley.IsThrown)
            {
                _volley = StartAVolley(index);
            }

            (float leaves, float lands) = _volley.Next();
            Emit(new Cue.MoveThrown(leaves, Tempo.MoveFlies, created.At));
            TurnTheGemInto(created, lands, _volley.GoesOffAt(created.At));
        }

        /// <summary>
        /// A wave of the finale begins at an event: the board has landed, and the engine names every rocket of the
        /// wave before it fires the first. A rocket that already stood on the board goes off in the wave too, the
        /// moment a stroke reaches it (one under tape does not: the tape takes the hit).
        /// </summary>
        private Volley StartAVolley(int index)
        {
            StartHitting(Math.Max(_phaseStart, _motionEnd));
            _aftermathBegins = null; // whatever burst after the wave before is part of the show: more rockets follow it
            var thrown = new List<RocketPlace>();
            for (int next = index; next < _events.Count && _events[next] is GameEvent.SpecialCreated made; next++)
            {
                thrown.Add(new RocketPlace(made.At, made.Kind));
            }

            RocketPlace[] standing = _ledger.Pieces
                .Where(piece => piece.Tile.Kind.IsRocket() && _ledger.TapeAt(piece.Cell) == 0)
                .Select(piece => new RocketPlace(piece.Cell, piece.Tile.Kind))
                .ToArray();
            var volley = new Volley(_phaseStart, thrown, standing);
            foreach (RocketPlace rocket in standing)
            {
                if (volley.IsSetOff(rocket.Cell))
                {
                    _armedAt[rocket.Cell] = volley.GoesOffAt(rocket.Cell);
                }
            }

            return volley;
        }

        private void Destroy(GameEvent.TileDestroyed destroyed)
        {
            Position at = destroyed.At;
            float time = _armedAt.Remove(at, out float armed) ? armed : HitTimeAt(at);
            PlacedPiece piece = TakeFromTheBoard(at, time);
            if (piece.Tile != destroyed.Tile)
            {
                throw new InvalidOperationException($"The engine destroyed {destroyed.Tile} in {at}, where the view has {piece.Tile}.");
            }

            if (piece.Tile.IsSpecial)
            {
                Emit(new Cue.Leave(time, Tempo.Fire, piece.Id, piece.Tile, at, LeaveManner.Fire, at));
                GoOffAt(at, time + Tempo.FireLead);
            }
            else if (_gathering is Gathering gathering && gathering.Holds(at))
            {
                Emit(new Cue.Leave(time, Tempo.Gather * _pace, piece.Id, piece.Tile, at, LeaveManner.Gather, gathering.Toward));
            }
            else
            {
                Emit(new Cue.Leave(time, Tempo.Pop * _pace, piece.Id, piece.Tile, at, LeaveManner.Pop, at));
            }

            if (piece.Tile.IsGem)
            {
                GemColor colour = piece.Tile.Color;
                Count(goal => goal is GoalSpec.CollectColor collect && collect.Color == colour, time);
            }

            Free(at, time + Tempo.CellFreeAfterHit * _pace);
        }

        private void HitCrate(GameEvent.BoxDamaged box)
        {
            float time = HitTimeOf(Layered.Crate, box.At);
            if (box.HpLeft > 0)
            {
                Tile weaker = Tile.Box(box.HpLeft);
                _ledger.Put(box.At, _ledger.PieceAt(box.At), weaker);
                Emit(new Cue.Reface(time, Tempo.Bump, _ledger.PieceAt(box.At), weaker, box.At));
                return;
            }

            PlacedPiece crate = TakeFromTheBoard(box.At, time);
            Emit(new Cue.Leave(time, Tempo.Break, crate.Id, crate.Tile, box.At, LeaveManner.Break, box.At));
            Count(goal => goal is GoalSpec.BreakBoxes, time);
            Free(box.At, time + Tempo.CellFreeAfterHit * _pace);
        }

        private void HitTape(GameEvent.ChainDamaged chain)
        {
            float time = HitTimeOf(Layered.Tape, chain.At);
            _ledger.SetTape(chain.At, chain.HpLeft);
            Emit(new Cue.TapeHit(time, chain.HpLeft > 0 ? Tempo.Bump : Tempo.Peel, chain.At, chain.HpLeft));
            if (chain.HpLeft == 0)
            {
                Count(goal => goal is GoalSpec.BreakChains, time);
            }
        }

        private void HitSlab(GameEvent.JellyDamaged jelly)
        {
            float time = HitTimeOf(Layered.Slab, jelly.At);
            _ledger.SetSlab(jelly.At, jelly.LayersLeft);
            Emit(new Cue.SlabHit(time, Tempo.Peel, jelly.At, jelly.LayersLeft));
            if (jelly.LayersLeft == 0)
            {
                Count(goal => goal is GoalSpec.ClearJelly, time);
            }
        }

        /// <summary>
        /// Takes the piece of a cell out of the ledger because it leaves the board at a moment. If the piece still
        /// gives under its landing then, the landing ends there: from that moment on the piece is its leaving's alone.
        /// </summary>
        /// <exception cref="InvalidOperationException">The cell is empty: the view and the engine have come apart.</exception>
        private PlacedPiece TakeFromTheBoard(Position cell, float leavesAt)
        {
            PlacedPiece piece = _ledger.Take(cell);
            if (_landing.Remove(piece.Id, out int landedFrom))
            {
                EndTheLandingBy(landedFrom, leavesAt);
            }

            return piece;
        }

        /// <summary>
        /// When a hit on something that takes several hits is shown: when the strike reaches its cell, but never
        /// before the hit the engine counted before it on the same thing. Two effects can reach a cell in the other
        /// order than the engine worked them out; shown that way, the hit that leaves one layer would come after
        /// the hit that leaves none and lay a layer back. Held back to the earlier hit's moment, both are seen
        /// together, in the engine's order.
        /// </summary>
        private float HitTimeOf(Layered what, Position cell)
        {
            float time = HitTimeAt(cell);
            if (_latestHitOn.TryGetValue((what, cell), out float hitBefore))
            {
                time = Math.Max(time, hitBefore);
            }

            _latestHitOn[(what, cell)] = time;
            return time;
        }

        /// <summary>
        /// When the hit that is being worked through reaches a cell; a colour bomb's light gets there as a ray. The
        /// strokes of a wave of the finale are told in the view's own order (<see cref="Volley.HitIsShownAt"/>),
        /// and what such a stroke set off (a bomb, a seeker, a colour bomb) never shows a hit later than the next
        /// stroke of the wave gets to the cell: the stroke would pass over a piece that stands, and only goes
        /// when the blast, the seeker or the ray arrives. No ray is sent for a piece a stroke takes first.
        /// </summary>
        private float HitTimeAt(Position cell)
        {
            if (_strokeOfTheVolley is Volley strokeOfTheWave)
            {
                return strokeOfTheWave.HitIsShownAt(cell);
            }

            float time = _strike.TimeAt(cell, out bool isANewRay);
            if (_volley?.NextStrokeReaches(cell) is float strokeGetsThere && strokeGetsThere < time)
            {
                return strokeGetsThere;
            }

            if (isANewRay)
            {
                Emit(new Cue.Zap(time - Tempo.Ray, Tempo.Ray, _strike.Origin, cell));
            }

            return time;
        }

        private void GoOffAt(Position cell, float time)
        {
            if (!_goesOffAt.TryGetValue(cell, out Queue<float>? times))
            {
                times = new Queue<float>();
                _goesOffAt[cell] = times;
            }

            times.Enqueue(time);
        }

        /// <summary>When the special piece of a cell goes off, if one is due there: the engine fires them in the order they were hit.</summary>
        private float? TakeGoingOffAt(Position cell) =>
            _goesOffAt.TryGetValue(cell, out Queue<float>? times) && times.Count > 0 ? times.Dequeue() : (float?)null;

        // ---------- falls ----------

        /// <summary>
        /// From here on events are falls. In the finale the first of them says how long everything waits: the
        /// engine fired the whole wave on the board as it lay, so no piece may fall into the way of a hit that is
        /// still to be shown, nor into a cell that a stroke of the wave has yet to run through.
        /// </summary>
        private void StartSettling()
        {
            if (_isTheFinale && !_isSettling)
            {
                _fallsNotBefore = _lastHitAt + Tempo.CellFreeAfterHit * _pace;
                KeepCellsUntilTheStrokesHavePassed();
            }

            _isSettling = true;
            _gathering = null;
        }

        /// <summary>
        /// A stroke runs on to the ends of its line after its last hit, over cells that are empty by then: each
        /// cell of the wave's lines is taken until the last stroke through it has got there.
        /// </summary>
        private void KeepCellsUntilTheStrokesHavePassed()
        {
            if (!(_volley is Volley wave))
            {
                return;
            }

            foreach (Position cell in _ledger.Cells())
            {
                if (wave.LastStrokeReaches(cell) is float reached)
                {
                    Free(cell, Math.Max(FreeAt(cell), reached));
                }
            }
        }

        private void MoveDown(GameEvent.TileMoved moved)
        {
            StartSettling();
            int id = _ledger.PieceAt(moved.From);
            bool isStraight = moved.From.X == moved.To.X;
            float wayIsFree = isStraight ? FreeAlong(moved.To.X, moved.From.Y + 1, moved.To.Y) : FreeAt(moved.To);
            float start = Math.Max(Math.Max(ReadyAt(id), wayIsFree), _fallsNotBefore);
            _ledger.Move(moved.From, moved.To);
            Free(moved.From, start + Tempo.FollowLag);
            Travel(id, isStraight
                ? new Cue.Fall(start, FallLaw.SecondsFor(moved.To.Y - moved.From.Y), id, moved.From, moved.To, Tempo.Land)
                : (Cue)new Cue.Slip(start, Tempo.Slip, id, moved.From, moved.To, Tempo.Land));
        }

        private void LetIn(GameEvent.TileSpawned spawned)
        {
            StartSettling();
            Position to = spawned.At;
            int top = TopRowOf(to.X);
            float wayIsFree = Math.Max(FreeAlong(to.X, top, to.Y), _fallsNotBefore);
            (int fromRow, float start) = _entries[to.X].Next(top, wayIsFree);
            float seconds = FallLaw.SecondsFor(to.Y - fromRow);
            _entries[to.X].Entered(start, seconds);

            int id = _ledger.NewId();
            _ledger.Put(to, id, spawned.Tile);
            Travel(id, new Cue.Enter(start, seconds, id, spawned.Tile, fromRow, to, Tempo.Land));
        }

        private void Collect(GameEvent.ItemCollected collected)
        {
            StartSettling();
            float start = Math.Max(Math.Max(ReadyAt(_ledger.PieceAt(collected.At)), _phaseStart), _fallsNotBefore);
            PlacedPiece item = TakeFromTheBoard(collected.At, start);
            Emit(new Cue.Leave(start, Tempo.Collect, item.Id, item.Tile, collected.At, LeaveManner.Collect, collected.At));
            Count(goal => goal is GoalSpec.DropItems, start + Tempo.Collect);
            Free(collected.At, start + Tempo.Collect / 2f);
        }

        /// <summary>
        /// Adds a piece's travel. The travel before it, in this fall or in one before it, loses its landing when
        /// this one follows without a pause; when the piece still gives as this one begins, the landing ends
        /// there. However short the piece's rest is, the give lasts as long as it: the view shows a landing cut
        /// short as the beginning of a whole one, and of a few milliseconds of it nothing is seen. A landing that
        /// is over by then is left as it is, and whether it is over is asked of the moment the give ends, not of
        /// how long the piece rested: where the give ends in the very moment this travel begins, the rest can
        /// come out a rounding short of the landing, and a landing that runs past nothing would be written anew
        /// that much shorter.
        /// </summary>
        private void Travel(int pieceId, Cue travel)
        {
            if (_landing.TryGetValue(pieceId, out int landedFrom))
            {
                Cue landed = _cues[landedFrom];
                bool travelsOnAtOnce = landed.End >= travel.Start - NoPause;
                bool stillGives = landed.End + LandingSecondsOf(landed) > travel.Start;
                if (travelsOnAtOnce)
                {
                    _cues[landedFrom] = LandingFor(landed, 0f);
                }
                else if (stillGives)
                {
                    EndTheLandingBy(landedFrom, travel.Start);
                }
            }

            _landing[pieceId] = _cues.Count;
            _readyAt[pieceId] = travel.End;
            Emit(travel);
        }

        /// <summary>
        /// Lets the give of a travel's landing end at a moment, if it would go on beyond it: the piece does its
        /// next thing then. Nothing is left of the landing when that is the moment the piece arrived.
        /// </summary>
        private void EndTheLandingBy(int landedFrom, float moment)
        {
            Cue travel = _cues[landedFrom];
            float untilThen = Math.Max(0f, moment - travel.End);
            if (untilThen < LandingSecondsOf(travel))
            {
                _cues[landedFrom] = LandingFor(travel, untilThen);
            }
        }

        /// <summary>A travel with its landing cut to so many seconds; none makes the piece travel on at once.</summary>
        private static Cue LandingFor(Cue travel, float seconds) => travel switch
        {
            Cue.Fall fall => fall.LandingFor(seconds),
            Cue.Enter enter => enter.LandingFor(seconds),
            Cue.Slip slip => slip.LandingFor(seconds),
            _ => throw new InvalidOperationException($"{travel.GetType().Name} is not a travel."),
        };

        private static float LandingSecondsOf(Cue travel) => travel switch
        {
            Cue.Fall fall => fall.LandingSeconds,
            Cue.Enter enter => enter.LandingSeconds,
            Cue.Slip slip => slip.LandingSeconds,
            _ => throw new InvalidOperationException($"{travel.GetType().Name} is not a travel."),
        };

        private float ReadyAt(int pieceId) => _readyAt.TryGetValue(pieceId, out float ready) ? ready : 0f;

        private float FreeAt(Position cell) => _freeAt[cell.Y, cell.X];

        private void Free(Position cell, float from) => _freeAt[cell.Y, cell.X] = from;

        /// <summary>From when on a stretch of a column is empty, top row to bottom row. Gaps in the board are always empty.</summary>
        private float FreeAlong(int column, int fromRow, int untilRow)
        {
            float free = 0f;
            for (int row = fromRow; row <= untilRow; row++)
            {
                free = Math.Max(free, _freeAt[row, column]);
            }

            return free;
        }

        private int TopRowOf(int column)
        {
            for (int row = 0; row < _ledger.Shape.Height; row++)
            {
                if (_ledger.Shape.IsPlayable(new Position(column, row)))
                {
                    return row;
                }
            }

            throw new InvalidOperationException($"The engine put a piece into column {column}, which has no cell.");
        }

        // ---------- the rest ----------

        /// <summary>
        /// The engine only says that it shuffled: it deals the colours of the free gems anew, each gem cell stays a
        /// gem cell. So every free gem of the view keeps its place and takes the colour the board now has there.
        /// </summary>
        private void Reshuffle()
        {
            var dealt = new List<PlacedPiece>();
            foreach (Position cell in _ledger.Cells())
            {
                if (_ledger.TileAt(cell).IsGem && _ledger.TapeAt(cell) == 0)
                {
                    _ledger.Put(cell, _ledger.PieceAt(cell), _boardAfterwards.TileAt(cell));
                    dealt.Add(new PlacedPiece(_ledger.PieceAt(cell), cell, _ledger.TileAt(cell)));
                }
            }

            float start = Math.Max(_motionEnd, _phaseStart) + Tempo.PauseBeforeReshuffle;
            Emit(new Cue.Reshuffle(start, Tempo.Reshuffle, dealt));
        }

        /// <summary>One more of every goal that counts this, at this moment.</summary>
        private void Count(Func<GoalSpec, bool> counts, float time)
        {
            for (int goal = 0; goal < _goals.Count; goal++)
            {
                if (counts(_goals[goal]))
                {
                    Emit(new Cue.GoalTick(time, goal));
                }
            }
        }

        /// <summary>Adds a cue. The pieces are at rest when the last cue that moves or changes one is over; lights may still fade then.</summary>
        private void Emit(Cue cue)
        {
            _cues.Add(cue);
            bool isOnlyLight = cue is Cue.Effect || cue is Cue.Zap || cue is Cue.Flight || cue is Cue.FinaleOpens || cue is Cue.MoveThrown;
            if (!isOnlyLight)
            {
                _motionEnd = Math.Max(_motionEnd, cue.End);
            }

            bool isAHit = cue is Cue.Leave || cue is Cue.Reface || cue is Cue.TapeHit || cue is Cue.SlabHit;
            if (isAHit)
            {
                _lastHitAt = Math.Max(_lastHitAt, cue.Start);
            }
        }

        /// <summary>The cells in which special pieces are made before the hits that begin at an event are over.</summary>
        private List<Position> SpecialsMadeBeforeTheStrikeEnds(int from)
        {
            var made = new List<Position>();
            for (int index = from; index < _events.Count && !EndsAStrike(_events[index]); index++)
            {
                if (_events[index] is GameEvent.SpecialCreated created)
                {
                    made.Add(created.At);
                }
            }

            return made;
        }

        /// <summary>How many cells the effect that begins at an event reaches: what a colour bomb spaces its rays by.</summary>
        private int CellsReachedBeforeTheStrikeEnds(int from)
        {
            var reached = new HashSet<Position>();
            for (int index = from; index < _events.Count && !EndsAStrike(_events[index]); index++)
            {
                switch (_events[index])
                {
                    case GameEvent.TileDestroyed destroyed:
                        reached.Add(destroyed.At);
                        break;
                    case GameEvent.SpecialCreated created:
                        reached.Add(created.At);
                        break;
                    case GameEvent.ChainDamaged chain:
                        reached.Add(chain.At);
                        break;
                }
            }

            return reached.Count;
        }

        /// <summary>
        /// The hits of one round of matches or of one effect end where the next effect, the next round or a fall begins.
        /// The matches of a round are one strike: the engine names the special pieces they leave after all of them.
        /// </summary>
        private static bool EndsAStrike(GameEvent gameEvent) =>
            gameEvent is GameEvent.EffectTriggered || gameEvent is GameEvent.RoundStarted
            || gameEvent is GameEvent.TileMoved || gameEvent is GameEvent.TileSpawned
            || gameEvent is GameEvent.ItemCollected || gameEvent is GameEvent.Shuffled;

        /// <summary>What on a cell takes more than one hit. A cell can hold all three: a crate on a slab, or a piece under tape on one.</summary>
        private enum Layered
        {
            Crate,
            Tape,
            Slab,
        }

        /// <summary>The cells of a match that leaves a special piece: its pieces are drawn into that piece's cell.</summary>
        private sealed class Gathering
        {
            private readonly HashSet<Position> _cells;

            private Gathering(IEnumerable<Position> cells, Position toward)
            {
                _cells = new HashSet<Position>(cells);
                Toward = toward;
            }

            public Position Toward { get; }

            /// <summary>Nothing when the match leaves no special piece.</summary>
            public static Gathering? Of(GameEvent.MatchCleared match, List<Position> specialsMade)
            {
                foreach (Position made in specialsMade)
                {
                    if (match.Cells.Contains(made))
                    {
                        return new Gathering(match.Cells, made);
                    }
                }

                return null;
            }

            public bool Holds(Position cell) => _cells.Contains(cell);
        }

        /// <summary>
        /// The way into a column from above. New pieces queue up over the column's top cell, one row higher each, and
        /// let go one after another; once the queue has landed, the next piece starts next to the board again.
        /// </summary>
        private struct ColumnEntry
        {
            private int _queued;
            private float _freeAt;
            private float _busyUntil;

            /// <summary>The row the next piece starts in and the moment it lets go.</summary>
            public (int FromRow, float Start) Next(int topRow, float wayIsFree)
            {
                if (wayIsFree >= _busyUntil)
                {
                    _queued = 0;
                }

                _queued++;
                return (topRow - _queued, Math.Max(wayIsFree, _freeAt));
            }

            public void Entered(float start, float seconds)
            {
                _freeAt = start + Tempo.FollowLag;
                _busyUntil = start + seconds;
            }
        }
    }
}
