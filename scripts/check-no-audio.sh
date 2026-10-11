#!/usr/bin/env bash
# Fails when git tracks audio files or anything under evals/private/.
# Recordings of real people never go into the repository (see docs/privacy.md).
# Usage: scripts/check-no-audio.sh   (exit 0 clean, 1 offending files found, 2 not a git checkout)
set -euo pipefail

root="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "check-no-audio: not inside a git checkout" >&2; exit 2; }
cd "$root"

audio_re='\.(wav|mp3|m4a|opus|ogg|flac|webm)$'
found=()
while IFS= read -r -d '' path; do
    lower="$(printf '%s' "$path" | tr '[:upper:]' '[:lower:]')"
    if [[ "$lower" =~ $audio_re || "$path" == evals/private/* ]]; then
        found+=("$path")
    fi
done < <(git ls-files -z --cached)

if ((${#found[@]} > 0)); then
    echo "ERROR: these files must not be in git (audio, or private eval data):" >&2
    printf '  %s\n' "${found[@]}" >&2
    echo "Remove them with: git rm --cached <file>  (keep private data in evals/private/, which is git-ignored)" >&2
    exit 1
fi
echo "OK: no audio files or evals/private/ data tracked by git."
