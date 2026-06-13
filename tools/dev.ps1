# =============================================================================
# tools/dev.ps1 — clean dev loop helper
# -----------------------------------------------------------------------------
# A running app (dotnet run / VS debugger) keeps its output DLLs open, so the
# NEXT `dotnet build` fails with MSB3027/MSB3021 "file is locked by
# CanteenManagementSystem.Presentation (PID …)". This helper always frees those
# locks first, so build/run/test never trip over a stale instance.
#
# Usage (from repo root):
#   pwsh tools/dev.ps1 stop      # kill any running app instance
#   pwsh tools/dev.ps1 build     # stop, then build the solution
#   pwsh tools/dev.ps1 test      # stop, then run the full test suite
#   pwsh tools/dev.ps1 run       # stop, build, then run the app (foreground)
#   pwsh tools/dev.ps1 watch     # stop, then `dotnet watch run` (hot reload)
# =============================================================================
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('stop', 'build', 'test', 'run', 'watch')]
    [string]$Command = 'run'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$sln  = Join-Path $repo 'CanteenManagementSystem.sln'
$web  = Join-Path $repo 'src/CanteenManagementSystem.Presentation'

function Stop-App {
    $procs = Get-Process -Name 'CanteenManagementSystem.Presentation' -ErrorAction SilentlyContinue
    if ($procs) {
        $procs | ForEach-Object {
            Write-Host "Stopping running app (PID $($_.Id))…" -ForegroundColor Yellow
            Stop-Process -Id $_.Id -Force
        }
        Start-Sleep -Milliseconds 800   # let the OS release the file handles
    }
    else {
        Write-Host 'No running app instance.' -ForegroundColor DarkGray
    }
}

# Local-dev connection string default (LocalDB). Override by pre-setting the env var.
if (-not $env:ConnectionStrings__DefaultConnection) {
    $env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=SmartCanteen;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=SmartCanteen'
}

switch ($Command) {
    'stop'  { Stop-App }
    'build' { Stop-App; dotnet build $sln }
    'test'  { Stop-App; dotnet test  $sln }
    'run'   { Stop-App; dotnet build $sln; if ($LASTEXITCODE -eq 0) { dotnet run --project $web --no-build --launch-profile https } }
    'watch' { Stop-App; Push-Location $web; try { dotnet watch run } finally { Pop-Location } }
}
