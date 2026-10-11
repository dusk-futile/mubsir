#!/usr/bin/env bash
# Copies the newest Basira log files from the Windows test PC to scripts/dev/.devlogs/.
#
# Usage: scripts/dev/logs.sh [host] [-n COUNT]
#   host      ssh destination (default: $BASIRA_WINPC)
#   -n COUNT  how many of the newest files to fetch (default: 5)
#
# Logs live in %LOCALAPPDATA%\Basira\logs on the PC. They hold metadata only (event types,
# timings, token counts, error codes), never speech text or transcripts, but .devlogs/ is still
# git-ignored.
#
# Exit codes: 0 ok (also when there are no logs yet), 2 usage, 1 copy failed, 255 ssh failed.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

host_arg=""
count=5
while (($# > 0)); do
    case "$1" in
        -n)
            [[ $# -ge 2 && "$2" =~ ^[0-9]+$ && "$2" -gt 0 ]] || die "-n needs a positive number"
            count="$2"
            shift
            ;;
        -h | --help)
            sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        -*) die "unknown option $1" ;;
        *)
            [[ -z "$host_arg" ]] || die "only one host can be given"
            host_arg="$1"
            ;;
    esac
    shift
done
host="$(resolve_host "$host_arg")"

dest="$DEV_DIR/.devlogs"
mkdir -p "$dest"
# Belt and braces: never let these files be committed, even if the folder is moved.
[[ -f "$dest/.gitignore" ]] || printf '*\n' >"$dest/.gitignore"

# Lists the newest files as forward-slash paths that scp understands (C:/Users/...).
list_script="\$dir = Join-Path \$env:LOCALAPPDATA 'Basira\\logs'
if (-not (Test-Path -LiteralPath \$dir)) { exit 0 }
Get-ChildItem -LiteralPath \$dir -File | Sort-Object LastWriteTime -Descending | Select-Object -First $count |
    ForEach-Object { \$_.FullName.Replace('\\', '/') }"

listing="$(remote_ps "$host" "$list_script")"
files=()
while IFS= read -r line; do
    line="${line%$'\r'}"
    if [[ -n "$line" ]]; then files+=("$line"); fi
done <<<"$listing"

if ((${#files[@]} == 0)); then
    echo "No log files on $host yet (%LOCALAPPDATA%\\Basira\\logs is empty or missing)."
    exit 0
fi

copied=0
failed=0
for remote in "${files[@]}"; do
    if scp -q -o ConnectTimeout=10 "$host:$remote" "$dest/"; then
        echo "  ${remote##*/}"
        copied=$((copied + 1))
    else
        echo "  failed: $remote" >&2
        failed=$((failed + 1))
    fi
done
echo "Copied $copied file(s) to $dest"
((failed == 0)) || exit 1
