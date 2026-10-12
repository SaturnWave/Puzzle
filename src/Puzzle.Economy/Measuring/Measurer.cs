using System.Collections.Concurrent;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Economy.Units;
using Puzzle.Tooling;

namespace Puzzle.Economy.Measuring;

/// <summary>A level cannot be measured; the message names the level and the board start, the inner exception is the engine's reason.</summary>
public sealed class UnmeasurableLevelException : Exception
{
    public UnmeasurableLevelException(string message)
        : base(message)
    {
    }

    public UnmeasurableLevelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>What to measure and where to keep it.</summary>
public sealed record MeasureRequest
{
    /// <summary>The measurement file; lines it already has for other levels are kept.</summary>
    public required string Path { get; init; }

    public required MeasurementPlan Plan { get; init; }

    /// <summary>The levels to measure.</summary>
    public required IReadOnlyList<LevelDefinition> Levels { get; init; }

    public required IReadOnlyList<SkillRung> Rungs { get; init; }

    /// <summary>Lines played at the same time; absent for one per core.</summary>
    public int? Workers { get; init; }

    /// <summary>Discard what the file holds and measure everything asked for again.</summary>
    public bool Restart { get; init; }
}

/// <summary>How far a running measurement is; games are counted when their line is finished.</summary>
public sealed record MeasureProgress(int LinesDone, int LinesToPlay, long GamesDone, long GamesToPlay);

/// <summary>What a measurement did, and every line the file now holds.</summary>
public sealed record MeasureOutcome(MeasurementSpec Spec, IReadOnlyList<MeasuredLine> Lines, int LinesKept, int LinesPlayed, long GamesPlayed);

/// <summary>
/// Plays the bot games the economy is read from and keeps them in a measurement file. The unit of work is one line
/// (a level, a rung and a board start) with a bot of its own; lines are played across the cores and come back in the
/// order they were asked for, so the file is the same bytes whatever the number of workers. Every finished line is
/// appended to a part file beside the measurement at once: a run that is stopped goes on from there, and ends as the
/// file an undisturbed run would have written.
/// </summary>
public static class Measurer
{
    /// <param name="onProgress">Told after every finished line, on the calling thread.</param>
    /// <exception cref="MeasurementMismatchException">The file there was measured otherwise (another engine, other depths); nothing was changed.</exception>
    /// <exception cref="MeasurementFormatException">The file there, or its part file, is not a measurement or is damaged.</exception>
    /// <exception cref="UnmeasurableLevelException">A level has no seed pool, or cannot start with a board start's special.</exception>
    /// <exception cref="IOException">The measurement could not be written.</exception>
    public static MeasureOutcome Run(MeasureRequest request, Action<MeasureProgress>? onProgress = null)
    {
        MeasurementSpec spec = MeasurementSpec.OfThisBuild(request.Plan);
        string partPath = MeasurementFile.PartPathOf(request.Path);
        // Before anything is deleted or read: deleting in a folder that is not there fails, and a restart into a new folder is no failure.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.Path)) ?? ".");
        if (request.Restart)
        {
            File.Delete(request.Path);
            File.Delete(partPath);
        }

        SortedDictionary<LineKey, MeasuredLine> lines = KeptLines(request, spec, partPath);
        int kept = lines.Count;
        LineJob[] toPlay = PlannedLines(request).Where(job => !lines.ContainsKey(job.Key)).ToArray();
        long gamesPlayed = PlayInto(lines, toPlay, spec, partPath, request.Workers, onProgress);

        MeasurementFile.Write(request.Path, spec, lines.Values);
        File.Delete(partPath);
        return new MeasureOutcome(spec, [.. lines.Values], kept, toPlay.Length, gamesPlayed);
    }

