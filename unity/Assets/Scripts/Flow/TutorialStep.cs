#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// A step of the game's tutorial (docs/GAME_DESIGN.md §19, the owner's decisions of 2026-10-19), in the order a
    /// player meets them: the first level's goal plaque, its match shown by hand and its moves medallion; the four and
    /// the five shown by hand; each obstacle where it is first seen; the first piece placed on a jigsaw and the first
    /// painting made whole; and the booster intro (#7): each booster used during play used once by hand on the level it
    /// opens on, the start boosters on the card before a level after its first loss, and after the first booster used
    /// by hand that a long press shows what a booster does. Each is shown once to a
    /// player. A closed set: a step is never renamed once a progress file names it (<see cref="TutorialSteps.NameOf"/>),
    /// and a new one is added at the end. No Unity dependency.
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>Level 1, before the first move: the plaque with the goal, what the level asks for.</summary>
        Goal,

        /// <summary>Level 1: two neighbours traded into a match of three, shown by hand.</summary>
        Match,

        /// <summary>Level 1, after the first move: the moves medallion, which every move counts down.</summary>
        Moves,

        /// <summary>Level 2: four in a line, which makes a brush (a rocket), shown by hand.</summary>
        FourInALine,

        /// <summary>Level 3: five in a line, which makes a palette (a colour bomb), shown by hand.</summary>
        FiveInALine,

        /// <summary>The first board with crates (the engine's boxes): what they are and how they break.</summary>
        Box,

        /// <summary>The first board with varnish on its floor (the engine's jelly).</summary>
        Jelly,

        /// <summary>The first board with taped pieces (the engine's chains).</summary>
        Chain,

        /// <summary>The first board with lost frame corners to bring down (the engine's items).</summary>
        Item,

        /// <summary>The first jigsaw a piece waits on: the piece carried to its place, shown.</summary>
        FirstPiece,

        /// <summary>The first painting made whole: celebrated with a word about the museum.</summary>
        FirstPainting,

        /// <summary>The level the spatula opens on: the spatula used once by hand, free (docs/GAME_DESIGN.md §19, #7).</summary>
        BoosterHitCell,

        /// <summary>The level the row brush opens on: the row brush used once by hand, free.</summary>
        BoosterHitRow,

        /// <summary>The level the column brush opens on: the column brush used once by hand, free.</summary>
        BoosterHitColumn,

        /// <summary>The level the shuffle opens on: the shuffle used once by hand, free.</summary>
        BoosterShuffle,

        /// <summary>
        /// The card before a level, the first time the player comes back to it after the level was lost: the three
        /// start boosters lit one after another, each with one sentence.
        /// </summary>
        StartBoosters,

        /// <summary>
        /// Right after the first booster used by hand (the owner's decision of 2026-10-19): its disc is lit once more and
        /// the pill says that holding a booster shows what it does again. A touch goes on.
        /// </summary>
        LongPress,
    }

    /// <summary>What the steps are called in a progress file, and which of them are shown on a board.</summary>
    public static class TutorialSteps
    {
        /// <summary>Every step, in the order of the enumeration: the order a player meets them.</summary>
        public static IReadOnlyList<TutorialStep> All { get; } = (TutorialStep[])Enum.GetValues(typeof(TutorialStep));

        private static readonly IReadOnlyDictionary<TutorialStep, string> Names = new Dictionary<TutorialStep, string>
        {
            [TutorialStep.Goal] = "goal",
            [TutorialStep.Match] = "match",
            [TutorialStep.Moves] = "moves",
            [TutorialStep.FourInALine] = "four-in-a-line",
            [TutorialStep.FiveInALine] = "five-in-a-line",
            [TutorialStep.Box] = "box",
            [TutorialStep.Jelly] = "jelly",
            [TutorialStep.Chain] = "chain",
            [TutorialStep.Item] = "item",
            [TutorialStep.FirstPiece] = "first-piece",
            [TutorialStep.FirstPainting] = "first-painting",
            [TutorialStep.BoosterHitCell] = "booster-hit-cell",
            [TutorialStep.BoosterHitRow] = "booster-hit-row",
            [TutorialStep.BoosterHitColumn] = "booster-hit-column",
            [TutorialStep.BoosterShuffle] = "booster-shuffle",
            [TutorialStep.StartBoosters] = "start-boosters",
            [TutorialStep.LongPress] = "long-press",
        };

        /// <summary>The booster each step of a booster used during play teaches.</summary>
        private static readonly IReadOnlyDictionary<TutorialStep, BoosterKind> Boosters = new Dictionary<TutorialStep, BoosterKind>
        {
            [TutorialStep.BoosterHitCell] = BoosterKind.HitCell,
            [TutorialStep.BoosterHitRow] = BoosterKind.HitRow,
            [TutorialStep.BoosterHitColumn] = BoosterKind.HitColumn,
            [TutorialStep.BoosterShuffle] = BoosterKind.Shuffle,
        };

        /// <summary>The step's name in a progress file: lower case, words joined by hyphens, never changed once given.</summary>
        public static string NameOf(this TutorialStep step) =>
            Names.TryGetValue(step, out string? name) ? name : throw new ArgumentOutOfRangeException(nameof(step), step, "A step the tutorial does not have.");

        /// <summary>The step a progress file names; nothing for a name of no step.</summary>
        public static TutorialStep? Find(string name) => All.Where(step => step.NameOf() == name).Select(step => (TutorialStep?)step).FirstOrDefault();

        /// <summary>True for the steps a board shows; the others are shown on a jigsaw, or on the card before a level.</summary>
        public static bool IsOnTheBoard(this TutorialStep step) =>
            step != TutorialStep.FirstPiece && step != TutorialStep.FirstPainting && step != TutorialStep.StartBoosters;

        /// <summary>The booster used during play a step teaches by hand; nothing for every other step.</summary>
        public static BoosterKind? BoosterOf(this TutorialStep step) => Boosters.TryGetValue(step, out BoosterKind booster) ? booster : (BoosterKind?)null;

        /// <summary>The step that teaches a booster used during play by hand.</summary>
        /// <exception cref="ArgumentException">The booster is taken before a level: no step teaches it on a board.</exception>
        public static TutorialStep Teaching(BoosterKind booster) =>
            Boosters.Where(each => each.Value == booster).Select(each => (TutorialStep?)each.Key).FirstOrDefault()
            ?? throw new ArgumentException($"{booster.Name()} is taken before a level; no step teaches it on a board.", nameof(booster));
    }

    /// <summary>The steps of the tutorial a player has been shown, each to its end. Immutable.</summary>
    public sealed class TutorialSeen : IEquatable<TutorialSeen>
    {
        private readonly int _steps;

        private TutorialSeen(int steps)
        {
            _steps = steps;
        }

        /// <summary>A player who has seen no step: a new player.</summary>
        public static TutorialSeen None { get; } = new TutorialSeen(0);

        /// <summary>
        /// A player who has seen every step: where the questions of the levels and the museum start, in which the
        /// tutorial plays no part (the tests' and the Editor's progress, which must play as they always did).
        /// </summary>
        public static TutorialSeen All { get; } = Of(TutorialSteps.All);

        /// <summary>The steps seen, in the order of the enumeration.</summary>
        public IReadOnlyList<TutorialStep> Steps => TutorialSteps.All.Where(Has).ToArray();

        public static TutorialSeen Of(IEnumerable<TutorialStep> steps) => new TutorialSeen(steps.Aggregate(0, (mask, step) => mask | BitOf(step)));

        public bool Has(TutorialStep step) => (_steps & BitOf(step)) != 0;

        /// <summary>The step has been shown to its end.</summary>
        public TutorialSeen With(TutorialStep step) => new TutorialSeen(_steps | BitOf(step));

        public bool Equals(TutorialSeen? other) => other is not null && other._steps == _steps;

        public override bool Equals(object? obj) => Equals(obj as TutorialSeen);

        public override int GetHashCode() => _steps;

        public override string ToString() => _steps == 0 ? "no step seen" : string.Join(", ", Steps.Select(TutorialSteps.NameOf));

        private static int BitOf(TutorialStep step) => 1 << (int)step;
    }
}
