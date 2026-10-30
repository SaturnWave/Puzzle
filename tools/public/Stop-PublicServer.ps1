<#
.SYNOPSIS
    Stops the public server the way Ctrl+C in its window does, so it writes its pending records first.

.DESCRIPTION
    Start-PublicServer.ps1 runs the server in a console window; pressing Ctrl+C there is the normal way to stop it.
    This script does the same from another window: it finds the web app listening on the public port and sends
    Ctrl+C to the console that app runs in. The server, "dotnet run" and the start script all end, as they do when
    the key is pressed. Nothing is killed: when the server does not end in time, the script says so and stops.

    Players in the middle of a level keep their moves (they are saved as they are played); the level continues after
    the next start. Start again with Start-PublicServer.ps1.

    Works in Windows PowerShell 5.1 and in PowerShell 7.

.EXAMPLE
    .\tools\public\Stop-PublicServer.ps1
#>

param(
    # Internal: the process whose console gets the Ctrl+C. The script starts itself once more with this set,
    # because a process must leave its own console before it can attach to another one.
    [int] $SendCtrlCTo = 0
)

$ErrorActionPreference = 'Stop'

$Port = 5199
$ServerProcessName = 'Puzzle.Web'
$SecondsToWait = 30

function Send-CtrlC([int] $ProcessId) {
    Add-Type -Namespace PuzzleNative -Name ConsoleControl -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError = true)] public static extern bool FreeConsole();
[DllImport("kernel32.dll", SetLastError = true)] public static extern bool AttachConsole(uint processId);
[DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
[DllImport("kernel32.dll", SetLastError = true)] public static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);
'@

    $ctrlC = 0
    $wholeConsole = 0

    [void][PuzzleNative.ConsoleControl]::FreeConsole()
    if (-not [PuzzleNative.ConsoleControl]::AttachConsole([uint32] $ProcessId)) {
        exit 2
    }

    # This helper shares the console now and must not end from its own Ctrl+C before the event is out.
    [void][PuzzleNative.ConsoleControl]::SetConsoleCtrlHandler([IntPtr]::Zero, $true)
    if (-not [PuzzleNative.ConsoleControl]::GenerateConsoleCtrlEvent($ctrlC, $wholeConsole)) {
        exit 3
    }

    Start-Sleep -Seconds 2
    [void][PuzzleNative.ConsoleControl]::FreeConsole()
    exit 0
}

function Get-ServerProcessId {
    $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $listener) {
        return $null
    }

    $process = Get-Process -Id $listener.OwningProcess
    if ($process.ProcessName -ne $ServerProcessName) {
        Write-Host "Port $Port is held by '$($process.ProcessName)', not by the public server. Nothing was stopped." -ForegroundColor Red
        exit 1
    }

    return $process.Id
}

function Wait-UntilEnded([int] $ProcessId) {
    $deadline = (Get-Date).AddSeconds($SecondsToWait)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

if ($SendCtrlCTo -gt 0) {
    Send-CtrlC $SendCtrlCTo
}

$serverId = Get-ServerProcessId
if (-not $serverId) {
    Write-Host "No server is listening on port $Port."
    exit 0
}

$shell = (Get-Process -Id $PID).Path
$helper = Start-Process $shell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-SendCtrlCTo', $serverId -WindowStyle Hidden -PassThru -Wait
if ($helper.ExitCode -ne 0) {
    Write-Host "Ctrl+C could not be sent to the server's console (step $($helper.ExitCode)). Press Ctrl+C in the server's window instead." -ForegroundColor Red
    exit 1
}

if (Wait-UntilEnded $serverId) {
    Write-Host "The public server has stopped. Start it again with .\tools\public\Start-PublicServer.ps1"
    exit 0
}

Write-Host "The server did not stop within $SecondsToWait seconds. Press Ctrl+C in its window." -ForegroundColor Red
exit 1
