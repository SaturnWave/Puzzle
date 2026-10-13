using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>Where a player stood at a moment the reports ask about, and everything counted about them until then.</summary>
public sealed record Checkpoint(PlayerTime At, Standing Standing, JourneyTotals Totals);

/// <summary>One player's journey as the reports read it.</summary>
public sealed record PlayerOutcome
{
    public required SimulatedPlayer Player { get; init; }

    /// <summary>For each milestone of the settings, in their order: the moment its level was won; absent when it never was.</summary>
    public required IReadOnlyList<Checkpoint?> AtMilestones { get; init; }

    /// <summary>For each day the pass listed, in their order: the end of that day. A journey that was over by then stands as it ended.</summary>
    public required IReadOnlyList<Checkpoint> AtDayEnds { get; init; }

    public required Checkpoint AtEnd { get; init; }
}

/// <summary>Every player's journey on one half of the stored games, and what each rung did level by level.</summary>
public sealed record PassResult(MeasuredHalf Half, IReadOnlyList<PlayerOutcome> Players, IReadOnlyDictionary<SkillRung, GroupTallies> ByRung);

/// <summary>
/// A whole run: the players on every stored game, and the same players again on the even and on the odd games alone.
/// What differs between the two halves is the noise of the measurement, not a finding about the game.
/// </summary>
public sealed record PopulationResult(EconomySettings Settings, SimulatedLevels Levels, PassResult All, PassResult EvenGames, PassResult OddGames);

/// <summary>
/// Plays every simulated player, one after the other in the order of their numbers, and folds each journey into what
/// the reports read. Nothing here is random beyond the players' own seeds, so the same settings, measurement and
/// seed give the same result.
/// </summary>
public static class PopulationRun
{
    /// <summary>
    /// The simulated players. The mix is split exactly, not drawn: the weakest rung gets the first numbers. Player i's
    /// own seed is derived from the settings' seed and i, so a player is the same person under every settings file
    /// with the same players block.
    /// </summary>
    public static IReadOnlyList<SimulatedPlayer> PlayersOf(PopulationSettings settings)
    {
        var players = new List<SimulatedPlayer>(settings.Count);
        foreach (MixShare part in settings.SkillMix)
        {
            int ofRung = settings.Count * part.Share.Value / 100;
            for (int player = 0; player < ofRung; player++)
            {
                int index = players.Count;
                players.Add(new SimulatedPlayer(index, part.Rung, new PlayerSeed(Rng.Derive(settings.Seed, (ulong)index))));
            }
        }

        return players;
    }

    /// <summary>The three passes of a run. Only the pass on every game keeps the day ends; the halves are read for the pace and the levels alone.</summary>
    public static PopulationResult Play(EconomySettings settings, SimulatedLevels levels) => new(
        settings,
        levels,
        PlayPass(settings, levels, MeasuredHalf.All, settings.Report.BalanceAtDays),
        PlayPass(settings, levels, MeasuredHalf.Even, dayEndsKept: []),
        PlayPass(settings, levels, MeasuredHalf.Odd, dayEndsKept: []));

    /// <param name="dayEndsKept">The days at whose end every player's standing is kept.</param>
    public static PassResult PlayPass(EconomySettings settings, SimulatedLevels levels, MeasuredHalf half, IReadOnlyList<DayNumber> dayEndsKept)
    {
        var simulator = new PlayerSimulator(settings, levels, half);
        Dictionary<SkillRung, GroupTallies> byRung = settings.Players.Rungs.ToDictionary(rung => rung, _ => new GroupTallies(levels.Count));
        var outcomes = new List<PlayerOutcome>(settings.Players.Count);
        foreach (SimulatedPlayer player in PlayersOf(settings.Players))
        {
            var recorder = new JourneyRecorder(player, settings.Report.Milestones, dayEndsKept, byRung[player.Rung]);
            simulator.Play(player, recorder);
            outcomes.Add(recorder.Finish());
        }

        return new PassResult(half, outcomes, byRung);
    }

