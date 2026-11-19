# demo-data.ps1 — reset the D-07 demo catalogs and save folders to one defined start state.
#
# Everything lives OUTSIDE the repository, under %TEMP%\SavegameBunkerDemo by default, so no demo
# run can touch the default src/SavegameBunker/games.json or the owner's real backup folders.
# Every capture run (capture.ps1) calls this script first, so before/after-19-20/after-21 sets
# all start from the same rows and archive state.
#
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File demo-data.ps1 [-Root <path>]
#
# The reset deletes everything inside the root before recreating it, so the script refuses
# (exit non-zero, nothing deleted) unless:
#   - the root's leaf name is exactly 'SavegameBunkerDemo', and
#   - the root lies outside this repository.
# The guard runs before any deletion and before anything is created.

param(
    [string]$Root = (Join-Path $env:TEMP 'SavegameBunkerDemo')
)

$ErrorActionPreference = 'Stop'

# --- Guard: leaf name must be exactly SavegameBunkerDemo ---------------------------------------

$leaf = [System.IO.Path]::GetFileName($Root.TrimEnd('\', '/'))
if ($leaf -ne 'SavegameBunkerDemo') {
    Write-Error "Refusing to reset: the root's leaf name must be exactly 'SavegameBunkerDemo' (got '$leaf'). Nothing was deleted or created."
    exit 1
}

# --- Guard: the root must lie outside this repository ------------------------------------------

# The script sits at <repo>/.planning/phases/02-audit-follow-ups/screenshots/. Walk up from here
# until a directory holding a .git entry is found; that is the repository root.
$repoRoot = $null
$dir = $PSScriptRoot
while ($dir -and -not (Test-Path (Join-Path $dir '.git'))) {
    $parent = Split-Path -Parent $dir
    if ($parent -eq $dir) { $dir = $null; break }
    $dir = $parent
}
if ($dir) { $repoRoot = $dir }

if ($repoRoot) {
    $fullRoot = [System.IO.Path]::GetFullPath($Root)
    $fullRepo = [System.IO.Path]::GetFullPath($repoRoot)
    if ($fullRoot.StartsWith($fullRepo, [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Error "Refusing to reset: '$Root' lies inside the repository ($fullRepo). Nothing was deleted or created."
        exit 1
    }
}

# --- Reset: delete and recreate the demo root ---------------------------------------------------

# The reset deletes inside the root selectively: catalogs, backups and the small save folders
# are wiped and recreated, but the Large Save Game's 1 GiB file is KEPT when it already exists
# with exactly that size — a re-run must not spend the seconds (nor shift the bytes) of
# regenerating it. Everything else is rebuilt, so the start state stays one defined state.
$largeFileProbe = Join-Path (Join-Path $Root 'saves\Large Save Game') 'world.sav'
$keepLargeFile = (Test-Path -LiteralPath $largeFileProbe) -and (Get-Item -LiteralPath $largeFileProbe).Length -eq 1GB

if (Test-Path -LiteralPath $Root) {
    Get-ChildItem -LiteralPath $Root -Force | Remove-Item -Recurse -Force
}

$saves     = Join-Path $Root 'saves'     # the demo save folders the catalog points at
$backups   = Join-Path $Root 'backups'   # backupRoot of both catalogs (relative -> beside the catalog)
$largeDir  = Join-Path $saves 'Large Save Game'
$smallDir  = Join-Path $saves 'Small Save Game'
$neverDir  = Join-Path $saves 'Never Backed Up'

New-Item -ItemType Directory -Path $largeDir, $smallDir, $neverDir, $backups -Force | Out-Null

# Forward slashes in every catalog path — the form the seeded template shows (SPEC: Data Model).
# Paths are absolute because the demo save folders live in %TEMP%, not under any known folder.
$savesFwd    = ($saves    -replace '\\', '/')
$largeDirFwd = ($largeDir -replace '\\', '/')
$smallDirFwd = ($smallDir -replace '\\', '/')
$neverDirFwd = ($neverDir -replace '\\', '/')

# --- Large Save Game: one 1 GiB file of random bytes, so a backup or restore runs long enough
#     to photograph. An existing file of exactly that size is kept instead of rewritten, so a
#     re-run of this script does not spend the time (and does not change the file the last run
#     already backed up in an interactive session).
$largeFile = Join-Path $largeDir 'world.sav'
$largeSize = 1GB
if (-not $keepLargeFile) {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $buffer = New-Object byte[] (1MB)
    $stream = [System.IO.File]::Create($largeFile)
    try {
        for ($i = 0; $i -lt ($largeSize / $buffer.Length); $i++) {
            $rng.GetBytes($buffer)
            $stream.Write($buffer, 0, $buffer.Length)
        }
    } finally {
        $stream.Dispose()
        $rng.Dispose()
    }
}

# --- Small Save Game: three small files and one subfolder ---------------------------------------
Set-Content -Path (Join-Path $smallDir 'settings.cfg')  -Value 'volume=80' -Encoding Ascii
Set-Content -Path (Join-Path $smallDir 'progress.dat')  -Value 'level=7'   -Encoding Ascii
Set-Content -Path (Join-Path $smallDir 'controls.cfg')  -Value 'jump=space' -Encoding Ascii
$subDir = Join-Path $smallDir 'screenshots'
New-Item -ItemType Directory -Path $subDir -Force | Out-Null
Set-Content -Path (Join-Path $subDir 'shot1.png') -Value 'not really a png' -Encoding Ascii

# --- Never Backed Up: one small file ------------------------------------------------------------
Set-Content -Path (Join-Path $neverDir 'profile.dat') -Value 'fresh install' -Encoding Ascii

# --- The catalogs --------------------------------------------------------------------------------
# Four games: one large (long-running operations), one small with a subfolder, one never backed
# up, one whose save folder does not exist (its row shows the error-severity text).
$gamesJson = @'
{
  "backupRoot": "backups",
  "games": [
    {
      "id": "large-save-game",
      "displayName": "Large Save Game",
      "saveDirTemplate": "__LARGE__"
    },
    {
      "id": "small-save-game",
      "displayName": "Small Save Game",
      "saveDirTemplate": "__SMALL__"
    },
    {
      "id": "never-backed-up",
      "displayName": "Never Backed Up",
      "saveDirTemplate": "__NEVER__"
    },
    {
      "id": "missing-folder",
      "displayName": "Missing Folder",
      "saveDirTemplate": "__MISSING__"
    }
  ]
}
'@

$missingDirFwd = ($savesFwd + '/Missing Folder')
$gamesJson = $gamesJson.Replace('__LARGE__', $largeDirFwd)
$gamesJson = $gamesJson.Replace('__SMALL__', $smallDirFwd)
$gamesJson = $gamesJson.Replace('__NEVER__', $neverDirFwd)
$gamesJson = $gamesJson.Replace('__MISSING__', $missingDirFwd)
# Re-assert forward slashes in the emitted catalog (the plan requires them everywhere).
$gamesJson = $gamesJson.Replace('\\', '/')

Set-Content -Path (Join-Path $Root 'games.json') -Value $gamesJson -Encoding Ascii

$emptyJson = @'
{
  "backupRoot": "backups",
  "games": []
}
'@
Set-Content -Path (Join-Path $Root 'empty.json') -Value $emptyJson -Encoding Ascii

# --- Emptied backups folder ---------------------------------------------------------------------
Get-ChildItem -LiteralPath $backups -Force | Remove-Item -Recurse -Force

Write-Host "Demo data reset at $Root"
Write-Host "  games.json : 4 games (Large Save Game 1 GiB, Small Save Game, Never Backed Up, Missing Folder)"
Write-Host "  empty.json : 0 games"
Write-Host "  backups/   : emptied"
exit 0
