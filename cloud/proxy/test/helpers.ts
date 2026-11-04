import { createExecutionContext, waitOnExecutionContext } from 'cloudflare:test';
import { env } from 'cloudflare:workers';
import worker from '../src/index';

type TestEnv = Cloudflare.Env;

/**
 * The real test env with some settings changed. Overrides are defined as own properties: plain
 * assignment would go through the env object's setters and change the shared env for later tests.
 */
export function withEnv(overrides: Partial<Record<keyof TestEnv, string>>): TestEnv {
  const changed = Object.create(env) as TestEnv;
  for (const [key, value] of Object.entries(overrides)) {
    Object.defineProperty(changed, key, { value, enumerable: true });
  }
  return changed;
}

/** Calls the Worker; `done()` waits for its background work (usage recording, caching). */
export async function call(path: string, init: RequestInit = {}, e: TestEnv = env) {
  const ctx = createExecutionContext();
  const request = new Request(`https://relay.test${path}`, init) as Parameters<typeof worker.fetch>[0];
  const res = await worker.fetch(request, e, ctx);
  return { res, done: () => waitOnExecutionContext(ctx) };
}

let network = 0;

/** Registers a device from its own network and returns its token and id. */
export async function newDevice(e: TestEnv = env): Promise<{ token: string; id: string }> {
  network += 1;
  const { res } = await call(
    '/v1/register',
    { method: 'POST', headers: { 'content-type': 'application/json', 'cf-connecting-ip': `10.1.${network >> 8}.${network & 255}` }, body: '{}' },
    e,
  );
  if (res.status !== 201) throw new Error(`register failed: ${res.status}`);
  const body = await res.json<{ token: string; device_id: string }>();
  return { token: body.token, id: body.device_id };
}

export const jsonResponse = (data: unknown, init: ResponseInit = {}): Response =>
  new Response(JSON.stringify(data), { ...init, headers: { 'content-type': 'application/json', ...(init.headers ?? {}) } });

export async function usageOf(deviceId: string) {
  return env.DB.prepare(
    'SELECT requests, input_tokens, output_tokens, cache_read_tokens, cache_write_tokens, tts_bytes, cost_micro FROM usage_daily WHERE device_id = ?',
  )
    .bind(deviceId)
    .first<Record<string, number>>();
}

export async function monthSpend(): Promise<number> {
  const row = await env.DB.prepare('SELECT COALESCE(SUM(cost_micro), 0) AS c FROM spend_monthly').first<{ c: number }>();
  return row?.c ?? 0;
}
