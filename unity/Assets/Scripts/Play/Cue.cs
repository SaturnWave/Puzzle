#nullable enable
using System.Collections.Generic;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;

namespace Puzzle.Client.Play
{
    /// <summary>How a piece slides when the player trades two pieces.</summary>
    public enum SlideManner
    {
        /// <summary>The piece the player moved: it travels over the other one.</summary>
        Carried,

        /// <summary>The piece that makes room for it.</summary>
        Displaced,

        /// <summary>A special piece pushed onto another special piece: it stays there and both go off together.</summary>
        Onto,
    }

    /// <summary>How a piece goes from the board.</summary>
    public enum LeaveManner
    {
        /// <summary>A matched or hit piece bursts where it lies.</summary>
        Pop,

        /// <summary>A matched piece is drawn into the cell where its match leaves a special piece.</summary>
        Gather,

        /// <summary>A special piece goes off.</summary>
        Fire,

        /// <summary>A crate takes its last hit.</summary>
        Break,

        /// <summary>An item drops out of the board through its column's exit.</summary>
        Collect,
    }

    /// <summary>
    /// One thing the view does in the course of a turn, with the moment it begins (seconds after the player's move,
    /// or after the finale began) and how long it takes. A closed set: the private constructor keeps every kind
    /// inside this file. Cues are what the engine's events become once each has a time; the view only plays them
    /// and decides nothing about order. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public abstract class Cue
    {
        private Cue(float start, float seconds)
        {
            Start = start;
            Seconds = seconds;
        }

        public float Start { get; }

        public float Seconds { get; }

        public float End => Start + Seconds;

        /// <summary>The move is counted: the moves left drop by one.</summary>
        public sealed class MoveSpent : Cue
        {
            public MoveSpent(float start)
                : base(start, 0f)
            {
            }
        }

        /// <summary>One more of a goal is done: what is left of it drops by one, unless nothing is left.</summary>
        public sealed class GoalTick : Cue
        {
            public GoalTick(float start, int goalIndex)
                : base(start, 0f)
            {
                GoalIndex = goalIndex;
            }

            /// <summary>The goal's place in the level's list of goals.</summary>
            public int GoalIndex { get; }
        }

        /// <summary>A piece slides to a neighbouring cell because the player traded it.</summary>
        public sealed class Slide : Cue
        {
            public Slide(float start, float seconds, int pieceId, Position from, Position to, SlideManner manner)
                : base(start, seconds)
            {
                PieceId = pieceId;
                From = from;
                To = to;
                Manner = manner;
            }

            public int PieceId { get; }

            public Position From { get; }

            public Position To { get; }

            public SlideManner Manner { get; }
        }

        /// <summary>A piece falls straight down its column, by <see cref="FallLaw"/>.</summary>
        public sealed class Fall : Cue
        {
            public Fall(float start, float seconds, int pieceId, Position from, Position to, float landingSeconds)
                : base(start, seconds)
            {
                PieceId = pieceId;
                From = from;
                To = to;
                LandingSeconds = landingSeconds;
            }

            public int PieceId { get; }

            public Position From { get; }

            public Position To { get; }

            /// <summary>
            /// How long the piece gives under its own weight once it has arrived: <see cref="Tempo.Land"/> where it
            /// comes to rest, nothing when it travels on at once, and less than the whole when it travels on or
            /// leaves the board before it has stood up again: the give ends where the next thing the piece does
            /// begins. Nothing a piece does may go on after it has left.
            /// </summary>
            public float LandingSeconds { get; }

            public bool Lands => LandingSeconds > 0f;

            public Fall LandingFor(float seconds) => new Fall(Start, Seconds, PieceId, From, To, seconds);
        }

        /// <summary>A new piece falls into its column from above the column's top cell, by <see cref="FallLaw"/>.</summary>
        public sealed class Enter : Cue
        {
            public Enter(float start, float seconds, int pieceId, Tile tile, int fromRow, Position to, float landingSeconds)
                : base(start, seconds)
            {
                PieceId = pieceId;
                Tile = tile;
                FromRow = fromRow;
                To = to;
                LandingSeconds = landingSeconds;
            }

            public int PieceId { get; }

            public Tile Tile { get; }

            /// <summary>The row the piece starts in: above the column's top cell, so less than that cell's row and often negative.</summary>
            public int FromRow { get; }

            public Position To { get; }

            /// <summary>As <see cref="Fall.LandingSeconds"/>.</summary>
            public float LandingSeconds { get; }

            public bool Lands => LandingSeconds > 0f;

            public Enter LandingFor(float seconds) => new Enter(Start, Seconds, PieceId, Tile, FromRow, To, seconds);
        }

        /// <summary>A piece slips diagonally down into a cell that nothing can fall into from above.</summary>
        public sealed class Slip : Cue
        {
            public Slip(float start, float seconds, int pieceId, Position from, Position to, float landingSeconds)
                : base(start, seconds)
            {
                PieceId = pieceId;
                From = from;
                To = to;
                LandingSeconds = landingSeconds;
            }

            public int PieceId { get; }

            public Position From { get; }

            public Position To { get; }

            /// <summary>As <see cref="Fall.LandingSeconds"/>.</summary>
            public float LandingSeconds { get; }

            public bool Lands => LandingSeconds > 0f;

            public Slip LandingFor(float seconds) => new Slip(Start, Seconds, PieceId, From, To, seconds);
        }

        /// <summary>A special piece appears in a cell: left by a match, or made of a gem by a colour bomb or by a move of the finale.</summary>
        public sealed class Appear : Cue
        {
            public Appear(float start, float seconds, int pieceId, Tile tile, Position at)
                : base(start, seconds)
            {
                PieceId = pieceId;
                Tile = tile;
                At = at;
            }

            public int PieceId { get; }

            public Tile Tile { get; }

            public Position At { get; }
        }

        /// <summary>A piece goes from the board.</summary>
        public sealed class Leave : Cue
        {
            public Leave(float start, float seconds, int pieceId, Tile tile, Position at, LeaveManner manner, Position toward)
                : base(start, seconds)
            {
                PieceId = pieceId;
                Tile = tile;
                At = at;
                Manner = manner;
                Toward = toward;
            }

            public int PieceId { get; }

            /// <summary>What the piece was, for the colour of what flies off it.</summary>
            public Tile Tile { get; }

            /// <summary>The cell the piece belonged to. A special piece pushed onto another is seen in that other's cell.</summary>
            public Position At { get; }

            public LeaveManner Manner { get; }

            /// <summary>Where a gathered piece is drawn to; the piece's own cell in every other manner.</summary>
            public Position Toward { get; }
        }

        /// <summary>A crate that took a hit and still stands shows its new look.</summary>
        public sealed class Reface : Cue
        {
            public Reface(float start, float seconds, int pieceId, Tile tile, Position at)
                : base(start, seconds)
            {
                PieceId = pieceId;
                Tile = tile;
                At = at;
            }

            public int PieceId { get; }

            public Tile Tile { get; }

            public Position At { get; }
        }

        /// <summary>The tape over a cell takes a hit.</summary>
        public sealed class TapeHit : Cue
        {
            public TapeHit(float start, float seconds, Position at, int hitsLeft)
                : base(start, seconds)
            {
                At = at;
                HitsLeft = hitsLeft;
            }

            public Position At { get; }

            /// <summary>Zero when the tape is gone.</summary>
            public int HitsLeft { get; }
        }

        /// <summary>The slab on a cell's floor loses a layer.</summary>
        public sealed class SlabHit : Cue
        {
            public SlabHit(float start, float seconds, Position at, int layersLeft)
                : base(start, seconds)
            {
                At = at;
                LayersLeft = layersLeft;
            }

            public Position At { get; }

            /// <summary>Zero when the floor is clean.</summary>
            public int LayersLeft { get; }
        }

        /// <summary>A special piece's effect, as a whole: the stroke of a rocket, the splash of a bomb, the light of a colour bomb.</summary>
        public sealed class Effect : Cue
        {
            public Effect(float start, float seconds, EffectKind kind, Position origin, int radius, float secondsPerCell)
                : base(start, seconds)
            {
                Kind = kind;
                Origin = origin;
                Radius = radius;
                SecondsPerCell = secondsPerCell;
            }

            public EffectKind Kind { get; }

            public Position Origin { get; }

            /// <summary>The blast's radius, or half the width of a cross's bands, in cells.</summary>
            public int Radius { get; }

            /// <summary>How long the effect takes to reach one cell further from its origin; the pieces it hits go at that pace.</summary>
            public float SecondsPerCell { get; }
        }

        /// <summary>A seeker flies from where it went off to the cell it picked.</summary>
        public sealed class Flight : Cue
        {
            public Flight(float start, float seconds, Position from, Position to)
                : base(start, seconds)
            {
                From = from;
                To = to;
            }

            public Position From { get; }

            public Position To { get; }
        }

        /// <summary>A ray of a colour bomb's light, from the bomb to one of the cells it reaches.</summary>
        public sealed class Zap : Cue
        {
            public Zap(float start, float seconds, Position from, Position to)
                : base(start, seconds)
            {
                From = from;
                To = to;
            }

            public Position From { get; }

            public Position To { get; }
        }

        /// <summary>The board had no move left: its free gems are dealt anew.</summary>
        public sealed class Reshuffle : Cue
        {
            public Reshuffle(float start, float seconds, IReadOnlyList<PlacedPiece> dealt)
                : base(start, seconds)
            {
                Dealt = dealt;
            }

            /// <summary>Every piece that was dealt, with the cell it lies in and what it shows from now on.</summary>
            public IReadOnlyList<PlacedPiece> Dealt { get; }
        }

        /// <summary>
        /// The tool of a booster used during play: the spatula comes down onto its cell, strikes it and lifts away;
        /// the brush comes in over the board's edge and paints its row or column. The hits come by cues of their own,
        /// as the tool reaches each cell. No move is spent.
        /// </summary>
        public sealed class ToolUsed : Cue
        {
            public ToolUsed(float start, float seconds, BoosterUse booster, float strikesAt)
                : base(start, seconds)
            {
                Booster = booster;
                StrikesAt = strikesAt;
            }

            /// <summary>The booster, with the cell it was aimed at.</summary>
            public BoosterUse Booster { get; }

            /// <summary>When the tool reaches the board: the spatula strikes its cell, the brush its row's or column's first cell.</summary>
            public float StrikesAt { get; }
        }

        /// <summary>The finale of a won game opens: light widens at the moves medallion.</summary>
        public sealed class FinaleOpens : Cue
        {
            public FinaleOpens(float start, float seconds)
                : base(start, seconds)
            {
            }
        }

        /// <summary>
        /// A move left over leaves the medallion for a cell, where a gem turns into a rocket as it arrives. The
        /// moves left drop by one as it leaves.
        /// </summary>
        public sealed class MoveThrown : Cue
        {
            public MoveThrown(float start, float seconds, Position to)
                : base(start, seconds)
            {
                To = to;
            }

            /// <summary>The cell of the gem the move lands on.</summary>
            public Position To { get; }
        }
    }

    /// <summary>
    /// A turn as the view plays it, or the finale of a won game: its cues in the order they begin, and when the
    /// last of them is over.
    /// </summary>
    public sealed class TurnScript
    {
        /// <param name="showSeconds">Only a finale's script has it; see <see cref="ShowSeconds"/>.</param>
        public TurnScript(IReadOnlyList<Cue> cues, float seconds, float? showSeconds = null)
        {
            Cues = cues;
            Seconds = seconds;
            ShowSeconds = showSeconds;
        }

        public IReadOnlyList<Cue> Cues { get; }

        /// <summary>
        /// From the player's move, or from the finale's beginning, to the end of the last cue. The give of a piece
        /// that has landed is not counted.
        /// </summary>
        public float Seconds { get; }

        /// <summary>
        /// In a finale's script, how long its show takes: from its beginning until the board has come down after
        /// the last of its rockets. What the pieces that fell in set off after that is the finale's aftermath,
        /// which may be played faster (<see cref="FinalePace"/>). Nothing in a turn's script, and in a finale's
        /// that has no aftermath: all of it is then its show.
        /// </summary>
        public float? ShowSeconds { get; }
    }
}
