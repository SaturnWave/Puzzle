using System;
using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Engine.Resolution;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;

namespace Puzzle.Core.Engine
{
    /// <summary>
    /// One play-through of a level. The single entry point shared by the Unity client, the test bot and the
    /// level generator, so all three run exactly the same rules.
    /// </summary>
    /// <remarks>
    /// Every random decision comes from streams derived from the seed given to <see cref="Start"/>: the same
    /// seed and the same moves always produce the same game; with boosters and the extra moves, the same
    /// <see cref="Actions"/> do (<see cref="AttemptReplay"/>).
    /// </remarks>
    public sealed class Game
    {
        private const ulong InitialStream = 1;
        private const ulong SpawnStream = 2;
        private const ulong EffectStream = 3;
        private const ulong ShuffleStream = 4;
        private const ulong StartBoosterStream = 5;

        private readonly GameOptions _options;
        private readonly Mode _mode;
        private readonly ItemLedger _items;
        private readonly ITileSource _tiles;
        private readonly Rng _effectRng;
        private readonly Rng _shuffleRng;
        private readonly StatsCounter _moveStats = new StatsCounter();
        private readonly StatsCounter _totalStats;
        private readonly EventLog _events;

        /// <summary>
        /// What the player did, in order; nothing while <see cref="GameOptions.RecordActions"/> is off, and in every copy
        /// of a game. Added to with <c>?.</c>, which builds no action at all when there is no list.
        /// </summary>
        private readonly List<AttemptAction>? _actions;
        private readonly MatchFinder _matches;
        private readonly MoveFinder _moves;
        private readonly TurnResolver _resolver;
        private readonly Shuffler _shuffler;
        private IReadOnlyList<GameEvent> _lastEvents = Array.Empty<GameEvent>();

        /// <summary>Moves a start booster or the extra-moves offer added to the level's limit.</summary>
        private int _bonusMoves;

        /// <summary>Replaced on every use and never changed in place, so a copy of the game may share it.</summary>
        private string[] _boostersUsed = Array.Empty<string>();

        private Game(
            LevelDefinition level,
            GameOptions options,
            Mode mode,
            Board board,
            GoalTracker goals,
            ItemLedger items,
            ITileSource tiles,
            Rng effectRng,
            Rng shuffleRng,
            StatsCounter totalStats)
        {
            Level = level;
            _options = options;
            _mode = mode;
            Board = board;
            Goals = goals;
            _items = items;
            _tiles = tiles;
            _effectRng = effectRng;
            _shuffleRng = shuffleRng;
            _totalStats = totalStats;
            _events = options.RecordEvents && mode == Mode.Live ? new EventLog(true) : EventLog.Disabled;
            _actions = options.RecordActions && mode == Mode.Live ? new List<AttemptAction>() : null;
            Actions = _actions?.AsReadOnly() ?? (IReadOnlyList<AttemptAction>)Array.Empty<AttemptAction>();

            RuleSet rules = options.Rules;
            var pending = new Queue<Effect>();
            var damage = new DamageResolver(board, goals, _moveStats, _events, pending, rules);
            var effects = new EffectResolver(board, goals, _moveStats, _events, damage, pending, rules, effectRng);
            _matches = new MatchFinder(board);
            var gravity = new Gravity(board, tiles, _matches, goals, _moveStats, _events, items);
            _moves = new MoveFinder(board, _matches, rules);
            _resolver = new TurnResolver(board, rules, pending, damage, effects, _matches, gravity, _moveStats, _events);
            _shuffler = new Shuffler(board, _matches, _moves, rules, shuffleRng, _moveStats, _events);
        }

        private enum Mode
        {
            /// <summary>A real game: real refills, shuffles, events if requested.</summary>
            Live,

            /// <summary>A bot's what-if copy: refills hidden, no shuffles, no events.</summary>
            Lookahead,

            /// <summary>An exact copy that knows the future refills (for solvers): live rules, no events.</summary>
            Replica,

            /// <summary>A bot's sampled future: live rules with freshly seeded randomness, no events, no move limit.</summary>
            Imagined,
        }

        public LevelDefinition Level { get; }

        public Board Board { get; }

        public GoalTracker Goals { get; }

        public GameStatus Status { get; private set; }

        public int MovesUsed { get; private set; }

