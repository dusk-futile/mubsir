#!/usr/bin/env bash
# Sends one command to a running dev build of Basira through its debug pipe (\\.\pipe\basira-dev).
#
# Usage: scripts/dev/send.sh [-H host] '<json>'
#        scripts/dev/send.sh [-H host] <command> [arg...]
#   -H host  ssh destination (default: $BASIRA_WINPC)
#
# The pipe takes one JSON object per line. With a command name and arguments this script sends
# {"cmd": "<command>", "args": ["arg", ...]}; a single argument starting with "{" is sent as it is.
# The commands are defined by the app's debug pipe (dev builds only, started with --dev).
# Examples:
#   scripts/dev/send.sh key-tap RControl
#   scripts/dev/send.sh say "افتح جوجل وابحث عن الطقس"
#   scripts/dev/send.sh '{"cmd":"key","key":"RControl","down":true}'
#
# Exit codes: 0 sent, 2 usage, 3 the pipe is not open (Basira not running with --dev), 255 ssh failed.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

host_arg=""
while (($# > 0)); do
    case "$1" in
        -H)
            [[ $# -ge 2 ]] || die "-H needs a host"
            host_arg="$2"
            shift 2
            ;;
        -h | --help)
            sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        --)
            shift
            break
            ;;
        *) break ;;
    esac
done
(($# > 0)) || die "nothing to send (see --help)"
host="$(resolve_host "$host_arg")"

# Build and check the JSON line on the Mac, so a typo fails here and not inside Basira.
line="$(python3 -c '
import json, sys
args = sys.argv[1:]
if len(args) == 1 and args[0].lstrip().startswith("{"):
    try:
        value = json.loads(args[0])
    except ValueError as e:
        sys.exit("error: not valid JSON: %s" % e)
    if not isinstance(value, dict):
        sys.exit("error: the JSON must be an object")
else:
    value = {"cmd": args[0], "args": args[1:]}
print(json.dumps(value, ensure_ascii=True, separators=(",", ":")))
' "$@")" || exit 2

# ASCII-only JSON (non-ASCII is \u-escaped above), so the line survives any code page.
payload="$(printf '%s' "$line" | base64 | tr -d '\n')"
script="\$line = [Text.Encoding]::ASCII.GetString([Convert]::FromBase64String('$payload'))
\$pipe = New-Object IO.Pipes.NamedPipeClientStream('.', 'basira-dev', [IO.Pipes.PipeDirection]::Out)
try { \$pipe.Connect(3000) } catch { Write-Host 'The debug pipe is not open: is the dev build running (scripts/dev/run.sh)?'; exit 3 }
\$writer = New-Object IO.StreamWriter(\$pipe, (New-Object Text.UTF8Encoding(\$false)))
\$writer.NewLine = \"\`n\"
\$writer.WriteLine(\$line)
\$writer.Flush()
\$writer.Dispose()
Write-Host ('sent ' + \$line)
exit 0"

remote_ps "$host" "$script"
