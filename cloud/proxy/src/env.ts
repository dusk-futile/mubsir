/** Bindings, secrets and settings of the relay. Secrets are set with `wrangler secret put`. */
export interface Env {
  DB: D1Database;
  /** Cache of generated speech, keyed by a hash of provider, voice, format, speed and text. */
  AUDIO: R2Bucket;
  /** Velopack update feed (releases.win.json and packages), served at /update/. */
  UPDATES: R2Bucket;

  // Secrets
  ANTHROPIC_API_KEY: string;
  FISH_API_KEY: string;
  /** Token for /admin/* (the owner's dashboard and switches). */
  ADMIN_TOKEN: string;
  /** Salt for hashing IP addresses in the registration limit; never stored in clear. */
  IP_SALT: string;
  /** Optional Slack/Discord-style webhook that receives budget alerts. */
  ALERT_WEBHOOK_URL?: string;

  // Settings (wrangler.toml [vars]); numbers are strings because vars are strings
  ANTHROPIC_VERSION: string;
  ALLOWED_MODELS: string;
  ALLOWED_BETAS: string;
  ALLOWED_TOOL_TYPES: string;
  MAX_OUTPUT_TOKENS: string;
  MAX_BODY_BYTES: string;
  DEVICE_DAILY_REQUESTS: string;
  DEVICE_DAILY_TTS_BYTES: string;
  MONTHLY_BUDGET_USD: string;
  REGISTRATIONS_PER_IP_PER_DAY: string;
  INVITE_CODES: string;
  FISH_MODEL: string;
  FISH_USD_PER_MILLION_BYTES: string;
  VOICE_CONVERSATION: string;
  VOICE_READING: string;
  STT_PROVIDER: string;
}

/** A device row, as returned by authentication. */
export interface Device {
  id: string;
  status: string;
}

/** Reads a numeric setting; empty or invalid values fall back. */
export function num(value: string | undefined, fallback: number): number {
  if (value === undefined || value.trim() === '') return fallback;
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

/** Reads a comma-separated setting. */
export function list(value: string | undefined): string[] {
  return (value ?? '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
}
