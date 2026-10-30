#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Meta;

namespace Puzzle.Client.Play
{
    /// <summary>One thing the card of a booster does on its tiny board. A closed set (private constructor).</summary>
    public abstract class ShowcaseAct
    {
        private ShowcaseAct()
        {
        }

        /// <summary>A booster used during play is used: one that strikes is aimed first, where the finger would aim it; the shuffle at once.</summary>
        public sealed class UseTheBooster : ShowcaseAct
        {
            public UseTheBooster(BoosterUse use)
            {
                Use = use ?? throw new ArgumentNullException(nameof(use));
            }

            public BoosterUse Use { get; }

            public override string ToString() => $"use {Use}";
        }

        /// <summary>A start booster's special is set off: tapped, or traded with a neighbouring gem.</summary>
        public sealed class Play : ShowcaseAct
        {
            public Play(Move move)
            {
                Move = move;
            }

            public Move Move { get; }

            public override string ToString() => $"play {Move}";
        }

        /// <summary>The start booster of extra moves: the moves medallion counts this many up.</summary>
        public sealed class CountTheMovesUp : ShowcaseAct
        {
            public CountTheMovesUp(int from, int moves)
            {
                From = from;
                Moves = moves > 0 ? moves : throw new ArgumentOutOfRangeException(nameof(moves), moves, "The medallion counts one move up at least.");
            }

            /// <summary>The level's own moves, which the medallion shows first.</summary>
            public int From { get; }

            public int Moves { get; }

            public override string ToString() => $"count {From} up by {Moves}";
        }
    }

    /// <summary>
    /// What the card a long press on a booster opens plays over and over (#7, the owner's decision of 2026-10-19): the
    /// booster's effect, shown by the game's own engine on a tiny scripted board of 5 by 5 cells and four colours.
    /// Nothing of it is a picture made beforehand: the card lays the board out and plays the engine's events as a
    /// board's turn is played. The spatula takes away the crate in the middle; a brush paints the middle row or column;
    /// the shuffle deals the board anew; the brush and splash, and the rainbow palette, lie on the board as it opens and
    /// are set off (the brush tapped, then the splash, the palette traded with a neighbour); the extra moves are counted
    /// up on the medallion. The same booster's board is the same every time. No Unity dependency: the repository's
    /// tests compile it too.
    /// </summary>
    public sealed class BoosterShowcase
    {
        /// <summary>The tiny board is this many cells across and down.</summary>
        public const int Cells = 5;

        /// <summary>The level number the tiny boards are known by; no player ever stands on it.</summary>
        private const int ShowcaseLevel = 9999;

        /// <summary>The cell every booster that strikes is aimed at: the board's middle.</summary>
        private static readonly Position Middle = new Position(Cells / 2, Cells / 2);

        private readonly IReadOnlyList<StartBooster> _startBoosters;
        private readonly AttemptSeeds _seeds;

        private BoosterShowcase(BoosterKind booster, LevelDefinition level, ulong seed, IReadOnlyList<StartBooster> startBoosters)
        {
            Booster = booster;
            Level = level;
            _seeds = AttemptSeeds.From(seed);
            _startBoosters = startBoosters;
        }

        /// <summary>The booster the card shows.</summary>
        public BoosterKind Booster { get; }

        /// <summary>The tiny board's level: its cells, the crate of the spatula's, four colours.</summary>
        public LevelDefinition Level { get; }

        /// <summary>The card of a booster: any of the seven, used during play or taken before a level.</summary>
        public static BoosterShowcase Of(BoosterKind booster) => booster switch
        {
            BoosterKind.HitCell => new BoosterShowcase(booster, LevelOf(withACrateInTheMiddle: true), 7101, Array.Empty<StartBooster>()),
            BoosterKind.HitRow => new BoosterShowcase(booster, LevelOf(withACrateInTheMiddle: false), 7102, Array.Empty<StartBooster>()),
            BoosterKind.HitColumn => new BoosterShowcase(booster, LevelOf(withACrateInTheMiddle: false), 7103, Array.Empty<StartBooster>()),
            BoosterKind.Shuffle => new BoosterShowcase(booster, LevelOf(withACrateInTheMiddle: false), 7104, Array.Empty<StartBooster>()),
            _ => new BoosterShowcase(booster, LevelOf(withACrateInTheMiddle: false), 7105 + (ulong)booster, new[] { booster.AsStartBooster() }),
        };

