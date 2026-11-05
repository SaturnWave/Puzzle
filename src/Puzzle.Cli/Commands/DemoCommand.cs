using Puzzle.Bot;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary><c>demo &lt;level.json&gt; [--bot average] [--seed 1] [--moves N]</c>: one game, printed move by move.</summary>
internal static class DemoCommand
{
    public static int Run(CliArgs args)
    {
        LevelDefinition level = LevelJson.Load(args.Required(0, "level.json"));
        ulong seed = args.ULong("seed", 1);
        IBotPolicy policy = SimCommand.CreatePolicy(args.Text("bot", "average"));
        int maxMoves = args.Int("moves", level.MoveLimit);

        Game game = Game.Start(level, seed, new GameOptions { RecordEvents = true, MoveLimitOverride = maxMoves });
        var rng = new Rng(Rng.Derive(seed, 2));
        Console.WriteLine(SimCommand.Describe(level));
        Console.WriteLine(BoardRenderer.Render(game));

        while (game.Status == GameStatus.Playing)
        {
            Move move = policy.ChooseMove(game, game.FindLegalMoves(), rng);
            game.TryPlay(move);
            PlayStats stats = game.LastMove;
            Console.WriteLine($"#{game.MovesUsed} {move}  cleared {stats.GemsCleared}, specials +{stats.SpecialsCreated}/fired {stats.SpecialsActivated}, cascades {stats.Cascades}, events {game.LastEvents.Count}{(stats.Shuffles > 0 ? ", SHUFFLED" : string.Empty)}");
            Console.WriteLine($"   goals: {BoardRenderer.Goals(game)}");
            Console.WriteLine(BoardRenderer.Render(game));
        }

        Console.WriteLine($"Result: {game.Status} after {game.MovesUsed} moves.");
        Console.WriteLine("Legend: R G B Y P O C K gems, - | rockets, * bomb, % big bomb, + seeker, @ color bomb, 1-3 box, $ item, [x] chained, ' \" jelly");
        return 0;
    }
}
