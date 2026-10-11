#!/usr/bin/env bash
# Builds and starts the dev build of Basira on the Windows test PC.
#
# Usage: scripts/dev/run.sh [host] [--no-pull] [--no-start]
#   host        ssh destination (default: $BASIRA_WINPC)
#   --no-pull   build what is already checked out on the PC
#   --no-start  build only; do not (re)start the BasiraDev task
#
# On the PC (repo: $BASIRA_WIN_REPO, default C:\dev\basira) it runs:
#   git pull --ff-only
#   stops the running dev build (task BasiraDev and any Basira.exe of this user)
#   dotnet build src/Basira.App -c Debug -o artifacts\dev
#   schtasks /run /tn BasiraDev
# Basira must start through the scheduled task: an SSH session cannot reach the desktop,
# keyboard hooks or audio. scripts/dev/setup-dev-pc.ps1 creates the task.
#
# Exit codes: 0 ok, 2 usage, 10 git pull failed, 11 build failed, 12 task failed, 255 ssh failed.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

host_arg=""
pull=1
start=1
for arg in "$@"; do
    case "$arg" in
        --no-pull) pull=0 ;;
        --no-start) start=0 ;;
        -h | --help)
            sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        -*) die "unknown option $arg" ;;
        *)
            [[ -z "$host_arg" ]] || die "only one host can be given"
            host_arg="$arg"
            ;;
    esac
done
host="$(resolve_host "$host_arg")"

script="\$ErrorActionPreference = 'Continue'
\$ProgressPreference = 'SilentlyContinue'
\$repo = $(ps_quote "$BASIRA_WIN_REPO")
if (-not (Test-Path -LiteralPath (Join-Path \$repo '.git'))) { Write-Host \"No git repository at \$repo (set BASIRA_WIN_REPO).\"; exit 2 }
Set-Location -LiteralPath \$repo
if ($pull) {
    # A build on Windows may rewrite NuGet lock files; drop that so the pull can fast-forward.
    \$dirty = @(git status --porcelain --untracked-files=no)
    \$locks = @(\$dirty | Where-Object { \$_ -match 'packages\.lock\.json\$' } | ForEach-Object { \$_.Substring(3) })
    if (\$locks.Count -gt 0 -and \$locks.Count -eq \$dirty.Count) {
        Write-Host '==> resetting NuGet lock files changed by the last build'
        git checkout -- @locks
    }
    Write-Host '==> git pull'
    git pull --ff-only
    if (\$LASTEXITCODE -ne 0) { Write-Host 'git pull failed (local changes on the PC, or no access to GitHub).'; exit 10 }
}
Write-Host '==> stopping the running dev build'
schtasks /end /tn BasiraDev 2>&1 | Out-Null
Get-Process -Name Basira -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
Write-Host '==> dotnet build src/Basira.App -c Debug'
dotnet build src/Basira.App -c Debug -o artifacts\\dev --nologo -v minimal
if (\$LASTEXITCODE -ne 0) { exit 11 }
if ($start) {
    Write-Host '==> starting task BasiraDev'
    schtasks /run /tn BasiraDev
    if (\$LASTEXITCODE -ne 0) { Write-Host 'Could not start task BasiraDev. Run scripts\\dev\\setup-dev-pc.ps1 once, and make sure the user is signed in.'; exit 12 }
}
exit 0"

echo "Building on $host ($BASIRA_WIN_REPO)..."
remote_ps "$host" "$script"