        /// <summary>
        /// The tiny board as it opens, the start booster's special on it: a game that records its events, which the
        /// card plays. A new one each time the card plays the booster again, always the same.
        /// </summary>
        public Game Start() =>
            Game.Start(Level, _seeds, new GameOptions
            {
                RecordEvents = true,
                StartBoosters = _startBoosters,
                ShuffleBoosterRule = ShuffleBoosterRule.LaidWithoutAMatch,
            });

        /// <summary>What the card does next on the game as it stands, having done so many acts already; nothing once the booster has been shown.</summary>
        public ShowcaseAct? NextAct(Game game, int done) => (Booster, done) switch
        {
            (BoosterKind.HitCell, 0) => new ShowcaseAct.UseTheBooster(new BoosterUse.HitCell(Middle)),
            (BoosterKind.HitRow, 0) => new ShowcaseAct.UseTheBooster(new BoosterUse.HitRow(Middle)),
            (BoosterKind.HitColumn, 0) => new ShowcaseAct.UseTheBooster(new BoosterUse.HitColumn(Middle)),
            (BoosterKind.Shuffle, 0) => new ShowcaseAct.UseTheBooster(new BoosterUse.Shuffle()),
            (BoosterKind.StartRocketAndBomb, 0) => TapThe(game, IsARocket),
            (BoosterKind.StartRocketAndBomb, 1) => TapThe(game, kind => kind == TileKind.Bomb),
            (BoosterKind.StartColorBomb, 0) => TradeTheColourBomb(game),
            (BoosterKind.StartExtraMoves, 0) => new ShowcaseAct.CountTheMovesUp(game.Level.MoveLimit, game.MoveLimit - game.Level.MoveLimit),
            _ => null,
        };

        private static bool IsARocket(TileKind kind) => kind == TileKind.RocketH || kind == TileKind.RocketV;

        /// <summary>The special of a kind is tapped where it lies; nothing when none lies on the board any more (another set it off).</summary>
        private static ShowcaseAct? TapThe(Game game, Func<TileKind, bool> isIt) =>
            CellsOf(game.Board).Where(cell => isIt(game.Board.TileAt(cell).Kind)).Select(cell => (ShowcaseAct?)new ShowcaseAct.Play(Move.Tap(cell))).FirstOrDefault();

        /// <summary>The colour bomb is traded with its first neighbour (right, left, down, up) the engine takes the trade of.</summary>
        private static ShowcaseAct? TradeTheColourBomb(Game game)
        {
            Position? bomb = CellsOf(game.Board).Where(cell => game.Board.TileAt(cell).Kind == TileKind.ColorBomb).Select(cell => (Position?)cell).FirstOrDefault();
            if (!(bomb is Position at))
            {
                return null;
            }

            return new[] { new Position(at.X + 1, at.Y), new Position(at.X - 1, at.Y), new Position(at.X, at.Y + 1), new Position(at.X, at.Y - 1) }
                .Select(neighbour => Move.Swap(at, neighbour))
                .Where(game.IsLegal)
                .Select(trade => (ShowcaseAct?)new ShowcaseAct.Play(trade))
                .FirstOrDefault();
        }

        private static IEnumerable<Position> CellsOf(Board board) =>
            Enumerable.Range(0, board.Height).SelectMany(y => Enumerable.Range(0, board.Width).Select(x => new Position(x, y)));

        /// <summary>The tiny board: every cell playable, every gem of four colours chosen by its seed, a crate in the middle for the spatula.</summary>
        private static LevelDefinition LevelOf(bool withACrateInTheMiddle)
        {
            string row = new string(TileGlyphs.Playable, Cells);
            string random = new string(TileGlyphs.RandomGem, Cells);
            string[] tiles = Enumerable.Repeat(random, Cells).ToArray();
            if (withACrateInTheMiddle)
            {
                tiles[Middle.Y] = random.Substring(0, Middle.X) + "1" + random.Substring(Middle.X + 1);
            }

            LevelLayout layout = LevelLayout.Parse(new LayoutLayers { Cells = Enumerable.Repeat(row, Cells).ToArray(), Tiles = tiles });
            return new LevelDefinition(
                new LevelId(ShowcaseLevel),
                DifficultyTier.Normal,
                layout,
                new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 99) },
                moveLimit: 20,
                new SpawnRules { Palette = GemColors.First(4), Items = ItemSpawnRules.None });
        }
    }
}
