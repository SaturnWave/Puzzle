using System;

namespace Puzzle.Core.Levels
{
    /// <summary>A level description is malformed or internally inconsistent.</summary>
    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(string message)
            : base(message)
        {
        }

        public LevelFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
