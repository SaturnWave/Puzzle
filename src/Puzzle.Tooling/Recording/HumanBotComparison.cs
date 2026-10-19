using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Hints;

namespace Puzzle.Tooling.Recording;

/// <summary>
/// One bot's win rates at a move limit, each on the games of the attempts the human rate it stands beside covers, so like
/// is compared with like.
/// </summary>
/// <param name="WinRateOnFinished">
/// On the games of the finished attempts, beside <see cref="ComparisonFigures.HumanWinRate"/>; absent when none finished.
/// </param>
/// <param name="WinRateOnAll">On the games of every attempt, abandoned ones too, beside <see cref="ComparisonFigures.HumanWinRateLowerBound"/>.</param>
public sealed record BotWinRate(string Bot, double? WinRateOnFinished, double WinRateOnAll);

/// <summary>
/// Human results next to the bots' win rates on the same games. At its move limit an attempt was won, lost, or — abandoned
/// before the limit was used up — undecided: the player walked away, and how it would have ended is unknown.
/// </summary>
/// <param name="Finished">Attempts decided at the move limit: won or lost.</param>
/// <param name="Abandoned">Attempts left undecided before the move limit.</param>
/// <param name="Wins">Attempts won within the move limit.</param>
/// <param name="Devices">Devices that played the attempts.</param>
/// <param name="Bots">Each profile's win rates, in <see cref="HumanBotReport.Bots"/> order; empty without bots or without attempts.</param>
public sealed record ComparisonFigures(int Finished, int Abandoned, int Wins, int Devices, IReadOnlyList<BotWinRate> Bots)
{
    /// <summary>
    /// Share of the finished attempts won, as if the players who walked away would have done as well as those who stayed.
    /// Walking away mostly hides a loss, so this leans high. Absent when none finished.
    /// </summary>
    public double? HumanWinRate => Finished > 0 ? Wins / (double)Finished : null;

    /// <summary>Share of all attempts won, every abandoned one counted as a loss: the lowest the humans' rate can be. Absent without attempts.</summary>
    public double? HumanWinRateLowerBound => Finished + Abandoned > 0 ? Wins / (double)(Finished + Abandoned) : null;

    /// <summary>
    /// The bot whose win rate on the finished attempts' games is nearest <see cref="HumanWinRate"/> (the weaker one on a
    /// tie). People tend to walk away from bad boards, so the finished games may be easier than all of them.
    /// </summary>
    public string? ClosestBot => NearestBot(HumanWinRate, bot => bot.WinRateOnFinished);

    /// <summary>The bot whose win rate on every attempt's game is nearest <see cref="HumanWinRateLowerBound"/> (the weaker one on a tie).</summary>
    public string? ClosestBotAtLowerBound => NearestBot(HumanWinRateLowerBound, bot => bot.WinRateOnAll);

    /// <summary>Whether the two human rates point at different bots: then the abandoned attempts decide which bot people play like.</summary>
    public bool AbandonsDecideClosest => ClosestBot != ClosestBotAtLowerBound;

    /// <summary>
    /// The human figures of <paramref name="attempts"/>, every one of them comparable (<see cref="AttemptStanding.Comparable"/>),
    /// next to <paramref name="bots"/>; with no bots, the people's figures alone.
    /// </summary>
    public static ComparisonFigures Of(IReadOnlyCollection<AttemptRecord> attempts, IReadOnlyList<BotWinRate> bots)
    {
        LimitVerdict[] verdicts = attempts.Select(AttemptCounting.AtLimit).ToArray();
        int abandoned = verdicts.Count(verdict => verdict == LimitVerdict.Undecided);
        int wins = verdicts.Count(verdict => verdict == LimitVerdict.Won);
        int devices = attempts.Select(attempt => attempt.Start.Device).Distinct().Count();
        return new ComparisonFigures(attempts.Count - abandoned, abandoned, wins, devices, bots);
    }

    private string? NearestBot(double? humans, Func<BotWinRate, double?> botRate)
    {
        if (humans is not double rate)
        {
            return null;
        }

        // MinBy keeps the first of equals, and the bots come weakest first.
        BotWinRate? nearest = Bots
            .Where(bot => botRate(bot) is not null)
            .MinBy(bot => Math.Abs(botRate(bot).GetValueOrDefault() - rate));
        return nearest?.Bot;
    }
}

