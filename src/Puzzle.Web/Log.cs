using Puzzle.Core.Recording;

namespace Puzzle.Web;

internal static partial class Log
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Skipped level {Path}: {Problem}")]
    public static partial void LevelSkipped(ILogger logger, string path, string problem);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Loaded {Count} levels from {Path}")]
    public static partial void LevelsLoaded(ILogger logger, int count, string path);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Recording attempts to {Path}, which holds {Count} records")]
    public static partial void RecordingTo(ILogger logger, string path, int count);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Recorded attempt: {Result} on {Level} after {Moves} moves")]
    public static partial void AttemptRecorded(ILogger logger, AttemptResult result, string level, int moves);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "Could not write the record of an attempt on {Level} to {Path}; it waits for another try and the game goes on")]
    public static partial void AttemptNotWritten(ILogger logger, string level, string path, Exception exception);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Error, Message = "Could not build the record of an attempt on {Level}; the game goes on")]
    public static partial void AttemptNotBuilt(ILogger logger, string level, Exception exception);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "Skipped line {Line} of the attempt records: {Problem}")]
    public static partial void RecordSkipped(ILogger logger, int line, string problem);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Error, Message = "Could not read the attempt records at {Path} for the recordings panel")]
    public static partial void RecordsNotRead(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Information, Message = "Compared {Attempts} attempts with the bots ({Runs} games each) in {ElapsedMs} ms")]
    public static partial void BotsCompared(ILogger logger, int attempts, int runs, long elapsedMs);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Warning, Message = "Still cannot write to {Path}; {Count} attempt records wait")]
    public static partial void RecordsStillWaiting(ILogger logger, int count, string path, Exception exception);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Error, Message = "Gave up the record of an attempt on {Level}: {Max} records already wait for {Path}")]
    public static partial void WaitingRecordDropped(ILogger logger, string level, int max, string path);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Error, Message = "Lost {Count} attempt records that could not be written to {Path} before the lab stopped")]
    public static partial void WaitingRecordsLost(ILogger logger, int count, string path);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Warning, Message = "Refused a request that came through a tunnel or proxy (it carries {Header}): the lab mode serves direct requests only, and refuses every such request from now on without another line here. To serve players through a tunnel, start the server with Puzzle__Mode=Public (tools/public/Start-PublicServer.ps1 does)")]
    public static partial void LabRefusedRelayedRequest(ILogger logger, string header);

    // The public mode. Nothing here names a player: a username next to a player id in a log would be a second copy of
    // the mapping only the account store may hold, and what a failed sign-in typed may be a password in the wrong box.
    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Public mode: {Levels} levels from '{LevelSet}', {Accounts} accounts in {StorePath}, lab {Lab}")]
    public static partial void PublicModeStarted(ILogger logger, int levels, string levelSet, int accounts, string storePath, string lab);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "Public mode has no accounts in {StorePath}, so nobody can sign in: stop the server, run 'puzzle players add <username>' (or list ready-made test accounts in the file Puzzle:ReadyMadePlayersPath names) and start it again")]
    public static partial void PublicModeHasNoAccounts(ILogger logger, string storePath);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "A username is locked after {Failures} failed sign-ins in a row, for {Minutes} minutes")]
    public static partial void SignInLocked(ILogger logger, int failures, double minutes);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Warning, Message = "A sign-in to the lab was refused: wrong owner key")]
    public static partial void OwnerSignInRefused(ILogger logger);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Error, Message = "Could not write the player store for {Request}; the request was answered as not saved")]
    public static partial void PlayerChangeNotSaved(ILogger logger, string request, Exception exception);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Error, Message = "Could not save a player's moves or win to {Path}; they are kept and tried again: at the player's next request, once a minute, and when the server stops")]
    public static partial void ProgressNotSaved(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 1107, Level = LogLevel.Warning, Message = "Could not store a password hash at today's cost in {Path}; the old hash stays")]
    public static partial void PasswordHashNotRaised(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 1108, Level = LogLevel.Error, Message = "A level of the public set could not be started")]
    public static partial void PublicLevelNotStarted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1109, Level = LogLevel.Warning, Message = "Public mode listens on {Address}, which is not this machine alone: its sign-in cookies need HTTPS, and it believes the client address a local proxy names")]
    public static partial void PublicModeNotLocal(ILogger logger, string address);

    [LoggerMessage(EventId = 1110, Level = LogLevel.Error, Message = "Lost the moves or wins of {Players} players: {Path} could not be written before the server stopped. Their games go on from what the file holds")]
    public static partial void UnsavedProgressLost(ILogger logger, int players, string path);

    [LoggerMessage(EventId = 1111, Level = LogLevel.Warning, Message = "A player's saved attempt on level {Level} was dropped: the public set no longer has that level as the attempt knew it. The player starts the level afresh")]
    public static partial void SavedAttemptLevelChanged(ILogger logger, int level);

    [LoggerMessage(EventId = 1112, Level = LogLevel.Warning, Message = "A player's saved attempt on level {Level} was dropped: the game does not take its {Moves} saved moves. The player starts the level afresh")]
    public static partial void SavedAttemptNotReplayed(ILogger logger, int level, int moves);

    [LoggerMessage(EventId = 1113, Level = LogLevel.Error, Message = "A player's saved attempt on level {Level} was dropped: the level could not be started")]
    public static partial void SavedAttemptNotStarted(ILogger logger, int level, Exception exception);

    [LoggerMessage(EventId = 1114, Level = LogLevel.Information, Message = "Made {Made} of the {Listed} ready-made test accounts the test settings file lists (one that was there already is left as it is)")]
    public static partial void ReadyMadePlayersAdded(ILogger logger, int made, int listed);
}
