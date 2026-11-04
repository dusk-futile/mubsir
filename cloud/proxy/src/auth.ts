import type { Device, Env } from './env';
import { apiError, safeEqual, sha256Hex } from './util';

/** The app's Anthropic SDK sends the device token as `x-api-key`; other clients may use Bearer. */
export function tokenFrom(request: Request): string | null {
  const key = request.headers.get('x-api-key');
  if (key) return key.trim();
  const match = request.headers.get('authorization')?.match(/^Bearer\s+(.+)$/i);
  return match?.[1]?.trim() ?? null;
}

export async function isPaused(env: Env): Promise<boolean> {
  const row = await env.DB.prepare("SELECT value FROM settings WHERE key = 'paused'").first<{ value: string }>();
  return row?.value === '1';
}

/** Returns the device, or the error response to send. */
export async function authenticate(request: Request, env: Env): Promise<Device | Response> {
  if (await isPaused(env)) return apiError(503, 'service_paused', 'Mubsir is paused for a moment.');
  const token = tokenFrom(request);
  if (!token || !token.startsWith('mbs_')) return apiError(401, 'authentication_error', 'Missing or invalid device token.');
  const device = await env.DB.prepare('SELECT id, status FROM devices WHERE token_hash = ?')
    .bind(await sha256Hex(token))
    .first<Device>();
  if (!device) return apiError(401, 'authentication_error', 'Unknown device token.');
  if (device.status !== 'active') return apiError(403, 'permission_error', 'This device is switched off.');
  return device;
}

export function isAdmin(request: Request, env: Env): boolean {
  const token = tokenFrom(request);
  return !!token && !!env.ADMIN_TOKEN && safeEqual(token, env.ADMIN_TOKEN);
}
