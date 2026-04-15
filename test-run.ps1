# test-run.ps1 - Build NrgOverlay and launch with a clean test config.
# Run from repo root: .\test-run.ps1
# Optional flags:
#   -Configuration Release   (default: Debug)
#   -KeepConfig              (skip config wipe - use existing %APPDATA%\NrgOverlay\config.json)

param(
    [string] $Configuration = "Debug",
    [switch] $KeepConfig
)

$repo    = $PSScriptRoot
$sln     = Join-Path $repo "NrgOverlay.sln"
$exe     = Join-Path $repo "src\NrgOverlay.App\bin\x64\$Configuration\net8.0-windows\NrgOverlay.App.exe"
$cfgDir  = Join-Path $env:APPDATA "NrgOverlay"
$cfgFile = Join-Path $cfgDir "config.json"

# в”Ђв”Ђ 1. Build в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

# в”Ђв”Ђ 2. Config в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
if ($KeepConfig) {
    Write-Host "==> Keeping existing config at $cfgFile" -ForegroundColor Yellow
} else {
    Write-Host "==> Resetting config at $cfgFile" -ForegroundColor Cyan
    if (Test-Path $cfgDir) { Remove-Item $cfgDir -Recurse -Force }
}

# в”Ђв”Ђ 3. Launch в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
Write-Host "==> Launching $exe"
Write-Host ""
& $exe


