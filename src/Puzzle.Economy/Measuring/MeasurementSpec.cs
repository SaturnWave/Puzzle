using System.Globalization;
using Puzzle.Bank.Measuring;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Measuring;

/// <summary>A measurement file is not one, or is damaged; the message names the file and the line.</summary>
public sealed class MeasurementFormatException : Exception
{
    public MeasurementFormatException(string message)
        : base(message)
    {
    }

    public MeasurementFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A measurement does not fit what it is asked for: it was played by another engine or on other levels, was measured
/// with other settings, or does not cover the run. The message says what differs and what to measure again.
/// </summary>
public sealed class MeasurementMismatchException : Exception
{
    public MeasurementMismatchException(string message)
        : base(message)
    {
    }
}

/// <summary>What the settings ask of a measurement: how far games are followed, which are played and how many.</summary>
public sealed record MeasurementPlan
{
    /// <summary>Every game is followed this far and no further.</summary>
    public required MoveCount MoveCap { get; init; }

    /// <summary>Game j of a seed is played with bot repeat <c>FirstRepeat + j</c>: a range no game of the level bank uses.</summary>
    public required int FirstRepeat { get; init; }

    /// <summary>The shares of the goals whose first move is kept for every game.</summary>
    public required IReadOnlyList<Percent> ProgressMarks { get; init; }

    /// <summary>Games on a plain board per pool position; the last number holds for every later position.</summary>
    public required IReadOnlyList<int> PlainGamesBySeedPosition { get; init; }

    /// <summary>Games per pool seed on a board that starts with a booster's special.</summary>
    public required int BoostedGamesPerSeed { get; init; }

    public required IReadOnlyList<BoardStart> BoardStarts { get; init; }
}

/// <summary>
/// What identifies a measurement file, written as its first line: the engine that played, how far and how deep, and the
/// two numbers of the engine a stored game is read with. Lines of two files with different specs are never read together.
/// </summary>
public sealed record MeasurementSpec
{
    public const int Format = 1;

    /// <summary>The fingerprint of how the engine and the greedy bot played (<see cref="MeasurementFingerprint"/>).</summary>
    public required string Engine { get; init; }

    public required MoveCount MoveCap { get; init; }

    public required int FirstRepeat { get; init; }

    public required IReadOnlyList<Percent> ProgressMarks { get; init; }

    public required IReadOnlyList<int> PlainGamesBySeedPosition { get; init; }

    public required int BoostedGamesPerSeed { get; init; }

    /// <summary>The moves the start booster added when the games were played (RuleSet.StartBoosterExtraMoves).</summary>
    public required MoveCount StartBoosterExtraMoves { get; init; }

    /// <summary>The moves the extra-moves offer added when the games were played (<see cref="ExtraMovesOffer.Moves"/>).</summary>
    public required MoveCount ExtraMovesOfferMoves { get; init; }

    /// <summary>The spec a measurement made now, by this build, to <paramref name="plan"/> has.</summary>
    public static MeasurementSpec OfThisBuild(MeasurementPlan plan) => new()
    {
        Engine = MeasurementFingerprint.OfThisBuild,
        MoveCap = plan.MoveCap,
        FirstRepeat = plan.FirstRepeat,
        ProgressMarks = plan.ProgressMarks,
        PlainGamesBySeedPosition = plan.PlainGamesBySeedPosition,
        BoostedGamesPerSeed = plan.BoostedGamesPerSeed,
        StartBoosterExtraMoves = new MoveCount(RuleSet.Default.StartBoosterExtraMoves),
        ExtraMovesOfferMoves = new MoveCount(ExtraMovesOffer.Moves),
    };

    /// <summary>
    /// The stored games are read with the moves the start booster and the offer add, and the settings were checked
    /// with this build's. No game was played with either, so no fingerprint and no replay can see them change: the two
    /// numbers of the file are compared with the engine's here, before any run, checked or trusted.
    /// </summary>
    /// <exception cref="MeasurementMismatchException">The file says another number of moves than the engine adds today.</exception>
    public void RequireTheAddedMovesOfThisBuild()
    {
        MeasurementSpec today = this with
        {
            StartBoosterExtraMoves = new MoveCount(RuleSet.Default.StartBoosterExtraMoves),
            ExtraMovesOfferMoves = new MoveCount(ExtraMovesOffer.Moves),
        };
        IReadOnlyList<string> differences = today.DifferencesFrom(this);
        if (differences.Count > 0)
        {
            throw new MeasurementMismatchException(
                $"The measurement was made when the engine added other moves than it does today: {string.Join("; ", differences)}. "
                + "Its games do not depend on these numbers, but a run would read them with the old ones: make the file again with puzzle economy measure --restart.");
        }
    }

    /// <summary>
    /// In words, every depth in which <paramref name="plan"/> asks for another measurement than this one. A run reads
    /// the file, whatever the measurement block of its settings says.
    /// </summary>
    public IReadOnlyList<string> DifferencesFromThePlan(MeasurementPlan plan)
    {
        MeasurementSpec asked = OfThisBuild(plan) with
        {
            Engine = Engine,
            StartBoosterExtraMoves = StartBoosterExtraMoves,
            ExtraMovesOfferMoves = ExtraMovesOfferMoves,
        };
        return DifferencesFrom(asked);
    }

    /// <summary>How many games a seed at <paramref name="position"/> of a pool has for <paramref name="start"/>.</summary>
    public int GamesOn(BoardStart start, SeedPosition position) => start == BoardStart.Plain
        ? PlainGamesBySeedPosition[Math.Min(position.Value, PlainGamesBySeedPosition.Count - 1)]
        : BoostedGamesPerSeed;

    /// <summary>In words, every field in which <paramref name="other"/> differs from this spec; none when they are the same.</summary>
    public IReadOnlyList<string> DifferencesFrom(MeasurementSpec other)
    {
        var differences = new List<string>();
        void Compare(string field, string mine, string theirs)
        {
            if (mine != theirs)
            {
                differences.Add($"{field} {theirs} instead of {mine}");
            }
        }

        Compare("engine fingerprint", Abbreviated(Engine), Abbreviated(other.Engine));
        Compare("moveCap", MoveCap.ToString(), other.MoveCap.ToString());
        Compare("firstRepeat", Text(FirstRepeat), Text(other.FirstRepeat));
        Compare("progressMarks", string.Join('/', ProgressMarks), string.Join('/', other.ProgressMarks));
        Compare("plainGamesBySeedPosition", string.Join('/', PlainGamesBySeedPosition), string.Join('/', other.PlainGamesBySeedPosition));
        Compare("boostedGamesPerSeed", Text(BoostedGamesPerSeed), Text(other.BoostedGamesPerSeed));
        Compare("the start booster's moves", StartBoosterExtraMoves.ToString(), other.StartBoosterExtraMoves.ToString());
        Compare("the extra-moves offer's moves", ExtraMovesOfferMoves.ToString(), other.ExtraMovesOfferMoves.ToString());
        return differences;
    }

    /// <summary>Enough of a fingerprint for a person to tell two apart.</summary>
    public static string Abbreviated(string fingerprint) => fingerprint.Length > 16 ? fingerprint[..16] : fingerprint;

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
}
