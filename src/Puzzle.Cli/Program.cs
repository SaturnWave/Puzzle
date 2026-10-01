using Puzzle.Cli;
using Puzzle.Cli.Commands;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Generator.Profiles;

const string Usage = """
    puzzle <command> [options]

      sim <level.json>      Play the level many times with a bot and report the move budget it needs.
                            --bot random|novice|casual|average|expert|all (average)  --runs 300  --cap 150  --seed 1
      demo <level.json>     Print one bot game move by move.   --bot average  --seed 1  --moves N
      bench <level.json>    Engine and bot throughput.          --runs 200
      solve <level.json>    Perfect-information search vs. bots on the same boards.  --seeds 8  --beam 8  --cap 150  --prove 4
      seeds <level.json>    Measure seed difficulty spread and build the equal-difficulty retry pool.
                            --board varied|fixed  --board-seed N  --target 0.6  --candidates 64  --games 64  --pool 16  --tolerance 5  --write
      generate              Produce bot-calibrated levels from a generation profile.
                            --profile profiles/default.json  --from 1  --to 20  --out levels/generated  --quick  --overwrite  --repair
      validate <dir|file>   Parse every level file and build a starting board for it.
    """;

CliArgs cli = CliArgs.Parse(args);
try
{
    return cli.Command switch
    {
        "sim" => SimCommand.Run(cli),
        "demo" => DemoCommand.Run(cli),
        "bench" => BenchCommand.Run(cli),
        "solve" => SolveCommand.Run(cli),
        "seeds" => SeedsCommand.Run(cli),
        "generate" => GenerateCommand.Run(cli),
        "validate" => ValidateCommand.Run(cli),
        "help" or "--help" or "-h" => PrintUsage(),
        _ => throw new UsageException($"Unknown command '{cli.Command}'."),
    };
}
catch (UsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(Usage);
    return 2;
}
catch (LevelFormatException ex)
{
    Console.Error.WriteLine($"Invalid level: {ex.Message}");
    return 1;
}
catch (LevelInitializationException ex)
{
    Console.Error.WriteLine($"Unplayable level: {ex.Message}");
    return 1;
}
catch (ProfileFormatException ex)
{
    Console.Error.WriteLine($"Invalid profile: {ex.Message}");
    return 1;
}

static int PrintUsage()
{
    Console.WriteLine(Usage);
    return 0;
}
