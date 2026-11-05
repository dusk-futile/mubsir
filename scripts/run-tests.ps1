# What the owner means by "run tests" on Windows: set everything up, then run all the automatic checks and show the UI.
# Run as administrator from the repo root:  pwsh scripts/run-tests.ps1
# Results go to test-results\. The tests that need a person or real keys are in docs/WINDOWS-TEST.md.
$ErrorActionPreference = 'Continue'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$out = Join-Path $root 'test-results'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$summary = Join-Path $out 'summary.txt'
Set-Content $summary "Mubsir test run $(Get-Date -Format s)"

function Step($name, [scriptblock]$work) {
    Write-Host "== $name"
    & $work
    $result = if ($LASTEXITCODE -in 0, $null) { 'PASS' } else { "FAIL ($LASTEXITCODE)" }
    Add-Content $summary "$result  $name"
    Write-Host "$result  $name"
    $global:LASTEXITCODE = 0
}

function Need($command, $wingetId) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        Write-Host "Installing $wingetId"
        winget install --id $wingetId -e --silent --accept-package-agreements --accept-source-agreements
    }
}

Need dotnet Microsoft.DotNet.SDK.10
Need git Git.Git
Need python Python.Python.3.12
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
if (-not (Test-Path "$env:ProgramFiles\NVDA\nvda.exe") -and -not (Test-Path "${env:ProgramFiles(x86)}\NVDA\nvda.exe")) {
    winget install --id NVAccess.NVDA -e --silent --accept-package-agreements --accept-source-agreements
}

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($admin) {
    Add-WindowsCapability -Online -Name 'Language.OCR~~~ar-SA~0.0.1.0' | Out-Null
    Add-WindowsCapability -Online -Name 'Language.TextToSpeech~~~ar-EG~0.0.1.0' | Out-Null
} else {
    Add-Content $summary 'SKIP  Arabic OCR and speech: not running as administrator'
}

Step 'Get the latest code' { git pull --ff-only }
Step 'Core tests' { dotnet test tests/Mubsir.Core.Tests -c Release }
Step 'Build the app' { dotnet publish src/Mubsir.App -c Release -r win-x64 -o publish }
Step 'Build the NVDA add-on' { python scripts/build-addon.py }
Step 'Book check' {
    $p = Start-Process publish/Mubsir.exe -ArgumentList '--book-check', 'tests/fixtures/books' -Wait -PassThru
    if (Test-Path book-check.txt) { Move-Item -Force book-check.txt (Join-Path $out 'book-check.txt') }
    $global:LASTEXITCODE = $p.ExitCode
}
Step 'Notch and Hudhud' { pwsh scripts/overlay-smoke.ps1 -Exe publish/Mubsir.exe -Out (Join-Path $out 'overlay') }
Step 'Setup window' { pwsh scripts/setup-shots.ps1 -Exe publish/Mubsir.exe -Out (Join-Path $out 'setup') }

Get-Content $summary
Write-Host 'Showing the notch and Hudhud. Press Esc to close them.'
Start-Process publish/Mubsir.exe -ArgumentList '--overlay-demo'
Start-Process explorer.exe $out
