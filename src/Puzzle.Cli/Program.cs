using Puzzle.Bank.Assembling;
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
      bank grow             Grow the level bank: candidate boards that belong to no level number, each stored with the
                            games the whole skill ladder played on it, drawn where the bank is thinnest for the plan.
                            --bank <folder> (or the environment variable PUZZLE_BANK; outside the repository)
                            --policy policies/default.json (or --plan profiles/default.json: the plan's own difficulty)
                            --levels 51-1000  --minutes N | --candidates N
                            --also-aim skill-45,skill-65  --spare 0.25  --siblings 0  --seed N (new bank)  --accept-damage
                            Depth is set by --spare (spare boards per level of a class). A run that ends before its
                            budget has made the bank as deep as that asks or as draws can make it, and says which.
                            Stop with Ctrl+C or let the budget run out; the same command goes on where it stopped.
      bank remeasure        After a change of the engine or the bot: play the ladder games of the boards the bank holds
                            again under the new rules (the boards behind --in-use levels first). Resumable.
                            --bank <folder>  --in-use levels/generated  --minutes N  --candidates N
                            --new-epoch  opens the epoch of this build when the bank has none for it; where the build
                            already works in an epoch, name it (--new-epoch <epoch id>): its games stop counting.
      bank status           What the bank holds; with --plan and --levels, what each class of levels still lacks.
                            --bank <folder>  --policy ... | --plan ...  --levels ...  --also-aim ...  --spare 0.25  --out status.csv
                            --draws draws.csv (every variant the grower tried: its target, probe limit and outcome)
      bank verify           Check every record of the bank and replay stored games.   --bank <folder>  --candidates 24
      bank import <dir|file>
                            Take existing levels into the bank, each measured on the whole ladder like a drawn board,
                            so a level can keep its board under another policy.   --bank <folder>  --from N  --to N
                            A level held from before a rule change gets its ladder games again.
      assemble              Make the levels a difficulty policy asks for: pinned levels and levels that still stand are
                            copied byte for byte; every other number gets a bank candidate, its limit and seed pool read
                            from stored games and checked on fresh ones. Writes level files, report.csv, assembly.csv and
                            assembly.manifest into a staging folder (replaced whole), never into the base folder.
                            A level stands only while policy, plan, engine rules and checks are what it was decided
                            under, the plan admits its board and its limit lies in its range. The summary names every
                            level that does not meet the policy and every kind that missed its target.
                            --policy policies/default.json  --base levels/generated  --out out/assemble/<policy name>
                            --bank <folder> (or PUZZLE_BANK)  --from 1  --to <last>  --accept-damage
                            --recordings recordings/attempts.jsonl | none
                            Seconds: a repeat, or a run in which nothing changed. Minutes: other shares for some tiers.
                            Another player is as slow as a regeneration the first time (every level's confirm and
                            audit games are new); what it saves is the boards, not the time.
      promote <staging folder>
                            Take an assembled set live, with the test server stopped: checks the folder against its
                            manifest, that every pinned or kept level is the live file, and that every level short of
                            the policy was accepted by number; archives replaced files that have recordings, backs up
                            what it replaces, then copies levels, report, assembly.csv and manifest.
                            --live levels/generated  --accept 52,53  --dry-run  --recordings <file> | none
                            --archive levels/archive/before-<policy>-<date>  --backup out/promote/<time>
                            --server-process Puzzle.Web | none
      compare <folder> <other folder>
                            Two folders of levels side by side: limits and shares per tier, the mix of level kinds, and
                            how many numbers changed limit, amounts or board.   --from N  --to N  --out changes.csv
      patch make            The next patch pack for a released game: every level that ever got a correction, whole,
                            at its latest content version; a level that differs from its latest published content
                            gets the next version. One pack serves every release, and the newest alone is enough.
                            --shipped <folder of a release>  --current levels/generated  --out pack.json
                            --previous <pack.json> | --first   the pack published before, or that there is none:
                            without it content versions that are taken would be given out again
      patch apply <pack.json>
                            The levels a player of a release has with the pack, as level files with their content
                            versions; also how the level folder of the next release is made.
                            --shipped <folder of a release>  --out <folder>
      botstats <dir|file>   Moves one bot needs per level, as CSV.  --bot expert|usta|usta-<n>  --runs 30  --cap 150  --out reports/<bot>_moves.csv
      ladder <dir|file>     Existing levels against the skill ladder, every bot of a level on the same fresh games, as CSV:
                            win rate at the move limit, moves needed and the limit each win rate would take, per level and bot.
                            --from 1  --to <last>  --from-skill 30  --to-skill 100  --step 5  --runs 60  --cap 150  --seed 7
                            --games fresh|attempts (attempts: each level's retry pool in turn, as a player meets it)
                            --out reports/ladder.csv   A run replaces the rows of its own levels there and keeps the others.
      hints <dir|file>      What the game's hints do to a player model's win rate at the move limits: every attempt
                            played without hints and with each hint bot's hints followed, on the levels' own seeds.
                            --levels 1-250,260-1000/10  --player skill-55  --hint-bots novice,casual,average
                            --moment idle|stuck (idle: a chance before every move, --idle-percent 50; stuck: the
                            player's own move counts nothing for the goals)  --per-attempt 3  --attempts 8  --seed 7
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
      economy measure [settings.json]
                            The economy simulator's measurement: bots of the settings' rungs play every level on its own
                            pool seeds past its limit, plain and with each board booster; what each game needed is kept.
                            A stopped run goes on where it stopped; unchanged levels are not played again.
                            --levels levels/generated  --from 1  --to <last>  --out out/economy/measurement.jsonl
                            --workers N  --restart (discard a measurement made with other settings or another engine)
                            --low-priority (let everything else on the machine go first)
      economy run [settings.json]
                            Simulate the settings' free players from the measurement alone, through days, sessions,
                            lives, coins and boosters: days to the milestone levels, where players stall, what lives
                            cost, the coin balance, what a free player affords. Writes pace, stalls, lives, coins,
                            spending, boosters, offers and settings as CSV and summary.txt (also printed).
                            --measurement out/economy/measurement.jsonl  --levels levels/generated
                            --out out/economy/<settings name>  --trust-measurement (skip the check against the engine
                            and the level files; then no game is played at all)
      economy compare <a.json> <b.json>
                            Two settings files on the same measurement: the fields that differ, then the figures up to
                            each milestone side by side, as compare.csv and differences.txt.
                            --measurement ...  --levels ...  --out out/economy/compare-<a>-vs-<b>  --trust-measurement
                            All three: the settings default to economy/default.json in the repository root; every
                            economy value lives in the settings file, never in an option. --restart, --low-priority
                            and --trust-measurement take no value: put the settings file before the options.
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
        "bank" => BankCommand.Run(cli),
        "assemble" => AssembleCommand.Run(cli),
        "compare" => AssembleCommand.Compare(cli),
        "promote" => PromoteCommand.Run(cli),
        "patch" => PatchCommand.Run(cli),
        "botstats" => BotStatsCommand.Run(cli),
        "ladder" => LadderCommand.Run(cli),
        "hints" => HintsCommand.Run(cli),
        "validate" => ValidateCommand.Run(cli),
        "humans" => HumansCommand.Run(cli),
        "players" => PlayersCommand.Run(cli),
        "economy" => EconomyCommand.Run(cli),
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
catch (PolicyFormatException ex)
{
    Console.Error.WriteLine(ex.Message);
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