/// <summary>One level at one move limit; a level whose limit changed between recordings gets one row per limit.</summary>
/// <param name="LevelFingerprint">
/// The version of the level compared (<see cref="LevelJson.Fingerprint"/>): two levels may share an id, a sample and a
/// generated one, say.
/// </param>
public sealed record LevelComparison(LevelId Level, string LevelFingerprint, int MoveLimit, double? MedianWinMoves, ComparisonFigures Figures);

/// <summary>What happened to each record: compared, or left out and why.</summary>
/// <param name="Compared">Records compared with the bots, in either group: without hints and <paramref name="Hinted"/>.</param>
/// <param name="TesterMode">Records of attempts played in the released game's tester mode.</param>
/// <param name="Hinted">Of <paramref name="Compared"/>, the attempts the released game showed hints in, compared apart (<see cref="HintedComparison"/>).</param>
public sealed record RecordTally(int Compared, int Assisted, int ChosenSeeds, int UnknownLevel, int TesterMode, int Hinted = 0)
{
    public int Total => Compared + Assisted + ChosenSeeds + UnknownLevel + TesterMode;
}

/// <summary>
/// The attempts the released game showed hints in (<see cref="AttemptStanding.Hinted"/>), compared apart from the
/// others: beside the same bot profiles, each shown the game's hints on the same games and following every one.
/// </summary>
/// <param name="Hints">
/// The hints the bots were shown; absent when the comparison was asked without them (the web lab, whose records hold
/// none of the game's hints), and then the people's figures stand alone: no bot rate (<see cref="ComparisonFigures.Bots"/> is empty).
/// </param>
/// <param name="Levels">By level and move limit, as <see cref="HumanBotReport.Levels"/>.</param>
/// <param name="Overall">All levels together, as <see cref="HumanBotReport.Overall"/>.</param>
public sealed record HintedComparison(GameHints? Hints, IReadOnlyList<LevelComparison> Levels, ComparisonFigures Overall);

/// <param name="Bots">
/// The bot profiles compared, weakest first; every <see cref="ComparisonFigures.Bots"/> list follows this order, the
/// hinted group's too, where each profile follows the game's hints.
/// </param>
/// <param name="Levels">The attempts no hint was shown in, by level and move limit.</param>
/// <param name="Overall">All levels together: every attempt no hint was shown in counts once, on the humans' side and on the bots'.</param>
/// <param name="Hinted">The attempts the released game showed hints in, compared apart.</param>
public sealed record HumanBotReport(IReadOnlyList<string> Bots, IReadOnlyList<LevelComparison> Levels, ComparisonFigures Overall, RecordTally Records, HintedComparison Hinted);

/// <summary>
/// Compares recorded human attempts with the greedy bot profiles ("which bot do people play like?", docs/GAME_DESIGN.md
/// §20) as a paired comparison: the bots replay each attempt's own game — its seeds, so its starting board and every
/// refill — under the attempt's move limit, and each human rate stands beside the bots' rates on the games it covers
/// (<see cref="BotWinRate"/>). Only comparable attempts (<see cref="AttemptCounting.Standing"/>) are
/// compared: no help from the lab's tools, the tutorial's hand or boosters, seeds drawn from the level's attempt
/// policy, since a hand-chosen seed may replay one already seen, and none played in the released game's tester mode.
/// The attempts the released game showed hints in are compared apart, beside the same profiles following the game's
/// hints (<see cref="HintedComparison"/>): a hinted person beside unhinted bots would read as a stronger player.
/// </summary>
public static class HumanBotComparison
{
    /// <summary>Bot run <c>k</c> of every game uses bot seed <c>Rng.Derive(BotSeed, k)</c>: only the bot's own choices differ between runs.</summary>
    public const ulong BotSeed = 1;

    /// <summary>
    /// Compares without the game's hints: the attempts the released game showed hints in are counted and summed up
    /// apart, and no bot stands beside them (<see cref="HintedComparison.Hints"/> is absent).
    /// </summary>
    /// <param name="levels">The levels the records may belong to, matched as the other overload matches them.</param>
    /// <param name="botRuns">Games each bot plays on each attempt's game.</param>
    /// <exception cref="LevelInitializationException">A matched level cannot produce a starting board.</exception>
    public static HumanBotReport Compare(IEnumerable<AttemptRecord> records, IReadOnlyList<LevelDefinition> levels, int botRuns) =>
        Compared(records, levels, botRuns, gameHints: null);

