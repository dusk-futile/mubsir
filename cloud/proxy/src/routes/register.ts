import { isPaused } from '../auth';
import { list, num, type Env } from '../env';
import { apiError, base64Url, randomBytes, safeEqual, sha256Hex, toHex, utcDay } from '../util';

/**
 * POST /v1/register: a new install gets its own random token. No account, no personal data.
 * Body (optional): { "app_version": "2.0.0", "invite": "code" }. The token is returned once; only
 * its SHA-256 is stored, so a database leak cannot be used to call the relay.
 */
export async function register(request: Request, env: Env): Promise<Response> {
  if (await isPaused(env)) return apiError(503, 'service_paused', 'Mubsir is paused for a moment.');

  let body: { app_version?: unknown; invite?: unknown } = {};
  if (request.headers.get('content-length') !== '0') {
    try {
      body = (await request.json()) ?? {};
    } catch {
      body = {};
    }
  }

  const invites = list(env.INVITE_CODES);
  if (invites.length > 0) {
    const invite = typeof body.invite === 'string' ? body.invite.trim() : '';
    if (!invites.some((code) => safeEqual(code, invite))) {
      return apiError(403, 'permission_error', 'This copy of Mubsir needs an invite code.');
    }
  }

  const ip = request.headers.get('cf-connecting-ip') ?? 'unknown';
  const ipHash = await sha256Hex(`${env.IP_SALT}|${ip}`);
  const tries = await env.DB.prepare(
    `INSERT INTO registrations (ip_hash, day, count) VALUES (?1, ?2, 1)
     ON CONFLICT(ip_hash, day) DO UPDATE SET count = count + 1
     RETURNING count`,
  )
    .bind(ipHash, utcDay())
    .first<{ count: number }>();
  if ((tries?.count ?? 1) > num(env.REGISTRATIONS_PER_IP_PER_DAY, 5)) {
    return apiError(429, 'rate_limit_error', 'Too many new installs from this network today.', { 'retry-after': '3600' });
  }

  const deviceId = toHex(randomBytes(16));
  const token = `mbs_${base64Url(randomBytes(32))}`;
  const appVersion = typeof body.app_version === 'string' ? body.app_version.slice(0, 32) : null;
  await env.DB.prepare('INSERT INTO devices (id, token_hash, status, created_at, app_version) VALUES (?1, ?2, ?3, ?4, ?5)')
    .bind(deviceId, await sha256Hex(token), 'active', Date.now(), appVersion)
    .run();

  return Response.json({ device_id: deviceId, token }, { status: 201 });
}
