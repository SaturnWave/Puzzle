# Moves the Unity project's large, rebuildable folders to another drive and leaves a junction in each one's place,
# so the system drive keeps room: the Android build's intermediates (Library\Bee, several GB), the build output and
# the renders. The import cache (the rest of Library) stays where it is: Unity reads it at every start and a slow
# drive would make every run slower.
#
# usage (from the repository root): .\tools\unity\Move-CachesToDrive.ps1 -Drive D
# Running it again is harmless: a folder that is already a junction is left alone. While the other drive is not
# connected, builds and renders fail; connect it again, or delete the junctions (Unity rebuilds the folders).
param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z]$')][string]$Drive)

$ErrorActionPreference = 'Stop'
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$cache = "${Drive}:\puzzle-cache"
$moves = @(
    @{ Source = Join-Path $repository 'unity\Library\Bee'; Target = Join-Path $cache 'unity-Library-Bee' },
    @{ Source = Join-Path $repository 'unity\Build';       Target = Join-Path $cache 'unity-Build' },
    @{ Source = Join-Path $repository 'out\unity';         Target = Join-Path $cache 'out-unity' }
)

foreach ($move in $moves) {
    $source = $move.Source
    $target = $move.Target
    $existing = Get-Item $source -ErrorAction SilentlyContinue
    if ($existing -and ($existing.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Write-Output "already a junction: $source"
        continue
    }

    New-Item -ItemType Directory -Force $target | Out-Null
    if ($existing) {
        # robocopy moves files and folders and removes the emptied source; exit codes below 8 mean success.
        robocopy $source $target /E /MOVE /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy could not move $source (exit code $LASTEXITCODE)" }
        if (Test-Path $source) { cmd /c rmdir "$source" }
    }

    cmd /c mklink /J "$source" "$target" | Out-Null
    $bytes = (Get-ChildItem $target -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    Write-Output ("{0} -> {1} ({2:N1} GB)" -f $source, $target, ($bytes / 1GB))
}

Get-PSDrive C, $Drive | ForEach-Object { Write-Output ("{0}: {1:N1} GB free" -f $_.Name, ($_.Free / 1GB)) }
