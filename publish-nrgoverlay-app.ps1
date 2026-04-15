param(
    [string] $Configuration = "Debug",
    [string] $Runtime = "win-x64",
    [string] $OutputDir = ".\out\NrgOverlay.App"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==> Killing stale dotnet processes..." -ForegroundColor Cyan
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "==> Cleaning publish folder: $OutputDir" -ForegroundColor Cyan
if (Test-Path $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "==> Publishing NrgOverlay.App ($Configuration, $Runtime)..." -ForegroundColor Cyan
dotnet publish .\src\NrgOverlay.App\NrgOverlay.App.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:Platform=x64 `
    -o $OutputDir

$exitCode = $LASTEXITCODE
Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "==> Publish succeeded." -ForegroundColor Green
    Write-Host "    EXE: $((Resolve-Path $OutputDir).Path)\NrgOverlay.App.exe" -ForegroundColor Green
} else {
    Write-Host "==> Publish failed (exit code: $exitCode)." -ForegroundColor Red
}

exit $exitCode
