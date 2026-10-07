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
    /// of matches waits until everything has landed. A piece that leaves the board is done with everything else by
    /// then: the give of its landing ends the moment it is hit, so nothing in the script reaches for a piece that
    /// has gone. And what takes several hits (a crate, a tape, a slab) is hit in the script in the order the engine
    /// counted its hits, even when the effect of a later hit reaches the cell first: each cue says what is left
    /// after it, and the view lays exactly that.
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

        /// <summary>Where in <see cref="_cues"/> each piece's latest travel of the current fall is, by its number.</summary>
        private readonly Dictionary<int, int> _latestTravel = new Dictionary<int, int>();

        /// <summary>
        /// Where in <see cref="_cues"/> the travel is that each piece last landed from, by its number. Unlike
        /// <see cref="_latestTravel"/> it is kept from one fall to the next round of hits: a piece that is hit while
        /// it still gives under its landing has that landing cut off (<see cref="TakeFromTheBoard"/>).
        /// </summary>
        private readonly Dictionary<int, int> _landing = new Dictionary<int, int>();

        /// <summary>When the latest hit on a cell's crate, tape or slab is shown, for <see cref="HitTimeOf"/>.</summary>
        private readonly Dictionary<(Layered What, Position Cell), float> _latestHitOn = new Dictionary<(Layered, Position), float>();

        /// <summary>When the special piece that lay in a cell goes off there; several when several are due in one cell.</summary>
        private readonly Dictionary<Position, Queue<float>> _goesOffAt = new Dictionary<Position, Queue<float>>();

        /// <summary>When the special pieces a colour bomb made go off, by their cell.</summary>
        private readonly Dictionary<Position, float> _armedAt = new Dictionary<Position, float>();

        /// <summary>From when on each cell is empty, by row and column.</summary>
        private readonly float[,] _freeAt;

        private readonly ColumnEntry[] _entries;

        private Strike _strike = Strike.AllAtOnce(0f);
        private Gathering? _gathering;
        private float _phaseStart;
        private float _pace = 1f;
        private float _motionEnd;
        private bool _isSettling;
        private bool _nextSeekerCarriesASpecial;

        private TurnChoreographer(PieceLedger ledger, IReadOnlyList<GoalSpec> goals, IReadOnlyList<GameEvent> events, Board boardAfterwards)
        {
            _ledger = ledger;
            _goals = goals;
            _events = events;
            _boardAfterwards = boardAfterwards;
            _freeAt = new float[ledger.Shape.Height, ledger.Shape.Width];
            _entries = new ColumnEntry[ledger.Shape.Width];
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
            var choreographer = new TurnChoreographer(ledger, goals, events, boardAfterwards);
            choreographer.Emit(new Cue.MoveSpent(0f));
            for (int index = 0; index < events.Count; index++)
            {
                choreographer.Time(events[index], index);
            }

            return choreographer.ToScript();
        }

        private TurnScript ToScript()
        {
            // OrderBy keeps cues that begin together in the order they were made, which is the engine's order.
            Cue[] inOrder = _cues.OrderBy(cue => cue.Start).ToArray();
            float seconds = inOrder.Length == 0 ? 0f : inOrder.Max(cue => cue.End);
            return new TurnScript(inOrder, seconds);
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
                    MakeSpecial(created);
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
            _pace = Tempo.PaceOfRound(round);
            StartHitting(Math.Max(_phaseStart, _motionEnd));
        }

        /// <summary>From here on events are hits again: what fell has landed, and nothing is left over from the hits before.</summary>
        private void StartHitting(float notBefore)
        {
            _phaseStart = _isSettling ? _motionEnd + Tempo.PauseBeforeCascade * _pace : notBefore;
            _isSettling = false;
            _latestTravel.Clear();
            _goesOffAt.Clear();
            _armedAt.Clear();
            _gathering = null;
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

        private void MakeSpecial(GameEvent.SpecialCreated created)
        {
            Tile special = Tile.Special(created.Kind);
            int id = _ledger.NewId();
            float start;
            if (_strike.MakesSpecials)
            {
                // A colour bomb turns a gem into a special piece where it lies: the gem goes as the special comes.
                start = HitTimeAt(created.At);
                PlacedPiece gem = TakeFromTheBoard(created.At, start);
                Emit(new Cue.Leave(start, Tempo.Pop * _pace, gem.Id, gem.Tile, created.At, LeaveManner.Pop, created.At));
                _armedAt[created.At] = _strike.NextArmedTime();
            }
            else
            {
                start = _strike.Start + Tempo.AppearAfterGather * _pace;
            }

            _ledger.Put(created.At, id, special);
            _readyAt[id] = start + Tempo.AppearBeforeFalling;
            Emit(new Cue.Appear(start, Tempo.Appear, id, special, created.At));
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
                Cue travel = _cues[landedFrom];
                float untilItLeaves = Math.Max(0f, leavesAt - travel.End);
                if (untilItLeaves < LandingSecondsOf(travel))
                {
                    _cues[landedFrom] = LandingFor(travel, untilItLeaves);
                }
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

        /// <summary>When the hit that is being worked through reaches a cell; a colour bomb's light gets there as a ray.</summary>
        private float HitTimeAt(Position cell)
        {
            float time = _strike.TimeAt(cell, out bool isANewRay);
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

        private void StartSettling()
        {
            _isSettling = true;
            _gathering = null;
        }

        private void MoveDown(GameEvent.TileMoved moved)
        {
            StartSettling();
            int id = _ledger.PieceAt(moved.From);
            bool isStraight = moved.From.X == moved.To.X;
            float wayIsFree = isStraight ? FreeAlong(moved.To.X, moved.From.Y + 1, moved.To.Y) : FreeAt(moved.To);
            float start = Math.Max(ReadyAt(id), wayIsFree);
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
            float wayIsFree = FreeAlong(to.X, top, to.Y);
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
            float start = Math.Max(ReadyAt(_ledger.PieceAt(collected.At)), _phaseStart);
            PlacedPiece item = TakeFromTheBoard(collected.At, start);
            Emit(new Cue.Leave(start, Tempo.Collect, item.Id, item.Tile, collected.At, LeaveManner.Collect, collected.At));
            Count(goal => goal is GoalSpec.DropItems, start + Tempo.Collect);
            Free(collected.At, start + Tempo.Collect / 2f);
        }

        /// <summary>Adds a piece's travel; the travel before it loses its landing when this one follows without a pause.</summary>
        private void Travel(int pieceId, Cue travel)
        {
            if (_latestTravel.TryGetValue(pieceId, out int before) && _cues[before].End >= travel.Start - NoPause)
            {
                _cues[before] = LandingFor(_cues[before], 0f);
            }

            _latestTravel[pieceId] = _cues.Count;
            _landing[pieceId] = _cues.Count;
            _readyAt[pieceId] = travel.End;
            Emit(travel);
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
            bool isOnlyLight = cue is Cue.Effect || cue is Cue.Zap || cue is Cue.Flight;
            if (!isOnlyLight)
            {
                _motionEnd = Math.Max(_motionEnd, cue.End);
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
