using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bot;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Bank.Assembling;

/// <summary>How hard a level should be: for whom, how often that player wins the first attempt, and how long it may be.</summary>
/// <param name="Share">The share of first attempts <paramref name="Player"/> should win at the calibrated limit.</param>
/// <param name="Moves">The range the calibrated limit should land in.</param>
/// <param name="Margin">Extra moves the final limit carries on top of the calibrated one.</param>
/// <param name="KindMoves">Ranges of their own that levels of a kind have in place of <paramref name="Moves"/>; empty when every kind shares the tier's range.</param>
public sealed record SlotDifficulty(PlayerMix Player, double Share, MoveRange Moves, MoveMargin Margin, IReadOnlyDictionary<LevelKind, MoveRange> KindMoves)
{
    public SlotDifficulty(PlayerMix player, double share, MoveRange moves, MoveMargin margin)
        : this(player, share, moves, margin, new Dictionary<LevelKind, MoveRange>())
    {
    }

    /// <summary>
    /// What is asked of this board: the same player, share and margin, and the move range of its kind where the
    /// policy gives the kind one (<see cref="LevelKinds.MovesFor"/>).
    /// </summary>
    public SlotDifficulty For(CandidateFeatures board) => this with { Moves = LevelKinds.MovesFor(LevelKinds.Of(board), KindMoves, Moves) };
}

/// <summary>
/// What the plan and the policy ask of one level number, before any board is chosen for it.
/// </summary>
/// <param name="Plan">What the number is: tier, size, palette, mechanics. Its calibration fields are the generator's and are not read here.</param>
/// <param name="Structure">What a board must have to stand at this number.</param>
/// <param name="Difficulty">Absent for a pinned level before the policy's first band: nothing is decided for it.</param>
/// <param name="PinnedBecause">Why the level stays as it is; absent for a level the assembly decides.</param>
/// <param name="RecordedSeeds">
/// The pool seeds people recorded attempts on, when recordings protect the level's board
/// (<see cref="RecordedProtection.KeepBoard"/>); absent for a level no recording protects.
/// </param>
public sealed record LevelSlot(int Number, LevelPlan Plan, StructureNeeds Structure, SlotDifficulty? Difficulty, string? PinnedBecause, IReadOnlyList<uint>? RecordedSeeds = null)
{
    public bool IsPinned => PinnedBecause is not null;

    /// <summary>People recorded attempts on the level's board: it is decided anew on that board or not at all.</summary>
    public bool KeepsBoard => RecordedSeeds is not null;

    public DifficultyTier Tier => Plan.Tier;
}

/// <summary>Turns level numbers into slots under a policy.</summary>
public static class LevelSlots
{
    /// <param name="recorded">The levels people have recorded attempts on; protected as the policy says.</param>
    /// <exception cref="PolicyFormatException">A level is neither pinned nor covered by a band of the policy.</exception>
    public static IReadOnlyList<LevelSlot> Of(DifficultyPolicy policy, IEnumerable<int> numbers, RecordedLevels recorded)
    {
        var planner = new LevelPlanner(policy.Plan);
        return numbers.Select(number => SlotOf(policy, planner.Plan(number), recorded)).ToArray();
    }

    private static LevelSlot SlotOf(DifficultyPolicy policy, LevelPlan plan, RecordedLevels recorded)
    {
        string? pinnedBecause = policy.Pins(plan.Number) ? "pinned by the policy"
            : policy.Recorded == RecordedProtection.Freeze && recorded.Contains(plan.Number) ? "people have recorded attempts on it"
            : null;
        PolicyBand? band = policy.BandFor(plan.Number);
        if (band is null && pinnedBecause is null)
        {
            throw new PolicyFormatException(
                $"Level {plan.Number} is neither pinned nor covered by a band: the policy's first band starts at level {policy.Bands[0].FromLevel}. Pin the levels before it or add a band for them.");
        }

        SlotDifficulty? difficulty = band is null
            ? null
            : new SlotDifficulty(
                band.Player,
                band.WinShare.For(plan.Tier),
                (band.Moves ?? policy.Plan.BandFor(plan.Number).Moves).For(plan.Tier),
                band.MoveMargin,
                band.KindMovesFor(plan.Tier));
        bool keepsBoard = pinnedBecause is null && policy.Recorded == RecordedProtection.KeepBoard && recorded.Contains(plan.Number);
        return new LevelSlot(plan.Number, plan, StructureNeeds.Of(plan), difficulty, pinnedBecause, keepsBoard ? recorded.SeedsOf(plan.Number) : null);
    }
}

/// <summary>
/// The identity of a decision: everything a level's limit and board were decided from. A level whose key under
/// today's policy equals the key its file and report row were made under needs no new decision and stays byte for
/// byte; any change of player, share, margin, move range, tier, size, palette or board gives another key.
/// </summary>
public static class DecisionKey
{
    private const string Scheme = "d1";

    /// <param name="difficulty">What is asked of the candidate's kinds (<see cref="SlotDifficulty.For(CandidateFeatures)"/>).</param>
    public static string Of(LevelSlot slot, SlotDifficulty difficulty, CandidateId candidate) => Of(
        difficulty.Player.Label,
        ShareText(difficulty.Share),
        difficulty.Margin.ToString(),
        difficulty.Moves.ToString(),
        slot.Tier,
        slot.Plan.Size.Width,
        slot.Plan.Size.Height,
        slot.Plan.Palette,
        candidate);

