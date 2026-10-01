using System;

namespace Puzzle.Core.Engine
{
    /// <summary>
    /// A level parsed fine but no valid starting board can be built from it (e.g. its preset tiles already
    /// contain a match, or no arrangement offers enough legal moves). The level generator rejects such levels.
    /// </summary>
    public sealed class LevelInitializationException : Exception
    {
        public LevelInitializationException(string message)
            : base(message)
        {
        }
    }
}
