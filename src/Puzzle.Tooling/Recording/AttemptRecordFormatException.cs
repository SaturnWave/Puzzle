namespace Puzzle.Tooling.Recording;

/// <summary>A line of an attempt log is not a valid attempt record.</summary>
public sealed class AttemptRecordFormatException : Exception
{
    public AttemptRecordFormatException(string message)
        : base(message)
    {
    }

    public AttemptRecordFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
