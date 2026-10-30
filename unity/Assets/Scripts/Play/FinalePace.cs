#nullable enable
using System;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// How fast the script of a won game's finale is played against the stage's clock. A finale has two parts. Its
    /// show is what the moves left over do: they fly, the rockets fire, the board comes down. Its aftermath is what
    /// the pieces that fell in set off after that, which is luck: nothing, or cascades for seconds. The show is
    /// played as it is written. The aftermath is played faster when the finale would otherwise take longer than its
    /// moves may (<see cref="Tempo.FinaleMayTake"/>), up to <see cref="Tempo.FinaleAftermathFastest"/> times; past
    /// that a finale takes longer than it should rather than rush. But no finale is shown for longer than
    /// <see cref="Tempo.FinaleLongest"/>, whatever the engine did in it: one that would be is played faster in both
    /// parts until it is not. Nothing of a script is left out for any of this.
    /// </summary>
    /// <remarks>
    /// The aftermath does not change its pace between two frames: it picks its pace up over the first
    /// <see cref="Tempo.FinaleHurriesWithin"/> of it, evenly, which the eye takes for a cascade gathering speed
    /// as cascades do. That costs a few hundredths of a second of what the hurry saves, so a hurried finale is
    /// over that much after the time its moves may take. The longest time is kept exactly.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </remarks>
    public sealed class FinalePace
    {
        private readonly float _showSeconds;
        private readonly float _aftermathSeconds;

        private FinalePace(float showSeconds, float aftermathSeconds, float ofTheShow, float ofTheAftermath)
        {
            _showSeconds = showSeconds;
            _aftermathSeconds = aftermathSeconds;
            OfTheShow = ofTheShow;
            OfTheAftermath = ofTheAftermath;
        }

        /// <summary>How much faster than written the show is played: 1 but for a finale that would outlast the longest time.</summary>
        public float OfTheShow { get; }

        /// <summary>How much faster than written the aftermath is played once it has picked its pace up: the show's pace when the finale is over in the time its moves may take.</summary>
        public float OfTheAftermath { get; }

        /// <summary>How long the finale is shown on the stage's clock.</summary>
        public float ShownSeconds => _showSeconds / OfTheShow + AftermathShownSeconds;

        /// <summary>The pace of a finale's script in which so many moves are thrown.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The count of moves is negative.</exception>
        public static FinalePace Of(TurnScript script, int moves)
        {
            if (moves < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(moves), moves, "A finale throws no fewer moves than none.");
            }

            float showSeconds = script.ShowSeconds ?? script.Seconds;
            float aftermathSeconds = script.Seconds - showSeconds;
            float roomForTheAftermath = Tempo.FinaleMayTake(moves) - showSeconds;
            float toBeOverInTime = roomForTheAftermath > 0f ? aftermathSeconds / roomForTheAftermath : Tempo.FinaleAftermathFastest;
            float hurry = Math.Clamp(toBeOverInTime, 1f, Tempo.FinaleAftermathFastest);

            // Whatever is still too long for the longest time is played faster as a whole: a finale played so
            // many times faster in both parts is shown for exactly that part of its time.
            float shown = new FinalePace(showSeconds, aftermathSeconds, 1f, hurry).ShownSeconds;
            float both = Math.Max(1f, shown / Tempo.FinaleLongest);
            return new FinalePace(showSeconds, aftermathSeconds, both, hurry * both);
        }

        /// <summary>
        /// How much faster than the stage's clock the script runs at a moment of it, counted in the script's own
        /// seconds: the show's pace up to the end of the show, then rising evenly to the aftermath's.
        /// </summary>
        public float At(float scriptSeconds)
        {
            float intoTheAftermath = scriptSeconds - _showSeconds;
            if (intoTheAftermath <= 0f)
            {
                return OfTheShow;
            }

            float pickedUp = Math.Min(1f, intoTheAftermath / Tempo.FinaleHurriesWithin);
            return OfTheShow + (OfTheAftermath - OfTheShow) * pickedUp;
        }

        /// <summary>
        /// How long the aftermath is shown: the stretch in which it picks its pace up, and the rest at that pace.
        /// A stretch of the script played at a pace that rises evenly from one pace to another takes the
        /// stretch's length times ln(to / from) / (to - from).
        /// </summary>
        private float AftermathShownSeconds
        {
            get
            {
                float gain = OfTheAftermath - OfTheShow;
                if (gain <= 0f)
                {
                    return _aftermathSeconds / OfTheShow;
                }

                float picksUpFor = Math.Min(_aftermathSeconds, Tempo.FinaleHurriesWithin);
                float reaches = OfTheShow + gain * picksUpFor / Tempo.FinaleHurriesWithin;
                float pickingUp = Tempo.FinaleHurriesWithin * (float)Math.Log(reaches / OfTheShow) / gain;
                return pickingUp + (_aftermathSeconds - picksUpFor) / OfTheAftermath;
            }
        }
    }
}