    /// <summary>
    /// The lines of the file and of its part file that still stand: played by a rung that has not changed, on the level
    /// content the request names (a level outside the request is not judged here; a run checks every line it reads).
    /// What an interrupted run left in the part file is first put into the measurement itself, so nothing is lost
    /// when the part file starts anew.
    /// </summary>
    private static SortedDictionary<LineKey, MeasuredLine> KeptLines(MeasureRequest request, MeasurementSpec spec, string partPath)
    {
        Dictionary<int, string> fingerprints = request.Levels.ToDictionary(level => level.Id.Value, LevelJson.Fingerprint);
        var kept = new SortedDictionary<LineKey, MeasuredLine>();
        StoredMeasurement? finished = MeasurementFile.Read(request.Path);
        StoredMeasurement? left = MeasurementFile.ReadPart(partPath);
        if (finished is not null)
        {
            RequireSameSpec(finished.Spec, spec, request.Path, howToGoOn: "measure again with --restart (which discards the file), or keep it and name another with --out");
        }

        if (left is not null)
        {
            RequireSameSpec(left.Spec, spec, partPath, howToGoOn: "it is what a stopped run left behind, so delete that one file and measure again; the measurement beside it is not touched");
        }

        foreach (StoredMeasurement stored in new[] { finished, left }.OfType<StoredMeasurement>())
        {
            foreach (MeasuredLine line in stored.Lines.Where(line => StillStands(line, fingerprints)))
            {
                kept[line.Key] = line;
            }
        }

        if (left is not null)
        {
            MeasurementFile.Write(request.Path, spec, kept.Values);
        }

        return kept;
    }

    private static bool StillStands(MeasuredLine line, Dictionary<int, string> fingerprints) =>
        line.Player.IsOnLadder && (!fingerprints.TryGetValue(line.Level.Value, out string? today) || today == line.Fingerprint);

    /// <param name="path">The file the spec was read from: the measurement, or the part file a stopped run left beside it.</param>
    /// <param name="howToGoOn">What to do about that file.</param>
    private static void RequireSameSpec(MeasurementSpec stored, MeasurementSpec wanted, string path, string howToGoOn)
    {
        IReadOnlyList<string> differences = wanted.DifferencesFrom(stored);
        if (differences.Count > 0)
        {
            throw new MeasurementMismatchException(
                $"'{path}' was measured otherwise than is asked for now: {string.Join("; ", differences)}. Nothing was changed. "
                + $"Its games cannot be read together with new ones: {howToGoOn}.");
        }
    }

    /// <summary>Every line the request asks for, in file order.</summary>
    /// <exception cref="UnmeasurableLevelException">A level has no seed pool: nobody knows which seeds its players meet.</exception>
    private static IEnumerable<LineJob> PlannedLines(MeasureRequest request)
    {
        if (request.Levels.FirstOrDefault(level => level.Attempts.SeedPool.Count == 0) is { } unpooled)
        {
            throw new UnmeasurableLevelException($"Level {unpooled.Id} has no seed pool; the economy is measured on the seeds a player meets, in their order.");
        }

        return from level in request.Levels.OrderBy(level => level.Id.Value)
               from rung in request.Rungs.Order()
               from start in request.Plan.BoardStarts.Order()
               select new LineJob(level, rung, start);
    }

