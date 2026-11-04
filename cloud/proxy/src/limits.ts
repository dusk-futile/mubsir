import { num, type Env } from './env';
import { claudeCostMicro, speechCostMicro, type Usage } from './pricing';
import { utcDay, utcMonth } from './util';

/** Counts one request for the device today and returns the new total. */
export async function countRequest(env: Env, deviceId: string): Promise<number> {
  const row = await env.DB.prepare(
    `INSERT INTO usage_daily (device_id, day, requests) VALUES (?1, ?2, 1)
     ON CONFLICT(device_id, day) DO UPDATE SET requests = requests + 1
     RETURNING requests`,
  )
    .bind(deviceId, utcDay())
    .first<{ requests: number }>();
  return row?.requests ?? 1;
}

/** Adds (or, with a negative number, gives back) speech bytes for today; returns the new total. */
export async function addSpeechBytes(env: Env, deviceId: string, bytes: number): Promise<number> {
  const row = await env.DB.prepare(
    `INSERT INTO usage_daily (device_id, day, tts_bytes) VALUES (?1, ?2, ?3)
     ON CONFLICT(device_id, day) DO UPDATE SET tts_bytes = tts_bytes + ?3
     RETURNING tts_bytes`,
  )
    .bind(deviceId, utcDay(), bytes)
    .first<{ tts_bytes: number }>();
  return row?.tts_bytes ?? bytes;
}

const budgetMicro = (env: Env): number => num(env.MONTHLY_BUDGET_USD, 0) * 1_000_000;

export async function monthSpendMicro(env: Env, month: string = utcMonth()): Promise<number> {
  const row = await env.DB.prepare('SELECT cost_micro FROM spend_monthly WHERE month = ?').bind(month).first<{ cost_micro: number }>();
  return row?.cost_micro ?? 0;
}

/** True once this month's spend reaches the budget. A budget of 0 means "no budget set". */
export async function budgetExhausted(env: Env): Promise<boolean> {
  const budget = budgetMicro(env);
  return budget > 0 && (await monthSpendMicro(env)) >= budget;
}

/** Records a Claude call's tokens and cost for the device and the month. */
export async function recordClaude(env: Env, deviceId: string, model: string, usage: Usage): Promise<void> {
  const cost = claudeCostMicro(model, usage);
  const cacheWrite = usage.cache_creation_input_tokens;
  await env.DB.batch([
    env.DB.prepare(
      `INSERT INTO usage_daily (device_id, day, input_tokens, output_tokens, cache_read_tokens, cache_write_tokens, cost_micro)
       VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)
       ON CONFLICT(device_id, day) DO UPDATE SET
         input_tokens = input_tokens + ?3, output_tokens = output_tokens + ?4,
         cache_read_tokens = cache_read_tokens + ?5, cache_write_tokens = cache_write_tokens + ?6,
         cost_micro = cost_micro + ?7`,
    ).bind(deviceId, utcDay(), usage.input_tokens, usage.output_tokens, usage.cache_read_input_tokens, cacheWrite, cost),
    addToMonth(env, cost),
  ]);
  await alertIfNeeded(env);
}

/** Records the cost of newly generated speech (cached speech is free and not recorded). */
export async function recordSpeech(env: Env, deviceId: string, bytes: number): Promise<void> {
  const cost = speechCostMicro(bytes, num(env.FISH_USD_PER_MILLION_BYTES, 15));
  await env.DB.batch([
    env.DB.prepare(
      `INSERT INTO usage_daily (device_id, day, cost_micro) VALUES (?1, ?2, ?3)
       ON CONFLICT(device_id, day) DO UPDATE SET cost_micro = cost_micro + ?3`,
    ).bind(deviceId, utcDay(), cost),
    addToMonth(env, cost),
  ]);
  await alertIfNeeded(env);
}

function addToMonth(env: Env, cost: number): D1PreparedStatement {
  return env.DB.prepare(
    `INSERT INTO spend_monthly (month, cost_micro) VALUES (?1, ?2)
     ON CONFLICT(month) DO UPDATE SET cost_micro = cost_micro + ?2`,
  ).bind(utcMonth(), cost);
}

const THRESHOLDS = [100, 80, 50] as const;

/** Sends one alert per threshold (50 %, 80 %, 100 %) per month to ALERT_WEBHOOK_URL. */
export async function alertIfNeeded(env: Env): Promise<void> {
  const budget = budgetMicro(env);
  if (budget <= 0) return;
  const month = utcMonth();
  const row = await env.DB.prepare('SELECT cost_micro, alerted FROM spend_monthly WHERE month = ?')
    .bind(month)
    .first<{ cost_micro: number; alerted: number }>();
  if (!row) return;
  const percent = (row.cost_micro / budget) * 100;
  const level = THRESHOLDS.find((t) => percent >= t) ?? 0;
  if (level <= row.alerted) return;
  // Claim the level in one statement, so two requests finishing together send one alert.
  const claim = await env.DB.prepare('UPDATE spend_monthly SET alerted = ?1 WHERE month = ?2 AND alerted < ?1')
    .bind(level, month)
    .run();
  if (!claim.meta.changes || !env.ALERT_WEBHOOK_URL) return;
  const spent = (row.cost_micro / 1_000_000).toFixed(2);
  const total = (budget / 1_000_000).toFixed(2);
  const text =
    `Mubsir has used ${level}% of this month's $${total} budget ($${spent} spent).` +
    (level >= 100 ? ' New paid requests are stopped until next month or until you raise the budget.' : '');
  try {
    await fetch(env.ALERT_WEBHOOK_URL, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ text, content: text }),
    });
  } catch {
    // An alert that fails to send must never break a user's request.
  }
}
