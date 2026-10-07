#nullable enable
using System;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// How long things take on the board, in seconds. One place for all of them: the pace of the game is felt as a
    /// whole. A plain move is over in about half a second (a trade, a burst, a short fall); what a special piece
    /// does takes as long as its effect needs to cross the board. Falls are not here: they follow <see cref="FallLaw"/>.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class Tempo
    {
        /// <summary>Two pieces trade places.</summary>
        public const float Swap = 0.16f;

        /// <summary>A refused trade: this long out, this long back.</summary>
        public const float RefusedSwap = 0.13f;

        /// <summary>A matched or hit piece bursts.</summary>
        public const float Pop = 0.2f;

        /// <summary>Matched pieces are drawn into the cell where their match leaves a special piece.</summary>
        public const float Gather = 0.17f;

        /// <summary>The special piece of a match shows up this long after its match began to gather.</summary>
        public const float AppearAfterGather = 0.09f;

        /// <summary>A special piece pops into being. It may fall on before it has quite settled.</summary>
        public const float Appear = 0.28f;
        public const float AppearBeforeFalling = 0.17f;

        /// <summary>A special piece flares up and is gone; its effect starts a moment into that.</summary>
        public const float Fire = 0.14f;
        public const float FireLead = 0.04f;

        /// <summary>A crate breaks; a crate or a tape that holds gives under the hit; a tape or a slab layer comes off.</summary>
        public const float Break = 0.22f;
        public const float Bump = 0.22f;
        public const float Peel = 0.2f;

        /// <summary>A cell counts as empty this long after its piece was hit: pieces above start to fall while it still bursts.</summary>
        public const float CellFreeAfterHit = 0.1f;

        /// <summary>A piece follows the one under it this long after that one let go, so a column falls as a loose train.</summary>
        public const float FollowLag = 0.03f;

        /// <summary>A piece slips diagonally into a cell nothing can fall into.</summary>
        public const float Slip = 0.11f;

        /// <summary>A landed piece gives a little and stands up again. It does not hold the turn up.</summary>
        public const float Land = 0.16f;

        /// <summary>The eye gets this long to see the board at rest before a cascade's next match bursts.</summary>
        public const float PauseBeforeCascade = 0.05f;

        /// <summary>An item drops out through its column's exit.</summary>
        public const float Collect = 0.24f;

        /// <summary>A rocket's stroke takes this long per cell; a blast per ring of cells; the light of a whole-board clear per cell.</summary>
        public const float RocketPerCell = 0.028f;
        public const float BlastPerRing = 0.045f;
        public const float WavePerCell = 0.03f;

        /// <summary>An effect stays visible this long after it has reached its last cell.</summary>
        public const float EffectLinger = 0.26f;

        /// <summary>A seeker rises before it flies; flights of one seeker leave this long after each other.</summary>
        public const float SeekerRise = 0.1f;
        public const float FlightBase = 0.3f;
        public const float FlightPerCell = 0.022f;
        public const float FlightStagger = 0.07f;

        /// <summary>
        /// A colour bomb gathers light before its first ray; a ray takes <see cref="Ray"/> to arrive; rays follow each
        /// other at <see cref="RayEvery"/>, or closer when there are many, so that all of them leave within <see cref="RaysWithin"/>.
        /// </summary>
        public const float RayLead = 0.14f;
        public const float Ray = 0.12f;
        public const float RayEvery = 0.03f;
        public const float RaysWithin = 0.42f;

        /// <summary>The special pieces a colour bomb made go off one after another, this far apart.</summary>
        public const float ArmedEvery = 0.07f;

        /// <summary>A board without a move rests a moment, then its gems are dealt anew.</summary>
        public const float PauseBeforeReshuffle = 0.15f;
        public const float Reshuffle = 0.55f;

        /// <summary>
        /// A decided game: the board is seen at rest for a moment, then the room goes dark while the result's card
        /// comes down on its wires; what the card shows in its alcove pops up as the card settles, one after another.
        /// </summary>
        public const float PauseBeforeResult = 0.4f;
        public const float RoomDarkens = 0.22f;
        public const float CardComesDown = 0.44f;
        public const float CardAfterDark = 0.06f;
        public const float ExhibitsAfterCard = 0.3f;
        public const float ExhibitPop = 0.32f;
        public const float ExhibitEvery = 0.09f;

        /// <summary>The player went on: the card is drawn up out of sight; the next board then comes out of the dark.</summary>
        public const float CardGoesUp = 0.22f;
        public const float RoomLightens = 0.32f;

        /// <summary>A page of the museum is turned: the next one slides in from its side while the one shown slides out.</summary>
        public const float PageTurns = 0.3f;

        /// <summary>
        /// A jigsaw piece is picked up from the tray and grows to its size in the painting; let go near its place it
        /// drops in, and its edge lights up for a moment; let go anywhere else it goes back to the tray. The place
        /// it left in the tray is filled by the next piece that waited behind.
        /// </summary>
        public const float PieceLifts = 0.1f;
        public const float PieceDropsIn = 0.13f;
        public const float PieceGoesBack = 0.2f;
        public const float PieceLightsUp = 0.34f;
        public const float TrayRefills = 0.22f;

        /// <summary>
        /// All waiting pieces are placed for the player: each flies from the tray to its place in <see cref="PieceFlies"/>,
        /// one after another at <see cref="PiecesLeaveEvery"/>, or closer when there are many, so that the last
        /// one leaves within <see cref="PiecesLeaveWithin"/>.
        /// </summary>
        public const float PieceFlies = 0.36f;
        public const float PiecesLeaveEvery = 0.09f;
        public const float PiecesLeaveWithin = 1.3f;

        /// <summary>
        /// A painting is whole again: after a breath a light runs over it, the frame swells once, and sparkles
        /// burst around it one after another.
        /// </summary>
        public const float PauseBeforeRestored = 0.12f;
        public const float RestoredLight = 0.7f;
        public const float RestoredSwell = 0.4f;
        public const float SparkleBursts = 0.5f;
        public const float SparkleEvery = 0.07f;

        /// <summary>A painting of the collection is brought forward to be looked at, and put back.</summary>
        public const float PaintingComesForward = 0.24f;

        /// <summary>
        /// A tap or a swipe made while the board is still moving waits for the board to come to rest and is acted
        /// on then, if that is no later than this. Long enough for a player who is ahead of the last pieces coming
        /// down; a touch older than that was aimed at a board that has changed since, and is forgotten.
        /// </summary>
        public const float TouchWaits = 0.8f;

        /// <summary>The fastest a cascade gets: its bursts and pauses never take less than this part of their time.</summary>
        private const float FastestPace = 0.72f;

        /// <summary>Each round of a cascade is this much quicker than the one before.</summary>
        private const float QuickerPerRound = 0.07f;

        /// <summary>
        /// What the bursts and pauses of a cascade round are multiplied by: 1 for the player's own match, a little
        /// less for every round after it, so a long cascade picks up speed instead of dragging.
        /// </summary>
        public static float PaceOfRound(int round) => Math.Max(FastestPace, 1f - QuickerPerRound * round);
    }
}
