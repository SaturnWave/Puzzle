using Puzzle.Cli;
using Puzzle.Cli.Commands;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Generator.Profiles;
using Puzzle.Tooling.Ladder;

const string Usage = """
    puzzle <command> [options]

      sim <level.json>      Play the level many times with a bot and report the move budget it needs.
                            --bot random|novice|casual|average|expert|all (average)  --runs 300  --cap 150  --seed 1
                            --bot skill-<n>  a rung of the skill ladder, n = 30, 35 .. 100: it notices n % of the moves
                                             (skill-50 = novice, skill-70 = casual, skill-85 = average, skill-100 = expert)
                            --bot usta|usta-<n>  the fair search bot (n simulations per move, default 24); slow, for measurements
      demo <level.json>     Print one bot game move by move.   --bot average|usta|usta-<n>  --seed 1  --moves N
      bench <level.json>    Engine, greedy bot and usta throughput.   --runs 200
      solve <level.json>    Perfect-information search vs. bots on the same boards.  --seeds 8  --beam 8  --cap 150  --prove 4
      seeds <level.json>    Measure seed difficulty spread and build the equal-difficulty retry pool.
                            --board varied|fixed  --board-seed N  --target 0.6  --candidates 64  --games 64  --pool 16  --tolerance 5  --write
      generate              Produce bot-calibrated levels from a generation profile.
                            --profile profiles/default.json  --from 1  --to 20  --out levels/generated  --quick  --overwrite  --repair
      botstats <dir|file>   Moves one bot needs per level, as CSV.  --bot expert|usta|usta-<n>  --runs 30  --cap 150  --out reports/<bot>_moves.csv
      ladder <dir|file>     Existing levels against the skill ladder, every bot of a level on the same fresh games, as CSV:
                            win rate at the move limit, moves needed and the limit each win rate would take, per level and bot.
                            --from 1  --to <last>  --from-skill 30  --to-skill 100  --step 5  --runs 60  --cap 150  --seed 7
                            --out reports/ladder.csv   A run replaces the rows of its own levels there and keeps the others.
      validate <dir|file>   Parse every level file and build a starting board for it.
      humans [attempts.jsonl]
                            Web lab attempts of people vs. the greedy bots on the same games, as CSV.
                            Default file: recordings/attempts.jsonl in the repository root, where the lab records.
                            --levels <folder> (default: levels in the repository root, as the lab)  --runs 30  --out reports/humans.csv
      players add <username>
                            Make a public-mode account (the server registers nobody). The password is asked for here,
                            hidden and twice; never give it as an argument. Stop the public server first.
      players password <username>
                            Give a player a new password, asked for like the first one. Their progress and records
                            stay; every browser signed in with the old password is signed out. Stop the public server first.
      players list          The public mode's accounts: username, player id, progress and recorded attempts.
      players delete <username>
                            Delete a player on request: the account, its progress and every attempt record of theirs.
                            Stop the public server first. All four: --data <folder> (default: data in the repository
                            root); list and delete: --records <attempts.jsonl> (default: recordings/attempts.jsonl there)
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
        "botstats" => BotStatsCommand.Run(cli),
        "ladder" => LadderCommand.Run(cli),
        "validate" => ValidateCommand.Run(cli),
        "humans" => HumansCommand.Run(cli),
        "players" => PlayersCommand.Run(cli),
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
catch (LadderReportFormatException ex)
{
    Console.Error.WriteLine($"Not a ladder report: {ex.Message}");
    return 1;
}

static int PrintUsage()
{
    Console.WriteLine(Usage);
    return 0;
}