        /// <summary>The moves the player has in this attempt: the level's limit and whatever a booster or the extra-moves offer added.</summary>
        public int MoveLimit => (_options.MoveLimitOverride ?? Level.MoveLimit) + _bonusMoves;

        public int MovesLeft => Math.Max(0, MoveLimit - MovesUsed);

        /// <summary>What the most recent action of the player did: a move, a booster, or taking the extra moves (nothing, unless the board had to be shuffled).</summary>
        public PlayStats LastMove { get; private set; } = PlayStats.None;

        /// <summary>
        /// Events of the most recent action, in order: a move, a booster, the extra moves taken, the finale; right after
        /// the start, the specials the start boosters put on the board. Empty unless
        /// <see cref="GameOptions.RecordEvents"/> is on.
        /// </summary>
        public IReadOnlyList<GameEvent> LastEvents => _lastEvents;

        /// <summary>The boosters used so far by their record names (<see cref="BoosterNames"/>), in order; start boosters come first.</summary>
        public IReadOnlyList<string> BoostersUsed => Array.AsReadOnly(_boostersUsed);

        /// <summary>Whether the "+5 moves" offer was taken in this attempt.</summary>
        public bool ExtraMovesTaken { get; private set; }

        /// <summary>
        /// What the player did in this attempt, in order, as a replay needs it (<see cref="AttemptAction"/>): the moves and
        /// boosters the game accepted, the extra moves taken, and the refused requests that drew from the shuffle stream.
        /// Empty unless <see cref="GameOptions.RecordActions"/> is on; a copy of the game keeps none. It grows as the game
        /// goes on: whoever keeps it copies it.
        /// </summary>
        public IReadOnlyList<AttemptAction> Actions { get; }

        /// <summary>Whether <see cref="PlayFinale"/> has run.</summary>
        public bool FinalePlayed { get; private set; }

        public PlayStats TotalStats => _totalStats.ToSnapshot();

        /// <summary>An imagined future is open-ended: a bot's thinking must not depend on the moves left.</summary>
        private bool HasMoveLimit => _mode != Mode.Imagined;

        /// <summary>Starts an attempt whose board and play both come from <paramref name="seed"/>.</summary>
        /// <exception cref="LevelInitializationException">The level cannot produce a valid starting board.</exception>
        public static Game Start(LevelDefinition level, ulong seed, GameOptions? options = null) =>
            Start(level, AttemptSeeds.From(seed), options);

        /// <exception cref="LevelInitializationException">The level cannot produce a valid starting board.</exception>
        public static Game Start(LevelDefinition level, AttemptSeeds seeds, GameOptions? options = null)
        {
            options ??= GameOptions.Default;
            ulong seed = seeds.Play;
            var board = new Board(level.Layout.Shape);
            var items = new ItemLedger { Spawned = level.Layout.PresetItemCount };
            SpawnDirector director = SpawnDirector.Create(level.Spawn, options.AssistPercent, new Rng(Rng.Derive(seed, SpawnStream)));
            var game = new Game(
                level,
                options,
                Mode.Live,
                board,
                new GoalTracker(level.Goals),
                items,
                director,
                new Rng(Rng.Derive(seed, EffectStream)),
                new Rng(Rng.Derive(seed, ShuffleStream)),
                new StatsCounter());

            var initializer = new BoardInitializer(board, game._matches, game._moves, options.Rules);
            initializer.Fill(level.Layout, level.Spawn.Palette, new Rng(Rng.Derive(seeds.Board, InitialStream)));
            game.TakeStartBoosters(seed);
            return game;
        }

        /// <summary>
        /// A copy for trying moves without seeing the future: refills arrive as <see cref="TileKind.Hidden"/>
        /// tiles that never match, and other random choices come from <paramref name="seed"/> instead of the
        /// live game's streams. No events, no shuffles.
        /// </summary>
        public Game CreateLookahead(ulong seed) =>
            new Game(
                Level,
                _options,
                Mode.Lookahead,
                Board.Clone(),
                Goals.Clone(),
                _items.Clone(),
                HiddenTileSource.Instance,
                new Rng(Rng.Derive(seed, EffectStream)),
                new Rng(Rng.Derive(seed, ShuffleStream)),
                _totalStats.Clone()).ContinuingFrom(this);

