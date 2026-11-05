#nullable enable
using System;
using System.Globalization;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// The time of day as the player's phone tells it, for the rules that count real time: when a lost life comes
    /// back, and which calendar day an ad belongs to. It is a seam, so the Editor's tools and the tests say what time
    /// it is instead of reading the machine's. Every clock of the game hands out whole seconds: the save file keeps
    /// instants in whole seconds (<see cref="ProgressDocument"/>), so an instant that came from a clock, and every
    /// instant the rules count on from it in whole minutes, reads back from the file exactly as it was.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public interface IWallClock
    {
        /// <summary>Now, to the whole second, with the offset of the player's time zone: a calendar day is the player's own (Puzzle.Meta.DayStamp).</summary>
        DateTimeOffset Now { get; }
    }

    /// <summary>The device's own clock, in the device's time zone: the clock of the running game.</summary>
    public sealed class SystemWallClock : IWallClock
    {
        public DateTimeOffset Now
        {
            get
            {
                DateTimeOffset now = DateTimeOffset.Now;
                return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
            }
        }
    }

    /// <summary>A clock that always tells the same time: for the Editor's tools and the tests, whose pictures and checks must not depend on the hour they run at.</summary>
    public sealed class FixedWallClock : IWallClock
    {
        /// <exception cref="ArgumentException">The time has a fraction of a second, which no clock of the game hands out.</exception>
        public FixedWallClock(DateTimeOffset now)
        {
            if (now.Ticks % TimeSpan.TicksPerSecond != 0)
            {
                throw new ArgumentException($"A clock of the game tells whole seconds; {now.ToString("O", CultureInfo.InvariantCulture)} is not one.", nameof(now));
            }

            Now = now;
        }

        public DateTimeOffset Now { get; }
    }
}
