# Dev loop: Mac to Windows test PC

Code is written and unit-tested on the Mac (`dotnet test tests/Basira.Core.Tests`). The Windows test PC builds and runs the app. These scripts drive it over SSH.

## Once, on the Windows PC (as administrator)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\setup-dev-pc.ps1 -AuthorizedKey 'ssh-ed25519 AAAA... you@mac'
git clone https://github.com/dusk-futile/basira.git C:\dev\basira
```

`setup-dev-pc.ps1` installs or checks the .NET 10 SDK and Git, turns on OpenSSH Server (service, firewall rule, PowerShell as the SSH shell), adds your Mac's public key, and creates the `BasiraDev` scheduled task. It prints the PC's IP address and the `ssh` command to use. To copy the script to the PC before the repository is cloned, use a USB stick or download the raw file from GitHub.

Leave the test user **signed in**: `BasiraDev` runs only on a signed-in desktop. A program started straight from an SSH session cannot reach the desktop, keyboard hooks or audio, which is why the dev build is started through the task.

## Every day, on the Mac

```bash
export BASIRA_WINPC=omar@192.168.1.20      # or a Host alias from ~/.ssh/config
export BASIRA_WIN_REPO='C:\dev\basira'      # optional; this is the default

git push                                    # the PC pulls from GitHub
scripts/dev/run.sh                          # pull, build (Debug), restart BasiraDev
scripts/dev/send.sh key-tap RControl        # feed the debug pipe
scripts/dev/logs.sh -n 3                    # copy the newest logs to scripts/dev/.devlogs/
```

| Script | What it does | Exit codes |
|---|---|---|
| `run.sh [host] [--no-pull] [--no-start]` | `git pull --ff-only`, stops the running dev build, `dotnet build src/Basira.App -c Debug -o artifacts\dev`, `schtasks /run /tn BasiraDev` | 0 ok, 10 pull, 11 build, 12 task, 255 ssh |
| `logs.sh [host] [-n COUNT]` | copies the newest files from `%LOCALAPPDATA%\Basira\logs` with `scp` | 0 ok, 1 copy failed |
| `send.sh [-H host] <cmd> [args...]` or `send.sh '<json>'` | writes one JSON line to `\\.\pipe\basira-dev` | 0 sent, 3 pipe not open |

The dev build always lands in `artifacts\dev\Basira.exe` (git-ignored), so the task never has to guess the output folder.

## Debug pipe

Dev builds (`--dev`) listen on `\\.\pipe\basira-dev` for one JSON object per line. `send.sh cmd a b` sends `{"cmd":"cmd","args":["a","b"]}`; a single argument that starts with `{` is sent as it is. The command names (synthetic key events, typed requests, state dumps) are defined by the app's debug pipe; the examples in `send.sh --help` are the intended shape.

The pipe is reached from the SSH session, which is a different Windows logon session from the desktop. Named pipes are machine-wide, and the pipe's owner is the same user, so this should work; the M0 spike confirms it (docs/spikes.md).

## Troubleshooting

- `ssh: connect ... timed out`: the PC's network is marked Public (the firewall rule covers Private and Domain only), or the PC is asleep.
- `git pull failed`: the PC has local changes, or Git cannot sign in to GitHub. Run `git status` on the PC.
- `Could not start task BasiraDev`: nobody is signed in on the PC, or `setup-dev-pc.ps1` was not run.