        /// <summary>
        /// An exact copy, including every random stream: playing the same moves on it reproduces this game's
        /// future. That is perfect information — meant for solvers and analysis, never for a fair player bot.
        /// A replica of an imagined copy (<see cref="CreateImagined"/>) stays imagined, so it has no move limit either.
        /// </summary>
        public Game CreateReplica() =>
            new Game(
                Level,
                _options,
                _mode == Mode.Imagined ? Mode.Imagined : Mode.Replica,
                Board.Clone(),
                Goals.Clone(),
                _items.Clone(),
                _tiles.Clone(),
                _effectRng.Clone(),
                _shuffleRng.Clone(),
                _totalStats.Clone())
            {
                LastMove = LastMove,
            }.ContinuingFrom(this);

        /// <summary>
        /// A sampled future for a fair search bot: this game's present state under live rules, with every random
        /// stream (refills, effect choices, shuffles) freshly seeded from <paramref name="seed"/>. Nothing is cloned
        /// from, advanced on or derived from this game's own streams, so the copy knows no more than the player
        /// does, and the same state, seed and moves always give the same imagined game.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The copy keeps the spawn director's dueness weights: they follow from the refills seen so far, which a
        /// player with perfect memory knows. It also inherits the director's strategies, including any
        /// <see cref="GameOptions.AssistPercent"/>; reports and calibration always play with assist 0.
        /// </para>
        /// <para>
        /// It never runs out of moves (a bot's thinking must not depend on the moves left), records no events,
        /// shuffles a dead board like a live game, and starts with an empty <see cref="LastMove"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">This game's refills are hidden (a lookahead copy or a copy of one).</exception>
        public Game CreateImagined(ulong seed) =>
            new Game(
                Level,
                _options,
                Mode.Imagined,
                Board.Clone(),
                Goals.Clone(),
                _items.Clone(),
                _tiles.Reseed(new Rng(Rng.Derive(seed, SpawnStream))),
                new Rng(Rng.Derive(seed, EffectStream)),
                new Rng(Rng.Derive(seed, ShuffleStream)),
                _totalStats.Clone()).ContinuingFrom(this);

        public IReadOnlyList<Move> FindLegalMoves()
        {
            var moves = new List<Move>();
            _moves.Collect(moves);
            return moves;
        }

        /// <summary>Appends the legal moves to <paramref name="buffer"/> (reuse it across calls to avoid allocations).</summary>
        public void CollectLegalMoves(List<Move> buffer) => _moves.Collect(buffer);

        public bool IsLegal(Move move)
        {
            BoardShape shape = Board.Shape;
            if (!shape.IsPlayable(move.From) || !shape.IsPlayable(move.To))
            {
                return false;
            }

            return move.Kind == MoveKind.Tap
                ? _moves.IsLegalTap(shape.IndexOf(move.From))
                : _moves.IsLegalSwap(shape.IndexOf(move.From), shape.IndexOf(move.To));
        }

        /// <summary>Plays a move. An illegal move is rejected without costing a move.</summary>
        /// <exception cref="InvalidOperationException">The game is already over.</exception>
        public bool TryPlay(Move move)
        {
            if (Status.IsOver())
            {
                throw new InvalidOperationException($"The game is over ({Status}).");
            }

            if (!IsLegal(move))
            {
                return false;
            }

            _moveStats.Reset();
            _events.Clear();
            MovesUsed++;

            int from = Board.Shape.IndexOf(move.From);
            if (move.Kind == MoveKind.Tap)
            {
                _resolver.ResolveTap(from);
            }
            else
            {
                _resolver.ResolveSwap(from, Board.Shape.IndexOf(move.To));
            }

            FinishAction();
            _actions?.Add(new AttemptAction.MovePlayed(move));
            return true;
        }

        /// <summary>
        /// Uses a booster. It costs no move. A booster that would change nothing (a strike on cells with nothing to hit,
        /// a shuffle of a board that cannot be rearranged) is rejected and the board stays as it was. A rejected shuffle
        /// has still drawn from the shuffle stream, so asking again may find an arrangement the first try missed.
        /// </summary>
        /// <exception cref="InvalidOperationException">The game is already over.</exception>
        public bool TryUseBooster(BoosterUse booster)
        {
            if (booster is null)
            {
                throw new ArgumentNullException(nameof(booster));
            }

            if (Status.IsOver())
            {
                throw new InvalidOperationException($"The game is over ({Status}).");
            }

            _moveStats.Reset();
            _events.Clear();
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.BoosterUsed(booster));
            }

