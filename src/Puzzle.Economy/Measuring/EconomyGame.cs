using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Measuring;

/// <summary>
/// The one place of the economy that plays the game. A game is the level's attempt on a pool seed, followed past the
/// level's limit to the move cap, played by a ladder rung: on a plain board it is the very game the level bank plays
/// for that seed and repeat (LadderMeasurer.Play), and a board start only adds the booster's special to it. Neither
/// the start booster's extra moves nor the extra-moves offer is ever played: both are read from the stored game
/// (<see cref="MeasuredGame"/>). Assist is never set.
/// </summary>
public static class EconomyGame
{
    /// <summary>
    /// Plays game <paramref name="repeat"/> of <paramref name="poolSeed"/>. It depends on the level's content, the seed,
    /// the bot's settings, the board start and the repeat, and on nothing else.
    /// </summary>
    /// <param name="bot">A policy of the caller's own thread: policies keep scratch buffers.</param>
    /// <param name="repeat">The bot repeat; the bot's own choices are seeded with <c>Rng.Derive(poolSeed, repeat)</c>.</param>
    /// <exception cref="LevelInitializationException">The level cannot produce a starting board, or its board has no free gem for the start's special.</exception>
    /// <exception cref="InvalidOperationException">The bot chose a move the engine rejects.</exception>
    public static MeasuredGame Play(LevelDefinition level, GreedyPolicy bot, BoardStart start, uint poolSeed, int repeat, MoveCount moveCap, IReadOnlyList<Percent> marks)
    {
        var options = new GameOptions { MoveLimitOverride = moveCap.Value, StartBoosters = start.Boosters() };
        Game game = Game.Start(level, level.Attempts.SeedsForPlaySeed(poolSeed), options);
        var botChoices = new Rng(Rng.Derive(poolSeed, (ulong)repeat));
        var legalMoves = new List<Move>();
        var reachedOn = new MoveCount?[marks.Count];
        NoteMarksReached(game, marks, reachedOn);

        while (game.Status == GameStatus.Playing)
        {
            legalMoves.Clear();
            game.CollectLegalMoves(legalMoves);
            Move move = bot.ChooseMove(game, legalMoves, botChoices);
            if (!game.TryPlay(move))
            {
                throw new InvalidOperationException($"{bot.Name} chose illegal move {move} on level {level.Id}.");
            }

            NoteMarksReached(game, marks, reachedOn);
        }

        return new MeasuredGame(EndOf(game), marks, reachedOn);
    }

    /// <summary>
    /// Fills in the marks the goals' progress has reached by now and had not before. The comparison is the bank's
    /// (RungGames): progress as a single-precision share against the mark as one, so both keep the same moves.
    /// </summary>
    private static void NoteMarksReached(Game game, IReadOnlyList<Percent> marks, MoveCount?[] reachedOn)
    {
        float progress = (float)game.Goals.OverallProgress;
        for (int mark = 0; mark < marks.Count; mark++)
        {
            if (reachedOn[mark] is null && progress >= marks[mark].Value / 100f)
            {
                reachedOn[mark] = new MoveCount(game.MovesUsed);
            }
        }
    }

    private static GameEnd EndOf(Game game) => game.Status switch
    {
        GameStatus.Won => new GameEnd.Won(new MoveCount(game.MovesUsed)),
        GameStatus.LostNoMovesPossible => new GameEnd.Stuck(new MoveCount(game.MovesUsed)),
        GameStatus.LostOutOfMoves => new GameEnd.Unfinished(),
        GameStatus.Playing => throw new InvalidOperationException("A game that is still playing has no end."),
        _ => throw new InvalidOperationException($"Unhandled game status {game.Status}."),
    };
}
