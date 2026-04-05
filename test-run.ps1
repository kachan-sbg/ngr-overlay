# test-run.ps1 — Build SimOverlay and launch with a clean test config.
# Run from repo root: .\test-run.ps1
# Optional flags:
#   -Configuration Release   (default: Debug)
#   -KeepConfig              (skip config wipe — use existing %APPDATA%\SimOverlay\config.json)

param(
    [string] $Configuration = "Debug",
    [switch] $KeepConfig
)

$repo    = $PSScriptRoot
$sln     = Join-Path $repo "SimOverlay.sln"
$exe     = Join-Path $repo "src\SimOverlay.App\bin\x64\$Configuration\net8.0-windows\SimOverlay.App.exe"
$cfgDir  = Join-Path $env:APPDATA "SimOverlay"
$cfgFile = Join-Path $cfgDir "config.json"

# ── 1. Build ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "==> Building $Configuration x64..." -ForegroundColor Cyan

$buildOutput = dotnet build $sln -c $Configuration -p:Platform=x64 --nologo 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "==> BUILD FAILED" -ForegroundColor Red
    Write-Host "--------------------------------------------------------------" -ForegroundColor Red
    $buildOutput | ForEach-Object { Write-Host $_ }
    Write-Host "--------------------------------------------------------------" -ForegroundColor Red
    Write-Host ""
    Write-Host "Fix the errors above, then re-run: .\test-run.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host "==> Build succeeded." -ForegroundColor Green

# ── 2. Config ────────────────────────────────────────────────────────────────
if ($KeepConfig) {
    Write-Host "==> Keeping existing config at $cfgFile" -ForegroundColor Yellow
} else {
    Write-Host "==> Resetting config at $cfgFile" -ForegroundColor Cyan
    if (Test-Path $cfgDir) { Remove-Item $cfgDir -Recurse -Force }
}

# ── 3. Launch ────────────────────────────────────────────────────────────────
Write-Host "==> Launching $exe"
Write-Host ""
& $exe
