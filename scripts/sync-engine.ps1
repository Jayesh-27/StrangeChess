# sync-engine.ps1 — Copies engine source files from the reference repo into our project.
# Source (READ-ONLY): C:\Users\jayes\Desktop\StrangeChess\Engine\Engine\
# Target:             engine\StrangeChess.Engine\
# Never modifies the source repo.

$ErrorActionPreference = "Stop"

$source = "C:\Users\jayes\Desktop\StrangeChess\Engine\Engine"
$target = Join-Path $PSScriptRoot "..\engine\StrangeChess.Engine"

$files = @("Board.cs", "Chess.cs", "AI.cs", "Zobrist.cs", "Perft.cs")

if (-not (Test-Path $target)) {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Write-Host "Created target directory: $target"
}

foreach ($file in $files) {
    $src = Join-Path $source $file
    $dst = Join-Path $target $file
    if (-not (Test-Path $src)) {
        Write-Error "Source file not found: $src"
        exit 1
    }
    Copy-Item $src $dst -Force
    Write-Host "Copied $file"
}

Write-Host "`nEngine sync complete. Files are byte-identical copies of the originals."
