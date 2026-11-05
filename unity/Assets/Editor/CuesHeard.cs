#nullable enable
using System;
using Puzzle.Client.Flow;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// A cue player for the Editor's checks and walks: it plays nothing and counts what reached it, so that a check can
    /// say what the player would have heard and felt with the settings as they were (the session asks them before every
    /// cue). It holds one count per cue and per tick, however long it is used.
    /// </summary>
    public sealed class CuesHeard : ICuePlayer
    {
        private readonly int[] _heard = new int[Enum.GetValues(typeof(SoundCue)).Length];
        private readonly int[] _felt = new int[Enum.GetValues(typeof(Tick)).Length];

        /// <summary>How many sounds reached the player in all.</summary>
        public int SoundsInAll { get; private set; }

        /// <summary>How many ticks reached the player in all.</summary>
        public int TicksInAll { get; private set; }

        /// <summary>How many times a cue was heard.</summary>
        public int Of(SoundCue cue) => _heard[(int)cue];

        /// <summary>How many times the phone ticked so.</summary>
        public int Of(Tick tick) => _felt[(int)tick];

        public void Sound(SoundCue cue)
        {
            _heard[(int)cue]++;
            SoundsInAll++;
        }

        public void Buzz(Tick tick)
        {
            _felt[(int)tick]++;
            TicksInAll++;
        }

        public override string ToString() => $"{SoundsInAll} sounds and {TicksInAll} ticks";
    }
}
