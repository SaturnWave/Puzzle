<#
.SYNOPSIS
    Starts the web app in the public mode on http://localhost:5199, the address Tailscale Funnel passes players on to.

.DESCRIPTION
    The server's own default is the lab, which is not for the internet. This script is the way to start the server
    that players reach from outside:

      1. It stops when the Release build is missing.
             Build:  dotnet build -c Release
      2. It lists the players' accounts and says which ready-made test accounts the server will make at its start:
         the ones src\Puzzle.Web\public-test-players.json lists that do not exist yet (an account that exists is
         left alone). It stops when there is no account and none to make.
             Make one by hand:  dotnet run --project src\Puzzle.Cli -c Release --no-build -- players add <username>
      3. It takes the owner key, which opens /lab, from the Windows user's environment variable Puzzle__AdminKey.
         The first time there is none: it makes a random one, stores it there and shows it, that once.
             Read it again:  [Environment]::GetEnvironmentVariable('Puzzle__AdminKey', 'User')
      4. It sets Puzzle__Mode=Public for this PowerShell process and starts the server. Ctrl+C stops it.

    The server listens on this machine only. Tailscale Funnel publishes it; run these in a second window:

             Open:     tailscale funnel --bg --https=8443 5199
             Address:  tailscale funnel status       (the https://...ts.net:8443 line is the players' address)
             Close:    tailscale funnel --https=8443 off

    The funnel stays open in the background until it is closed, also while the server is stopped. Do not close it
    with "tailscale funnel reset": that also closes whatever else this machine publishes.
    A Cloudflare Tunnel works as well:  cloudflared tunnel --url http://localhost:5199

    The owner key is never written into the repository. The ready-made accounts' passwords are in it on purpose:
    they are test passwords. Works in Windows PowerShell 5.1 and in PowerShell 7.

.EXAMPLE
    .\tools\public\Start-PublicServer.ps1
#>

$ErrorActionPreference = 'Stop'

$Port = 5199
$FunnelPort = 8443
$Address = "http://localhost:$Port"
$Repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# The test settings file of the public test: the ready-made accounts, with their test passwords.
$ReadyMadePlayersFile = Join-Path $Repository 'src\Puzzle.Web\public-test-players.json'

function Stop-WithHint([string] $Problem, [string] $Hint) {
    Write-Host $Problem -ForegroundColor Red
    Write-Host $Hint
    exit 1
}

function Assert-ReleaseBuild {
    foreach ($output in 'src\Puzzle.Web\bin\Release\*\Puzzle.Web.dll', 'src\Puzzle.Cli\bin\Release\*\puzzle.dll') {
        if (-not (Test-Path $output)) {
            Stop-WithHint "The Release build is missing: no $output." "Build it first, in ${Repository}:  dotnet build -c Release"
        }
    }
}

# The usernames the test settings file lists; none when there is no such file.
function Get-ReadyMadeUsernames {
    if (-not (Test-Path $ReadyMadePlayersFile)) {
        return @()
    }

    try {
        $listed = Get-Content -Raw -Encoding UTF8 $ReadyMadePlayersFile | ConvertFrom-Json
    }
    catch {
        # The parser's own message may quote the file, passwords and all.
        Stop-WithHint "The test settings file is not valid JSON: $ReadyMadePlayersFile" 'Fix it first; the server would not start with it either.'
    }

    return @($listed.players | ForEach-Object { $_.username } | Where-Object { $_ })
}

# Lists the accounts, says which ready-made accounts the server will make, and stops when nobody could sign in.
function Show-Accounts {
    $listing = @(& dotnet run --project src\Puzzle.Cli -c Release --no-build -- players list)
    if ($LASTEXITCODE -ne 0) {
        Stop-WithHint 'The accounts could not be listed.' 'The message above says why.'
    }

    $listing | Write-Host

    # After the count line and the heading line, each line of the listing starts with a username.
    $existing = @($listing | Select-Object -Skip 2 | ForEach-Object { ($_ -split '\s+')[0] } | Where-Object { $_ })
    $toMake = @(Get-ReadyMadeUsernames | Where-Object { $existing -notcontains $_ })
    if ($toMake.Count -gt 0) {
        Write-Host "The server will make these ready-made test accounts when it starts: $($toMake -join ', ')"
        Write-Host "    (listed, with their test passwords, in $ReadyMadePlayersFile)"
        return
    }

    if ($existing.Count -eq 0) {
        Stop-WithHint 'There is no account yet and the test settings file lists none, so nobody could sign in.' "Make one first, in ${Repository}:  dotnet run --project src\Puzzle.Cli -c Release --no-build -- players add <username>"
    }
}

function New-OwnerKey {
    # 32 bytes from the system's random generator, as 64 hex digits. Written this way for Windows PowerShell 5.1,
    # which has neither Get-SecureRandom nor the static RandomNumberGenerator.GetBytes.
    $bytes = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }

    return [System.BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}

function Show-NewOwnerKey([string] $Key) {
    Write-Host ''
    Write-Host 'A new owner key was made and stored for your Windows user (environment variable Puzzle__AdminKey).'
    Write-Host 'It opens /lab on the public address. It is shown this once - copy it now:'
    Write-Host ''
    Write-Host "    $Key" -ForegroundColor Yellow
    Write-Host ''
}

function Show-HowToPublish {
    Write-Host 'For the players, in a second window (Tailscale Funnel; it stays open in the background):'
    Write-Host "    tailscale funnel --bg --https=$FunnelPort $Port"
    Write-Host "    tailscale funnel status                   shows the players' address: https://...ts.net:$FunnelPort"
    Write-Host "    tailscale funnel --https=$FunnelPort off          closes it again"
}

Push-Location $Repository
try {
    Assert-ReleaseBuild
    Show-Accounts

    $ownerKey = [Environment]::GetEnvironmentVariable('Puzzle__AdminKey', 'User')
    if (-not $ownerKey) {
        $ownerKey = New-OwnerKey
        [Environment]::SetEnvironmentVariable('Puzzle__AdminKey', $ownerKey, 'User')
        Show-NewOwnerKey $ownerKey
    }

    $env:Puzzle__AdminKey = $ownerKey
    $env:Puzzle__Mode = 'Public'
    $env:Puzzle__ReadyMadePlayersPath = $ReadyMadePlayersFile # the file read above, so the server makes what was said
    $env:ASPNETCORE_ENVIRONMENT = 'Production' # whatever this machine sets for development: no developer pages, no user secrets

    Write-Host "Starting the public server on $Address (Ctrl+C stops it)."
    Show-HowToPublish
    & dotnet run --project src\Puzzle.Web -c Release --no-build --no-launch-profile -- --urls $Address
}
finally {
    Pop-Location
}

exit $LASTEXITCODE
