# Security

Mubsir can work the whole PC for someone who can't see it, so security problems matter more than usual.

## Reporting

Please report security problems privately through
[GitHub's private vulnerability reporting](https://github.com/dusk-futile/mubsir/security/advisories/new),
not in a public issue.

Especially welcome: a way to make Mubsir act without the user asking, to skip a confirmation, to send a
password, card number or one-time code anywhere, to read keys, or to run a script.

## How Mubsir protects people

- API keys are stored with Windows DPAPI for the signed-in user only, never in a file in plain text and
  never in the repo or the zip.
- Passwords, card numbers and one-time codes are found and removed on the PC before anything is sent.
- On bank and payment pages the AI is switched off; the person presses Pay themselves.
- Sending needs the person's OK. Paying, deleting and installing need a real key press, which the AI
  can't make.
- Scripts are never run.
- Logs hold timings and error codes only.
