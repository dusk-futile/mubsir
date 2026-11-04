/**
 * Errors use Anthropic's error shape, so the app's Anthropic SDK parses relay errors the same way as
 * API errors. Relay-only types the app must handle: `quota_exceeded` and `budget_exhausted` (402,
 * never retried: fall back quietly, no "limit reached" message), `service_paused` (503),
 * `not_configured` (501/503).
 */
export function apiError(status: number, type: string, message: string, headers?: HeadersInit): Response {
  return Response.json({ type: 'error', error: { type, message } }, { status, headers });
}

export const utcDay = (d: Date = new Date()): string => d.toISOString().slice(0, 10);
export const utcMonth = (d: Date = new Date()): string => d.toISOString().slice(0, 7);

const encoder = new TextEncoder();

export const utf8Length = (text: string): number => encoder.encode(text).byteLength;

export function toHex(bytes: Uint8Array): string {
  let out = '';
  for (const b of bytes) out += b.toString(16).padStart(2, '0');
  return out;
}

export async function sha256Hex(text: string): Promise<string> {
  return toHex(new Uint8Array(await crypto.subtle.digest('SHA-256', encoder.encode(text))));
}

export function randomBytes(length: number): Uint8Array {
  const bytes = new Uint8Array(length);
  crypto.getRandomValues(bytes);
  return bytes;
}

export function base64Url(bytes: Uint8Array): string {
  let binary = '';
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Constant-time comparison for secrets. */
export function safeEqual(a: string, b: string): boolean {
  const x = encoder.encode(a);
  const y = encoder.encode(b);
  if (x.byteLength !== y.byteLength) return false;
  return crypto.subtle.timingSafeEqual(x, y);
}
