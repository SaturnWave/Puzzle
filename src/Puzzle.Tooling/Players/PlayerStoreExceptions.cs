namespace Puzzle.Tooling.Players;

/// <summary>The player store's file is not a valid store: not JSON, a missing or invalid field, or a newer schema.</summary>
public sealed class PlayerStoreFormatException : Exception
{
    public PlayerStoreFormatException(string message)
        : base(message)
    {
    }

    public PlayerStoreFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Another process holds the player store open for writing: the public server, or a <c>puzzle players add</c>, <c>password</c> or <c>delete</c>.</summary>
public sealed class PlayerStoreInUseException : Exception
{
    public PlayerStoreInUseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