            bool changedTheBoard = booster is BoosterUse.Shuffle
                ? _shuffler.TryRearrangeOrLeave()
                : TryStrike(CellsStruckBy(booster));
            if (!changedTheBoard)
            {
                if (booster is BoosterUse.Shuffle)
                {
                    // The shuffle drew from the shuffle stream before it gave up: a replay has to ask for it here too.
                    _actions?.Add(new AttemptAction.ShuffleRefused());
                }

                return false;
            }

            _boostersUsed = Appended(_boostersUsed, BoosterNames.Of(booster));
            FinishAction();
            _actions?.Add(new AttemptAction.BoosterUsed(booster));
            return true;
        }

        /// <summary>
        /// Takes the "+5 moves" offer (docs/GAME_DESIGN.md §19): a game that ran out of moves goes on with
        /// <see cref="ExtraMovesOffer.Moves"/> more. Once per attempt. A client that records attempts writes the record
        /// only once the player has turned the offer down or the attempt has ended for good.
        /// </summary>
        /// <returns>
        /// False when there is nothing to take, and nothing changed: the game did not run out of moves, the offer was
        /// taken before, or the board has no move left that a shuffle could bring back.
        /// </returns>
        public bool TryTakeExtraMoves()
        {
            if (Status != GameStatus.LostOutOfMoves || ExtraMovesTaken)
            {
                return false;
            }

            _moveStats.Reset();
            _events.Clear();

            // The last move ended the game before anyone asked whether the board still had a move in it. A lookahead
            // copy never asks (see NextStatus); any other game does not sell moves nobody could play.
            if (_mode != Mode.Lookahead && !_moves.HasAny() && !_shuffler.TryShuffleOrLeave(Level.Spawn.Palette))
            {
                // The shuffles it tried drew from the shuffle stream: a replay has to ask for the moves here too.
                _actions?.Add(new AttemptAction.ExtraMovesRefused());
                return false;
            }

            _bonusMoves += ExtraMovesOffer.Moves;
            ExtraMovesTaken = true;
            FinishAction();
            _actions?.Add(new AttemptAction.ExtraMovesTaken());
            return true;
        }

        /// <summary>
        /// The celebration after a win (docs/GAME_DESIGN.md §19): every move left turns a free gem into a rocket that
        /// fires. It runs only when asked for and changes neither the result, the moves used, the totals nor the goals'
        /// counts, so no measurement and no attempt record sees it. Its events are in <see cref="LastEvents"/>;
        /// <see cref="MovesLeft"/> still tells what was left.
        /// </summary>
        /// <exception cref="InvalidOperationException">The game is not won, or its finale has already run.</exception>
        public FinaleOutcome PlayFinale()
        {
            if (Status != GameStatus.Won)
            {
                throw new InvalidOperationException($"Only a won game has a finale ({Status}).");
            }

            if (FinalePlayed)
            {
                throw new InvalidOperationException("The finale has already run.");
            }

            FinalePlayed = true;
            _moveStats.Reset();
            _events.Clear();
            GoalTracker reached = Goals.Clone();
            int rockets = _resolver.ResolveFinale(MovesLeft, new FreeGems(Board), _effectRng);
            Goals.RestoreFrom(reached);
            _lastEvents = _events.Snapshot();
            return new FinaleOutcome(rockets, _moveStats.ToSnapshot());
        }

        /// <summary>Puts the start boosters' specials on the freshly filled board and adds their moves.</summary>
        /// <exception cref="LevelInitializationException">The board has too few free gems for the specials.</exception>
        private void TakeStartBoosters(ulong playSeed)
        {
            IReadOnlyList<StartBooster> boosters = _options.StartBoosters;
            if (boosters.Count == 0)
            {
                return;
            }

            // A stream of its own: the board and everything that falls later are the same with and without boosters.
            var rng = new Rng(Rng.Derive(playSeed, StartBoosterStream));
            var gems = new FreeGems(Board);
            gems.Collect();
            foreach (StartBooster booster in boosters)
            {
                switch (booster)
                {
                    case StartBooster.RocketAndBomb:
                        PlaceOnFreeGem(rng.NextInt(2) == 0 ? TileKind.RocketH : TileKind.RocketV, gems, rng);
                        PlaceOnFreeGem(TileKind.Bomb, gems, rng);
                        break;
                    case StartBooster.ColorBomb:
                        PlaceOnFreeGem(TileKind.ColorBomb, gems, rng);
                        break;
                    case StartBooster.ExtraMoves:
                        _bonusMoves += _options.Rules.StartBoosterExtraMoves;
                        break;
                    default:
                        throw new InvalidOperationException($"Unhandled start booster {booster}.");
                }

                _boostersUsed = Appended(_boostersUsed, BoosterNames.Of(booster));
            }

            _lastEvents = _events.Snapshot();
        }

        private void PlaceOnFreeGem(TileKind special, FreeGems gems, Rng rng)
        {
            if (gems.Count == 0)
            {
                throw new LevelInitializationException($"The starting board has no free gem left to put a {special} on.");
            }

            int cell = gems.Take(rng);
            Board.Set(cell, Tile.Special(special));
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.SpecialCreated(Board.Shape.PositionOf(cell), special));
            }
        }

        /// <summary>The cells a striking booster hits; none when it aims outside the board.</summary>
        private List<int> CellsStruckBy(BoosterUse booster)
        {
            BoardShape shape = Board.Shape;
            var cells = new List<int>();
            switch (booster)
            {
                case BoosterUse.HitCell cell:
                    AddIfPlayable(cells, cell.At);
                    break;
                case BoosterUse.HitRow row:
                    for (int x = 0; x < shape.Width && shape.IsPlayable(row.At); x++)
                    {
                        AddIfPlayable(cells, new Position(x, row.At.Y));
                    }

                    break;
                case BoosterUse.HitColumn column:
                    for (int y = 0; y < shape.Height && shape.IsPlayable(column.At); y++)
                    {
                        AddIfPlayable(cells, new Position(column.At.X, y));
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unhandled booster {booster.GetType().Name}.");
            }

            return cells;
        }

        private void AddIfPlayable(List<int> cells, Position position)
        {
            if (Board.Shape.IsPlayable(position))
            {
                cells.Add(Board.Shape.IndexOf(position));
            }
        }

        /// <summary>Strikes the cells unless none of them has anything a hit would change.</summary>
        private bool TryStrike(List<int> cells)
        {
            if (!cells.Exists(HasSomethingToHit))
            {
                return false;
            }

            _resolver.ResolveStrike(cells);
            return true;
        }

        /// <summary>Whether a hit changes the cell: a chain, a jelly layer, or any tile but an item (a hit never harms an item).</summary>
        private bool HasSomethingToHit(int cell)
        {
            TileKind kind = Board.Get(cell).Kind;
            return Board.Chain(cell) > 0 || Board.Jelly(cell) > 0 || (kind != TileKind.Empty && kind != TileKind.Item);
        }

        /// <summary>Closes an action that changed the board: the game's new status, the totals, and what the view is told.</summary>
        private void FinishAction()
        {
            Status = NextStatus();
            _moveStats.AddTo(_totalStats);
            LastMove = _moveStats.ToSnapshot();
            _lastEvents = _events.Snapshot();
        }

        /// <summary>Hands a fresh copy what <paramref name="source"/> has reached; board, goals and randomness came through the constructor.</summary>
        private Game ContinuingFrom(Game source)
        {
            Status = source.Status;
            MovesUsed = source.MovesUsed;
            _bonusMoves = source._bonusMoves;
            _boostersUsed = source._boostersUsed;
            ExtraMovesTaken = source.ExtraMovesTaken;
            FinalePlayed = source.FinalePlayed;
            return this;
        }

        private static string[] Appended(string[] names, string name)
        {
            var longer = new string[names.Length + 1];
            names.CopyTo(longer, 0);
            longer[names.Length] = name;
            return longer;
        }

        private GameStatus NextStatus()
        {
            if (Goals.AllComplete)
            {
                return GameStatus.Won;
            }

            if (HasMoveLimit && MovesUsed >= MoveLimit)
            {
                return GameStatus.LostOutOfMoves;
            }

            if (_mode == Mode.Lookahead || _moves.HasAny())
            {
                return GameStatus.Playing;
            }

            return _shuffler.TryShuffle(Level.Spawn.Palette) ? GameStatus.Playing : GameStatus.LostNoMovesPossible;
        }
    }
}