    /// <summary>Listens to one player's journey: keeps the running totals, the checkpoints, and adds what the player did on each level to their rung's tallies.</summary>
    private sealed class JourneyRecorder(SimulatedPlayer player, IReadOnlyList<LevelId> milestones, IReadOnlyList<DayNumber> dayEndsKept, GroupTallies rung) : IJourneyLog
    {
        private readonly JourneyTotals _totals = new();
        private readonly Checkpoint?[] _atMilestones = new Checkpoint?[milestones.Count];
        private readonly Checkpoint?[] _atDayEnds = new Checkpoint?[dayEndsKept.Count];
        private Standing _standing = new(0, Coins.None, BoosterShelf.Empty);
        private PlayerTime _now;

        /// <summary>When the level being played was first attempted; absent before its first attempt.</summary>
        private PlayerTime? _levelStartedAt;
        private TimeSpan _playTimeOnLevel;

        public void AttemptPlayed(AttemptPlayed attempt)
        {
            _totals.Count(attempt);
            _standing = attempt.After;
            _now = attempt.DecidedAt;
            LevelTally level = rung.Of(attempt.Level);
            level.CountAttempt(attempt);
            rung.PlaySecondsPerAttempt.Add((int)attempt.PlayTime.TotalSeconds);
            PlayerTime levelStartedAt = _levelStartedAt ??= attempt.StartedAt;
            _playTimeOnLevel += attempt.PlayTime;
            if (!attempt.IsWon)
            {
                return;
            }

            // Attempt k meets seed k of the pool in turn; the two numbers part only once the pool has been gone through.
            bool metASeedTwice = attempt.Attempt.Value != attempt.Met.Seed.Value;
            level.CountWin(attempt.Attempt.Value + 1, _playTimeOnLevel, attempt.DecidedAt - levelStartedAt, metASeedTwice);
            _levelStartedAt = null;
            _playTimeOnLevel = TimeSpan.Zero;
            if (MilestoneAt(attempt.Level.Value) is int milestone)
            {
                _atMilestones[milestone] = new Checkpoint(attempt.DecidedAt, _standing, _totals.Snapshot());
            }
        }

        public void SessionEnded(SessionEnded session)
        {
            _totals.Count(session);
            _now = session.StartedAt + session.Length;
            if (session.Attempts > 0)
            {
                rung.AttemptsPerSession.Add(session.Attempts);
            }
        }

        public void RewardGiven(RewardGiven reward)
        {
            _totals.Count(reward);
            _standing = reward.After;
            _now = reward.At;

            // The reward of the painting a milestone level completes comes with that win: it belongs to the milestone.
            if (reward.Source == CoinSource.PaintingCompleted && MilestoneAt(_standing.LevelsWon) is int milestone && _atMilestones[milestone] is { } reached)
            {
                _atMilestones[milestone] = reached with { Standing = _standing, Totals = _totals.Snapshot() };
            }
        }

        public void DayEnded(DayEnded day)
        {
            _standing = day.Standing;
            for (int kept = 0; kept < dayEndsKept.Count; kept++)
            {
                if (dayEndsKept[kept] == day.Day)
                {
                    _atDayEnds[kept] = new Checkpoint(PlayerTime.EndOf(day.Day), _standing, _totals.Snapshot());
                }
            }
        }

        /// <summary>
        /// Closes the journey: a player who had started a level and did not win it is left on it. A player whose last
        /// attempt was a win has arrived at the next level and played nothing of it: they are not one of its players.
        /// </summary>
        public PlayerOutcome Finish()
        {
            var end = new Checkpoint(_now, _standing, _totals.Snapshot());
            if (_levelStartedAt is not null)
            {
                rung.Of(new LevelId(_standing.LevelsWon + 1)).CountLeftOnIt();
            }

            return new PlayerOutcome
            {
                Player = player,
                AtMilestones = _atMilestones,
                AtDayEnds = _atDayEnds.Select(dayEnd => dayEnd ?? end).ToArray(),
                AtEnd = end,
            };
        }

        private int? MilestoneAt(int level)
        {
            for (int milestone = 0; milestone < milestones.Count; milestone++)
            {
                if (milestones[milestone].Value == level)
                {
                    return milestone;
                }
            }

            return null;
        }
    }
}