    /// <param name="levels">
    /// The levels the records may belong to. A record matches by level id and by the revision of the level's content:
    /// the level fingerprint when it has lab details, its content version when it has none (the released game's records).
    /// </param>
    /// <param name="botRuns">Games each bot plays on each attempt's game, in each group.</param>
    /// <param name="gameHints">The hints the released game shows, which the bots beside the hinted attempts follow.</param>
    /// <exception cref="LevelInitializationException">A matched level cannot produce a starting board.</exception>
    public static HumanBotReport Compare(IEnumerable<AttemptRecord> records, IReadOnlyList<LevelDefinition> levels, int botRuns, GameHints gameHints) =>
        Compared(records, levels, botRuns, gameHints);

    private static HumanBotReport Compared(IEnumerable<AttemptRecord> records, IReadOnlyList<LevelDefinition> levels, int botRuns, GameHints? gameHints)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(botRuns, 1);
        ILookup<LevelId, KnownLevel> known = levels.Select(level => new KnownLevel(level, LevelJson.Fingerprint(level))).ToLookup(k => k.Level.Id);
        var unhinted = new List<MatchedAttempt>();
        var hinted = new List<MatchedAttempt>();
        int assisted = 0;
        int chosenSeeds = 0;
        int unknownLevel = 0;
        int testerMode = 0;
        foreach (AttemptRecord record in records)
        {
            AttemptStanding standing = AttemptCounting.Standing(record);
            switch (standing)
            {
                case AttemptStanding.Assisted:
                    assisted++;
                    break;
                case AttemptStanding.ChosenSeeds:
                    chosenSeeds++;
                    break;
                case AttemptStanding.TesterMode:
                    testerMode++;
                    break;
                case AttemptStanding.Comparable or AttemptStanding.Hinted when LevelOf(record, known) is { } level:
                    var matched = new MatchedAttempt(record, level, new HumanGame(level.Level, record.Start.Seeds, record.Start.MoveLimit));
                    (standing == AttemptStanding.Hinted ? hinted : unhinted).Add(matched);
                    break;
                case AttemptStanding.Comparable or AttemptStanding.Hinted:
                    unknownLevel++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(records), "A record has an unknown standing.");
            }
        }

        var plainBots = new BotGames(GamesOf(unhinted), botRuns, SkillProfile.All, AsItIs);
        BotGames hintedBots = gameHints is null
            ? new BotGames(GamesOf(hinted), botRuns, [], AsItIs)
            : new BotGames(GamesOf(hinted), botRuns, SkillProfile.All, gameHints.Follow);
        var tally = new RecordTally(unhinted.Count + hinted.Count, assisted, chosenSeeds, unknownLevel, testerMode, Hinted: hinted.Count);
        var hintedGroup = new HintedComparison(gameHints, Rows(hinted, hintedBots), Figures(hinted, hintedBots));
        return new HumanBotReport(plainBots.Names, Rows(unhinted, plainBots), Figures(unhinted, plainBots), tally, hintedGroup);
    }

    /// <summary>A bot that plays as it is: the profile shown no hint.</summary>
    private static IBotPolicy AsItIs(IBotPolicy profile) => profile;

    private static HumanGame[] GamesOf(IEnumerable<MatchedAttempt> attempts) => attempts.Select(attempt => attempt.Game).Distinct().ToArray();

    /// <summary>One row for each level version and move limit the attempts were played at, in level and limit order.</summary>
    private static LevelComparison[] Rows(IEnumerable<MatchedAttempt> attempts, BotGames bots) => attempts
        .GroupBy(attempt => (attempt.Level, attempt.Game.MoveLimit))
        .OrderBy(group => group.Key.Level.Level.Id.Value)
        .ThenBy(group => group.Key.MoveLimit)
        .Select(group => CompareLevel(group.Key.Level, group.Key.MoveLimit, group.ToArray(), bots))
        .ToArray();

    /// <summary>The one level the record was played on; absent when none or several match.</summary>
    private static KnownLevel? LevelOf(AttemptRecord record, ILookup<LevelId, KnownLevel> known)
    {
        KnownLevel[] matches = known[record.Start.Level]
            .Where(level => record.Lab is null
                ? level.Level.ContentVersion == record.Start.ContentVersion
                : level.Fingerprint == record.Lab.LevelFingerprint)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static LevelComparison CompareLevel(KnownLevel level, int moveLimit, IReadOnlyList<MatchedAttempt> attempts, BotGames bots) =>
        new(level.Level.Id, level.Fingerprint, moveLimit, AttemptCounting.MedianWinMoves(attempts.Select(attempt => attempt.Record)), Figures(attempts, bots));

    private static ComparisonFigures Figures(IReadOnlyList<MatchedAttempt> attempts, BotGames bots) =>
        ComparisonFigures.Of(attempts.Select(attempt => attempt.Record).ToArray(), bots.WinRatesOn(attempts));

    /// <summary>A game a person started: the level, the attempt's seeds and the move limit it was played under.</summary>
    private readonly record struct HumanGame(LevelDefinition Level, AttemptSeeds Seeds, int MoveLimit);

    /// <summary>A level the records may match, with the fingerprint that tells its version apart.</summary>
    private sealed record KnownLevel(LevelDefinition Level, string Fingerprint);

    private sealed record MatchedAttempt(AttemptRecord Record, KnownLevel Level, HumanGame Game);

    /// <summary>Each greedy profile's wins on every distinct game people started, each played <c>runs</c> times.</summary>
    private sealed class BotGames
    {
        private readonly int _runs;

        /// <summary>Wins out of <see cref="_runs"/>, by game, then by profile in the order of <see cref="Names"/>.</summary>
        private readonly Dictionary<HumanGame, int[]> _wins;

        /// <param name="profiles">The profiles that play, weakest first; none plays no game.</param>
        /// <param name="playerOf">The player for one game, made from the profile's policy: as it is, or following the game's hints.</param>
        /// <exception cref="LevelInitializationException">A level cannot produce a starting board.</exception>
        public BotGames(IReadOnlyList<HumanGame> games, int runs, IReadOnlyList<SkillProfile> profiles, Func<IBotPolicy, IBotPolicy> playerOf)
        {
            _runs = runs;
            int[][] winsByProfile = profiles.Select(profile => WinsOf(profile, playerOf, games, runs)).ToArray();
            _wins = games
                .Select((game, index) => (game, index))
                .ToDictionary(pair => pair.game, pair => winsByProfile.Select(wins => wins[pair.index]).ToArray());
            Names = profiles.Select(profile => profile.Name).ToArray();
        }

        /// <summary>The profiles, weakest first.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>
        /// Each profile's win rates on the games of <paramref name="attempts"/>: on the finished attempts' games, and on
        /// all of them. Every attempt counts its game once, so a game two attempts share counts twice. Empty without attempts.
        /// </summary>
        public BotWinRate[] WinRatesOn(IReadOnlyList<MatchedAttempt> attempts)
        {
            if (attempts.Count == 0)
            {
                return [];
            }

            HumanGame[] all = attempts.Select(attempt => attempt.Game).ToArray();
            HumanGame[] finished = attempts.Where(attempt => AttemptCounting.IsDecided(attempt.Record)).Select(attempt => attempt.Game).ToArray();
            return Names
                .Select((name, profile) => new BotWinRate(
                    name,
                    WinRateOnFinished: finished.Length > 0 ? ShareWon(profile, finished) : null,
                    WinRateOnAll: ShareWon(profile, all)))
                .ToArray();
        }

        /// <summary>The profile's share of wins over every run on <paramref name="games"/>, which are not empty.</summary>
        private double ShareWon(int profile, IReadOnlyList<HumanGame> games) =>
            games.Sum(game => _wins[game][profile]) / (double)(games.Count * _runs);

        /// <summary>
        /// The profile's wins on each game, played on the game's own seeds and stopped at its move limit: the bot ignores
        /// the moves left, so this is the game the person had. A hinted player is made for each game, since it counts
        /// the hints of one game.
        /// </summary>
        private static int[] WinsOf(SkillProfile profile, Func<IBotPolicy, IBotPolicy> playerOf, IReadOnlyList<HumanGame> games, int runs)
        {
            var won = new bool[games.Count * runs];
            Parallel.For(
                0,
                won.Length,
                () => new GreedyPolicy(profile),
                (i, _, policy) =>
                {
                    HumanGame game = games[i / runs];
                    RunResult run = SimulationRunner.Play(game.Level, playerOf(policy), game.Seeds, Rng.Derive(BotSeed, (ulong)(i % runs)), game.MoveLimit);
                    won[i] = run.FinalStatus == GameStatus.Won;
                    return policy;
                },
                _ => { });
            return games.Select((_, g) => new ArraySegment<bool>(won, g * runs, runs).Count(isWin => isWin)).ToArray();
        }
    }
}
