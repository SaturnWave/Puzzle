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
        private readonly bool _isLookahead;
        private readonly ItemLedger _items;
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
            Board board,
            GoalTracker goals,
            ItemLedger items,
            ITileSource tiles,
            ulong randomSeed,
            StatsCounter totalStats,
            bool isLookahead)
        {
            Level = level;
            _options = options;
            Board = board;
            Goals = goals;
            _items = items;
            _totalStats = totalStats;
            _isLookahead = isLookahead;
            _events = options.RecordEvents && !isLookahead ? new EventLog(true) : EventLog.Disabled;

            RuleSet rules = options.Rules;
            var pending = new Queue<Effect>();
            var damage = new DamageResolver(board, goals, _moveStats, _events, pending, rules);
            var effects = new EffectResolver(board, goals, _moveStats, _events, damage, pending, rules, new Rng(Rng.Derive(randomSeed, EffectStream)));
            var gravity = new Gravity(board, tiles, goals, _moveStats, _events, items);
            _matches = new MatchFinder(board);
            _moves = new MoveFinder(board, _matches, rules);
            _resolver = new TurnResolver(board, rules, pending, damage, effects, _matches, gravity, _moveStats, _events);
            _shuffler = new Shuffler(board, _matches, _moves, rules, new Rng(Rng.Derive(randomSeed, ShuffleStream)), _moveStats, _events);
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

        /// <exception cref="LevelInitializationException">The level cannot produce a valid starting board.</exception>
        public static Game Start(LevelDefinition level, ulong seed, GameOptions? options = null)
        {
            options ??= GameOptions.Default;
            var board = new Board(level.Layout.Shape);
            var items = new ItemLedger { Spawned = level.Layout.PresetItemCount };
            var colors = new ColorPicker(level.Spawn.Colors, level.Spawn.SmoothingPermille);
            var tiles = new RandomTileSource(colors, new Rng(Rng.Derive(seed, SpawnStream)), level.Spawn.Items, items);
            var game = new Game(level, options, board, new GoalTracker(level.Goals), items, tiles, seed, new StatsCounter(), isLookahead: false);

            var initializer = new BoardInitializer(board, game._matches, game._moves, options.Rules);
            initializer.Fill(level.Layout, level.Spawn.Colors, new Rng(Rng.Derive(seed, InitialStream)));
            return game;
        }

        /// <summary>
        /// A copy for trying moves without seeing the future: refills arrive as <see cref="TileKind.Hidden"/>
        /// tiles that never match, and other random choices come from <paramref name="seed"/> instead of the
        /// live game's streams. No events, no shuffles.
        /// </summary>
        public Game CreateLookahead(ulong seed)
        {
            var items = _items.Clone();
            return new Game(Level, _options, Board.Clone(), Goals.Clone(), items, HiddenTileSource.Instance, seed, _totalStats.Clone(), isLookahead: true)
            {
                Status = Status,
                MovesUsed = MovesUsed,
            };
        }

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

            if (MovesUsed >= MoveLimit)
            {
                return GameStatus.LostOutOfMoves;
            }

            if (_isLookahead || _moves.HasAny())
            {
                return GameStatus.Playing;
            }

            return _shuffler.TryShuffle(Level.Spawn.Colors) ? GameStatus.Playing : GameStatus.LostNoMovesPossible;
        }
    }
}