    /// <summary>Plays the lines across the cores, appending each to the part file and adding it to <paramref name="lines"/> as it comes back.</summary>
    /// <returns>The games played.</returns>
    private static long PlayInto(
        SortedDictionary<LineKey, MeasuredLine> lines,
        LineJob[] toPlay,
        MeasurementSpec spec,
        string partPath,
        int? workers,
        Action<MeasureProgress>? onProgress)
    {
        if (toPlay.Length == 0)
        {
            return 0;
        }

        long gamesToPlay = toPlay.Sum(job => (long)job.GamesIn(spec));
        long gamesDone = 0;
        int linesDone = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(partPath)) ?? ".");
        using StreamWriter part = MeasurementFile.Create(partPath);
        MeasurementFile.Append(part, MeasurementFile.HeaderText(spec));
        foreach (LineOutcome outcome in InOrder(toPlay, workers).Select(job => job.Play(spec)).AsSequential())
        {
            MeasuredLine line = outcome switch
            {
                LineOutcome.Played played => played.Line,
                LineOutcome.Failed failed => throw new UnmeasurableLevelException($"The games of {failed.Key} could not be played: {failed.Exception.Message}", failed.Exception),
                _ => throw new InvalidOperationException($"Unhandled line outcome {outcome.GetType().Name}."),
            };
            MeasurementFile.Append(part, MeasurementFile.LineText(line));
            lines[line.Key] = line;
            gamesDone += line.GameCount;
            onProgress?.Invoke(new MeasureProgress(++linesDone, toPlay.Length, gamesDone, gamesToPlay));
        }

        return gamesDone;
    }

    /// <summary>The jobs as one queue across the cores whose results come back in the order of the jobs, each as soon as it and every job before it is done.</summary>
    private static ParallelQuery<LineJob> InOrder(LineJob[] jobs, int? workers)
    {
        ParallelQuery<LineJob> queue = Partitioner.Create(jobs.AsEnumerable(), EnumerablePartitionerOptions.NoBuffering)
            .AsParallel()
            .AsOrdered()
            .WithMergeOptions(ParallelMergeOptions.NotBuffered);
        return workers is int count ? queue.WithDegreeOfParallelism(count) : queue;
    }

    /// <summary>One line to play: a level, a rung and a board start.</summary>
    private sealed record LineJob(LevelDefinition Level, SkillRung Rung, BoardStart Start)
    {
        public LineKey Key => LineKey.Of(Level.Id, Rung, Start);

        public int GamesIn(MeasurementSpec spec) =>
            Enumerable.Range(0, Level.Attempts.SeedPool.Count).Sum(position => spec.GamesOn(Start, new SeedPosition(position)));

        /// <summary>
        /// Plays the line with a bot of its own. What a game can fail with (a board that cannot start, or a bot or engine
        /// fault reported as <see cref="InvalidOperationException"/>) becomes the outcome, to be reported in file order;
        /// nothing is swallowed.
        /// </summary>
        public LineOutcome Play(MeasurementSpec spec)
        {
            try
            {
                return new LineOutcome.Played(Measure(spec));
            }
            catch (Exception ex) when (ex is LevelInitializationException or InvalidOperationException)
            {
                return new LineOutcome.Failed(Key, ex);
            }
        }

        private MeasuredLine Measure(MeasurementSpec spec)
        {
            var bot = new GreedyPolicy(Rung.Model.ToProfile());
            IReadOnlyList<uint> pool = Level.Attempts.SeedPool;
            var seeds = new SeedGames[pool.Count];
            for (int position = 0; position < seeds.Length; position++)
            {
                var games = new MeasuredGame[spec.GamesOn(Start, new SeedPosition(position))];
                for (int game = 0; game < games.Length; game++)
                {
                    games[game] = EconomyGame.Play(Level, bot, Start, pool[position], spec.FirstRepeat + game, spec.MoveCap, spec.ProgressMarks);
                }

                seeds[position] = new SeedGames(games, spec.ProgressMarks);
            }

            return new MeasuredLine
            {
                Level = Level.Id,
                Fingerprint = LevelJson.Fingerprint(Level),
                Tier = Level.Difficulty,
                MoveLimit = new MoveCount(Level.MoveLimit),
                Pool = pool,
                Player = Rung.Model,
                Start = Start,
                Seeds = seeds,
            };
        }
    }

    /// <summary>How a line ended: with its games, or with the exception a game failed with.</summary>
    private abstract record LineOutcome
    {
        private LineOutcome()
        {
        }

        public sealed record Played(MeasuredLine Line) : LineOutcome;

        public sealed record Failed(LineKey Key, Exception Exception) : LineOutcome;
    }
}
