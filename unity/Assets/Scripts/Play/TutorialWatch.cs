#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Flow;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;
using Puzzle.Meta;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// What one step of the tutorial shows on a board. A closed set (private constructor): a step that asks only to
    /// be looked at, which a touch anywhere ends; a move shown by hand, which only playing that move ends; and a
    /// booster of the shelf used by hand, which only that use ends.
    /// </summary>
    public abstract class Lesson
    {
        private Lesson(TutorialStep step)
        {
            Step = step;
        }

        public TutorialStep Step { get; }

        /// <summary>Something on the stage is pointed at and said what it is; a touch anywhere ends it.</summary>
        public sealed class Look : Lesson
        {
            public Look(TutorialStep step, LessonSpot spot)
                : base(step)
            {
                Spot = spot ?? throw new ArgumentNullException(nameof(spot));
            }

            public LessonSpot Spot { get; }

            public override string ToString() => $"{Step.NameOf()}: look at {Spot}";
        }

        /// <summary>A move is shown by hand; the board takes that move and no other until it is played.</summary>
        public sealed class Guided : Lesson
        {
            public Guided(TutorialStep step, Move move)
                : base(step)
            {
                Move = move;
            }

            public Move Move { get; }

            public override string ToString() => $"{Step.NameOf()}: play {Move}";
        }

        /// <summary>
        /// A booster of the shelf is used once by hand, free (#7): its disc and its target are lit, and the board takes
        /// that booster's press and that use and nothing else until the use is made. A brush is taken anywhere on the
        /// row or the column shown; the spatula only on the cell shown; the shuffle needs no aim.
        /// </summary>
        public sealed class Booster : Lesson
        {
            public Booster(TutorialStep step, BoosterUse use)
                : base(step)
            {
                Use = use ?? throw new ArgumentNullException(nameof(use));
                if (step.BoosterOf() != BoosterKinds.Of(use))
                {
                    throw new ArgumentException($"The step {step.NameOf()} does not teach {BoosterKinds.Of(use).Name()}.", nameof(use));
                }
            }

            /// <summary>The use shown: the cell, the row or the column aimed at, or the shuffle.</summary>
            public BoosterUse Use { get; }

            /// <summary>The booster whose disc is pressed.</summary>
            public BoosterKind Kind => BoosterKinds.Of(Use);

            public override string ToString() => $"{Step.NameOf()}: use {Use}";
        }
    }

    /// <summary>What a step that is looked at points at. A closed set (private constructor).</summary>
    public abstract class LessonSpot
    {
        private LessonSpot()
        {
        }

        /// <summary>The plaque that shows the level's goals.</summary>
        public static LessonSpot GoalPlaque { get; } = new Plaque("the goal plaque");

        /// <summary>The moves medallion between the plaques.</summary>
        public static LessonSpot MovesMedallion { get; } = new Plaque("the moves medallion");

        /// <summary>One of the plaques over the board.</summary>
        public sealed class Plaque : LessonSpot
        {
            private readonly string _name;

            internal Plaque(string name)
            {
                _name = name;
            }

            public override string ToString() => _name;
        }

        /// <summary>The disc of a booster on the board's shelf.</summary>
        public sealed class Disc : LessonSpot
        {
            public Disc(BoosterKind booster)
            {
                Booster = booster.IsInLevel() ? booster : throw new ArgumentException($"{booster.Name()} is taken before a level; the board's shelf has no disc for it.", nameof(booster));
            }

            public BoosterKind Booster { get; }

            public override string ToString() => $"the disc of {Booster.Name()}";
        }

        /// <summary>A cell of the board, and what lies in it.</summary>
        public sealed class Cell : LessonSpot
        {
            public Cell(Position at)
            {
                At = at;
            }

            public Position At { get; }

            public override string ToString() => $"the cell {At}";
        }
    }

    /// <summary>
    /// The tutorial on one board: which of its steps may still come, which one shows, and how it ends
    /// (docs/GAME_DESIGN.md §19). A step begins only at a quiet moment the stage tells of (at rest, taking a finger, no
    /// card, nothing armed), and only when the board offers what it teaches: the goal before the attempt has begun,
    /// the match shown by hand, the moves medallion once a move was played, the four and the five once the board
    /// offers such a move (the hint's advisor finds it, Puzzle.Bot's MoveLessons says what a move teaches), an
    /// obstacle where one lies on the board, a booster used during play once the engine would take it somewhere
    /// (<see cref="BoosterLessonAim"/> says where it is worth the most). Steps come in the plan's order: of those that
    /// may come, the first whose subject the board offers begins, and the others wait, saying nothing. While a move is
    /// shown by hand the board takes that move and no other; while a booster is used by hand it takes that booster's
    /// press and the use shown, free, and no move; a step that is looked at takes no move, and a touch anywhere ends it. A step that
    /// ends is done for good and is reported to whoever keeps the player's progress; a step cut short by leaving the
    /// board is not, and comes again. No Unity dependency: the repository's tests compile it too.
    /// </summary>
    public sealed class TutorialWatch
    {
        /// <summary>
        /// The player model of the bot family whose move a step shows by hand, among the moves that teach it: the
        /// expert, who sees every move and picks the best of them (forTheTutorial of the hint's hand-off).
        /// </summary>
        public const string ShownBy = "expert";

        private readonly List<TutorialStep> _mayCome;
        private readonly HintAdvisor _advisor;
        private readonly ulong _attemptSeed;
        private readonly List<TutorialStep> _done = new List<TutorialStep>();
        private readonly List<string> _guidedBoosters = new List<string>();

        /// <summary>The booster used by hand last on this board, whose disc the long press's step lights; nothing before one is.</summary>
        private BoosterKind? _lastUsedByHand;

        /// <param name="mayCome">The steps that may come on this board, in the order they are shown (<see cref="TutorialPlan.MayComeOn"/>).</param>
        /// <param name="advisor">Finds the move a step shows by hand.</param>
        /// <param name="attemptSeed">The attempt's play seed, which seeds the advisor as it does a hint.</param>
        public TutorialWatch(IReadOnlyList<TutorialStep> mayCome, HintAdvisor advisor, ulong attemptSeed)
        {
            if (mayCome.Any(step => !step.IsOnTheBoard()))
            {
                throw new ArgumentException($"Steps of the jigsaw do not come on a board: {string.Join(", ", mayCome)}.", nameof(mayCome));
            }

            _mayCome = mayCome.ToList();
            _advisor = advisor ?? throw new ArgumentNullException(nameof(advisor));
            _attemptSeed = attemptSeed;
        }

        /// <summary>
        /// The tutorial on the board of a level for a player who has seen these steps: the steps that may come there
        /// (<see cref="TutorialPlan.MayComeOn"/>), shown by <see cref="ShownBy"/>. Nothing when none may come.
        /// </summary>
        /// <param name="level">The level's number, from 1.</param>
        /// <param name="attemptSeed">The attempt's play seed (AttemptSeeds.Play).</param>
        /// <param name="unlocks">Where the boosters used during play open: each is used by hand on its level.</param>
        public static TutorialWatch? ForTheBoard(int level, TutorialSeen seen, ulong attemptSeed, BoosterUnlocks unlocks)
        {
            IReadOnlyList<TutorialStep> mayCome = TutorialPlan.MayComeOn(level, seen, unlocks);
            if (mayCome.Count == 0)
            {
                return null;
            }

            return HintAdvisor.TryCreate(ShownBy, out HintAdvisor? advisor)
                ? new TutorialWatch(mayCome, advisor, attemptSeed)
                : throw new InvalidOperationException($"The tutorial's moves are shown by \"{ShownBy}\", which is no player model of the bot family.");
        }

        /// <summary>The step that shows now; nothing while none does.</summary>
        public Lesson? Showing { get; private set; }

        /// <summary>The moves of the attempt that were shown by hand and played: what its record counts.</summary>
        public int GuidedMoves { get; private set; }

        /// <summary>
        /// The boosters of the attempt that were used by hand, free, by their record names (Puzzle.Core's BoosterNames),
        /// in the order they were used: what its record names, each one also among the boosters it used.
        /// </summary>
        public IReadOnlyList<string> GuidedBoosters => _guidedBoosters;

        /// <summary>The steps that may still come on this board, in their order.</summary>
        public IReadOnlyList<TutorialStep> MayStillCome => _mayCome;

        /// <summary>
        /// At a quiet moment of the board: unless a step shows already, the first step that may still come and that
        /// the board offers now begins and shows. Nothing begins when none is offered: those steps wait.
        /// </summary>
        /// <returns>The step that shows now; nothing while none does.</returns>
        public Lesson? BeginWhatIsDue(Game game)
        {
            if (Showing != null || game.Status != GameStatus.Playing)
            {
                return Showing;
            }

            foreach (TutorialStep step in _mayCome)
            {
                if (LessonOn(game, step) is Lesson lesson)
                {
                    Showing = lesson;
                    return lesson;
                }
            }

            return null;
        }

        /// <summary>
        /// True when the board may play this move now: any while nothing shows, only the one shown by hand while a move
        /// is (the trade of the same two pieces, from either of them), none while a step is looked at or a booster is
        /// used by hand.
        /// </summary>
        public bool Takes(Move move) => Showing switch
        {
            null => true,
            Lesson.Guided guided => IsTheMoveShown(guided, move),
            _ => false,
        };

        /// <summary>True when a booster's disc on the shelf may be pressed now: any while nothing shows, only the booster used by hand while one is, none while another step shows.</summary>
        public bool TakesThePress(BoosterKind booster) => Showing switch
        {
            null => true,
            Lesson.Booster lesson => lesson.Kind == booster,
            _ => false,
        };

        /// <summary>
        /// True when the board may use this booster now: any use while nothing shows; while a booster is used by hand,
        /// only the use shown (the spatula on its cell, a brush anywhere on its row or column, the shuffle); none while
        /// another step shows.
        /// </summary>
        public bool TakesTheUse(BoosterUse use) => Showing switch
        {
            null => true,
            Lesson.Booster lesson => IsTheUseShown(lesson.Use, use),
            _ => false,
        };

        /// <summary>True when this use is the one a step shows by hand, which is free: the wallet pays nothing for it.</summary>
        public bool IsFree(BoosterUse use) => Showing is Lesson.Booster lesson && IsTheUseShown(lesson.Use, use);

        /// <summary>The engine used a booster: the use shown by hand ends its step, and the attempt's record names it as guided.</summary>
        /// <returns>The step that is done now; nothing when none is.</returns>
        public TutorialStep? Used(BoosterUse use)
        {
            if (!(Showing is Lesson.Booster lesson) || !IsTheUseShown(lesson.Use, use))
            {
                return null;
            }

            _guidedBoosters.Add(BoosterNames.Of(use));
            _lastUsedByHand = lesson.Kind;
            return Finish(lesson);
        }

        /// <summary>A move was played: the move shown by hand ends its step.</summary>
        /// <returns>The step that is done now; nothing when none is.</returns>
        public TutorialStep? Played(Move move)
        {
            if (!(Showing is Lesson.Guided guided) || !IsTheMoveShown(guided, move))
            {
                return null;
            }

            GuidedMoves++;
            return Finish(guided);
        }

        /// <summary>A finger came down anywhere: a step that is looked at ends.</summary>
        /// <returns>The step that is done now; nothing when none is.</returns>
        public TutorialStep? Touched() => Showing is Lesson.Look look ? Finish(look) : (TutorialStep?)null;

        /// <summary>The steps done on this board since this was asked last, in the order they ended, each once.</summary>
        public IReadOnlyList<TutorialStep> TakeWhatWasDone()
        {
            TutorialStep[] done = _done.ToArray();
            _done.Clear();
            return done;
        }

        /// <summary>The move shown by hand, or the trade of its two pieces made from the other one: a finger may start at either.</summary>
        private static bool IsTheMoveShown(Lesson.Guided guided, Move move) =>
            guided.Move == move || (move.Kind == MoveKind.Swap && guided.Move.Kind == MoveKind.Swap && guided.Move.From == move.To && guided.Move.To == move.From);

        /// <summary>The use shown, or another of the same booster that strikes what it strikes: the spatula's cell, a brush's row or column, any shuffle.</summary>
        private static bool IsTheUseShown(BoosterUse shown, BoosterUse use) => (shown, use) switch
        {
            (BoosterUse.HitCell cell, BoosterUse.HitCell other) => cell.At == other.At,
            (BoosterUse.HitRow row, BoosterUse.HitRow other) => row.At.Y == other.At.Y,
            (BoosterUse.HitColumn column, BoosterUse.HitColumn other) => column.At.X == other.At.X,
            (BoosterUse.Shuffle _, BoosterUse.Shuffle _) => true,
            _ => false,
        };

        private TutorialStep Finish(Lesson lesson)
        {
            Showing = null;
            _mayCome.Remove(lesson.Step);
            _done.Add(lesson.Step);
            return lesson.Step;
        }

        /// <summary>What the step shows on the game as it stands; nothing when the board does not offer its subject now.</summary>
        private Lesson? LessonOn(Game game, TutorialStep step) => step switch
        {
            TutorialStep.Goal => AttemptBegun.In(game) ? null : new Lesson.Look(step, LessonSpot.GoalPlaque),
            TutorialStep.Match => Guide(game, step, MoveLesson.Match),
            TutorialStep.Moves => game.MovesUsed > 0 ? new Lesson.Look(step, LessonSpot.MovesMedallion) : null,
            TutorialStep.FourInALine => Guide(game, step, MoveLesson.FourInALine),
            TutorialStep.FiveInALine => Guide(game, step, MoveLesson.FiveInALine),
            TutorialStep.Box => Point(step, CellsOf(game.Board, cell => game.Board.TileAt(cell).Kind == TileKind.Box), game.Board),
            TutorialStep.Jelly => Point(step, CellsOf(game.Board, cell => game.Board.JellyAt(cell) > 0), game.Board),
            TutorialStep.Chain => Point(step, CellsOf(game.Board, cell => game.Board.ChainAt(cell) > 0), game.Board),
            TutorialStep.Item => Point(step, CellsOf(game.Board, cell => game.Board.TileAt(cell).Kind == TileKind.Item), game.Board),
            TutorialStep.BoosterHitCell or TutorialStep.BoosterHitRow or TutorialStep.BoosterHitColumn or TutorialStep.BoosterShuffle => Aim(game, step),
            TutorialStep.LongPress => _lastUsedByHand is BoosterKind used ? new Lesson.Look(step, new LessonSpot.Disc(used)) : null,
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "A step that does not come on a board."),
        };

        /// <summary>The booster's use where it is worth the most to the goals (<see cref="BoosterLessonAim"/>); nothing while the engine would take none.</summary>
        private Lesson? Aim(Game game, TutorialStep step)
        {
            BoosterKind booster = step.BoosterOf() ?? throw new ArgumentOutOfRangeException(nameof(step), step, "A step that teaches no booster.");
            return BoosterLessonAim.For(game, booster, _attemptSeed) is BoosterUse use ? new Lesson.Booster(step, use) : null;
        }

        private Lesson? Guide(Game game, TutorialStep step, MoveLesson lesson) =>
            _advisor.MoveFor(game, _attemptSeed, lesson) is Move move ? new Lesson.Guided(step, move) : null;

        /// <summary>The obstacle nearest the board's middle is pointed at (the first of them in reading order where two are as near); nothing for a board without one.</summary>
        private static Lesson? Point(TutorialStep step, IReadOnlyList<Position> cells, Board board)
        {
            if (cells.Count == 0)
            {
                return null;
            }

            float middleX = (board.Width - 1) / 2f;
            float middleY = (board.Height - 1) / 2f;
            Position nearest = cells.OrderBy(cell => (cell.X - middleX) * (cell.X - middleX) + (cell.Y - middleY) * (cell.Y - middleY)).First();
            return new Lesson.Look(step, new LessonSpot.Cell(nearest));
        }

        /// <summary>The playable cells of the board where something lies, in reading order.</summary>
        private static IReadOnlyList<Position> CellsOf(Board board, Func<Position, bool> holds)
        {
            var cells = new List<Position>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new Position(x, y);
                    if (board.Shape.IsPlayable(cell) && holds(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }
    }
}
