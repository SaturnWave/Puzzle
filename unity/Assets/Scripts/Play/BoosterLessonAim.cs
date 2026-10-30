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
    /// <summary>
    /// Where the tutorial aims a booster used during play that it shows by hand (#7, the owner's decision of
    /// 2026-10-19): the use that is worth the most to the level's goals as the board stands. Every candidate is tried
    /// on a lookahead copy of the game (Game.CreateLookahead: refills come hidden and never match, and its streams are
    /// seeded apart, so the game itself and its random streams are never touched); one the engine refuses is never
    /// chosen. What a use is worth is how far it brings the goals on, every goal's count added up, cascades of what
    /// was already on the board included. The candidates:
    /// <list type="bullet">
    /// <item>the spatula: every cell that holds an obstacle (a crate, a piece under tape, varnish under a piece) or a
    /// gem of a colour a goal collects; where the engine takes none of them, every cell. Of equal worth the obstacle
    /// goes first, then the cell nearest the board's middle, then the first in reading order;</item>
    /// <item>a brush: every row (or column) that has a playable cell, aimed at its playable cell nearest the board's
    /// middle. Of equal worth the line nearest the middle goes first, then the upper (or the left) one;</item>
    /// <item>the shuffle: the shuffle itself, when the engine would deal the board anew (asked on an exact copy).</item>
    /// </list>
    /// No Unity dependency: the repository's tests compile it too.
    /// </summary>
    public static class BoosterLessonAim
    {
        /// <summary>The use the tutorial shows by hand on the game as it stands; nothing when the engine would take none.</summary>
        /// <param name="seed">Seeds the lookahead copies (the attempt's play seed): the same board always gets the same aim.</param>
        /// <exception cref="ArgumentException">The booster is taken before a level: it has no aim on a board.</exception>
        public static BoosterUse? For(Game game, BoosterKind booster, ulong seed) => booster switch
        {
            BoosterKind.HitCell => Best(game, seed, SpatulaCandidates(game)) ?? Best(game, seed, EveryCell(game)),
            BoosterKind.HitRow => Best(game, seed, Lines(game.Board, (row, x) => new Position(x, row), game.Board.Width, game.Board.Height, at => new BoosterUse.HitRow(at))),
            BoosterKind.HitColumn => Best(game, seed, Lines(game.Board, (column, y) => new Position(column, y), game.Board.Height, game.Board.Width, at => new BoosterUse.HitColumn(at))),
            BoosterKind.Shuffle => game.CreateReplica().TryUseBooster(new BoosterUse.Shuffle()) ? new BoosterUse.Shuffle() : null,
            _ => throw new ArgumentException($"{booster.Name()} is taken before a level; nothing aims it on a board.", nameof(booster)),
        };

        /// <summary>How far the goals have come, every goal's count added up, as far as each goal asks.</summary>
        public static int GoalsDone(Game game) => Enumerable.Range(0, game.Goals.Count).Sum(goal => Math.Min(game.Goals[goal].Done, game.Goals[goal].Goal.Target));

        /// <summary>The candidate worth the most that the engine takes, by the candidates' own order where two are worth as much.</summary>
        private static BoosterUse? Best(Game game, ulong seed, IEnumerable<Candidate> candidates)
        {
            int before = GoalsDone(game);
            return candidates
                .Select(candidate => (Candidate: candidate, Worth: WorthOf(game, seed, candidate.Use, before)))
                .Where(tried => tried.Worth is int)
                .OrderByDescending(tried => tried.Worth)
                .ThenBy(tried => tried.Candidate.Rank)
                .Select(tried => tried.Candidate.Use)
                .FirstOrDefault();
        }

        /// <summary>What the use brings the goals on a lookahead copy; nothing when the engine refuses it.</summary>
        private static int? WorthOf(Game game, ulong seed, BoosterUse use, int goalsDoneBefore)
        {
            Game copy = game.CreateLookahead(seed);
            return copy.TryUseBooster(use) ? GoalsDone(copy) - goalsDoneBefore : (int?)null;
        }

        /// <summary>The spatula's candidates: obstacles first, then gems a goal collects, each group nearest the middle first.</summary>
        private static IEnumerable<Candidate> SpatulaCandidates(Game game)
        {
            Board board = game.Board;
            GemColor[] collected = game.Level.Goals.OfType<GoalSpec.CollectColor>().Select(goal => goal.Color).ToArray();
            return PlayableCells(board)
                .Select(cell => (Cell: cell, IsObstacle: IsObstacle(board, cell), IsCollected: board.TileAt(cell).IsGem && collected.Contains(board.TileAt(cell).Color)))
                .Where(each => each.IsObstacle || each.IsCollected)
                .OrderBy(each => each.IsObstacle ? 0 : 1)
                .ThenBy(each => DistanceFromTheMiddle(board, each.Cell))
                .ThenBy(each => ReadingOrder(board, each.Cell))
                .Select((each, rank) => new Candidate(new BoosterUse.HitCell(each.Cell), rank));
        }

        /// <summary>Every playable cell, nearest the middle first: where a level's goals leave the spatula nothing to aim at.</summary>
        private static IEnumerable<Candidate> EveryCell(Game game) =>
            PlayableCells(game.Board)
                .OrderBy(cell => DistanceFromTheMiddle(game.Board, cell))
                .ThenBy(cell => ReadingOrder(game.Board, cell))
                .Select((cell, rank) => new Candidate(new BoosterUse.HitCell(cell), rank));

        /// <summary>
        /// Every row or column that has a playable cell, aimed at its playable cell nearest the middle along it; the
        /// line nearest the board's middle first, then the upper or left one.
        /// </summary>
        /// <param name="cellAt">The cell of a line (its number) at a place along it.</param>
        /// <param name="length">How many cells a line has.</param>
        /// <param name="lines">How many lines the board has.</param>
        /// <param name="useAt">The brush aimed at a cell of the line.</param>
        private static IEnumerable<Candidate> Lines(Board board, Func<int, int, Position> cellAt, int length, int lines, Func<Position, BoosterUse> useAt)
        {
            float middleLine = (lines - 1) / 2f;
            float middleAlong = (length - 1) / 2f;
            return Enumerable.Range(0, lines)
                .Select(line => (Line: line, Aim: Enumerable.Range(0, length)
                    .Where(along => board.Shape.IsPlayable(cellAt(line, along)))
                    .OrderBy(along => Math.Abs(along - middleAlong))
                    .ThenBy(along => along)
                    .Select(along => (Position?)cellAt(line, along))
                    .FirstOrDefault()))
                .Where(each => each.Aim.HasValue)
                .OrderBy(each => Math.Abs(each.Line - middleLine))
                .ThenBy(each => each.Line)
                .Select((each, rank) => new Candidate(useAt(each.Aim!.Value), rank));
        }

        private static bool IsObstacle(Board board, Position cell) =>
            board.TileAt(cell).Kind == TileKind.Box || board.ChainAt(cell) > 0 || board.JellyAt(cell) > 0;

        private static IEnumerable<Position> PlayableCells(Board board) =>
            Enumerable.Range(0, board.Height).SelectMany(y => Enumerable.Range(0, board.Width).Select(x => new Position(x, y))).Where(cell => board.Shape.IsPlayable(cell));

        private static float DistanceFromTheMiddle(Board board, Position cell)
        {
            float dx = cell.X - (board.Width - 1) / 2f;
            float dy = cell.Y - (board.Height - 1) / 2f;
            return dx * dx + dy * dy;
        }

        private static int ReadingOrder(Board board, Position cell) => cell.Y * board.Width + cell.X;

        /// <summary>A use the tutorial may show, and its place among the candidates where two are worth as much: 0 first.</summary>
        private readonly struct Candidate
        {
            public Candidate(BoosterUse use, int rank)
            {
                Use = use;
                Rank = rank;
            }

            public BoosterUse Use { get; }

            public int Rank { get; }
        }
    }
}