    /// <summary>The key a level on disk was decided under, read from its report row and its file.</summary>
    /// <param name="player">The row's calibration bot; a named profile is the ladder rung that plays like it.</param>
    /// <param name="share">The row's target win share, as written (three decimals).</param>
    public static string Of(string player, string share, string margin, string moves, DifficultyTier tier, int width, int height, IReadOnlyList<GemColor> palette, CandidateId candidate)
    {
        string who = PlayerMix.TryParse(player, out PlayerMix? mix, out _) ? mix.Label : player.ToLowerInvariant();
        string text = string.Join('|', Scheme, who, share, MarginText(margin), moves, tier, $"{width}x{height}", string.Join(',', palette), candidate);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>A margin in one spelling, so 0, 0.0 and the empty cell of a report older than margins are the same margin.</summary>
    private static string MarginText(string margin) =>
        margin.Length == 0 ? "0"
        : decimal.TryParse(margin, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal share) ? share.ToString("0.####", CultureInfo.InvariantCulture)
        : margin;

    /// <summary>A share as the report writes it, so a policy's 0.9 and a row's 0.900 are the same share.</summary>
    public static string ShareText(double share) => share.ToString("0.000", CultureInfo.InvariantCulture);
}

/// <summary>
/// What a level was decided under besides its key: the rules of the engine and the bot, and how its seed pool and
/// its checks were made. The key is recomputed from a level's file and report row; these two cannot be, so an
/// assembly writes them beside the level (assembly.csv) and a later assembly keeps the level only while both still
/// hold. A level of a folder no assembly wrote (the generator's) has no stamp: the first assembly takes it over under
/// the stamp of its own build, which is the statement that those levels were made with these rules.
/// </summary>
/// <param name="Rules">A digest of the build's behaviour fingerprint (<see cref="MeasurementFingerprint"/>) and of the skill ladder's settings, 16 hex digits.</param>
/// <param name="Checks">A digest of the seed-pool rules, the win-share tolerance and the sizes of the checks.</param>
/// <param name="Epoch">
/// The epoch of the bank whose games decided the level; empty when no bank was at hand. An epoch opened by hand for a
/// change the fingerprint does not see leaves <paramref name="Rules"/> as it was, and this is what then tells the
/// levels of before from the levels of after.
/// </param>
public sealed record DecisionStamp(string Rules, string Checks, string Epoch)
{
    private const int Digits = 16;

    private static readonly Lazy<string> RulesOfThisBuild = new(RulesDigest);

    /// <param name="epoch">The epoch of the bank the assembly reads; absent without a bank.</param>
    public static DecisionStamp Of(DifficultyPolicy policy, EpochId? epoch = null) =>
        new(RulesOfThisBuild.Value, ChecksDigest(policy), epoch?.ToString() ?? string.Empty);

    /// <summary>
    /// The fingerprint plays fixed probe players, so that one changed rung does not make every stored game stale.
    /// A level, though, was made for a rung by name: when skill-55 notices more or judges otherwise, a level made
    /// for the old skill-55 is no longer the level the policy asks for. So the ladder's settings are part of what a
    /// level stands under, beside the fingerprint.
    /// </summary>
    private static string RulesDigest()
    {
        IEnumerable<string> rungs = SkillLadder.Rungs.Select(rung => string.Create(CultureInfo.InvariantCulture, $"{rung.Name}:{rung.NoticePercent}:{rung.Temperature:R}"));
        string text = $"{MeasurementFingerprint.OfThisBuild}|{string.Join(';', rungs)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..Digits];
    }

    /// <summary>
    /// What of this stamp no longer holds under <paramref name="now"/>, in words; absent when a level made under
    /// this stamp may stay. Epochs are compared only where both are known: an assembly without a bank cannot tell.
    /// </summary>
    public string? WhatChangedBy(DecisionStamp now)
    {
        if (Rules != now.Rules)
        {
            return $"it was decided under other rules of the engine or the bot ({Rules}, this build has {now.Rules})";
        }

        if (Epoch.Length > 0 && now.Epoch.Length > 0 && Epoch != now.Epoch)
        {
            return $"it was decided on games of another epoch of the bank ({Epoch}; the games that count now are of {now.Epoch})";
        }

        return Checks != now.Checks ? "its seed pool and checks were made by other rules than the plan and the policy have now" : null;
    }

    /// <summary>
    /// How many candidates a level may try and how many games follow a passed audit are left out: they change how
    /// hard an assembly looks and what it reports, not what a decided level is.
    /// </summary>
    private static string ChecksDigest(DifficultyPolicy policy)
    {
        PoolRules pool = PoolRules.Of(policy.Plan);
        CheckSettings checks = policy.Checks;
        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"pool {pool.Size}/{pool.MinSize}/{pool.Tolerance:R}|win {policy.Plan.Calibration.WinRateTolerance:R}|confirm {checks.ConfirmSeeds}x{checks.ConfirmGamesPerSeed}|audit {checks.AuditGamesPerSeed}/{checks.FirstSeedGames}x{checks.AuditRounds}|first {checks.FirstTryTolerance:R}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..Digits];
    }
}
