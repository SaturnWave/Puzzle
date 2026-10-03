using System;
using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Engine.Resolution;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine
{
    /// <summary>
    /// One play-through of a level. The single entry point shared by the Unity client, the test bot and the
    /// level generator, so all three run exactly the same rules.
    /// </summary>
    /// <remarks>
    /// Every random decision comes from streams derived from the seed given to <see cref="Start"/>: the same
    /// seed and the same moves always produce the same game.
    /// </remarks>
    public sealed class Game
    {
        private const ulong InitialStream = 1;
        private const ulong SpawnStream = 2;
        private const ulong EffectStream = 3;
        private const ulong ShuffleStream = 4;

        private readonly GameOptions _options;
        private readonly Mode _mode;
        private readonly ItemLedger _items;
        private readonly ITileSource _tiles;
        private readonly Rng _effectRng;
        private readonly Rng _shuffleRng;
        private readonly StatsCounter _moveStats = new StatsCounter();
        private readonly StatsCounter _totalStats;
        private readonly EventLog _events;
        private readonly MatchFinder _matches;
        private readonly MoveFinder _moves;
        private readonly TurnResolver _resolver;
        private readonly Shuffler _shuffler;
        private IReadOnlyList<GameEvent> _lastEvents = Array.Empty<GameEvent>();

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

        public int MoveLimit => _options.MoveLimitOverride ?? Level.MoveLimit;

        public int MovesLeft => Math.Max(0, MoveLimit - MovesUsed);

        /// <summary>What the most recent move did.</summary>
        public PlayStats LastMove { get; private set; } = PlayStats.None;

        /// <summary>Events of the most recent move, in order; empty unless <see cref="GameOptions.RecordEvents"/> is on.</summary>
        public IReadOnlyList<GameEvent> LastEvents => _lastEvents;

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
                _totalStats.Clone())
            {
                Status = Status,
                MovesUsed = MovesUsed,
            };

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
                Status = Status,
                MovesUsed = MovesUsed,
                LastMove = LastMove,
            };

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
                _totalStats.Clone())
            {
                Status = Status,
                MovesUsed = MovesUsed,
            };

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

            Status = NextStatus();
            _moveStats.AddTo(_totalStats);
            LastMove = _moveStats.ToSnapshot();
            _lastEvents = _events.Snapshot();
            return true;
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
