# Shared helpers for the Mac -> Windows test PC dev loop. Sourced by run.sh, logs.sh and send.sh.
#
# Settings (environment):
#   BASIRA_WINPC     ssh destination of the Windows test PC, e.g. "omar@192.168.1.20" or a Host alias
#                    from ~/.ssh/config. A host given on the command line wins.
#   BASIRA_WIN_REPO  repository folder on the Windows PC (default: C:\dev\basira)
#
# Remote commands run in Windows PowerShell through -EncodedCommand (UTF-16LE, base64), so they
# work whether the PC's OpenSSH default shell is cmd.exe or PowerShell, and no quoting survives
# two shells by accident.

BASIRA_WIN_REPO="${BASIRA_WIN_REPO:-C:\\dev\\basira}"
DEV_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

die() {
    echo "error: $*" >&2
    exit 2
}

# Prints the ssh destination: the argument if given, else $BASIRA_WINPC.
resolve_host() {
    local host="${1:-${BASIRA_WINPC:-}}"
    [[ -n "$host" ]] || die "no Windows PC given: pass it as an argument or set BASIRA_WINPC (e.g. export BASIRA_WINPC=omar@192.168.1.20)"
    printf '%s' "$host"
}

# Quotes a value as a PowerShell single-quoted string literal.
ps_quote() {
    local q="'"
    local s="${1//$q/$q$q}"
    printf "'%s'" "$s"
}

# Runs a PowerShell script on the Windows PC. Usage: remote_ps HOST SCRIPT
# Returns the remote exit code.
remote_ps() {
    local host="$1" script="$2" encoded
    encoded="$(printf '%s' "$script" | iconv -f UTF-8 -t UTF-16LE | base64 | tr -d '\n')"
    ssh -o ConnectTimeout=10 "$host" \
        "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $encoded"
}
