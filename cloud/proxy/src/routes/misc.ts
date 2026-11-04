import { isAdmin } from '../auth';
import { num, type Device, type Env } from '../env';
import { monthSpendMicro } from '../limits';
import { apiError, utcDay, utcMonth } from '../util';

/**
 * POST /v1/stt-token: a short-lived token for the speech-to-text websocket. The provider is chosen in
 * Phase 0 (Soniox, ElevenLabs Scribe, Deepgram or the current Gemini path); until then this answers
 * 501 and the app keeps its current path.
 */
export async function sttToken(_request: Request, env: Env, _ctx: ExecutionContext, _device: Device): Promise<Response> {
  return apiError(501, 'not_configured', `No speech-to-text provider is set on the relay yet${env.STT_PROVIDER ? ` (${env.STT_PROVIDER} is not supported)` : ''}.`);
}

const TYPES: Record<string, string> = { json: 'application/json', nupkg: 'application/octet-stream', exe: 'application/octet-stream', zip: 'application/zip' };

/** GET /update/<file>: the Velopack update feed, served from R2. Public, like the GitHub releases. */
export async function update(url: URL, env: Env): Promise<Response> {
  const key = decodeURIComponent(url.pathname.slice('/update/'.length));
  if (!key || key.includes('..') || key.startsWith('/')) return apiError(404, 'not_found_error', 'No such file.');
  const file = await env.UPDATES.get(key);
  if (!file) return apiError(404, 'not_found_error', 'No such file.');
  const ext = key.split('.').pop()?.toLowerCase() ?? '';
  return new Response(file.body, {
    headers: { 'content-type': TYPES[ext] ?? 'application/octet-stream', 'cache-control': 'public, max-age=300', etag: file.httpEtag },
  });
}

/**
 * The owner's switches, behind ADMIN_TOKEN:
 *   GET  /admin/usage                     this month's spend, today's totals, device counts
 *   POST /admin/pause   { "paused": true } stop (or restart) the relay for everyone
 *   POST /admin/devices/<id>/block|unblock switch one device off or on
 */
export async function admin(request: Request, url: URL, env: Env): Promise<Response> {
  if (!isAdmin(request, env)) return apiError(401, 'authentication_error', 'Admin token required.');

  if (request.method === 'GET' && url.pathname === '/admin/usage') {
    const today = await env.DB.prepare(
      'SELECT COALESCE(SUM(requests), 0) AS requests, COALESCE(SUM(tts_bytes), 0) AS tts_bytes, COALESCE(SUM(cost_micro), 0) AS cost_micro, COUNT(*) AS active_devices FROM usage_daily WHERE day = ?',
    )
      .bind(utcDay())
      .first<{ requests: number; tts_bytes: number; cost_micro: number; active_devices: number }>();
    const devices = await env.DB.prepare(
      "SELECT COUNT(*) AS total, COALESCE(SUM(status = 'blocked'), 0) AS blocked FROM devices",
    ).first<{ total: number; blocked: number }>();
    const month = await env.DB.prepare('SELECT alerted FROM spend_monthly WHERE month = ?').bind(utcMonth()).first<{ alerted: number }>();
    const paused = await env.DB.prepare("SELECT value FROM settings WHERE key = 'paused'").first<{ value: string }>();
    return Response.json({
      month: utcMonth(),
      spend_usd: (await monthSpendMicro(env)) / 1_000_000,
      budget_usd: num(env.MONTHLY_BUDGET_USD, 0),
      alerted_percent: month?.alerted ?? 0,
      paused: paused?.value === '1',
      today: {
        requests: today?.requests ?? 0,
        speech_bytes: today?.tts_bytes ?? 0,
        cost_usd: (today?.cost_micro ?? 0) / 1_000_000,
        active_devices: today?.active_devices ?? 0,
      },
      devices: { total: devices?.total ?? 0, blocked: devices?.blocked ?? 0 },
    });
  }

  if (request.method === 'POST' && url.pathname === '/admin/pause') {
    let paused = false;
    try {
      paused = ((await request.json()) as { paused?: unknown })?.paused === true;
    } catch {
      return apiError(400, 'invalid_request_error', 'Send { "paused": true } or { "paused": false }.');
    }
    await env.DB.prepare("INSERT INTO settings (key, value) VALUES ('paused', ?1) ON CONFLICT(key) DO UPDATE SET value = ?1")
      .bind(paused ? '1' : '0')
      .run();
    return Response.json({ paused });
  }

  const match = url.pathname.match(/^\/admin\/devices\/([0-9a-f]{32})\/(block|unblock)$/);
  if (request.method === 'POST' && match) {
    const [, id, action] = match;
    const result = await env.DB.prepare('UPDATE devices SET status = ?1 WHERE id = ?2')
      .bind(action === 'block' ? 'blocked' : 'active', id)
      .run();
    if (!result.meta.changes) return apiError(404, 'not_found_error', 'No such device.');
    return Response.json({ device_id: id, status: action === 'block' ? 'blocked' : 'active' });
  }

  return apiError(404, 'not_found_error', 'No such admin endpoint.');
}
