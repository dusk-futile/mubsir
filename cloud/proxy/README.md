# Mubsir relay

A small Cloudflare Worker on the owner's account. It holds the API keys, gives each install its own
token, checks limits and forwards the app's requests to Claude and the voice provider. No AI runs
here and nothing a user says, reads or types is stored or logged: only counts and costs per device
(PLAN §9).

## Endpoints

| Path | Who calls it | What it does |
| --- | --- | --- |
| `POST /v1/register` | App, first start | Returns `{ device_id, token }`. Only the token's SHA-256 is stored. Optional `{ "invite": "…" }` when invite codes are on |
| `POST /v1/messages` | App (Anthropic SDK) | Same path, body and stream as Anthropic's Messages API. The app sets the SDK's base URL to the relay and uses the device token as its API key. The relay swaps in the real key, forwards the body unchanged, streams the answer back unchanged and bills the device from the usage in the answer |
| `POST /v1/tts` | App | `{ text, voice: "conversation" \| "reading", format?: "mp3" \| "opus", speed?: 0.5–2 }` → audio. The relay picks the provider and voice. Every result is cached in R2, so a sentence is paid for once |
| `POST /v1/stt-token` | App | Short-lived speech-to-text token. Answers `501 not_configured` until Phase 0 picks the provider |
| `GET /update/<file>` | App updater | Velopack update feed from the `mubsir-updates` R2 bucket. Public |
| `GET /admin/usage` | Owner | This month's spend against the budget, today's totals, device counts |
| `POST /admin/pause` | Owner | `{ "paused": true }` stops the relay for everyone; `false` restarts it |
| `POST /admin/devices/<id>/block` (or `/unblock`) | Owner | Switches one device off or on |
| `GET /health` | Anyone | `{ "ok": true }` |

Device endpoints take the token as `x-api-key` (what the Anthropic SDK sends) or `Authorization: Bearer`.
Admin endpoints take `ADMIN_TOKEN` the same way.

### Errors the app must handle

Errors use Anthropic's error shape (`{ "type": "error", "error": { "type", "message" } }`), so the SDK
parses them. Besides Anthropic's own:

| Status | `error.type` | App behaviour |
| --- | --- | --- |
| 402 | `quota_exceeded` | The device's daily abuse guard. Fall back quietly (Windows voice, local features). Never say "limit reached" |
| 402 | `budget_exhausted` | The month's budget is used up. Same quiet fallback |
| 503 | `service_paused` | The owner paused the relay. Same quiet fallback |
| 501 / 503 | `not_configured` | That feature isn't set up on the relay yet (no STT provider, no voice id) |
| 401 / 403 | `authentication_error` / `permission_error` | Register again (401) or stop using the cloud (403: this device was switched off) |

402 is used for limits because the SDK never retries it (it retries 429 and 5xx).

## Settings

In `wrangler.toml` under `[vars]`, commented there. The main ones: `ALLOWED_MODELS` (Haiku 5.5 and
Sonnet 5.5; Opus off), `ALLOWED_TOOL_TYPES` (typed tools such as the computer toolset; tools Anthropic runs and bills apart stay off), `MAX_OUTPUT_TOKENS`, `DEVICE_DAILY_REQUESTS` (500), `DEVICE_DAILY_TTS_BYTES`
(≈ 8 hours of new speech), `MONTHLY_BUDGET_USD` (200, with alerts at 50 % and 80 % and a stop at
100 %), `INVITE_CODES`, `VOICE_CONVERSATION` / `VOICE_READING` (Fish Audio voice ids).

Secrets, set with `npx wrangler secret put <NAME>`, never in the repo:

| Secret | What |
| --- | --- |
| `ANTHROPIC_API_KEY` | Anthropic key. Also set a monthly spend limit in the Anthropic Console |
| `FISH_API_KEY` | Fish Audio key. Also set a spend limit there |
| `ADMIN_TOKEN` | A long random string for `/admin/*` (`openssl rand -base64 32`) |
| `IP_SALT` | Another random string; salts the IP hash used to limit new installs per network |
| `ALERT_WEBHOOK_URL` | Optional. A Slack or Discord webhook that receives budget alerts |

## Deploy (owner, once)

```bash
cd cloud/proxy
npm ci
npx wrangler login
npx wrangler d1 create mubsir-relay          # copy the printed database_id into wrangler.toml
npx wrangler r2 bucket create mubsir-audio
npx wrangler r2 bucket create mubsir-updates
npx wrangler d1 migrations apply mubsir-relay --remote
npx wrangler secret put ANTHROPIC_API_KEY     # then FISH_API_KEY, ADMIN_TOKEN, IP_SALT, ALERT_WEBHOOK_URL
npx wrangler deploy
```

After the week-1 listening test, put the chosen Fish Audio voice ids in `VOICE_CONVERSATION` and
`VOICE_READING` and deploy again.

## Develop

```bash
npm ci
npm test            # 37 tests in the Workers runtime (Miniflare); no network, no keys
npm run typecheck
npm run dev         # local relay on http://localhost:8787
```

The tests mock Anthropic, Fish Audio and the alert webhook, and cover registration, token checks,
forwarding with the server key, refusing tools that are billed apart, byte-for-byte streaming with metering, prices (including Haiku 5.5's
two rate cards and cache writes and reads), the daily limits, the budget stop and alerts, the pause
and block switches, the speech cache and the update feed.
